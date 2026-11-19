using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
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
    /// does 500 damage." Balanced after, by request: "make it decent against hardmode, 300 dps on
    /// no defense; and the light thing has a 3 sec cool down; it deals 350 damage and 2x if player
    /// is in hell".
    ///
    /// Left click cuts, up and then down, and on in turn (<see cref="GildedSwordPlayer"/> keeps
    /// which is next; left for <see cref="Rhythm"/> ticks, it starts again with the cut up): 100
    /// damage a cut, three cuts a second, so 300 damage a second against one thing with nothing
    /// else added. Right click is his long blade: the sword raised, light grows out of it into a
    /// blade 24 tiles long over a second, and it comes down in front, everything under it struck
    /// once for 3.5 times a cut (350), twice that (700) in the Underworld, a pillar of light where
    /// its point lands. A second and a half from the click to the end of it, whatever the wielder's
    /// speed; then not again until three seconds from the click (<see cref="LongCooldown"/>), a
    /// chime when it is ready.
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

        /// <summary>The long blade's damage, as many times a cut's (350); in the Underworld, that again as many times.</summary>
        public const float LongDamage = 3.5f;
        public const float UnderworldDamage = 2f;

        /// <summary>Ticks from one long blade's click to the next: three seconds.</summary>
        public const int LongCooldown = 180;

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

        /// <summary>The long blade only once its cooldown is over (vanilla has set altFunctionUse before asking).</summary>
        public override bool CanUseItem(Player player)
        {
            return player.altFunctionUse != 2 || player.GetModPlayer<GildedSwordPlayer>().LongReady;
        }

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
        /// use. The long blade is 3.5 times the damage, twice that in the Underworld, and starts its
        /// cooldown; a cut is up or down as the chain has it.
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
                chain.LongUsed();
                float times = LongDamage * (player.ZoneUnderworldHeight ? UnderworldDamage : 1f);
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, (int)(damage * times), knockback * 1.6f,
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
    /// (the only one that begins its moves): up, then down, in turn, back to up after a rest; and
    /// the long blade's cooldown, with a chime when it is over while the sword is in hand.
    /// </summary>
    public class GildedSwordPlayer : ModPlayer
    {
        private SwordMove last = SwordMove.Down;
        private uint lastAt;
        private int longWait;

        /// <summary>The long blade can be raised again.</summary>
        public bool LongReady => longWait <= 0;

        /// <summary>The long blade raised: its cooldown begins.</summary>
        public void LongUsed() => longWait = SwordOfAGildedKnight.LongCooldown;

        public override void PostUpdate()
        {
            if (longWait <= 0 || --longWait > 0 || Player.whoAmI != Main.myPlayer)
                return;

            if (Player.HeldItem.type == ModContent.ItemType<SwordOfAGildedKnight>())
            {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = 0.2f }, Player.Center);

                for (int d = 0; d < 10; d++)
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.GoldFlame, 0f, -1.5f, 0, default, 1.2f);
                    dust.noGravity = true;
                }
            }
        }

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
