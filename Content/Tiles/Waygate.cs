using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// A waygate, by request ("Create things like this (basically a fancy door for subworld)
    /// (with the effects, like the rays coming out) (right clicking on it give you two
    /// choices: Traverse the mysterious gateway? "Yes" "No")", the Elden Ring wiki's
    /// Waygates page; and then "gate is 2x2 tiles").
    ///
    /// Two tiles by two, standing on solid ground, walked through like any furniture. What
    /// is seen of it is mostly light: a ball of it over a low socket of carved stone (the
    /// texture), with rays turning out of it and motes drawn into it (WaygateRadiance draws
    /// those, every frame, over the tiles). Right clicked, it asks its question
    /// (WaygatePrompt); answered yes, it takes whoever asked to the place it leads to
    /// (<see cref="LeadsTo"/>), and the one standing there brings them back to it
    /// (SubworldPassage.ThroughWaygate).
    ///
    /// It cannot be broken, anywhere, by anything, as the Unmarked Door cannot: its own
    /// answers below, the game told the same where it decides such things
    /// (SubworldRules.NothingBreaks), and the ground under it held by the game's own sets.
    /// It is never framed by the game (<see cref="TileFrame"/>), so nothing done around it
    /// can take it apart either. There is no item for it: the world puts it down
    /// (BeachWaygate, VastOcean), and only /subworld ungate takes one up.
    ///
    /// The texture is a placeholder of mine (PlaceholderTextures.md): a 36x36 sheet, two
    /// frames each way, 16 pixels and 2 of padding; only the socket, in the lowest rows, is
    /// drawn in it.
    /// </summary>
    public class Waygate : ModTile
    {
        public const int Wide = 2;
        public const int Tall = 2;

        /// <summary>A frame of the sheet: 16 pixels and 2 of padding.</summary>
        private const int FrameStep = 18;

        /// <summary>Where every one of these leads, for now: the Vast Ocean, by request ("For now create one ... that basically takes you to a subworld where it is just the ocean").</summary>
        public static Subworld LeadsTo => ModContent.GetInstance<VastOcean>();

        /// <summary>What is heard as one is gone into, and on the other side as the black lifts: the game's own teleport, a little lower and softer the second time.</summary>
        public static SoundStyle Going => SoundID.Item6 with { Volume = 0.8f, Pitch = -0.25f };
        public static SoundStyle Coming => SoundID.Item6 with { Volume = 0.55f, Pitch = -0.45f };

        /// <summary>Its light, warm and white (ModifyLight): about a torch's, from each of its four tiles.</summary>
        public static readonly Vector3 Light = new Vector3(1.05f, 0.92f, 0.70f);

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;
            TileID.Sets.PreventsTileRemovalIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsTileReplaceIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsTileHammeringIfOnTopOfIt[Type] = true;
            TileID.Sets.PreventsSandfall[Type] = true;
            TileID.Sets.AvoidedByMeteorLanding[Type] = true;

            // Two by two on solid ground, as the Unmarked Door is two by three: what it stands
            // on is kept by the sets above, and a platform could be taken from under it.
            TileObjectData.newTile.CopyFrom(TileObjectData.Style2xX);
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(0, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16 };
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Wide, 0);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            DustType = DustID.GoldFlame;
            HitSound = SoundID.Tink;

            AddMapEntry(new Color(255, 230, 170), CreateMapEntryName());
        }

        // ---- never broken, never framed ----------------------------------------------------------

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            fail = true;
            noItem = true;
        }

        public override bool CanExplode(int i, int j) => false;

        public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;

        public override bool Slope(int i, int j) => false;

        /// <summary>
        /// The game frames a tile whenever anything beside it changes, and a thing of several
        /// tiles whose ground has gone is taken apart there. This one keeps the frames it was
        /// put down with, and is never taken apart.
        /// </summary>
        public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
        {
            noBreak = true;
            return false;
        }

        /// <summary>Nothing, whatever makes the game think one has come down: none ever has.</summary>
        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            float breath = WaygateRadiance.Breath;

            r = Light.X * breath;
            g = Light.Y * breath;
            b = Light.Z * breath;
        }

        // ---- asked ---------------------------------------------------------------------------------

        public override bool RightClick(int i, int j)
        {
            TopLeft(i, j, out int left, out int top);

            WaygatePrompt.Open(left, top);
            return true;
        }

        public override void MouseOver(int i, int j)
        {
            // Over it, nothing in hand is used: the click is the gate's.
            Main.LocalPlayer.noThrow = 2;

            TopLeft(i, j, out int left, out int top);
            WaygateRadiance.Hovered = new Point16(left, top);
        }

        // ---- where one stands ------------------------------------------------------------------

        /// <summary>The top left tile of the waygate this tile is part of.</summary>
        public static void TopLeft(int i, int j, out int left, out int top)
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

            return tile.HasTile && tile.TileType == ModContent.TileType<Waygate>();
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
        /// Puts one down with its top left tile here, over whatever is there, the way a place
        /// lays out its tiles: its four tiles written straight in, each its frame of the sheet.
        /// The ground under it is the caller's to have made. The tile type is given, so that
        /// this can be run where the mod's types are not known (the preview).
        /// </summary>
        public static void Put(int left, int top, ushort type)
        {
            for (int x = 0; x < Wide; x++)
            {
                for (int y = 0; y < Tall; y++)
                {
                    if (!WorldGen.InWorld(left + x, top + y))
                        continue;

                    Tile tile = Main.tile[left + x, top + y];

                    tile.ClearTile();
                    tile.HasTile = true;
                    tile.TileType = type;
                    tile.TileFrameX = (short)(x * FrameStep);
                    tile.TileFrameY = (short)(y * FrameStep);
                    tile.LiquidAmount = 0;
                }
            }
        }

        /// <summary>
        /// Takes up the waygate a tile of which is here, by hand (/subworld ungate): its
        /// tiles are cleared outright, since nothing that asks the game to break one is
        /// listened to. False if there is none here.
        /// </summary>
        internal static bool TakeUp(int x, int y)
        {
            if (!IsAt(x, y))
                return false;

            TopLeft(x, y, out int left, out int top);

            int type = ModContent.TileType<Waygate>();

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
