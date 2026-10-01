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
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The Flesh Maiden brought down from overhead (right click): Visus's blow (VisusChop) in
    /// 50 ticks, less with melee speed (ai[1], the item's use time for it), for 2.5 times the
    /// damage (FleshMaiden.Shoot multiplies it). ai[0] is the side it faces. The next one waits
    /// a second from the end of this one (<see cref="OnKill"/>, FleshMaidenState.Rest).
    ///
    /// The swing is VisusChop's, in pieces: lifted up and back, hung a beat, brought down with
    /// all the weight at the bottom, the blade leading. The ground stops it the same way: from
    /// the moment it starts down, the blade's edge and the point are tried against solid tiles,
    /// the angle it met them at is halved out between last tick and this, and the head stays
    /// there; the floor heaves, blood and dirt go up, and it stops hitting two ticks later. It
    /// hits along the shaft and the head over the whole sweep since last tick, and the first
    /// thing it hits holds it still for four ticks.
    ///
    /// Where it lands it throws its wielder back, 10 tiles and 2 up, bleeding
    /// (FleshMaidenState.Throw); with nothing under it, at the bottom of the blow. Once a smash.
    ///
    /// Drawn in the hands by VisusLayer; this draws what it leaves (PreDraw, behind the player):
    /// three shadows of itself along the way it came, and the smear (Strip).
    /// </summary>
    public class FleshMaidenSmash : ModProjectile
    {
        public override string Texture => PolearmArt.FleshMaiden.SheetPath;

        // ---- the timing, as shares of the swing --------------------------------------------------

        private const float TopAt = 0.30f;
        private const float DownAt = 0.42f;
        private const float ThroughAt = 0.62f;
        private const float HitsTo = 0.70f;

        /// <summary>How much of the swing the smear trails behind the head.</summary>
        private const float Trail = 0.1f;

        /// <summary>Held near the butt, for the weight.</summary>
        private const float Grip = 32f;

        /// <summary>Ticks the first hit holds it still.</summary>
        private const int Stall = 4;

        /// <summary>Degrees from straight ahead, down positive: lifted well past upright, hung, brought down to 60 below level and through.</summary>
        private static readonly Piece[] Swing =
        {
            new Piece(Ease.PolyOut, 0f, -25f, -95f, 2),
            new Piece(Ease.SineInOut, TopAt, -120f, -10f),
            new Piece(Ease.PolyIn, DownAt, -130f, 190f, 3),
            new Piece(Ease.PolyOut, ThroughAt, 60f, 6f, 2),
        };

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

        private int stall;
        private bool struck;

        /// <summary>Whether it has thrown its wielder back yet, and the tick it did.</summary>
        private bool threw;
        private float threwTick;

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
            Projectile.width = 30;
            Projectile.height = 30;
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

        /// <summary>Where the swing has it at <paramref name="progress"/>, radians, stopped by the ground if it was.</summary>
        private float AngleAt(float progress)
        {
            float a = MathHelper.ToRadians(Easing.At(progress, Swing));
            return float.IsNaN(ground) ? a : Math.Min(a, ground);
        }

        private static Player.CompositeArmStretchAmount StretchAt(float progress) =>
            progress < 0.25f ? Player.CompositeArmStretchAmount.ThreeQuarters : Player.CompositeArmStretchAmount.Full;

        private static float GlowAt(float progress) =>
            progress < TopAt ? progress / TopAt : progress < ThroughAt ? 1f : Math.Max(0f, 1f - (progress - ThroughAt) / 0.2f);

        private bool Grounded => !float.IsNaN(ground);

        /// <summary>
        /// It hits from the start of the blow until it is through, or two ticks after it throws
        /// its wielder back (where the ground stops it, or at the bottom of the blow).
        /// </summary>
        private bool Hitting
        {
            get
            {
                float t = Timer / Duration;
                return started && t >= DownAt && t <= HitsTo && (!threw || Timer - threwTick <= 2f);
            }
        }

        /// <summary>However the smash ends, through or cut short, the next one waits FleshMaiden.RestTicks from now.</summary>
        public override void OnKill(int timeLeft)
        {
            Owner.GetModPlayer<FleshMaidenState>().Rest = FleshMaiden.RestTicks;
        }

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.ModItem is not FleshMaiden)
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
            player.GetModPlayer<FleshMaidenState>().Idle = 0;

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

            // Once its wielder is thrown the blow is spent: nothing it passes over after is a landing.
            if (!Grounded && !threw && t > DownAt && angle > MathHelper.ToRadians(-30f))
                FindGround(player);

            // Nothing under it: the throw comes at the bottom of the blow instead.
            if (!threw && t >= ThroughAt)
                ThrowBack(player);

            Pose(player, t);

            if (!Main.dedServ)
                Effects(player, before, t);
        }

        private void Pose(Player player, float t)
        {
            Player.CompositeArmStretchAmount stretch = StretchAt(t);
            Vector2 hand = VisusGrip.Hold(player, angle, Grip, stretch, stretch, VisusGrip.Open, GlowAt(t), PolearmArt.FleshMaiden);

            Projectile.Center = FleshMaidenGrip.On(player, angle, hand, Grip, FleshMaidenGrip.Eye, false);
            Projectile.direction = Projectile.spriteDirection = Facing;
        }

        private void FindGround(Player player)
        {
            Player.CompositeArmStretchAmount stretch = StretchAt(1f);

            if (!FleshMaidenGrip.InGround(player, angle, stretch, Grip, false, out _))
                return;

            float met = FleshMaidenGrip.InGround(player, previousAngle, stretch, Grip, false, out _)
                ? previousAngle
                : FleshMaidenGrip.Meet(player, previousAngle, angle, stretch, Grip, false);

            ground = met;
            groundTick = Timer;
            angle = met;

            FleshMaidenGrip.InGround(player, met, stretch, Grip, false, out Vector2 contact);

            if (!Main.dedServ)
                Land(player, contact);

            ThrowBack(player);
        }

        /// <summary>Throws the wielder back, once, on every machine this runs on (only the owner's copy moves them).</summary>
        private void ThrowBack(Player player)
        {
            if (threw)
                return;

            threw = true;
            threwTick = Timer;
            player.GetModPlayer<FleshMaidenState>().Throw(Facing);
        }

        /// <summary>Which way the head is travelling at <paramref name="a"/>, in the world: across the shaft, toward the blade.</summary>
        private Vector2 Travel(Player player, float a)
        {
            Vector2 u = a.ToRotationVector2();
            return new Vector2(-u.Y * Facing, u.X * player.gravDir);
        }

        private static bool Crossed(float mark, float before, float t) => before < mark && t >= mark;

        private void Effects(Player player, float before, float t)
        {
            Vector2 hand = VisusGrip.FrontHand(StretchAt(t), angle);
            Vector2 point = FleshMaidenGrip.On(player, angle, hand, Grip, FleshMaidenGrip.Point, false);
            Vector2 eye = Projectile.Center;

            if (before <= 0f)
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.8f, Volume = 0.5f }, eye);

            // Lifted: it drips.
            if (t < DownAt && Timer % 3f == 0f)
            {
                Vector2 from = Vector2.Lerp(eye, point, Main.rand.NextFloat(0.1f, 0.7f));
                PADParticleHandler.SpawnParticle(new BloodDroplet(from, new Vector2(0f, 0.5f * player.gravDir) + Main.rand.NextVector2Circular(0.4f, 0.2f),
                    Main.rand.Next(26, 42), Main.rand.NextFloat(0.45f, 0.8f), FleshMaidenGrip.Blood));
            }

            if (Crossed(TopAt, before, t))
                PADParticleHandler.SpawnParticle(new CrownGlint(point, 26f, 20, FleshMaidenGrip.Bone));

            if (Crossed(DownAt, before, t))
            {
                SoundEngine.PlaySound(SoundID.DD2_SonicBoomBladeSlash with { Pitch = -0.6f, Volume = 0.8f }, eye);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.5f, Volume = 0.6f }, eye);
            }

            // Coming down: blood flung off its edge.
            if (t > DownAt && t < ThroughAt && !Grounded)
            {
                Vector2 travel = Travel(player, angle);

                for (int i = 0; i < 2; i++)
                {
                    Vector2 from = Vector2.Lerp(point, eye, Main.rand.NextFloat());
                    PADParticleHandler.SpawnParticle(new BloodDroplet(from, -travel * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        Main.rand.Next(24, 40), Main.rand.NextFloat(0.5f, 0.9f), FleshMaidenGrip.Blood));
                }
            }

            float glow = GlowAt(t);

            if (glow > 0f)
                Lighting.AddLight(eye, FleshMaidenGrip.Meat.ToVector3() * (0.4f * glow));
        }

        /// <summary>The smash lands on the ground.</summary>
        private void Land(Player player, Vector2 contact)
        {
            Vector2 travel = Travel(player, ground);

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.35f, Volume = 0.95f }, contact);
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = 0.3f, Volume = 0.35f }, contact);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.55f, Volume = 0.7f }, contact);

            if (Projectile.owner == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(6f, 14);

            // The face it hit looks back the way the blow came.
            GroundWave.Strike(contact, -travel, 11f, 4, 5f, 24);
            Collision.HitTiles(contact - new Vector2(8f), travel * 6f, 16, 16);

            for (int i = 0; i < 12; i++)
            {
                Vector2 velocity = (-travel).RotatedByRandom(1.1f) * Main.rand.NextFloat(2f, 6f);
                PADParticleHandler.SpawnParticle(new BloodDroplet(contact, velocity, Main.rand.Next(28, 48),
                    Main.rand.NextFloat(0.6f, 1.05f), FleshMaidenGrip.Blood));
            }

            SparkShard.Spray(contact, FleshMaidenGrip.Bone, 7, 4.5f, 28f, 18, falls: true, palette: FleshMaidenGrip.Spatter);
            PADParticleHandler.SpawnParticle(new BloomGeneric(contact, Vector2.Zero, FleshMaidenGrip.Meat * 0.7f, 0.7f, 18));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;

            // Last tick's angle to this one's, so nothing between is stepped over: it covers
            // more than thirty degrees a tick at the bottom.
            const int Samples = 8;

            for (int i = 0; i <= Samples; i++)
            {
                float a = MathHelper.Lerp(previousAngle, angle, i / (float)Samples);
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);

                if (FleshMaidenGrip.Hits(player, targetHitbox, a, hand, Grip))
                    return true;
            }

            return false;
        }

        public override void CutTiles()
        {
            if (Hitting)
                FleshMaidenGrip.Cut(Owner, angle, VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, angle), Grip);
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
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.3f, Volume = 0.8f }, at);
                    Main.LocalPlayer.PIS().BumpScreenShake(4.5f, 10);
                    PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, FleshMaidenGrip.Meat * 0.8f, 0.6f, 16));
                }
            }

            if (Main.dedServ)
                return;

            FleshMaidenGrip.Bleed(at, travel, damageDone);
            SparkShard.Spray(at, FleshMaidenGrip.Blood, 5, 4f, 30f, 18, falls: true, palette: FleshMaidenGrip.Spatter);
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

        /// <summary>Three shadows of it along the way it came, darkening what is behind them.</summary>
        private void DrawShadows(Player player, float t, float fade)
        {
            PolearmArt art = PolearmArt.FleshMaiden;
            bool flip = (player.direction < 0) != (player.gravDir < 0f);

            for (int i = 3; i >= 1; i--)
            {
                float p = t - 0.03f * i;

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

                Main.EntitySpriteDraw(texture, at, place.Frame, new Color(34, 6, 14, 255) * strength, place.Rotation,
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
        /// The smear over the sweep from <paramref name="tail"/> to <paramref name="head"/>, from
        /// the collar out past the point, strongest at the head: a dark band over what is behind,
        /// raw red light in it, then along the point's own path a band of blood with a line of
        /// pale bone light down its middle.
        /// </summary>
        private void DrawSmear(Player player, float head, float tail, float fade)
        {
            float inner = FleshMaidenGrip.HeadFrom - Grip - 8f;
            float outer = FleshMaidenGrip.Point.X - Grip + 4f;

            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                float a = AngleAt(MathHelper.Lerp(head, tail, k));
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);
                Vector2 u = a.ToRotationVector2();

                SmearInner[i] = VisusGrip.ToWorld(player, hand + u * inner);
                SmearOuter[i] = VisusGrip.ToWorld(player, hand + u * outer);
                SmearEdge[i] = VisusGrip.ToWorld(player, hand + u * (outer - 10f));
                SmearCoreIn[i] = VisusGrip.ToWorld(player, hand + u * (outer - 6f));
                SmearCoreOut[i] = VisusGrip.ToWorld(player, hand + u * (outer - 1f));
                SmearStrength[i] = (float)Math.Pow(1f - k, 1.3f) * fade;
            }

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dark = new Color(30, 4, 10, 255) * (0.8f * SmearStrength[i]);
                Strip.Pair(SmearInner[i], SmearOuter[i], dark, dark, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color red = new Color(150, 20, 34, 0) * (0.45f * SmearStrength[i]);
                Strip.Pair(SmearInner[i], SmearOuter[i], red, red, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color blood = new Color(120, 8, 18, 255) * (0.75f * SmearStrength[i]);
                Strip.Pair(SmearEdge[i], SmearOuter[i], blood, blood, i / (float)Steps);
            }
            Strip.Draw(line.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                float k = i / (float)Steps;
                Color hot = k < 0.3f
                    ? Color.Lerp(FleshMaidenGrip.Bone, FleshMaidenGrip.Meat, k / 0.3f)
                    : Color.Lerp(FleshMaidenGrip.Meat, new Color(80, 6, 18), (k - 0.3f) / 0.7f);

                hot.A = 0;
                hot *= 0.9f * SmearStrength[i];

                Strip.Pair(SmearCoreIn[i], SmearCoreOut[i], hot, hot, k);
            }
            Strip.Draw(line.Value);
        }
    }
}
