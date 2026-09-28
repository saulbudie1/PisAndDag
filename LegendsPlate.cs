using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// The world as the orb leaves it for a moment: two colours, printed like a poster.
    ///
    /// Black: everything that stands. Blocks and whatever else the game draws in its solid
    /// layer (platforms among them), doors, trapdoors and gates open or shut, trees, palms,
    /// cacti, bamboo and giant mushrooms, liquids, players and NPCs.
    ///
    /// Red: everything else, by not being drawn at all. Walls, the sky and the backgrounds,
    /// furniture and work stations, grass, flowers, rubble and all the other walk-through
    /// decoration, projectiles, dropped items, dust and gore.
    ///
    /// Light plays no part: an unlit block is as black as a lit one, a dark wall as red as a
    /// bright one, so noon and midnight give the same picture.
    ///
    /// How: once a frame, while <see cref="LegendsScreenSystem"/> wants it, everything black
    /// is drawn the way the game draws it into an ink target, whatever its colours, and the
    /// ink is laid over flat red as a stencil, its alpha in black. That happens at the start
    /// of the frame, straight after the camera and the zoom are settled for it and before
    /// anything has been drawn to the screen, because FNA clears a target of the game's kind
    /// every time one is bound: switching targets any later would wipe the frame. Main.DoDraw
    /// has no hook at that point, so an IL edit calls <see cref="Render"/> right after
    /// SystemLoader.ModifyTransformMatrix. The result is <see cref="Picture"/>, which the
    /// scene lays over the world through Cinematic.Cover.
    ///
    /// Tiles go through the game's own TileDrawing: GetTileDrawData for where and which
    /// frame, and its private DrawBasicTile for the shapes of slopes, half blocks and
    /// platforms. Never through DrawSingleTile, which is where a tile too dark to see is
    /// skipped. Tree tops go through its private DrawTrees, fed a list of every tree top on
    /// screen, because the game's own list only has the ones that were lit. Both are bound by
    /// reflection at load; if either has gone, the plate stays off and says so.
    ///
    /// Anything drawn a second time here must not move on a frame for it, so the game counts
    /// as paused while the ink is drawn (players would otherwise animate twice as fast, and
    /// trees would shed twice the leaves).
    ///
    /// Client only.
    /// </summary>
    public class LegendsPlate : ModSystem
    {
        /// <summary>The plate's red: the red of the mock the look was set from.</summary>
        public static readonly Color Red = new Color(173, 55, 46);

        /// <summary>How far past the screen's edges tiles are looked at, in tiles: a tree's top reaches well past its trunk.</summary>
        private const int Margin = 6;

        /// <summary>How far past the screen's edges NPCs are looked at, in pixels: big sprites reach well past their hitboxes.</summary>
        private const int NpcMargin = 600;

        private static RenderTarget2D ink;
        private static RenderTarget2D plate;

        /// <summary>True once this frame's picture is made. Cleared first thing every frame.</summary>
        private static bool ready;

        /// <summary>Whether everything the plate needs was found at load.</summary>
        public static bool Available { get; private set; }

        /// <summary>Set after the first failure, so a fault costs one line in the log rather than one a frame.</summary>
        private static bool broken;

        /// <summary>This frame's picture, screen sized, or null if there is none this frame.</summary>
        public static Texture2D Picture => ready ? plate : null;

        /// <summary>Per tile type: drawn black. Built once everything is loaded.</summary>
        private static bool[] inkTile;

        private delegate void BasicTileDrawer(TileDrawing self, Vector2 screenPosition, Vector2 screenOffset,
            int tileX, int tileY, TileDrawInfo drawData, Rectangle normalTileRect, Vector2 normalTilePosition);

        private static BasicTileDrawer drawBasicTile;
        private static Action<TileDrawing> drawTrees;
        private static FieldInfo activeAndNotPaused;
        private static FieldInfo specialsCount;
        private static FieldInfo specialPositions;
        private static FieldInfo batchBegun;

        private static readonly TileDrawInfo info = new TileDrawInfo();
        private static readonly List<Point> treeTops = new List<Point>();
        private static readonly List<Player> players = new List<Player>();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            Type drawing = typeof(TileDrawing);

            MethodInfo basic = drawing.GetMethod("DrawBasicTile", Private);
            MethodInfo trees = drawing.GetMethod("DrawTrees", Private, null, Type.EmptyTypes, null);
            activeAndNotPaused = drawing.GetField("_isActiveAndNotPaused", Private);
            specialsCount = drawing.GetField("_specialsCount", Private);
            specialPositions = drawing.GetField("_specialPositions", Private);
            batchBegun = typeof(SpriteBatch).GetField("beginCalled", Private);

            if (basic == null || trees == null || activeAndNotPaused == null || specialsCount == null || specialPositions == null)
            {
                Mod.Logger.Warn("Legends plate disabled: TileDrawing no longer has the members it draws through "
                    + "(DrawBasicTile, DrawTrees, _isActiveAndNotPaused, _specialsCount, _specialPositions).");
                return;
            }

            drawBasicTile = (BasicTileDrawer)Delegate.CreateDelegate(typeof(BasicTileDrawer), basic);
            drawTrees = (Action<TileDrawing>)Delegate.CreateDelegate(typeof(Action<TileDrawing>), trees);

            IL_Main.DoDraw += CallRenderAfterTheView;
        }

        /// <summary>Puts a call to <see cref="Render"/> straight after the frame's view matrix is final.</summary>
        private void CallRenderAfterTheView(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(SystemLoader), nameof(SystemLoader.ModifyTransformMatrix))))
            {
                Mod.Logger.Warn("Legends plate disabled: Main.DoDraw no longer calls SystemLoader.ModifyTransformMatrix.");
                return;
            }

            // Anything that jumps to the next instruction comes through here too.
            cursor.MoveAfterLabels();
            cursor.EmitDelegate<Action>(Render);
            Available = true;
        }

        public override void PostSetupContent()
        {
            if (Main.dedServ)
                return;

            bool[] black = new bool[TileLoader.TileCount];

            for (int type = 0; type < black.Length; type++)
            {
                // What the game itself draws in front, in its solid layer.
                bool? layer = type < TileID.Sets.DrawTileInSolidLayer.Length ? TileID.Sets.DrawTileInSolidLayer[type] : null;
                black[type] = layer ?? Main.tileSolid[type];

                // Trees, giant mushrooms included (the game counts them as tree trunks).
                if (type < TileID.Sets.IsATreeTrunk.Length && TileID.Sets.IsATreeTrunk[type])
                    black[type] = true;
            }

            // What stands like a tree without being counted as one.
            black[TileID.PalmTree] = true;
            black[TileID.Cactus] = true;
            black[TileID.Bamboo] = true;

            // Doors of every kind, open ones too, so a house keeps its outline. Closed doors,
            // platforms and the like are in already; this adds the open ones and any mod's.
            foreach (int door in TileID.Sets.RoomNeeds.CountsAsDoor)
            {
                if (door >= 0 && door < black.Length)
                    black[door] = true;
            }

            inkTile = black;
        }

        public override void Unload()
        {
            RenderTarget2D oldInk = ink;
            RenderTarget2D oldPlate = plate;

            ink = null;
            plate = null;
            ready = false;
            broken = false;
            Available = false;
            inkTile = null;
            drawBasicTile = null;
            drawTrees = null;
            activeAndNotPaused = null;
            specialsCount = null;
            specialPositions = null;
            batchBegun = null;

            if (oldInk != null || oldPlate != null)
            {
                Main.QueueMainThreadAction(() =>
                {
                    oldInk?.Dispose();
                    oldPlate?.Dispose();
                });
            }
        }

        /// <summary>
        /// Makes this frame's picture, if one is wanted. Called by the IL edit once a frame,
        /// with nothing yet drawn to the screen and the frame's camera and zoom final.
        /// </summary>
        private static void Render()
        {
            ready = false;

            if (Main.dedServ || Main.gameMenu || broken || inkTile == null || LegendsScreenSystem.Amount <= 0f)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            int width = Main.screenWidth;
            int height = Main.screenHeight;

            if (width <= 0 || height <= 0)
                return;

            Ensure(ref ink, device, width, height);
            Ensure(ref plate, device, width, height);

            RenderTargetBinding[] previous = device.GetRenderTargets();
            SpriteBatch batch = Main.spriteBatch;
            bool wasPaused = Main.gamePaused;

            try
            {
                device.SetRenderTarget(ink);
                device.Clear(Color.Transparent);

                Main.gamePaused = true;
                DrawInk(batch);
                Main.gamePaused = wasPaused;

                // The stencil: red everywhere, black wherever there is ink.
                device.SetRenderTarget(plate);
                device.Clear(Red);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                batch.Draw(ink, Vector2.Zero, Color.Black);
                batch.End();

                ready = true;
            }
            catch (Exception e)
            {
                broken = true;
                ModContent.GetInstance<LegendsPlate>().Mod.Logger.Error("Legends plate failed and is off until the mod reloads.", e);

                // Whatever was mid-draw when it threw must not be left open for the frame to trip over.
                if (batchBegun?.GetValue(batch) is bool open && open)
                {
                    try { batch.End(); }
                    catch { }
                }
            }
            finally
            {
                Main.gamePaused = wasPaused;
                device.SetRenderTargets(previous);

                // Binding the screen again can wipe it (FNA clears a target of this kind when it
                // is bound). Nothing had been drawn to it yet this frame except the game's own
                // clear to black, so that is put back.
                if (previous.Length == 0 && device.PresentationParameters.RenderTargetUsage == RenderTargetUsage.DiscardContents)
                    device.Clear(Color.Black);
            }
        }

        private static void Ensure(ref RenderTarget2D target, GraphicsDevice device, int width, int height)
        {
            if (target != null && !target.IsDisposed && target.Width == width && target.Height == height)
                return;

            target?.Dispose();
            target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.None);
        }

        /// <summary>Everything black, in whatever colours the game draws it, into the ink target.</summary>
        private static void DrawInk(SpriteBatch batch)
        {
            TileDrawing renderer = Main.instance.TilesRenderer;
            Vector2 screen = Main.screenPosition;

            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            DrawTiles(batch, renderer, screen);
            DrawTreeTops(renderer);
            DrawNPCs(screen);

            batch.End();

            // The player renderer opens and closes its own batches.
            players.Clear();

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player other = Main.player[i];

                if (i != Main.myPlayer && other.active && !other.outOfRange)
                    players.Add(other);
            }

            players.Add(Main.LocalPlayer);
            Main.PlayerRenderer.DrawPlayers(Main.Camera, players);
        }

        private static void DrawTiles(SpriteBatch batch, TileDrawing renderer, Vector2 screen)
        {
            // Zoom only ever brings the view in, so the unzoomed screen covers it.
            int left = Math.Max(1, (int)(screen.X / 16f) - Margin);
            int top = Math.Max(1, (int)(screen.Y / 16f) - Margin);
            int right = Math.Min(Main.maxTilesX - 2, (int)((screen.X + Main.screenWidth) / 16f) + Margin);
            int bottom = Math.Min(Main.maxTilesY - 2, (int)((screen.Y + Main.screenHeight) / 16f) + Margin);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            treeTops.Clear();

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile)
                    {
                        ushort type = tile.TileType;

                        if (type < inkTile.Length && inkTile[type] && TileDrawing.IsVisible(tile))
                            DrawTile(batch, renderer, tile, type, x, y, screen);
                    }

                    // Liquid as the game fills it: from the bottom of the tile, as deep as it is.
                    if (tile.LiquidAmount > 0)
                    {
                        int dry = (255 - tile.LiquidAmount) / 16;
                        batch.Draw(pixel, new Rectangle(x * 16 - (int)screen.X, y * 16 - (int)screen.Y + dry, 16, 16 - dry), Color.White);
                    }
                }
            }
        }

        /// <summary>One tile, the way TileDrawing.DrawSingleTile would draw it at full light.</summary>
        private static void DrawTile(SpriteBatch batch, TileDrawing renderer, Tile tile, ushort type, int x, int y, Vector2 screen)
        {
            if (!TextureAssets.Tile[type].IsLoaded)
                Main.instance.LoadTiles(type);

            info.tileCache = tile;
            info.typeCache = type;
            info.tileFrameX = tile.TileFrameX;
            info.tileFrameY = tile.TileFrameY;
            info.tileLight = Color.White;

            renderer.GetTileDrawData(x, y, tile, type, ref info.tileFrameX, ref info.tileFrameY, out info.tileWidth,
                out info.tileHeight, out info.tileTop, out info.halfBrickHeight, out info.addFrX, out info.addFrY,
                out info.tileSpriteEffect, out info.glowTexture, out info.glowSourceRect, out info.glowColor);

            info.drawTexture = renderer.GetTileDrawTexture(tile, x, y);
            info.colorTint = Color.White;
            info.finalColor = Color.White;

            // Tree tops and branches are drawn apart, from this list (TileDrawing's own test).
            if (type == TileID.PalmTree)
            {
                if (info.tileFrameX >= 88 && info.tileFrameX <= 132)
                    treeTops.Add(new Point(x, y));
            }
            else if (type == TileID.Trees || (type >= TileID.TreeTopaz && type <= TileID.TreeAmber)
                || type == TileID.VanityTreeSakura || type == TileID.VanityTreeYellowWillow || type == TileID.TreeAsh)
            {
                if (info.tileFrameY >= 198 && info.tileFrameX >= 22)
                    treeTops.Add(new Point(x, y));
            }

            Rectangle frame = new Rectangle(info.tileFrameX + info.addFrX, info.tileFrameY + info.addFrY,
                info.tileWidth, info.tileHeight - info.halfBrickHeight);

            Vector2 at = new Vector2(x * 16 - (int)screen.X - (info.tileWidth - 16f) / 2f,
                y * 16 - (int)screen.Y + info.tileTop + info.halfBrickHeight);

            // The few TileDrawing.DrawSingleTile handles itself before drawing the tile.
            switch (type)
            {
                case TileID.MushroomTrees:
                    if (info.tileFrameX >= 36)
                    {
                        int cap = info.tileFrameY == 18 ? 1 : info.tileFrameY == 36 ? 2 : 0;

                        batch.Draw(TextureAssets.ShroomCap.Value, new Vector2(x * 16 - (int)screen.X - 22, y * 16 - (int)screen.Y - 26),
                            new Rectangle(cap * 62, 0, 60, 42), Color.White, 0f, Vector2.Zero, 1f, info.tileSpriteEffect, 0f);
                    }

                    break;

                case TileID.Cactus:
                    WorldGen.GetCactusType(x, y, info.tileFrameX, info.tileFrameY, out bool evil, out bool good, out bool crimson);

                    if (evil)
                        frame.Y += 54;

                    if (good)
                        frame.Y += 108;

                    if (crimson)
                        frame.Y += 162;

                    break;

                case TileID.PalmTree:
                    // The top is all foliage, drawn with the other tree tops; the trunk leans
                    // by as many pixels as its frame Y says.
                    if (tile.TileFrameX >= 88 && tile.TileFrameX <= 132)
                        return;

                    at.X += tile.TileFrameY;
                    break;
            }

            // A whole, level tile is its frame, drawn once: what TileDrawing.DrawBasicTile
            // comes to for one, less the nine-way split it makes for smooth light, which a
            // stencil does not need. Slopes, half blocks and platforms go through it for
            // their shapes.
            if (tile.Slope == SlopeType.Solid && !tile.IsHalfBlock && !TileID.Sets.Platforms[type])
                batch.Draw(info.drawTexture, at, frame, Color.White, 0f, Vector2.Zero, 1f, info.tileSpriteEffect, 0f);
            else
                drawBasicTile(renderer, screen, Vector2.Zero, x, y, info, frame, at);
        }

        /// <summary>
        /// Tree tops and branches through TileDrawing.DrawTrees, which draws from its own list
        /// of them. That list is lent our list for the call and given back as it was.
        /// </summary>
        private static void DrawTreeTops(TileDrawing renderer)
        {
            if (treeTops.Count == 0)
                return;

            const int Tree = (int)TileDrawing.TileCounterType.Tree;

            int[] counts = (int[])specialsCount.GetValue(renderer);
            Point[][] positions = (Point[][])specialPositions.GetValue(renderer);
            Point[] list = positions[Tree];

            int theirs = counts[Tree];
            Point[] kept = new Point[theirs];
            Array.Copy(list, kept, theirs);

            bool wasActive = (bool)activeAndNotPaused.GetValue(renderer);

            try
            {
                int ours = Math.Min(treeTops.Count, list.Length);

                for (int i = 0; i < ours; i++)
                    list[i] = treeTops[i];

                counts[Tree] = ours;

                // Not active, so it sheds no leaves for being drawn twice.
                activeAndNotPaused.SetValue(renderer, false);
                drawTrees(renderer);
            }
            finally
            {
                activeAndNotPaused.SetValue(renderer, wasActive);
                Array.Copy(kept, list, theirs);
                counts[Tree] = theirs;
            }
        }

        private static void DrawNPCs(Vector2 screen)
        {
            Rectangle near = new Rectangle((int)screen.X - NpcMargin, (int)screen.Y - NpcMargin,
                Main.screenWidth + NpcMargin * 2, Main.screenHeight + NpcMargin * 2);

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type <= NPCID.None || npc.hide || !near.Intersects(npc.Hitbox))
                    continue;

                Main.instance.DrawNPC(i, npc.behindTiles);
            }
        }
    }
}
