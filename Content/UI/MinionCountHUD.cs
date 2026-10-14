using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.ResourceSets;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// How many minion slots are taken and how many there are, small, as a block of two lines
    /// centred on each other, by request ("place minion UI next to the health bar", "a centered
    /// block like this: Minion / num/max"):
    ///
    ///     Minion
    ///      2.5/4
    ///
    /// The slots taken to one place after the point (Player.slotsMinions, which every minion adds
    /// its minionSlots to each tick), always with a point and never in another language's way of
    /// writing one; then Player.maxMinions. To the left of the life display, level with its middle,
    /// wherever the player's choice of resource display puts it (LifeDisplay, Place). Always there
    /// in the world, for anyone: "0.0/1" with none out. Hidden with the rest of the interface, and
    /// over the full map.
    /// </summary>
    public class MinionCountHUD : ModSystem
    {
        /// <summary>The magazine's count's own colour and size (MagazineHUD), so the two read as one kind of thing.</summary>
        private static readonly Color TextColor = new Color(235, 235, 235);

        private const float TextScale = 0.75f;

        /// <summary>Between the block and the life display, in the interface's pixels.</summary>
        private const float Gap = 12f;

        /// <summary>
        /// The tooltip font (Mouse_Text), in its own rows: its lines are 29 apart; capitals and most
        /// figures stand on row 17 with their tops on row 3 (a 4 starts on row 1, a slash on row 2,
        /// and a 9 hangs to row 19). DrawBorderString's border is 1.5 pixels wide all round.
        /// </summary>
        private const float LineRows = 29f, CapTop = 3f, Foot = 17f, Lowest = 19f, Border = 1.5f;

        /// <summary>The block's first line.</summary>
        public const string Label = "Minion";

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Resource Bars");

            if (index == -1)
                index = layers.FindIndex(layer => layer.Name == "Vanilla: Map / Minimap");

            if (index == -1)
                return;

            layers.Insert(index + 1, new LegacyGameInterfaceLayer(
                "PisAndDag: Minion Count",
                () =>
                {
                    Draw(Main.spriteBatch);
                    return true;
                },
                // UI, as the resource bars' own layer is: Main.screenWidth is then the interface's width.
                InterfaceScaleType.UI));
        }

        /// <summary>The block's second line: "2.5/4".</summary>
        public static string Count(Player player) =>
            player.slotsMinions.ToString("0.0", CultureInfo.InvariantCulture) + "/" + player.maxMinions.ToString(CultureInfo.InvariantCulture);

        /// <summary>The bars' own displays (HorizontalBarsPlayerResourcesDisplaySet), by name.</summary>
        private static bool Bars(string set) => set == "HorizontalBars" || set == "HorizontalBarsWithText" || set == "HorizontalBarsWithFullText";

        /// <summary>
        /// Where the game draws the life display, in the interface's pixels: its left edge, and its top
        /// and bottom. As each of the game's own resource displays lays it out, by the display's name
        /// (PlayerResourceSetsManager.ActiveSetKeyName) and the player's number of hearts:
        /// - "Default" (Classic): hearts 22 square, the first's left 300 in from the right edge, rows
        ///   26 apart from 32 down, ten to a row.
        /// - "New" (Fancy): heart panels 30 high from 296 in and 15 down, rows 28 apart; 6 lower when
        ///   it shows its text ("NewWithText"). A heart alone in its row (1, or 11) is drawn 4 further
        ///   left and up.
        /// - "HorizontalBars": the life bar 24 high from 306 in and 18 down, shorter from the left by 12
        ///   for each heart under 20; 4 lower with its text ("HorizontalBarsWithText"), 2 with the
        ///   full text ("HorizontalBarsWithFullText").
        /// A display of another mod's is taken to be where the game's own "New" is.
        /// </summary>
        public static (float Left, float Top, float Bottom) LifeDisplay(string set, int hearts, float screenWidth)
        {
            bool second = hearts > 10;

            switch (set)
            {
                case "Default":
                    return (screenWidth - 300f, 32f, second ? 80f : 54f);

                case "HorizontalBars":
                case "HorizontalBarsWithText":
                case "HorizontalBarsWithFullText":
                {
                    float top = set == "HorizontalBarsWithText" ? 22f : set == "HorizontalBarsWithFullText" ? 20f : 18f;
                    float left = screenWidth - 306f + (20 - Math.Clamp(hearts, 0, 20)) * 12f;
                    return (left, top, top + 24f);
                }

                default:
                {
                    float top = set == "NewWithText" ? 21f : 15f;
                    float left = hearts == 1 || hearts == 11 ? screenWidth - 300f : screenWidth - 296f;
                    return (left, top, top + (second ? 28f : 0f) + 30f);
                }
            }
        }

        /// <summary>
        /// Where the block goes, in the interface's pixels: its right edge, and the top of its first
        /// line as DrawBorderString takes it. Gap short of the life display, its letters (from the
        /// first line's capitals to the second line's foot) level with the display's middle.
        ///
        /// The bars' own displays draw more beside the life bar: their mana bar right under it, from
        /// 316 in and shorter from the left by 12 for each star under 20, and with their text, the life
        /// text's line 20 over it, centred 130 in from where the bar starts with 20 hearts (lifeText is
        /// its width: "Life: 400/400", as they measure it). Where the block would run into either, it
        /// is moved up or down beside the life bar as far as it must; where there is no room between
        /// them, it goes out past both to the left, level with the bar again.
        /// </summary>
        public static (float Right, float Line) Place(string set, int hearts, int stars, float lifeText, float screenWidth)
        {
            (float left, float top, float bottom) = LifeDisplay(set, hearts, screenWidth);
            float tall = (LineRows + Foot - CapTop) * TextScale;
            float right = left - Gap;
            float capitals = (top + bottom - tall) / 2f;

            if (Bars(set))
            {
                float textLeft = set == "HorizontalBars" ? float.MaxValue : screenWidth - 306f + 130f - lifeText / 2f;
                float manaLeft = screenWidth - 316f + (20 - stars) * 12f;

                // Where the block is beside them: its border no higher than a pixel under the life text's lowest
                // (a 9's, on row 19 of its line), nor lower than a pixel over the mana bar.
                float highest = textLeft < right + Border ? top - 20f + Lowest + 1f + Border : float.MinValue;
                float lowest = manaLeft < right + Border ? bottom - 1f - Border - (Lowest - Foot) * TextScale - tall : float.MaxValue;

                if (highest <= lowest)
                {
                    capitals = Math.Clamp(capitals, highest, lowest);
                }
                else
                {
                    right = Math.Min(left, Math.Min(textLeft, manaLeft)) - Gap;
                }
            }

            return (right, capitals - CapTop * TextScale);
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.hideUI || Main.mapFullscreen)
                return;

            Player player = Main.LocalPlayer;

            if (player == null || !player.active)
                return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string set = Main.ResourceSetsManager?.ActiveSetKeyName ?? "New";
            PlayerStatsSnapshot stats = new PlayerStatsSnapshot(player);
            int hearts = stats.AmountOfLifeHearts;
            int stars = stats.AmountOfManaStars;
            float lifeText = font.MeasureString(Lang.inter[0].Value + " " + player.statLifeMax2 + "/" + player.statLifeMax2).X;
            (float right, float line) = Place(set, hearts, stars, lifeText, Main.screenWidth);

            // Each line centred across the block.
            string count = Count(player);
            float width = Math.Max(font.MeasureString(Label).X, font.MeasureString(count).X) * TextScale;
            float middle = right - width / 2f;

            Utils.DrawBorderString(spriteBatch, Label, new Vector2(middle, line), TextColor, TextScale, 0.5f, 0f);
            Utils.DrawBorderString(spriteBatch, count, new Vector2(middle, line + LineRows * TextScale), TextColor, TextScale, 0.5f, 0f);
        }
    }
}
