using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.GildedKnight;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>The Sword Of A Gilded Knight's moves.</summary>
    public enum SwordMove
    {
        Up,
        Down,
        Long,
    }

    /// <summary>
    /// The Sword Of A Gilded Knight in the hand, for one use of it (SwordOfAGildedKnight.Shoot):
    /// a cut up, a cut down, or the long blade. ai[0] is the move, ai[1] its ticks, ai[2] the side
    /// its wielder faces; the ticks into it are counted on each machine for itself, so every
    /// machine sees the same move.
    ///
    /// The cuts: the blade swept through some two fifths of a turn, from low in front to high
    /// behind (up) or back (down), slow out of the wind-up, fastest through the middle, easing at
    /// the end; it cuts along its length (<see cref="Width"/> wide) over its whole way since last
    /// tick, each thing once a cut, only what its wielder can see, and in the fast part only.
    ///
    /// The long blade, as the knight's own (GildedKnight's LongBlade): the sword raised over the
    /// head; light grows out of its point into a blade <see cref="BeamLength"/> long, over a
    /// second; then it comes down in front, faster and faster, to lie along the ground, and where
    /// its point lands a pillar of light goes up. Everything under it on the way down is struck,
    /// once; it is light, so through walls as well.
    ///
    /// Drawn in two places: the sword in the hand by GildedSwordLayer (between the body and the
    /// front arm, so the hand closes over the grip), from the pose this sets every tick
    /// (GildedSwordPose); and here (PreDraw, behind the player) what it leaves: the cut's trail,
    /// the blade of light, the pillar.
    /// </summary>
    public class GildedSwordSwing : ModProjectile
    {
        public override string Texture => SwordOfAGildedKnight_Texture;

        public const string SwordOfAGildedKnight_Texture = "PisAndDag/Content/Items/Melee/SwordOfAGildedKnight";

        // ---- the sprite (SwordOfAGildedKnight.png: the sword on the diagonal, the point up and to the right) ----

        /// <summary>The sprite is this many texels square.</summary>
        public const int Size = 58;

        /// <summary>The middle of the grip, where the hand holds it, in texels from the sprite's top left.</summary>
        public static readonly Vector2 Grip = new Vector2(7.9f, 49.1f);

        /// <summary>From the middle of the grip along the sword: the guard, and the point.</summary>
        public const float GuardOut = 9f;
        public const float PointOut = 68f;

        // ---- the cuts ----

        /// <summary>The cut up from low in front (this far below level) to high behind (this far over it), degrees; the cut down the other way.</summary>
        private const float CutLow = 30f;
        private const float CutHigh = -118f;

        /// <summary>The part of a cut that cuts: from and to these shares of it.</summary>
        private const float CutsFrom = 0.16f, CutsTo = 0.86f;

        /// <summary>What a cut cuts with: from this far out from the hand, as wide as this.</summary>
        private const float Inner = 10f;
        private const float Width = 30f;

        // ---- the long blade ----

        /// <summary>Its ticks at its own speed: the sword raised, the light grown, the fall, and lying while the light goes.</summary>
        public const int RaiseTicks = 14;
        public const int FocusTicks = 60;
        public const int FallTicks = 10;
        public const int LieTicks = 22;
        public const int LongTicks = FocusTicks + FallTicks + LieTicks;

        /// <summary>The blade of light, out past the sword's point: 24 tiles.</summary>
        public const float BeamLength = 384f;

        /// <summary>How wide the blade of light strikes.</summary>
        private const float BeamWidth = 34f;

        /// <summary>The sword held up (a little back from straight up), and laid along the ground at the end, degrees.</summary>
        private const float Raised = -100f;
        private const float Laid = 4f;

        /// <summary>Struck by the long blade for this many ticks after it has landed, too: what it has come to rest on.</summary>
        private const int StrikesAfter = 3;

        /// <summary>The pillar where its point lands: how high.</summary>
        private const float PillarHigh = 460f;

        private static readonly Color Gold = new Color(255, 196, 92);
        private static readonly Color Pale = new Color(255, 240, 196);
        private static readonly Color White = new Color(255, 252, 236);
        private static readonly Color Dusk = new Color(110, 64, 22);

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;
        private static Asset<Texture2D> beam;
        private static Asset<Texture2D> soft;
        private static Asset<Texture2D> star;

        private SwordMove Move => (SwordMove)Math.Clamp((int)Projectile.ai[0], 0, 2);

        private float Ticks => Math.Max(1f, Projectile.ai[1]);

        private int Facing => Projectile.ai[2] < 0f ? -1 : 1;

        /// <summary>Ticks into the move, counted here.</summary>
        private float timer;

        /// <summary>The blade's angle last tick and this one, degrees, in the wielder's own frame (facing right, 0 ahead, less than 0 up).</summary>
        private float previousAngle;
        private float angle;

        /// <summary>Where the long blade's point landed, once it has.</summary>
        private Vector2 landed;
        private bool hasLanded;

        private Player Owner => Main.player[Projectile.owner];

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
            beam = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftBeam");
            soft = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft");
            star = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowStar");
        }

        public override void Unload()
        {
            smear = null;
            line = null;
            beam = null;
            soft = null;
            star = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        // ---- where it is in its move ------------------------------------------------------------

        /// <summary>The long blade's moments at this move's length (the wielder's speed stretches or squeezes all of it alike).</summary>
        private float Scale => Ticks / LongTicks;

        private float RaiseEnd => RaiseTicks * Scale;
        private float FocusEnd => FocusTicks * Scale;
        private float FallEnd => (FocusTicks + FallTicks) * Scale;

        /// <summary>The blade's angle at this many ticks into the move, degrees, in the wielder's frame.</summary>
        private float AngleAt(float t)
        {
            switch (Move)
            {
                case SwordMove.Up:
                    return MathHelper.Lerp(CutLow, CutHigh, CutEase(t / Ticks));

                case SwordMove.Down:
                    return MathHelper.Lerp(CutHigh, CutLow, CutEase(t / Ticks));

                default:
                    if (t < RaiseEnd)
                    {
                        float k = t / RaiseEnd;
                        return MathHelper.Lerp(30f, Raised, (float)Math.Sin(k * MathHelper.PiOver2));
                    }

                    if (t < FocusEnd)
                        return Raised + 1.2f * (float)Math.Sin(t * 0.7f) * Utils.GetLerpValue(RaiseEnd, FocusEnd, t, true);

                    if (t < FallEnd)
                    {
                        float k = (t - FocusEnd) / (FallEnd - FocusEnd);
                        return MathHelper.Lerp(Raised, Laid, k * k * k);
                    }

                    return Laid;
            }
        }

        /// <summary>A cut's way: out of the wind-up slowly, fastest through the middle, easing at the end.</summary>
        private static float CutEase(float p)
        {
            p = MathHelper.Clamp(p, 0f, 1f);
            return p < 0.5f ? 4f * p * p * p : 1f - (float)Math.Pow(-2f * p + 2f, 3) / 2f;
        }

        /// <summary>How long the light out of the sword is at this many ticks into the long blade.</summary>
        private float BeamAt(float t)
        {
            if (Move != SwordMove.Long || t < RaiseEnd)
                return 0f;

            float k = Utils.GetLerpValue(RaiseEnd, FocusEnd, t, true);
            return BeamLength * (float)Math.Sin(k * MathHelper.PiOver2);
        }

        /// <summary>How bright the light out of the sword is: all of it until it has landed, then going.</summary>
        private float BeamShown(float t)
        {
            if (Move != SwordMove.Long)
                return 0f;

            if (t < FallEnd)
                return Utils.GetLerpValue(RaiseEnd, RaiseEnd + 8f, t, true);

            float k = Utils.GetLerpValue(FallEnd, Ticks, t, true);
            return 1f - (float)Math.Pow(k, 1.5f);
        }

        private bool Strikes
        {
            get
            {
                if (Move == SwordMove.Long)
                    return timer >= FocusEnd && timer <= FallEnd + StrikesAfter;

                float p = timer / Ticks;
                return p >= CutsFrom && p <= CutsTo;
            }
        }

        // ---- every tick ---------------------------------------------------------------------------

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.type != ModContent.ItemType<SwordOfAGildedKnight>())
            {
                Projectile.Kill();
                return;
            }

            if (timer == 0f)
            {
                previousAngle = AngleAt(0f);
                Projectile.ownerHitCheck = Move != SwordMove.Long;
            }

            float before = timer;
            timer++;

            if (timer > Ticks)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;

            previousAngle = angle == 0f && before == 0f ? AngleAt(0f) : angle;
            angle = AngleAt(timer);

            player.ChangeDir(Facing);

            float radians = MathHelper.ToRadians(angle);
            Player.CompositeArmStretchAmount stretch = Move == SwordMove.Long && timer < FocusEnd ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;
            Vector2 hand = VisusGrip.FrontHand(stretch, radians);

            // The arm as it is the right way up: vanilla turns it over itself under reversed gravity
            // (Player.SetCompositeArmFront), so no gravDir here (IronFlowHoldout's form).
            player.SetCompositeArmFront(true, stretch, (player.direction == 1 ? radians : MathHelper.Pi - radians) - MathHelper.PiOver2);

            GildedSwordPose pose = player.GetModPlayer<GildedSwordPose>();
            pose.Held = true;
            pose.Blade = radians;
            pose.Hand = hand;
            pose.Glow = Move == SwordMove.Long ? 0.85f * BeamShown(timer) : (Strikes ? 0.25f : 0f);

            Projectile.Center = VisusGrip.ToWorld(player, hand + radians.ToRotationVector2() * (PointOut * 0.6f));
            Projectile.velocity = Vector2.Zero;
            Projectile.direction = Projectile.spriteDirection = player.direction;
            Projectile.noEnchantmentVisuals = !Strikes;

            if (!Main.dedServ)
                Effects(player, hand, before);
        }

        /// <summary>The sounds and the light of the move, on every machine that shows it.</summary>
        private void Effects(Player player, Vector2 hand, float before)
        {
            if (Move != SwordMove.Long)
            {
                float from = CutsFrom * Ticks;

                if (before < from && timer >= from)
                {
                    KnightSounds.Play(KnightSounds.Swing with { Pitch = Move == SwordMove.Up ? 0.12f : -0.04f, Volume = 0.75f }, Projectile.Center);
                }

                return;
            }

            float radians = MathHelper.ToRadians(angle);
            Vector2 u = radians.ToRotationVector2();
            Vector2 point = VisusGrip.ToWorld(player, hand + u * PointOut);

            // The light gathering: the sound as it begins, motes drawn up off the blade while it grows.
            if (before < RaiseEnd && timer >= RaiseEnd)
                KnightSounds.Play(KnightSounds.GrowBlade with { Volume = 0.8f }, point);

            if (timer >= RaiseEnd && timer < FocusEnd && (int)timer % 3 == 0)
            {
                Vector2 along = VisusGrip.ToWorld(player, hand + u * Main.rand.NextFloat(GuardOut, PointOut + BeamAt(timer)));
                KnightFx.Motes(along, 1, 10f, Pale);
            }

            if (before < FocusEnd && timer >= FocusEnd)
                KnightSounds.Play(KnightSounds.BladeSweep with { Volume = 0.7f, Pitch = 0.1f }, point);

            // Landed: the slam, the flash, the pillar where the point is.
            if (before < FallEnd && timer >= FallEnd)
            {
                Vector2 end = VisusGrip.ToWorld(player, hand + u * (PointOut + BeamLength));
                landed = end;
                hasLanded = true;

                KnightSounds.Play(KnightSounds.BladeSlam with { Volume = 0.9f }, end);
                KnightSounds.Play(KnightSounds.Slam with { Volume = 0.6f, Pitch = 0.15f }, player.Center);
                KnightFx.Flash(end, new Color(255, 236, 190), 0.1f);
                KnightFx.Shake(player.Center, 8f, 18);
                KnightFx.Ring(end, 10f, 150f, 26, Pale);
                KnightFx.Bloom(end, Gold, 1.4f, 34);
                KnightFx.Sparks(end, -Vector2.UnitY * player.gravDir, 18, Pale, 9f, 1.1f);
                KnightFx.Motes(end, 10, 30f, Gold);

                // All along it: light thrown up off the ground.
                Vector2 from = VisusGrip.ToWorld(player, hand + u * PointOut);

                for (int i = 0; i < 9; i++)
                {
                    Vector2 at = Vector2.Lerp(from, end, (i + 0.5f) / 9f);
                    KnightFx.Sparks(at, -Vector2.UnitY * player.gravDir, 3, Gold, 6f, 0.8f);
                    KnightFx.Bloom(at, Gold * 0.6f, 0.5f, 20);
                }
            }
        }

        // ---- what it strikes ----------------------------------------------------------------------

        public override bool? CanDamage() => Strikes ? null : false;

        /// <summary>What strikes at this angle, in the world: along the sword, and the light past it while there is any.</summary>
        private void Edge(Player player, float degrees, float t, out Vector2 from, out Vector2 to)
        {
            float radians = MathHelper.ToRadians(degrees);
            Vector2 u = radians.ToRotationVector2();
            Player.CompositeArmStretchAmount stretch = Move == SwordMove.Long && t < FocusEnd ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;
            Vector2 hand = VisusGrip.FrontHand(stretch, radians);

            from = VisusGrip.ToWorld(player, hand + u * Inner);
            to = VisusGrip.ToWorld(player, hand + u * (PointOut + 2f + BeamAt(t)));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Strikes)
                return false;

            Player player = Owner;
            float collision = 0f;
            float width = Move == SwordMove.Long ? BeamWidth : Width;

            // From last tick's angle to this one's, in steps close enough at the point (the long
            // blade's is some 450 pixels out) that the lines overlap and nothing between is missed.
            float reach = PointOut + BeamAt(timer);
            float travel = MathHelper.ToRadians(Math.Abs(angle - previousAngle)) * reach;
            int steps = Math.Clamp((int)Math.Ceiling(travel / (width * 0.75f)), 1, 40);

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Edge(player, MathHelper.Lerp(previousAngle, angle, k), timer - 1f + k, out Vector2 from, out Vector2 to);

                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, width, ref collision))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (!Strikes || Move == SwordMove.Long)
                return;

            Edge(Owner, angle, timer, out Vector2 from, out Vector2 to);

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(from, to, Width, DelegateMethods.CutTiles);
        }

        /// <summary>What it strikes is thrown away from its wielder.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = target.Center.X < Owner.Center.X ? -1 : 1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            Player player = Owner;
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);

            if (Move == SwordMove.Long)
                at = target.Center;

            // The way the blade was going: across itself.
            Vector2 u = MathHelper.ToRadians(angle).ToRotationVector2();
            float way = angle > previousAngle ? 1f : -1f;
            Vector2 travel = new Vector2(-u.Y * player.direction, u.X * player.gravDir) * way;

            bool heavy = Move == SwordMove.Long;

            KnightFx.Sparks(at, travel, heavy ? 12 : 6, heavy ? Pale : Gold, heavy ? 8f : 5.5f, 0.7f);
            KnightFx.Bloom(at, (heavy ? Pale : Gold) * 0.7f, heavy ? 0.6f : 0.35f, 14);
            PADParticleHandler.SpawnParticle(new SlashStreak(at, travel, heavy ? 50f : 32f, heavy ? 9f : 5f, 12));
        }

        // ---- what it leaves -----------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Owner;

            if (Move == SwordMove.Long)
            {
                DrawLight(player);
                return false;
            }

            if (smear == null || line == null)
                return false;

            // The trail: while the blade is fast and a few ticks after.
            float p = timer / Ticks;
            float fade = MathHelper.Clamp(1f - (p - CutsTo) / 0.12f, 0f, 1f) * Utils.GetLerpValue(CutsFrom - 0.04f, CutsFrom + 0.06f, p, true);

            if (fade <= 0.01f)
                return false;

            Main.spriteBatch.End();
            DrawTrail(player, fade);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>Steps along the trail, and how many ticks of the blade's way it shows.</summary>
        private const int Steps = 20;
        private const float TrailTicks = 4f;
        private const float TrailInner = 22f, TrailOuter = PointOut + 3f;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] TrailIn = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailOut = new Vector2[Steps + 1];
        private static readonly Vector2[] TrailEdge = new Vector2[Steps + 1];
        private static readonly float[] TrailStrength = new float[Steps + 1];

        /// <summary>
        /// The cut's trail: a band from near the guard to the point over the blade's way the last
        /// few ticks, strongest at the blade: first a little dark, so that it shows against a bright
        /// sky; then gold over it, as light; then a pale line along the point's own path.
        /// </summary>
        private void DrawTrail(Player player, float fade)
        {
            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float t = Math.Max(0f, timer - k * TrailTicks);
                float radians = MathHelper.ToRadians(AngleAt(t));
                Vector2 u = radians.ToRotationVector2();
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, radians);

                TrailIn[i] = VisusGrip.ToWorld(player, hand + u * TrailInner);
                TrailOut[i] = VisusGrip.ToWorld(player, hand + u * TrailOuter);
                TrailEdge[i] = VisusGrip.ToWorld(player, hand + u * (TrailOuter - 7f));
                TrailStrength[i] = (float)Math.Pow(1f - k, 1.4f) * fade;
            }

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(Dusk.R, Dusk.G, Dusk.B, 255) * (0.3f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color light = new Color(Gold.R, Gold.G, Gold.B, 0) * (0.55f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], light, light, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color pale = new Color(White.R, White.G, White.B, 0) * (0.9f * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)Steps);
            }
            Strip.Draw(line.Value);
        }

        private static Color Lit(Color c, float strength) => new Color(c.R, c.G, c.B, 0) * strength;

        /// <summary>
        /// The long blade's light: out of the sword's point, as long as it has grown, gold about a
        /// white core, brightest at its point; then the pillar where it landed.
        /// </summary>
        private void DrawLight(Player player)
        {
            if (beam == null || line == null || soft == null || star == null)
                return;

            float a = BeamShown(timer);
            float length = BeamAt(timer);
            float radians = MathHelper.ToRadians(angle);
            Vector2 u = radians.ToRotationVector2();
            Player.CompositeArmStretchAmount stretch = timer < FocusEnd ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;
            Vector2 hand = VisusGrip.FrontHand(stretch, radians);

            Vector2 from = VisusGrip.ToWorld(player, hand + u * GuardOut);
            Vector2 end = VisusGrip.ToWorld(player, hand + u * (PointOut + length));
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);

            if (a > 0.01f && length > 4f)
            {
                Stretched(beam.Value, from, end, 42f * pulse, Lit(Gold, 0.45f * a));
                Stretched(beam.Value, from, end, 18f, Lit(Pale, 0.85f * a));
                Stretched(line.Value, from, end, 5f, Lit(White, a));
                Soft(end, 40f + 70f * (length / BeamLength), Lit(White, 0.55f * a));
                Soft(from, 70f, Lit(Gold, 0.5f * a));
                Star(end, 54f * pulse, Main.GlobalTimeWrappedHourly * 2f, Lit(White, 0.8f * a));
            }

            if (!hasLanded)
                return;

            // The pillar: up from where the point landed, going as the light goes.
            float k = Utils.GetLerpValue(FallEnd, Ticks, timer, true);
            float pa = (1f - k) * (1f - k);

            if (pa <= 0.01f)
                return;

            Vector2 top = landed - new Vector2(0f, PillarHigh * (0.75f + 0.25f * (float)Math.Sqrt(k)) * player.gravDir);
            float wide = 70f * (1f - 0.5f * k);

            Stretched(beam.Value, landed, top, wide, Lit(Gold, 0.5f * pa));
            Stretched(beam.Value, landed, top, wide * 0.4f, Lit(Pale, 0.85f * pa));
            Stretched(line.Value, landed, top, 6f, Lit(White, pa));
            Soft(landed, 160f, Lit(Pale, 0.6f * pa));
        }

        private static void Stretched(Texture2D tex, Vector2 from, Vector2 to, float thickness, Color colour)
        {
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 0.5f)
                return;

            Main.spriteBatch.Draw(tex, from - Main.screenPosition, null, colour, d.ToRotation(), new Vector2(0f, tex.Height * 0.5f),
                new Vector2(length / tex.Width, thickness / tex.Height), SpriteEffects.None, 0f);
        }

        private static void Soft(Vector2 at, float across, Color colour)
        {
            Texture2D tex = soft.Value;
            Main.spriteBatch.Draw(tex, at - Main.screenPosition, null, colour, 0f, tex.Size() * 0.5f, across / tex.Width, SpriteEffects.None, 0f);
        }

        private static void Star(Vector2 at, float across, float rotation, Color colour)
        {
            Texture2D tex = star.Value;
            Main.spriteBatch.Draw(tex, at - Main.screenPosition, null, colour, rotation, tex.Size() * 0.5f, across / tex.Width, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// The Sword Of A Gilded Knight as the player holds it this tick, for GildedSwordLayer to
    /// draw: set by GildedSwordSwing on every machine, cleared at the start of every tick, so it
    /// is drawn exactly while a move is under way.
    /// </summary>
    public class GildedSwordPose : ModPlayer
    {
        public bool Held;

        /// <summary>The blade's angle in the player's own frame, radians.</summary>
        public float Blade;

        /// <summary>The front hand, in the player's own frame.</summary>
        public Vector2 Hand;

        /// <summary>How much the sword itself shines: with the long blade's light, and a little in a cut.</summary>
        public float Glow;

        public override void ResetEffects()
        {
            Held = false;
        }
    }

    /// <summary>
    /// Draws the Sword Of A Gilded Knight in the player's hand where vanilla draws a held item
    /// behind the front arm (after the body and the back arm, before the front arm, so the hand
    /// closes over the grip): IronFlowLayer's way, from where the player is being drawn, riding
    /// the walk as the arm does. Only while GildedSwordPose.Held. Its own light over it when it
    /// shines.
    /// </summary>
    public class GildedSwordLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> sword;

        public override void Load()
        {
            if (!Main.dedServ)
                sword = ModContent.Request<Texture2D>(GildedSwordSwing.SwordOfAGildedKnight_Texture);
        }

        public override void Unload()
        {
            sword = null;
        }

        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<GildedSwordPose>().Held;

        /// <summary>
        /// How to draw the sprite held at <paramref name="rotation"/> in the world, mirrored across
        /// its blade when <paramref name="flip"/>: the blade already leans an eighth of a turn up, so
        /// it turns an eighth less than it is held; mirrored top to bottom it leans an eighth down
        /// instead, and the grip's texel goes with the mirror (IronFlowLayer.Place).
        /// </summary>
        public static void Place(float rotation, bool flip, out float turn, out Vector2 origin, out SpriteEffects effects)
        {
            Vector2 grip = GildedSwordSwing.Grip;

            origin = flip ? new Vector2(grip.X, GildedSwordSwing.Size - grip.Y) : grip;
            turn = flip ? rotation - MathHelper.PiOver4 : rotation + MathHelper.PiOver4;
            effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (sword == null)
                return;

            Player player = drawInfo.drawPlayer;
            GildedSwordPose pose = player.GetModPlayer<GildedSwordPose>();

            // The middle the arms are drawn from, where the player is being drawn; and the walk.
            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);
            Vector2[] walks = Main.OffsetsPlayerHeadgear;
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < walks.Length ? walks[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + walk * player.gravDir;
            Vector2 position = (hand - Main.screenPosition).Floor();

            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            float rotation = VisusGrip.ToWorldAngle(player, pose.Blade);

            Vector2 middle = VisusGrip.ToWorld(player, pose.Hand + pose.Blade.ToRotationVector2() * 36f);
            Color light = Lighting.GetColor(middle.ToTileCoordinates());

            Place(rotation, flip, out float turn, out Vector2 origin, out SpriteEffects effects);

            drawInfo.DrawDataCache.Add(new DrawData(sword.Value, position, null, light, turn, origin, 1f, effects));

            if (pose.Glow > 0.01f)
            {
                Color glow = new Color(255, 236, 190, 0) * pose.Glow;
                drawInfo.DrawDataCache.Add(new DrawData(sword.Value, position, null, glow, turn, origin, 1f, effects));
            }
        }
    }
}
