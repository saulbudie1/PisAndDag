using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// A door that stands by itself: two tiles wide, three tall, walked past like any
    /// furniture. Right clicked in the world, it takes whoever tried it to the place it
    /// leads to (<see cref="LeadsTo"/>); the one standing there takes them back to the door
    /// they came through (SubworldPassage).
    ///
    /// It cannot be broken, anywhere, by anything: its own answers below, and the game is
    /// told the same where it decides such things at all (SubworldRules.NothingBreaks),
    /// which also keeps a pick from so much as chipping at it. What it stands on cannot be
    /// dug, hammered or swapped out from under it (the game's own sets for that). So where
    /// it is put down is where it stays; /subworld undoor takes one up again.
    ///
    /// Placed by the item of the same name (Content/Items/Misc/SubworldDoor). The texture is
    /// a placeholder (PlaceholderTextures.md): a 36x54 sheet, two frames across and three
    /// down, 16 pixels each with 2 of padding.
    /// </summary>
    public class SubworldDoor : ModTile
    {
        public const int Wide = 2;
        public const int Tall = 3;

        /// <summary>A frame of the sheet: 16 pixels and 2 of padding.</summary>
        private const int FrameStep = 18;

        /// <summary>Where every one of these leads, for now: there is one place.</summary>
        public static Subworld LeadsTo => ModContent.GetInstance<EmptyHall>();

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;

            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsTileReplaceIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsTileHammeringIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsSandfall[Type] = true;
            TileID.Sets.AvoidedByMeteorLanding[Type] = true;

            // Vanilla's two-wide tall thing (a statue: 2 by 3, placed by its bottom right
            // tile), standing on solid ground only: a platform could be taken from under it.
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16 };
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Wide, 0);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            DustType = DustID.WoodFurniture;
            HitSound = SoundID.Dig;

            AddMapEntry(new Color(119, 86, 62), CreateMapEntryName());
        }

        // ---- never broken ----------------------------------------------------------------------

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            fail = true;
            noItem = true;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;

        /// <summary>Nothing, whatever makes the game think one has come down: none ever has.</summary>
        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        // ---- tried ---------------------------------------------------------------------------------

        public override bool RightClick(int i, int j)
        {
            TopLeft(i, j, out int left, out int top);

            SubworldPassage.Through(left, top);
            return true;
        }

        public override void MouseOver(int i, int j)
        {
            Player player = Main.LocalPlayer;

            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = ModContent.ItemType<Items.Misc.SubworldDoor>();
        }

        // ---- where one stands ------------------------------------------------------------------

        /// <summary>The top left tile of the door this tile is part of.</summary>
        private static void TopLeft(int i, int j, out int left, out int top)
        {
            Tile tile = Main.tile[i, j];

            left = i - tile.TileFrameX / FrameStep % Wide;
            top = j - tile.TileFrameY / FrameStep % Tall;
        }

        /// <summary>A tile of one of these is here.</summary>
        public static bool IsAt(int x, int y)
        {
            if (!WorldGen.InWorld(x, y))
                return false;

            Tile tile = Main.tile[x, y];

            return tile.HasTile && tile.TileType == ModContent.TileType<SubworldDoor>();
        }

        /// <summary>One of these stands with its top left tile here.</summary>
        public static bool StandsAt(int left, int top)
        {
            if (!IsAt(left, top))
                return false;

            Tile tile = Main.tile[left, top];

            return tile.TileFrameX == 0 && tile.TileFrameY == 0;
        }

        /// <summary>
        /// Takes up the door a tile of which is here, by hand (/subworld undoor): its tiles
        /// are cleared outright, since nothing that asks the game to break one is listened
        /// to. False if there is none here.
        /// </summary>
        internal static bool TakeUp(int x, int y)
        {
            if (!IsAt(x, y))
                return false;

            TopLeft(x, y, out int left, out int top);

            int type = ModContent.TileType<SubworldDoor>();

            for (int i = left; i < left + Wide; i++)
            {
                for (int j = top; j < top + Tall; j++)
                {
                    if (!WorldGen.InWorld(i, j))
                        continue;

                    Tile tile = Main.tile[i, j];

                    if (tile.HasTile && tile.TileType == type)
                        tile.ClearTile();
                }
            }

            if (Main.netMode != NetmodeID.SinglePlayer)
                NetMessage.SendTileSquare(-1, left, top, Wide, Tall);

            return true;
        }
    }
}
