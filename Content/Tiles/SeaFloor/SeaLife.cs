namespace PisAndDag.Content.Tiles.SeaFloor
{
    /// <summary>
    /// What lives on the Vast Ocean's floor, and its vents, as the place lays them out
    /// (VastOceanFloor, by request: "you can also create ambiance tiles for extra ambiances, like
    /// smoke/gas emitting things and Coral reefs and sponges and other tiles that is needed",
    /// "be sure to also create particles for those ambience tiles too") and as the game holds
    /// them (<see cref="SeaLifeTile"/>): which, how many tiles wide and tall, how many pictures
    /// of each. Asks nothing of the mod's loaded types, so that the preview can lay the floor out
    /// with the game alone.
    ///
    /// Each is of the deep sea as it is known (the notes for section 16.87 have the sources):
    ///   - giant tube worms (Riftia) about the black smokers, white tubes and red plumes, mussels
    ///     at their feet; bushes of the thinner seep tube worms (Lamellibrachia) rooted in the
    ///     seeps' carbonate;
    ///   - the cold-water corals of seamounts: the white or orange bushes that build reefs
    ///     (Lophelia), bubblegum coral (Paragorgia) pink or red, bamboo coral (white joints, dark
    ///     nodes), black coral (a dark skeleton under orange or white polyps);
    ///   - glass sponges (Venus' flower basket) standing on their glass threads, yellow cup
    ///     sponges, stalked sea lilies, anemones (one a tube anemone), sea pens (Umbellula, a
    ///     cluster of polyps on a long stalk, and a feather one), and xenophyophores, cells as big
    ///     as a fist built out of the sediment;
    ///   - the vents themselves: a black smoker's mouth, a white smoker's, a seep's crack.
    /// </summary>
    public static class SeaLife
    {
        public enum Kind : byte
        {
            GiantTubeWorms,
            SeepTubeWorms,
            ColdWaterCoral,
            BubblegumCoral,
            BambooCoral,
            BlackCoral,
            GlassSponge,
            CupSponge,
            SeaPen,
            SeaLily,
            DeepAnemone,
            Xenophyophore,
            VentMouth,
            WhiteVentMouth,
            SeepVent,
        }

        public const int Kinds = 15;

        private static readonly byte[] wide = { 3, 3, 3, 4, 2, 3, 1, 2, 1, 2, 2, 2, 1, 1, 1 };
        private static readonly byte[] tall = { 4, 4, 3, 4, 5, 4, 3, 2, 4, 4, 2, 2, 1, 1, 1 };
        private static readonly byte[] styles = { 3, 2, 3, 2, 2, 3, 3, 2, 3, 2, 3, 3, 1, 1, 2 };

        public static int Wide(Kind kind) => wide[(int)kind];

        public static int Tall(Kind kind) => tall[(int)kind];

        /// <summary>How many pictures it has, side by side on its sheet.</summary>
        public static int Styles(Kind kind) => styles[(int)kind];

        /// <summary>The cell it is put down by: its bottom row, its middle column (of two middle ones, the left).</summary>
        public static int OriginX(Kind kind) => (wide[(int)kind] - 1) / 2;

        public static int OriginY(Kind kind) => tall[(int)kind] - 1;
    }
}
