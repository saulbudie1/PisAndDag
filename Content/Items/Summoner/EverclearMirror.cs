using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.Projectiles.Summoner;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Summoner
{
    /// <summary>
    /// Everclear Mirror, by request: "Add an end game Summoner weapon: Everclear Mirror; 15 minion slot. Summons a copy
    /// of the player with the weapon in the offhand (make it so lore weapons can also go offhand) This summon is basically
    /// a mimic tear (eldenring). With player's life with 30 Super Healing potions and mirror whatever effect/buff the
    /// player is having. Train this AI so it can have the best summoner skills, mix of dodging, parry (this train less)
    /// and dealing damage. It also mirror player's accessory; it can also use every alt of an item. Sad thing is that it
    /// is killable, its HP is Triple player's HP tho. So train it very well to make it last long in the fight." And,
    /// later: "create pseudo player, doesnt count towards scaling; but still attracts the boss".
    ///
    /// The copy is MimicSystem's (and MimicPlayer's, MimicSense's, MimicBrain's); this is what calls it. It takes 15
    /// minion slots (asked: "Costs 15 slots"): the anchor it leaves with its owner is a minion of that many
    /// (MimicAnchor), so it can be called only by someone with room for 15, and calling it sends away older minions to
    /// make that room, as calling any minion does. One at a time; a world of one only.
    ///
    /// Mine: 80 mana to call; held up as a mirror is, with the mirror's sound; when the copy breaks, the mirror is
    /// clouded for a minute (MimicSystem.Clouded) and cannot be used. Nothing gives it yet (RELEASE_TODO): one way it
    /// could be made is written out below, left off.
    /// </summary>
    public class EverclearMirror : ModItem
    {
        /// <summary>
        /// Minion slots it needs (by request, later: "Make it so that the mirror costs 12 minion slot"). It takes all of
        /// its owner's in the end ("So it takes up all minion slots": MimicAnchor), each past these twelve making the copy
        /// stronger (MimicSystem.Extra).
        /// </summary>
        public const int Slots = 12;

        /// <summary>Mine.</summary>
        public const int ManaCost = 80;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // The room the game makes for it when it is used: older minions sent away until there is this much.
            ItemID.Sets.StaffMinionSlotsRequired[Type] = Slots;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;

            Item.DamageType = DamageClass.Summon;
            Item.mana = ManaCost;

            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item6;
            Item.noMelee = true;

            Item.buffType = ModContent.BuffType<EverclearMirrorBuff>();
            Item.shoot = ModContent.ProjectileType<MimicAnchor>();

            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 20);
        }

        /// <summary>None out already, the mirror not clouded, room for its slots, and a world of one.</summary>
        public override bool CanUseItem(Player player)
        {
            return Main.netMode == NetmodeID.SinglePlayer && !MimicSystem.Exists
                && player.GetModPlayer<MimicPlayer>().Cloud <= 0 && player.maxMinions >= Slots;
        }

        /// <summary>The copy, stepping out beside its owner (behind them, or where they stand if that is in a wall); the buff; the anchor.</summary>
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 at = player.Center + new Vector2(-player.direction * 36f, 0f);

            if (Collision.SolidCollision(at - player.Size / 2f, player.width, player.height))
                at = player.Center;

            if (!MimicSystem.Summon(player, at))
                return false;

            // It takes every slot: the minions the game left it room beside go now (sentries are not minions, and stay).
            int anchor = ModContent.ProjectileType<MimicAnchor>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];

                if (p.active && p.owner == player.whoAmI && p.minion && p.type != anchor)
                    p.Kill();
            }

            player.AddBuff(Item.buffType, 2);
            Projectile.NewProjectile(source, at, Vector2.Zero, type, 0, 0f, player.whoAmI);
            return false;
        }

        /// <summary>Why it cannot be used now, when it cannot: clouded, and for how long; or not room enough for it.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;
            int cloud = player.GetModPlayer<MimicPlayer>().Cloud;

            if (cloud > 0)
            {
                tooltips.Add(new TooltipLine(Mod, "Clouded", Language.GetTextValue("Mods.PisAndDag.Items.EverclearMirror.Clouded", (cloud + 59) / 60))
                {
                    OverrideColor = new Color(170, 180, 200),
                });
            }
            else if (player.maxMinions < Slots)
            {
                tooltips.Add(new TooltipLine(Mod, "NoRoom", Language.GetTextValue("Mods.PisAndDag.Items.EverclearMirror.NoRoom", Slots, player.maxMinions))
                {
                    OverrideColor = new Color(190, 120, 120),
                });
            }
        }
    }

    // One way it could be made, left off until the user decides (RELEASE_TODO): a Magic or Ice Mirror, Luminite,
    // Stardust and Crystal Shards, at the Ancient Manipulator. To use it, take the comment marks off.
    //
    // public class EverclearMirrorRecipe : ModSystem
    // {
    //     public override void AddRecipes()
    //     {
    //         Recipe.Create(ModContent.ItemType<EverclearMirror>())
    //             .AddRecipeGroup("PisAndDag:Mirrors")
    //             .AddIngredient(ItemID.LunarBar, 10)
    //             .AddIngredient(ItemID.FragmentStardust, 18)
    //             .AddIngredient(ItemID.CrystalShard, 10)
    //             .AddTile(TileID.LunarCraftingStation)
    //             .Register();
    //     }
    //
    //     public override void AddRecipeGroups()
    //     {
    //         RecipeGroup.RegisterGroup("PisAndDag:Mirrors", new RecipeGroup(() => Lang.GetItemNameValue(ItemID.MagicMirror),
    //             ItemID.MagicMirror, ItemID.IceMirror));
    //     }
    // }
}
