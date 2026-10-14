using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Items.Mage.Incantation;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Betsy's Flames, from the cast to the head going back: the caster's head is Betsy's for the
    /// whole of it (BetsysFlamesHead draws it, BetsysFlamesPlayer hides theirs), and her breath comes
    /// out of its mouth.
    ///
    /// <list type="number">
    /// <item><b>The change</b>, <see cref="GrowTicks"/>: their head bursts (bone, blood, a red haze,
    /// the neck spurting) and hers heaves up out of the neck in seven throbs, raw red, swelling and
    /// sagging and twitching, her jaw hanging, her eyes dead; at the last heave her eyes open and her
    /// colour comes.</item>
    /// <item><b>The roar</b>, <see cref="RoarTicks"/>: she rears back, her jaw wide and shaking, her
    /// scream (the game's DD2_BetsyScream), the screen shaking (the only time it does), two rings of
    /// fire thrown out from her and sparks off her mouth.</item>
    /// <item><b>The breath</b>, <see cref="BreathTicks"/>: the game's own flame breath, held, and going
    /// half as far again as hers. The game throws Betsy's in one go (a projectile of 78 ticks: 40 in
    /// which it comes out, 38 in which the last of it leaves); here the same flames come for as long as
    /// the breath lasts, then the last of them leave the same way. Drawn by the game's own sums for it
    /// (Main.DrawProj, type 687): puffs of its picture going out, white, then red, then smoke; the light
    /// along it, its dust and smoke, as the game does them; all of it stretched to <see cref="Reach"/>
    /// at the same speed and as close together as hers. Hurts what it touches as hers does, along a
    /// line out of the mouth 40 wide (Projectile.Colliding, 687), every <see cref="HitCooldown"/> ticks
    /// each, and sets it on fire for 7 to 10 seconds, as hers sets the player
    /// (Projectile.StatusPlayer). Through walls, as hers. Her breath sound (DD2_BetsyFlameBreath) again
    /// every <see cref="BreathSoundEvery"/> ticks, and fire glowing in her mouth.</item>
    /// <item><b>Back</b>: the mouth shuts as the last flames leave it, and as the last of them reach the
    /// end her head sags and shrinks back into the neck, dead-eyed and raw again, with blood, and
    /// theirs is there again.</item>
    /// </list>
    ///
    /// Aimed at the cursor, turning toward it at most <see cref="TurnRate"/> a tick, so it sweeps like
    /// hers. The head faces the way it breathes; the caster turns with it.
    ///
    /// Mana: the cast's own (BetsysFlames.Mana, 30) pays for the first second of breath; then
    /// <see cref="ManaPerSecond"/> a second (less with mana cost reductions), a point at a time, as
    /// Aquatic Firearms pays. Out of mana, it stops there, as it does if the seal leaves the hand or the
    /// caster cannot act; the head goes back either way.
    ///
    /// ai[0] is the age in ticks; ai[1] the aim, in radians, sent by the owner as it turns; ai[2] the
    /// age the breath stopped at, 0 while it has not. Everything else follows from those on every
    /// machine; only the owner decides the aim and the stopping, and only the owner's hits count.
    /// </summary>
    public class BetsysFlamesBreath : ModProjectile
    {
        /// <summary>Ticks of the change: their head gone, hers heaving up out of the neck.</summary>
        public const int GrowTicks = 48;

        /// <summary>Ticks of the roar before the breath.</summary>
        public const int RoarTicks = 30;

        /// <summary>The age the breath starts at.</summary>
        public const int BreathStart = GrowTicks + RoarTicks;

        /// <summary>"shoots flames at cursor for 5 seconds".</summary>
        public const int BreathTicks = 5 * 60;

        /// <summary>The game's 40 ticks a puff takes from the mouth to the end of the breath, half as long again with the reach.</summary>
        public const float PuffTicks = 60f;

        /// <summary>The last flames leave this many ticks after it stops: a puff's whole way.</summary>
        public const int TailTicks = 60;

        /// <summary>Ticks the mouth takes to shut once it stops.</summary>
        public const int CloseTicks = 16;

        /// <summary>Ticks the head takes to go back into the neck: the last of the tail, or at once if it never breathed.</summary>
        public const int ShrinkTicks = 16;

        /// <summary>Ticks of breath the cast's own mana pays for: a second.</summary>
        public const int FreeTicks = 60;

        /// <summary>"Each sec consumes 30 mana".</summary>
        public const float ManaPerSecond = 30f;

        /// <summary>The game's: how far Betsy's breath goes, in pixels.</summary>
        public const float GameReach = 400f;

        /// <summary>How far this one goes: "extend the range by 1.5x".</summary>
        public const float Reach = GameReach * 1.5f;

        /// <summary>The game's 15 puffs, as many again as the reach is longer, so they are as close as hers.</summary>
        public const int Puffs = 22;

        /// <summary>The game's: how wide the line it hurts along is, in pixels.</summary>
        public const float StreamWidth = 40f;

        /// <summary>The game's 25 ticks for the hurting line to reach all the way out, half as long again with the reach.</summary>
        public const float StreamGrowTicks = 37.5f;

        /// <summary>Ticks before the same thing can be hurt by it again.</summary>
        public const int HitCooldown = 5;

        /// <summary>How fast it turns toward the cursor, in radians a tick.</summary>
        public const float TurnRate = 0.12f;

        /// <summary>Ticks between her breath sounds while it breathes.</summary>
        public const int BreathSoundEvery = 45;

        /// <summary>Her head is drawn at half its size: the picture's pixels are 2 by 2, so this is every one of them, one to one.</summary>
        public const float HeadScale = 0.5f;

        /// <summary>The most the jaw opens, in radians, at the roar.</summary>
        public const float JawMost = 0.5f;

        /// <summary>How far open it is while it breathes, as a share of <see cref="JawMost"/>.</summary>
        public const float BreathJaw = 0.72f;

        /// <summary>The breath leaves along the middle of the open mouth: this far under the way the head faces.</summary>
        public const float BreathTilt = BreathJaw * JawMost * 0.5f;

        /// <summary>Where the neck is on the caster: the game's own head origin, 2 back and a little under.</summary>
        private static readonly Vector2 Neck = new Vector2(-2f, -7f);

        /// <summary>The ticks of the change at which her head heaves up out of the neck, and how big it is after each.</summary>
        private static readonly int[] Heaves = { 5, 11, 17, 23, 28, 33, 37 };
        private static readonly float[] HeaveSize = { 0.24f, 0.36f, 0.48f, 0.6f, 0.72f, 0.84f, 0.94f };

        /// <summary>The tick of the change her eyes open at, just after the last heave.</summary>
        public const int EyesOpenAt = 39;

        private static readonly Color RingHot = new Color(255, 214, 120);
        private static readonly Color RingFire = new Color(255, 132, 48);
        private static readonly Color Spark = new Color(255, 170, 60);
        private static readonly Color Blood = new Color(150, 8, 12);

        private float manaDue;
        private float sentAim;
        private int sendWait;
        private int nextBreathSound = -1;
        private uint played;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2BetsyFlameBreath;

        private int Age => (int)Projectile.ai[0];

        private float Aim
        {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        /// <summary>The age the breath stopped at; 0 while it has not.</summary>
        private int Ended
        {
            get => (int)Projectile.ai[2];
            set => Projectile.ai[2] = value;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = HitCooldown;
            Projectile.alpha = 255;
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
        }

        public override bool? CanCutTiles() => false;

        // ---- every tick ---------------------------------------------------------------------

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            int age = Age;
            Projectile.timeLeft = 2;

            if (Projectile.owner == Main.myPlayer)
                Drive(player, age);

            // Five seconds of breath, on every machine alike.
            if (Ended == 0 && age >= BreathStart + BreathTicks)
                Ended = age;

            int dir = Math.Cos(Aim) >= 0.0 ? 1 : -1;

            if (Projectile.owner == Main.myPlayer)
                player.ChangeDir(dir);

            Look look = Pose(age);
            int turn = dir * (player.gravDir < 0f ? -1 : 1);
            float rotation = HeadRotation(Aim, dir, player.gravDir) + turn * (look.Twitch - look.Rear);
            Vector2 pivot = Pivot(player, dir);
            Vector2 mouth = Mouth(pivot, rotation, dir, player.gravDir, look);

            Projectile.Center = mouth;
            Projectile.rotation = Aim;
            Projectile.direction = dir;

            player.GetModPlayer<BetsysFlamesPlayer>().Show(look, rotation, dir);

            // The seal stays up the whole time, held out the way she breathes.
            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            Vector2 way = Aim.ToRotationVector2();
            player.itemRotation = (float)Math.Atan2(way.Y * player.direction, way.X * player.direction);

            if (!Main.dedServ)
                Effects(player, age, pivot, mouth);

            Projectile.ai[0] = age + 1;

            if (Ended > 0 && age >= Ended + (Ended < BreathStart ? ShrinkTicks : TailTicks))
                Projectile.Kill();
        }

        /// <summary>Owner only: the aim, and whether it stops (the seal gone, the caster held, the mana out).</summary>
        private void Drive(Player player, int age)
        {
            float target = (Main.MouseWorld - Pivot(player, player.direction)).ToRotation();
            float aim = Utils.AngleTowards(Aim, target, TurnRate);
            Aim = aim;

            if (sendWait > 0)
                sendWait--;

            if (sendWait == 0 && Math.Abs(MathHelper.WrapAngle(aim - sentAim)) > 0.03f)
            {
                sentAim = aim;
                sendWait = 3;
                Projectile.netUpdate = true;
            }

            if (Ended > 0)
                return;

            bool armed = player.HeldItem?.ModItem is Catalyst seal && seal.Selected(player) is BetsysFlames;

            if (!armed || player.CCed || player.noItems)
            {
                Stop(age);
                return;
            }

            if (age < BreathStart + FreeTicks)
                return;

            // Paid straight off the bar a point at a time, the reduction on the rate, as Aquatic
            // Firearms and Court of Hands pay.
            manaDue += ManaPerSecond / 60f * Math.Max(0f, player.manaCost);

            while (manaDue >= 1f)
            {
                manaDue -= 1f;

                if (player.statMana < 1)
                {
                    Stop(age);
                    return;
                }

                player.statMana--;
            }
        }

        private void Stop(int age)
        {
            Ended = Math.Max(1, age);
            Projectile.netUpdate = true;
        }

        // ---- the head ---------------------------------------------------------------------

        /// <summary>How the head looks at an age: everything BetsysFlamesHead needs besides where it is.</summary>
        public struct Look
        {
            /// <summary>Its size: 1 whole.</summary>
            public float Scale;

            /// <summary>Stretched across and squeezed down (or the other way) as it throbs: 1 by 1 is neither.</summary>
            public Vector2 Squash;

            /// <summary>The head jerking, in radians.</summary>
            public float Twitch;

            /// <summary>The head thrown back, snout up, in radians.</summary>
            public float Rear;

            /// <summary>How far the jaw is open: 0 shut, 1 <see cref="JawMost"/>.</summary>
            public float Jaw;

            /// <summary>The fire in her mouth: 0 none, 1 full.</summary>
            public float Fire;

            /// <summary>How raw it is: 1 all red flesh.</summary>
            public float Flesh;

            /// <summary>Her eyes open (else her dead face, eyes shut).</summary>
            public bool Eyes;
        }

        private Look Pose(int age)
        {
            Look look = new Look { Scale = 1f, Squash = Vector2.One, Eyes = true };
            int ended = Ended;

            if (ended > 0)
            {
                int e = age - ended;
                bool breathed = ended >= BreathStart;
                Look from = ended < GrowTicks ? Grow(ended) : look;

                // The mouth shuts as the last flames leave it, the fire going out in it.
                if (breathed)
                {
                    look.Jaw = BreathJaw * (1f - Math.Min(1f, e / (float)CloseTicks));
                    look.Fire = Math.Max(0f, 1f - e / 10f) * 0.8f;
                }

                int sag = e - (breathed ? TailTicks - ShrinkTicks : 0);

                if (sag >= 0)
                {
                    // Back into the neck: it throbs, sags and goes, dead-eyed and raw again.
                    float s = Math.Min(1f, sag / (float)ShrinkTicks);
                    float throb = (float)Math.Sin(s * MathHelper.Pi * 4f);
                    look.Scale = from.Scale * (1f - s * s) * (1f + 0.07f * throb);
                    look.Squash = new Vector2(1f + 0.08f * throb, 1f - 0.06f * throb);
                    look.Flesh = Math.Max(from.Flesh, s);
                    look.Eyes = false;
                    look.Jaw = Math.Max(look.Jaw, 0.25f * s);
                    look.Twitch = (Noise(age) - 0.5f) * 0.2f * s;
                }
                else if (!breathed)
                {
                    look = from;
                }

                return look;
            }

            if (age < GrowTicks)
                return Grow(age);

            if (age < BreathStart)
            {
                int r = age - GrowTicks;

                // Jaw flung wide and shaking, rearing back, and down again to breathe.
                look.Jaw = r < 4 ? MathHelper.Lerp(0.2f, 1f, r / 4f)
                    : r < RoarTicks - 6 ? ((r / 3) % 2 == 0 ? 1f : 0.86f)
                    : MathHelper.Lerp(1f, BreathJaw, (r - (RoarTicks - 6)) / 6f);
                look.Rear = 0.32f * (float)Math.Sin(MathHelper.Pi * r / RoarTicks);
                look.Scale = 1f + 0.05f * (float)Math.Sin(MathHelper.Pi * r / RoarTicks);
                return look;
            }

            // Breathing: the jaw open, the fire in her mouth flickering.
            int b = age - BreathStart;
            look.Jaw = BreathJaw + 0.04f * (float)Math.Sin(b * 0.9f);
            look.Fire = Math.Min(1f, b / 4f) * (0.75f + 0.25f * (float)Math.Sin(b * 0.6f));
            return look;
        }

        /// <summary>
        /// The change: a raw nub out of the neck, then seven heaves, each a jump in size that overshoots
        /// and sags back, the head bulging one way and then the other; jerking, the jaw hanging and
        /// working; raw red going to her own colours from the middle of it; her eyes dead until the end.
        /// </summary>
        private static Look Grow(int age)
        {
            Look look = new Look { Squash = Vector2.One, Eyes = age >= EyesOpenAt };
            int last = -1;

            for (int i = 0; i < Heaves.Length; i++)
            {
                if (age >= Heaves[i])
                    last = i;
            }

            float size = last < 0 ? 0.05f + 0.035f * age : HeaveSize[last];

            if (last >= 0)
            {
                float since = age - Heaves[last];
                float pulse = (float)Math.Exp(-since / 2.5f);
                float axis = last % 2 == 0 ? 1f : -1f;
                size += 0.08f * pulse;
                look.Squash = new Vector2(1f + 0.14f * pulse * axis, 1f - 0.1f * pulse * axis);
            }

            int settle = Heaves[Heaves.Length - 1];

            if (age > settle)
            {
                float t = Math.Min(1f, (age - settle) / (float)(GrowTicks - settle));
                size += MathHelper.Lerp(0f, 1f - HeaveSize[HeaveSize.Length - 1], EaseOutBack(t));
            }

            float left = 1f - age / (float)GrowTicks;
            look.Scale = size;
            look.Twitch = (Noise(age) - 0.5f) * 0.26f * left;
            look.Flesh = MathHelper.Clamp(1f - (age - 18) / 26f, 0f, 1f);
            look.Jaw = 0.2f * (0.5f + 0.5f * (float)Math.Sin(age * 0.9f)) * left;
            return look;
        }

        private static float EaseOutBack(float t)
        {
            const float back = 1.70158f;
            float u = t - 1f;
            return 1f + (back + 1f) * u * u * u + back * u * u;
        }

        /// <summary>The same jerk on every machine at the same age: a number from 0 to 1 that jumps about.</summary>
        private static float Noise(int n)
        {
            double v = Math.Sin(n * 12.9898) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }

        /// <summary>
        /// Where the head sits on the caster, in the world: the game's own head origin (the neck), with
        /// the walk's bob the game gives what is worn on the head (Main.OffsetsPlayerHeadgear).
        /// </summary>
        public static Vector2 Pivot(Player player, int dir)
        {
            Vector2 bob = Vector2.Zero;
            int height = player.bodyFrame.Height;

            if (height > 0)
            {
                int frame = player.bodyFrame.Y / height;

                if (frame >= 0 && frame < Main.OffsetsPlayerHeadgear.Length)
                    bob = Main.OffsetsPlayerHeadgear[frame];
            }

            return player.MountedCenter + new Vector2(Neck.X * dir, (Neck.Y + bob.Y) * player.gravDir + player.gfxOffY);
        }

        /// <summary>
        /// How far the head is turned: so that the way her breath leaves her (<see cref="BreathTilt"/>
        /// under her heading, turned over with her when she faces left or hangs upside down) is the aim.
        /// </summary>
        public static float HeadRotation(float aim, int dir, float gravDir)
        {
            float own = (float)Math.Atan2(Math.Sin(BreathTilt) * gravDir, Math.Cos(BreathTilt) * dir);
            return MathHelper.WrapAngle(aim - own);
        }

        /// <summary>
        /// Where the mouth is, in the world, for a head so turned and looking so on that pivot: the
        /// middle of the gap at the front, between the upper jaw's tip and the jaw's, a little forward.
        /// </summary>
        public static Vector2 Mouth(Vector2 pivot, float rotation, int dir, float gravDir, Look look)
        {
            Vector2 tip = BetsysFlamesHeadCut.Tip;
            Vector2 hinge = BetsysFlamesHeadCut.Hinge;
            Vector2 jawTip = hinge + (tip - hinge).RotatedBy(look.Jaw * JawMost);
            Vector2 middle = (tip + jawTip) * 0.5f + new Vector2(3f, 0f) - BetsysFlamesHeadCut.Pivot;
            float size = HeadScale * look.Scale;
            Vector2 local = new Vector2(middle.X * dir * look.Squash.X, middle.Y * gravDir * look.Squash.Y) * size;
            return pivot + local.RotatedBy(rotation);
        }

        // ---- the breath ---------------------------------------------------------------------

        /// <summary>
        /// The line it hurts along right now, as shares of the reach, the game's sums (Projectile.Colliding,
        /// 687): its front out over <see cref="StreamGrowTicks"/>, its back leaving the mouth once it stops.
        /// False when there is none.
        /// </summary>
        private bool Stream(int age, out float back, out float front)
        {
            back = 0f;
            front = 0f;

            if (age < BreathStart)
                return false;

            front = Math.Min(1f, (age - BreathStart) / StreamGrowTicks);

            if (Ended > 0)
            {
                if (Ended <= BreathStart)
                    return false;

                back = Math.Min(1f, (age - Ended) / (float)TailTicks);

                if (back >= 1f)
                    return false;
            }

            return front > 0f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Stream(Age, out float back, out float front))
                return false;

            Vector2 way = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center + way * Reach * back;
            Vector2 end = Projectile.Center + way * Reach * front;
            float point = 0f;

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, StreamWidth * Projectile.scale, ref point);
        }

        /// <summary>The game's: Betsy's breath sets the player on fire for 7 to 10 seconds; this, what it hurts.</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 60 * Main.rand.Next(7, 11));
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.OnFire, 60 * Main.rand.Next(7, 11));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int age = Age;

            if (age < BreathStart || (Ended > 0 && Ended <= BreathStart))
                return false;

            Main.instance.LoadProjectile(ProjectileID.DD2BetsyFlameBreath);
            Texture2D texture = TextureAssets.Projectile[ProjectileID.DD2BetsyFlameBreath].Value;

            // Main.DrawProj's sums for 687, but for when a puff is shown: it is if it left the mouth
            // after the breath began and before it stopped (the game's own breath is one 40-tick
            // cycle coming out and one leaving, which this is at its ends).
            Vector2 centre = Projectile.Center - Main.screenPosition;
            float cycle = age / PuffTicks;
            float step = 1f / Puffs;
            int stopped = Ended > 0 ? Ended : int.MaxValue;
            Color hot = new Color(255, 255, 255, 0);
            Color red = new Color(180, 30, 30, 200);
            Color smoke = new Color(0, 0, 0, 30);
            ulong seed = 1uL;

            for (float i = 0f; i < Puffs; i++)
            {
                float spread = Utils.RandomFloat(ref seed) * 0.25f - 0.125f;
                Vector2 way = (Projectile.rotation + spread).ToRotationVector2();
                Vector2 end = centre + way * Reach;
                float along = cycle + i * step;
                int which = (int)(along / step);
                along %= 1f;

                float left = age - along * PuffTicks;

                if (left < BreathStart || left >= stopped)
                    continue;

                Color colour = along < 0.1f ? Color.Lerp(Color.Transparent, hot, Utils.GetLerpValue(0f, 0.1f, along, true))
                    : along < 0.35f ? hot
                    : along < 0.7f ? Color.Lerp(hot, red, Utils.GetLerpValue(0.35f, 0.7f, along, true))
                    : along < 0.9f ? Color.Lerp(red, smoke, Utils.GetLerpValue(0.7f, 0.9f, along, true))
                    : Color.Lerp(smoke, Color.Transparent, Utils.GetLerpValue(0.9f, 1f, along, true));

                float scale = 0.9f + along * 0.8f;
                scale *= scale;
                scale *= 0.8f;

                Vector2 at = Vector2.SmoothStep(centre, end, along);
                Rectangle frame = texture.Frame(1, 7, 0, (int)(along * 7f));
                float turn = Projectile.rotation + MathHelper.TwoPi * (along + Main.GlobalTimeWrappedHourly * 1.2f) * 0.2f + which * (MathHelper.TwoPi / 5f);

                Main.EntitySpriteDraw(texture, at, frame, colour, turn, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
            }

            return false;
        }

        // ---- what is seen and heard ----------------------------------------------------------

        /// <summary>A one-off effect at an age: once, within a few ticks of it, so a skipped or repeated tick neither loses nor doubles it.</summary>
        private bool Once(int bit, int age, int at)
        {
            uint mask = 1u << bit;

            if ((played & mask) != 0 || age < at || age >= at + 6)
                return false;

            played |= mask;
            return true;
        }

        private void Effects(Player player, int age, Vector2 pivot, Vector2 mouth)
        {
            Vector2 up = -Vector2.UnitY * player.gravDir;

            // The change. Their head goes in one burst: the skull cracking (the game's skeletons' hit),
            // the wet death of the Brain of Cthulhu's kind, blood every way and a red haze of it.
            if (Once(0, age, 0))
            {
                SoundEngine.PlaySound(SoundID.NPCDeath11 with { Pitch = -0.3f, Volume = 0.9f }, pivot);
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f, Volume = 0.8f }, pivot);
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.6f, Volume = 0.7f }, pivot);
                BloodDroplet.SpawnBurst(pivot, up, 40, 55f);
                BloodDroplet.SpawnBurst(pivot, Vector2.UnitX, 10, 35f);
                BloodDroplet.SpawnBurst(pivot, -Vector2.UnitX, 10, 35f);

                for (int i = 0; i < 6; i++)
                {
                    PADParticleHandler.SpawnParticle(new BloodFume(pivot + Main.rand.NextVector2Circular(8f, 8f),
                        up.RotatedByRandom(1.2) * Main.rand.NextFloat(0.4f, 1.4f), Main.rand.NextFloat(0.1f, 0.18f), Main.rand.Next(30, 50)));
                }

                for (int i = 0; i < 30; i++)
                    Dust.NewDust(pivot - new Vector2(12f), 24, 24, DustID.Blood, Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(1f, 5f) * player.gravDir);

                for (int i = 0; i < 10; i++)
                    Dust.NewDust(pivot - new Vector2(10f), 20, 20, DustID.Bone, Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 5f) * player.gravDir);
            }

            // The neck spurting, while her head has not yet come.
            if (age >= 1 && age <= 15 && age % 2 == 1)
                BloodDroplet.SpawnBurst(pivot, up, 3, 12f);

            // Each heave: a wet sound (the game's Hungry's), the bigger ones the Wall of Flesh's too, and blood.
            for (int i = 0; i < Heaves.Length; i++)
            {
                if (Once(1 + i, age, Heaves[i]))
                {
                    SoundEngine.PlaySound(SoundID.NPCHit9 with { Pitch = Main.rand.NextFloat(-0.6f, -0.2f), Volume = 0.75f }, pivot);

                    if (i % 2 == 1)
                        SoundEngine.PlaySound(SoundID.NPCHit8 with { Pitch = -0.4f, Volume = 0.45f }, pivot);

                    BloodDroplet.SpawnBurst(pivot, up, 5, 50f);
                }
            }

            // Her eyes opening.
            if (Once(8, age, EyesOpenAt))
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.25f, Volume = 0.5f }, pivot);

            // As long as it is on, the neck drips.
            if (age > 20 && age % 18 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(pivot + Main.rand.NextVector2Circular(5f, 2f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), 0.5f * player.gravDir), Main.rand.Next(30, 45), Main.rand.NextFloat(0.5f, 0.8f), Blood));
            }

            // The roar, and the only shaking.
            if (Once(9, age, GrowTicks))
            {
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream, mouth, Follow);
                PADParticleHandler.SpawnParticle(new ParryRing(pivot, 16f, 170f, 28, RingFire, player));
                PADParticleHandler.SpawnParticle(new ParryRing(pivot, 10f, 110f, 22, RingHot, player));

                Vector2 way = Projectile.rotation.ToRotationVector2();

                for (int i = 0; i < 14; i++)
                {
                    Vector2 velocity = way.RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 6f);
                    PADParticleHandler.SpawnParticle(new Ember(mouth, velocity, Spark, Main.rand.NextFloat(0.45f, 0.85f), Main.rand.Next(18, 34)));
                }

                Shake(pivot, 8f, 30);
            }

            // Going back: it sags with a wet sound and blood, and at the last, theirs is there.
            if (Ended > 0)
            {
                int sagAt = Ended + (Ended < BreathStart ? 0 : TailTicks - ShrinkTicks);

                if (Once(10, age, sagAt))
                {
                    SoundEngine.PlaySound(SoundID.NPCDeath11 with { Pitch = 0.1f, Volume = 0.7f }, pivot);
                    BloodDroplet.SpawnBurst(pivot, up, 14, 60f);

                    for (int i = 0; i < 3; i++)
                    {
                        PADParticleHandler.SpawnParticle(new BloodFume(pivot + Main.rand.NextVector2Circular(6f, 6f),
                            up.RotatedByRandom(1.0) * Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(0.08f, 0.14f), Main.rand.Next(26, 40)));
                    }
                }

                if (Once(11, age, sagAt + ShrinkTicks - 1))
                {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f, Volume = 0.6f }, pivot);
                    BloodDroplet.SpawnBurst(pivot, up, 8, 50f);
                }
            }

            if (!Stream(age, out float back, out float _))
                return;

            if (Ended == 0 && (nextBreathSound < 0 || age >= nextBreathSound))
            {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { MaxInstances = 3 }, mouth, Follow);
                nextBreathSound = age + BreathSoundEvery;
            }

            // The game's own light along it, on three lines (AI_136_BetsyBreath).
            float lightFront = Math.Min(1f, (age - BreathStart) / PuffTicks);
            Vector2 aim = Projectile.rotation.ToRotationVector2();
            DelegateMethods.v3_1 = new Vector3(1.2f, 1f, 0.3f);

            for (int side = -1; side <= 1; side++)
            {
                Vector2 line = aim.RotatedBy(side * 0.19634954631328583);
                Utils.PlotTileLine(mouth + line * Reach * back, mouth + line * Reach * lightFront, 16f, DelegateMethods.CastLight);
            }

            // Its dust and smoke, as the game's.
            if (back == 0f && lightFront > 0.1f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch);
                    fire.fadeIn = 1.5f;
                    fire.velocity = aim.RotatedBy(Main.rand.NextFloatDirection() * (MathHelper.Pi / 12f)) * (0.5f + Main.rand.NextFloat() * 2.5f) * 15f;
                    fire.velocity += player.velocity * 2f;
                    fire.noLight = true;
                    fire.noGravity = true;
                    fire.alpha = 200;
                }
            }

            // The game's smoke at three quarters of the way out.
            if (Main.rand.NextBool(5) && age - BreathStart >= 15)
            {
                Vector2 at = mouth + aim * Reach * 0.75f - Utils.RandomVector2(Main.rand, -20f, 20f);
                Gore puff = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), at, Vector2.Zero, GoreID.Smoke1 + Main.rand.Next(3), 0.5f);
                puff.velocity *= 0.3f;
                puff.velocity += aim * 4f;
            }

            Dust soot = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke);
            soot.fadeIn = 1.5f;
            soot.scale = 0.4f;
            soot.velocity = aim.RotatedBy(Main.rand.NextFloatDirection() * (MathHelper.Pi / 12f)) * (0.5f + Main.rand.NextFloat() * 2.5f) * 15f;
            soot.velocity += player.velocity * 2f;
            soot.velocity *= 0.3f;
            soot.noLight = true;
            soot.noGravity = true;
            float along = Main.rand.NextFloat();
            soot.position = Vector2.Lerp(mouth + aim * Reach * back, mouth + aim * Reach * lightFront, along);
            soot.position += aim.RotatedBy(MathHelper.PiOver2) * (20f + 100f * (along - 0.5f));
        }

        /// <summary>Keeps a sound on the mouth while this breath lasts; after, it plays out where it was.</summary>
        private bool Follow(ActiveSound sound)
        {
            if (Projectile.active && Projectile.ModProjectile == this)
                sound.Position = Projectile.Center;

            return true;
        }

        /// <summary>This machine's own player shaken, if they are near enough to feel it (as Foigh's sword shakes it).</summary>
        private static void Shake(Vector2 at, float power, int ticks)
        {
            Player me = Main.LocalPlayer;

            if (me == null || !me.active || Vector2.Distance(me.Center, at) > 900f)
                return;

            me.PIS().BumpScreenShake(power, ticks);
        }
    }

    /// <summary>
    /// The caster's side of Betsy's Flames: how her head looks this tick (BetsysFlamesBreath sets it on
    /// every machine, every tick it lasts), and their own head hidden while hers is on: their face and
    /// hair, what they wear on the head, and on the face.
    /// </summary>
    public class BetsysFlamesPlayer : ModPlayer
    {
        public BetsysFlamesBreath.Look Look;
        public float Rotation;
        public int Direction = 1;

        private int shown;

        /// <summary>Her head is on them.</summary>
        public bool Transformed => shown > 0;

        public void Show(BetsysFlamesBreath.Look look, float rotation, int direction)
        {
            Look = look;
            Rotation = rotation;
            Direction = direction;
            shown = 1;
        }

        // Cleared at the start of every tick; the breath sets it again after, before anything is drawn.
        public override void ResetEffects()
        {
            if (shown > 0)
                shown--;
        }

        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (!Transformed || drawInfo.headOnlyRender)
                return;

            PlayerDrawLayers.HairBack.Hide();
            PlayerDrawLayers.HeadBack.Hide();
            PlayerDrawLayers.Head.Hide();
            PlayerDrawLayers.FaceAcc.Hide();
        }
    }

    /// <summary>
    /// Betsy's head on the caster, where theirs was, as the reference has it: the game's own picture of
    /// her head (Gore_1081, asked of the game at run time) strip by strip as BetsysFlamesHeadCut has it,
    /// with the reference's own pixels over it (BetsysFlamesHead.png: her eyes open), half size (one of
    /// its pixels, one of the screen's), set by the back of the jaw on the neck, turned about it, turned
    /// over when they face left or hang upside down. The jaw turns about the mouth's corner; behind it,
    /// the inside of her mouth (BetsysFlamesMaw.png, her own colours: dark, purple teeth) opens as far
    /// as the jaw does, and fire glows in it while she breathes (BetsysFlamesMawGlow.png). Lit where
    /// they stand, as what is worn on the head is; her eyes and the fire at full light. Raw red while it
    /// comes and goes, with her dead face (the game's own, under the reference's) until her eyes open.
    /// </summary>
    public class BetsysFlamesHead : PlayerDrawLayer
    {
        private static readonly Color Raw = new Color(255, 110, 100);

        private static Asset<Texture2D> overlay;
        private static Asset<Texture2D> maw;
        private static Asset<Texture2D> mawGlow;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            overlay = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Magic/BetsysFlamesHead");
            maw = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Magic/BetsysFlamesMaw");
            mawGlow = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Magic/BetsysFlamesMawGlow");
        }

        public override void Unload()
        {
            overlay = null;
            maw = null;
            mawGlow = null;
        }

        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.Head, PlayerDrawLayers.FinchNest);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && drawInfo.drawPlayer.GetModPlayer<BetsysFlamesPlayer>().Transformed;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (overlay == null || maw == null || mawGlow == null)
                return;

            Player player = drawInfo.drawPlayer;
            BetsysFlamesPlayer head = player.GetModPlayer<BetsysFlamesPlayer>();
            BetsysFlamesBreath.Look look = head.Look;
            float size = look.Scale * BetsysFlamesBreath.HeadScale;

            if (size <= 0.01f)
                return;

            Main.instance.LoadGore(GoreID.DD2BetsyHead);
            Texture2D gore = TextureAssets.Gore[GoreID.DD2BetsyHead].Value;

            int dir = head.Direction;
            bool mirrored = dir < 0;
            bool flipped = player.gravDir < 0f;
            int turn = (mirrored ? -1 : 1) * (flipped ? -1 : 1);
            SpriteEffects effects = (mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                | (flipped ? SpriteEffects.FlipVertically : SpriteEffects.None);

            Vector2 scale = look.Squash * size;
            float rotation = head.Rotation;
            Vector2 neck = BetsysFlamesBreath.Pivot(player, dir) - Main.screenPosition;
            neck = new Vector2((int)neck.X, (int)neck.Y);

            Vector2 toHinge = BetsysFlamesHeadCut.Hinge - BetsysFlamesHeadCut.Pivot;
            Vector2 hinge = neck + new Vector2(toHinge.X * (mirrored ? -1f : 1f) * scale.X, toHinge.Y * (flipped ? -1f : 1f) * scale.Y).RotatedBy(rotation);

            float shown = drawInfo.colorArmorHead.A / 255f;
            Color lit = drawInfo.colorArmorHead.MultiplyRGBA(Color.Lerp(Color.White, Raw, look.Flesh));
            Color glow = Color.White * shown * (1f - look.Flesh);

            // The inside of the mouth, its top on the upper jaw's lips, as deep as the jaw is open.
            if (look.Jaw > 0.01f)
            {
                Texture2D inside = maw.Value;
                Rectangle all = new Rectangle(0, 0, inside.Width, inside.Height);
                Vector2 origin = Origin(all, BetsysFlamesHeadCut.Hinge - BetsysFlamesHeadCut.MawAt, mirrored, flipped);
                Vector2 opened = new Vector2(scale.X, scale.Y * look.Jaw);

                drawInfo.DrawDataCache.Add(new DrawData(inside, hinge, all, lit, rotation, origin, opened, effects, 0f));

                if (look.Fire > 0f)
                {
                    drawInfo.DrawDataCache.Add(new DrawData(mawGlow.Value, hinge, all, new Color(255, 255, 255, 0) * (look.Fire * shown),
                        rotation, origin, opened, effects, 0f));
                }
            }

            float jaw = look.Jaw * BetsysFlamesBreath.JawMost;
            Strips(ref drawInfo, gore, BetsysFlamesHeadCut.JawKept, BetsysFlamesHeadCut.Hinge, hinge, lit, rotation + turn * jaw, scale, effects, mirrored, flipped);
            Strips(ref drawInfo, gore, BetsysFlamesHeadCut.UpperKept, BetsysFlamesHeadCut.Pivot, neck, lit, rotation, scale, effects, mirrored, flipped);

            if (look.Eyes)
            {
                Strips(ref drawInfo, overlay.Value, BetsysFlamesHeadCut.Overlay, BetsysFlamesHeadCut.Pivot, neck, lit, rotation, scale, effects, mirrored, flipped);

                if (glow.A > 0)
                    Strips(ref drawInfo, overlay.Value, BetsysFlamesHeadCut.Glow, BetsysFlamesHeadCut.Pivot, neck, glow, rotation, scale, effects, mirrored, flipped);
            }
            else
            {
                Strips(ref drawInfo, gore, BetsysFlamesHeadCut.DeadFace, BetsysFlamesHeadCut.Pivot, neck, lit, rotation, scale, effects, mirrored, flipped);
                Strips(ref drawInfo, overlay.Value, BetsysFlamesHeadCut.OverlayAdded, BetsysFlamesHeadCut.Pivot, neck, lit, rotation, scale, effects, mirrored, flipped);
            }
        }

        /// <summary>
        /// Where a point of the picture is in a piece drawn from it, measured from whichever corner the
        /// piece is drawn from once turned over: the origin that turns and turns over every piece about
        /// that one point, so that together they make the head again.
        /// </summary>
        private static Vector2 Origin(Rectangle source, Vector2 anchor, bool mirrored, bool flipped) =>
            new Vector2(mirrored ? source.Right - anchor.X : anchor.X - source.X, flipped ? source.Bottom - anchor.Y : anchor.Y - source.Y);

        private static void Strips(ref PlayerDrawSet drawInfo, Texture2D texture, short[] strips, Vector2 anchor, Vector2 at,
            Color colour, float rotation, Vector2 scale, SpriteEffects effects, bool mirrored, bool flipped)
        {
            for (int i = 0; i + 2 < strips.Length; i += 3)
            {
                int row = strips[i];
                int from = strips[i + 1];
                int to = strips[i + 2];

                Rectangle source = new Rectangle(from, row, to - from, 2);
                drawInfo.DrawDataCache.Add(new DrawData(texture, at, source, colour, rotation, Origin(source, anchor, mirrored, flipped), scale, effects, 0f));
            }
        }
    }
}
