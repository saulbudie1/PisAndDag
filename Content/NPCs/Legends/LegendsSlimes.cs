using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// One world scan a tick, shared by every slime alive.
    ///
    /// The King's presence provokes the whole species at once, and every passive slime
    /// needs to know. Asking NPC.AnyNPCs per slime per tick would be two hundred array
    /// walks a frame for one boolean that is the same for all of them.
    /// </summary>
    public class SlimeCourt : ModSystem
    {
        public static bool KingIsHere;

        public override void PostUpdateNPCs()
        {
            KingIsHere = NPC.AnyNPCs(NPCID.KingSlime);
        }

        public override void ClearWorld()
        {
            KingIsHere = false;
        }
    }

    /// <summary>
    /// Slimes stop being enemies once the orb is broken.
    ///
    /// Their AI is left completely alone — they still hop about exactly as they always
    /// did, and vanilla's is better at being a slime than anything written here would be.
    /// The only thing that changes is that an untouched slime is worth no damage, so it
    /// can be walked through, and town NPCs have no reason to swing at it.
    ///
    /// A slime becomes dangerous the moment it has been hurt, which is read straight off
    /// its own health rather than tracked with a flag. There is nothing to sync, nothing
    /// to get out of step, and no way for the state to disagree with what the player can
    /// see on the slime.
    /// </summary>
    public class LegendsSlime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// lateInstantiation is required here: aiStyle is written by the NPC's own
        /// SetDefaults, and the early pass runs before that, where every slime would still
        /// read as aiStyle 0 and be skipped.
        /// </summary>
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return lateInstantiation
                && entity.aiStyle == NPCAIStyleID.Slime
                && entity.type != NPCID.KingSlime;
        }

        // ---- pacifism -------------------------------------------------------

        /// <summary>
        /// Contact damage as the slime was built with.
        ///
        /// Captured on the first update rather than in SetDefaults, because difficulty
        /// scaling runs in between and the pre-scaling number is not the one to restore.
        /// </summary>
        private int baseDamage = -1;

        /// <summary>
        /// Whether this slime is willing to hurt anybody.
        ///
        /// Any wound at all is enough, and the King's arrival wakes the whole species
        /// regardless. Heralds are exempt for the length of the ritual.
        /// </summary>
        private bool Roused(NPC npc)
        {
            return !Herald && (npc.life < npc.lifeMax || SlimeCourt.KingIsHere);
        }

        // ---- herald ---------------------------------------------------------

        /// <summary>Called up by the King's arrival. Harmless, immortal, and on rails.</summary>
        public bool Herald;

        /// <summary>Where this one is headed. Y is the ground line of the altar.</summary>
        public Vector2 HeraldGoal;

        /// <summary>Per-slime jitter, so twenty of them do not move as one animation.</summary>
        public float HeraldHopScale = 1f;
        public float HeraldSpeedScale = 1f;

        /// <summary>Ticks before this one sets off.</summary>
        public int HeraldDelay;

        /// <summary>Beats spent sitting on the ground between hops.</summary>
        public int HeraldRest;

        private int heraldRestTimer;

        private const float HeraldGravity = 0.5f;
        private const float HeraldFall = 16f;
        private const float HeraldHopY = 9f;
        private const float HeraldHopX = 200f;
        private const float HeraldArrived = 40f;

        /// <summary>
        /// How long one hop is in the air, derived rather than picked.
        ///
        /// A hop rises and falls under HeraldGravity, so its airtime is fixed by the two
        /// numbers above. Guessing it separately is how the horizontal speed ends up
        /// mismatched and every hop overshoots the altar.
        /// </summary>
        private static float HopTicks(float rise) => 2f * rise / HeraldGravity;

        // ---- ai -------------------------------------------------------------

        /// <summary>
        /// Vanilla's AI is left running for every slime that is not a herald. The old
        /// version replaced it with a hand-written wander, which is what made passive
        /// slimes feel wrong — they stopped moving like slimes.
        /// </summary>
        public override bool PreAI(NPC npc)
        {
            if (!PADWorld.Legends)
                return true;

            if (baseDamage < 0)
                baseDamage = npc.damage;

            if (Herald)
            {
                HeraldHop(npc);
                return false;
            }

            // Zeroing the damage is what takes a passive slime off the town NPCs' target
            // list as well: they pick from things that can actually hurt somebody, so a
            // slime worth no damage is not worth swinging at.
            npc.damage = Roused(npc) ? baseDamage : 0;

            return true;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            return !PADWorld.Legends || Roused(npc);
        }

        /// <summary>
        /// The summoned hop toward the altar.
        ///
        /// Tile collision is off and the altar's own Y is treated as the ground line, so
        /// the arc is real but the terrain is not. Pathing twenty slimes over actual
        /// ground would strand half of them behind a hill, and the ritual would sit there
        /// waiting for arrivals that were never coming.
        /// </summary>
        private void HeraldHop(NPC npc)
        {
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.noTileCollide = true;
            npc.chaseable = false;

            // UpdateNPC applies gravity of its own AFTER the AI runs, so leaving it on
            // would pull twice per frame and the hop would land in half the airtime the
            // arc below is built around.
            npc.noGravity = true;

            if (HeraldDelay > 0)
            {
                HeraldDelay--;
                npc.velocity.X = 0f;
            }

            npc.velocity.Y += HeraldGravity;

            if (npc.velocity.Y > HeraldFall)
                npc.velocity.Y = HeraldFall;

            npc.spriteDirection = npc.direction;

            // Still in the air.
            if (npc.Bottom.Y < HeraldGoal.Y || npc.velocity.Y < 0f)
                return;

            npc.position.Y = HeraldGoal.Y - npc.height;
            npc.velocity.Y = 0f;
            npc.velocity.X *= 0.6f;

            if (HeraldDelay > 0)
                return;

            // A beat on the ground between hops, different per slime, so the crowd
            // arrives as a scatter instead of in lockstep.
            if (++heraldRestTimer < HeraldRest)
                return;

            heraldRestTimer = 0;

            float toGoal = HeraldGoal.X - npc.Center.X;

            if (Math.Abs(toGoal) < HeraldArrived)
                return;

            npc.direction = Math.Sign(toGoal);

            float rise = HeraldHopY * HeraldHopScale;

            // Never overshoot: the last hop is only as long as the distance left, so they
            // settle onto the altar instead of oscillating across it.
            float reach = Math.Min(Math.Abs(toGoal), HeraldHopX * HeraldSpeedScale);

            npc.velocity = new Vector2(npc.direction * reach / HopTicks(rise), -rise);
        }

        /// <summary>True once this herald has reached its place and stopped.</summary>
        public bool HeraldArrivedAt(NPC npc)
        {
            return Herald && Math.Abs(HeraldGoal.X - npc.Center.X) < HeraldArrived * 2f;
        }
    }

    /// <summary>
    /// The King Slime, in Legends.
    ///
    /// This is vanilla's own AI_015 rebuilt rather than replaced with something new: the
    /// same countdown, the same four-jump cycle ending in a long one, the same teleport
    /// pressure, the same slime-shedding as its health drops, the same shrink. Written out
    /// here so one extra move can be added to the cycle as a first-class member of it.
    ///
    /// Two things the first attempt got wrong, both worth stating because they are easy to
    /// repeat. Gravity is applied by NPC.UpdateNPC AFTER the AI runs, so an AI must never
    /// add its own — doing so also means velocity.Y is never zero when the AI looks at it,
    /// and "am I on the ground" is the test the entire jump cycle hangs off. And the frame
    /// the sprite draws comes from aiAction, so an AI that never sets it leaves the King
    /// stuck on one frame no matter how it moves.
    /// </summary>
    public class LegendsKingSlime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.KingSlime;
        }

        // ---- stats ----------------------------------------------------------

        private const float KingHealth = 1.3f;

        /// <summary>
        /// Contact damage, against vanilla's.
        ///
        /// Back to even, despite the 1.25 this was written with. The number was never the
        /// problem — it lands exactly once, before difficulty scaling, and defDamage is
        /// written after that so nothing compounds it. What changed is the body: at the
        /// sizes below the King is up to twice as wide and twice as tall, so it covers
        /// four times the ground and simply touches the player far more often than
        /// vanilla's ever could. Paying vanilla's rate on that many more contacts is
        /// already a large increase; charging a quarter more on top of it was what made
        /// the fight read as unfair rather than as heavy.
        /// </summary>
        private const float KingDamage = 1f;

        /// <summary>Size multiplier by difficulty, on top of vanilla's own scaling.</summary>
        private const float SizeNormal = 1.25f;
        private const float SizeExpert = 1.6f;
        private const float SizeMaster = 2f;

        private static float SizeForMode()
        {
            // Master reports expert as well, so it has to be tested first or every master
            // world quietly gets the expert number.
            return Main.masterMode ? SizeMaster : Main.expertMode ? SizeExpert : SizeNormal;
        }

        /// <summary>Contact damage as built, captured once the scaling has finished.</summary>
        private int baseDamage = -1;

        /// <summary>
        /// Whether it was off the ground last tick.
        ///
        /// Vanilla has no landing effect at all, so there is nothing to read this off —
        /// but a King Slime this size dropping onto stone should move some of it, so the
        /// one frame it touches down has to be found rather than inferred from a timer.
        /// </summary>
        private bool airborne;

        // ---- vanilla's own numbers -----------------------------------------

        /// <summary>Ticks the countdown gains per grounded frame, before health bonuses.</summary>
        private const float WindupRate = 2f;

        /// <summary>How long before the jump the squash tell starts.</summary>
        private const float SquashLead = -30f;

        /// <summary>Rest after a normal hop and after the long one.</summary>
        private const float RestShort = -120f;
        private const float RestLong = -200f;

        private const float HopUp = -8f;
        private const float HopSide = 4f;
        private const float LeapUp = -6f;
        private const float LeapSide = 4.5f;
        private const float BoundUp = -13f;
        private const float BoundSide = 3.5f;

        /// <summary>Cap on horizontal drift while airborne, and how fast it is reached.</summary>
        private const float AirSpeed = 3f;
        private const float AirAccel = 0.2f;

        /// <summary>Teleport pressure needed, and how far out of reach counts as pressure.</summary>
        private const float TeleportPressure = 300f;
        private const float TeleportHeadroom = 160f;

        private const float TeleportOutTime = 60f;
        private const float TeleportInTime = 30f;

        /// <summary>Beyond this the King gives up and leaves.</summary>
        private const float LeashRange = 3000f;

        /// <summary>Fraction of max health lost between litters of slimes.</summary>
        private const float SheddingStep = 0.05f;

        // ---- the added move -------------------------------------------------

        /// <summary>
        /// Jumps between specials — counted per jump, not per cycle.
        ///
        /// A cycle is four jumps, so triggering once a cycle put a special nine seconds
        /// apart, and the coin flip below then halved the slams again. Counting jumps
        /// instead is what makes the slam a move you see rather than one you hear about,
        /// and it tightens on its own as the King's wind-up shortens with its health.
        /// </summary>
        private const float SpecialEvery = 3f;

        /// <summary>
        /// Ticks planted and squashed before it launches.
        ///
        /// The move had no wind-up at all, so from the player's side the King simply left
        /// the ground at four times its usual speed with no warning. This is the tell.
        /// </summary>
        private const float SlamCrouch = 40f;

        /// <summary>How much of a slam's landing an ordinary hop is worth.</summary>
        private const float HopLandWeight = 0.35f;

        private const float SlamHeight = 500f;
        private const float SlamHeightJitter = 30f;

        /// <summary>
        /// Ticks to get overhead, from wherever it happens to be standing.
        ///
        /// A duration, not a speed. At a fixed speed the climb took as long as the
        /// distance demanded, so a slam from across the arena read completely differently
        /// from one launched at the player's feet — and neither could be learned. Pinning
        /// the time means the move has one rhythm no matter where it starts.
        /// </summary>
        private const float SlamRiseTime = 90f;

        /// <summary>Held overhead before it comes down.</summary>
        private const float SlamHang = 30f;

        /// <summary>Planted after a miss, before it tries again.</summary>
        private const float SlamRecover = 120f;

        /// <summary>Attempts per slam. Two, then it gives up and moves on.</summary>
        private const int SlamTries = 2;

        private const float SlamAccel = 1.6f;
        private const float SlamMaxFall = 34f;

        /// <summary>
        /// Safety net on the drop, not the way it is meant to end.
        ///
        /// The drop ends the moment it touches ground; this only exists so a slam over a
        /// bottomless pit cannot fall forever with the AI waiting on a landing that is
        /// never coming.
        /// </summary>
        private const float SlamDropLimit = 300f;

        /// <summary>Where the current climb set off from, so it can be timed exactly.</summary>
        private Vector2 slamStart;

        /// <summary>Attempts spent on this slam.</summary>
        private int slamTries;

        private const int GrabMinTicks = 180;
        private const int GrabMaxTicks = 301;

        // ---- ai[1] states ---------------------------------------------------
        //
        // 0..3 are vanilla's jump cycle, 5 and 6 its teleport. The slam takes 10..12,
        // deliberately clear of both, so the cycle counter can never wander into it and
        // the teleport can never fire on top of it.

        private const float StateTeleportOut = 5f;
        private const float StateTeleportIn = 6f;
        private const float StateSlamCrouch = 10f;
        private const float StateSlamRise = 11f;
        private const float StateSlamHang = 12f;
        private const float StateSlamDrop = 13f;
        private const float StateSlamRecover = 14f;

        // ---- ritual ---------------------------------------------------------

        private enum Rite { None, Gather, Approach, Rise, Withdraw, Done }

        private Rite rite;
        private int riteTimer;

        /// <summary>Where the ritual is staged. Bottom-centre, on the ground.</summary>
        private Vector2 altar;

        private const int HeraldCount = 20;

        /// <summary>How many have to make it before the camera commits. Not all twenty —
        /// one stuck straggler should not hold the whole opening hostage.</summary>
        private const int HeraldsNeeded = 15;

        /// <summary>Ceiling on the gathering, whatever the heralds are doing.</summary>
        private const int GatherLimit = 900;

        private const int RitePan = 90;
        private const int RiteRise = 60;

        /// <summary>What the King swells out of. Barely a slime.</summary>
        private const float SeedScale = 0.2f;

        private const float AltarNear = 500f;
        private const float AltarFar = 900f;

        private const float HeraldSpawnNear = 1300f;
        private const float HeraldSpawnStep = 130f;

        /// <summary>How far either side of the altar the crowd is allowed to gather.</summary>
        private const float HeraldSpread = 130f;

        private bool InRite => rite != Rite.None && rite != Rite.Done;

        private bool Hidden => rite == Rite.Gather || rite == Rite.Approach;

        // ---- death ----------------------------------------------------------
        //
        // Beats, in ticks, in order. Written as durations and summed into marks below, so
        // stretching one moves everything after it instead of needing eight numbers
        // re-added by hand.

        private const int DeathPan = 90;
        private const int DeathHold = 120;
        private const int DeathSlash = 120;
        private const int DeathShrink = 60;
        private const int DeathBurst = 60;
        private const int DeathNinja = 60;
        private const int DeathWatch = 120;
        private const int DeathBack = 90;

        private const int SlashAt = DeathPan + DeathHold;
        private const int ShrinkAt = SlashAt + DeathSlash;
        private const int BurstAt = ShrinkAt + DeathShrink;
        private const int NinjaAt = BurstAt + DeathBurst;
        private const int WatchAt = NinjaAt + DeathNinja;
        private const int BackAt = WatchAt + DeathWatch;
        private const int DeathEnd = BackAt + DeathBack;

        /// <summary>
        /// How long the cut is actually on screen, inside its beat.
        ///
        /// Half a second of the two. The rest of the beat is deliberately empty — the
        /// slash lands, and then nothing happens for a second and a half while the King
        /// stands there cut. That silence is the point of the beat; stretching the line
        /// itself across all of it just made a fast motion look slow.
        /// </summary>
        private const int SlashShow = 30;

        /// <summary>
        /// Thickness of the cut's middle layer. The particle builds its bloom and its
        /// core off this one number.
        /// </summary>
        private const float SlashWidth = 26f;

        /// <summary>How many slimes are left standing where the King was.</summary>
        private const int DeathSlimes = 16;

        /// <summary>Latched by CheckDead. Everything else in the AI defers to it.</summary>
        private bool dying;

        private int deathTimer;

        /// <summary>Scale at the moment it starts collapsing, so the shrink has a start.</summary>
        private float deathScale = -1f;

        private bool BodyGone => dying && deathTimer >= BurstAt;
        private bool NinjaGone => dying && deathTimer >= NinjaAt;

        // ---- setup ----------------------------------------------------------

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            // Before vanilla's difficulty scaling, which multiplies whatever it finds here
            // — so these read as "on top of the usual", which is what they mean.
            entity.lifeMax = (int)(entity.lifeMax * KingHealth);
            entity.damage = (int)(entity.damage * KingDamage);
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            // Server-authoritative: it picks the altar and calls up the heralds, and the
            // clients are told where everything ended up by the usual NPC sync.
            if (!PADWorld.Legends || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            npc.TargetClosest(false);

            Player player = Main.player[npc.target];

            if (!player.active || player.dead)
                return;

            altar = PickAltar(player);

            rite = Rite.Gather;
            riteTimer = 0;

            Resize(npc, SeedScale);
            Anchor(npc);

            SummonHeralds(npc);

            npc.netUpdate = true;
        }

        /// <summary>
        /// A patch of ground the stated distance out from the player, on one side or the
        /// other.
        /// </summary>
        private static Vector2 PickAltar(Player player)
        {
            float side = Main.rand.NextBool() ? 1f : -1f;
            float distance = Main.rand.NextFloat(AltarNear, AltarFar);

            float x = player.Center.X + side * distance;

            // Kept well inside the world edge, or a fight near the ocean stages its ritual
            // out of bounds.
            x = MathHelper.Clamp(x, 80f * 16f, (Main.maxTilesX - 80) * 16f);

            return new Vector2(x, GroundAt(x));
        }

        /// <summary>World Y of the first solid tile under a column.</summary>
        private static float GroundAt(float worldX)
        {
            int tileX = (int)(worldX / 16f);
            int from = (int)(Main.worldSurface * 0.35);

            for (int y = from; y < Main.maxTilesY - 10; y++)
            {
                Tile tile = Framing.GetTileSafely(tileX, y);

                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                    return y * 16f;
            }

            return (float)Main.worldSurface * 16f;
        }

        /// <summary>
        /// Nails the King to the altar.
        ///
        /// Written every tick of the ritual, and paired with noGravity. UpdateNPC adds
        /// gravity after the AI has run, so a boss that merely zeroes its velocity is
        /// still moved a little further down every single frame.
        /// </summary>
        private void Anchor(NPC npc)
        {
            npc.velocity = Vector2.Zero;
            npc.noGravity = true;
            npc.position.X = altar.X - npc.width * 0.5f;
            npc.position.Y = altar.Y - npc.height;
        }

        /// <summary>
        /// Net IDs, not types. Everything below BlueSlime is a NEGATIVE id — a variant
        /// that has no NPC slot of its own — so these cannot be handed to NPC.NewNPC. The
        /// slime is spawned as a plain blue one and then told what it really is, which is
        /// exactly what vanilla does when the King sheds them.
        /// </summary>
        private static readonly int[] HeraldKinds =
        {
            NPCID.BlueSlime, NPCID.GreenSlime, NPCID.RedSlime, NPCID.PurpleSlime,
            NPCID.YellowSlime, NPCID.IceSlime, NPCID.JungleSlime, NPCID.MotherSlime,
            NPCID.BlackSlime, NPCID.BabySlime
        };

        private void SummonHeralds(NPC king)
        {
            for (int i = 0; i < HeraldCount; i++)
            {
                // Alternating sides and stepping outward, then jittered, so they come over
                // the horizon in a ragged line rather than as one block.
                float side = i % 2 == 0 ? 1f : -1f;
                float away = HeraldSpawnNear + i / 2 * HeraldSpawnStep
                    + Main.rand.NextFloat(-90f, 90f);

                float x = altar.X + side * away;
                float y = GroundAt(x) - 48f;

                int index = NPC.NewNPC(king.GetSource_FromAI(), (int)x, (int)y, NPCID.BlueSlime);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC slime = Main.npc[index];

                int kind = HeraldKinds[Main.rand.Next(HeraldKinds.Length)];

                if (kind != NPCID.BlueSlime)
                {
                    slime.SetDefaults(kind);
                    slime.position = new Vector2(x, y);
                }

                if (!slime.TryGetGlobalNPC(out LegendsSlime legends))
                    continue;

                legends.Herald = true;

                // Their own spot in the crowd, not one shared point every slime stacks on.
                legends.HeraldGoal = new Vector2(
                    altar.X + Main.rand.NextFloat(-HeraldSpread, HeraldSpread), altar.Y);

                legends.HeraldHopScale = Main.rand.NextFloat(0.75f, 1.35f);
                legends.HeraldSpeedScale = Main.rand.NextFloat(0.7f, 1.3f);
                legends.HeraldDelay = Main.rand.Next(0, 90);
                legends.HeraldRest = Main.rand.Next(0, 14);

                // Slimes come in sizes. Twenty identical ones read as a copy-paste.
                slime.scale *= Main.rand.NextFloat(0.7f, 1.4f);

                slime.netUpdate = true;
            }
        }

        // ---- ai -------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (!PADWorld.Legends)
                return true;

            if (baseDamage < 0)
                baseDamage = npc.damage;

            // The death owns the boss outright, ahead of the ritual and the fight both.
            if (dying)
            {
                RunDeath(npc);
                return false;
            }

            if (InRite)
            {
                RunRite(npc);
                return false;
            }

            KingAI(npc);
            return false;
        }

        /// <summary>
        /// The fight proper.
        ///
        /// Laid out in vanilla's order because the order is load-bearing: the busy states
        /// are resolved first and set a flag, and the grounded jump logic only runs when
        /// nothing else has claimed the frame. That single flag is what guarantees a
        /// teleport and a slam can never both be driving the King on the same tick.
        /// </summary>
        private void KingAI(NPC npc)
        {
            // Scale is squeezed during the teleport, so the shrink and the swell are the
            // same value the rest of the AI already computes.
            float squeeze = 1f;
            bool busy = false;
            bool gone = false;

            npc.aiAction = 0;
            npc.noGravity = false;
            npc.damage = baseDamage;

            if (npc.ai[3] == 0f && npc.life > 0)
                npc.ai[3] = npc.lifeMax;

            npc.TargetClosest();

            Player player = Main.player[npc.target];

            // Out of reach entirely: give up rather than hover at the edge of the world.
            if (player.dead || Vector2.Distance(npc.Center, player.Center) > LeashRange)
            {
                npc.EncourageDespawn(10);
                npc.direction = player.Center.X < npc.Center.X ? 1 : -1;
            }

            // Teleport pressure. It builds while the King cannot see the player or the
            // player is perched well above it, which is precisely the situation where a
            // King Slime would otherwise just bounce impotently below a platform.
            bool reachable = Collision.CanHitLine(npc.Center, 0, 0, player.Center, 0, 0)
                && Math.Abs(npc.Top.Y - player.Bottom.Y) <= TeleportHeadroom;

            if (!reachable)
            {
                npc.ai[2]++;
                npc.localAI[0]++;
            }
            else if (npc.localAI[0] > 0f)
            {
                npc.localAI[0]--;
            }

            // Only from a standing start, and never on top of anything else.
            if (!player.dead && npc.ai[2] >= TeleportPressure && npc.ai[1] < StateTeleportOut
                && npc.velocity.Y == 0f)
            {
                StartTeleport(npc, player);
            }

            if (npc.ai[1] == StateTeleportOut)
            {
                busy = true;
                npc.aiAction = 1;
                npc.ai[0]++;

                // Shrinks away to half over the second it takes to vanish.
                squeeze = 0.5f + MathHelper.Clamp((TeleportOutTime - npc.ai[0]) / TeleportOutTime, 0f, 1f) * 0.5f;

                gone = npc.ai[0] >= TeleportOutTime;

                if (gone)
                {
                    npc.Bottom = new Vector2(npc.localAI[1], npc.localAI[2]);
                    npc.ai[1] = StateTeleportIn;
                    npc.ai[0] = 0f;
                    npc.netUpdate = true;
                }
                else
                {
                    TeleportDust(npc, 0.5f);
                }
            }
            else if (npc.ai[1] == StateTeleportIn)
            {
                busy = true;
                npc.ai[0]++;

                squeeze = 0.5f + MathHelper.Clamp(npc.ai[0] / TeleportInTime, 0f, 1f) * 0.5f;

                TeleportDust(npc, 2f);

                if (npc.ai[0] >= TeleportInTime)
                {
                    npc.ai[1] = 0f;
                    npc.ai[0] = 0f;
                    npc.netUpdate = true;
                    npc.TargetClosest();
                }
            }
            else if (npc.ai[1] >= StateSlamCrouch)
            {
                busy = true;
                RunSlam(npc, player);
            }

            // Vanilla ties both to the same flag: while it is not on screen it cannot be
            // hit, and hide is what takes the crown and the ninja away with the body.
            npc.hide = gone;
            npc.dontTakeDamage = gone;

            if (npc.velocity.Y == 0f && !busy)
            {
                Grounded(npc, player);
            }
            else
            {
                if (!busy && npc.target < 255)
                    Airborne(npc);

                if (npc.velocity.Y != 0f)
                    airborne = true;
            }

            if (!Main.dedServ)
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                    npc.velocity.X, npc.velocity.Y, 255, new Color(0, 80, 255, 80), npc.scale * 1.2f);

                dust.noGravity = true;
                dust.velocity *= 0.5f;
            }

            if (npc.life <= 0)
                return;

            ApplySize(npc, squeeze);
            Shed(npc);
        }

        /// <summary>
        /// On the ground: bleed off speed, wind up, and jump.
        ///
        /// The countdown climbs faster the more hurt the King is, which is the whole
        /// difficulty curve of the vanilla fight — it never gains a new move, it just
        /// stops giving you time between the ones it has.
        /// </summary>
        private void Grounded(NPC npc, Player player)
        {
            // A fraction of the slam's weight. Using the same burst for both made an
            // ordinary hop look like the big one, which cost the big one its impact.
            if (airborne)
            {
                airborne = false;
                Land(npc, HopLandWeight);
            }

            npc.velocity.X *= 0.8f;

            if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f)
                npc.velocity.X = 0f;

            npc.ai[0] += WindupRate;

            float ratio = npc.life / (float)npc.lifeMax;

            if (ratio < 0.8f) npc.ai[0] += 1f;
            if (ratio < 0.6f) npc.ai[0] += 1f;
            if (ratio < 0.4f) npc.ai[0] += 2f;
            if (ratio < 0.2f) npc.ai[0] += 3f;
            if (ratio < 0.1f) npc.ai[0] += 4f;

            // Still winding up. The squash frame comes in for the last half second, which
            // is the only warning a jump gets and the reason the fight is readable at all.
            if (npc.ai[0] < 0f)
            {
                if (npc.ai[0] >= SquashLead)
                    npc.aiAction = 1;

                return;
            }

            npc.netUpdate = true;
            npc.TargetClosest();

            // A slam takes the place of a jump rather than interrupting one, so the two
            // can never be in flight together.
            npc.localAI[3]++;

            if (npc.localAI[3] >= SpecialEvery)
            {
                npc.localAI[3] = 0f;

                // A coin. Both are the cycle's special, so which one turns up is not
                // something the player can count out — the teleport keeps its own
                // pressure trigger as well, for the case where it genuinely cannot
                // reach them and the fight would otherwise stall.
                if (Main.rand.NextBool())
                    StartSlam(npc, player);
                else
                    StartTeleport(npc, player);

                return;
            }

            if (npc.ai[1] == 3f)
            {
                // The long one, and the end of a cycle.
                npc.velocity.Y = BoundUp;
                npc.velocity.X += BoundSide * npc.direction;
                npc.ai[0] = RestLong;
                npc.ai[1] = 0f;
            }
            else if (npc.ai[1] == 2f)
            {
                npc.velocity.Y = LeapUp;
                npc.velocity.X += LeapSide * npc.direction;
                npc.ai[0] = RestShort;
                npc.ai[1]++;
            }
            else
            {
                npc.velocity.Y = HopUp;
                npc.velocity.X += HopSide * npc.direction;
                npc.ai[0] = RestShort;
                npc.ai[1]++;
            }
        }

        /// <summary>
        /// Mid-air steering. Gentle, and capped — a King Slime commits to its arc, and
        /// being able to walk out from under one is the point.
        /// </summary>
        private static void Airborne(NPC npc)
        {
            bool room = (npc.direction == 1 && npc.velocity.X < AirSpeed)
                || (npc.direction == -1 && npc.velocity.X > -AirSpeed);

            if (!room)
                return;

            bool pushing = (npc.direction == -1 && npc.velocity.X < 0.1f)
                || (npc.direction == 1 && npc.velocity.X > -0.1f);

            if (pushing)
                npc.velocity.X += AirAccel * npc.direction;
            else
                npc.velocity.X *= 0.93f;
        }

        private static void TeleportDust(NPC npc, float speed)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(npc.position + Vector2.UnitX * -20f,
                    npc.width + 40, npc.height, DustID.t_Slime, npc.velocity.X, npc.velocity.Y,
                    150, new Color(78, 136, 255, 80), 2f);

                dust.noGravity = true;
                dust.velocity *= speed;
            }
        }

        /// <summary>
        /// Sheds a litter of slimes for every slice of health it loses. Driven off ai[3],
        /// which holds the health at the last litter, so damage that arrives in one lump
        /// pays out once rather than for every tick it took to land.
        /// </summary>
        private static void Shed(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int step = (int)(npc.lifeMax * SheddingStep);

            if (npc.life + step >= npc.ai[3])
                return;

            npc.ai[3] = npc.life;

            int litter = Main.rand.Next(1, 4);

            for (int i = 0; i < litter; i++)
            {
                int x = (int)(npc.position.X + Main.rand.Next(Math.Max(1, npc.width - 32)));
                int y = (int)(npc.position.Y + Main.rand.Next(Math.Max(1, npc.height - 32)));

                int kind = Main.expertMode && Main.rand.NextBool(4)
                    ? NPCID.SlimeSpiked
                    : NPCID.BlueSlime;

                int index = NPC.NewNPC(npc.GetSource_FromAI(), x, y, kind);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC child = Main.npc[index];

                child.SetDefaults(kind);
                child.velocity = new Vector2(Main.rand.Next(-15, 16) * 0.1f,
                    Main.rand.Next(-30, 1) * 0.1f);

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: index);
            }
        }

        // ---- teleport spot --------------------------------------------------

        /// <summary>
        /// Somewhere solid near the player with headroom, no lava, and a clear line to
        /// them. Searched as a ring so the King lands at arm's length rather than inside
        /// the player; the tighter ring is the fallback for cramped ground.
        /// </summary>
        private static Vector2 FindTeleportSpot(NPC npc, Player player, bool cornered)
        {
            // Out of patience: it comes straight down on top of them.
            if (cornered)
                return player.Bottom;

            Point centre = player.Center.ToTileCoordinates();

            if (RingSearch(npc, player, centre, 10, 7, out Vector2 wide))
                return wide;

            if (RingSearch(npc, player, centre, 6, 2, out Vector2 close))
                return close;

            return player.Bottom;
        }

        private static bool RingSearch(NPC npc, Player player, Point centre, int outer, int inner,
            out Vector2 spot)
        {
            spot = Vector2.Zero;

            int found = 0;

            for (int x = centre.X - outer; x <= centre.X + outer; x++)
            {
                for (int y = centre.Y - outer; y <= centre.Y + outer; y++)
                {
                    // The hole in the middle is what makes this a ring.
                    if (Math.Abs(x - centre.X) < inner && Math.Abs(y - centre.Y) < inner)
                        continue;

                    if (!Standable(npc, player, x, y))
                        continue;

                    // Reservoir sampling: one pass, no list, and every candidate equally
                    // likely without knowing up front how many there will be.
                    found++;

                    if (Main.rand.Next(found) == 0)
                        spot = new Vector2(x * 16 + 8, y * 16);
                }
            }

            return found > 0;
        }

        private static bool Standable(NPC npc, Player player, int x, int y)
        {
            if (x < 10 || y < 10 || x >= Main.maxTilesX - 10 || y >= Main.maxTilesY - 10)
                return false;

            Tile tile = Framing.GetTileSafely(x, y);

            if (!tile.HasUnactuatedTile)
                return false;

            bool footing = (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                || TileID.Sets.Platforms[tile.TileType];

            if (!footing || tile.LiquidType == LiquidID.Lava)
                return false;

            // Something has to be able to stand here, and it has to be able to see out.
            if (WorldGen.SolidTile(x, y - 1))
                return false;

            Vector2 stand = new Vector2(x * 16 + 8, y * 16 - npc.height / 2);

            return Collision.CanHitLine(stand, 0, 0, player.Center, 0, 0);
        }

        // ---- size -----------------------------------------------------------

        private void ApplySize(NPC npc, float squeeze)
        {
            float ratio = MathHelper.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);
            float wanted = (ratio * 0.5f + 0.75f) * squeeze * SizeForMode();

            if (Math.Abs(wanted - npc.scale) < 0.0001f)
                return;

            Resize(npc, wanted);
        }

        /// <summary>
        /// Anchored bottom-centre, the way the vanilla King Slime resizes itself. Growing
        /// from the top-left corner instead would drive it through the floor.
        /// </summary>
        private static void Resize(NPC npc, float scale)
        {
            NPC sample = ContentSamples.NpcsByNetId[NPCID.KingSlime];

            float bottomX = npc.position.X + npc.width * 0.5f;
            float bottomY = npc.position.Y + npc.height;

            npc.scale = scale;
            npc.width = Math.Max(2, (int)(sample.width * scale));
            npc.height = Math.Max(2, (int)(sample.height * scale));

            npc.position.X = bottomX - npc.width * 0.5f;
            npc.position.Y = bottomY - npc.height;
        }

        // ---- the slam -------------------------------------------------------

        /// <summary>
        /// Vanishes and picks somewhere to come back.
        ///
        /// Reached two ways: the pressure counter, when the King cannot get at the player
        /// at all, and the coin flip at the end of a jump cycle. Both land here so there
        /// is one place that knows how a teleport starts.
        /// </summary>
        private void StartTeleport(NPC npc, Player player)
        {
            npc.ai[2] = 0f;
            npc.ai[0] = 0f;
            npc.ai[1] = StateTeleportOut;

            // Long enough out of reach and it stops being polite about where it lands.
            bool cornered = npc.localAI[0] >= 360f
                || Vector2.Distance(npc.Center, player.Center) > 2000f;

            Vector2 spot = FindTeleportSpot(npc, player, cornered);

            npc.localAI[1] = spot.X;
            npc.localAI[2] = spot.Y;
            npc.netUpdate = true;
        }

        private void StartSlam(NPC npc, Player player)
        {
            npc.ai[1] = StateSlamCrouch;
            npc.ai[0] = 0f;

            slamTries = 0;
            npc.netUpdate = true;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.6f, Volume = 0.7f }, npc.Center);
        }

        /// <summary>
        /// Opens a climb from wherever the King is standing now.
        ///
        /// Called for the first attempt and again for the retry, so both are timed from
        /// their own starting point and both roll their own height.
        /// </summary>
        private void BeginRise(NPC npc, Player player)
        {
            slamStart = npc.Center;

            // Stored as an OFFSET from the player rather than an absolute height, so the
            // target follows them up a hill or down a hole instead of aiming at wherever
            // they happened to be standing when it launched.
            npc.localAI[1] = -SlamHeight + Main.rand.NextFloat(-SlamHeightJitter, SlamHeightJitter);

            npc.ai[1] = StateSlamRise;
            npc.ai[0] = 0f;
            npc.netUpdate = true;

            if (!Main.dedServ)
                Main.LocalPlayer.PIS().BumpScreenShake(5f, 14);
        }

        /// <summary>
        /// Up over the player's head, hold, and drop.
        ///
        /// The climb and the hold are scripted — gravity off, velocity written by hand —
        /// and only the drop is physics. That split is deliberate: the parts that have to
        /// be readable are on a clock, and the part that has to feel heavy is not.
        ///
        /// A miss buys one more try and no more. Letting it keep dropping until a timer
        /// expired, which is what it used to do, meant a slam could tie up the whole fight
        /// while the player simply walked away from it.
        /// </summary>
        private void RunSlam(NPC npc, Player player)
        {
            npc.ai[0]++;

            if (player.dead || !player.active)
            {
                EndSlam(npc);
                return;
            }

            if (npc.ai[1] == StateSlamCrouch)
            {
                // Planted and squashing. Everything the player needs is here: it has
                // stopped, it is not winding up a hop, and it is about to leave.
                npc.velocity = Vector2.Zero;
                npc.noGravity = true;
                npc.aiAction = 1;

                if (!Main.dedServ && npc.ai[0] % 5f == 0f)
                {
                    Dust dust = Dust.NewDustDirect(
                        new Vector2(npc.position.X, npc.position.Y + npc.height - 10f),
                        npc.width, 14, DustID.t_Slime,
                        Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f),
                        120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1f, 1.6f));

                    dust.noGravity = true;
                }

                if (npc.ai[0] >= SlamCrouch)
                    BeginRise(npc, player);

                return;
            }

            if (npc.ai[1] == StateSlamRise)
            {
                // Terrain is ignored on the way up. A boss that has to find a hole in the
                // ceiling is a boss that never uses this attack indoors.
                npc.noTileCollide = true;
                npc.noGravity = true;
                npc.aiAction = 0;

                float t = Ease(MathHelper.Clamp((npc.ai[0] + 1f) / SlamRiseTime, 0f, 1f));

                Vector2 want = new Vector2(
                    MathHelper.Lerp(slamStart.X, player.Center.X, t),
                    MathHelper.Lerp(slamStart.Y, player.Center.Y + npc.localAI[1], t));

                // Handed to velocity rather than written into position, because UpdateNPC
                // moves by velocity immediately after this. Setting position here would
                // then be moved AGAIN by whatever velocity was left over, and the boss
                // would arrive early and overshoot; this way it lands on the mark on
                // exactly the right tick and still leaves a real velocity behind for the
                // squash animation and the ninja's wobble to read.
                npc.velocity = want - npc.Center;

                if (npc.ai[0] >= SlamRiseTime)
                {
                    npc.ai[1] = StateSlamHang;
                    npc.ai[0] = 0f;
                }

                return;
            }

            if (npc.ai[1] == StateSlamHang)
            {
                npc.noTileCollide = true;
                npc.noGravity = true;
                npc.aiAction = 1;

                // Dead still. The climb already decided where this comes down, and the
                // half second is there to let the player read that decision and leave —
                // a boss that keeps creeping after them during it is answering the
                // question it just asked.
                npc.velocity = Vector2.Zero;

                if (npc.ai[0] >= SlamHang)
                {
                    npc.ai[1] = StateSlamDrop;
                    npc.ai[0] = 0f;
                    npc.velocity = new Vector2(0f, 6f);
                    npc.noTileCollide = false;

                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item167 with { Pitch = 0.5f, Volume = 0.6f }, npc.Center);
                }

                return;
            }

            if (npc.ai[1] == StateSlamRecover)
            {
                // Sat on the ground where it missed. Long enough to punish the whiff, and
                // the squash at the end is the tell that a second one is coming.
                npc.noTileCollide = false;
                npc.noGravity = false;
                npc.velocity.X *= 0.8f;
                npc.aiAction = npc.ai[0] >= SlamRecover - 25f ? 1 : 0;

                if (npc.ai[0] >= SlamRecover)
                    BeginRise(npc, player);

                return;
            }

            // ---- the drop ----

            npc.noGravity = true;
            npc.aiAction = 0;

            // Read BEFORE the acceleration below is applied.
            //
            // Collision zeroes velocity.Y on impact, but the very next thing this method
            // used to do was add SlamAccel to it — so by the time the landing test ran
            // there was no zero left to find, and the King sat grinding into the floor
            // until the drop limit timed out. That is the hovering.
            //
            // The position test is the one that actually carries it: oldPosition is
            // written by UpdateCollision, so a body that is still falling has moved and a
            // body that has landed has not, whatever anything has done to its velocity.
            bool landed = npc.ai[0] > 4f
                && (npc.velocity.Y == 0f || npc.position.Y - npc.oldPosition.Y < 0.5f);

            npc.velocity.X *= 0.96f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + SlamAccel, SlamMaxFall);

            // Air being shoved out from under it. Thrown hard sideways off the bottom
            // edge while the body falls, which is what sells the weight — the same trick
            // the Queen's slam uses, and the reason hers reads as heavy rather than fast.
            if (!Main.dedServ)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 at = npc.Bottom
                        - new Vector2(Main.rand.NextFloatDirection() * (npc.width * 0.4f),
                            Main.rand.Next(8));

                    Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, Vector2.Zero, 40, default, 1.4f);

                    dust.noGravity = true;
                    dust.velocity.Y = npc.velocity.Y * 0.9f;
                    dust.velocity.X = (Main.rand.NextBool() ? -10f : 10f)
                        + Main.rand.NextFloatDirection() * 3f;
                }
            }

            // Caught. That ends the whole move regardless of which attempt it was.
            if (npc.Hitbox.Intersects(player.Hitbox))
            {
                Seize(npc, player);
                Land(npc, 1f);
                EndSlam(npc);
                return;
            }

            // The landing is the exit, not a timer — the drop limit is only there so a
            // slam over open air cannot wait forever for ground that is not coming.
            if (!landed && npc.ai[0] < SlamDropLimit)
                return;

            Land(npc, 1f);

            slamTries++;

            if (slamTries >= SlamTries)
            {
                EndSlam(npc);
                return;
            }

            npc.ai[1] = StateSlamRecover;
            npc.ai[0] = 0f;
            npc.netUpdate = true;
        }

        private void EndSlam(NPC npc)
        {
            npc.ai[1] = 0f;

            // The slam has already played its own landing; without this the next grounded
            // tick would play a second, smaller one on top of it.
            airborne = false;

            // Straight back into the jump cycle at a normal rest, so the slam costs the
            // same beat a jump would have.
            npc.ai[0] = RestShort;

            npc.noTileCollide = false;
            npc.noGravity = false;
            npc.netUpdate = true;
        }

        /// <summary>
        /// The impact.
        ///
        /// Three layers, because one is never enough to read as an impact: a low sheet of
        /// smoke thrown up and out, slime shaken off the body, and the ground itself
        /// coming up in pieces. The last one is what ties the hit to the floor rather
        /// than leaving it looking like an effect played on top of the scene.
        /// </summary>
        /// <param name="weight">Scales the whole thing. A slam is worth far more than the
        /// end of an ordinary hop, and using the same burst for both made every landing
        /// look like the big one.</param>
        private static void Land(NPC npc, float weight)
        {
            if (Main.dedServ)
                return;

            int smoke = (int)(20 * weight);

            for (int i = 0; i < smoke; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    npc.Bottom - new Vector2(npc.width / 2f, 30f), npc.width, 30,
                    DustID.Smoke, 0f, 0f, 40, default, 1.5f * weight);

                dust.noGravity = true;

                // Straight up and spread wide: a low sheet rather than a ball.
                dust.velocity.Y = -5f + Main.rand.NextFloat() * -3f;
                dust.velocity.X *= 7f;
            }

            int slime = (int)(24 * weight);

            for (int i = 0; i < slime; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    new Vector2(npc.position.X, npc.position.Y + npc.height - 8f),
                    npc.width, 12, DustID.t_Slime,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f),
                    120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1.2f, 2f) * weight);

                dust.noGravity = true;
                dust.velocity *= 1.3f;
            }

            GroundDust(npc, weight);

            SoundEngine.PlaySound(SoundID.Item167 with { Volume = MathHelper.Clamp(weight, 0.3f, 1f) },
                npc.Center);

            Main.LocalPlayer.PIS().BumpScreenShake(10f * weight, (int)(24 * weight));
        }

        /// <summary>
        /// Kicks the floor up.
        ///
        /// The dust type is whatever the tile underneath would throw if it were mined, so
        /// landing on stone throws stone and landing on sand throws sand — asked for by
        /// tile rather than guessed, which also means it is right for modded ground. The
        /// velocity is then overwritten to send it upward, since the mining dust it
        /// returns is built to fall out of a block, not to be knocked out of one.
        /// </summary>
        private static void GroundDust(NPC npc, float weight)
        {
            int row = (int)((npc.position.Y + npc.height + 4f) / 16f);
            int from = (int)(npc.position.X / 16f);
            int to = (int)((npc.position.X + npc.width) / 16f);

            int each = Math.Max(1, (int)(3 * weight));

            for (int x = from; x <= to; x++)
            {
                Tile tile = Framing.GetTileSafely(x, row);

                if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType])
                    continue;

                for (int i = 0; i < each; i++)
                {
                    int index = WorldGen.KillTile_MakeTileDust(x, row, tile);

                    // 6000 is the "no dust for this tile" answer, and it is also one past
                    // the end of the array.
                    if (index < 0 || index >= Main.maxDust)
                        continue;

                    Dust dust = Main.dust[index];

                    // Gravity left on, so it arcs and falls back rather than drifting off.
                    dust.noGravity = false;
                    dust.velocity = new Vector2(
                        Main.rand.NextFloat(-3.5f, 3.5f),
                        Main.rand.NextFloat(-9f, -4f) * weight);

                    dust.scale = Main.rand.NextFloat(0.9f, 1.5f);
                }
            }
        }

        private static void Seize(NPC npc, Player player)
        {
            player.GetModPlayer<SlimeGrabPlayer>()
                .Seize(npc.whoAmI, Main.rand.Next(GrabMinTicks, GrabMaxTicks));

            if (Main.dedServ)
                return;

            Main.LocalPlayer.PIS().BumpScreenShake(12f, 30);
        }

        // ---- ritual ---------------------------------------------------------

        private void RunRite(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.noTileCollide = true;
            npc.aiAction = 1;

            Anchor(npc);

            switch (rite)
            {
                case Rite.Gather:
                    if (HeraldsReady() || riteTimer >= GatherLimit)
                    {
                        rite = Rite.Approach;
                        riteTimer = 0;
                    }

                    break;

                case Rite.Approach:
                    Look(altar, Ease(riteTimer / (float)RitePan));

                    if (riteTimer >= RitePan)
                    {
                        Burst();

                        rite = Rite.Rise;
                        riteTimer = 0;
                    }

                    break;

                case Rite.Rise:
                    Look(altar, 1f);

                    Resize(npc, MathHelper.Lerp(SeedScale, 1.25f * SizeForMode(),
                        Ease(riteTimer / (float)RiteRise)));

                    Anchor(npc);

                    if (riteTimer >= RiteRise)
                    {
                        rite = Rite.Withdraw;
                        riteTimer = 0;
                    }

                    break;

                case Rite.Withdraw:
                    Look(altar, 1f - Ease(riteTimer / (float)RitePan));

                    if (riteTimer >= RitePan)
                    {
                        rite = Rite.Done;

                        npc.dontTakeDamage = false;
                        npc.chaseable = true;
                        npc.noTileCollide = false;
                        npc.noGravity = false;
                        npc.hide = false;

                        // Handed to the jump cycle mid-wind-up, so the fight opens with a
                        // beat of squash rather than with an instant jump.
                        npc.ai[0] = RestShort;
                        npc.ai[1] = 0f;
                        npc.ai[2] = 0f;

                        npc.netUpdate = true;
                    }

                    break;
            }

            riteTimer++;
        }

        /// <summary>
        /// Nothing is drawn at all while the ritual is still hiding the King — body, crown
        /// and ninja alike. The crown and the ninja are separate draw passes inside
        /// vanilla's King Slime draw and do not honour npc.alpha, so fading the body left
        /// both of them hanging in mid-air over the altar.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!PADWorld.Legends)
                return true;

            if (Hidden)
                return false;

            if (!BodyGone)
                return true;

            // The body is gone but the ninja is not, and vanilla draws the two together —
            // the crown is part of the King's own sprite and the ninja is a second pass
            // tinted by the same colour, so there is no alpha that hides one and keeps
            // the other. Refusing the whole draw and putting the ninja back by hand is
            // what lets him outlive the thing he was riding in.
            if (!NinjaGone)
                DrawNinja(npc, spriteBatch, screenPos, drawColor);

            return false;
        }

        /// <summary>
        /// The ninja, placed the way vanilla places him: lagging the King's motion, twice
        /// as hard sideways as vertically, tilted by the horizontal speed, and nudged by
        /// whichever squash frame is showing.
        /// </summary>
        private static void DrawNinja(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D ninja = TextureAssets.Ninja.Value;

            Vector2 lag = new Vector2(-npc.velocity.X * 2f, -npc.velocity.Y);

            if (npc.frame.Y == 120)
                lag.Y += 2f;
            else if (npc.frame.Y == 360)
                lag.Y -= 2f;
            else if (npc.frame.Y == 480)
                lag.Y -= 6f;

            Vector2 at = npc.position - screenPos + npc.Size * 0.5f + lag;

            spriteBatch.Draw(ninja, at, null, npc.GetNPCColorTintedByBuffs(drawColor),
                npc.velocity.X * 0.05f, ninja.Size() * 0.5f, 1f, SpriteEffects.None, 0f);
        }

        // ---- death ----------------------------------------------------------

        /// <summary>
        /// Refuses the first death and hands the King to the animation instead.
        ///
        /// Life pinned at one, damage off, and false returned so vanilla does not remove
        /// it. The sequence zeroes the life itself at the end and calls checkDead again;
        /// the flag makes that second call the real one, which is the frame the loot
        /// drops on.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (!PADWorld.Legends || dying)
                return true;

            dying = true;
            deathTimer = 0;

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;

            return false;
        }

        /// <summary>
        /// The end of the King.
        ///
        /// Pan out, a long silent hold, then it comes apart in stages — cut, collapse,
        /// burst, and the ninja last of all. The silence before the cut is doing as much
        /// work as anything after it: with no pause the slash lands while the fight is
        /// still ringing and reads as one more attack rather than as the end.
        /// </summary>
        private void RunDeath(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.noTileCollide = false;
            npc.aiAction = 0;

            // Left to settle rather than frozen mid-air, in case it died during a slam.
            npc.velocity.X *= 0.9f;

            if (npc.velocity.Y == 0f)
                npc.velocity.X *= 0.8f;

            DeathCamera(npc);

            if (deathTimer == SlashAt)
                Cut(npc);

            if (deathTimer >= ShrinkAt && deathTimer < BurstAt)
                Collapse(npc);

            if (deathTimer == BurstAt)
                Rupture(npc);

            if (deathTimer == NinjaAt)
                NinjaGoes(npc);

            // Smoke and slime keep coming off it right through the middle beats, so the
            // holds are never actually still.
            if (!Main.dedServ && deathTimer > SlashAt && deathTimer < NinjaAt && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, 0f),
                    120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1f, 1.8f));

                dust.noGravity = true;
            }

            deathTimer++;

            if (deathTimer < DeathEnd)
                return;

            // Handed back to vanilla. CheckDead now returns true, so this is the frame the
            // loot drops on and the camera is already home.
            //
            // HitEffect is deliberately NOT called here. Vanilla's own runs a King Slime
            // branch when life hits zero that throws two hundred dust AND a second crown
            // gore — which is where the duplicate crown was coming from. Everything that
            // branch would have done has already happened, at the moment the body went,
            // and it looked better there.
            npc.life = 0;
            npc.checkDead();
        }

        private void DeathCamera(NPC npc)
        {
            if (Main.dedServ)
                return;

            float blend =
                deathTimer < DeathPan ? Ease(deathTimer / (float)DeathPan)
                : deathTimer < BackAt ? 1f
                : 1f - Ease((deathTimer - BackAt) / (float)DeathBack);

            PisPlayer.BlendCamera(npc.Center, blend);
        }

        /// <summary>
        /// The cut lands.
        ///
        /// The line itself is a SlashStreak particle rather than something drawn in
        /// PostDraw. That buys two things: the particle handler draws it additively, which
        /// is what makes it read as light rather than a painted bar, and it draws at the
        /// dust layer — above the King — without this class having to close and reopen
        /// the sprite batch mid-frame to change blend state.
        /// </summary>
        private static void Cut(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(8f, 20);

            // Down and to the right, through him, and longer than he is — so it reads as
            // something that passed through rather than something drawn on.
            Vector2 along = new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX);
            float reach = npc.Size.Length() * 1.15f;

            PADParticleHandler.SpawnParticle(
                new SlashStreak(npc.Center, along, reach, SlashWidth, SlashShow, npc));

            // Thrown along the cut in both directions, so the spray follows the line
            // rather than blooming out of the middle of him.

            for (int i = 0; i < 40; i++)
            {
                Vector2 at = npc.Center + along * Main.rand.NextFloat(-npc.width * 0.6f, npc.width * 0.6f);

                Dust dust = Dust.NewDustPerfect(at, DustID.t_Slime,
                    along.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-6f, 6f),
                    80, new Color(120, 200, 255, 80), Main.rand.NextFloat(1.2f, 2f));

                dust.noGravity = true;
            }
        }

        /// <summary>Falls in on itself for a second before it goes.</summary>
        private void Collapse(NPC npc)
        {
            if (deathScale < 0f)
                deathScale = npc.scale;

            float t = (deathTimer - ShrinkAt) / (float)DeathShrink;

            Resize(npc, MathHelper.Lerp(deathScale, deathScale * 0.45f, Ease(t)));

            if (Main.dedServ || !Main.rand.NextBool(2))
                return;

            Dust puff = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f),
                80, new Color(0, 80, 255, 80), Main.rand.NextFloat(1.4f, 2.2f));

            puff.noGravity = true;
        }

        /// <summary>
        /// The body lets go: dust, a crowd of slimes, and the crown falls off as a gore.
        ///
        /// The crown is part of the King's sprite rather than a separate draw, so it can
        /// only leave by the body leaving — the gore is what carries it on afterwards,
        /// and it is the same one vanilla drops when the King teleports.
        /// </summary>
        private void Rupture(NPC npc)
        {
            if (!Main.dedServ)
            {
                SlimeDust(npc.position, npc.width, npc.height, 70, new Color(0, 80, 255, 100));

                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.4f, Volume = 1f }, npc.Center);
                Main.LocalPlayer.PIS().BumpScreenShake(14f, 34);

                Gore.NewGore(npc.GetSource_FromAI(), npc.Center + new Vector2(-40f, -npc.height / 2f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -4f), GoreID.KingSlimeCrown);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < DeathSlimes; i++)
            {
                int index = NPC.NewNPC(npc.GetSource_FromAI(),
                    (int)npc.Center.X, (int)(npc.position.Y + npc.height), NPCID.BlueSlime);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC slime = Main.npc[index];

                slime.velocity = new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-11f, -5f));
                slime.netUpdate = true;
            }
        }

        /// <summary>The last of him. Nothing is left drawing after this.</summary>
        private static void NinjaGoes(NPC npc)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Particles.BloodDroplet.SpawnBurst(npc.Center + Main.rand.NextVector2Circular(10f, 10f),
                    (MathHelper.TwoPi * i / 10f).ToRotationVector2(), 9, 18f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.5f, Volume = 0.8f }, npc.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(7f, 20);
        }

        private static void Look(Vector2 at, float blend)
        {
            if (Main.dedServ)
                return;

            PisPlayer.BlendCamera(at, blend);
        }

        /// <summary>Smoothstep. Zero slope at both ends, so pans start and stop softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static bool HeraldsReady()
        {
            int here = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC slime = Main.npc[i];

                if (!slime.active || !slime.TryGetGlobalNPC(out LegendsSlime legends))
                    continue;

                if (legends.HeraldArrivedAt(slime))
                    here++;
            }

            return here >= HeraldsNeeded;
        }

        /// <summary>Every herald bursts at once and the King is what is left.</summary>
        private static void Burst()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC slime = Main.npc[i];

                if (!slime.active || !slime.TryGetGlobalNPC(out LegendsSlime legends) || !legends.Herald)
                    continue;

                if (!Main.dedServ)
                    SlimeDust(slime.position, slime.width, slime.height, 24, default);

                slime.active = false;
                slime.netUpdate = true;
            }

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.5f, Volume = 0.9f });
            Main.LocalPlayer.PIS().BumpScreenShake(9f, 26);
        }

        /// <summary>
        /// A scatter of slime, thrown up and out of a box.
        ///
        /// The helper the ritual's burst and the King's death both want. It went missing
        /// when the burst was rewritten to inline its own loop, which left the death
        /// calling something that no longer existed.
        /// </summary>
        /// <param name="tint">Pass default for the King's own blue.</param>
        private static void SlimeDust(Vector2 position, int width, int height, int count, Color tint)
        {
            if (tint == default)
                tint = new Color(0, 80, 255, 100);

            for (int i = 0; i < count; i++)
            {
                Dust dust = Dust.NewDustDirect(position, width, height, DustID.t_Slime,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, 1f),
                    120, tint, Main.rand.NextFloat(1.1f, 1.8f));

                dust.noGravity = true;
                dust.velocity *= 1.2f;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (!PADWorld.Legends)
                return true;

            if (InRite)
                return false;

            // Already being drained. Contact damage on top of the grab would be charging
            // twice for the same hit.
            return !target.GetModPlayer<SlimeGrabPlayer>().Held;
        }
    }

    /// <summary>
    /// Being inside the King Slime.
    ///
    /// Held at its centre, unable to move or use anything, losing health a second at a
    /// time — and what is lost goes into the boss rather than simply going away.
    /// </summary>
    public class SlimeGrabPlayer : ModPlayer
    {
        /// <summary>Index of the NPC holding this player, or -1.</summary>
        public int HeldBy = -1;

        public int HeldTicks;

        private int drain;

        /// <summary>Health taken, and given to the King, once a second.</summary>
        private const int DrainPerSecond = 20;

        public bool Held =>
            HeldBy >= 0
            && HeldBy < Main.maxNPCs
            && Main.npc[HeldBy].active
            && Main.npc[HeldBy].type == NPCID.KingSlime
            && HeldTicks > 0;

        public void Seize(int npcIndex, int ticks)
        {
            HeldBy = npcIndex;
            HeldTicks = ticks;
            drain = 0;
        }

        /// <summary>Grace after being spat out, so the release is not a second attack.</summary>
        private const int ReleaseGrace = 60;

        public override void ResetEffects()
        {
            if (!Held)
            {
                if (HeldBy >= 0)
                    Release();

                HeldBy = -1;
                HeldTicks = 0;
                return;
            }

            HeldTicks--;

            // Blocks every item, including ones that bypass the control flags below.
            Player.noItems = true;
        }

        /// <summary>
        /// Lets go.
        ///
        /// The player is standing in the middle of the King at this moment, so without a
        /// grace window the very next tick is a full contact hit, and walking out of a
        /// body that size is several more. That was the single worst source of damage in
        /// the fight, and none of it was the boss actually landing anything — it was the
        /// player being released inside it and charged for the trip out.
        /// </summary>
        private void Release()
        {
            Player.fullRotation = 0f;

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, ReleaseGrace);

            // Thrown clear as well, so the grace is spent leaving rather than standing in
            // the middle of it waiting for the timer.
            if (HeldBy >= 0 && HeldBy < Main.maxNPCs && Main.npc[HeldBy].active)
            {
                Vector2 out2 = (Player.Center - Main.npc[HeldBy].Center)
                    .SafeNormalize(-Vector2.UnitY);

                Player.velocity = out2 * 6f + new Vector2(0f, -4f);
            }
        }

        public override void SetControls()
        {
            if (!Held)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override void PreUpdateMovement()
        {
            if (!Held)
                return;

            NPC king = Main.npc[HeldBy];

            // The ninja's own wobble, lifted from how vanilla draws him inside the King:
            // the body lags the movement, twice as hard sideways as vertically, and tilts
            // with the horizontal speed. Reusing his numbers rather than inventing some is
            // what makes a caught player look like they belong in there.
            Vector2 lag = new Vector2(-king.velocity.X * 2f, -king.velocity.Y);

            // And the squash frames nudge him, so a landing visibly jolts whoever is
            // inside instead of leaving them serenely centred through it.
            if (king.frame.Y == 120)
                lag.Y += 2f;
            else if (king.frame.Y == 360)
                lag.Y -= 2f;
            else if (king.frame.Y == 480)
                lag.Y -= 6f;

            // Written straight rather than eased. This is a hold — anything that looks
            // like give invites the player to try to fight out of it.
            Player.Center = king.Center + lag;
            Player.velocity = Vector2.Zero;

            Player.fullRotation = king.velocity.X * 0.05f;
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole climb the boss made counts as a fall the moment it lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (++drain < 60)
                return;

            drain = 0;

            Player.Hurt(PlayerDeathReason.ByNPC(HeldBy), DrainPerSecond, 0);

            // Stolen, not merely spent.
            king.life = Math.Min(king.lifeMax, king.life + DrainPerSecond);
            king.HealEffect(DrainPerSecond);
        }
    }
}
