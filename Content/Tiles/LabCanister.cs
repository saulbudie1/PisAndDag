using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Lab;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// The Abandoned Lab's specimen canisters, by request ("add a 5tall 3wide decorative walk thru tile that is basically
    /// js a canister with this thing inside; breaking it with pickaxe opens it"; "Release it, keep broken can"; "A few in
    /// the lab"; and a Culture let out counts toward the lab's two): three tiles wide and five tall, walked through. A steel
    /// cap and base, a glass tube between, the lab's green fluid in it, and in the fluid a Culture asleep, curled up, its
    /// strands drifting as it turns over now and then (LabCanisterSpecimen, four frames) and bobbing a little, bubbles
    /// going up past it; the glass's shine over it all (LabCanisterGlass). The fluid gives a little light.
    ///
    /// Broken with a pickaxe (the lab lets that much of it be broken: AbandonedLab.MayBreak), it is not taken away but
    /// opened: the glass bursts, the fluid and the glass go everywhere, and the Culture in it wakes and comes out at
    /// whoever is nearest; the canister stays, broken (its second style), and nothing more can be done to it. While two
    /// Cultures are already about, the glass only rings: nothing comes of it yet.
    ///
    /// Put down by the lab as it is laid out (LabPlan.Canisters), never by hand; no item, and nothing comes of it but the
    /// Culture.
    /// </summary>
    public class LabCanister : ModTile
    {
        public const int Wide = 3, Tall = 5;
        public const int Whole = 0, Broken = 1;

        private const int FrameStep = 18;
        private const int StyleWidth = Wide * FrameStep;

        /// <summary>Where the specimen hangs in it (from its top left, pixels), and the tube's fluid between these rows.</summary>
        private const int SpecimenX = 8, SpecimenY = 18;
        private const int SpecimenWide = 30, SpecimenHigh = 46, SpecimenFrames = 4;
        private const int FluidTop = 20, FluidBottom = 64, FluidLeft = 8, FluidRight = 38;

        /// <summary>The fluid's colour over what is seen through it, and its light.</summary>
        private static readonly Vector3 Through = new(0.78f, 0.94f, 0.86f);
        private static readonly Vector3 Glow = new(0.03f, 0.1f, 0.08f);
        private static readonly Color Bubble = new(150, 214, 186);

        private Asset<Texture2D> glass, specimen;

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileWaterDeath[Type] = false;
            Main.tileLighted[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
            TileObjectData.newTile.Width = Wide;
            TileObjectData.newTile.Height = Tall;
            TileObjectData.newTile.Origin = new Point16(1, Tall - 1);
            TileObjectData.newTile.CoordinateHeights = new[] { 16, 16, 16, 16, 16 };
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinatePadding = 2;
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, Wide, 0);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.addTile(Type);

            DustType = DustID.Glass;
            HitSound = SoundID.Tink;
            MineResist = 2f;

            AddMapEntry(new Color(52, 104, 88), CreateMapEntryName());

            if (!Main.dedServ)
            {
                glass = ModContent.Request<Texture2D>(Texture + "Glass");
                specimen = ModContent.Request<Texture2D>(Texture + "Specimen");
            }
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        /// <summary>Only a pick opens it.</summary>
        public override bool CanExplode(int i, int j) => false;

        // ---- where, and which --------------------------------------------------------------------------------------

        private static Point TopLeft(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            return new Point(i - tile.TileFrameX % StyleWidth / FrameStep, j - tile.TileFrameY / FrameStep);
        }

        /// <summary>A whole canister, a Culture still in it.</summary>
        public static bool IsWhole(int i, int j)
        {
            Tile tile = Main.tile[i, j];
            return tile.HasTile && tile.TileType == ModContent.TileType<LabCanister>() && tile.TileFrameX / StyleWidth == Whole;
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

        // ---- opened ------------------------------------------------------------------------------------------------

        /// <summary>
        /// Hit, it rings; broken, it is not taken away but opened (Open). Every hit on a broken one, or on a whole one
        /// while two Cultures are about, comes to nothing but the ring.
        /// </summary>
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            noItem = true;

            if (fail || effectOnly)
                return;

            fail = true;

            if (IsWhole(i, j))
                Open(i, j);
        }

        /// <summary>The glass bursts; the Culture in it comes out at whoever is nearest; the canister stays, broken.</summary>
        private static void Open(int i, int j)
        {
            Point top = TopLeft(i, j);
            Vector2 middle = new Vector2((top.X + Wide * 0.5f) * 16f, top.Y * 16f + 42f);
            int culture = ModContent.NPCType<Culture>();

            if (NPC.CountNPCS(culture) >= CulturePath.Most)
            {
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.4f, Volume = 0.8f }, middle);
                return;
            }

            for (int x = top.X; x < top.X + Wide; x++)
            {
                for (int y = top.Y; y < top.Y + Tall; y++)
                {
                    Tile tile = Main.tile[x, y];

                    if (tile.HasTile && tile.TileType == ModContent.TileType<LabCanister>() && tile.TileFrameX < StyleWidth)
                        tile.TileFrameX += StyleWidth;
                }
            }

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Shatter, middle);
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Pitch = 0.3f, Volume = 0.6f }, middle);

                Vector2 tube = new Vector2(top.X * 16f + FluidLeft, top.Y * 16f + FluidTop);

                for (int k = 0; k < 24; k++)
                    Dust.NewDust(tube, FluidRight - FluidLeft, FluidBottom - FluidTop, DustID.Glass, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 1f));

                for (int k = 0; k < 30; k++)
                {
                    Dust fluid = Dust.NewDustDirect(tube, FluidRight - FluidLeft, FluidBottom - FluidTop, DustID.t_Slime, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 1f), 120, new Color(70, 160, 120), 1.2f);
                    fluid.noGravity = false;
                }

                for (int k = 0; k < 12; k++)
                    Dust.NewDust(middle - new Vector2(10f), 20, 20, DustID.Blood, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 0f));
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int at = NPC.NewNPC(new EntitySource_TileBreak(i, j), (int)middle.X, (int)middle.Y + 16, culture);

            if (at < Main.maxNPCs)
            {
                NPC woke = Main.npc[at];
                int near = Player.FindClosest(woke.position, woke.width, woke.height);
                woke.velocity = (Main.player[near].Center - woke.Center).SafeNormalize(Vector2.UnitY) * 6f;
                woke.netUpdate = true;
            }
        }

        // ---- lit, and drawn --------------------------------------------------------------------------------------------

        /// <summary>A whole one's fluid gives a little light, about its tube.</summary>
        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            Tile tile = Main.tile[i, j];
            int row = tile.TileFrameY / FrameStep;

            if (tile.TileFrameX >= StyleWidth || row < 1 || row > 3)
                return;

            r = Glow.X;
            g = Glow.Y;
            b = Glow.Z;
        }

        /// <summary>From a whole one's top left tile: the specimen, its bubbles and the glass over them, drawn once every tile is.</summary>
        public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
        {
            Tile tile = Main.tile[i, j];

            if (tile.TileFrameX == 0 && tile.TileFrameY == 0)
                Main.instance.TilesRenderer.AddSpecialLegacyPoint(i, j);
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (glass == null || specimen == null || !glass.IsLoaded || !specimen.IsLoaded)
                return;

            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 corner = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offScreen;
            uint h = Hash(i, j);
            uint time = Main.GameUpdateCount + h % 1000u;

            Color light = Lighting.GetColor(i + 1, j + 2);
            Color seen = new Color(light.ToVector3() * Through);

            // It turns over in its sleep now and then; bobs a little, on the two-pixel grid.
            int frame = (int)(time / 24u % SpecimenFrames);
            int bob = MathF.Sin(time * 0.035f) > 0f ? 2 : 0;
            Rectangle source = new Rectangle(0, frame * SpecimenHigh, SpecimenWide, SpecimenHigh);

            spriteBatch.Draw(specimen.Value, corner + new Vector2(SpecimenX, SpecimenY + bob), source, seen);

            // Three bubbles up through the fluid, each in its own time.
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color bubble = Bubble.MultiplyRGB(light);

            for (int k = 0; k < 3; k++)
            {
                uint mixed = Hash((int)h + k * 97, k);
                uint period = 110u + mixed % 90u;
                float t = (time + mixed % 400u) % period / (float)period;
                int x = FluidLeft + (int)(mixed % 15u) * 2;
                int y = (int)((FluidBottom - (FluidBottom - FluidTop) * t) / 2f) * 2;

                if (y > FluidTop && y < FluidBottom)
                    spriteBatch.Draw(pixel, new Rectangle((int)corner.X + x, (int)corner.Y + y, 2, 2), bubble);
            }

            spriteBatch.Draw(glass.Value, corner, light);
        }
    }
}
