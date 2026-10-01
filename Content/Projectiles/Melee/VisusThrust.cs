using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// Visus's thrust (left click): drawn back, driven out to its full reach, 7 tiles from the
    /// player, held a moment, drawn in again, over the item's use time (45 ticks, less with melee
    /// speed; ai[1] is it, from Visus.Shoot). The back hand pushes the butt through the front one,
    /// so the spear slides out through the grip rather than the arms growing.
    ///
    /// Each thrust carries the player 2 tiles forward, over most of the thrust, so it is a step
    /// and not a dash (<see cref="Lunge"/>). Only as much of it as the thrust is level: thrust
    /// straight up or down and it is no step at all.
    ///
    /// It hits along the shaft, from the front hand to the point, while it is out; each enemy
    /// once a thrust, through walls only where the player could have reached (ownerHitCheck), and
    /// it cuts grass on the way, as vanilla's spears do.
    ///
    /// Drawn by VisusLayer, among the player's layers; this draws only the streak the point leaves
    /// as it goes out (PreDraw, behind the player).
    /// </summary>
    public class VisusThrust : ModProjectile
    {
        public override string Texture => VisusGrip.SpearTexture;

        // ---- the timing, as shares of the thrust -------------------------------------------------

        /// <summary>Drawn back by here.</summary>
        private const float BackAt = 0.16f;

        /// <summary>At full reach by here.</summary>
        private const float OutAt = 0.40f;

        /// <summary>Held out until here, then drawn in.</summary>
        private const float HeldTo = 0.58f;

        /// <summary>It hits from here to here.</summary>
        private const float HitsFrom = 0.20f, HitsTo = 0.62f;

        /// <summary>The step forward runs from here, for this much of the thrust.</summary>
        private const float StepFrom = 0.12f, StepFor = 0.48f;

        // ---- the reach ---------------------------------------------------------------------------

        /// <summary>
        /// Where the front hand holds the shaft, in texels from the butt: at rest, drawn back, and
        /// driven out. Out, the point is 112 pixels (7 tiles) from the player's centre when the
        /// thrust is level: the hand is 6 in front of the centre and the point 106 past the hand.
        /// </summary>
        private const float GripRest = 56f, GripBack = 64f, GripOut = 14f;

        /// <summary>How far each thrust carries the player, in pixels, when it is level.</summary>
        public const float Step = 32f;

        /// <summary>The fastest the step may carry the player, pixels a tick.</summary>
        private const float StepSpeedCap = 4f;

        /// <summary>How thick the thrust is to what it hits, in pixels.</summary>
        private const float Width = 14f;

        private static readonly Piece[] Grip =
        {
            new Piece(Ease.SineOut, 0f, GripRest, GripBack - GripRest),
            new Piece(Ease.PolyOut, BackAt, GripBack, GripOut - GripBack, 4),
            new Piece(Ease.SineInOut, OutAt, GripOut, 2f),
            new Piece(Ease.PolyInOut, HeldTo, GripOut + 2f, GripRest - GripOut - 2f, 2),
        };

        private static readonly Color Crimson = new Color(190, 28, 48);
        private static readonly Color Cold = new Color(185, 200, 255);
        private static readonly Color Soot = new Color(16, 8, 14);

        private static Asset<Texture2D> streak;

        private Player Owner => Main.player[Projectile.owner];

        private float Duration => Math.Max(1f, Projectile.ai[1]);

        private ref float Timer => ref Projectile.localAI[0];

        private bool started;

        /// <summary>The thrust's angle from straight ahead, upright (VisusGrip), set when it starts.</summary>
        private float angle;

        private int facing;

        /// <summary>The front hand this tick, upright.</summary>
        private Vector2 hand;

        /// <summary>The point this tick and the last few, in the world, newest first.</summary>
        private readonly Vector2[] points = new Vector2[5];

        /// <summary>How far the step has carried the player so far, and where they were last tick (owner only).</summary>
        private float stepped;
        private float lastX;

        public override void Load()
        {
            if (!Main.dedServ)
                streak = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Streak");
        }

        public override void Unload()
        {
            streak = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
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

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.ModItem is not Visus)
            {
                Projectile.Kill();
                return;
            }

            if (!started)
                Start(player);

            float before = Timer / Duration;
            Timer++;
            float t = Timer / Duration;

            if (t >= 1f)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            player.ChangeDir(facing);

            Player.CompositeArmStretchAmount stretch = t < BackAt + 0.02f
                ? Player.CompositeArmStretchAmount.None
                : t < HitsTo ? Player.CompositeArmStretchAmount.Full
                : t < 0.8f ? Player.CompositeArmStretchAmount.ThreeQuarters
                : Player.CompositeArmStretchAmount.Quarter;

            float grip = Easing.At(t, Grip);
            int eye = player.GetModPlayer<VisusPose>().RestingEye;

            // A cold glance off the eye as the point arrives.
            float glint = Math.Max(0f, 1f - Math.Abs(t - OutAt) / 0.1f);

            hand = VisusGrip.Hold(player, angle, grip, stretch, stretch, eye, glint * 0.8f);

            for (int i = points.Length - 1; i > 0; i--)
                points[i] = points[i - 1];

            points[0] = VisusGrip.On(player, angle, hand, grip, VisusGrip.Point.X, VisusGrip.Point.Y);

            Projectile.Center = points[0];
            Projectile.direction = Projectile.spriteDirection = facing;

            if (Projectile.owner == Main.myPlayer)
                Lunge(player, t);

            if (!Main.dedServ)
                Effects(player, before, t);
        }

        private void Start(Player player)
        {
            started = true;

            Vector2 aim = Projectile.velocity.SafeNormalize(new Vector2(player.direction, 0f));

            facing = aim.X > 0f ? 1 : aim.X < 0f ? -1 : player.direction;
            angle = (float)Math.Atan2(aim.Y * player.gravDir, Math.Abs(aim.X));

            lastX = player.position.X;

            hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.None, angle);
            Vector2 point = VisusGrip.On(player, angle, hand, GripRest, VisusGrip.Point.X, VisusGrip.Point.Y);

            for (int i = 0; i < points.Length; i++)
                points[i] = point;
        }

        /// <summary>
        /// The step. Where the player should be by the end of the next tick, along the ground, on
        /// a curve with nothing moving at either end, and the speed that gets them there, set only
        /// when it is more than they already have forward: a thrust never slows a run and never
        /// holds anyone back. It measures where the player really went, so ground friction, a
        /// held key and a wall all come out in the next tick's sum, and a wall that ends does not
        /// fling anyone (the speed is capped).
        /// </summary>
        private void Lunge(Player player, float t)
        {
            stepped += (player.position.X - lastX) * facing;
            lastX = player.position.X;

            if (t < StepFrom || t > StepFrom + StepFor || player.mount.Active || player.grappling[0] >= 0)
                return;

            float reach = Step * Math.Abs((float)Math.Cos(angle));

            if (reach < 1f)
                return;

            float next = reach * Easing.Smoother((t + 1f / Duration - StepFrom) / StepFor);

            // Ground friction takes this off before the player moves, when nothing is held.
            float friction = player.velocity.Y == 0f ? player.runSlowdown : 0f;
            float speed = Math.Min(next - stepped + friction, StepSpeedCap);

            if (speed > player.velocity.X * facing)
                player.velocity.X = speed * facing;
        }

        private static bool Crossed(float mark, float before, float t) => before < mark && t >= mark;

        private void Effects(Player player, float before, float t)
        {
            Vector2 point = points[0];
            Vector2 way = angle.ToRotationVector2();
            way = new Vector2(way.X * facing, way.Y * player.gravDir);

            if (Crossed(BackAt, before, t))
                SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = -0.35f, Volume = 0.8f, PitchVariance = 0.1f }, point);

            // Going out: a little soot off the point.
            if (t > BackAt && t < OutAt && Main.rand.NextBool(2))
            {
                PADParticleHandler.SpawnParticle(new FlameSmoke(point - way * Main.rand.NextFloat(4f, 16f),
                    -way * Main.rand.NextFloat(0.3f, 0.9f) + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    Soot, Main.rand.Next(26, 40), 10f, 26f, 0.42f));
            }

            // Arrived: a glint on the point, a spit of crimson off it.
            if (Crossed(OutAt, before, t))
            {
                PADParticleHandler.SpawnParticle(new CrownGlint(point, 22f, 16, Cold));

                for (int i = 0; i < 3; i++)
                {
                    float length = Main.rand.NextFloat(16f, 26f);

                    PADParticleHandler.SpawnParticle(new SparkShard(point,
                        way.RotatedByRandom(0.35f) * Main.rand.NextFloat(2.5f, 5f),
                        Color.Lerp(Crimson, Cold, Main.rand.NextFloat(0.3f)), length * 0.5f, 18f, length, Main.rand.Next(12, 18)));
                }
            }

            if (t > BackAt && t < HeldTo)
                Lighting.AddLight(point, Crimson.ToVector3() * 0.25f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float t = Timer / Duration;

            if (!started || t < HitsFrom || t > HitsTo)
                return false;

            float collision = 0f;
            Vector2 from = VisusGrip.ToWorld(Owner, hand);

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), from, points[0], Width, ref collision);
        }

        public override void CutTiles()
        {
            float t = Timer / Duration;

            if (!started || t < HitsFrom || t > HitsTo)
                return;

            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(VisusGrip.ToWorld(Owner, hand), points[0], Width, DelegateMethods.CutTiles);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            Vector2 from = VisusGrip.ToWorld(Owner, hand);
            Vector2 way = (points[0] - from).SafeNormalize(new Vector2(facing, 0f));
            Vector2 at = Vector2.Clamp(points[0], target.TopLeft, target.BottomRight);

            for (int i = 0; i < 4; i++)
            {
                float length = Main.rand.NextFloat(18f, 30f);

                PADParticleHandler.SpawnParticle(new SparkShard(at,
                    way.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 6.5f),
                    Crimson, length * 0.5f, 18f, length, Main.rand.Next(12, 20)));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Crimson * 0.8f, 0.45f, 14));
        }

        /// <summary>
        /// The streak the point leaves going out: along the way it went over the last few ticks,
        /// a dark smear under a crimson thread with a pale core, so it reads on a bright sky as
        /// shadow and in a cave as light. Gone once the point stops.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            float t = Timer / Duration;

            if (!started || streak == null || t < BackAt || t > HeldTo)
                return false;

            Vector2 head = points[0];
            Vector2 run = head - points[points.Length - 1];
            float length = run.Length();

            // Only going out: drawn back or drawn in, the point leaves nothing.
            Vector2 way = angle.ToRotationVector2();
            way = new Vector2(way.X * facing, way.Y * Owner.gravDir);

            if (length < 6f || Vector2.Dot(run, way) <= 0f)
                return false;

            Texture2D texture = streak.Value;
            float rotation = run.ToRotation();
            Vector2 origin = new Vector2(texture.Width, texture.Height * 0.5f);
            Vector2 at = head - Main.screenPosition;
            float strength = MathHelper.Clamp(length / 40f, 0f, 1f);
            float along = Math.Min(length, 96f) / texture.Width;

            // Darkens what is behind (alpha), then adds light over it (alpha 0).
            Main.EntitySpriteDraw(texture, at, null, new Color(14, 6, 12, 255) * (0.55f * strength), rotation, origin,
                new Vector2(along * 1.1f, 16f / texture.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, at, null, new Color(Crimson.R, Crimson.G, Crimson.B, 0) * (0.9f * strength), rotation, origin,
                new Vector2(along, 8f / texture.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, at, null, new Color(255, 225, 230, 0) * (0.55f * strength), rotation, origin,
                new Vector2(along * 0.7f, 3f / texture.Height), SpriteEffects.None, 0);

            return false;
        }
    }
}
