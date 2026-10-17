using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles.Lighthouse
{
    /// <summary>
    /// The lens of the Vast Ocean's lighthouse (VastOceanIsland), on its pedestal in the lantern: a
    /// lens of glass rings in a brass frame round the lamp, two bull's-eyes in it, one either side,
    /// turning with it (its picture, eight frames to the half turn, follows <see cref="LighthouseBeam.Turn"/>,
    /// so each bull's-eye comes round to the front as its beam swings toward whoever is looking).
    /// It lights the lantern and the gallery round it; its beams over the sea at night are
    /// LighthouseBeam's. Put there by the place as it is made, and kept: it cannot be mined or
    /// blown up, and nothing comes of it.
    /// </summary>
    public class LighthouseLens : ModTile
    {
        public const int Wide = 3, Tall = 4, Frames = 8;

        /// <summary>One frame of its picture, its four rows (16, 16, 16 and 18 pixels, two between each) one under another.</summary>
        public const int FrameHeight = 74;

        /// <summary>The lamp's light, a warm white, on every tile of it.</summary>
        private static readonly Vector3 Lamp = new Vector3(1.25f, 1.05f, 0.75f);

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;

            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(1, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 18 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Wide, 0);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.WaterPlacement = LiquidPlacement.Allowed;
            TileObjectData.newTile.WaterDeath = false;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            AnimationFrameHeight = FrameHeight;
            DustType = DustID.Glass;
            HitSound = SoundID.Shatter;

            AddMapEntry(new Color(255, 214, 120), CreateMapEntryName());
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

        public override bool CanExplode(int i, int j) => false;

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            frame = LighthouseBeam.LensFrame;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            r = Lamp.X;
            g = Lamp.Y;
            b = Lamp.Z;
        }
    }
}
