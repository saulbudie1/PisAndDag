using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.GildedKnight;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// The Gilded Knight's spur, which he leaves where he goes into light (by request: "resummon
    /// with an item dropped from him"). Rung on his field before the Curtain, it calls him back to
    /// his post, and he comes down the field at whoever rang it as he did the first time
    /// (KnightEncounter.Call). In a world with no kingdom it calls him to a field about them.
    ///
    /// Not used up: he leaves one every time, and a lost fight would otherwise leave no way to try
    /// again. Held up and rung; not while he is already about, nor off his field.
    /// </summary>
    public class GildedSpur : ModItem
    {
        /// <summary>The line over the player, off his field: the spur speaks for itself.</summary>
        public const string NothingHere = "It rings, and nothing answers here.";

        /// <summary>When that was last said (not again for a second, however long the button is held).</summary>
        private static uint said;

        /// <summary>Over this machine's player: nothing answers the spur here.</summary>
        public static void SayNothing(Player player)
        {
            if (Main.GameUpdateCount - said <= 60)
                return;

            said = Main.GameUpdateCount;
            CombatText.NewText(player.Hitbox, new Color(255, 214, 140), NothingHere);
        }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;

            // In the boss summon sort, with the other things that call a boss.
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 22;
            Item.maxStack = 20;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 1);

            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 40;
            Item.useAnimation = 40;
            Item.consumable = false;
            Item.UseSound = KnightSounds.Point;
        }

        public override bool CanUseItem(Player player)
        {
            string why = KnightEncounter.CannotCall(player);

            if (why == "Elsewhere" && player.whoAmI == Main.myPlayer)
                SayNothing(player);

            return why == null;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
                KnightEncounter.Ring(player);

            return true;
        }
    }
}
