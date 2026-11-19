using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// The Abandoned Lab's lights, by request ("create a light decor chandelier typa tile that hangs on walls; flicker
    /// rarely", "to decorate the lab", "light decor is a flat flurescent bulb 1x4wide", "glow the color #82bd7d downwards",
    /// "like a long flursecent bulb you have on hospitals"): a long fluorescent tube in a steel housing, four tiles wide
    /// and one tall, hung on the wall behind it. It throws its light down: a little where it hangs, more on what is under
    /// it, as far as six tiles or the floor (Lighting, added each frame it is drawn), and a beam of it drawn down from the
    /// tube, the colour of the light (#82bd7d), widening and fading.
    ///
    /// Seven of them (styles, one under another on the sheet): working, which now and then, rarely, stutters off and on
    /// for a moment; failing, its ends gone black, which does it often; dead, cracked, dark; a red working one and a red
    /// failing one (#d64a46; by request: "this room can use red fluorescent light", the hall); and a white working one and
    /// a white failing one (#e6eaf0; by request: "white light in document room"). Each fitting keeps its own
    /// time (from where it hangs), so no two stutter together.
    ///
    /// Put up by the lab as it is laid out (LabPlan), never by hand; there is no item for it, and nothing comes of breaking
    /// one.
    /// </summary>
    public class LabLight : ModTile
    {
        public const int Wide = 4;
        public const int Tall = 1;

        public const int Working = 0, Failing = 1, Dead = 2, RedWorking = 3, RedFailing = 4, WhiteWorking = 5, WhiteFailing = 6;

        private const int FrameStep = 18;

        /// <summary>Its light: #82bd7d; or, the red ones, #d64a46; or, the white ones, #e6eaf0.</summary>
        public static readonly Color Colour = new Color(130, 189, 125);
        public static readonly Color Red = new Color(214, 74, 70);
        public static readonly Color White = new Color(230, 234, 240);

        private static Color ColourAt(int i, int j)
        {
            int style = StyleAt(i, j);
            return style >= WhiteWorking ? White : style >= RedWorking ? Red : Colour;
        }

        private static bool FailingAt(int style) => style == Failing || style == RedFailing || style == WhiteFailing;

        /// <summary>How much of its light stays where it hangs, and how much falls on each tile under it, nearest first.</summary>
        private const float AtTube = 0.45f;
        private static readonly float[] Under = { 0.95f, 0.85f, 0.7f, 0.55f, 0.38f, 0.22f };

        /// <summary>How strong its beam is drawn.</summary>
        private const float BeamStrength = 0.26f;

        private Asset<Texture2D> glow, beam;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = true;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(0, 0);
            TileObjectData.newTile.CoordinateHeights = new[] { 16 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorWall = true;
            TileObjectData.newTile.AnchorTop = AnchorData.Empty;
            TileObjectData.newTile.AnchorBottom = AnchorData.Empty;
            TileObjectData.newTile.AnchorLeft = AnchorData.Empty;
            TileObjectData.newTile.AnchorRight = AnchorData.Empty;
            TileObjectData.newTile.StyleHorizontal = false;
            TileObjectData.newTile.LavaDeath = true;
            TileObjectData.addTile(Type);

            DustType = DustID.Glass;
            HitSound = SoundID.Shatter;

            AddMapEntry(new Color(170, 200, 166), CreateMapEntryName());

            if (!Main.dedServ)
            {
                glow = ModContent.Request<Texture2D>(Texture + "Glow");
                beam = ModContent.Request<Texture2D>(Texture + "Beam");
            }
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        // ---- lit ---------------------------------------------------------------------------------------------

        private static int StyleAt(int i, int j) => Main.tile[i, j].TileFrameY / FrameStep;

        private static int LeftOf(int i, int j) => i - Main.tile[i, j].TileFrameX / FrameStep % Wide;

        /// <summary>
        /// How lit the fitting this tile is of is now, 0 to 1: dead, never; otherwise fully, but for its stutters, each a
        /// second or so of off and on: a working one once in thirty to ninety seconds, a failing one once in four to ten.
        /// </summary>
        public static float Lit(int i, int j)
        {
            int style = StyleAt(i, j);

            if (style == Dead)
                return 0f;

            uint h = Hash(LeftOf(i, j), j);
            bool failing = FailingAt(style);
            uint period = failing ? 240u + h % 360u : 1800u + h % 3600u;
            uint length = failing ? 54u : 40u;
            uint t = (Main.GameUpdateCount + h) % period;

            if (t >= length)
                return failing ? 0.82f : 1f;

            // In a stutter: off and on, three ticks at a time, by its own pattern; dim, not quite out, when off.
            uint step = t / 3u;
            bool on = Hash((int)step, (int)h) % 3u != 0u;

            return on ? 0.75f : 0.06f;
        }

        private static uint Hash(int x, int y)
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
            float lit = Lit(i, j) * AtTube;
            Vector3 light = ColourAt(i, j).ToVector3() * lit;

            r = light.X;
            g = light.Y;
            b = light.Z;
        }

        /// <summary>
        /// The tube lit, over it; and its light thrown down this tile's column, to the floor or six tiles; and from the
        /// fitting's first tile, the beam, drawn in the light's colour so that it adds to what is behind it.
        /// </summary>
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            float lit = Lit(i, j);

            if (lit <= 0f)
                return;

            Tile tile = Main.tile[i, j];
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 at = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offScreen;

            if (glow != null && glow.IsLoaded)
                spriteBatch.Draw(glow.Value, at, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16), Color.White * lit);

            Color colour = ColourAt(i, j);
            Vector3 light = colour.ToVector3() * lit;

            for (int k = 0; k < Under.Length; k++)
            {
                int y = j + 1 + k;

                if (!WorldGen.InWorld(i, y) || WorldGen.SolidTile(i, y))
                    break;

                Lighting.AddLight(i, y, light.X * Under[k], light.Y * Under[k], light.Z * Under[k]);
            }

            if (i != LeftOf(i, j) || beam == null || !beam.IsLoaded)
                return;

            Texture2D picture = beam.Value;
            Vector2 under = at + new Vector2(Wide * 8 - picture.Width * 0.5f, 12f);

            // A colour with no alpha adds to what is drawn behind it, in the game's premultiplied drawing.
            Color add = new Color(colour.R, colour.G, colour.B, 0) * (BeamStrength * lit);

            spriteBatch.Draw(picture, under, add);
        }
    }
}
