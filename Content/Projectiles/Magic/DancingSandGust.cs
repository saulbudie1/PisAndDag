using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Dancing Sand's spray (Items.Mage.Spell.DancingSand): the breath of sand blown off the staff's head at the
    /// mouth (ColdCallHold.HeadOf), toward the cursor, a cone <see cref="Spread"/> wide and <see cref="Reach"/>
    /// long, cut short by the ground. One for each caster, kept up while the casts come (<see cref="Keep"/>: ai[0]
    /// the ticks left, ai[1] the way it blows, turned toward the cursor by the one casting and sent to the others).
    ///
    /// It hurts whatever is in the cone and in the open from the mouth (<see cref="Colliding"/>), each thing on a
    /// count of its own (local immunity), the count set at each hit by how near the thing is
    /// (<see cref="Cooldown"/>): 7 ticks within 5 tiles, 40 at 30, evenly between.
    ///
    /// What is seen: grains of sand thrown off the head along the cone, fast, slowing and wavering as they go and
    /// turning about one another where they give out (SandGrain), bouncing off the ground; a puff of the game's sand
    /// at the mouth. Nothing drawn of its own.
    /// </summary>
    public class DancingSandGust : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Items/Mage/Spell/DancingSand";

        /// <summary>How far it reaches: thirty tiles.</summary>
        public const float Reach = 30f * 16f;

        /// <summary>The cone: fifteen degrees across, half of it either side of the way it blows.</summary>
        public const float Spread = MathHelper.Pi / 12f;

        /// <summary>The count between one hit and the next on the same thing: this near and nearer, the fewest; this far, the most.</summary>
        public const float NearTiles = 5f, FarTiles = 30f;
        public const int NearCooldown = 7, FarCooldown = 40;

        /// <summary>How fast the one casting turns the spray toward the cursor, radians a tick: quick, but a sweep is seen as one.</summary>
        private const float Turn = 0.3f;

        /// <summary>The ticks between one hit and the next on something this many tiles off.</summary>
        public static int Cooldown(float tiles) =>
            tiles <= NearTiles ? NearCooldown
            : (int)Math.Round(MathHelper.Lerp(NearCooldown, FarCooldown, MathHelper.Clamp((tiles - NearTiles) / (FarTiles - NearTiles), 0f, 1f)));

        /// <summary>The grains' colours, darkest to palest.</summary>
        internal static readonly Color[] Sand =
        {
            new Color(158, 112, 58), new Color(204, 160, 92), new Color(226, 192, 124), new Color(242, 218, 160),
        };

        /// <summary>How far the spray goes this tick before the ground stops it.</summary>
        private float open = Reach;

        private int lastSent;

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = NearCooldown;
            Projectile.netImportant = true;
            Projectile.hide = true;
        }

        /// <summary>
        /// The spray kept up for this player for so many ticks more, blowing this way, at this damage: the one that is
        /// up given the new count and numbers, or a new one.
        /// </summary>
        public static void Keep(Player player, IEntitySource source, float ticks, float aim, int damage, float knockback)
        {
            int type = ModContent.ProjectileType<DancingSandGust>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == type)
                {
                    p.ai[0] = ticks;
                    p.damage = damage;
                    p.knockBack = knockback;

                    // As ColdCallHold's: outside its own update, netUpdate2 is the one the game keeps.
                    p.netUpdate2 = true;
                    return;
                }
            }

            Projectile.NewProjectile(source, ColdCallHold.HeadOf(player), Vector2.Zero, type, damage, knockback, player.whoAmI, ticks, aim);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem?.ModItem is not Catalyst)
            {
                Projectile.Kill();
                return;
            }

            // The casts keep it up; the owner puts it away when they stop, everyone else when told (or long after).
            Projectile.ai[0]--;

            if (Projectile.ai[0] < 0f && (Projectile.owner == Main.myPlayer || Projectile.ai[0] < -30f))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = ColdCallHold.HeadOf(player);
            Projectile.velocity = Vector2.Zero;

            // The one casting turns it toward the cursor, and tells the others now and then as it goes.
            if (Projectile.owner == Main.myPlayer)
            {
                float want = (Main.MouseWorld - Projectile.Center).SafeNormalize(Projectile.ai[1].ToRotationVector2()).ToRotation();
                float now = Projectile.ai[1];
                float step = MathHelper.Clamp(MathHelper.WrapAngle(want - now), -Turn, Turn);

                Projectile.ai[1] = MathHelper.WrapAngle(now + step);

                if (Math.Abs(step) > 0.01f && ++lastSent >= 6)
                {
                    lastSent = 0;
                    Projectile.netUpdate = true;
                }
            }

            float aim = Projectile.ai[1];
            Vector2 way = aim.ToRotationVector2();

            // Blown the way the caster faces: they turn to it.
            player.ChangeDir(way.X >= 0f ? 1 : -1);
            Projectile.rotation = aim;

            // How far it goes before the ground stops it, down its middle.
            open = Reach;

            for (float d = 12f; d <= Reach; d += 8f)
            {
                Point at = (Projectile.Center + way * d).ToTileCoordinates();

                if (WorldGen.SolidTile(at.X, at.Y))
                {
                    open = d;
                    break;
                }
            }

            Lighting.AddLight(Projectile.Center + way * 10f, new Vector3(0.22f, 0.17f, 0.09f));

            if (!Main.dedServ)
                Spray(way);
        }

        /// <summary>The grains thrown off the head this tick, along the cone; and a little of the game's sand at the mouth.</summary>
        private void Spray(Vector2 way)
        {
            Vector2 mouth = Projectile.Center + way * 4f;

            for (int i = 0; i < 6; i++)
            {
                float angle = Projectile.ai[1] + Main.rand.NextFloat(-Spread, Spread) * 0.5f;
                float speed = Main.rand.NextFloat(20f, 26f);
                Vector2 velocity = angle.ToRotationVector2() * speed;

                // Lives as long as it takes to slow to where the spray ends (or to the ground first: it stops there itself).
                int life = Main.rand.Next(30, 40);

                PADParticleHandler.SpawnParticle(new SandGrain(mouth + Main.rand.NextVector2Circular(2f, 2f), velocity, Sand[Main.rand.Next(Sand.Length)],
                    life, Main.rand.NextFloat(1f, 2f)));
            }

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(mouth, DustID.Sand, way.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f), 80, default, Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
            }
        }

        /// <summary>
        /// In the cone and in the open: its nearest point within reach (and short of the ground down the middle), its
        /// middle within the cone's half-width of the way it blows (and a little more for a big thing, by how wide it
        /// looks from the mouth), and nothing solid between.
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 from = Projectile.Center;
            float distance = NearestDistance(from, targetHitbox);

            if (distance > Math.Max(open, 48f))
                return false;

            Vector2 to = targetHitbox.Center.ToVector2() - from;
            float off = Math.Abs(MathHelper.WrapAngle(to.ToRotation() - Projectile.ai[1]));
            float wide = (float)Math.Atan2(Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f, Math.Max(to.Length(), 1f));

            if (off > Spread * 0.5f + wide)
                return false;

            return Collision.CanHitLine(from, 1, 1, targetHitbox.Center.ToVector2(), 1, 1);
        }

        /// <summary>The count before this thing can be hit again, by how near it is: set before the hit, read by the game after it.</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            Projectile.localNPCHitCooldown = Cooldown(NearestDistance(Projectile.Center, target.Hitbox) / 16f);
            modifiers.HitDirectionOverride = Math.Cos(Projectile.ai[1]) >= 0 ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            // Sand thrown back off what it hit.
            Vector2 at = target.Hitbox.ClosestPointInRect(Projectile.Center);
            Vector2 back = (Projectile.Center - at).SafeNormalize(-Vector2.UnitX);

            for (int i = 0; i < 4; i++)
            {
                PADParticleHandler.SpawnParticle(new SandGrain(at, back.RotatedByRandom(1.2f) * Main.rand.NextFloat(2f, 5f),
                    Sand[Main.rand.Next(Sand.Length)], Main.rand.Next(14, 24), 1f));
            }
        }

        private static float NearestDistance(Vector2 from, Rectangle box) => Vector2.Distance(from, box.ClosestPointInRect(from));

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
