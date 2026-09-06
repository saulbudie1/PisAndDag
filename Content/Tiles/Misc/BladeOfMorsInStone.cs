using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Melee;
using Terraria;
using Terraria.DataStructures;   // AnchorData, Point16
using Terraria.Enums;            // AnchorType
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;       // TileObjectData

namespace PisAndDag.Content.Tiles.Misc
{
    /// <summary>
    /// The Blade of Mors planted in a rock, built to behave like vanilla's Enchanted
    /// Sword in the Stone: worldgen places it, you walk straight through it, one hit
    /// from a pickaxe frees it, and the blade itself is what drops.
    ///
    /// There is deliberately no item that places this. The only copy in a world is the
    /// one the Mors Monument pass puts there, so pulling the blade out is a one-off.
    /// </summary>
    public class BladeOfMorsInStone : ModTile
    {
        public override void SetStaticDefaults()
        {
            // Carries its own placement frame; nothing anchors to it; never solid, so
            // it reads as scenery you can stand inside rather than a block.
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;

            // The vanilla sword pops out in a single hit rather than being mined down
            // over several. tileNoFail is what produces that.
            Main.tileNoFail[Type] = true;

            // It stands in the Underworld, so neither lava nor an obsidian collision
            // may quietly delete it.
            Main.tileLavaDeath[Type] = false;
            Main.tileObsidianKill[Type] = false;

            TileID.Sets.DisableSmartCursor[Type] = true;

            // Built by hand rather than CopyFrom(a stock style): the texture is 52x34,
            // which is 3 tiles wide by 2 tall on the usual 16px tile + 2px padding grid,
            // and nothing in TileObjectData's stock list is that shape.
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 2;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
            TileObjectData.newTile.CoordinatePadding = 2;

            // Bottom centre. WorldGen.PlaceObject is then handed the tile the rock
            // stands on, so the generation pass never has to offset anything itself.
            TileObjectData.newTile.Origin = new Point16(1, 1);

            // Needs all three tiles under it to be solid ground — the plateau is flat
            // ash, so this holds, and it also stops the sword surviving if something
            // later carves the hilltop out from under it.
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 3, 0);

            TileObjectData.newTile.LavaDeath = false;

            // Sinks the rock a couple of pixels into the ground so it looks driven in
            // rather than resting on the surface.
            TileObjectData.newTile.DrawYOffset = 2;

            TileObjectData.addTile(Type);

            DustType = DustID.Stone;
            HitSound = SoundID.Tink;

            // Any pickaxe will do. Gating the blade behind a mining tier would only
            // move the wall, since it is placed in the Underworld either way.
            MinPick = 0;

            AddMapEntry(new Color(120, 38, 46), CreateMapEntryName());
        }

        /// <summary>
        /// Breaking the rock frees the blade. This is the only reason the tile exists.
        /// </summary>
        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield return new Item(ModContent.ItemType<BladeOfMors>());
        }
    }
}
