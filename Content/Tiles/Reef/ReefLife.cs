using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles.Reef
{
    /// <summary>
    /// The reefs' life (ReefPlan.Decor), put down on them by the place as it is made (VastOceanReef), never by hand, and
    /// nothing comes of breaking it: an object standing on whole solid ground (its last row two pixels into it), or
    /// hanging from under it, its pictures side by side, one to a style. By request ("Soft glow"): the anemones'
    /// tips, the zoanthids and the clams' mantles glow softly, lit a little and drawn over in their own light
    /// (<c>Glow</c> sheet); and by request ("with glowing sea pickles basically ambient torches") the sea pickles light
    /// the reef about them as a torch would, more of them in a cluster the brighter.
    /// </summary>
    public abstract class ReefLifeTile : ModTile
    {
        /// <summary>Each kind's tile type once loaded; 0 before.</summary>
        private static readonly ushort[] types = new ushort[ReefPlan.Decors];

        public static ushort TypeOf(ReefPlan.Decor kind) => types[(int)kind];

        public abstract ReefPlan.Decor Kind { get; }

        /// <summary>What the map calls it and shows it as, by style: from each style named here (in order) to the next.</summary>
        protected abstract (int from, string name, Color colour)[] Map { get; }

        protected virtual int Dust => DustID.Coralstone;

        protected virtual SoundStyle? Sound => SoundID.Dig;

        /// <summary>Whether this style glows, and the light it gives (nothing for one that does not).</summary>
        protected virtual Vector3 Light(int style) => Vector3.Zero;

        private int Wide => ReefPlan.WideOf(Kind);

        private int Tall => ReefPlan.TallOf(Kind);

        private bool Hangs => Kind == ReefPlan.Decor.Hanging;

        private Asset<Texture2D> glow;

        public override void SetStaticDefaults()
        {
            types[(int)Kind] = Type;

            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;

            int[] heights = new int[Tall];

            for (int k = 0; k < Tall; k++)
                heights[k] = k == Tall - 1 && !Hangs ? 18 : 16;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(0, Hangs ? 0 : Tall - 1);
            TileObjectData.newTile.CoordinateHeights = heights;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.AnchorTop = Hangs ? new AnchorData(AnchorType.SolidTile, Wide, 0) : AnchorData.Empty;
            TileObjectData.newTile.AnchorBottom = Hangs ? AnchorData.Empty : new AnchorData(AnchorType.SolidTile, Wide, 0);
            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.WaterPlacement = LiquidPlacement.Allowed;
            TileObjectData.newTile.WaterDeath = false;
            TileObjectData.newTile.LavaPlacement = LiquidPlacement.NotAllowed;
            TileObjectData.newTile.LavaDeath = true;
            TileObjectData.addTile(Type);

            DustType = Dust;
            HitSound = Sound;

            foreach (var entry in Map)
                AddMapEntry(entry.colour, this.GetLocalization("Map" + entry.name));

            if (!Main.dedServ && ModContent.HasAsset(Texture + "Glow"))
                glow = ModContent.Request<Texture2D>(Texture + "Glow");
        }

        /// <summary>The style of the object this cell is of.</summary>
        private int StyleAt(int i, int j) => Main.tile[i, j].TileFrameX / (Wide * 18);

        public override ushort GetMapOption(int i, int j)
        {
            int style = StyleAt(i, j);
            var map = Map;

            for (int k = map.Length - 1; k > 0; k--)
                if (style >= map[k].from)
                    return (ushort)k;

            return 0;
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

        /// <summary>A slow breath in the light, its own for each place.</summary>
        private static float Breath(int i, int j) => 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + i * 0.9f + j * 0.4f);

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            Vector3 light = Light(StyleAt(i, j)) * Breath(i, j);

            r = light.X;
            g = light.Y;
            b = light.Z;
        }

        /// <summary>What of a glowing style glows, drawn over it at full light, a little less than whole.</summary>
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (glow == null || !glow.IsLoaded || Light(StyleAt(i, j)) == Vector3.Zero)
                return;

            Tile tile = Main.tile[i, j];
            int row = tile.TileFrameY / 18;
            int height = row == Tall - 1 && !Hangs ? 18 : 16;
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 at = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offScreen;

            spriteBatch.Draw(glow.Value, at, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, height), Color.White * (0.75f * Breath(i, j)));
        }
    }

    /// <summary>One tile: anemones (glowing), sea urchins, starfish, zoanthids (glowing).</summary>
    public class ReefPolyp : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Polyp;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        {
            (0, "Anemone", new Color(220, 128, 156)),
            (4, "Urchin", new Color(60, 52, 72)),
            (6, "Starfish", new Color(230, 138, 70)),
            (9, "Zoanthids", new Color(98, 182, 120)),
        };

        protected override int Dust => DustID.PinkStarfish;

        /// <summary>The anemones' tips, in their own colours, and the zoanthids': soft.</summary>
        protected override Vector3 Light(int style) => style switch
        {
            0 => new Vector3(0.24f, 0.12f, 0.17f),
            1 => new Vector3(0.12f, 0.24f, 0.14f),
            2 => new Vector3(0.18f, 0.12f, 0.26f),
            3 => new Vector3(0.26f, 0.17f, 0.08f),
            9 => new Vector3(0.1f, 0.24f, 0.12f),
            10 => new Vector3(0.26f, 0.16f, 0.07f),
            11 => new Vector3(0.08f, 0.22f, 0.22f),
            _ => Vector3.Zero,
        };
    }

    /// <summary>One wide, two tall: tube sponges, sea whips.</summary>
    public class ReefSponge : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Sponge;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        {
            (0, "TubeSponge", new Color(222, 186, 64)),
            (3, "SeaWhip", new Color(204, 70, 70)),
        };

        protected override int Dust => DustID.YellowStarfish;
    }

    /// <summary>Two by two: sea fans, branching corals, brain corals.</summary>
    public class ReefCoral : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Coral;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        {
            (0, "SeaFan", new Color(146, 92, 196)),
            (3, "BranchingCoral", new Color(230, 138, 70)),
            (6, "BrainCoral", new Color(186, 176, 102)),
        };

        protected override SoundStyle? Sound => SoundID.Tink;
    }

    /// <summary>Two wide, one tall: giant clams (their mantles glowing), mushroom corals.</summary>
    public class ReefClam : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Clam;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        {
            (0, "GiantClam", new Color(110, 180, 230)),
            (3, "MushroomCoral", new Color(190, 166, 116)),
        };

        protected override int Dust => DustID.BeachShell;

        protected override SoundStyle? Sound => SoundID.Tink;

        protected override Vector3 Light(int style) => style switch
        {
            0 => new Vector3(0.06f, 0.16f, 0.3f),
            1 => new Vector3(0.18f, 0.1f, 0.3f),
            2 => new Vector3(0.06f, 0.26f, 0.16f),
            _ => Vector3.Zero,
        };
    }

    /// <summary>Three wide, two tall: table corals.</summary>
    public class ReefTable : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Table;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        { (0, "TableCoral", new Color(70, 176, 176)) };

        protected override SoundStyle? Sound => SoundID.Tink;
    }

    /// <summary>One wide, two tall, hanging from under a reef: soft corals.</summary>
    public class ReefHanging : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Hanging;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        { (0, "SoftCoral", new Color(214, 104, 146)) };

        protected override int Dust => DustID.PinkStarfish;
    }

    /// <summary>One tile: one to four sea pickles (by style), lighting the reef as a torch would, the more the brighter.</summary>
    public class SeaPickle : ReefLifeTile
    {
        public override ReefPlan.Decor Kind => ReefPlan.Decor.Pickle;

        protected override (int from, string name, Color colour)[] Map => Entries;

        private static readonly (int from, string name, Color colour)[] Entries =
        { (0, "SeaPickle", new Color(150, 222, 120)) };

        protected override int Dust => DustID.GreenTorch;

        protected override Vector3 Light(int style) => new Vector3(0.5f, 0.82f, 0.42f) * (0.62f + 0.14f * style);
    }
}
