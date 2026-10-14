using System;
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

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Metallurgist's Brilliant Stone, by request: "In a 38(wide)x39 rectangle, every ore that can be
    /// turned on by spelunker, will be draw with no light level. Sold by Merchant for 30 gold." Then:
    /// "change the range of Metallurgist's Brilliant Stone to 58 wide and 45 high".
    ///
    /// Worn, every block of ore in a field 58 tiles across and 45 down about its wearer is drawn as
    /// it looks in full light, however dark it is there, and now and then one glints. The field: the
    /// tile the middle of the wearer is in, 29 tiles to its left and 28 to its right, 22 above and 22
    /// below. Ore: what is in the game's own list of it (TileID.Sets.Ore) and lit by the Spelunker
    /// Potion (Main.IsTileSpelunkable): copper, tin, iron, lead, silver, tungsten, gold, platinum,
    /// meteorite, cobalt, palladium, mythril, orichalcum, adamantite, titanium and chlorophyte, and
    /// any other mod's ore the potion lights. Not demonite, crimtane or hellstone, which the potion
    /// does not light either (Main.tileSpelunker); not the gems, chests, pots and the rest it does
    /// light, which are not ore. The wearer alone sees it.
    ///
    /// How it is drawn is <see cref="MetallurgistsSight"/>. Sold by the Merchant, for 30 gold before
    /// his mood moves it as it moves all his prices (<see cref="MetallurgistsBrilliantStoneShop"/>).
    /// Mine: Green rarity; its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class MetallurgistsBrilliantStone : ModItem
    {
        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(gold: 30);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<MetallurgistsStonePlayer>().Worn = true;
        }

        // Recipes stay commented out for now; none was asked for. The Merchant sells it (below).
    }

    /// <summary>The Merchant sells it, for what it is worth (Item.value: 30 gold), as a shop prices anything.</summary>
    public class MetallurgistsBrilliantStoneShop : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Merchant)
                shop.Add(ModContent.ItemType<MetallurgistsBrilliantStone>());
        }
    }

    /// <summary>
    /// Whether the stone is worn, held across the tick for the drawing (which comes after the
    /// update), and the glints: on the wearer's own machine, now and then a spark of the Spelunker
    /// Potion's (its TreasureSparkle) over one of the ores in the field, fewer than the potion makes.
    /// </summary>
    public class MetallurgistsStonePlayer : ModPlayer
    {
        /// <summary>One in this many ores glints each tick.</summary>
        private const int GlintChance = 300;

        /// <summary>Worn this tick (MetallurgistsBrilliantStone.UpdateAccessory).</summary>
        public bool Worn;

        /// <summary>Worn when everything worn last had its say (PostUpdateEquips).</summary>
        public bool Sees { get; private set; }

        public override void ResetEffects()
        {
            Worn = false;
        }

        public override void PostUpdateEquips()
        {
            Sees = Worn;
        }

        public override void PostUpdate()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer || !Sees || Player.dead)
                return;

            Rectangle field = MetallurgistsSight.Field(Player);

            for (int y = field.Top; y < field.Bottom; y++)
            {
                for (int x = field.Left; x < field.Right; x++)
                {
                    if (!Main.rand.NextBool(GlintChance) || !MetallurgistsSight.Shows(x, y))
                        continue;

                    Dust glint = Dust.NewDustDirect(new Vector2(x * 16f, y * 16f), 16, 16, DustID.TreasureSparkle, 0f, 0f, 150, default, 0.8f);
                    glint.velocity *= 0.1f;
                    glint.noGravity = true;
                }
            }
        }
    }

    /// <summary>
    /// The ore, drawn as it looks in full light, for the player wearing the stone.
    ///
    /// The game will not draw a tile it has no light on at all (TileDrawing.DrawSingleTile decides
    /// that before any mod's hook is asked), so the ore is drawn a second time, over the game's own:
    /// once a frame, right after the game's solid tiles and before what stands among them (the
    /// mannequins and the like, the cracks of a block being mined, then the NPCs, the players and
    /// everything else), so all of that is still drawn over it. Main.DoDraw_Tiles_Solid has no hook
    /// there, so an IL edit calls <see cref="DrawOres"/> right after it ends the batch its tiles were
    /// drawn in, before it draws what stands among them (Main.DrawTileEntities).
    ///
    /// Each tile goes through the game's own TileDrawing, as LegendsPlate draws them: GetTileDrawData
    /// for its frame, and the private DrawBasicTile for the shapes of slopes and half blocks, at full
    /// light; a whole, level one is its frame drawn once. Painted ore keeps its paint; ore under the
    /// Echo Coating stays unseen unless the game shows it; ore turned off by an actuator is not
    /// drawn. One small difference from the game: a whole block of ore beside a half block, which the
    /// game draws in two parts so the two meet, is drawn whole. If the game's TileDrawing no longer
    /// has DrawBasicTile, or Main.DoDraw_Tiles_Solid no longer calls Main.DrawTileEntities, the
    /// stone shows nothing and says so in the log; if drawing ever throws, it stops until the mod
    /// reloads and says so once.
    /// </summary>
    public class MetallurgistsSight : ModSystem
    {
        /// <summary>The field, in tiles, from the tile the wearer's middle is in.</summary>
        public const int Left = 29;
        public const int Right = 28;
        public const int Up = 22;
        public const int Down = 22;

        private delegate void BasicTileDrawer(TileDrawing self, Vector2 screenPosition, Vector2 screenOffset,
            int tileX, int tileY, TileDrawInfo drawData, Rectangle normalTileRect, Vector2 normalTilePosition);

        private static BasicTileDrawer drawBasicTile;
        private static bool broken;
        private static readonly TileDrawInfo info = new TileDrawInfo();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            MethodInfo basic = typeof(TileDrawing).GetMethod("DrawBasicTile", BindingFlags.Instance | BindingFlags.NonPublic);

            if (basic == null)
            {
                Mod.Logger.Warn("Metallurgist's Brilliant Stone shows nothing: TileDrawing no longer has DrawBasicTile.");
                return;
            }

            drawBasicTile = (BasicTileDrawer)Delegate.CreateDelegate(typeof(BasicTileDrawer), basic);
            IL_Main.DoDraw_Tiles_Solid += CallDrawOres;
        }

        public override void Unload()
        {
            drawBasicTile = null;
            broken = false;
        }

        /// <summary>Puts a call to <see cref="DrawOres"/> after the solid tiles' batch ends and before Main.DrawTileEntities.</summary>
        private void CallDrawOres(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall(typeof(Main), "DrawTileEntities"))
                || !cursor.TryGotoPrev(MoveType.After, i => i.MatchCallvirt(typeof(SpriteBatch), "End")))
            {
                Mod.Logger.Warn("Metallurgist's Brilliant Stone shows nothing: Main.DoDraw_Tiles_Solid no longer ends a batch before Main.DrawTileEntities.");
                return;
            }

            cursor.MoveAfterLabels();
            cursor.EmitDelegate<Action>(DrawOres);
        }

        /// <summary>The field about a player, in tiles, kept inside the world (and a tile in from its edges).</summary>
        public static Rectangle Field(Player player)
        {
            Point middle = player.Center.ToTileCoordinates();
            int left = Math.Max(1, middle.X - Left);
            int top = Math.Max(1, middle.Y - Up);
            int right = Math.Min(Main.maxTilesX - 2, middle.X + Right);
            int bottom = Math.Min(Main.maxTilesY - 2, middle.Y + Down);

            return new Rectangle(left, top, Math.Max(0, right - left + 1), Math.Max(0, bottom - top + 1));
        }

        /// <summary>Whether the stone shows this tile: ore, there for the Spelunker Potion, not turned off, not hidden.</summary>
        public static bool Shows(int x, int y)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile || tile.IsActuated)
                return false;

            ushort type = tile.TileType;

            return type < TileID.Sets.Ore.Length && TileID.Sets.Ore[type] && Main.IsTileSpelunkable(x, y) && TileDrawing.IsVisible(tile);
        }

        private static void DrawOres()
        {
            if (broken || drawBasicTile == null || Main.gameMenu)
                return;

            Player player = Main.LocalPlayer;

            if (!player.active || player.dead || !player.GetModPlayer<MetallurgistsStonePlayer>().Sees)
                return;

            SpriteBatch batch = Main.spriteBatch;
            bool begun = false;

            try
            {
                TileDrawing renderer = Main.instance.TilesRenderer;
                Vector2 screen = Main.screenPosition;
                Rectangle field = Field(player);

                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                begun = true;

                for (int y = field.Top; y < field.Bottom; y++)
                {
                    for (int x = field.Left; x < field.Right; x++)
                    {
                        if (Shows(x, y))
                            DrawTile(batch, renderer, Main.tile[x, y], x, y, screen);
                    }
                }

                batch.End();
                begun = false;
            }
            catch (Exception e)
            {
                broken = true;
                ModContent.GetInstance<MetallurgistsSight>().Mod.Logger.Error("Metallurgist's Brilliant Stone failed to draw and is off until the mod reloads.", e);

                if (begun)
                {
                    try { batch.End(); }
                    catch { }
                }
            }
        }

        /// <summary>One tile as TileDrawing.DrawSingleTile would draw it at full light (LegendsPlate.DrawTile, less its trees and cacti).</summary>
        private static void DrawTile(SpriteBatch batch, TileDrawing renderer, Tile tile, int x, int y, Vector2 screen)
        {
            ushort type = tile.TileType;

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

            if (info.drawTexture == null)
                return;

            Rectangle frame = new Rectangle(info.tileFrameX + info.addFrX, info.tileFrameY + info.addFrY,
                info.tileWidth, info.tileHeight - info.halfBrickHeight);

            Vector2 at = new Vector2(x * 16 - (int)screen.X - (info.tileWidth - 16f) / 2f,
                y * 16 - (int)screen.Y + info.tileTop + info.halfBrickHeight);

            if (tile.Slope == SlopeType.Solid && !tile.IsHalfBlock)
                batch.Draw(info.drawTexture, at, frame, Color.White, 0f, Vector2.Zero, 1f, info.tileSpriteEffect, 0f);
            else
                drawBasicTile(renderer, screen, Vector2.Zero, x, y, info, frame, at);
        }
    }
}
