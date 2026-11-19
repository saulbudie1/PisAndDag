using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles.Lab
{
    /// <summary>
    /// The Abandoned Lab's own furnishings, by request ("check calamity fables mod to see how ... they decorate their lab;
    /// also check calamity to see how they decorate draedon's lab to add our very own lab decoration"; "Screens &amp;
    /// consoles, Server racks &amp; cabinets, Pipes, fans &amp; hazard signs, Dining &amp; safe-room furniture"; and "in
    /// document room the blues are the book shelves; create our own 6 tall and 6 wide sheves"): what a lab of this kind
    /// would have had in it (the two references were read for that, and for nothing of how they look; the art is our own,
    /// drawn by code, every art pixel two by two).
    ///
    /// Each kind (LabPlan.LabDecor: its size, whether it hangs on a wall or stands, how many looks) is a tile of several
    /// tiles, its looks side by side on its sheet; one that glows has a second sheet (its name and Glow) of what is lit on
    /// it, a frame under another, drawn over it unlit by the place's light, each fitting in its own time; one that is lit
    /// gives a little light of its colour. Put down by the lab as it is laid out, never by hand: no items, and nothing
    /// comes of breaking one (the lab lets none of them be broken).
    /// </summary>
    public abstract class LabFurnishing : ModTile
    {
        /// <summary>Its kind (LabPlan.LabDecor).</summary>
        public abstract int Kind { get; }

        /// <summary>The tile type of each kind, as the game numbered them (LabWorld puts them down by kind).</summary>
        public static readonly int[] TypeOf = new int[LabPlan.LabDecor.Count];

        protected int Wide => LabPlan.LabDecor.Size[Kind].Wide;
        protected int Tall => LabPlan.LabDecor.Size[Kind].Tall;
        protected bool OnWall => LabPlan.LabDecor.Size[Kind].OnWall;

        /// <summary>Frames of its glow sheet (none: no glow), ticks a frame; the light it gives; its colour on the map.</summary>
        protected virtual int GlowFrames => 0;
        protected virtual int GlowTicks => 10;
        protected virtual Vector3 Gives => Vector3.Zero;
        protected virtual Color Map => new Color(84, 90, 100);

        private Asset<Texture2D> glow;

        public override void SetStaticDefaults()
        {
            TypeOf[Kind] = Type;

            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = Gives != Vector3.Zero;

            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(OnWall ? TileObjectData.Style3x3Wall : TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = OnWall ? new Point16(0, 0) : new Point16(0, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = Enumerable.Repeat(16, Tall).ToArray();
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;

            if (OnWall)
            {
                TileObjectData.newTile.AnchorWall = true;
                TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            }
            else
            {
                TileObjectData.newTile.AnchorWall = false;
                TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, Wide, 0);
            }

            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            DustType = DustID.Iron;
            HitSound = SoundID.Tink;

            AddMapEntry(Map, CreateMapEntryName());

            if (!Main.dedServ && GlowFrames > 0)
                glow = ModContent.Request<Texture2D>(Texture + "Glow");

            More();
        }

        /// <summary>Anything more a kind sets up.</summary>
        protected virtual void More()
        {
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        /// <summary>Its top left tile.</summary>
        public static Point TopLeft(int i, int j, int wide, int tall)
        {
            Tile tile = Main.tile[i, j];
            return new Point(i - tile.TileFrameX % (wide * 18) / 18, j - tile.TileFrameY % (tall * 18) / 18);
        }

        protected static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            if (Gives == Vector3.Zero)
                return;

            r = Gives.X;
            g = Gives.Y;
            b = Gives.Z;
        }

        /// <summary>What is lit on it, over it: this fitting's frame, in its own time.</summary>
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (glow == null || !glow.IsLoaded)
                return;

            Tile tile = Main.tile[i, j];
            Point top = TopLeft(i, j, Wide, Tall);
            int frame = (int)((Main.GameUpdateCount / (uint)GlowTicks + Hash(top.X, top.Y) % 64u) % (uint)GlowFrames);
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 at = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offScreen;
            Rectangle source = new Rectangle(tile.TileFrameX, tile.TileFrameY % (Tall * 18) + frame * Tall * 18, 16, 16);

            spriteBatch.Draw(glow.Value, at, source, Color.White);
        }
    }

    // ---- the kinds ------------------------------------------------------------------------------------------------------

    /// <summary>A case of four steel shelves of records, six by six: binders, papers, boxes of files; full, half gone, or in disorder.</summary>
    public class LabBookshelf : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Bookshelf;
        protected override Color Map => new Color(92, 80, 70);
    }

    /// <summary>A screen on the wall, three by two: lines of green going by, or a trace.</summary>
    public class LabMonitor : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Monitor;
        protected override int GlowFrames => 4;
        protected override int GlowTicks => 14;
        protected override Vector3 Gives => new(0.04f, 0.12f, 0.06f);
    }

    /// <summary>A desk of controls, four by three, its small screen and its lamps still going; one with papers left on it.</summary>
    public class LabConsole : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Console;
        protected override int GlowFrames => 4;
        protected override int GlowTicks => 12;
        protected override Vector3 Gives => new(0.03f, 0.09f, 0.05f);
    }

    /// <summary>A wide screen on the wall, six by three: text and a trace; one cracked, only flickers of it left.</summary>
    public class LabTerminal : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Terminal;
        protected override int GlowFrames => 4;
        protected override int GlowTicks => 16;
        protected override Vector3 Gives => new(0.05f, 0.14f, 0.07f);
    }

    /// <summary>A rack of units, two by four, its lamps blinking: green, or most of them red.</summary>
    public class LabServerRack : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.ServerRack;
        protected override int GlowFrames => 8;
        protected override int GlowTicks => 9;
        protected override Vector3 Gives => new(0.02f, 0.06f, 0.03f);
    }

    /// <summary>A filing cabinet, two by three; one with its top drawer out, papers up out of it.</summary>
    public class LabFilingCabinet : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.FilingCabinet;
    }

    /// <summary>A length of pipe along a wall, four by two, a run of them joining; one with a gauge on it.</summary>
    public class LabPipe : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Pipe;
        protected override Color Map => new Color(96, 100, 110);
    }

    /// <summary>A valve in a run of pipe, two by two, its red wheel.</summary>
    public class LabValve : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Valve;
        protected override Color Map => new Color(120, 70, 70);
    }

    /// <summary>A fan in the wall, three by three, turning.</summary>
    public class LabFan : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Fan;

        protected override void More() => AnimationFrameHeight = 3 * 18;

        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            if (++frameCounter >= 4)
            {
                frameCounter = 0;
                frame = (frame + 1) % 4;
            }
        }
    }

    /// <summary>A sign on the wall, two by two: what grows here (a knot of hooked strands in a yellow diamond), or a fall.</summary>
    public class LabHazardSign : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.HazardSign;
        protected override Color Map => new Color(170, 140, 40);
    }

    /// <summary>A canteen table, five by two, its benches with it.</summary>
    public class LabCanteenTable : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.CanteenTable;
    }

    /// <summary>A vending machine, three by four, still lit.</summary>
    public class LabVendingMachine : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.VendingMachine;
        protected override int GlowFrames => 4;
        protected override int GlowTicks => 20;
        protected override Vector3 Gives => new(0.05f, 0.12f, 0.07f);
        protected override Color Map => new Color(80, 100, 90);
    }

    /// <summary>A locker, two by four; one ajar.</summary>
    public class LabLocker : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Locker;
        protected override Color Map => new Color(64, 84, 98);
    }

    /// <summary>A kitchen counter, three by two: cupboards, or a hob and its oven.</summary>
    public class LabCounter : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Counter;
        protected override Color Map => new Color(110, 102, 92);
    }

    /// <summary>A length of the elevator's rail, one tile, on the wall of its shaft.</summary>
    public class LabElevatorRail : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.ElevatorRail;
    }

    /// <summary>
    /// The lamp over the safe room's door, two by one: a violet tube, and the Cultures will not come past it (CulturePath,
    /// Culture). By request: "Cultures can't enter".
    /// </summary>
    public class LabUVLamp : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.UVLamp;
        protected override int GlowFrames => 1;
        protected override Vector3 Gives => new(0.32f, 0.12f, 0.6f);
        protected override Color Map => new Color(150, 100, 220);
    }

    /// <summary>
    /// A bunk, four by three, in the safe room: lain in (right-click), it is where one wakes after dying in the lab
    /// (LabPlayer), as a bed is anywhere. By request: "a bed you can sleep in to set your respawn".
    /// </summary>
    public class LabBunk : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.Bunk;
        protected override Color Map => new Color(70, 92, 76);

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ItemID.Bed;
        }

        public override bool RightClick(int i, int j)
        {
            Point top = TopLeft(i, j, Wide, Tall);
            Main.LocalPlayer.GetModPlayer<LabPlayer>().Bunk = top;
            Main.NewText(Language.GetTextValue("Game.SpawnPointSet"), 255, 240, 20);
            return true;
        }
    }

    /// <summary>The elevator's panel at each floor, one by two: right-click to call the car to this floor, or, the car here, to send it to the other (LabElevator).</summary>
    public class LabElevatorPanel : LabFurnishing
    {
        public override int Kind => LabPlan.LabDecor.ElevatorPanel;
        protected override int GlowFrames => 2;
        protected override int GlowTicks => 30;
        protected override Vector3 Gives => new(0.04f, 0.1f, 0.05f);

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ItemID.Lever;
        }

        public override bool RightClick(int i, int j)
        {
            int floor = j < (LabPlan.ElevatorTop + LabPlan.ElevatorBottom) / 2 ? LabElevator.Top : LabElevator.Bottom;
            LabElevator.Press(floor);
            return true;
        }
    }
}
