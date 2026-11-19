using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// The Abandoned Lab, by request ("sub world is : Abandoned Lab"): what the programme kept under the Wasted Mile, gone
    /// down into by the vent on the Mile once Amalgaermis is dead (LabVent), come back up out of by the vent standing in
    /// its way in. Its layout is LabPlan's, whose notes say what it is.
    ///
    /// As every place is unless it says otherwise (Subworld): nothing in it broken or built, nothing spawns, the clock
    /// stands still, no music. Shut in on every side and walled throughout, so the sky's light does not reach in: its
    /// light is its own, red mostly, and little of it.
    /// </summary>
    public class AbandonedLab : Subworld
    {
        public override int Width => LabPlan.Wide;

        public override int Height => LabPlan.High;

        /// <summary>7: white tubes in the document room. 6: the new wing (the user's floor plan: annex, document room, elevator, dining and safe rooms, the pit, vents), laid 130 east in a place 570 by 290. 5: no torches; red tubes in the hall where they were, another in the store room. 4: specimen canisters. 3: walls behind the ducts' grates, red lights in the hall. 2: its vent one tile wide (LabVent). Before it: the first of it (LabPlan), 1.</summary>
        public override int Layout => 7;

        /// <summary>The vent back up, on the floor of the way in.</summary>
        public override Point Door => new Point(LabPlan.VentLeft, LabPlan.VentTop);

        /// <summary>Beside the vent, on the floor it stands on.</summary>
        public override Point Spawn => new Point(LabPlan.VentLeft + LabVent.Wide + 1, LabPlan.VentTop + LabVent.Tall);

        /// <summary>
        /// By request ("Also the cobweb should be destroyable"): its cobwebs may be cut through, though nothing else here is
        /// broken; and a whole specimen canister may be broken open (LabCanister: it is not taken away, only opened).
        /// </summary>
        public override bool MayBreak(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return false;

            return tile.TileType == TileID.Cobweb || tile.TileType == ModContent.TileType<LabCanister>() && LabCanister.IsWhole(x, y);
        }

        /// <summary>A whole specimen canister: the one thing here a pick works on (LabCanister).</summary>
        public override bool MayDig(int x, int y) => LabCanister.IsWhole(x, y);

        public override void Build()
        {
            int seed = Main.ActiveWorldFileData?.Seed ?? 0;
            var canvas = new LabWorld((ushort)ModContent.TileType<LabVent>(), (ushort)ModContent.TileType<LabLight>(), (ushort)ModContent.TileType<LabCanister>());

            LabPlan.Lay(canvas, seed);

            Mod.Logger.Info($"Abandoned Lab: laid out; {canvas.Placed} things put down, {canvas.Missed} would not go" + (canvas.Missed > 0 ? $" ({canvas.MissedKinds})." : "."));
        }
    }

    /// <summary>The lab's plan written into the world's tiles: blocks and walls straight in, everything else put down by the game's own hand.</summary>
    internal sealed class LabWorld : LabPlan.ICanvas
    {
        private readonly ushort vent, light, canister;

        public int Placed, Missed;
        private readonly Dictionary<int, int> missed = new();

        /// <summary>What would not go, by tile type: "type x count".</summary>
        public string MissedKinds
        {
            get
            {
                var parts = new List<string>();

                foreach (var pair in missed)
                    parts.Add(pair.Key + " x" + pair.Value);

                return string.Join(", ", parts);
            }
        }

        public LabWorld(ushort vent, ushort light, ushort canister)
        {
            this.vent = vent;
            this.light = light;
            this.canister = canister;
        }

        public void Block(int x, int y, ushort type)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.ClearTile();
            tile.HasTile = true;
            tile.TileType = type;
            tile.LiquidAmount = 0;
        }

        public void Clear(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.ClearTile();
            tile.LiquidAmount = 0;
        }

        public void Wall(int x, int y, ushort type)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.WallType = type;
        }

        public void Water(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return;

            Tile tile = Main.tile[x, y];
            tile.LiquidType = LiquidID.Water;
            tile.LiquidAmount = 255;
        }

        public bool Empty(int x, int y) => WorldGen.InWorld(x, y) && !Main.tile[x, y].HasTile;

        public bool Solid(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return true;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && Main.tileSolid[tile.TileType];
        }

        public bool Furnish(ushort type, int style, int left, int top)
        {
            TileObjectData data = TileObjectData.GetTileData(type, style);
            int px = left + (data?.Origin.X ?? 0), py = top + (data?.Origin.Y ?? 0);
            bool ok = data == null ? WorldGen.PlaceTile(px, py, type, true, false, -1, style) : WorldGen.PlaceObject(px, py, type, true, style);

            return Took(ok && WorldGen.InWorld(left, top) && Main.tile[left, top].HasTile && Main.tile[left, top].TileType == type, type);
        }

        public bool Chest(ushort type, int style, int left, int top, List<(int item, int stack)> items)
        {
            TileObjectData data = TileObjectData.GetTileData(type, style);
            int px = left + (data?.Origin.X ?? 0), py = top + (data?.Origin.Y ?? 1);

            if (!Took(WorldGen.PlaceChest(px, py, type, false, style) >= 0, type))
                return false;

            int id = Terraria.Chest.FindChest(left, top);

            if (id < 0 || Main.chest[id] == null)
                return true;

            Item[] slots = Main.chest[id].item;
            int k = 0;

            foreach (var (item, stack) in items)
            {
                if (k >= slots.Length)
                    break;

                slots[k].SetDefaults(item);
                slots[k].stack = Math.Min(stack, slots[k].maxStack);
                k++;
            }

            return true;
        }

        public void Place(ushort type, int style, int x, int y)
        {
            Took(WorldGen.PlaceTile(x, y, type, true, false, -1, style) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == type, type);
        }

        public void Vent(int left, int top)
        {
            if (vent != 0)
                LabVent.Put(left, top, vent);
        }

        public bool Light(int left, int top, int style) => light != 0 && Furnish(light, style, left, top);

        public bool Canister(int left, int top) => canister != 0 && Furnish(canister, LabCanister.Whole, left, top);

        /// <summary>One of the lab's own furnishings (LabFurnishing), by kind; the pit's burner a block.</summary>
        public bool Decor(int kind, int style, int left, int top)
        {
            ushort type = (ushort)Tiles.Lab.LabFurnishing.TypeOf[kind];

            if (type == 0)
                return Took(false, kind);

            if (kind == LabPlan.LabDecor.Burner)
            {
                Block(left, top, type);
                return Took(true, type);
            }

            return Furnish(type, style, left, top);
        }

        private bool Took(bool ok, int type)
        {
            if (ok)
            {
                Placed++;
                return true;
            }

            Missed++;
            missed.TryGetValue(type, out int n);
            missed[type] = n + 1;
            return false;
        }
    }
}
