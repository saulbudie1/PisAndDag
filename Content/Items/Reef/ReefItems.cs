using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Reef
{
    /// <summary>
    /// A reef block, as an item (Tiles.Reef): what mining one gives, and what puts one down. Found only on the Vast
    /// Ocean's reefs (VastOceanReef).
    /// </summary>
    public abstract class ReefBlockItem : ModItem
    {
        protected abstract int Places { get; }

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 100;

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(Places);
            Item.width = 16;
            Item.height = 16;
            Item.value = Item.sellPrice(copper: 20);
        }
    }

    public class ReefRock : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.ReefRock>(); }

    public class CoralRubble : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.CoralRubble>(); }

    public class CorallineCrust : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.CorallineCrust>(); }

    public class BrainCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.BrainCoral>(); }

    public class StarCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.StarCoral>(); }

    public class StaghornCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.StaghornCoral>(); }

    public class PlateCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.PlateCoral>(); }

    public class BlueCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.BlueCoral>(); }

    public class OrganPipeCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.OrganPipeCoral>(); }

    public class BubbleCoral : ReefBlockItem { protected override int Places => ModContent.TileType<Tiles.Reef.BubbleCoral>(); }

    /// <summary>
    /// A reef wall, as an item (Walls.Reef): what breaking one gives, and what puts one up; four of them from a block of
    /// the reef they are of at a work bench, and that block back from four.
    /// </summary>
    public abstract class ReefWallItem : ModItem
    {
        protected abstract int Places { get; }

        /// <summary>The block it is made from, and back into.</summary>
        protected abstract int Block { get; }

        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 400;

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableWall(Places);
            Item.width = 24;
            Item.height = 24;
        }

        public override void AddRecipes()
        {
            CreateRecipe(4)
                .AddIngredient(Block)
                .AddTile(TileID.WorkBenches)
                .Register();

            Recipe.Create(Block)
                .AddIngredient(Type, 4)
                .AddTile(TileID.WorkBenches)
                .Register();
        }
    }

    public class ReefRockWall : ReefWallItem
    {
        protected override int Places => ModContent.WallType<Walls.Reef.ReefRockWall>();
        protected override int Block => ModContent.ItemType<ReefRock>();
    }

    public class StaghornCoralWall : ReefWallItem
    {
        protected override int Places => ModContent.WallType<Walls.Reef.StaghornCoralWall>();
        protected override int Block => ModContent.ItemType<StaghornCoral>();
    }

    public class SeaFanWall : ReefWallItem
    {
        protected override int Places => ModContent.WallType<Walls.Reef.SeaFanWall>();
        protected override int Block => ModContent.ItemType<BubbleCoral>();
    }

    public class BrainCoralWall : ReefWallItem
    {
        protected override int Places => ModContent.WallType<Walls.Reef.BrainCoralWall>();
        protected override int Block => ModContent.ItemType<BrainCoral>();
    }
}
