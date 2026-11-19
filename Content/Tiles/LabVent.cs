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
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// The vent on the Wasted Mile, by request ("Lets make a vent tile in the miles; sortalike chimney walkthruable; right
    /// click to go onto another dimension but locked after armagaermis", "sub world is : Abandoned Lab"; two wide and four
    /// tall, in the middle of the Mile, a line in chat while it is shut; then "Make the vent only 1 tile wide"): a stack of
    /// riveted iron coming up out of the ash, one tile wide,
    /// a cowl of louvres on top and a grated hatch low on its front, walked past like furniture. Right clicked, it takes
    /// whoever asked down to the Abandoned Lab (AbandonedLab), once Amalgaermis is dead (PADWorld.DownedAbomanation, the
    /// world's: in a world where it is not, it stays shut and says so). The one standing in the lab brings them back up
    /// to it (SubworldPassage.ThroughVent).
    ///
    /// Open, it breathes: warm air through its louvres and its hatch, lit from inside (the Glow sheet), and steam off its
    /// cowl now and then. Shut, it is cold iron.
    ///
    /// Never broken, never framed, as the waygate is (Waygate): the world puts it down (MileVent), and the lab stands one
    /// by its way in. There is no item for it.
    /// </summary>
    public class LabVent : ModTile
    {
        public const int Wide = 1;
        public const int Tall = 4;

        private const int FrameStep = 18;

        /// <summary>Heard as it is gone into: its grate unlatched; and on the other side as the black lifts: an iron door, low.</summary>
        public static SoundStyle Going => SoundID.Unlock with { Volume = 0.8f, Pitch = -0.3f };
        public static SoundStyle Coming => SoundID.DoorClosed with { Volume = 0.7f, Pitch = -0.5f };

        /// <summary>The warm light of the air it breathes, open, from each of its tiles.</summary>
        private static readonly Vector3 Breath = new Vector3(0.34f, 0.16f, 0.06f);

        private Asset<Texture2D> glow;

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

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1xX);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(0, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 18 };
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Wide, 0);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            DustType = DustID.Iron;
            HitSound = SoundID.Tink;

            AddMapEntry(new Color(112, 100, 92), CreateMapEntryName());

            if (!Main.dedServ)
                glow = ModContent.Request<Texture2D>(Texture + "Glow");
        }

        /// <summary>Open: Amalgaermis is dead in this world; or this is the lab, where it is always the way back.</summary>
        public static bool Open => Subworld.Current is AbandonedLab || PADWorld.DownedAbomanation;

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

        public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
        {
            noBreak = true;
            return false;
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        // ---- its breath ----------------------------------------------------------------------------

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            if (!Open)
                return;

            float pulse = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 1.3f + i);

            r = Breath.X * pulse;
            g = Breath.Y * pulse;
            b = Breath.Z * pulse;
        }

        /// <summary>Steam off its cowl now and then, open: from the top left tile only, so once a vent.</summary>
        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (Main.dedServ || !Open || Main.gamePaused)
                return;

            Tile tile = Main.tile[i, j];

            if (tile.TileFrameX != 0 || tile.TileFrameY != 0 || !Main.rand.NextBool(14))
                return;

            Dust puff = Dust.NewDustDirect(new Vector2(i * 16 + 2, j * 16 - 2), 12, 6, DustID.Smoke, 0f, -1f, 160, new Color(210, 200, 190), 1.0f);
            puff.noGravity = true;
            puff.velocity = new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.1f, -0.5f));
        }

        /// <summary>Open, the warm inside of it showing through its louvres and its hatch.</summary>
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (glow == null || !glow.IsLoaded || !Open)
                return;

            Tile tile = Main.tile[i, j];
            int row = tile.TileFrameY / FrameStep;
            int height = row == Tall - 1 ? 18 : 16;
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 at = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offScreen;
            float pulse = 0.8f + 0.2f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 1.3f + i);

            spriteBatch.Draw(glow.Value, at, new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, height), Color.White * pulse);
        }

        // ---- asked ---------------------------------------------------------------------------------

        public override bool RightClick(int i, int j)
        {
            TopLeft(i, j, out int left, out int top);

            if (!Open)
            {
                Main.NewText(Language.GetTextValue("Mods.PisAndDag.Subworlds.VentShut"), 170, 160, 150);
                return true;
            }

            SubworldPassage.ThroughVent(left, top);
            return true;
        }

        public override void MouseOver(int i, int j)
        {
            Main.LocalPlayer.noThrow = 2;
        }

        // ---- where one stands ------------------------------------------------------------------

        /// <summary>The top left tile of the vent this tile is part of.</summary>
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

            return tile.HasTile && tile.TileType == ModContent.TileType<LabVent>();
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
        /// A vent put down two wide before it was one (r82v): the column right of the one standing here taken away, if it is
        /// what is left of it (a tile of this kind framed as a second column). True if anything was.
        /// </summary>
        public static bool TidyOldWidth(int left, int top)
        {
            bool any = false;
            int type = ModContent.TileType<LabVent>();

            for (int y = top; y < top + Tall; y++)
            {
                if (!WorldGen.InWorld(left + 1, y))
                    continue;

                Tile tile = Main.tile[left + 1, y];

                if (tile.HasTile && tile.TileType == type && tile.TileFrameX >= FrameStep)
                {
                    tile.ClearTile();
                    any = true;
                }
            }

            if (any && Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, left + 1, top, 1, Tall);

            return any;
        }

        /// <summary>
        /// Puts one down with its top left tile here, over whatever is there, as a place lays out its tiles: its eight tiles
        /// written straight in, each its frame of the sheet, with no water in them. The ground under it is the caller's.
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
    }
}
