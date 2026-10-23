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
    /// its lowest at 5376. Over the water, the game's own words for over its line: Surface, or
    /// Space as high as the game counts space (the same reckoning). None of the game's lines
    /// is moved.
    ///
    /// Under the water the word after the feet is the part of the sea the player is in, and the
    /// whole line is in that part's colour, by request ("Section 2: Schooler Zone (text color
    /// for depth meter:#26e9f0) / section 3-4: Hummer's Sea #4d5abd / section 5 -6: Light Party
    /// #b853b1 / Foreverzone (i like your color) / Abyssal Trench Lerp down thru time: #291852
    /// #210d3b #2d1733", the Forevernight Zone's "the pressure red", and "remember to color
    /// everything"; <see cref="Zones"/>): the 1st section the Vast Ocean, in the game's own white;
    /// the 2nd the Schooler Zone; the 3rd and 4th Hummer's Sea; the 5th and 6th the Light Party;
    /// the 7th to the 9th the Forevernight Zone; from the 10th down, the trench under the floor
    /// too, the Abyssal Trench, its colour going from one of its three to the next and back
    /// round (<see cref="TrenchStep"/> seconds each). In the game's own pulse of its text, as
    /// every line of it is.
    ///
    /// And by the water's weight (VastOceanWeight, by request: "put much effort into the depth
    /// pressure system"): going amber over the last 40 rows before where it begins for the
    /// player; while it takes their life, flashing between its red and the game's white, by
    /// request ("When its hurting from pressure it flashes red and normal text color"), the
    /// Forevernight Zone's own red as it is; over the last 80 rows before where it would implode
    /// them, flashing three times a second between a hot red and white, harder the nearer they are.
    /// </summary>
    public class VastOceanDepthMeter : GlobalInfoDisplay
    {
        private static readonly Color Amber = new Color(255, 190, 90);
        private static readonly Color Hurt = new Color(255, 92, 80);
        private static readonly Color Brink = new Color(255, 30, 40);

        /// <summary>
        /// The parts of the sea, from the 1st section down: the last section of each, its word,
        /// and its colour (none: the game's white).
        /// </summary>
        private static readonly (int lastSection, string key, Color? colour)[] Zones =
        {
            (1, "VastOcean", null),
            (2, "SchoolerZone", new Color(0x26, 0xE9, 0xF0)),
            (4, "HummersSea", new Color(0x4D, 0x5A, 0xBD)),
            (6, "LightParty", new Color(0xB8, 0x53, 0xB1)),
            (9, "ForevernightZone", Hurt),
            (VastOcean.Sections, "AbyssalTrench", null),
        };

        /// <summary>The Abyssal Trench's colours, gone through in turn and back round.</summary>
        private static readonly Color[] Trench = { new Color(0x29, 0x18, 0x52), new Color(0x21, 0x0D, 0x3B), new Color(0x2D, 0x17, 0x33) };

        /// <summary>Seconds from one of the trench's colours to the next.</summary>
        private const float TrenchStep = 2.5f;

        /// <summary>How many times a second the line flashes between red and white while the water takes the player's life.</summary>
        private const float HurtFlashes = 1.5f;

        public override void ModifyDisplayParameters(InfoDisplay currentDisplay, ref string displayValue, ref string displayName, ref Color displayColor, ref Color displayShadowColor)
        {
            if (currentDisplay != InfoDisplay.DepthMeter || SubworldKeeper.Current is not VastOcean)
                return;

            Player player = Main.LocalPlayer;

            // The game's reckoning, from the top of the surface row in place of its surface line.
            int feet = (int)((player.position.Y + player.height) * 2f / 16f - VastOcean.Surface * 2.0);
            string where;
            Color colour = Color.White;

            if (feet > 0)
            {
                int section = Math.Max(1, VastOcean.SectionOf(VastOcean.RowOf(player)));
                int zone = 0;

                while (zone < Zones.Length - 1 && section > Zones[zone].lastSection)
                    zone++;

                where = Language.GetTextValue("Mods.PisAndDag.Subworlds.DepthMeter." + Zones[zone].key);
                colour = zone == Zones.Length - 1 ? TrenchColour() : Zones[zone].colour ?? Color.White;
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

            // The water's weight: amber coming down to it; flashing red and white under it.
            VastOceanWeightPlayer weight = player.GetModPlayer<VastOceanWeightPlayer>();

            if (weight.Taking > 0f)
            {
                float flash = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * HurtFlashes);

                colour = Color.Lerp(Color.White, Hurt, flash);
            }
            else if (weight.Nearing > 0f)
            {
                colour = Color.Lerp(colour, Amber, weight.Nearing);
            }

            if (weight.Brink > 0f)
            {
                float flash = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 3f);
                colour = Color.Lerp(colour, Color.Lerp(Brink, Color.White, flash * 0.75f * weight.Brink), weight.Brink);
            }

            // In the game's pulse of its text (Main.mouseTextColor, the brightness its white is drawn at).
            float bright = Main.mouseTextColor / 255f;

            displayColor = new Color((int)(colour.R * bright), (int)(colour.G * bright), (int)(colour.B * bright), (int)Main.mouseTextColor);
        }

        /// <summary>The Abyssal Trench's colour now: from one of its three to the next, eased, and round again.</summary>
        private static Color TrenchColour()
        {
            float through = Main.GlobalTimeWrappedHourly / TrenchStep;
            int step = (int)Math.Floor(through);
            float t = through - step;

            t = t * t * (3f - 2f * t);

            return Color.Lerp(Trench[((step % Trench.Length) + Trench.Length) % Trench.Length], Trench[(((step + 1) % Trench.Length) + Trench.Length) % Trench.Length], t);
        }
    }
}
