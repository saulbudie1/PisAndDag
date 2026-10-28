using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// Harenaeum Ore, the tile (Items.Misc.Harenaeum): veins of it in the underground desert's hardened sand and
    /// sandstone (HarenaeumPass, every new world; by request, none put into worlds made before it). By request, a
    /// pickaxe of 65 power to mine it, the Nightmare's or the Deathbringer's, as Hellstone wants.
    ///
    /// An ore as the game's are: found by the Spelunker and the metal detector (ranked over Demonite, Crimtane
    /// and Meteorite, under Hellstone), it glitters now and then where there is light on it (the game's own shine,
    /// as its gold has) and is brighter for the light, and it rings as metal when struck, throwing gold dust.
    ///
    /// The picture is a placeholder (PlaceholderTextures.md): the game's standard sheet of a block's shapes.
    /// </summary>
    public class HarenaeumOre : ModTile
    {
        public override void SetStaticDefaults()
        {
            TileID.Sets.Ore[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 610;
            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 900;
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            // It sits in sand and sandstone, and shows as one with them at its edges.
            Main.tileMerge[Type][TileID.Sandstone] = true;
            Main.tileMerge[Type][TileID.HardenedSand] = true;
            Main.tileMerge[TileID.Sandstone][Type] = true;
            Main.tileMerge[TileID.HardenedSand][Type] = true;

            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(224, 170, 70), name);

            DustType = DustID.GoldCoin;
            HitSound = SoundID.Tink;
            MineResist = 2.5f;
            MinPick = 65;
        }

        /// <summary>Struck, it throws sand and gold, and now and then a fleck of its turquoise, lit.</summary>
        public override bool CreateDust(int i, int j, ref int type)
        {
            if (Main.rand.NextBool(5))
            {
                Dust fleck = Dust.NewDustDirect(new Vector2(i, j) * 16f, 16, 16, DustID.TintableDustLighted, 0f, 0f, 120,
                    Items.Misc.Harenaeum.Turquoise, 0.8f);
                fleck.noGravity = true;
                fleck.velocity *= 0.6f;
                return false;
            }

            type = Main.rand.NextBool(3) ? DustID.Sand : DustID.GoldCoin;
            return true;
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 2 : 6;
    }
}
