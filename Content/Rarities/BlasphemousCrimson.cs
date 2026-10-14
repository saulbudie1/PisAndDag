using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Mage;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Rarities
{
    /// <summary>
    /// Blasphemous Crimson, by request: the letters of the name run from #641e07 at the top
    /// through #431938 to #7b0a31 at the bottom, and their outline from #c42727 at the bottom
    /// through #d13d62 to #c40808 at the top (PADWorkFlow §16.61); and the colours move, the
    /// letters' down them and the outline's up it, round and round, each running from its last
    /// colour back into its first (§16.62: "the color moves with the 3 colors from up to down
    /// and lerp again"), a turn every <see cref="FlowSeconds"/>. For now only Foigh's Rust Side
    /// Sword is of it.
    ///
    /// To the game a rarity is one colour. In a tooltip the name is drawn by hand
    /// (<see cref="RampedNames"/>); wherever else the game shows a rarity's colour (the name
    /// that rises from an item as it is picked up) it is <see cref="RarityColor"/>. No prefix
    /// moves an item out of it (ModRarity.GetPrefixedRarity, left as it is).
    /// </summary>
    public class BlasphemousCrimson : ModRarity
    {
        /// <summary>The letters, top to bottom.</summary>
        public static readonly Color[] Fill = { new Color(0x64, 0x1e, 0x07), new Color(0x43, 0x19, 0x38), new Color(0x7b, 0x0a, 0x31) };

        /// <summary>Their outline, bottom to top.</summary>
        public static readonly Color[] Border = { new Color(0xc4, 0x27, 0x27), new Color(0xd1, 0x3d, 0x62), new Color(0xc4, 0x08, 0x08) };

        /// <summary>Where it can only be one colour: the outline's middle, the brightest of the six, which reads on anything.</summary>
        public override Color RarityColor => Border[1];

        /// <summary>Seconds for the colours to go once round. A whole number of them is an hour, so the turn never jumps when the game's clock comes round.</summary>
        public const float FlowSeconds = 2f;
    }

    /// <summary>
    /// The name of an item of <see cref="BlasphemousCrimson"/> in its tooltip, drawn in place of
    /// vanilla's (which is skipped) where vanilla draws it, laid out as vanilla lays it out:
    /// <see cref="MagicColours.Ramped"/>. The lines under it are where they were.
    /// </summary>
    public class RampedNames : GlobalItem
    {
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != "Terraria" || line.Name != "ItemName" || item.rare != ModContent.RarityType<BlasphemousCrimson>())
                return true;

            // How far round the colours have moved: the game's clock (Main.GlobalTimeWrappedHourly,
            // set every update, the game paused or not), in turns.
            float flow = Main.GlobalTimeWrappedHourly / BlasphemousCrimson.FlowSeconds % 1f;

            MagicColours.Ramped(Main.spriteBatch, line.Font, line.Text, new Vector2(line.X, line.Y),
                BlasphemousCrimson.Fill, BlasphemousCrimson.Border, line.BaseScale, flow, line.Rotation, line.Origin, line.Spread);
            return false;
        }
    }
}
