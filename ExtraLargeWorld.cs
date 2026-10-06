using System;
using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.GameContent.Biomes;
using Terraria.UI;
using Terraria.WorldBuilding;

namespace PisAndDag
{
    /// <summary>
    /// A fourth world size, past Large.
    ///
    /// 12600 x 3000: one and a half times a Large world across, one and a quarter down.
    /// Vanilla lays its layers out as fractions of the world height, so the sky, the
    /// dirt layer and the rock layer all come with it; only the Underworld is a fixed
    /// two hundred rows off the bottom and stays where it is. The height is a quarter
    /// rather than a fifth because a world is divided into sections of 200 by 150 for
    /// drawing and for sending over the wire, and every vanilla size is an exact number
    /// of them both ways. 2880 is not: the last row of sections would run 120 rows off
    /// the bottom of the map and the fullscreen map would read past the end of it.
    /// 3000 is 20 sections exactly, and the sky is the part that gains by it.
    ///
    /// Everything vanilla counts by width comes out one and a half times over on its
    /// own, because every one of those formulas is linear in Main.maxTilesX:
    /// floating islands (maxTilesX * 0.0008) go 6 to 10, living trees (maxTilesX /
    /// 4200) go 2 to 3, gem caves (maxTilesX * 0.003) go 25 to 37, and the cabins,
    /// ore veins, chests and the rest follow the same rule. None of those have a size
    /// that scales, so a giant tree here is a giant tree there.
    ///
    /// Two structures do scale their own size with the world, and they are held back
    /// to what a Large world would have given them: the Dungeon and the Lihzahrd
    /// Temple. Both are clamped by IL edit rather than by moving maxTilesX around,
    /// because the same field is doing bounds checking three lines further down and
    /// lying to it would put the dungeon through the edge of the world.
    ///
    /// Vanilla allocates the tile array, the map and the map's render targets once,
    /// at startup, big enough for a Large world and no bigger. WorldGen.clearWorld is
    /// the one gate every path goes through -- new world, loading a world, and a
    /// multiplayer client being told the world size -- so the growing happens there,
    /// before anything writes a tile.
    ///
    /// A world made with this cannot be opened without the mod. Nothing warns the
    /// player about that; vanilla would simply write past the end of its own arrays.
    /// </summary>
    public class ExtraLargeWorld : ModSystem
    {
        /// <summary>Large is 8400. One and a half times, and 63 sections exactly.</summary>
        public const int Width = 12600;

        /// <summary>Large is 2400. One and a quarter, 20 sections exactly, layers riding along.</summary>
        public const int Height = 3000;

        /// <summary>A world is drawn and sent one section at a time. These are not negotiable.</summary>
        private const int SectionWidth = 200;
        private const int SectionHeight = 150;

        /// <summary>
        /// The world currently loaded, or about to be, is one of these. Read during
        /// generation as well as play: maxTilesX and maxTilesY are set before the
        /// first pass runs and before a client is sent anything.
        /// </summary>
        public static bool Active => Main.maxTilesX == Width && Main.maxTilesY == Height;

        /// <summary>What a structure that scales with the world is allowed to think it is.</summary>
        private const int StructureWidth = 8400;

        /// <summary>The other half of that, for anything that scales by area.</summary>
        private const int LargeHeight = 2400;

        /// <summary>The fourth value of the private WorldSizeId enum. Vanilla uses 0, 1, 2.</summary>
        private const int SizeOption = 3;

        /// <summary>The Grand button's icon, inside the mod. 42 by 32, like vanilla's.</summary>
        private const string IconPath = "Content/ExtraTexture/GrandWorldSize";

        /// <summary>What the button wears until that file exists.</summary>
        private const string VanillaIcon = "Images/UI/WorldCreation/IconSizeLarge";

        /// <summary>Vanilla's map render target grid, which is never gone below.</summary>
        private const int VanillaMapTargetX = 5;
        private const int VanillaMapTargetY = 2;

        /// <summary>checkMap gives the last column and the last row these sizes, and no others.</summary>
        private const int LastColumnWidth = 400;
        private const int LastRowHeight = 600;

        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        private static ILog log;
        private static ConstructorInfo tilemapCtor;

        public override void Load()
        {
            log = Mod.Logger;

            if (Width % SectionWidth != 0 || Height % SectionHeight != 0)
            {
                log.Error($"Extra Large world size: {Width} x {Height} is not a whole number of {SectionWidth} x {SectionHeight} sections. " +
                          "Every vanilla size is. Pick one that is, or the world will be drawn and sent in pieces that run off the end of it.");
            }

            On_UIWorldCreation.AddWorldSizeOptions += AddTheButton;
            On_UIWorldCreation.ShowOptionDescription += SayWhatTheyLack;
            On_UIWorldCreation.ClearOptionDescription += OneLineAgain;
            On_UIWorldCreation.SetDefaultOptions += StartOnIt;
            On_UIWorldCreation.FinishCreatingWorld += SetTheSize;
            On_UIWorldCreationPreview.UpdateOption += ShowTheLargePlate;
            On_WorldFileData.SetWorldSize += NameTheSize;
            On_WorldGen.clearWorld += MakeRoomFirst;
            IL_WorldGen.MakeDungeon += HoldDungeonToLarge;

            // Everything else whose *size* is a multiple of the world's width. Counts are
            // left alone on purpose and come out 1.5x by themselves.
            IL_WorldGen.makeTemple += HoldWidthTermToLarge;
            IL_JunglePass.ApplyPass += HoldWidthTermToLarge;
            IL_HiveBiome.CreateHiveTunnel += HoldWidthTermToLarge;

            // The general scaler behind most of vanilla's gen ranges. It is a pure
            // function of maxTilesX and maxTilesY, so the honest way to hold it back is
            // to hand it a Large world and put the real one back afterwards.
            On_WorldGenRange.ScaleValue += ScaleAsLarge;
        }

        public override void Unload()
        {
            log = null;
            tilemapCtor = null;
        }

        // ---------------------------------------------------------------- the button

        /// <summary>
        /// Vanilla builds three buttons and puts them in _sizeButtons. A fourth is
        /// built to match, the row is laid out again for four instead of three, and the
        /// array is replaced. ClickSizeOption reads whatever is in that array and casts
        /// it to the same closed generic type, so the new button clicks itself without
        /// any further help.
        /// </summary>
        private static void AddTheButton(On_UIWorldCreation.orig_AddWorldSizeOptions orig,
            UIWorldCreation self, UIElement container, float accumulatedHeight,
            UIElement.MouseEvent clickEvent, string tagGroup, float usableWidthPercent)
        {
            orig(self, container, accumulatedHeight, clickEvent, tagGroup, usableWidthPercent);

            FieldInfo slot = typeof(UIWorldCreation).GetField("_sizeButtons", Hidden);

            if (slot?.GetValue(self) is not Array vanilla || vanilla.Length != 3)
            {
                log?.Warn("Extra Large world size: the three vanilla size buttons were not where they should be. Leaving the menu alone.");
                return;
            }

            // GroupOptionButton<WorldSizeId>, where WorldSizeId is private and nested and
            // cannot be named here. The array's own element type hands it over.
            Type buttonType = slot.FieldType.GetElementType();
            Type sizeType = buttonType.GetGenericArguments()[0];

            PaleTheOthers(buttonType, vanilla);
             
            object mine = Activator.CreateInstance(buttonType,
                Enum.ToObject(sizeType, SizeOption),
                Language.GetText("Mods.PisAndDag.UI.WorldSizeExtraLarge"),
                Language.GetText("Mods.PisAndDag.UI.WorldDescriptionSizeExtraLarge"),
                Color.Lerp(new Color(255, 248, 48), Color.Gold, 0.5f),
                null,
                0.85f, 1f, 16f);

            GiveItAnIcon(buttonType, mine);

            Array all = Array.CreateInstance(buttonType, 4);
            vanilla.CopyTo(all, 0);
            all.SetValue(mine, 3);

            // Four across where vanilla had three: same maths, different n.
            for (int i = 0; i < 4; i++)
            {
                UIElement button = (UIElement)all.GetValue(i);

                button.Width = StyleDimension.FromPixelsAndPercent(-4f * 3f, usableWidthPercent / 4f);
                button.Left = StyleDimension.FromPercent(1f - usableWidthPercent);
                button.HAlign = i / 3f;
                button.Top.Set(accumulatedHeight, 0f);
            }

            UIElement added = (UIElement)mine;
             

            added.OnLeftMouseDown += clickEvent;
            added.OnMouseOver += self.ShowOptionDescription;
            added.OnMouseOut += self.ClearOptionDescription;
            added.SetSnapPoint(tagGroup, 3);

            container.Append(added);
            slot.SetValue(self, all);
        }

        // ------------------------------------------------------- the other three, pale

        /// <summary>
        /// What the three vanilla sizes are not, said under what they are: on a line of its
        /// own, pale (<see cref="PaleTag"/>), where the Grand's says "(Recommended)" in gold.
        /// </summary>
        private const string NotFullKey = "Mods.PisAndDag.UI.WorldSizeNotFull";

        /// <summary>
        /// The three vanilla buttons' names, washed this far toward this grey: pale beside the
        /// Grand's gold, and still their own colours (cyan, the cyan-green, the green) under it.
        /// </summary>
        private static readonly Color PaleGrey = new Color(200, 200, 200);
        private const float PaleBy = 0.7f;

        /// <summary>The line's colour, as a chat tag colour (the Grand's "(Recommended)" is fcbd1c).</summary>
        private const string PaleTag = "b4b4b4";

        /// <summary>
        /// vanilla's description panel (UIWorldCreation.AddDescriptionPanel): 40 tall; its text
        /// at 0.82, one line, not wrapped, the text element set 5 down in it and padded 6 more.
        /// </summary>
        private const float DescriptionHeight = 40f;
        private const float DescriptionScale = 0.82f;
        private const float DescriptionOffset = 5f;
        private const float DescriptionTop = 6f;

        /// <summary>Two lines instead: the element at the panel's top, padded this far, and this much kept clear of the foot.</summary>
        private const float TwoLinesTop = 2f;
        private const float TwoLinesFoot = 3f;

        private static FieldInfo descriptionText;

        /// <summary>The description is two lines at its own size right now, and wants putting back.</summary>
        private static bool twoLines;

        /// <summary>
        /// The names of the three vanilla sizes (the title on each button, cyan to green in
        /// vanilla) washed pale. The title is the button's own UIText (GroupOptionButton._title),
        /// coloured once when it is made and never again (DrawSelf does not touch it).
        /// </summary>
        private static void PaleTheOthers(Type buttonType, Array vanilla)
        {
            FieldInfo title = buttonType.GetField("_title", Hidden);

            if (title == null)
            {
                log?.Warn("Extra Large world size: the size buttons no longer keep their name in _title, so the vanilla sizes are not paled.");
                return;
            }

            foreach (object button in vanilla)
            {
                if (title.GetValue(button) is UIText name)
                    name.TextColor = Color.Lerp(name.TextColor, PaleGrey, PaleBy);
            }
        }

        /// <summary>
        /// Hovering a vanilla size: vanilla says what it is (UI.WorldDescriptionSize...), and under
        /// it, pale, what it is not. Vanilla's panel holds one line at 0.82; two are fitted into
        /// it by the font's own line height and the widest line, never over 0.82.
        /// </summary>
        private static void SayWhatTheyLack(On_UIWorldCreation.orig_ShowOptionDescription orig, UIWorldCreation self,
            UIMouseEvent evt, UIElement listeningElement)
        {
            UIText text = Description(self);

            OneLine(text);
            orig(self, evt, listeningElement);

            LocalizedText said = VanillaSize(listeningElement);

            if (text == null || said == null)
                return;

            string first = said.Value;
            string second = Language.GetTextValue(NotFullKey);

            // The panel's width, not the text's own: a line wider than its element widens it
            // (UIText's MinWidth), and the last one vanilla wrote may have been.
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float width = (text.Parent?.GetInnerDimensions().Width ?? 0f) - text.PaddingLeft - text.PaddingRight;
            float widest = Math.Max(font.MeasureString(first).X, font.MeasureString(second).X);

            float scale = Math.Min(DescriptionScale, (DescriptionHeight - TwoLinesTop - TwoLinesFoot) / (2f * font.LineSpacing));

            if (width > 0f && widest > 0f)
                scale = Math.Min(scale, width / widest);

            text.Top = StyleDimension.FromPixels(0f);
            text.PaddingTop = TwoLinesTop;
            text.SetText(first + "\n[c/" + PaleTag + ":" + second + "]", scale, false);
            text.Recalculate();
            twoLines = true;
        }

        /// <summary>Off a button: back to vanilla's one line before vanilla puts its default in.</summary>
        private static void OneLineAgain(On_UIWorldCreation.orig_ClearOptionDescription orig, UIWorldCreation self,
            UIMouseEvent evt, UIElement listeningElement)
        {
            OneLine(Description(self));
            orig(self, evt, listeningElement);
        }

        /// <summary>vanilla's size and place again, so the next line vanilla writes is drawn as vanilla draws it.</summary>
        private static void OneLine(UIText text)
        {
            if (text == null || !twoLines)
                return;

            twoLines = false;
            text.Top = StyleDimension.FromPixels(DescriptionOffset);
            text.PaddingTop = DescriptionTop;
            text.SetText(string.Empty, DescriptionScale, false);
            text.Recalculate();
        }

        private static UIText Description(UIWorldCreation self)
        {
            descriptionText ??= typeof(UIWorldCreation).GetField("_descriptionText", Hidden);
            return descriptionText?.GetValue(self) as UIText;
        }

        /// <summary>
        /// The description of a vanilla size button (Small, Medium, Large), or null for anything
        /// else, the Grand included: theirs are the descriptions under UI.WorldDescriptionSize.
        /// </summary>
        private static LocalizedText VanillaSize(UIElement element)
        {
            if (element == null)
                return null;

            Type type = element.GetType();

            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(GroupOptionButton<>))
                return null;

            LocalizedText said = type.GetField("Description")?.GetValue(element) as LocalizedText;

            return said != null && said.Key.StartsWith("UI.WorldDescriptionSize", StringComparison.Ordinal) ? said : null;
        }

        /// <summary>
        /// The button's icon, put in by hand.
        ///
        /// A GroupOptionButton loads its own icon with `Main.Assets.Request`, and
        /// Main.Assets is vanilla's asset repository: it resolves `Images/UI/...` and
        /// nothing else. A mod's texture comes out of `ModContent.Request` instead. Both
        /// hand back the same `ReLogic.Content.Asset&lt;Texture2D&gt;`, so the field takes
        /// it without complaint — it is only the constructor that cannot be told.
        ///
        /// ImmediateLoad on purpose. The button is built the moment the world creation
        /// menu opens and drawn on the next frame, and an asset still loading in the
        /// background has a null Value that DrawSelf would hand to SpriteBatch.
        ///
        /// The icon is drawn 1:1 at the panel's own corner, no source rectangle and no
        /// scale, inside a panel 44 by 34. Vanilla's are 42 by 32 and this one wants to
        /// be as well.
        /// </summary>
        private static void GiveItAnIcon(Type buttonType, object button)
        {
            FieldInfo slot = buttonType.GetField("_iconTexture", Hidden);

            if (slot == null)
            {
                log?.Warn("Extra Large world size: the size buttons no longer keep their icon in _iconTexture, so the Grand button will not have one.");
                return;
            }

            string mine = $"PisAndDag/{IconPath}";

            if (ModContent.HasAsset(mine))
            {
                slot.SetValue(button, ModContent.Request<Texture2D>(mine, AssetRequestMode.ImmediateLoad));
                return;
            }

            // Not drawn yet. Vanilla's Large icon rather than an empty plate.
            log?.Warn($"Extra Large world size: {IconPath}.png is not in the mod, so the Grand button is wearing vanilla's Large icon. It wants to be 42 by 32.");
            slot.SetValue(button, Main.Assets.Request<Texture2D>(VanillaIcon, AssetRequestMode.ImmediateLoad));
        }

        /// <summary>
        /// Vanilla opens the menu on Medium. It opens on this instead, which is what
        /// recommending something means.
        /// </summary>
        private static void StartOnIt(On_UIWorldCreation.orig_SetDefaultOptions orig, UIWorldCreation self)
        {
            orig(self);

            FieldInfo slot = typeof(UIWorldCreation).GetField("_sizeButtons", Hidden);
            FieldInfo chosen = typeof(UIWorldCreation).GetField("_optionSize", Hidden);

            if (slot?.GetValue(self) is not Array all || all.Length != 4 || chosen == null)
                return;

            Type buttonType = slot.FieldType.GetElementType();
            Type sizeType = buttonType.GetGenericArguments()[0];
            object value = Enum.ToObject(sizeType, SizeOption);

            MethodInfo select = buttonType.GetMethod("SetCurrentOption", new[] { sizeType });

            if (select == null)
                return;

            foreach (object button in all)
                select.Invoke(button, new[] { value });

            chosen.SetValue(self, value);

            typeof(UIWorldCreation).GetMethod("UpdatePreviewPlate", Hidden)?.Invoke(self, null);
        }

        /// <summary>
        /// Vanilla switches on the three it knows and leaves maxTilesX and maxTilesY
        /// untouched for anything else, then calls setWorldSize over the top. Setting
        /// them before it runs is all that is needed.
        /// </summary>
        private static void SetTheSize(On_UIWorldCreation.orig_FinishCreatingWorld orig, UIWorldCreation self)
        {
            object chosen = typeof(UIWorldCreation).GetField("_optionSize", Hidden)?.GetValue(self);

            if (chosen != null && Convert.ToInt32(chosen) == SizeOption)
            {
                Main.maxTilesX = Width;
                Main.maxTilesY = Height;
            }

            orig(self);
        }

        /// <summary>
        /// The preview plate draws small, medium or large and nothing at all for a
        /// fourth value. It gets shown the Large glyph, which is the nearest true thing.
        /// </summary>
        private static void ShowTheLargePlate(On_UIWorldCreationPreview.orig_UpdateOption orig,
            UIWorldCreationPreview self, byte difficulty, byte evil, byte size)
        {
            orig(self, difficulty, evil, size >= SizeOption ? (byte)2 : size);
        }

        /// <summary>
        /// The world select list calls any size it does not recognise Unknown. This one
        /// has a name.
        /// </summary>
        private static void NameTheSize(On_WorldFileData.orig_SetWorldSize orig,
            WorldFileData self, int x, int y)
        {
            orig(self, x, y);

            if (x == Width && y == Height)
                self._worldSizeName = Language.GetText("Mods.PisAndDag.UI.WorldSizeExtraLarge");
        }

        // ------------------------------------------------------------------- the room

        /// <summary>
        /// Every path into a world goes through here: generating one, loading one off
        /// disk after the header has been read, and a multiplayer client after the
        /// server has told it how big the world is. Nothing has been written yet at
        /// this point, which is the only moment the arrays can be swapped.
        /// </summary>
        private static void MakeRoomFirst(On_WorldGen.orig_clearWorld orig)
        {
            try
            {
                MakeRoom();
            }
            catch (Exception e)
            {
                log?.Error("Extra Large world size: could not grow the world arrays. " + e);
            }

            orig();
        }

        private static void MakeRoom()
        {
            // Vanilla allocates one more than the world is wide and tall, and indexes
            // up to and including maxTilesX.
            //
            // Rounded up to a whole number of sections on top of that. Every vanilla
            // world size already is one, so this changes nothing for them; it is here
            // because a world that is not gets walked off the end of by anything that
            // works a section at a time -- DrawToMap_Section reads 200 by 150 out of
            // Main.Map whether or not the world reaches that far -- and a few spare
            // rows of blank tiles are cheaper than finding that out again.
            int wantX = Math.Max(Main.maxTilesX, Sections(Main.maxTilesX, SectionWidth)) + 1;
            int wantY = Math.Max(Main.maxTilesY, Sections(Main.maxTilesY, SectionHeight)) + 1;

            GrowTiles(wantX, wantY);
            GrowMap(wantX, wantY);
            FitMapTargets();
        }

        /// <summary>How far the sections covering a world of this size actually reach.</summary>
        private static int Sections(int tiles, int section) => ((tiles - 1) / section + 1) * section;

        /// <summary>
        /// Main.tile is a struct holding a width, a height and the length of the global
        /// tile data. Its constructor is internal and there is no other way in, so it is
        /// called by reflection. The grid is only ever grown: the indexer works out
        /// x * Height + y, so a world loaded after this one reads the same layout it was
        /// written with.
        /// </summary>
        private static void GrowTiles(int wantX, int wantY)
        {
            if (Main.tile.Width >= wantX && Main.tile.Height >= wantY)
                return;

            int x = Math.Max(Main.tile.Width, wantX);
            int y = Math.Max(Main.tile.Height, wantY);

            tilemapCtor ??= typeof(Tilemap).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(ushort), typeof(ushort) }, null);

            if (tilemapCtor == null)
            {
                log?.Error("Extra Large world size: Tilemap(ushort, ushort) is not there. Worlds past Large will not load.");
                return;
            }

            Main.tile = (Tilemap)tilemapCtor.Invoke(new object[] { (ushort)x, (ushort)y });
            log?.Info($"Extra Large world size: tile grid grown to {x} x {y}.");
        }

        /// <summary>The map the player draws on the wall. A server has none.</summary>
        private static void GrowMap(int wantX, int wantY)
        {
            if (Main.dedServ)
                return;

            if (Main.Map != null && Main.Map.MaxWidth >= wantX && Main.Map.MaxHeight >= wantY)
                return;

            int x = Math.Max(Main.Map?.MaxWidth ?? 0, wantX);
            int y = Math.Max(Main.Map?.MaxHeight ?? 0, wantY);

            Main.Map = new WorldMap(x, y);
        }

        /// <summary>
        /// The full screen map is drawn out of a grid of render targets. DrawToMap walks
        /// columns 0 to maxTilesX / textureMaxWidth inclusive, and checkMap gives the
        /// last column of the grid 400 pixels and the last row 600, on the assumption
        /// that a world is at most 8400 by 2400. A wider world needs both a column the
        /// loop can reach and a column after it, so the one it reaches is not the last
        /// and gets its full size. Worked back, this returns vanilla's own 5 by 2 for
        /// all three vanilla sizes and 8 by 3 here.
        /// </summary>
        internal static Point MapGrid(int tilesX, int tilesY)
        {
            int columns = tilesX / Main.textureMaxWidth;
            int rows = tilesY / Main.textureMaxHeight;

            int spareX = tilesX - columns * Main.textureMaxWidth;
            int spareY = tilesY - rows * Main.textureMaxHeight;

            return new Point(
                Math.Max(VanillaMapTargetX, spareX <= LastColumnWidth ? columns + 1 : columns + 2),
                Math.Max(VanillaMapTargetY, spareY <= LastRowHeight ? rows + 1 : rows + 2));
        }

        /// <summary>
        /// The grid is made the size the world wants (<see cref="MapGrid"/>), here and now,
        /// before the world is cleared: empty, as the game starts with it, and the game fills
        /// it as it draws the map (Main.checkMap).
        ///
        /// A grid with the last world's map in it is only ever made bigger, never smaller.
        /// The targets in it serve a smaller world as they are: the game draws over the ones
        /// that world reaches and leaves the rest alone (Main.DrawToMap, DrawMap), as it does
        /// with its own five columns in a world that fills three; and they are there still
        /// for the next world as big. A door out of a Grand world and back so keeps its
        /// fourteen. When the grid does have to grow under targets, they are let go of
        /// (<see cref="LetGo"/>): the last column and the last row of a grid are made short
        /// (Main.checkMap), and would be short in the wrong place in a bigger one.
        /// </summary>
        private static void FitMapTargets()
        {
            if (Main.dedServ || Main.instance == null)
                return;

            Point grid = MapGrid(Main.maxTilesX, Main.maxTilesY);
            RenderTarget2D[,] old = Main.instance.mapTarget;

            if (Holds(old))
                grid = new Point(Math.Max(grid.X, Main.mapTargetX), Math.Max(grid.Y, Main.mapTargetY));

            if (grid.X == Main.mapTargetX && grid.Y == Main.mapTargetY)
                return;

            Main.instance.mapTarget = new RenderTarget2D[grid.X, grid.Y];
            Main.initMap = new bool[grid.X, grid.Y];
            Main.mapWasContentLost = new bool[grid.X, grid.Y];
            Main.mapTargetX = grid.X;
            Main.mapTargetY = grid.Y;

            LetGo(old);

            log?.Info($"Extra Large world size: map render grid set to {grid.X} x {grid.Y}.");
        }

        /// <summary>
        /// The render targets of a grid that had to grow are disposed of, by the main thread
        /// and no other. A world is cleared on whichever thread is loading or making it,
        /// which is seldom the main one, and tModLoader's FNA throws at a texture disposed
        /// of from any other (ThreadCheck, in FNA3D_AddDisposeTexture); nor does it free one
        /// that is simply dropped (GraphicsResource's finaliser is empty there). So off the
        /// main thread they are handed to it, and are gone by the end of its next update
        /// (Main.QueueMainThreadAction, run by Main.Update).
        /// </summary>
        private static void LetGo(RenderTarget2D[,] old)
        {
            if (!Holds(old))
                return;

            if (Program.IsMainThread)
                DisposeOf(old);
            else
                Main.QueueMainThreadAction(() => DisposeOf(old));
        }

        /// <summary>There is a render target in the grid: a map has been drawn into it.</summary>
        private static bool Holds(RenderTarget2D[,] grid)
        {
            if (grid == null)
                return false;

            foreach (RenderTarget2D target in grid)
            {
                if (target != null)
                    return true;
            }

            return false;
        }

        /// <summary>Each by itself: one that will not go does not keep the rest.</summary>
        private static void DisposeOf(RenderTarget2D[,] targets)
        {
            foreach (RenderTarget2D target in targets)
            {
                try
                {
                    target?.Dispose();
                }
                catch (Exception e)
                {
                    log?.Warn("Extra Large world size: a map render target of the last world's would not be disposed of. " + e);
                }
            }
        }

        // -------------------------------------------------------------- the structures

        /// <summary>What a structure that scales with the world is told the world is.</summary>
        private static int HoldToLarge(int width) => Math.Min(width, StructureWidth);

        /// <summary>
        /// Four terms in MakeDungeon size the dungeon off the world width: maxTilesX / 60,
        /// maxTilesX * 2, maxTilesX / 100 and maxTilesX / 20. Each is capped at what a
        /// Large world would have given. Every other read of maxTilesX in that method is
        /// a bounds check against the real edge of the world and is left alone.
        /// </summary>
        private static void HoldDungeonToLarge(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int held = 0;

            while (cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdsfld(typeof(Main), nameof(Main.maxTilesX))))
            {
                Instruction number = cursor.Next;
                Instruction operation = number?.Next;

                if (number == null || operation == null)
                    continue;

                bool sizing =
                    (number.MatchLdcI4(60) || number.MatchLdcI4(100) || number.MatchLdcI4(20))
                        && operation.OpCode == OpCodes.Div
                    || number.MatchLdcI4(2) && operation.OpCode == OpCodes.Mul;

                if (!sizing)
                    continue;

                cursor.EmitDelegate<Func<int, int>>(HoldToLarge);
                held++;
            }

            if (held != 4)
                log?.Warn($"Extra Large world size: expected to hold 4 dungeon size terms, held {held}. The dungeon may come out oversized.");
        }

        /// <summary>
        /// A Large world is 8400 by 2400. ScaleValue reads nothing else, so lending it
        /// those for the length of one call is exact rather than approximate.
        /// </summary>
        private static int ScaleAsLarge(On_WorldGenRange.orig_ScaleValue orig, WorldGenRange self, int value)
        {
            if (!Active)
                return orig(self, value);

            int wasX = Main.maxTilesX;
            int wasY = Main.maxTilesY;

            Main.maxTilesX = Math.Min(wasX, StructureWidth);
            Main.maxTilesY = Math.Min(wasY, LargeHeight);

            try
            {
                return orig(self, value);
            }
            finally
            {
                Main.maxTilesX = wasX;
                Main.maxTilesY = wasY;
            }
        }

        /// <summary>
        /// The one shape that means "a size, in worlds": maxTilesX as a double over
        /// 4200. makeTemple has one, the Jungle has one for its whole scale, and the
        /// Hive has one. Bounds checks never look like this, so they are left alone.
        /// </summary>
        private static void HoldWidthTermToLarge(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int held = 0;

            while (cursor.TryGotoNext(MoveType.After,
                i => i.MatchLdsfld(typeof(Main), nameof(Main.maxTilesX))))
            {
                Instruction convert = cursor.Next;
                Instruction number = convert?.Next;

                if (convert == null || number == null)
                    continue;

                if (convert.OpCode != OpCodes.Conv_R8)
                    continue;

                if (number.OpCode != OpCodes.Ldc_R8 || number.Operand is not double d || d != 4200.0)
                    continue;

                cursor.EmitDelegate<Func<int, int>>(HoldToLarge);
                held++;
            }

            if (held != 1)
                log?.Warn($"Extra Large world size: expected one width term in {il.Method?.Name}, held {held}. It may come out oversized.");
        }
    }
}
