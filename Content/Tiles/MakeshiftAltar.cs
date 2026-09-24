using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// The Makeshift Altar: where incantations and spells are written. A crafting station
    /// and nothing else, three tiles wide and two tall, standing on the floor the way
    /// vanilla's Demon Altar does.
    ///
    /// Placed by the item of the same name (Content/Items/Misc/MakeshiftAltar), which
    /// tModLoader hands back when the altar is broken. The texture is a placeholder
    /// (PlaceholderTextures.md): a 54x38 sheet, three frames across and two down, 16 pixels
    /// each with 2 of padding, the lower row 18 tall so it sits into the ground.
    /// </summary>
    public class MakeshiftAltar : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;

            // Vanilla's 3x2 (origin at the middle of the bottom row, standing on solid
            // ground, a platform or a table), with the lower row two pixels taller, as the
            // Demon Altar has it.
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 18 };
            TileObjectData.newTile.LavaDeath = true;
            TileObjectData.addTile(Type);

            DustType = DustID.Bone;

            AddMapEntry(new Color(147, 108, 70), CreateMapEntryName());
        }
    }
}
