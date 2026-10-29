using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// One of Deep Roots' roots: the game's Blood Thorn thorn (projectile 756, 1.4.4: its AI 157, its draw in
    /// Main.DrawProj, its hit line in Projectile.Colliding), made of root.
    ///
    /// ai[0] is its age. It is made below zero and waits in its caster, unseen and harmless, until the count reaches
    /// zero; then, on its caster's machine, it finds where it comes up (<see cref="Spot"/>), goes there pointed at the
    /// cursor, and is sent to the others (ai[2] 1: placed). ai[1] is its length, a share of the texture's 200 pixels.
    /// From there as the thorn: it grows in over <see cref="GrowTicks"/> ticks (its opacity and its size together),
    /// stands, withdraws from <see cref="FadeAt"/> and is gone at <see cref="GoneAt"/>. It hurts along a line from
    /// where it came up, as long as it is and 22 pixels wide at full size, through <see cref="Pierce"/> enemies, each
    /// no more than once every 25 ticks; after its last it stays to the end without hurting
    /// (stopsDealingDamageAfterPenetrateHits, as the game keeps the thorn). Grass and vines along it are cut.
    /// </summary>
    public class DeepRoot : ModProjectile
    {
        public const int Pierce = 3;

        /// <summary>Its length, the least and the most, as a share of the picture (the Blood Thorn's: 0.6 to 1.1).</summary>
        public const float ShortScale = 0.5f, LongScale = 0.85f;

        public const int GrowTicks = 20, FadeAt = 30, GoneAt = 35;

        /// <summary>The picture's length along the root, and how far around a spot it looks for the ground.</summary>
        public const float Length = 200f;
        public const float Width = 22f;
        public const float SpotReach = 200f;

        public const int Frames = 6;

        private static readonly Color Bark = new Color(52, 34, 20);

        private int Age => (int)Projectile.ai[0];
        private bool Placed => Projectile.ai[2] == 1f;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = Frames;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = Pierce;
            Projectile.stopsDealingDamageAfterPenetrateHits = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 25;
            Projectile.timeLeft = 600;
        }

        public override bool? CanDamage() => Placed && Age < FadeAt ? null : false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!Placed)
            {
                Projectile.Center = owner.MountedCenter;

                if (Age < 0)
                {
                    Projectile.ai[0]++;
                    return;
                }

                // Its turn: only its caster's machine knows the cursor. The others wait to be told where.
                if (Projectile.owner != Main.myPlayer)
                    return;

                Grow(owner);
            }

            Projectile.ai[0]++;
            bool growing = Age < GrowTicks;
            bool fading = Age >= FadeAt;

            if (growing)
            {
                Projectile.Opacity = Math.Min(1f, Projectile.Opacity + 0.1f);
                Projectile.scale = Projectile.Opacity * Projectile.ai[1];
                Shed(2, 0.5f);
            }

            if (fading)
            {
                Projectile.Opacity = Math.Max(0f, Projectile.Opacity - 0.2f);
                Shed(2, 0.5f);
            }

            if (Age >= GoneAt)
                Projectile.Kill();
        }

        /// <summary>
        /// Comes up: where (<see cref="Spot"/>), pointed at the cursor as far as the caster can reach
        /// (Player.LimitPointToPlayerReachableArea, the Blood Thorn's limit), with the burst of earth and the sound.
        /// </summary>
        private void Grow(Player owner)
        {
            Vector2 target = Main.MouseWorld;
            owner.LimitPointToPlayerReachableArea(ref target);
            target += Main.rand.NextVector2Circular(8f, 8f);

            Point tile = Spot(owner, target);
            Vector2 at = tile.ToWorldCoordinates(Main.rand.Next(17), Main.rand.Next(17));

            Projectile.Center = at;
            Projectile.velocity = (target - at).SafeNormalize(-Vector2.UnitY) * 16f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.ai[2] = 1f;
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// Where a root comes up, for a root at <paramref name="target"/>: the Blood Thorn's choice (Player.FindSharpTearsSpot)
        /// and then the walls. A line from the caster toward the target is stopped at the first thing on the way
        /// (Collision.AimingLaserScan, three samples, 4 pixels wide); around that point, 6 tiles either side and 16 up
        /// and down, inside the world's edge, every solid block or platform within <see cref="SpotReach"/>, first those
        /// with an open side facing the point. None: every background wall there with no block in front of it, the
        /// same reach. None of either: the caster's own place, a little off.
        /// </summary>
        public static Point Spot(Player owner, Vector2 target)
        {
            Vector2 from = owner.Center;
            Collision.AimingLaserScan(from, target, 4f, 3, out Vector2 toward, out float[] samples);

            float clear = float.PositiveInfinity;

            for (int i = 0; i < samples.Length; i++)
                clear = Math.Min(clear, samples[i]);

            Vector2 met = from + toward.SafeNormalize(Vector2.Zero) * clear;
            Point point = met.ToTileCoordinates();

            Rectangle look = new Rectangle(point.X, point.Y, 1, 1);
            look.Inflate(6, 16);
            Rectangle world = new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY);
            world.Inflate(-40, -40);
            look = Rectangle.Intersect(look, world);

            List<Point> open = new List<Point>();
            List<Point> shut = new List<Point>();
            List<Point> walls = new List<Point>();

            for (int x = look.Left; x <= look.Right; x++)
            {
                for (int y = look.Top; y <= look.Bottom; y++)
                {
                    if (Vector2.Distance(met, new Vector2(x * 16 + 8, y * 16 + 8)) > SpotReach)
                        continue;

                    if (WorldGen.SolidTile2(x, y))
                    {
                        if (Opening(x, y, x > point.X, x < point.X, y > point.Y, y < point.Y))
                            open.Add(new Point(x, y));
                        else
                            shut.Add(new Point(x, y));

                        continue;
                    }

                    Tile tile = Main.tile[x, y];

                    if (tile.WallType > 0)
                        walls.Add(new Point(x, y));
                }
            }

            List<Point> pick = open.Count > 0 ? open : shut.Count > 0 ? shut : walls;

            if (pick.Count == 0)
                return (owner.Center.ToTileCoordinates().ToVector2() + Main.rand.NextVector2Square(-2f, 2f)).ToPoint();

            return pick[Main.rand.Next(pick.Count)];
        }

        /// <summary>The Blood Thorn's opening (Player.FindSharpTearsOpening): an open side on one of the sides allowed.</summary>
        private static bool Opening(int x, int y, bool left, bool right, bool up, bool down) =>
            left && !WorldGen.SolidTile(x - 1, y) || right && !WorldGen.SolidTile(x + 1, y)
            || up && !WorldGen.SolidTile(x, y - 1) || down && !WorldGen.SolidTile(x, y + 1);

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            Projectile.frame = Main.rand.Next(Frames);
        }

        /// <summary>Whether this machine has made its burst yet.</summary>
        private bool burst;

        /// <summary>When it is placed, wherever it is seen: the burst of earth and leaf, and the sound.</summary>
        public override void PostAI()
        {
            if (!Placed || burst)
                return;

            burst = true;

            for (int i = 0; i < 18; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f), Main.rand.NextBool(3) ? DustID.Grass : DustID.Dirt,
                    Projectile.velocity * MathHelper.Lerp(0.2f, 0.6f, Main.rand.NextFloat()) + Main.rand.NextVector2Circular(1f, 1f));
                dust.scale = 0.8f + Main.rand.NextFloat() * 0.5f;
            }

            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.35f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item60 with { Pitch = -0.55f, Volume = 0.55f }, Projectile.Center);
        }

        /// <summary>Earth and bits of leaf off it while it grows and withdraws, as the thorn sheds blood.</summary>
        private void Shed(int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f), Main.rand.NextBool(4) ? DustID.Grass : DustID.Dirt,
                    Projectile.velocity * MathHelper.Lerp(0.2f, 0.5f, Main.rand.NextFloat()) * speed);
                dust.velocity += Main.rand.NextVector2Circular(0.5f, 0.5f);
                dust.scale = 0.8f + Main.rand.NextFloat() * 0.5f;
            }
        }

        private Vector2 Tip => Projectile.Center + Projectile.velocity.SafeNormalize(-Vector2.UnitY) * Length * Projectile.scale;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float at = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Tip, Width * Projectile.scale, ref at);
        }

        public override void CutTiles()
        {
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;
            Utils.PlotTileLine(Projectile.Center, Tip, Width * Projectile.scale, DelegateMethods.CutTiles);
        }

        public override bool? CanCutTiles() => Placed && Age < FadeAt;

        /// <summary>
        /// As the game draws its thorn (Main.DrawProj, type 756): a dark smear behind (Extra 98) tinted the colour of
        /// bark, then the root from its base, one of six, longer as it grows and drawn back in as it withdraws.
        /// </summary>
        public override bool PreDraw(ref Color lightColor)
        {
            if (!Placed)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(1, Frames, 0, Projectile.frame);
            Vector2 origin = new Vector2(16f, frame.Height / 2f);
            Color colour = Projectile.GetAlpha(Color.Lerp(lightColor, Color.Black, 0.15f));
            Vector2 scale = new Vector2(Projectile.scale);
            scale.Y *= Utils.GetLerpValue(GoneAt, FadeAt, Projectile.ai[0], clamped: true);

            Vector4 lit = lightColor.ToVector4() * Bark.ToVector4();
            Texture2D smear = TextureAssets.Extra[98].Value;
            Vector2 at = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

            Main.EntitySpriteDraw(smear, at - Projectile.velocity * Projectile.scale * 0.5f, null, Projectile.GetAlpha(new Color(lit.X, lit.Y, lit.Z, lit.W)),
                Projectile.rotation + MathHelper.PiOver2, smear.Size() / 2f, Projectile.scale * 0.9f, SpriteEffects.None);
            Main.EntitySpriteDraw(texture, at, frame, colour, Projectile.rotation, origin, scale, SpriteEffects.None);
            return false;
        }
    }
}
