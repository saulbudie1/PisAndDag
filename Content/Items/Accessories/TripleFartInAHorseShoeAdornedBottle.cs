using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Triple Fart In A Horse Shoe Adorned Bottle, by request: "as the name suggests: it creates
    /// allow quadruple jumps and negates fall damage". Worn, it gives three of the Fart in a Jar's
    /// jumps in the air (TripleFartJump): four jumps in all, with the one off the ground. And no
    /// fall damage, as the Lucky Horseshoe gives (TripleFartPlayer).
    ///
    /// Mine: its rarity and value are the Bundle of Horseshoe Balloons', the game's own accessory
    /// that lets its holder quadruple jump and negates fall damage; its tooltip says what the
    /// game's own say of the same. Its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class TripleFartInAHorseShoeAdornedBottle : ModItem
    {
        /// <summary>Beside the mod's other accessory in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 4);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetJumpState<TripleFartJump>().Enable();
            player.noFallDmg = true;
            player.GetModPlayer<TripleFartPlayer>().Worn = true;
        }

        // Recipes stay commented out for now. "you know what the recipe is": three Farts in a Jar and a
        // Lucky Horseshoe, at a Tinkerer's Workshop, where the game puts a jump and a horseshoe together
        // (its Green Horseshoe Balloon is a Fart in a Balloon and a Lucky Horseshoe there):
        //
        // public override void AddRecipes()
        // {
        //     CreateRecipe()
        //         .AddIngredient(ItemID.FartinaJar, 3)
        //         .AddIngredient(ItemID.LuckyHorseshoe)
        //         .AddTile(TileID.TinkerersWorkbench)
        //         .Register();
        // }
    }

    /// <summary>
    /// The bottle's jump in the air: the Fart in a Jar's own (ExtraJump.FartInAJar: held up twice as
    /// long as a jump off the ground, 1.75 times as fast across while it lasts, its sound, its gas
    /// and its puffs), three times before the ground is touched again. Tried right after the jar's
    /// own, if both are worn.
    /// </summary>
    public class TripleFartJump : ExtraJump
    {
        /// <summary>How many times it can be used in the air.</summary>
        public const int Farts = 3;

        public override Position GetDefaultPosition() => new After(FartInAJar);

        public override float GetDurationMultiplier(Player player) => FartInAJar.GetDurationMultiplier(player);

        public override void UpdateHorizontalSpeeds(Player player) => FartInAJar.UpdateHorizontalSpeeds(player);

        public override void ShowVisuals(Player player) => FartInAJar.ShowVisuals(player);

        /// <summary>On the ground again (or wherever the game gives a player their jumps back): all three.</summary>
        public override void OnRefreshed(Player player)
        {
            player.GetModPlayer<TripleFartPlayer>().Left = Farts;
        }

        /// <summary>One used: the game has just taken it away (ExtraJumpState.Start); it is given back while any are left.</summary>
        public override void OnStarted(Player player, ref bool playSound)
        {
            FartInAJar.OnStarted(player, ref playSound);

            TripleFartPlayer farts = player.GetModPlayer<TripleFartPlayer>();
            farts.Left--;

            if (farts.Left > 0)
                player.GetJumpState(this).Available = true;
        }
    }

    /// <summary>How many of the bottle's jumps a player has left in the air, and whether the bottle is worn this tick.</summary>
    public class TripleFartPlayer : ModPlayer
    {
        public int Left = TripleFartJump.Farts;

        public bool Worn;

        public override void ResetEffects()
        {
            Worn = false;
        }

        /// <summary>
        /// No fall damage again once everything worn has had its say: the mod's grounding of wings
        /// (PSItem) takes fall damage back from a player wearing wings, and would take the bottle's
        /// too where the wings come after it.
        /// </summary>
        public override void PostUpdateEquips()
        {
            if (Worn)
                Player.noFallDmg = true;
        }
    }
}
