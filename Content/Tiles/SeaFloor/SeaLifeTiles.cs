using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles.SeaFloor
{
    /// <summary>
    /// One of the Vast Ocean floor's living things or vents (<see cref="SeaLife"/>), as the game
    /// holds it: an object standing on whole solid ground, as wide and tall as SeaLife says, its
    /// last row two pixels into the ground under it, its pictures side by side; put down in the
    /// water by the place as it is made (VastOceanFloor), never by hand, and nothing comes of
    /// breaking it. What it gives off (smoke, bubbles, motes) is SeaLifeEffects'. Some glow:
    /// the black smoker's mouth a dull red, and the sea pens, the bubblegum coral and the bamboo
    /// coral, all octocorals, with a blue-green light where they are brushed against, as the
    /// octocorals of the deep do (the sea pens faintly even at rest).
    /// </summary>
    public abstract class SeaLifeTile : ModTile
    {
        /// <summary>Each kind's tile type once loaded; 0 before.</summary>
        private static readonly ushort[] types = new ushort[SeaLife.Kinds];

        public static ushort TypeOf(SeaLife.Kind kind) => types[(int)kind];

        public abstract SeaLife.Kind Kind { get; }

        protected abstract Color MapColour { get; }

        protected abstract int Dust { get; }

        protected virtual SoundStyle? Sound => SoundID.Dig;

        /// <summary>Frames of its picture's animation, one under another (one-tile ones only).</summary>
        protected virtual int Frames => 1;

        /// <summary>Whether it gives light (<see cref="Light"/>).</summary>
        protected virtual bool Glows => false;

        public override void SetStaticDefaults()
        {
            types[(int)Kind] = Type;

            int wide = SeaLife.Wide(Kind), tall = SeaLife.Tall(Kind);

            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = Glows;

            TileID.Sets.DisableSmartCursor[Type] = true;

            int[] heights = new int[tall];

            for (int k = 0; k < tall; k++)
                heights[k] = k == tall - 1 ? 18 : 16;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Width = wide;
            TileObjectData.newTile.Height = tall;
            TileObjectData.newTile.Origin = new Point16(SeaLife.OriginX(Kind), SeaLife.OriginY(Kind));
            TileObjectData.newTile.CoordinateHeights = heights;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, wide, 0);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.WaterPlacement = LiquidPlacement.Allowed;
            TileObjectData.newTile.WaterDeath = false;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
            TileObjectData.newTile.LavaDeath = true;
            TileObjectData.addTile(Type);

            if (Frames > 1)
                AnimationFrameHeight = 18;

            DustType = Dust;
            HitSound = Sound;

            AddMapEntry(MapColour, CreateMapEntryName());
        }

        /// <summary>Puts one down by the game's own hand (WorldGen.PlaceObject), at the cell it goes by (its bottom row, its middle column); true if it went. What the place is made with hands to VastOceanFloor.</summary>
        public static bool Plant(int x, int y, SeaLife.Kind kind, int style)
        {
            ushort type = TypeOf(kind);

            if (type == 0 || !WorldGen.PlaceObject(x, y, type, true, style))
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && tile.TileType == type;
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            if (Frames <= 1)
                return;

            if (++frameCounter >= 7)
            {
                frameCounter = 0;
                frame = (frame + 1) % Frames;
            }
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            Vector3 light = Light(i, j);

            r = light.X;
            g = light.Y;
            b = light.Z;
        }

        /// <summary>The light it gives this cell of it.</summary>
        protected virtual Vector3 Light(int i, int j) => Vector3.Zero;

        /// <summary>How near the player is to this cell, 1 within a tile and a half, nothing six tiles off: brushed against, more or less.</summary>
        public static float Touch(int i, int j)
        {
            if (Main.gameMenu || Main.dedServ)
                return 0f;

            float d = Vector2.Distance(Main.LocalPlayer.Center, new Vector2(i * 16 + 8, j * 16 + 8));

            return 1f - MathHelper.Clamp((d - 24f) / 72f, 0f, 1f);
        }

        /// <summary>The octocorals' light where they are touched: a wave of blue-green running along them.</summary>
        protected static Vector3 Bioluminescence(int i, int j, float resting)
        {
            float touch = Touch(i, j);
            float wave = 0.75f + 0.25f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 6f + j * 0.7f + i * 0.3f);
            float strength = resting + touch * wave;

            return new Vector3(0.08f, 0.42f, 0.62f) * strength;
        }
    }

    public class GiantTubeWorms : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.GiantTubeWorms;
        protected override Color MapColour => new Color(226, 220, 206);
        protected override int Dust => DustID.Bone;
    }

    public class SeepTubeWorms : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.SeepTubeWorms;
        protected override Color MapColour => new Color(178, 156, 108);
        protected override int Dust => DustID.Sand;
    }

    public class ColdWaterCoral : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.ColdWaterCoral;
        protected override Color MapColour => new Color(232, 222, 210);
        protected override int Dust => DustID.Marble;
        protected override SoundStyle? Sound => SoundID.Tink;
    }

    public class BubblegumCoral : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.BubblegumCoral;
        protected override Color MapColour => new Color(222, 104, 140);
        protected override int Dust => DustID.PinkStarfish;
        protected override bool Glows => true;
        protected override Vector3 Light(int i, int j) => Bioluminescence(i, j, 0f);
    }

    public class BambooCoral : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.BambooCoral;
        protected override Color MapColour => new Color(222, 220, 214);
        protected override int Dust => DustID.Marble;
        protected override SoundStyle? Sound => SoundID.Tink;
        protected override bool Glows => true;
        protected override Vector3 Light(int i, int j) => Bioluminescence(i, j, 0f);
    }

    public class BlackCoral : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.BlackCoral;
        protected override Color MapColour => new Color(200, 110, 50);
        protected override int Dust => DustID.Ash;
    }

    public class GlassSponge : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.GlassSponge;
        protected override Color MapColour => new Color(212, 222, 226);
        protected override int Dust => DustID.Glass;
        protected override SoundStyle? Sound => SoundID.Shatter;
    }

    public class CupSponge : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.CupSponge;
        protected override Color MapColour => new Color(220, 172, 52);
        protected override int Dust => DustID.YellowStarfish;
    }

    public class SeaPen : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.SeaPen;
        protected override Color MapColour => new Color(214, 170, 200);
        protected override int Dust => DustID.PinkStarfish;
        protected override bool Glows => true;
        protected override Vector3 Light(int i, int j) => Bioluminescence(i, j, 0.22f);
    }

    public class SeaLily : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.SeaLily;
        protected override Color MapColour => new Color(212, 162, 56);
        protected override int Dust => DustID.YellowStarfish;
    }

    public class DeepAnemone : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.DeepAnemone;
        protected override Color MapColour => new Color(220, 128, 156);
        protected override int Dust => DustID.PinkStarfish;
    }

    public class Xenophyophore : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.Xenophyophore;
        protected override Color MapColour => new Color(166, 150, 132);
        protected override int Dust => DustID.Sand;
    }

    public class VentMouth : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.VentMouth;
        protected override Color MapColour => new Color(40, 34, 34);
        protected override int Dust => DustID.Smoke;
        protected override int Frames => 4;
        protected override bool Glows => true;

        /// <summary>Water at three hundred and fifty degrees: a dull red, breathing a little.</summary>
        protected override Vector3 Light(int i, int j) => new Vector3(0.32f, 0.09f, 0.02f) * (0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2.3f + i));
    }

    public class WhiteVentMouth : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.WhiteVentMouth;
        protected override Color MapColour => new Color(226, 236, 238);
        protected override int Dust => DustID.Marble;
        protected override int Frames => 4;
    }

    public class SeepVent : SeaLifeTile
    {
        public override SeaLife.Kind Kind => SeaLife.Kind.SeepVent;
        protected override Color MapColour => new Color(78, 60, 54);
        protected override int Dust => DustID.Mud;
    }
}
