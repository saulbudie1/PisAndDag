using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Water! Resuscitate Me!, by request ("Create incantation: Water! Resuscitate Me! Allow you
    /// to breathe under water for 5 mins basically gills potion. Allows you to go to section 8
    /// without being crushed. Gotten from fishing crates early game."). Asks the water to let
    /// the caster breathe it: Resuscitated for <see cref="Duration"/> ticks, five minutes, in
    /// which they breathe water as the Gills Potion lets one, at any depth, and the Vast
    /// Ocean's weight does not touch them down to the end of its 8th section (Resuscitated,
    /// VastOceanWeight). Hurts nothing.
    ///
    /// Abjuration on the chart of schools: it protects. Water: drawn from the sea. An
    /// incantation because a seal asks directly; so, as every incantation, it is cast through a
    /// seal, and the only seal comes after Skeletron.
    ///
    /// It will not go off again while more than <see cref="RecastWindow"/> ticks of the last are
    /// left (thirty seconds of the five minutes: as it fades), so holding the button does not
    /// pour mana into it; asked again inside that, it is back to five minutes.
    ///
    /// Found in fishing crates (<see cref="WaterResuscitateMeSource"/>). Placeholder art: see
    /// PlaceholderTextures.md.
    /// </summary>
    public class WaterResuscitateMe : Castable
    {
        /// <summary>Five minutes.</summary>
        public const int Duration = 5 * 60 * 60;

        /// <summary>Thirty seconds.</summary>
        public const int RecastWindow = 30 * 60;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 0;
        public override int Mana => 30;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<ResuscitateCall>();
        public override string CastType => "Abjuration";
        public override string Element => "Water";

        /// <summary>Its minutes and the depth it holds to, in the tooltip, from the numbers themselves.</summary>
        public override LocalizedText Tooltip => base.Tooltip.WithFormatArgs(Duration / 3600, VastOceanWeight.FeetAt(Resuscitated.HoldsTo + 1));

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Blue;
        }

        public override bool CanCast(Player player)
        {
            int index = player.FindBuffIndex(ModContent.BuffType<Resuscitated>());
            return index < 0 || player.buffTime[index] <= RecastWindow;
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            player.AddBuff(ModContent.BuffType<Resuscitated>(), Duration);
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, 0, 0f, player.whoAmI);
        }
    }

    /// <summary>
    /// Where it comes from: fishing crates, the early ones and what they become in hard mode, so
    /// it can still be found after the Wall: one in 20 Wooden (and Pearlwood) Crates, one in 10
    /// Iron (and Mythril), one in 5 Golden (and Titanium), one in 4 Ocean (and Seaside).
    /// </summary>
    public class WaterResuscitateMeSource : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            int oneIn = item.type switch
            {
                ItemID.WoodenCrate or ItemID.WoodenCrateHard => 20,
                ItemID.IronCrate or ItemID.IronCrateHard => 10,
                ItemID.GoldenCrate or ItemID.GoldenCrateHard => 5,
                ItemID.OceanCrate or ItemID.OceanCrateHard => 4,
                _ => 0,
            };

            if (oneIn > 0)
                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<WaterResuscitateMe>(), oneIn));
        }
    }
}
