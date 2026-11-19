using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes.Sludge;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles.Sludge
{
    /// <summary>
    /// The sludge pocket's blocks (SludgePocket), by request ("the pocket is made of its own blocks (Acidic Mud, Stained
    /// Cobblestone) and blends onto nearby tiles"). Each mined gives itself, as a block does (Items.Sludge). They join one
    /// another, the Jungle's mud and grass, and stone, so that where the pocket has eaten into the ground around it
    /// (in a halftone, SludgePocketPlan) the two read as one ground going bad.
    /// </summary>
    public abstract class SludgeBlock : ModTile
    {
        protected abstract Color MapColour { get; }

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            foreach (int other in new int[] { TileID.Mud, TileID.JungleGrass, TileID.Stone, TileID.Dirt })
            {
                Main.tileMerge[Type][other] = true;
                Main.tileMerge[other][Type] = true;
            }

            foreach (SludgeBlock other in ModContent.GetContent<SludgeBlock>())
            {
                Main.tileMerge[Type][other.Type] = true;
                Main.tileMerge[other.Type][Type] = true;
            }

            AddMapEntry(MapColour, CreateMapEntryName());
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;
    }

    /// <summary>Acidic Mud: the Jungle's mud, soaked through. Soft, quick to dig.</summary>
    public class AcidicMud : SludgeBlock
    {
        protected override Color MapColour => new(86, 70, 38);

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            DustType = DustID.Mud;
            MineResist = 0.8f;
        }
    }

    /// <summary>Stained Cobblestone: stone laid by hand once, under the vats, stained brown with what ran over it.</summary>
    public class StainedCobblestone : SludgeBlock
    {
        protected override Color MapColour => new(104, 92, 76);

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();
            DustType = DustID.Stone;
            HitSound = SoundID.Tink;
            MineResist = 1.5f;
        }
    }

    /// <summary>
    /// A remnant of the works that poisoned the Jungle (Lore Bible §XVII: "pumps, vats, pipes"), laid by the pocket's
    /// generation only (SludgePocket), never by hand: it gives nothing when broken. Sludge drips from wherever it is
    /// open or split (<see cref="Drips"/>).
    /// </summary>
    public abstract class SludgeRemnant : ModTile
    {
        protected abstract int Wide { get; }
        protected abstract int Tall { get; }
        protected abstract Color MapColour { get; }

        /// <summary>Whether a style drips, and from where in the object (pixels from its top left).</summary>
        protected virtual bool Drips(int style, out Vector2 from)
        {
            from = Vector2.Zero;
            return false;
        }

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(0, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Repeat(16, Tall));
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.WaterDeath = false;
            TileObjectData.newTile.WaterPlacement = LiquidPlacement.Allowed;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.Allowed;
            Anchor();
            TileObjectData.addTile(Type);

            DustType = DustID.Iron;
            HitSound = SoundID.Tink;
            AddMapEntry(MapColour, CreateMapEntryName());
        }

        /// <summary>What it stands on: by default, solid ground under all of it.</summary>
        protected virtual void Anchor()
        {
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop, Wide, 0);
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (Main.gamePaused || !Main.rand.NextBool(50))
                return;

            Tile tile = Main.tile[i, j];
            int style = tile.TileFrameX / (Wide * 18);
            int cx = tile.TileFrameX % (Wide * 18) / 18, cy = tile.TileFrameY / 18;

            if (!Drips(style, out Vector2 from))
                return;

            // From the cell the drip point is in only, so that it drips once, not once for each cell.
            if ((int)(from.X / 16f) != cx || (int)(from.Y / 16f) != cy)
                return;

            Vector2 at = new Vector2((i - cx) * 16f, (j - cy) * 16f) + from;
            Dust drop = Dust.NewDustPerfect(at, DustID.t_Slime, new Vector2(0f, 0.6f), 140, new Color(96, 72, 36), 0.9f);
            drop.noGravity = false;
        }
    }

    /// <summary>A rusted barrel, of the vats' leavings: upright, dented and leaking, toppled and spilling, or crushed; a number stencilled on each and nothing else.</summary>
    public class RustedBarrel : SludgeRemnant
    {
        protected override int Wide => 2;
        protected override int Tall => 2;
        protected override Color MapColour => new(120, 66, 40);

        protected override bool Drips(int style, out Vector2 from)
        {
            from = style switch
            {
                (int)SludgePocketPlan.Barrel.Dented => new Vector2(22f, 22f),
                (int)SludgePocketPlan.Barrel.Toppled => new Vector2(30f, 26f),
                _ => Vector2.Zero,
            };

            return from != Vector2.Zero;
        }
    }

    /// <summary>
    /// A rusted pipe, a cell of it: runs, bends, an open mouth, a valve, a joint, a split (SludgePocketPlan.Pipe has
    /// which is which). Hangs where it was put, needing nothing under it; sludge drips from its mouths and its splits.
    /// </summary>
    public class RustedPipe : SludgeRemnant
    {
        protected override int Wide => 1;
        protected override int Tall => 1;
        protected override Color MapColour => new(112, 72, 46);

        protected override void Anchor()
        {
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
        }

        protected override bool Drips(int style, out Vector2 from)
        {
            from = style switch
            {
                (int)SludgePocketPlan.Pipe.MouthDown => new Vector2(8f, 15f),
                (int)SludgePocketPlan.Pipe.MouthRight => new Vector2(15f, 12f),
                (int)SludgePocketPlan.Pipe.MouthLeft => new Vector2(1f, 12f),
                (int)SludgePocketPlan.Pipe.Broken => new Vector2(8f, 12f),
                _ => Vector2.Zero,
            };

            return from != Vector2.Zero;
        }
    }

    /// <summary>A vat, rusted through and cracked, on its legs; an inlet on its top and an outlet low on one side (style 0: the outlet right; 1: left).</summary>
    public class SludgeVat : SludgeRemnant
    {
        protected override int Wide => 4;
        protected override int Tall => 4;
        protected override Color MapColour => new(108, 70, 44);

        protected override bool Drips(int style, out Vector2 from)
        {
            from = style == 0 ? new Vector2(22f, 40f) : new Vector2(42f, 40f);
            return true;
        }
    }
}
