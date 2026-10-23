using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// The Signal Pistol: a brass flare pistol, one round to a load and two and a half seconds to
    /// load the next (PSItem's magazine). In the east sea chest of the lighthouse's watch room, in the
    /// Vast Ocean (LighthouseTower; the sea's layout 9, so a Vast Ocean made before it is made again),
    /// by request (PADWorkFlow §16.98).
    ///
    /// It fires a red flare (SignalFlare) that burns six seconds wherever it ends up: stuck in what it
    /// hit, burning it; stuck where it landed; or sinking slowly through the water. It lights the dark
    /// red round it, and what swims near it turns toward the light. While it burns, its wielder's own
    /// sight is dimmed (the game's Darkness): the light has gone where it was sent. That is what this
    /// gun takes (Lore Bible §XXI); and its report is the one §XXI gives every gun, made to be seen,
    /// "the gun tells the whole world something happened".
    ///
    /// The lighthouse keeper is not the Bible's: the tower has his room and nobody in it.
    /// </summary>
    public class SignalPistol : ModItem
    {
        public const int Damage = 14;
        public const int UseTicks = 24;
        public const float Knockback = 2f;
        public const float ShootSpeed = 9.5f;

        /// <summary>One round to a load, and the ticks to break it open and load the next.</summary>
        public const int Rounds = 1;
        public const int ReloadTicks = 150;

        private const float MuzzleReach = 0.94f;
        private const float MuzzleRise = -3f;

        private static readonly Color[] Sparks = { new Color(255, 236, 200), new Color(255, 120, 80), new Color(220, 40, 46) };

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 36;
            Item.height = 22;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = false;
            Item.noMelee = true;

            Item.shoot = ModContent.ProjectileType<SignalFlare>();
            Item.shootSpeed = ShootSpeed;

            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 80);

            Item.PIS().Magazine(Rounds, ReloadTicks);
        }

        public override Vector2? HoldoutOffset() => new Vector2(-3f, 1f);

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 from = player.RotatedRelativePoint(player.MountedCenter, false, true);
            Vector2 muzzle = Mantrap.MuzzleOf(Item, player, from, aim, MuzzleReach, MuzzleRise);
            Vector2 start = Collision.CanHitLine(from, 1, 1, muzzle, 1, 1) ? muzzle : from;

            Projectile.NewProjectile(source, start, velocity, type, damage, knockback, player.whoAmI);

            if (!Main.dedServ)
                Fire(player, muzzle, aim);

            return false;
        }

        /// <summary>A hollow pop and a hiss, a red flash, sparks and smoke.</summary>
        private static void Fire(Player player, Vector2 muzzle, Vector2 aim)
        {
            SoundEngine.PlaySound(SoundID.Item11 with { Pitch = -0.45f, Volume = 0.8f }, muzzle);
            SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = 0.4f, Volume = 0.35f }, muzzle);

            PADParticleHandler.SpawnParticle(new MuzzleFlash(muzzle, aim, 1.4f));
            PADParticleHandler.SpawnParticle(new SoftGlow(muzzle, Vector2.Zero, new Color(255, 84, 60) * 0.85f, 0.45f, 14));
            MuzzleSmoke.Spawn(muzzle, aim, 4);

            for (int i = 0; i < 8; i++)
            {
                Vector2 thrown = aim.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-22f, 22f))) * Main.rand.NextFloat(3f, 9f);

                PADParticleHandler.SpawnParticle(new SparkShard(muzzle + Main.rand.NextVector2Circular(2f, 2f), thrown,
                    Sparks[Main.rand.Next(Sparks.Length)], 8f, 14f, Main.rand.NextFloat(14f, 24f), Main.rand.Next(8, 14), falls: true));
            }

            player.PIS().BumpScreenShake(2.5f, 5);
        }
    }
}
