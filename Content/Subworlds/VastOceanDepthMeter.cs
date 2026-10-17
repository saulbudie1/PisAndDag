using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Depth Meter in the Vast Ocean, by request ("Change depth meter so its 0 in the
    /// surface").
    ///
    /// The game reckons depth at the feet, two feet to a tile, in whole feet toward nought,
    /// from its surface line (Main.worldSurface), and names the layer the player is in
    /// (Main.DrawInfoAccs, 1.4.4: Space, Surface, Underground, Caverns, the Underworld). Here
    /// its surface line is 70 rows under the water (VastOcean.SurfaceLineRow, where the game's
    /// fishing and Duke Fishron need it), so at the water it read some 140 feet up, everything
    /// under that Underground, and the foot of the trench the Underworld.
    ///
    /// So in the Vast Ocean only what is shown is reckoned again, the game's way, from the top
    /// of the water's surface row, the row its twelve sections are counted from (the water's
    /// top is half way down it): nought at the water, each section 448 feet (224 rows), the
    /// 5th from 1792, the 7th from 2688, the 8th from 3136, the 10th from 4032, the floor at
    /// its lowest at 5376. Under the water the word after the feet is the part of the sea the
    /// player is in, as the game's are on land: the Vast Ocean to the end of the 6th section,
    /// then the Forevernight Zone, then the Abyssal Trench, by the rows the two biomes go by
    /// (VastOceanDepths). Over it, the game's own words for over its line: Surface, or Space as
    /// high as the game counts space (the same reckoning). None of the game's lines is moved.
    ///
    /// And by the water's weight (VastOceanWeight, by request: "put much effort into the depth
    /// pressure system"): amber, deepening, over the last 40 rows before where it begins for
    /// the player; red while it takes their life; in the game's own pulse of the text either way.
    /// Over the last 80 rows before where it would implode them, flashing three times a second
    /// between a hot red and white, harder the nearer they are.
    /// </summary>
    public class VastOceanDepthMeter : GlobalInfoDisplay
    {
        private static readonly Color Amber = new Color(255, 190, 90);
        private static readonly Color Hurt = new Color(255, 92, 80);
        private static readonly Color Brink = new Color(255, 30, 40);

        public override void ModifyDisplayParameters(InfoDisplay currentDisplay, ref string displayValue, ref string displayName, ref Color displayColor, ref Color displayShadowColor)
        {
            if (currentDisplay != InfoDisplay.DepthMeter || SubworldKeeper.Current is not VastOcean)
                return;

            Player player = Main.LocalPlayer;

            // The game's reckoning, from the top of the surface row in place of its surface line.
            int feet = (int)((player.position.Y + player.height) * 2f / 16f - VastOcean.Surface * 2.0);
            string where;

            if (feet > 0)
            {
                float row = VastOcean.RowOf(player);

                where = Language.GetTextValue(row >= VastOcean.AbyssalTrenchFrom ? "Mods.PisAndDag.Subworlds.DepthMeter.AbyssalTrench"
                    : row >= VastOcean.ForevernightFrom ? "Mods.PisAndDag.Subworlds.DepthMeter.ForevernightZone"
                    : "Mods.PisAndDag.Subworlds.DepthMeter.VastOcean");
            }
            else
            {
                // The game's space: rows counted from the top, more the wider the world, over a fifth of its surface line's.
                float wide = Main.maxTilesX / 4200f;
                wide *= wide;
                float space = (float)((player.Center.Y / 16f - (65f + 10f * wide)) / (Main.worldSurface / 5.0));

                where = Language.GetTextValue(space >= 1f ? "GameUI.LayerSurface" : "GameUI.LayerSpace");
            }

            feet = Math.Abs(feet);
            displayValue = (feet == 0 ? Language.GetTextValue("GameUI.DepthLevel") : Language.GetTextValue("GameUI.Depth", feet)) + " " + where;

            // The water's weight: amber coming down to it, red under it. The game's text is white
            // at the brightness it pulses to (Main.mouseTextColor), so these are too.
            VastOceanWeightPlayer weight = player.GetModPlayer<VastOceanWeightPlayer>();

            if (weight.Taking <= 0f && weight.Nearing <= 0f && weight.Brink <= 0f)
                return;

            Color colour = weight.Taking > 0f ? Hurt : Color.Lerp(Color.White, Amber, weight.Nearing);

            if (weight.Brink > 0f)
            {
                float flash = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 3f);
                colour = Color.Lerp(colour, Color.Lerp(Brink, Color.White, flash * 0.75f * weight.Brink), weight.Brink);
            }

            float bright = Main.mouseTextColor / 255f;

            displayColor = new Color((int)(colour.R * bright), (int)(colour.G * bright), (int)(colour.B * bright), (int)Main.mouseTextColor);
        }
    }
}
