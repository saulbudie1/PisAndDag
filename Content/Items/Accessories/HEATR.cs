using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// H.E.A.T.R, the Hyper Engineered Abyssal Trench Roamer, by request: "Create an endgame
    /// accessory gear: HEATR (Hyper Engineered Abyssal Trench Roamer) which basically allows you
    /// to breathe underwater and not get crushed at such depth section 12, 20 defense but at the
    /// expense that you are slowed down by 15% (even jump speed). your top speed. Create a void
    /// for top speed calculator. to do this. ... no crafting recipe yet. include the MUPRAL
    /// functions with upgraded version(radial for 30 tiles and same beam with 45 degree angle
    /// continuously so no swittch). (Create a force field Over the player like that one rick and
    /// morty thing where it goes over the body, make it have a hexagon texture going up tho so
    /// its futuristic)".
    ///
    /// Worn:
    ///   - 20 defense;
    ///   - breath underwater (Player.gills, as the Gills Potion gives it, though not the potion's
    ///     buff, so the Vast Ocean's depth does not wear it down: VastOceanGills). In a world For
    ///     the Worthy, where the game's gills drown their wearer in air, its breath is kept full
    ///     instead (<see cref="HEATRPlayer"/>);
    ///   - the water's weight held off to the floor of the Vast Ocean, all twelve sections
    ///     (VastOceanWeight.HoldTo, <see cref="VastOceanWeight.Deepest"/>): never pressed, never
    ///     imploded;
    ///   - <see cref="Slow"/> off the top speed and the jump speed (<see cref="TopSpeed"/>; how fast
    ///     the top speed is reached is left as it is), and the tooltip says what the wearer's top
    ///     speed is with it and without it, in miles an hour as the Stopwatch counts;
    ///   - the M.U.P.R.A.L's lamp on the shoulder, both its lights at once and no switching: a
    ///     glow 20 tiles about through water (33 through air: two thirds of the 30 first asked
    ///     for, by request, "radial radius still need be 2/3 tho") and a 45 degree beam to the
    ///     cursor, 50 tiles through water and 68 through air (MupralKit.Suit, MupralLight);
    ///   - a field over its wearer: a thin outline two pixels out from their silhouette, fading
    ///     in, with hexagons rising in it, all faint (<see cref="HEATRField"/>, <see cref="HEATRShell"/>).
    /// The lamp and the field are not drawn when its slot hides its picture; worn as vanity it
    /// does nothing and shows nothing. No recipe yet, by request. Red rarity, as the game's last
    /// things are. Its picture is mine (PlaceholderTextures.md), in 2x2 pixels.
    /// </summary>
    public class HEATR : ModItem
    {
        /// <summary>The share of the top speed and the jump speed it costs.</summary>
        public const float Slow = 0.15f;

        public const int Defense = 20;

        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 38;
            Item.height = 38;
            Item.accessory = true;
            Item.defense = Defense;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 25);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            if (!Main.getGoodWorld)
                player.gills = true;

            VastOceanWeight.HoldTo(player, VastOceanWeight.Deepest);
            TopSpeed.Slow(player, Slow);

            MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();
            lamp.SuitWorn = true;
            lamp.SuitShown |= !hideVisual;

            HEATRPlayer suit = player.GetModPlayer<HEATRPlayer>();
            suit.Worn = true;
            suit.Shown |= !hideVisual;
        }

        /// <summary>The wearer's top speed with it and without it, as of the last tick, over its last line.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;

            if (Main.gameMenu || player == null)
                return;

            float now = player.GetModPlayer<TopSpeedPlayer>().After;

            if (now <= 0f)
                return;

            bool worn = player.GetModPlayer<HEATRPlayer>().Worn;
            float with = worn ? now : now * (1f - Slow);
            float without = worn ? now / (1f - Slow) : now;
            string key = worn ? "Mods.PisAndDag.Items.Accessories.HEATR.TopSpeedWorn" : "Mods.PisAndDag.Items.Accessories.HEATR.TopSpeedOff";
            int at = tooltips.FindLastIndex(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));

            TooltipLine speed = new TooltipLine(Mod, "HEATRTopSpeed", Language.GetTextValue(key, TopSpeed.Mph(with), TopSpeed.Mph(without)))
            {
                OverrideColor = new Color(236, 176, 200),
            };

            if (at < 0)
                tooltips.Add(speed);
            else
                tooltips.Insert(at, speed);
        }
    }

    /// <summary>
    /// The HEATR on a player: worn this tick (<see cref="HEATR.UpdateAccessory"/>), and its field
    /// shown (HEATRShell). And breath, in a world For the Worthy: the game's gills turn there
    /// (breath in water, none in air), so the suit does not lend them; it keeps the wearer's breath
    /// full instead, on their own machine where breath is counted, every tick after the game
    /// has taken any (Player.CheckDrowning takes it a point at a time, and harms only at none).
    /// </summary>
    public class HEATRPlayer : ModPlayer
    {
        public bool Worn;
        public bool Shown;

        public override void ResetEffects()
        {
            Worn = false;
            Shown = false;
        }

        public override void PostUpdate()
        {
            if (Worn && Main.getGoodWorld && Player.whoAmI == Main.myPlayer)
            {
                Player.breath = Player.breathMax;
                Player.breathCD = 0;
            }
        }
    }
}
