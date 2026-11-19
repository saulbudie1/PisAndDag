using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles.Reef
{
    /// <summary>
    /// The Vast Ocean's reef blocks (VastOceanReef grows them; ReefPlan.Block, in that order): reef rock and what lies on
    /// and under it, and the corals grown over it. By request ("Yes, mine and place"): each mined gives itself, as a block
    /// does (the item that places it: Items.Reef), and each goes down by hand. They join one another as one block does
    /// with itself, so that a reef shows as one growth whatever it is grown of.
    /// </summary>
    public abstract class ReefBlock : ModTile
    {
        protected abstract Color MapColour { get; }

        protected virtual int Dust => DustID.Coralstone;

        protected virtual SoundStyle? Sound => SoundID.Tink;

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            foreach (ReefBlock other in ModContent.GetContent<ReefBlock>())
            {
                Main.tileMerge[Type][other.Type] = true;
                Main.tileMerge[other.Type][Type] = true;
            }

            DustType = Dust;
            HitSound = Sound;
            MineResist = 1f;

            AddMapEntry(MapColour, CreateMapEntryName());
        }

        public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;
    }

    public class ReefRock : ReefBlock
    {
        protected override Color MapColour => new Color(146, 140, 128);
        protected override int Dust => DustID.Stone;
    }

    public class CoralRubble : ReefBlock
    {
        protected override Color MapColour => new Color(214, 196, 178);
        protected override SoundStyle? Sound => SoundID.Dig;
    }

    public class CorallineCrust : ReefBlock
    {
        protected override Color MapColour => new Color(206, 104, 130);
    }

    public class BrainCoral : ReefBlock
    {
        protected override Color MapColour => new Color(180, 170, 98);
    }

    public class StarCoral : ReefBlock
    {
        protected override Color MapColour => new Color(124, 138, 88);
    }

    public class StaghornCoral : ReefBlock
    {
        protected override Color MapColour => new Color(206, 146, 92);
    }

    public class PlateCoral : ReefBlock
    {
        protected override Color MapColour => new Color(96, 156, 176);
    }

    public class BlueCoral : ReefBlock
    {
        protected override Color MapColour => new Color(64, 128, 222);
    }

    public class OrganPipeCoral : ReefBlock
    {
        protected override Color MapColour => new Color(206, 62, 64);
    }

    public class BubbleCoral : ReefBlock
    {
        protected override Color MapColour => new Color(156, 104, 210);
    }
}
