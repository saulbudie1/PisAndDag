using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Visus brought down (right click): raised overhead and a little behind, held there a beat,
    /// then brought down in front, hard, for triple damage (Visus.Shoot triples it). The next one
    /// waits 2 seconds from the end of this one (<see cref="OnKill"/>, VisusPose.Rest). The swing
    /// is written as Calamity writes theirs (Easing: a curve in pieces): the lift eases out, the
    /// hang eases in and out, the blow starts slow and ends at its fastest, so all the weight is
    /// at the bottom. ai[0] is the side it faces, ai[1] its length in ticks (the item's use time:
    /// Visus makes it 60 ticks where a thrust takes 45, less with melee speed).
    ///
    /// The ground stops it. From the moment it starts down, the hammer's teeth and the point are
    /// tried against solid tiles every tick, and the first tick one of them is in the ground the
    /// angle it met the ground at is found (halved between this tick's and last tick's eight
    /// times) and the head stays there: the blow lands, the floor heaves (GroundWave), the dust
    /// of whatever it hit goes up, and it stops hitting two ticks later. With nothing under it,
    /// it goes on through to 60 degrees below level.
    ///
    /// What it hits it hits along the head's whole sweep since last tick, not only where it is
    /// now, so at the bottom of the blow, where it covers a tile a tick, nothing is stepped over.
    /// The first thing it hits stops it dead for five ticks (its owner's view: hits are only
    /// counted there).
    ///
    /// Drawn by VisusLayer; this draws what it leaves behind (PreDraw, behind the player): three
    /// shadows of itself along the way it came, and the smear, a band from the upper shaft to the
    /// point over the last part of the sweep, dark with a crimson edge (Strip).
    /// </summary>
    public class VisusChop : ModProjectile
    {
        public override string Texture => VisusGrip.SpearTexture;

        // ---- the timing, as shares of the swing --------------------------------------------------

        /// <summary>Raised by here.</summary>
        private const float TopAt = 0.36f;

        /// <summary>Starts down here.</summary>
        private const float DownAt = 0.50f;

        /// <summary>Through by here, if nothing stopped it.</summary>
        private const float ThroughAt = 0.66f;

        /// <summary>Hits from DownAt to here.</summary>
        private const float HitsTo = 0.72f;

        /// <summary>How much of the swing the smear trails behind the head.</summary>
        private const float Trail = 0.09f;

        /// <summary>Held near the butt: the back hand has the rest of the shaft to push with.</summary>
        private const float Grip = 26f;

        /// <summary>The smear, and what it hits, from and to this far out from the front hand, in texels.</summary>
        private const float Inner = 40f, Outer = 96f;

        /// <summary>How thick the head is to what it hits, in pixels.</summary>
        private const float Width = 26f;

        /// <summary>Ticks the first hit holds it still.</summary>
        private const int Stall = 5;

        /// <summary>
        /// The swing, in degrees from straight ahead, down positive: lifted from a little above
        /// level to well past upright, hung there, brought down to 60 below level and through.
        /// </summary>
        private static readonly Piece[] Swing =
        {
            new Piece(Ease.PolyOut, 0f, -25f, -95f, 2),
            new Piece(Ease.SineInOut, TopAt, -120f, -10f),
            new Piece(Ease.PolyIn, DownAt, -130f, 190f, 3),
            new Piece(Ease.PolyOut, ThroughAt, 60f, 6f, 2),
        };

        private static readonly Color Crimson = new Color(196, 26, 48);
        private static readonly Color Cold = new Color(185, 200, 255);
        private static readonly Color Soot = new Color(16, 8, 14);
        private static readonly Color[] Sparks = { new Color(196, 26, 48), new Color(150, 16, 34), new Color(230, 90, 100) };

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;

        private Player Owner => Main.player[Projectile.owner];

        private int Facing => Projectile.ai[0] < 0f ? -1 : 1;

        private float Duration => Math.Max(1f, Projectile.ai[1]);

        private ref float Timer => ref Projectile.localAI[0];

        private bool started;

        /// <summary>The angle this tick and last, upright, radians.</summary>
        private float angle;
        private float previousAngle;

        /// <summary>The angle the ground stopped it at, NaN until it does; and the tick it did.</summary>
        private float ground = float.NaN;
        private float groundTick;

        /// <summary>Ticks it has left to stand still for, after the first hit.</summary>
        private int stall;

        private bool struck;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            smear = null;
            line = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
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

        /// <summary>Where the swing has the spear at <paramref name="progress"/>, radians, stopped by the ground if it was.</summary>
        private float AngleAt(float progress)
        {
            float a = MathHelper.ToRadians(Easing.At(progress, Swing));
            return float.IsNaN(ground) ? a : Math.Min(a, ground);
        }

        private static Player.CompositeArmStretchAmount StretchAt(float progress) =>
            progress < 0.3f ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;

        /// <summary>The eye opens as it is raised and shuts once the blow is spent.</summary>
        private static int EyeAt(float progress) =>
            progress < 0.08f ? VisusGrip.Half : progress < 0.82f ? VisusGrip.Open : progress < 0.9f ? VisusGrip.Half : VisusGrip.Shut;

        private static float GlowAt(float progress) =>
            progress < TopAt ? progress / TopAt : progress < ThroughAt ? 1f : Math.Max(0f, 1f - (progress - ThroughAt) / 0.15f);

        private bool Grounded => !float.IsNaN(ground);

        /// <summary>It hits from the start of the blow until it is through, or two ticks after the ground stops it.</summary>
        private bool Hitting
        {
            get
            {
                float t = Timer / Duration;
                return started && t >= DownAt && t <= HitsTo && (!Grounded || Timer - groundTick <= 2f);
            }
        }

        /// <summary>
        /// However the blow ends, through or cut short, the next one waits Visus.RestTicks from
        /// now. On every machine it ends on, so the eye rests the same everywhere.
        /// </summary>
        public override void OnKill(int timeLeft)
        {
            Owner.GetModPlayer<VisusPose>().Rest = Visus.RestTicks;
        }

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.ModItem is not Visus)
            {
                Projectile.Kill();
                return;
            }

            if (!started)
            {
                started = true;
                angle = previousAngle = AngleAt(0f);
            }

            Projectile.timeLeft = 2;
            player.ChangeDir(Facing);

            if (stall > 0)
            {
                stall--;
                Pose(player, Timer / Duration);
                return;
            }

            float before = Timer / Duration;
            Timer++;
            float t = Timer / Duration;

            if (t >= 1f)
            {
                Projectile.Kill();
                return;
            }

            previousAngle = angle;
            angle = AngleAt(t);

            if (!Grounded && t > DownAt && angle > MathHelper.ToRadians(-30f))
                FindGround(player, t);

            Pose(player, t);

            if (!Main.dedServ)
                Effects(player, before, t);
        }

        private void Pose(Player player, float t)
        {
            Player.CompositeArmStretchAmount stretch = StretchAt(t);
            Vector2 hand = VisusGrip.Hold(player, angle, Grip, stretch, stretch, EyeAt(t), GlowAt(t));

            Projectile.Center = VisusGrip.On(player, angle, hand, Grip, VisusGrip.Eye.X, VisusGrip.Eye.Y);
            Projectile.direction = Projectile.spriteDirection = Facing;
        }

        /// <summary>How far the hammer's teeth bite into what the blow lands on, in pixels.</summary>
        private const float Bite = 6f;

        /// <summary>
        /// Whether the head at <paramref name="a"/> is in the ground: its teeth, less the bite they
        /// take, or its point.
        /// </summary>
        private static bool InGround(Player player, float a, out Vector2 contact)
        {
            Vector2 hand = VisusGrip.FrontHand(StretchAt(1f), a);

            contact = VisusGrip.On(player, a, hand, Grip, VisusGrip.Teeth.X, VisusGrip.Teeth.Y - Bite);
            if (Collision.SolidCollision(contact - new Vector2(3f), 6, 6, true))
                return true;

            contact = VisusGrip.On(player, a, hand, Grip, VisusGrip.Point.X, VisusGrip.Point.Y);
            return Collision.SolidCollision(contact - new Vector2(3f), 6, 6, true);
        }

        private void FindGround(Player player, float t)
        {
            if (!InGround(player, angle, out _))
                return;

            // Where between last tick and this one it met the ground.
            float lo = previousAngle, hi = angle;

            if (InGround(player, lo, out _))
                hi = lo;
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    float mid = (lo + hi) * 0.5f;

                    if (InGround(player, mid, out _))
                        hi = mid;
                    else
                        lo = mid;
                }
            }

            ground = hi;
            groundTick = Timer;
            angle = hi;

            InGround(player, hi, out Vector2 contact);

            if (!Main.dedServ)
                Land(player, contact);
        }

        /// <summary>Which way the head is travelling at <paramref name="a"/>, in the world: across the spear, toward the hammer.</summary>
        private Vector2 Travel(Player player, float a)
        {
            Vector2 u = a.ToRotationVector2();
            return new Vector2(-u.Y * Facing, u.X * player.gravDir);
        }

        private static bool Crossed(float mark, float before, float t) => before < mark && t >= mark;

        private void Effects(Player player, float before, float t)
        {
            Vector2 hand = VisusGrip.FrontHand(StretchAt(t), angle);
            Vector2 point = VisusGrip.On(player, angle, hand, Grip, VisusGrip.Point.X, VisusGrip.Point.Y);
            Vector2 eye = Projectile.Center;

            if (before <= 0f)
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.75f, Volume = 0.5f }, eye);

            // Lifted: embers come off it, the eye opening on them.
            if (t < DownAt && Timer % 3f == 0f)
            {
                PADParticleHandler.SpawnParticle(new Ember(eye + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(0.6f, 0.6f) + new Vector2(0f, -0.4f * player.gravDir),
                    Crimson, Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(24, 40)));
            }

            if (Crossed(TopAt, before, t))
                PADParticleHandler.SpawnParticle(new CrownGlint(point, 28f, 22, Cold));

            if (Crossed(DownAt, before, t))
            {
                SoundEngine.PlaySound(SoundID.DD2_SonicBoomBladeSlash with { Pitch = -0.45f, Volume = 0.8f }, eye);
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.25f, Volume = 0.7f }, eye);
            }

            // Coming down: it sheds soot off its edge.
            if (t > DownAt && t < ThroughAt && !Grounded)
            {
                Vector2 travel = Travel(player, angle);

                for (int i = 0; i < 2; i++)
                {
                    Vector2 from = Vector2.Lerp(point, eye, Main.rand.NextFloat());
                    PADParticleHandler.SpawnParticle(new FlameSmoke(from, -travel * Main.rand.NextFloat(0.5f, 1.6f),
                        Soot, Main.rand.Next(24, 40), 14f, 40f, 0.5f));
                }

                if (Main.rand.NextBool(2))
                {
                    PADParticleHandler.SpawnParticle(new Ember(point, -travel * Main.rand.NextFloat(1f, 2.5f),
                        Crimson, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(18, 30), 0.02f));
                }
            }

            float glow = GlowAt(t);

            if (glow > 0f)
                Lighting.AddLight(eye, Crimson.ToVector3() * (0.45f * glow));
        }

        /// <summary>The blow lands on the ground.</summary>
        private void Land(Player player, Vector2 contact)
        {
            Vector2 travel = Travel(player, ground);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.3f, Volume = 0.95f }, contact);
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = 0.35f, Volume = 0.35f }, contact);

            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(6f, 14);

            // The face it hit looks back the way the blow came.
            GroundWave.Strike(contact, -travel, 10f, 4, 5f, 24);
            Collision.HitTiles(contact - new Vector2(8f), travel * 6f, 16, 16);

            for (int i = 0; i < 5; i++)
            {
                PADParticleHandler.SpawnParticle(new FlameSmoke(contact + Main.rand.NextVector2Circular(8f, 4f),
                    -travel * Main.rand.NextFloat(0.6f, 1.8f) + Main.rand.NextVector2Circular(1f, 0.5f),
                    Soot, Main.rand.Next(40, 64), 24f, 70f, 0.55f));
            }

            SparkShard.Spray(contact, Crimson, 7, 4.5f, 28f, 18, falls: true, palette: Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(contact, Vector2.Zero, Crimson * 0.7f, 0.7f, 18));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;
            float collision = 0f;

            // Last tick's angle to this one's, so nothing between is stepped over.
            for (int i = 0; i <= 3; i++)
            {
                float a = MathHelper.Lerp(previousAngle, angle, i / 3f);
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);
                Vector2 u = a.ToRotationVector2();

                Vector2 from = VisusGrip.ToWorld(player, hand + u * 20f);
                Vector2 to = VisusGrip.ToWorld(player, hand + u * Outer);

                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, to, Width, ref collision))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (!Hitting)
                return;

            Player player = Owner;
            Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, angle);
            Vector2 u = angle.ToRotationVector2();

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(VisusGrip.ToWorld(player, hand + u * 20f), VisusGrip.ToWorld(player, hand + u * Outer), Width, DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Owner;
            Vector2 travel = Travel(player, angle);
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);

            if (!struck)
            {
                struck = true;
                stall = Stall;

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = 0.1f, Volume = 0.75f }, at);
                    Main.LocalPlayer.PIS().BumpScreenShake(4.5f, 10);
                    PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Crimson * 0.8f, 0.6f, 16));
                }
            }

            if (Main.dedServ)
                return;

            for (int i = 0; i < 5; i++)
            {
                float length = Main.rand.NextFloat(20f, 34f);

                PADParticleHandler.SpawnParticle(new SparkShard(at,
                    travel.RotatedByRandom(0.6f) * Main.rand.NextFloat(3f, 7f),
                    Sparks[Main.rand.Next(Sparks.Length)], length * 0.5f, 20f, length, Main.rand.Next(14, 22)));
            }
        }

        // ---- drawing -------------------------------------------------------------------------------

        /// <summary>How much of the trail is left: all of it while the blow is moving, gone within eight ticks of it stopping.</summary>
        private float Fade()
        {
            float stop = Grounded ? groundTick : ThroughAt * Duration;
            return MathHelper.Clamp(1f - (Timer - stop) / 8f, 0f, 1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!started)
                return false;

            Player player = Owner;
            float t = Timer / Duration;
            float fade = Fade();

            if (t <= DownAt || fade <= 0f)
                return false;

            DrawShadows(player, t, fade);

            float tail = Math.Max(DownAt, t - Trail);

            if (smear != null && line != null && t - tail > 0.004f)
            {
                Main.spriteBatch.End();
                DrawSmear(player, t, tail, fade);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                    Main.Rasterizer, null, Main.Transform);
            }

            return false;
        }

        /// <summary>Three shadows of the spear along the way it came, darkening what is behind them.</summary>
        private void DrawShadows(Player player, float t, float fade)
        {
            PolearmArt art = PolearmArt.Visus;
            bool flip = (player.direction < 0) != (player.gravDir < 0f);

            for (int i = 3; i >= 1; i--)
            {
                float p = t - 0.028f * i;

                if (p <= DownAt)
                    continue;

                float a = AngleAt(p);

                if (Math.Abs(a - angle) < 0.02f)
                    continue;

                Vector2 hand = VisusGrip.FrontHand(StretchAt(p), a);
                Vector2 at = VisusGrip.ToWorld(player, hand) - Main.screenPosition;
                float strength = 0.42f * (1f - i / 4f) * fade;

                // Drawn as it is in the hands at this angle.
                PolearmPlacement place = art.Place(VisusGrip.Open, VisusGrip.ToWorldAngle(player, a), Grip, flip);
                Texture2D texture = place.Texture ?? Terraria.GameContent.TextureAssets.Projectile[Type].Value;

                Main.EntitySpriteDraw(texture, at, place.Frame, new Color(26, 8, 20, 255) * strength, place.Rotation,
                    place.Origin, 1f, place.Effects, 0);
            }
        }

        /// <summary>Steps along the smear.</summary>
        private const int Steps = 22;

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] SmearInner = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearOuter = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearEdge = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearCoreIn = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearCoreOut = new Vector2[Steps + 1];
        private static readonly float[] SmearStrength = new float[Steps + 1];

        /// <summary>
        /// The smear: a band from the upper shaft out past the point, over the sweep from
        /// <paramref name="head"/> back to <paramref name="tail"/>, strongest at the head. First a
        /// dark band that shadows what is behind it, then a faint crimson light over it; then the
        /// edge along the point's own path, a band of blood red over what is behind (so it reads
        /// on a bright sky) with a thinner light down its middle (so it reads in the dark), cold
        /// at the head, as the eye is, and crimson after.
        /// </summary>
        private void DrawSmear(Player player, float head, float tail, float fade)
        {
            Vector2[] inner = SmearInner;
            Vector2[] outer = SmearOuter;
            Vector2[] edgeIn = SmearEdge;
            Vector2[] coreIn = SmearCoreIn;
            Vector2[] coreOut = SmearCoreOut;
            float[] strength = SmearStrength;

            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float a = AngleAt(MathHelper.Lerp(head, tail, k));
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);
                Vector2 u = a.ToRotationVector2();

                inner[i] = VisusGrip.ToWorld(player, hand + u * Inner);
                outer[i] = VisusGrip.ToWorld(player, hand + u * Outer);
                edgeIn[i] = VisusGrip.ToWorld(player, hand + u * (Outer - 9f));
                coreIn[i] = VisusGrip.ToWorld(player, hand + u * (Outer - 6f));
                coreOut[i] = VisusGrip.ToWorld(player, hand + u * (Outer - 1f));
                strength[i] = (float)Math.Pow(1f - k, 1.3f) * fade;
            }

            // The shadow.
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(18, 6, 14, 255) * (0.8f * strength[i]);
                Strip.Pair(inner[i], outer[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            // The crimson light in it.
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color red = new Color(120, 12, 30, 0) * (0.4f * strength[i]);
                Strip.Pair(inner[i], outer[i], red, red, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            // The edge: blood red over what is behind.
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color blood = new Color(110, 8, 22, 255) * (0.75f * strength[i]);
                Strip.Pair(edgeIn[i], outer[i], blood, blood, i / (float)Steps);
            }
            Strip.Draw(line.Value);

            // And light down the middle of it.
            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                Color hot = k < 0.25f
                    ? Color.Lerp(Cold, Crimson, k / 0.25f)
                    : Color.Lerp(Crimson, new Color(80, 6, 18), (k - 0.25f) / 0.75f);

                hot.A = 0;
                hot *= 0.9f * strength[i];

                Strip.Pair(coreIn[i], coreOut[i], hot, hot, k);
            }
            Strip.Draw(line.Value);
        }
    }
}
