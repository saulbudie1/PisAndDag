using System;
using Terraria.ID;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The Kingdom of Grace, laid out and built, west to east in the order the player
    /// meets it (PAD_Grace_Structure_Plan.md): the willows on the flat of the plateau, the
    /// Curtain with the bore through its foot, the Processional, the Low City with its
    /// granaries and market and watch tower, the High Ward and the Court of Grace, the
    /// Font over the pool shaft, and the Dry Shore at the end of the world. Under it all,
    /// the Undercity, the Long Reliquary and the room around the Shimmer.
    ///
    /// Leyndell in its ashen hour: white stone and gold, whole where the siege was and
    /// ruined where it was not, and ash lying on everything.
    /// </summary>
    public sealed partial class GraceBuilder
    {
        // ------------------------------------------------------------ the plan, in columns

        /// <summary>The first column of flat ground: where the willows begin.</summary>
        private int ForestStart;

        /// <summary>The Curtain's outer face.</summary>
        private int CurtainX;

        /// <summary>How thick the Curtain is, and how tall over the plain.</summary>
        private const int CurtainThick = 40;
        private const int CurtainHeight = 160;

        /// <summary>The first column inside the Curtain.</summary>
        private int Inside => CurtainX + CurtainThick;

        /// <summary>Quarter boundaries. Each is the first column of the quarter after it.</summary>
        private int LowCityX;
        private int WardX;
        private int FontX;
        private int ShoreX;

        /// <summary>The last column anyone can stand in.</summary>
        private int EndX;

        /// <summary>How wide the quarters east of the Processional are, laid back from the end of the world.</summary>
        private const int ShoreW = 138, FontW = 210, WardW = 340, LowW = 340;

        /// <summary>The Font chamber's place in its quarter, and so the shaft's.</summary>
        private const int ArenaInset = 22, ArenaW = 160;

        /// <summary>
        /// The plan column the Font's shaft comes down at, for a plain this wide: where the
        /// Shimmer has to be. Worldgen asks this long before the kingdom is built.
        /// </summary>
        public static int ShaftColumn(int plainWidth, int edgeMargin)
        {
            int fontX = plainWidth - edgeMargin - ShoreW - FontW;
            int a0 = fontX + ArenaInset;
            return (a0 + a0 + ArenaW - 1) / 2;
        }

        /// <summary>The fewest columns of willow before the wall, and of road between the wall and the town.</summary>
        private const int ForestMin = 90;
        private const int ProcMin = 130;

        private void Layout()
        {
            ForestStart = Math.Max(0, site.ForestStart);
            EndX = site.PlainWidth - site.EdgeMargin;

            // The quarters are laid from the far end back, so the court keeps its size
            // whatever the ramp took.
            ShoreX = EndX - ShoreW;
            FontX = ShoreX - FontW;
            WardX = FontX - WardW;
            int lowNominal = WardX - LowW;

            // The Curtain stands a hundred and fifty into the willows if there is room, never
            // closer to the Low City than the Processional needs, never nearer the ramp than
            // the least of a wood. If that still leaves the Processional short, the Low City
            // gives up its west end and builds fewer houses.
            CurtainX = Math.Min(lowNominal - 170 - CurtainThick, Math.Max(ForestStart + 150, 480));
            CurtainX = Math.Max(CurtainX, ForestStart + ForestMin);
            LowCityX = Math.Max(lowNominal, CurtainX + CurtainThick + ProcMin);
        }

        private void Run()
        {
            Layout();

            Ground();
            Forest();
            Curtain();
            Processional();
            LowCity();
            HighWard();
            Font();
            DryShore();

            Undercity();
            Reliquary();
            Rootworks();

            // The finish, over everything at once.
            int fx0 = Math.Max(0, ForestStart - 30), fx1 = site.PlainWidth;
            Texture(Inside - CurtainThick - 16, fx1, -site.Top, site.Depth);
            Vines(Inside - CurtainThick - 16, fx1, -site.Top, site.Depth);
            Ashfall();
            Hammer(fx0, -site.Top, fx1, site.Depth);

            // After the hammer, so the crack's brick keeps exactly the cuts its rock had.
            DeepShell();

            FillBehindCuts(fx0, -site.Top, fx1, site.Depth);
            Backfill(fx0, -site.Top, fx1, site.Depth);

            // Last of all: no room of it will pass for a house.
            Unhouse(fx0 - 20, -site.Top, fx1 + 20, site.Depth);
        }

        // ------------------------------------------------------------ the ground

        /// <summary>
        /// Inside the Curtain the ground is made level: the plain's gentle swell is cut
        /// and filled to the one line, and paved, so every building stands on the same
        /// floor. Everything above is cleared to the sky.
        /// </summary>
        private void Ground()
        {
            for (int x = Inside; x <= site.PlainWidth; x++)
            {
                for (int y = -site.Top; y < 0; y++)
                {
                    c.Erase(x, y);
                    c.EraseWall(x, y);
                    c.Liquid(x, y, 0, 0);
                }

                for (int y = 0; y <= 6; y++)
                    c.Block(x, y, y == 0 ? TileID.StoneSlab : TileID.MarbleBlock, y == 0 ? White : NoPaint);
            }
        }
    }
}
