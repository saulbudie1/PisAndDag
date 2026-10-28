using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Sword Of A Gilded Knight: the Grace, his pale sword, at a player's size. From the Gilded
    /// Knight of Grace, every kill (GildedKnight.ModifyNPCLoot). By request: "normal chain of
    /// attacks: up down; alternative: Focus into a big light beam and smash down, like one of his
    /// attacks with devastating damage. Dps normal attack should be 300 dps ish, and the smack down
    /// does 500 damage."
    ///
    /// Left click cuts, up and then down, and on in turn (<see cref="GildedSwordPlayer"/> keeps
    /// which is next; left for <see cref="Rhythm"/> ticks, it starts again with the cut up): 100
    /// damage a cut, three cuts a second, so 300 damage a second against one thing with nothing
    /// else added. Right click is his long blade: the sword raised, light grows out of it into a
    /// blade 24 tiles long over a second, and it comes down in front, everything under it struck
    /// once for five times the damage (500), a pillar of light where its point lands. A second and
    /// a half from the click to the end of it, whatever the wielder's speed, and held it goes on:
    /// about 330 a second against one thing, and far more against many.
    ///
    /// The sword in the hand is GildedSwordSwing, one for every use, for as long as the use: the
    /// item itself hits nothing and is never drawn in use.
    /// </summary>
    public class SwordOfAGildedKnight : ModItem
    {
        public const int Damage = 100;

        /// <summary>A cut's ticks: three a second.</summary>
        public const int UseTicks = 20;

        public const float Knockback = 6f;

        /// <summary>The long blade's damage, as many times a cut's.</summary>
        public const float LongDamage = 5f;

        /// <summary>Ticks after a cut ends within which the next cut follows it; after that, the cut up again.</summary>
        public const int Rhythm = 45;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // The right button held goes on into the next long blade, as the left held goes on
            // cutting (the game asks for it to be let go between two right clicks otherwise).
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.width = 58;
            Item.height = 58;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = UseTicks;
            Item.useAnimation = UseTicks;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<GildedSwordSwing>();
            Item.shootSpeed = 1f;

            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 4);
        }

        public override bool AltFunctionUse(Player player) => true;

        // The item is in use for exactly as long as the sword's move: a cut its use time at the
        // wielder's speed; the long blade its own ticks (GildedSwordSwing.LongTicks), whatever the
        // speed. Vanilla is about to divide the use time by the speed, and the animation by way of
        // the use time cut to whole ticks (CombinedHooks.TotalUseTimeMultiplier,
        // TotalUseAnimationMultiplier), so each is asked for as what comes out at the ticks meant
        // (FoighsRustSideSword's sums): both the same, and the sword never outlives the use.

        public override float UseTimeMultiplier(Player player)
        {
            if (player.altFunctionUse != 2)
                return 1f;

            float speed = CombinedHooks.TotalUseSpeedMultiplier(player, Item);
            return (GildedSwordSwing.LongTicks + 0.5f) * speed / Item.useTime;
        }

        public override float UseAnimationMultiplier(Player player)
        {
            if (player.altFunctionUse != 2)
                return 1f;

            float speed = CombinedHooks.TotalUseSpeedMultiplier(player, Item);
            int swing = System.Math.Max(1, (int)(Item.useTime * (1f / speed)));
            return (GildedSwordSwing.LongTicks + 0.5f) / swing;
        }

        /// <summary>
        /// Nothing is shot: the sword's move begins, in the hand, for as long as the item is in
        /// use. The long blade is five times the damage; a cut is up or down as the chain has it.
        /// </summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback)
        {
            int ticks = System.Math.Max(1, player.itemAnimationMax);
            GildedSwordPlayer chain = player.GetModPlayer<GildedSwordPlayer>();
            int facing = player.direction;

            if (player.altFunctionUse == 2)
            {
                chain.Rested();
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, (int)(damage * LongDamage), knockback * 1.6f,
                    player.whoAmI, (float)SwordMove.Long, ticks, facing);
                return false;
            }

            SwordMove move = chain.NextCut();
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI, (float)move, ticks, facing);
            return false;
        }
    }

    /// <summary>
    /// Which cut of the Sword Of A Gilded Knight's chain is next, on its wielder's own machine
    /// (the only one that begins its moves): up, then down, in turn, back to up after a rest.
    /// </summary>
    public class GildedSwordPlayer : ModPlayer
    {
        private SwordMove last = SwordMove.Down;
        private uint lastAt;

        /// <summary>The next cut, and that it has been made.</summary>
        public SwordMove NextCut()
        {
            uint now = Main.GameUpdateCount;
            bool rested = now - lastAt > (uint)(SwordOfAGildedKnight.Rhythm + Player.itemAnimationMax);

            SwordMove next = rested || last == SwordMove.Down ? SwordMove.Up : SwordMove.Down;

            last = next;
            lastAt = now;
            return next;
        }

        /// <summary>The long blade: after it, the chain starts again with the cut up.</summary>
        public void Rested()
        {
            last = SwordMove.Down;
            lastAt = 0;
        }
    }
}
