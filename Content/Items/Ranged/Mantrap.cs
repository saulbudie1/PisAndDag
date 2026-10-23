using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Ranged;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// The Mantrap: a goblin pistol that throws iron jaws (MantrapJaws). 22 damage, a shot every
    /// half second, three jaws to a load and 80 ticks to set three more by hand (PSItem's magazine,
    /// as every gun's). It drops from the goblins of an army, one in <see cref="DropChance"/>
    /// (VanillaLoot), by request (PADWorkFlow §16.98: five of the weapons suggested for the first
    /// days, picked by the user).
    ///
    /// The jaws fly open, turning over, and fall. What they hit, they shut on: an enemy the Meat
    /// Hook would drag (MeatHookRules.Hauled) is held where it stands for a second, anything bigger
    /// is bitten and the jaws drop off. What they miss, they land on and lie open on the ground, set
    /// (three at most to a player; a fourth set shuts the oldest on nothing), and shut on the first
    /// thing that walks into them, for half as much again. The first foot, the owner's included:
    /// that is what this gun takes from whoever holds it (Lore Bible §XXI, "every gun in the mod
    /// takes something from the wielder"; the Artificers' first one was "a trap you carry").
    ///
    /// Lore: §XXI (the gun's verb is stop), §XIX (Grace fell to "a better trap").
    /// </summary>
    public class Mantrap : ModItem
    {
        public const int Damage = 22;
        public const int UseTicks = 30;
        public const float Knockback = 3f;
        public const float ShootSpeed = 10.5f;

        /// <summary>Jaws to a load, and the ticks it takes to set three more by hand.</summary>
        public const int Jaws = 3;
        public const int ReloadTicks = 80;

        /// <summary>One in this many goblins of an army drops it (VanillaLoot).</summary>
        public const int DropChance = 50;

        /// <summary>The muzzle: this share of the sprite's width out along the aim, this many texels up off the line.</summary>
        private const float MuzzleReach = 0.92f;
        private const float MuzzleRise = -4f;

        private static readonly Color[] Sparks = { new Color(255, 232, 186), new Color(255, 176, 96), new Color(206, 116, 72) };

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 22;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = false;
            Item.noMelee = true;

            Item.shoot = ModContent.ProjectileType<MantrapJaws>();
            Item.shootSpeed = ShootSpeed;

            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);

            Item.PIS().Magazine(Jaws, ReloadTicks);
        }

        public override Vector2? HoldoutOffset() => new Vector2(-4f, 2f);

        /// <summary>
        /// Where a held gun's barrel ends, for a shot from <paramref name="from"/> along
        /// <paramref name="aim"/>: PSItem's own sum (BarrelTip), the sprite's width once it is
        /// loaded, the item's until then. <paramref name="rise"/> is texels off the line, up
        /// negative, turned with the gun as the game turns it when aimed left.
        /// </summary>
        public static Vector2 MuzzleOf(Item item, Player player, Vector2 from, Vector2 aim, float reach, float rise)
        {
            float length = item.width;
            Asset<Texture2D> texture = TextureAssets.Item[item.type];

            if (texture != null && texture.IsLoaded)
                length = texture.Value.Width;

            Vector2 across = new Vector2(-aim.Y, aim.X) * player.direction;
            return from + aim * (length * item.scale * reach) + across * (rise * item.scale);
        }

        /// <summary>
        /// The jaws leave from the muzzle, or from the middle of the one who fires if the muzzle is
        /// past a wall. Nothing is left for the game to shoot.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            Vector2 aim = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 from = player.RotatedRelativePoint(player.MountedCenter, false, true);
            Vector2 muzzle = MuzzleOf(Item, player, from, aim, MuzzleReach, MuzzleRise);
            Vector2 start = Collision.CanHitLine(from, 1, 1, muzzle, 1, 1) ? muzzle : from;

            Projectile.NewProjectile(source, start, velocity, type, damage, knockback, player.whoAmI);

            if (!Main.dedServ)
                Fire(player, muzzle, aim);

            return false;
        }

        /// <summary>The spring let go: a twang and a knock, a little smoke, a few sparks off the iron.</summary>
        private static void Fire(Player player, Vector2 muzzle, Vector2 aim)
        {
            SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with { Pitch = 0.35f, PitchVariance = 0.08f, Volume = 0.55f }, muzzle);
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.3f, Volume = 0.45f }, muzzle);

            MuzzleSmoke.Spawn(muzzle, aim, 3);

            for (int i = 0; i < 6; i++)
            {
                Vector2 thrown = aim.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-30f, 30f))) * Main.rand.NextFloat(3f, 8f);

                PADParticleHandler.SpawnParticle(new SparkShard(muzzle + Main.rand.NextVector2Circular(2f, 2f), thrown,
                    Sparks[Main.rand.Next(Sparks.Length)], 8f, 14f, Main.rand.NextFloat(14f, 22f), Main.rand.Next(8, 13), falls: true));
            }

            player.PIS().BumpScreenShake(2.5f, 5);
        }
    }
}
