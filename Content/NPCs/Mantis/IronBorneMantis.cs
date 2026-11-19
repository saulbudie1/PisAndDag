using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mantis
{
    /// <summary>
    /// The Iron Borne Mantis, a Hardmode boss, by request ("large NPC around 10 blocks tall and wide ... Make it now
    /// just have a standard slashing attack (anchor the arm to shoulder puppet style) make the slashes dynamic ... in
    /// 3D ... a slash can range distance max 13 tiles. Moves mechanically and like a robot; basically a product of
    /// Tentochi. Im giving you creativity, spare no effort").
    ///
    ///   its body     one sheet of fourteen pictures, drawn in code (scratchpad rig.py; PlaceholderTextures.md), with
    ///                its legs and without its arms: idle, six of its walk, in the air, three of its landing (the
    ///                third a smear, getting up), three of rearing up (the third its mouth open, the roar).
    ///   its arms     two, each three pieces end to end (upper arm, forearm, blade), hung from the shoulder its
    ///                picture has and worked as a puppet's (IronBorneMantis.Arms): the joints are places in three
    ///                dimensions, solved from where the blade's point is to be, and each piece is drawn as it would
    ///                be seen, bigger and wider the nearer it comes, so a cut swings out at whoever is watching.
    ///   how it goes  as a machine: it walks a stride and stops, turns only standing still, moves its arms a notch at
    ///                a time, and every move ends dead.
    ///
    /// What it does, for now: it rears and roars as it comes; walks at whoever it is after; within its blades' reach
    /// (thirteen tiles from its shoulder) it stops, readies one, fixes on where they are (a ring closes on the place),
    /// and strikes there: near, a slash, the blade cocked and swept round low in a great crescent; far, a pierce, the
    /// arm drawn back and shot out straight; then, if they are still in reach, the other arm. Out
    /// of reach too long, above it, or walled off, it crouches and jumps to them, and what is near where it lands is
    /// hurt. Under half its life it does all of it a quarter faster and cuts three times.
    ///
    /// Its touch hurts; its blades hurt more, worked out on each machine for its own player (as the Gilded Knight's
    /// are). Mine, all of its numbers. It drops nothing yet, and nothing in the world calls it but /mantis.
    /// </summary>
    [AutoloadBossHead]
    public partial class IronBorneMantis : ModNPC
    {
        public const int LifeClassic = 34000;
        public const int Touch = 70;
        public const float BladeTimes = 1.5f;
        public const float SlamTimes = 1.2f;

        /// <summary>Its box: its body and its legs under it (its pictures are 192 a side, feet at the bottom).</summary>
        public const int Wide = 110, Tall = 150;

        /// <summary>How fast it walks; a stride and the stop after it, in ticks.</summary>
        private const float Walks = 2.6f;
        private const int Stride = 40, Stop = 12;

        /// <summary>
        /// A blow's clock: readying the arm (in notches), when in that it fixes on its place, the blow, the arm held
        /// where it ended, folded again. Two blows (by the sketch: "idle pierce slash"): the slash, the blade cocked and
        /// swept round low in a crescent; the pierce, the arm drawn back and shot out straight.
        /// </summary>
        private readonly record struct Blow(int Winds, int Fixes, int Cuts, int Holds, int Folds)
        {
            public int Whole => Winds + Cuts + Holds + Folds;
        }

        private static readonly Blow SlashBlow = new(36, 24, 14, 12, 26), PierceBlow = new(30, 18, 5, 14, 24);

        /// <summary>Further than this from its shoulder, it pierces; nearer, it slashes.</summary>
        private const float PiercesFrom = 150f;

        private enum Act { Arrive, Stalk, Slash, Crouch, Air, Land, Pierce }

        private bool Striking => State == Act.Slash || State == Act.Pierce;

        private Blow Now => State == Act.Pierce ? PierceBlow : SlashBlow;

        private Act State
        {
            get => (Act)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        private int Timer
        {
            get => (int)NPC.ai[1];
            set => NPC.ai[1] = value;
        }

        /// <summary>Which arm cuts (0 the near one, 1 the far one), and how many cuts this bout has had.</summary>
        private int Arm
        {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        private int Bout
        {
            get => (int)NPC.ai[3];
            set => NPC.ai[3] = value;
        }

        /// <summary>The way it faces; where a cut is fixed on; ticks it has been kept from them; told to the other machines.</summary>
        private int facing = 1;
        private Vector2 fixedOn;
        private int kept;

        /// <summary>On this machine: how far it has walked (for its legs), its picture, whether this cut has landed here.</summary>
        private float walked;
        private int picture;
        private bool landed;
        private Act wasState;

        /// <summary>On this machine: which of this state's moments have been shown already (each once).</summary>
        private int cued;

        private bool Once(int moment)
        {
            if ((cued & (1 << moment)) != 0)
                return false;

            cued |= 1 << moment;
            return true;
        }

        private bool Enraged => NPC.life < NPC.lifeMax / 2;

        /// <summary>How fast it does things: a quarter faster under half its life.</summary>
        private float Pace => Enraged ? 1.25f : 1f;

        private bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 14;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

            NPCID.Sets.NPCBestiaryDrawModifiers drawn = new() { Scale = 0.6f, PortraitScale = 0.75f, PortraitPositionYOverride = 30f, Position = new Vector2(0f, 40f) };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawn);
            LoadArt();
        }

        public override void SetDefaults()
        {
            NPC.width = Wide;
            NPC.height = Tall;
            NPC.lifeMax = LifeClassic;
            NPC.damage = Touch;
            NPC.defense = 34;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.npcSlots = 12f;
            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.lavaImmune = true;
            NPC.value = Item.buyPrice(gold: 12);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.SpawnWithHigherTime(30);

            if (!Main.dedServ)
                Music = MusicID.Boss3;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.Add(new FlavorTextBestiaryInfoElement("Mods.PisAndDag.NPCs.IronBorneMantis.Bestiary"));
        }

        public override void BossLoot(ref int potionType)
        {
            potionType = ItemID.GreaterHealingPotion;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((sbyte)facing);
            writer.WriteVector2(fixedOn);
            writer.Write((short)kept);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            facing = reader.ReadSByte();
            fixedOn = reader.ReadVector2();
            kept = reader.ReadInt16();
        }

        /// <summary>Its feet, and its shoulder in the world for the picture it shows.</summary>
        public Vector2 Feet => new Vector2(NPC.Center.X, NPC.position.Y + NPC.height);

        public Vector2 Shoulder => Feet + new Vector2(Shoulders[picture].X * facing, Shoulders[picture].Y + FootGap);

        public Vector2 Mouth => Feet + new Vector2(Mouths[picture].X * facing, Mouths[picture].Y + FootGap);

        // ---- each tick -----------------------------------------------------------------------------------------------

        public override void AI()
        {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
                NPC.TargetClosest(false);

            Player prey = Main.player[NPC.target];

            if (prey.dead || !prey.active)
            {
                // Nobody left: it walks off and is gone.
                NPC.velocity.X = facing * Walks;
                NPC.EncourageDespawn(60);
                picture = 1 + (int)(Main.GameUpdateCount / 6 % 6);
                UpdateArms(prey);
                return;
            }

            if (NPC.timeLeft < 1800)
                NPC.timeLeft = 1800;

            if (State != wasState)
            {
                wasState = State;
                landed = false;
                cued = 0;
            }

            // The same state begun again (the other arm's cut): its moments are to come again.
            if (Timer <= 0)
            {
                landed = false;
                cued = 0;
            }

            switch (State)
            {
                case Act.Arrive:
                    Arrive();
                    break;

                case Act.Stalk:
                    Stalk(prey);
                    break;

                case Act.Slash:
                case Act.Pierce:
                    Strike(prey);
                    break;

                case Act.Crouch:
                    Crouch(prey);
                    break;

                case Act.Air:
                    Air();
                    break;

                case Act.Land:
                    Land();
                    break;
            }

            if (!NPC.noTileCollide && NPC.velocity.X != 0f && NPC.velocity.Y == 0f)
                Collision.StepUp(ref NPC.position, ref NPC.velocity, NPC.width, NPC.height, ref NPC.stepSpeed, ref NPC.gfxOffY);

            NPC.direction = NPC.spriteDirection = facing;
            UpdateArms(prey);

            // The light of its eye.
            Lighting.AddLight(Mouth + new Vector2(0f, -24f), 0.5f, 0.1f, 0.14f);
            Timer++;
        }

        private void Go(Act next, int arm = 0)
        {
            if (!Authority)
                return;

            State = next;
            Timer = -1;
            Arm = arm;
            NPC.netUpdate = true;
        }

        private void Face(Player prey)
        {
            int to = prey.Center.X >= NPC.Center.X ? 1 : -1;

            if (to != facing && Authority)
            {
                facing = to;
                NPC.netUpdate = true;
            }
        }

        /// <summary>As it comes: it rears up a notch at a time, roars, and comes down again.</summary>
        private void Arrive()
        {
            NPC.velocity.X = 0f;
            int t = Timer;

            picture = t < 12 ? 11 : t < 24 ? 12 : t < 74 ? 13 : t < 86 ? 12 : t < 98 ? 11 : 0;

            if (!Main.dedServ)
            {
                if (t == 0 || t == 12 || t == 74 || t == 86)
                    Servo(0.2f);

                if (t == 24)
                {
                    cued = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.55f, Volume = 1f }, Mouth);
                    SoundEngine.PlaySound(SoundID.Item22 with { Pitch = -0.6f, Volume = 0.8f }, Mouth);
                    Main.LocalPlayer.PIS().BumpScreenShake(9f, 40);
                }

                if (t >= 24 && t < 70 && t % 8 == 0)
                    PADParticleHandler.SpawnParticle(new HitRing(Mouth, new Color(255, 96, 110), 10f, 150f, 26, 0.55f, (float)(-Math.PI / 2)) { Pixelate = true });
            }

            if (t >= 104)
                Go(Act.Stalk);
        }

        /// <summary>
        /// After them, on foot: a stride and a stop; it turns only stopped. In reach of its blades, a cut. Kept from
        /// them too long (too far, over its head, or walked into a wall), a jump.
        /// </summary>
        private void Stalk(Player prey)
        {
            int cycle = (int)((Stride + Stop) / Pace);
            int stride = (int)(Stride / Pace);
            int into = Timer % cycle;
            bool stopped = into >= stride;
            float reach = Vector2.Distance(prey.Center, Shoulder);
            float across = Math.Abs(prey.Center.X - NPC.Center.X);

            if (stopped || Timer < 8)
            {
                NPC.velocity.X = 0f;
                Face(prey);
                picture = 0;
            }
            else
            {
                // Near enough across, it stands where it is rather than walk under them.
                bool near = across < 90f;
                NPC.velocity.X = near ? 0f : facing * Walks * Pace;

                if (!near)
                {
                    walked += Math.Abs(NPC.velocity.X);
                    int was = picture;
                    picture = 1 + (int)(walked / 9.5f) % 6;

                    if (!Main.dedServ && picture != was && (picture == 1 || picture == 4))
                        Step();
                }
                else
                    picture = 0;
            }

            if (!Authority)
                return;

            bool blocked = NPC.collideX && !stopped;
            bool over = prey.Center.Y < NPC.position.Y - 200f;

            if (reach > Reach + 40f || blocked || over)
                kept += blocked ? 3 : 1;
            else
                kept = Math.Max(0, kept - 2);

            if (Timer > (int)(30 / Pace) && reach <= Reach && NPC.velocity.Y == 0f)
            {
                Bout = 0;
                Go(reach > PiercesFrom ? Act.Pierce : Act.Slash, Main.rand.Next(2));
                return;
            }

            if (kept > 150 && NPC.velocity.Y == 0f)
            {
                kept = 0;
                Go(Act.Crouch);
            }
        }

        /// <summary>
        /// A blow with one arm, a slash or a pierce (IronBorneMantis.Arms moves it by this state's clock): it stands;
        /// part way through readying the arm it fixes on where they will be, within its reach (a ring closes on the
        /// place, a dotted line runs to it); the blow; the arm held where it ended; folded again. Then the other
        /// arm, if they are still in reach and the bout has blows left: a pierce if they are far, a slash if near.
        /// </summary>
        private void Strike(Player prey)
        {
            NPC.velocity.X = 0f;
            picture = 0;

            Blow blow = Now;
            bool pierce = State == Act.Pierce;
            int t = (int)(Timer * Pace);

            if (Timer == 0)
                Face(prey);

            if (Authority && t >= blow.Fixes && fixedOn == Vector2.Zero)
            {
                Vector2 at = prey.Center + prey.velocity * 8f;
                Vector2 from = Shoulder;
                Vector2 off = at - from;
                float far = MathHelper.Clamp(off.Length(), pierce ? 120f : 70f, Reach);
                fixedOn = from + off.SafeNormalize(new Vector2(facing, 0f)) * far;
                NPC.netUpdate = true;
            }

            if (!Main.dedServ)
            {
                // A notch at a time, the arm readied.
                for (int notch = 0; notch < 3; notch++)
                    if (t >= notch * blow.Winds / 3 && Once(3 + notch))
                        Servo(0.3f + notch * 0.15f);

                // The place it has fixed on: a ring closing on it until the blow.
                if (fixedOn != Vector2.Zero && t >= blow.Fixes && t < blow.Winds && Once(0))
                {
                    PADParticleHandler.SpawnParticle(new HitRing(fixedOn, new Color(255, 70, 80), 46f, 8f, Math.Max(4, (int)((blow.Winds - t) / Pace) + 2)) { Pixelate = true });
                    SoundEngine.PlaySound(SoundID.Item23 with { Pitch = 0.9f, Volume = 0.5f }, Shoulder);
                }

                if (t >= blow.Winds && Once(1))
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = pierce ? 0.3f : -0.25f, Volume = 1f }, Shoulder);

                if (t >= blow.Winds + blow.Cuts && fixedOn != Vector2.Zero && Once(2))
                    Struck(pierce);

                Cut(t, blow, pierce);
            }

            if (t < blow.Whole)
                return;

            if (!Authority)
                return;

            fixedOn = Vector2.Zero;
            Bout++;

            float reach = Vector2.Distance(prey.Center, Shoulder);
            bool again = Bout < (Enraged ? 3 : 2) && reach <= Reach + 30f;

            if (again)
                Go(reach > PiercesFrom ? Act.Pierce : Act.Slash, 1 - Arm);
            else
                Go(Act.Stalk);
        }

        /// <summary>The blade has come down where it was fixed on: what it struck (the ground, if it is there), seen and heard.</summary>
        private void Struck(bool pierce)
        {
            Vector2 at = fixedOn;
            Vector2 way = (at - Shoulder).SafeNormalize(Vector2.UnitY);
            bool ground = Collision.SolidCollision(at - new Vector2(8f, 8f) + way * 14f, 16, 16);

            if (pierce)
                HitFx.Light(at, way, new Color(255, 120, 130));
            else
                HitFx.Slash(at, new Vector2(way.X, -0.35f), new Color(255, 120, 130), 100f);

            if (ground)
            {
                HitFx.Ground(at, new Color(130, 126, 132));
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.5f, Volume = 0.9f }, at);
            }

            if (Vector2.Distance(Main.LocalPlayer.Center, at) < 900f)
                Main.LocalPlayer.PIS().BumpScreenShake(ground ? 6f : 3f, 10);
        }

        /// <summary>The blade on this machine's player, while it cuts: once a cut, as it comes to where it was fixed on and a moment after.</summary>
        private void Cut(int t, Blow blow, bool pierce)
        {
            if (landed || fixedOn == Vector2.Zero || t < blow.Winds + blow.Cuts * 3 / 4 || t > blow.Winds + blow.Cuts + 3)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.immune)
                return;

            BladeLine(Arm, pierce, out Vector2 from, out Vector2 to, out float wide);
            float point = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, to, wide, ref point))
                return;

            landed = true;
            int direction = me.Center.X >= NPC.Center.X ? 1 : -1;
            int damage = Main.DamageVar(NPC.damage * BladeTimes, -me.luck);
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.MantisBlade", me.name));

            if (BattleRoyaleSupport.Hurt(me, reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 0f, 9f) > 0)
                HitFx.Heavy(me.Center, to - from, new Color(255, 96, 110));
        }

        /// <summary>Before a jump: down onto its legs, and off at them.</summary>
        private void Crouch(Player prey)
        {
            NPC.velocity.X = 0f;
            int t = (int)(Timer * Pace);
            picture = t < 8 ? 8 : 9;

            if (Timer == 0)
            {
                Face(prey);

                if (!Main.dedServ)
                    Servo(-0.3f);
            }

            if (t < 24 || !Authority)
                return;

            // To where they are, in three quarters of a second or so; higher if they are over it.
            float across = prey.Center.X - NPC.Center.X;
            float up = Math.Max(0f, NPC.Center.Y - prey.Center.Y);
            NPC.velocity = new Vector2(MathHelper.Clamp(across / 46f, -14f, 14f), -MathHelper.Clamp(12f + up * 0.022f, 12f, 21f));
            NPC.position.Y -= 4f;
            Go(Act.Air);
        }

        /// <summary>In the air: as it was thrown; down again and it lands.</summary>
        private void Air()
        {
            picture = 7;

            if (Timer == 1 && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.6f }, Feet);
                HitFx.Ground(Feet, new Color(130, 126, 132));
            }

            if (Timer > 6 && NPC.velocity.Y == 0f)
            {
                NPC.velocity.X = 0f;

                if (Authority)
                    Go(Act.Land);
            }
        }

        /// <summary>Down: what is near its feet is hurt; it sinks onto its legs and gets up again.</summary>
        private void Land()
        {
            NPC.velocity.X = 0f;
            int t = (int)(Timer * Pace);
            picture = t < 5 ? 8 : t < 19 ? 9 : t < 26 ? 10 : 0;

            if (Timer == 0 && !Main.dedServ)
            {
                Vector2 feet = Feet;
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 1f }, feet);
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.7f, Volume = 0.9f }, feet);
                GroundWave.Slam(feet, Wide * 0.5f, 9, 14f);
                HitFx.Ground(feet + new Vector2(-50f, 0f), new Color(130, 126, 132));
                HitFx.Ground(feet + new Vector2(50f, 0f), new Color(130, 126, 132));

                Player me = Main.LocalPlayer;
                float far = Vector2.Distance(me.Center, feet);

                if (far < 1100f)
                    me.PIS().BumpScreenShake(MathHelper.Lerp(12f, 2f, far / 1100f), 22);

                // On the ground near where it came down.
                if (me.active && !me.dead && !me.immune && me.velocity.Y == 0f && Math.Abs(me.Center.X - feet.X) < 190f && Math.Abs(me.Bottom.Y - feet.Y) < 48f)
                {
                    PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.MantisBlade", me.name));
                    BattleRoyaleSupport.Hurt(me, reason, Main.DamageVar(NPC.damage * SlamTimes, -me.luck), me.Center.X >= feet.X ? 1 : -1, false, false, ImmunityCooldownID.General, true, 0f, 0f, 11f);
                }
            }

            if (t >= 19 && !Main.dedServ && Once(0))
                Servo(0.3f);

            if (t >= 30)
                Go(Act.Stalk);
        }

        // ---- what is heard ---------------------------------------------------------------------------------------------

        private void Servo(float pitch) =>
            SoundEngine.PlaySound(SoundID.Item23 with { Pitch = pitch, Volume = 0.45f, PitchVariance = 0.08f }, NPC.Center);

        private void Step()
        {
            Vector2 foot = Feet + new Vector2(facing * 40f, 0f);
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.2f, Volume = 0.35f, PitchVariance = 0.15f }, foot);

            if (Vector2.Distance(Main.LocalPlayer.Center, foot) < 700f)
                Main.LocalPlayer.PIS().BumpScreenShake(1.2f, 4);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 3; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Iron, hit.HitDirection * 2f, -1f);

            if (NPC.life > 0)
                return;

            // Its end: it comes apart in sparks, smoke and scrap.
            for (int i = 0; i < 7; i++)
            {
                Vector2 at = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.5f, NPC.height * 0.45f);
                HitFx.Heavy(at, Main.rand.NextVector2Unit(), i % 2 == 0 ? new Color(255, 176, 72) : new Color(255, 96, 110));
            }

            for (int i = 0; i < 26; i++)
                PADParticleHandler.SpawnParticle(new StoneChip(NPC.Center + Main.rand.NextVector2Circular(50f, 60f),
                    new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-9f, -2f)), new Color(110, 118, 136), Main.rand.Next(50, 90), Main.rand.NextFloat(1.2f, 2.2f)) { Pixelate = true });

            Main.LocalPlayer.PIS().BumpScreenShake(12f, 30);
        }
    }
}
