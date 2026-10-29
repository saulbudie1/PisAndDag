using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Mimic;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The Wall's numbers in Legends, on everything it fields: the Wall and its eyes a
    /// little more to kill and all of it half again as hard to be hit by, the Hungry and
    /// the leeches included. Its lasers are LegendsWallLaser; the band of the Wall itself,
    /// which vanilla deals without naming anything, is WallGrabPlayer.ModifyHurt.
    ///
    /// Before vanilla's difficulty scaling, as the King's are, so they read as "on top of
    /// the usual". The eyes share the Wall's life (vanilla sets their realLife to it), so
    /// theirs only matters for being the same number.
    ///
    /// And nothing of the Wall touches a player it is holding: the grip is the damage.
    /// </summary>
    public class LegendsWallStats : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => LegendsWall.Family(entity.type);

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            if (entity.type == NPCID.WallofFlesh || entity.type == NPCID.WallofFleshEye)
                entity.lifeMax = (int)(entity.lifeMax * LegendsWall.LifeScale);

            entity.damage = (int)(entity.damage * LegendsWall.DamageScale);
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            return !target.GetModPlayer<WallGrabPlayer>().Held;
        }
    }

    /// <summary>The eyes' lasers, half again as hard in Legends, like the rest of the Wall.</summary>
    public class LegendsWallLaser : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.EyeLaser;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends)
                return;

            // The Retinazer fires the same laser; only the Wall's eyes are the Wall's.
            if (source is EntitySource_Parent parent && parent.Entity is NPC npc && npc.type == NPCID.WallofFleshEye)
                projectile.damage = (int)Math.Round(projectile.damage * LegendsWall.DamageScale);
        }
    }

    /// <summary>
    /// The Wall of Flesh, in Legends.
    ///
    /// It walks a tenth faster than vanilla's. Nothing may be behind it in the underworld:
    /// a player there dies at once (WallGrabPlayer), and anything else there is eaten,
    /// nothing left of it but its gore (Devour). At four tenths of its life it roars, not
    /// loudly, and four tentacles come out of its mouth for the rest of the fight
    /// (Unleash), and it walks faster again: fifteen in a hundred over vanilla.
    ///
    /// The tentacles writhe in front of the mouth. Every four seconds or so the server
    /// sends one at a player (a lash): it rears back and draws in on itself for a moment,
    /// lashes out straight at where they are, holds a breath, and comes back. Whoever it
    /// touches on the way out is taken (WallGrabPlayer): pulled into the mouth and held
    /// there for six seconds, moving with the Wall, losing 45 life a second through any
    /// armour while the Wall gains 200 (never past four tenths of its life).
    ///
    /// The shape of a tentacle is worked out wherever it is needed from a handful of
    /// synced numbers (Shape): the phase, the lash, its timer, and who is held. The
    /// server decides when to lash and at whom; the one being lashed decides whether it
    /// touched them, since only their own machine knows exactly where they are; everyone
    /// else is told (WallGrabPlayer.HandlePacket).
    /// </summary>
    public class LegendsWall : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.WallofFlesh;

        // ---- the Wall's numbers ----------------------------------------------

        public const float LifeScale = 1.12f;
        public const float DamageScale = 1.5f;

        /// <summary>
        /// Its pace against vanilla's, and once it is unleashed. Vanilla writes its speed
        /// fresh every tick (aiStyle 27 sets velocity.X to the full speed by its sign), so
        /// scaling it after the AI (PostAI) is exact and never builds on itself.
        /// </summary>
        public const float Pace = 1.1f;
        public const float PaceUnleashed = 1.15f;

        /// <summary>The share of its life at which it unleashes, and past which the grip will not feed it.</summary>
        public const float UnleashAt = 0.4f;

        /// <summary>Its whole family: the Wall, its eyes, the Hungry (tethered and loose), and the leech.</summary>
        public static bool Family(int type) => type >= NPCID.WallofFlesh && type <= NPCID.LeechTail;

        // ---- behind it ---------------------------------------------------------

        /// <summary>
        /// Vanilla's line (Player.WOFTongue): more than 40 pixels past the Wall's middle on
        /// the side it has come from, and below the top of the band where it can see you,
        /// 250 tiles off the bottom of the world.
        /// </summary>
        private const float BehindMargin = 40f;
        private const int SightDepth = 250;

        /// <summary>
        /// Nothing is killed for being behind it until its arrival is over and three
        /// seconds on. In multiplayer someone can be on the wrong side of it when it
        /// comes; in that time vanilla's tongue pulls them through to the front.
        /// </summary>
        public const int Grace = WallOfFleshIntro.Ticks + 180;

        public static bool Behind(NPC wall, Vector2 point)
        {
            if (wall.direction == 0 || point.Y <= (Main.maxTilesY - SightDepth) * 16f)
                return false;

            float middle = wall.position.X + wall.width / 2;

            return wall.direction < 0 ? point.X > middle + BehindMargin : point.X < middle - BehindMargin;
        }

        // ---- the tentacles -------------------------------------------------------

        public const int Tentacles = 4;

        /// <summary>Points worked out along each one, base to tip.</summary>
        private const int Points = 24;

        /// <summary>Where they come out: the throat, this far in front of its middle, and spread this much up and down it.</summary>
        private const float ThroatAhead = 40f;
        private static readonly float[] Spread = { -26f, -9f, 9f, 26f };

        /// <summary>Which way each points at rest, off straight ahead (radians, up is negative), and how long each is.</summary>
        private static readonly float[] Fan = { -0.6f, -0.2f, 0.2f, 0.6f };
        private static readonly float[] Rest = { 190f, 225f, 215f, 180f };

        /// <summary>Ticks to grow out of the mouth once it is unleashed.</summary>
        private const int EmergeTicks = 50;

        /// <summary>Between lashes, give or take, and before the first.</summary>
        private const int LashEvery = 240;
        private const int LashJitter = 30;
        private const int FirstLash = 90;

        /// <summary>
        /// A lash: rearing back (the tell), out, held, back. The tell is the warning, so
        /// it is the part that has to be long enough to read; the strike itself is too
        /// fast to react to.
        /// </summary>
        private const int TellTicks = 40;
        private const int StrikeTicks = 12;
        private const int HoldOutTicks = 10;
        private const int RetractTicks = 26;
        private const int LashTicks = TellTicks + StrikeTicks + HoldOutTicks + RetractTicks;

        /// <summary>How far a lash reaches from the throat (twenty-eight tiles), and how far off a player may be for one to be sent.</summary>
        public const float MaxReach = 448f;
        private const float LashRange = MaxReach * 1.3f;

        /// <summary>How far they swing up and down at rest, against the first cut of them.</summary>
        private const float Sway = 0.7f;

        /// <summary>Where a lash aims: where they are, and a little of where they are going.</summary>
        private const float AimLead = 6f;

        /// <summary>How far it curls back on itself at the height of the tell, in radians along its length.</summary>
        private const float CurlMost = 2.3f;

        /// <summary>The sprites: a segment 26 pixels thick, drawn every 10 along; the tip, 24 long, its flesh 14 thick.</summary>
        private const float SegmentThickness = 26f;
        private const float SegmentStep = 10f;
        private const float TipLength = 16f;
        private const float BaseWidth = 1f;
        private const float TipWidth = 0.55f;

        /// <summary>Where a held player is kept: this far in front of its middle, clear of the band of the Wall itself.</summary>
        private const float HoldAhead = 72f;

        internal static Asset<Texture2D> SegmentTexture;
        internal static Asset<Texture2D> TipTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            SegmentTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Legends/WallTentacle");
            TipTexture = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Legends/WallTentacleTip");
        }

        public override void Unload()
        {
            SegmentTexture = null;
            TipTexture = null;
        }

        // ---- state -----------------------------------------------------------------

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>Ticks since it came, as this machine counts them; the server's, near enough.</summary>
        private int age;

        /// <summary>Whether it has unleashed, and ticks since.</summary>
        private bool unleashed;
        private int unleashedAge;

        /// <summary>Whether this machine has seen it unleash (the roar, the blood).</summary>
        private bool unleashSeen;

        /// <summary>The lash under way: which tentacle (-1 for none), at whom, and how far in.</summary>
        private int lashTentacle = -1;
        private int lashTarget;
        private int lashTimer;

        /// <summary>The server's countdown to the next lash, and whose turn it is.</summary>
        private int nextLash = FirstLash;
        private int nextTentacle;

        /// <summary>Where the lash under way was aimed, fixed as it leaves: each machine's own view of it.</summary>
        private Vector2 aim;
        private bool aimed;

        /// <summary>The last stage of the lash under way this machine has made a sound for.</summary>
        private int lashHeard;

        /// <summary>Scratch for Shape: one tentacle at a time, never kept.</summary>
        private static readonly Vector2[] points = new Vector2[Points];
        private static readonly float[] widths = new float[Points];

        /// <summary>How far grown out of the mouth the tentacles are, 0 to 1.</summary>
        private float Grown => unleashed ? Ease(unleashedAge / (float)EmergeTicks) : 0f;

        /// <summary>Whether there are tentacles to draw.</summary>
        internal bool Showing => PADWorld.Legends && unleashed;

        /// <summary>Whether its arrival, and the grace after it, are over: being behind it now kills.</summary>
        internal bool PastGrace => age >= Grace;

        // ---- the fight ---------------------------------------------------------------

        public override void PostAI(NPC npc)
        {
            age++;

            if (!PADWorld.Legends)
                return;

            npc.velocity.X *= unleashed ? PaceUnleashed : Pace;

            if (Authority)
            {
                if (!unleashed && npc.life <= npc.lifeMax * UnleashAt)
                {
                    unleashed = true;
                    unleashedAge = 0;
                    npc.netUpdate = true;
                }

                if (age >= Grace)
                    DevourBehind(Mod, npc);
            }

            if (!unleashed)
                return;

            if (!Main.dedServ && !unleashSeen)
            {
                unleashSeen = true;

                // Joined long after it happened: no roar for something already over.
                if (unleashedAge < 20)
                    Unleash(npc);
            }

            unleashedAge++;

            if (Authority)
                ScheduleLash(npc);

            if (lashTentacle >= 0)
                RunLash(npc);

            if (!Main.dedServ)
                Ooze(npc);
        }

        /// <summary>The roar, not loud; the blood; the sound of them coming out.</summary>
        private static void Unleash(NPC npc)
        {
            Vector2 throat = Throat(npc);

            Cinematic.Roar(npc, 80, 0.45f, SoundID.ForceRoar with { Pitch = -0.4f, Volume = 0.65f });
            SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = -0.2f, Volume = 0.8f }, throat);

            for (int i = 0; i < 50; i++)
            {
                Vector2 velocity = new Vector2(npc.direction * Main.rand.NextFloat(2f, 9f), Main.rand.NextFloat(-5f, 5f));
                Dust.NewDustPerfect(throat + Main.rand.NextVector2Circular(20f, 40f), DustID.Blood, velocity, 0, default,
                    Main.rand.NextFloat(1.2f, 2f));
            }
        }

        /// <summary>
        /// The server's: when a lash is due and there is someone in reach who is not
        /// already held, the next free tentacle goes for the nearest of them.
        /// </summary>
        private void ScheduleLash(NPC npc)
        {
            if (unleashedAge < EmergeTicks || lashTentacle >= 0 || --nextLash > 0)
                return;

            nextLash = LashEvery + Main.rand.Next(-LashJitter, LashJitter + 1);

            int target = LashTarget(npc);
            int tentacle = FreeTentacle(npc);

            if (target < 0 || tentacle < 0)
                return;

            lashTentacle = tentacle;
            lashTarget = target;
            lashTimer = 0;
            aimed = false;
            lashHeard = 0;
            npc.netUpdate = true;
        }

        private static int LashTarget(NPC npc)
        {
            Vector2 throat = Throat(npc);
            int best = -1;
            float nearest = LashRange;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead || player.ghost || !player.GetModPlayer<WallGrabPlayer>().CanBeTaken)
                    continue;

                float distance = Vector2.Distance(player.Center, throat);

                if (distance < nearest)
                {
                    nearest = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The next tentacle, in turn, that is not holding anyone.</summary>
        private int FreeTentacle(NPC npc)
        {
            for (int n = 0; n < Tentacles; n++)
            {
                int k = (nextTentacle + n) % Tentacles;

                if (Holder(npc, k) != null)
                    continue;

                nextTentacle = (k + 1) % Tentacles;
                return k;
            }

            return -1;
        }

        /// <summary>
        /// The lash, on every machine: its sounds, its aim fixed as it leaves, and, on the
        /// machine of whoever it touches, the taking.
        /// </summary>
        private void RunLash(NPC npc)
        {
            lashTimer++;

            Player target = Main.player[lashTarget];
            Vector2 root = Root(npc, lashTentacle);

            if (!aimed && lashTimer >= TellTicks)
            {
                aimed = true;
                aim = target.active && !target.dead
                    ? target.Center + target.velocity * AimLead
                    : root + new Vector2(npc.direction * MaxReach, 0f);
            }

            if (!Main.dedServ)
            {
                if (lashHeard < 1)
                {
                    lashHeard = 1;
                    SoundEngine.PlaySound(SoundID.NPCHit9 with { Pitch = -0.6f, Volume = 0.9f }, root);

                    // A grab no shield turns off: the tell every such thing gives, the star on
                    // its tip and the note. Not for a lash this machine only heard of too late.
                    if (Grabbing)
                        CCBypass.Warn(TipOf(npc, lashTentacle));
                }

                if (lashHeard < 2 && lashTimer >= TellTicks)
                {
                    lashHeard = 2;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.6f, Volume = 1f }, root);
                    SoundEngine.PlaySound(SoundID.NPCHit8 with { Pitch = 0.2f, Volume = 0.6f }, root);
                }

                if (lashTimer >= TellTicks && lashTimer < TellTicks + StrikeTicks + HoldOutTicks)
                {
                    TryTake(npc);

                    // Or the Everclear Mirror's copy, as a second player: one lash takes whichever of them it touches first.
                    MimicSystem.AlsoOnCopy(() => TryTake(npc));
                }
            }

            if (lashTimer >= LashTicks)
                lashTentacle = -1;
        }

        /// <summary>
        /// This machine's own player (or the copy, under MimicSystem.AlsoOnCopy), touched by the
        /// lash on its way out or while it is held there: taken, and everyone told.
        /// </summary>
        private void TryTake(NPC npc)
        {
            if (lashTentacle < 0)
                return;

            Player me = Main.LocalPlayer;
            WallGrabPlayer grab = me.GetModPlayer<WallGrabPlayer>();

            if (!me.active || me.dead || me.ghost || !grab.CanBeTaken)
                return;

            Shape(npc, lashTentacle, points, widths);

            if (!Touches(me.Hitbox))
                return;

            int tentacle = lashTentacle;

            grab.Take(npc.whoAmI, tentacle, me.Center);
            lashTentacle = -1;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                WallGrabPlayer.SendTaken(Mod, me.whoAmI, npc.whoAmI, tentacle);
        }

        /// <summary>Whether the lashing part of the tentacle last shaped crosses this box: the outer seven tenths of it, and the hooks.</summary>
        private static bool Touches(Rectangle box)
        {
            Vector2 at = box.TopLeft();
            Vector2 size = box.Size();
            float ignored = 0f;

            for (int i = Points * 3 / 10; i < Points - 1; i++)
            {
                float thickness = (widths[i] + widths[i + 1]) * 0.5f * SegmentThickness;

                if (Collision.CheckAABBvLineCollision(at, size, points[i], points[i + 1], thickness, ref ignored))
                    return true;
            }

            Vector2 tip = points[Points - 1];
            Vector2 on = (tip - points[Points - 2]).SafeNormalize(Vector2.UnitX);

            return Collision.CheckAABBvLineCollision(at, size, tip, tip + on * 14f, 18f, ref ignored);
        }

        /// <summary>Whether the lash under way is about to take someone or can take them now: the tell, the throw and the hold out.</summary>
        private bool Grabbing => lashTentacle >= 0 && lashTimer < TellTicks + StrikeTicks + HoldOutTicks;

        /// <summary>
        /// Where the tip of tentacle k is, asked afresh each time, for the star to ride on.
        /// Its own scratch, so it can be asked at any moment without disturbing a shape
        /// half used. Once the Wall is gone the star stays where it last was.
        /// </summary>
        private Func<Vector2> TipOf(NPC npc, int k)
        {
            Vector2[] line = new Vector2[Points];
            float[] thick = new float[Points];
            Vector2 last = Throat(npc);

            return () =>
            {
                if (npc.active && npc.type == NPCID.WallofFlesh)
                {
                    Shape(npc, k, line, thick);
                    last = line[Points - 1];
                }

                return last;
            };
        }

        /// <summary>A lash that has just taken someone is over, on every machine told of it.</summary>
        internal void EndLash(int tentacle)
        {
            if (lashTentacle == tentacle)
                lashTentacle = -1;
        }

        /// <summary>Blood off them now and then, and the tentacles never still.</summary>
        private void Ooze(NPC npc)
        {
            if (!Main.rand.NextBool(3))
                return;

            int k = Main.rand.Next(Tentacles);

            Shape(npc, k, points, widths);

            Vector2 at = points[Main.rand.Next(4, Points)];

            Dust drop = Dust.NewDustPerfect(at, DustID.Blood, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 2f)),
                0, default, Main.rand.NextFloat(1f, 1.5f));

            drop.noGravity = false;
        }

        // ---- eaten -------------------------------------------------------------------

        /// <summary>
        /// The server's: everything behind it in the underworld is eaten. Not the Wall's
        /// own, no boss and nothing that cannot die (the target dummy); a worm goes whole.
        /// </summary>
        private static void DevourBehind(Mod mod, NPC wall)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC prey = Main.npc[i];

                if (!prey.active || Spared(prey) || !Behind(wall, prey.Center))
                    continue;

                // A body that shares its life with a head goes whole, or not at all.
                int head = prey.realLife >= 0 && prey.realLife < Main.maxNPCs ? prey.realLife : prey.whoAmI;

                if (head != prey.whoAmI && Spared(Main.npc[head]))
                    continue;

                for (int j = 0; j < Main.maxNPCs; j++)
                {
                    NPC part = Main.npc[j];

                    if (part.active && (j == head || part.realLife == head))
                        Devour(mod, part);
                }
            }
        }

        /// <summary>What it will not eat: its own, any boss or part counted as one, and what cannot die.</summary>
        private static bool Spared(NPC npc) =>
            Family(npc.type) || npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.immortal;

        /// <summary>
        /// Gone into the Wall: never through its death, so it drops nothing (no loot, no
        /// coins, no tally toward a banner), and is only its gore, its cry and the sound
        /// of being eaten. A town resident is announced as vanilla announces their death.
        /// </summary>
        private static void Devour(Mod mod, NPC prey)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                ModPacket packet = mod.GetPacket();
                packet.Write((byte)PADPacket.WallAte);
                packet.Write((byte)prey.whoAmI);
                packet.Send();
            }

            if (prey.townNPC)
            {
                NetworkText line = NetworkText.FromKey("Mods.PisAndDag.DeathText.KeptBehindTheWall", prey.GetFullNetName());
                Color red = new Color(255, 25, 25);

                if (Main.netMode == NetmodeID.SinglePlayer)
                    Main.NewText(line.ToString(), red);
                else if (Main.netMode == NetmodeID.Server)
                    ChatHelper.BroadcastChatMessage(line, red, -1);
            }

            Swallowed(prey);

            prey.life = 0;
            prey.active = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, prey.whoAmI);
        }

        /// <summary>What is seen and heard of it, on the machines that see it.</summary>
        private static void Swallowed(NPC prey)
        {
            if (Main.dedServ)
                return;

            prey.life = 0;
            prey.HitEffect(0, 10.0, true);

            if (prey.DeathSound.HasValue)
                SoundEngine.PlaySound(prey.DeathSound.Value, prey.Center);

            SoundEngine.PlaySound(SoundID.Item2 with { Pitch = -0.6f, Volume = 0.9f, MaxInstances = 4 }, prey.Center);

            for (int i = 0; i < 30; i++)
            {
                Dust.NewDustDirect(prey.position, prey.width, prey.height, DustID.Blood,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, 1f), 0, default, Main.rand.NextFloat(1.1f, 1.8f));
            }
        }

        /// <summary>A client, told by the server that something here was eaten.</summary>
        internal static void HandleAte(BinaryReader reader)
        {
            int index = reader.ReadByte();

            if (index >= Main.maxNPCs)
                return;

            NPC prey = Main.npc[index];

            if (!prey.active)
                return;

            Swallowed(prey);
            prey.active = false;
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            // Its death takes them with it.
            if (npc.life > 0 || !Showing || Main.dedServ)
                return;

            for (int k = 0; k < Tentacles; k++)
            {
                Shape(npc, k, points, widths);

                for (int i = 2; i < Points; i += 2)
                {
                    Dust.NewDustPerfect(points[i], DustID.Blood, Main.rand.NextVector2Circular(3f, 3f), 0, default,
                        Main.rand.NextFloat(1.3f, 2.1f));
                }
            }
        }

        // ---- the shape of a tentacle -----------------------------------------------

        /// <summary>The middle of the throat.</summary>
        private static Vector2 Throat(NPC npc) => npc.Center + new Vector2(npc.direction * ThroatAhead, 0f);

        /// <summary>Where tentacle k comes out.</summary>
        private static Vector2 Root(NPC npc, int k) => Throat(npc) + new Vector2(0f, Spread[k]);

        /// <summary>Where a held player is kept.</summary>
        internal static Vector2 HoldPoint(NPC npc) => npc.Center + new Vector2(npc.direction * HoldAhead, 0f);

        /// <summary>The player tentacle k is holding, or null.</summary>
        private static Player Holder(NPC npc, int k)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active)
                    continue;

                WallGrabPlayer grab = player.GetModPlayer<WallGrabPlayer>();

                if (grab.Held && grab.HeldBy == npc.whoAmI && grab.Tentacle == k)
                    return player;
            }

            return null;
        }

        /// <summary>
        /// Tentacle k, as points from its root to its tip and a thickness at each (a share
        /// of the segment sprite's).
        ///
        /// At rest each leaves the throat at its own angle and writhes: two waves run down
        /// it, the second faster and stronger toward the tip, so the tip whips and the root
        /// only sways. Lashing, it is bent toward a line at its aim: curled back on itself
        /// in the tell, thrown straight out along the line, and let go back to its rest.
        /// Holding someone, it runs to them, squirming, its tip on them.
        /// </summary>
        private void Shape(NPC npc, int k, Vector2[] into, float[] thickness)
        {
            int dir = npc.direction >= 0 ? 1 : -1;
            float face = dir > 0 ? 0f : MathHelper.Pi;
            float t = age;
            float grown = Grown;

            Vector2 root = Root(npc, k);
            float rest = Rest[k] * grown;
            float restAngle = face + dir * (Fan[k] + 0.14f * Sway * (float)Math.Sin(t * 0.021f + k * 1.7f));

            float length = rest;
            float wave = Sway;
            float blend = 0f;
            float lineAngle = 0f;
            float curl = 0f;
            float swell = 0f;

            Player holder = Holder(npc, k);

            if (holder != null)
            {
                Vector2 to = holder.Center - root;

                lineAngle = to.ToRotation();
                length = to.Length();
                blend = 1f;
                wave = 0.35f;
            }
            else if (k == lashTentacle)
            {
                Player target = Main.player[lashTarget];
                Vector2 at = aimed ? aim : target.active && !target.dead ? target.Center : root + new Vector2(dir * rest, 0f);
                float reach = MathHelper.Clamp(Vector2.Distance(at, root) + 24f, rest, MaxReach);

                lineAngle = (at - root).ToRotation();

                int lt = lashTimer;

                if (lt < TellTicks)
                {
                    float p = lt / (float)TellTicks;

                    blend = Ease(lt / 12f);
                    curl = CurlMost * Ease(p);
                    length = rest * MathHelper.Lerp(1f, 0.6f, Ease(p));
                    wave = MathHelper.Lerp(Sway, 1.8f, p);
                    swell = 0.3f * p;
                    lineAngle += 0.05f * (float)Math.Sin(lt * 2.7f) * p;
                }
                else if (lt < TellTicks + StrikeTicks)
                {
                    float p = (lt - TellTicks) / (float)StrikeTicks;
                    float thrown = 1f - (1f - p) * (1f - p) * (1f - p);

                    blend = 1f;
                    curl = CurlMost * (1f - thrown) * (1f - thrown);
                    length = MathHelper.Lerp(rest * 0.6f, reach, thrown);
                    wave = MathHelper.Lerp(1.8f, 0.08f, thrown);
                    swell = 0.3f * (1f - thrown);
                }
                else if (lt < TellTicks + StrikeTicks + HoldOutTicks)
                {
                    blend = 1f;
                    length = reach * (1f + 0.015f * (float)Math.Sin(lt * 1.4f));
                    wave = 0.08f;
                }
                else
                {
                    float p = Ease((lt - TellTicks - StrikeTicks - HoldOutTicks) / (float)RetractTicks);

                    blend = 1f - p;
                    length = MathHelper.Lerp(reach, rest, p);
                    wave = MathHelper.Lerp(0.08f, Sway, p);
                }
            }

            // Curled back over the top of itself, whichever way it is pointing.
            float side = Math.Cos(lineAngle) < 0 ? 1f : -1f;
            float step = length / (Points - 1);

            into[0] = root;

            for (int i = 1; i < Points; i++)
            {
                float s = i / (float)(Points - 1);
                float writhe = dir * wave * (s * 0.55f * (float)Math.Sin(t * 0.06f + s * 5.5f + k * 1.3f)
                    + s * s * 0.5f * (float)Math.Sin(t * 0.095f + s * 9f + k * 2.1f));

                float angle = restAngle + writhe;

                if (blend > 0f)
                {
                    float line = lineAngle + side * curl * s + writhe;
                    angle += MathHelper.WrapAngle(line - angle) * blend;
                }

                into[i] = into[i - 1] + angle.ToRotationVector2() * step;
            }

            // Held: the tip is on them, the rest of it bent to meet them.
            if (holder != null)
            {
                Vector2 miss = holder.Center - into[Points - 1];

                for (int i = 1; i < Points; i++)
                {
                    float s = i / (float)(Points - 1);
                    into[i] += miss * (float)Math.Pow(s, 1.5);
                }
            }

            for (int i = 0; i < Points; i++)
            {
                float s = i / (float)(Points - 1);

                thickness[i] = MathHelper.Lerp(BaseWidth, TipWidth, s) * (0.35f + 0.65f * grown)
                    * (1f + 0.12f * (float)Math.Sin(t * 0.25f - s * 8f + k))
                    + swell * s * s * s * s;
            }
        }

        // ---- drawn ---------------------------------------------------------------------

        /// <summary>
        /// All four, into an open batch in world space: over the tiles and the Wall, under
        /// projectiles and players (WallTentacleDraw), so a held player is in front of the
        /// tentacle that holds them and the grip over them is WallGrabOverlay's. The one
        /// lashing or holding is drawn last, over the others.
        /// </summary>
        internal void DrawTentacles(NPC npc, SpriteBatch spriteBatch)
        {
            if (SegmentTexture == null || TipTexture == null || !SegmentTexture.IsLoaded || !TipTexture.IsLoaded)
                return;

            int first = lashTentacle;

            for (int n = 0; n < Tentacles; n++)
            {
                int k = n;

                if (first >= 0)
                    k = n == Tentacles - 1 ? first : (n >= first ? n + 1 : n);

                Shape(npc, k, points, widths);

                // The tell, seen as well as heard: the one about to take someone, or able to
                // now, wears the yellow outline of everything that no shield turns off.
                if (k == lashTentacle && Grabbing)
                    Outline(spriteBatch, k);

                DrawOne(spriteBatch, k, Vector2.Zero, null);
            }
        }

        /// <summary>
        /// The CCBypass outline, for a tentacle the global draw knows nothing of: the one
        /// last shaped, eight times in the warning colour with no alpha, a ring of
        /// OutlineReach pixels round it, under the tentacle itself, so only the ring shows.
        /// </summary>
        private static void Outline(SpriteBatch spriteBatch, int k)
        {
            Color glow = CCBypass.Warning * CCBypass.OutlineGlow;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
                DrawOne(spriteBatch, k, (MathHelper.TwoPi * i / 8f).ToRotationVector2() * CCBypass.OutlineReach, glow);
        }

        /// <summary>
        /// Tentacle k as last shaped, <paramref name="shift"/> off where it is, lit where it
        /// is or, for the outline, all in <paramref name="flat"/>.
        /// </summary>
        private static void DrawOne(SpriteBatch spriteBatch, int k, Vector2 shift, Color? flat)
        {
            Texture2D segment = SegmentTexture.Value;
            Texture2D tip = TipTexture.Value;

            int frameHeight = segment.Height / 2;
            Vector2 segmentOrigin = new Vector2(segment.Width * 0.5f, frameHeight * 0.5f);
            Vector2 tipOrigin = new Vector2(2f, tip.Height * 0.5f);

            float total = 0f;

            for (int i = 1; i < Points; i++)
                total += Vector2.Distance(points[i - 1], points[i]);

            float end = Math.Max(0f, total - TipLength + 4f);

            // Walked along the line, a segment every step, each turned to the line and
            // thinned to it, until the tip.
            int piece = 0;
            int at = 1;
            float before = 0f;
            float span = Vector2.Distance(points[0], points[1]);

            for (float d = 0f; d <= end; d += SegmentStep, piece++)
            {
                while (at < Points - 1 && before + span < d)
                {
                    before += span;
                    at++;
                    span = Vector2.Distance(points[at - 1], points[at]);
                }

                float f = span > 0.001f ? MathHelper.Clamp((d - before) / span, 0f, 1f) : 0f;
                Vector2 position = Vector2.Lerp(points[at - 1], points[at], f);
                float width = MathHelper.Lerp(widths[at - 1], widths[at], f);
                float angle = (points[at] - points[at - 1]).ToRotation();

                Rectangle frame = new Rectangle(0, ((piece * 7 + k * 3) % 5 % 2) * frameHeight, segment.Width, frameHeight);

                spriteBatch.Draw(segment, position + shift - Main.screenPosition, frame, flat ?? Lit(position), angle, segmentOrigin,
                    new Vector2(1f, width), Flip(angle), 0f);
            }

            Vector2 last = points[Points - 1];
            Vector2 prior = points[Points - 2];
            float tipAngle = (last - prior).ToRotation();
            Vector2 tipAt = last - (last - prior).SafeNormalize(Vector2.UnitX) * (TipLength - 4f);

            spriteBatch.Draw(tip, tipAt + shift - Main.screenPosition, null, flat ?? Lit(tipAt), tipAngle, tipOrigin,
                Math.Max(0.35f, widths[Points - 1] / TipWidth), Flip(tipAngle), 0f);
        }

        /// <summary>The sprites are lit from above; pointing left, they are turned over rather than stood on their heads.</summary>
        private static SpriteEffects Flip(float angle) => Math.Cos(angle) < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

        private static Color Lit(Vector2 world) => Lighting.GetColor((int)(world.X / 16f), (int)(world.Y / 16f));

        // ---- sync ------------------------------------------------------------------------
        //
        // The phase, the lash and its timer, and the Wall's age. Each machine counts all
        // three on by itself between packets; a packet moves a count only when it has
        // drifted, so a late one cannot put an animation back.

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(unleashed);
            binaryWriter.Write((short)Math.Min(unleashedAge, short.MaxValue));
            binaryWriter.Write((sbyte)lashTentacle);
            binaryWriter.Write((byte)lashTarget);
            binaryWriter.Write((short)Math.Min(lashTimer, short.MaxValue));
            binaryWriter.Write(Math.Min(age, int.MaxValue));
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool wasUnleashed = bitReader.ReadBit();
            int unleashedFor = binaryReader.ReadInt16();
            int tentacle = binaryReader.ReadSByte();
            int target = binaryReader.ReadByte();
            int timer = binaryReader.ReadInt16();
            int ageNow = binaryReader.ReadInt32();

            if (wasUnleashed && !unleashed)
            {
                unleashed = true;
                unleashedAge = unleashedFor;
            }

            // The lash that has just taken this machine's player: the server has not heard
            // yet, and taking it up again would only play it a second time.
            WallGrabPlayer mine = Main.LocalPlayer.GetModPlayer<WallGrabPlayer>();

            if (tentacle >= 0 && mine.Held && mine.HeldBy == npc.whoAmI && mine.Tentacle == tentacle)
                tentacle = -1;

            if (tentacle != lashTentacle || target != lashTarget || Math.Abs(timer - lashTimer) > 6)
            {
                if (tentacle != lashTentacle || target != lashTarget)
                {
                    aimed = false;
                    lashHeard = 0;
                }

                lashTentacle = tentacle;
                lashTarget = target;
                lashTimer = timer;
            }

            if (Math.Abs(ageNow - age) > 30)
                age = ageNow;
        }

        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>The Wall's tentacles, drawn over the tiles and under the players (LegendsWall.DrawTentacles).</summary>
    public class WallTentacleDraw : ModSystem
    {
        public override void PostDrawTiles()
        {
            int index = Main.wofNPCIndex;

            if (index < 0 || index >= Main.maxNPCs)
                return;

            NPC wall = Main.npc[index];

            if (!wall.active || wall.type != NPCID.WallofFlesh || !wall.TryGetGlobalNPC(out LegendsWall state) || !state.Showing)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            state.DrawTentacles(wall, Main.spriteBatch);

            Main.spriteBatch.End();
        }
    }

    /// <summary>
    /// Being held by the Wall, and being behind it.
    ///
    /// Taken by a tentacle (LegendsWall.TryTake), a player is pulled into the Wall's mouth
    /// over half a second and held there for six, moving with it, unable to move or use
    /// anything. Once there they lose 45 life a second through any armour, and the Wall
    /// gains 200 for each, never past four tenths of its life. Then it lets go: thrown
    /// clear ahead of it, with a moment's grace, and a little longer before another lash
    /// can take them.
    ///
    /// Held is kept on every machine: the one taken tells the server, the server tells
    /// the rest (HandlePacket). The taken player's machine moves them and takes their
    /// life; the server feeds the Wall.
    ///
    /// And in Legends, a player behind the Wall in the underworld dies (PreUpdate), on
    /// their own machine, once its arrival is over (LegendsWall.Grace).
    /// </summary>
    public class WallGrabPlayer : ModPlayer
    {
        /// <summary>The Wall holding this player, or -1; which of its tentacles; and ticks left, the pull included.</summary>
        public int HeldBy = -1;
        public int Tentacle;
        public int HeldTicks;

        /// <summary>Where they were taken from, for the pull.</summary>
        private Vector2 takenFrom;

        private int drain;

        /// <summary>True only inside the drain's own Hurt, so ModifyHurt knows it for the drain.</summary>
        private bool draining;

        /// <summary>After a release, ticks before another lash can take them.</summary>
        private int cooldown;

        private const int PullTicks = 30;
        private const int HoldTicks = 360;
        private const int LifePerSecond = 45;
        private const int FeedPerSecond = 200;
        private const int ReleaseGrace = 60;
        private const int TakeCooldown = 120;

        public bool Held =>
            HeldBy >= 0
            && HeldBy < Main.maxNPCs
            && Main.npc[HeldBy].active
            && Main.npc[HeldBy].type == NPCID.WallofFlesh
            && HeldTicks > 0
            && !Player.dead;

        /// <summary>Not held, and not just let go.</summary>
        public bool CanBeTaken => !Held && cooldown <= 0;

        /// <summary>How far into the mouth they have been pulled, 0 to 1.</summary>
        private float Pulled => MathHelper.Clamp((PullTicks + HoldTicks - HeldTicks) / (float)PullTicks, 0f, 1f);

        public void Take(int wall, int tentacle, Vector2 from)
        {
            HeldBy = wall;
            Tentacle = tentacle;
            HeldTicks = PullTicks + HoldTicks;
            takenFrom = from;
            drain = 0;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit8 with { Pitch = -0.2f, Volume = 1f }, Player.Center);

            for (int i = 0; i < 20; i++)
            {
                Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Blood,
                    Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 1f), 0, default, Main.rand.NextFloat(1.1f, 1.6f));
            }
        }

        public override void ResetEffects()
        {
            if (!Held)
            {
                if (HeldBy >= 0)
                    Release();

                HeldBy = -1;
                HeldTicks = 0;

                if (cooldown > 0)
                    cooldown--;

                return;
            }

            HeldTicks--;

            // Blocks every item, including the ones that get round the controls below.
            Player.noItems = true;
        }

        /// <summary>Thrown clear, ahead of the Wall, with a moment's grace.</summary>
        private void Release()
        {
            Player.fullRotation = 0f;

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, ReleaseGrace);

            cooldown = TakeCooldown;

            if (HeldBy >= 0 && HeldBy < Main.maxNPCs && Main.npc[HeldBy].active)
                Player.velocity = new Vector2(Main.npc[HeldBy].direction * 8f, -6f);
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

            NPC wall = Main.npc[HeldBy];
            Vector2 hold = LegendsWall.HoldPoint(wall);
            float pulled = Pulled;
            float eased = pulled * pulled * (3f - 2f * pulled);

            // Squeezed, a little, all the while.
            Vector2 squeeze = new Vector2(0f, 2f * (float)Math.Sin(HeldTicks * 0.3f));

            Player.Center = Vector2.Lerp(takenFrom, hold, eased) + squeeze;
            Player.velocity = Vector2.Zero;
            Player.fullRotation = 0.08f * (float)Math.Sin(HeldTicks * 0.15f);
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole ride counts as a fall the moment it lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (pulled < 1f || ++drain < 60)
                return;

            drain = 0;

            // Their own machine takes the life, as vanilla deals contact damage, and Hurt
            // tells the others. The Everclear Mirror's copy is its owner's machine's, and is
            // drained there as if it were the one at the keyboard (MimicSystem.AlsoOnCopy).
            if (Player.whoAmI == Main.myPlayer)
                Drain();
            else if (MimicSystem.Is(Player))
                MimicSystem.AlsoOnCopy(Drain);

            // The server's, and one player's alone: the Wall's life is the server's to keep.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                Feed(wall);
        }

        /// <summary>
        /// A second of it out of them. Exactly 45 (ModifyHurt): no armour, no resistance, no
        /// share taken by anything. On its own cooldown slot, cleared again at once, so
        /// nothing else's immunity skips a second of it and it gives none to anything else.
        /// </summary>
        private void Drain()
        {
            draining = true;

            Player.Hurt(PlayerDeathReason.ByNPC(HeldBy), LifePerSecond, 0, cooldownCounter: ImmunityCooldownID.WrongBugNet,
                dodgeable: false, scalingArmorPenetration: 1f, knockback: 0f);

            draining = false;
            Player.hurtCooldowns[ImmunityCooldownID.WrongBugNet] = 0;
        }

        /// <summary>The Wall gains, never past four tenths of its life.</summary>
        private static void Feed(NPC wall)
        {
            int cap = (int)(wall.lifeMax * LegendsWall.UnleashAt);
            int gain = Math.Min(FeedPerSecond, cap - wall.life);

            if (gain <= 0)
                return;

            wall.life += gain;
            wall.HealEffect(gain, true);
            wall.netUpdate = true;
        }

        /// <summary>
        /// Behind the Wall, in Legends, is death, on the player's own machine; for the Everclear
        /// Mirror's copy, on its owner's, as if it were the one at the keyboard (MimicSystem.AlsoOnCopy).
        /// </summary>
        public override void PreUpdate()
        {
            if (Player.whoAmI == Main.myPlayer)
                KeptBehind();
            else if (MimicSystem.Is(Player))
                MimicSystem.AlsoOnCopy(KeptBehind);
        }

        /// <summary>This player, if they are behind the Wall past its grace: dead.</summary>
        private void KeptBehind()
        {
            if (!PADWorld.Legends || Player.dead || Player.ghost || Held)
                return;

            int index = Main.wofNPCIndex;

            if (index < 0 || index >= Main.maxNPCs)
                return;

            NPC wall = Main.npc[index];

            if (!wall.active || wall.type != NPCID.WallofFlesh || !wall.TryGetGlobalNPC(out LegendsWall state) || !state.PastGrace)
                return;

            if (!LegendsWall.Behind(wall, Player.Center))
                return;

            Player.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.KeptBehindTheWall", Player.name)),
                9999.0, 0, false);
        }

        /// <summary>
        /// The band of the Wall itself, half again as hard in Legends. Vanilla deals it in
        /// Player.WOFTongue to a horrified player overlapping 140 pixels at the Wall
        /// (40 on from its left edge, or 56 back from it when it walks right), under a
        /// reason naming nothing (LegacyDefault, other-index 255). That is the only way to
        /// know it, so it is known by being exactly that.
        /// </summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (draining)
            {
                modifiers.ModifyHurtInfo += Exactly;
                return;
            }

            if (!PADWorld.Legends)
                return;

            PlayerDeathReason why = modifiers.DamageSource;

            if (why.SourceOtherIndex != 255 || why.SourceNPCIndex >= 0 || why.SourcePlayerIndex >= 0
                || why.SourceProjectileLocalIndex >= 0 || why.CustomReason != null)
            {
                return;
            }

            int index = Main.wofNPCIndex;

            if (index < 0 || index >= Main.maxNPCs || !Player.gross)
                return;

            NPC wall = Main.npc[index];

            if (!wall.active || wall.type != NPCID.WallofFlesh)
                return;

            float band = wall.position.X + 40f;

            if (wall.direction > 0)
                band -= 96f;

            if (Player.position.X + Player.width > band && Player.position.X < band + 140f)
                modifiers.SourceDamage *= LegendsWall.DamageScale;
        }

        /// <summary>The drain's damage, whatever came between: the last word on it.</summary>
        private static void Exactly(ref Player.HurtInfo info) => info.Damage = LifePerSecond;

        // ---- told --------------------------------------------------------------------

        /// <summary>A client, taken: the server is told who, by which Wall and which of its tentacles.</summary>
        internal static void SendTaken(Mod mod, int player, int wall, int tentacle)
        {
            ModPacket packet = mod.GetPacket();
            packet.Write((byte)PADPacket.WallGrab);
            packet.Write((byte)player);
            packet.Write((byte)wall);
            packet.Write((byte)tentacle);
            packet.Send();
        }

        /// <summary>
        /// On the server, from the one taken: they are held here too, the lash is over,
        /// and everyone else is told. On a client, from the server: someone else is held.
        /// </summary>
        internal static void HandlePacket(Mod mod, BinaryReader reader, int from)
        {
            int player = reader.ReadByte();
            int wall = reader.ReadByte();
            int tentacle = reader.ReadByte();

            if (player >= Main.maxPlayers || wall >= Main.maxNPCs || tentacle >= LegendsWall.Tentacles)
                return;

            // Only a player may say they were taken.
            if (Main.netMode == NetmodeID.Server && player != from)
                return;

            Player taken = Main.player[player];
            NPC npc = Main.npc[wall];

            if (!taken.active || !npc.active || npc.type != NPCID.WallofFlesh)
                return;

            taken.GetModPlayer<WallGrabPlayer>().Take(wall, tentacle, taken.Center);

            if (npc.TryGetGlobalNPC(out LegendsWall state))
                state.EndLash(tentacle);

            if (Main.netMode != NetmodeID.Server)
                return;

            npc.netUpdate = true;

            ModPacket packet = mod.GetPacket();
            packet.Write((byte)PADPacket.WallGrab);
            packet.Write((byte)player);
            packet.Write((byte)wall);
            packet.Write((byte)tentacle);
            packet.Send(-1, from);
        }
    }

    /// <summary>
    /// The grip, over the player it holds: two loops of the tentacle across them, drawn
    /// after them. The rest of the tentacle is under them (WallTentacleDraw).
    /// </summary>
    public class WallGrabOverlay : PlayerDrawLayer
    {
        /// <summary>Across the chest and across the thighs: how far below the middle, and the slope.</summary>
        private static readonly Vector2[] Loops = { new Vector2(-6f, -0.35f), new Vector2(10f, 0.3f) };

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.BeetleBuff);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f
            && !drawInfo.headOnlyRender
            && drawInfo.drawPlayer.GetModPlayer<WallGrabPlayer>().Held;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Asset<Texture2D> asset = LegendsWall.SegmentTexture;

            if (asset == null || !asset.IsLoaded)
                return;

            Texture2D segment = asset.Value;
            Player player = drawInfo.drawPlayer;

            int frameHeight = segment.Height / 2;
            Vector2 origin = new Vector2(segment.Width * 0.5f, frameHeight * 0.5f);
            Vector2 middle = player.Center + new Vector2(0f, player.gfxOffY);
            Color light = Lighting.GetColor((int)(middle.X / 16f), (int)(middle.Y / 16f));

            for (int loop = 0; loop < Loops.Length; loop++)
            {
                float slope = Loops[loop].Y;

                for (int j = -1; j <= 1; j++)
                {
                    Vector2 at = middle + new Vector2(j * 9f, Loops[loop].X + j * 9f * slope);
                    Rectangle frame = new Rectangle(0, ((j + loop) & 1) * frameHeight, segment.Width, frameHeight);

                    drawInfo.DrawDataCache.Add(new DrawData(segment, at - Main.screenPosition, frame, light, slope, origin,
                        new Vector2(1f, 0.62f), SpriteEffects.None, 0f) { ignorePlayerRotation = true });
                }
            }
        }
    }
}
