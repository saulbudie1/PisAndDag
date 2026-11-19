using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Walls.Reef
{
    /// <summary>
    /// The Vast Ocean's reef walls (VastOceanReef grows them; ReefPlan.Wall, in that order): reef rock behind the reefs,
    /// staghorn and the lace of sea fans in the strands that join them and hanging from them, brain coral near their
    /// tops. The staghorn and the sea fans are see-through between their branches. Each gives itself when broken (the
    /// item that places it: Items.Reef) and goes up by hand; a house may be walled with them.
    ///
    /// As the game has its own: what the place grows is the wild wall (<c>…Natural</c>), which creatures may come out in
    /// front of as anywhere in the sea, and which gives the same item; what is put up by hand is a house's, which they may
    /// not. The two look the same.
    /// </summary>
    public abstract class ReefWall : ModWall
    {
        private const string Wild = "Natural";

        protected abstract Color MapColour { get; }

        protected virtual int Dust => DustID.Coralstone;

        /// <summary>Whether this is the wild one the place grows.</summary>
        private bool Natural => Name.EndsWith(Wild);

        /// <summary>The wild one is drawn with the house one's picture.</summary>
        public override string Texture => Natural ? base.Texture[..^Wild.Length] : base.Texture;

        public override void SetStaticDefaults()
        {
            Main.wallHouse[Type] = !Natural;
            DustType = Dust;
            AddMapEntry(MapColour);

            if (Natural)
                RegisterItemDrop(Mod.Find<ModItem>(Name[..^Wild.Length]).Type);
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 2;
    }

    public class ReefRockWall : ReefWall
    {
        protected override Color MapColour => new Color(78, 76, 76);
        protected override int Dust => DustID.Stone;
    }

    public class StaghornCoralWall : ReefWall
    {
        protected override Color MapColour => new Color(150, 98, 70);
    }

    public class SeaFanWall : ReefWall
    {
        protected override Color MapColour => new Color(118, 70, 136);
    }

    public class BrainCoralWall : ReefWall
    {
        protected override Color MapColour => new Color(100, 96, 62);
    }

    // The wild ones the place grows (ReefWall).
    public class ReefRockWallNatural : ReefRockWall { }

    public class StaghornCoralWallNatural : StaghornCoralWall { }

    public class SeaFanWallNatural : SeaFanWall { }

    public class BrainCoralWallNatural : BrainCoralWall { }
}
