using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Green Candiez, by request: "Recipe to combine triple fart in a jar thing with Terraspark: Have the attributes
    /// of the 2 accessories; the rocket boots gonna have farts out with the trail, it sparks red and green in the
    /// rocket boot trail. and add 6 mph on top of that; make sure it doesnt stack with terraspark."
    ///
    /// Worn, it is both, as the game and the mod have them:
    ///   - the Terraspark Boots, the game's own effects of them run for it (Player.ApplyEquipFunctional with the
    ///     game's boots): rocket flight (its rocket boots 4), the run to 6.75 pixels a tick, 8% movement speed,
    ///     ice skating, walking on water and lava, fire blocks and 7 seconds of lava;
    ///   - the Triple Fart In A Horse Shoe Adorned Bottle, its own UpdateAccessory run for it: three of the
    ///     Fart in a Jar's jumps in the air and no fall damage;
    ///   - and <see cref="Mph"/> more on the top speed, put on where the game has settled the run speeds, as
    ///     Nurtured puts its own on (so it reads that much more on the stopwatch, boots or not; not on a mount).
    /// While the rockets burn, gas and red and green sparks come off them and now and then a fart
    /// (<see cref="GreenCandiezPlayer"/>).
    ///
    /// "doesnt stack with terraspark": it cannot be worn with the Terraspark Boots, nor with the bottle it is made
    /// from, either way round (<see cref="CanAccessoryBeEquippedWith"/>). Nothing in it adds to itself in any case:
    /// the boots set their speeds, the bottle switches its jump on.
    ///
    /// The recipe is live, as it was asked for: the bottle and the boots at a Tinkerer's Workshop, where the game
    /// makes the Terraspark Boots. The bottle still has none (its recipe is commented out in its file). Lime and
    /// 19 gold: the boots' rarity, the two's worth together. Worn, the player wears the Terraspark Boots and the
    /// Fart in a Jar's jar (the game's pictures for both). Its own picture: Content/Items/Accessories/GreenCandiez.png.
    /// </summary>
    public class GreenCandiez : ModItem
    {
        /// <summary>Miles an hour on top of the boots' top speed.</summary>
        public const float Mph = 6f;

        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 32;
            Item.accessory = true;
            Item.shoeSlot = ArmorIDs.Shoe.TerrasparkBoots;
            Item.waistSlot = ArmorIDs.Waist.FartinaJar;
            Item.hasVanityEffects = true;
            Item.rare = ItemRarityID.Lime;
            Item.value = Item.sellPrice(gold: 19);
        }

        private static Item Boots => ContentSamples.ItemsByType[ItemID.TerrasparkBoots];

        private static Item Bottle => ContentSamples.ItemsByType[ModContent.ItemType<TripleFartInAHorseShoeAdornedBottle>()];

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.ApplyEquipFunctional(Boots, hideVisual);
            Bottle.ModItem.UpdateAccessory(player, hideVisual);

            GreenCandiezPlayer candiez = player.GetModPlayer<GreenCandiezPlayer>();
            candiez.Worn = true;
            candiez.Trail |= !hideVisual;
        }

        /// <summary>In a vanity slot: the boots' look and the trail, nothing else.</summary>
        public override void UpdateVanity(Player player)
        {
            player.ApplyEquipVanity(Boots);
            player.GetModPlayer<GreenCandiezPlayer>().Trail = true;
        }

        /// <summary>Not with the Terraspark Boots, nor with the bottle; the game asks of both items, either way round.</summary>
        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player)
        {
            Item other = equippedItem.type == Type ? incomingItem : equippedItem;
            return other.type != ItemID.TerrasparkBoots && other.type != ModContent.ItemType<TripleFartInAHorseShoeAdornedBottle>();
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<TripleFartInAHorseShoeAdornedBottle>()
                .AddIngredient(ItemID.TerrasparkBoots)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }

    /// <summary>
    /// What Green Candiez does beyond the two it is made of: the six miles an hour, and the trail. Every machine runs
    /// it for every player, as it does their rockets; the trail is dust, each machine's own.
    /// </summary>
    public class GreenCandiezPlayer : ModPlayer
    {
        /// <summary>Worn in a functional slot this tick.</summary>
        public bool Worn;

        /// <summary>Its trail is to be seen this tick: worn and shown, or in a vanity slot.</summary>
        public bool Trail;

        /// <summary>Ticks to the next fart while the rockets burn.</summary>
        private int fartIn;

        /// <summary>Ticks between farts while the rockets burn, at the least and at the most.</summary>
        private const int FartMin = 26, FartMax = 44;

        private static readonly Color Red = new Color(255, 70, 60);
        private static readonly Color Green = new Color(110, 255, 90);

        public override void ResetEffects()
        {
            Worn = false;
            Trail = false;
        }

        public override void PostUpdateRunSpeeds()
        {
            if (!Worn || Player.mount.Active)
                return;

            float faster = GreenCandiez.Mph / TopSpeed.MphPerPixelTick;
            Player.maxRunSpeed += faster;
            Player.accRunSpeed += faster;
        }

        /// <summary>
        /// The trail, while the rockets burn (Player.rocketFrame: the game's own rocket dust is coming off this tick,
        /// Player.RocketBootVisuals): from each boot, a puff of the Fart in a Jar's gas (dust 188), and a spark that
        /// is red or green (the Terraspark's own spark, DustID.FireworksRGB, in the colour given), going back the way the
        /// boot's flame goes; and every half second or so a fart: the jar's sound, and one of its clouds.
        /// </summary>
        public override void PostUpdate()
        {
            if (Main.dedServ || !Trail || !Player.rocketFrame)
            {
                fartIn = 0;
                return;
            }

            int foot = Player.gravDir == -1f ? 4 : Player.height;

            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? Player.position.X - 4f : Player.position.X + Player.width - 4f;

                if (Player.direction == -1)
                    x -= 4f;

                Vector2 at = new Vector2(x, Player.position.Y + foot - 10f + 2f * Player.gravDir);
                Vector2 away = new Vector2((side == 0 ? 2f : -2f) - Player.velocity.X * 0.3f, 2f * Player.gravDir - Player.velocity.Y * 0.3f);

                if (Main.rand.NextBool(2))
                {
                    Dust gas = Dust.NewDustDirect(at, 8, 8, DustID.FartInAJar, 0f, 0f, 100, default, 1.1f);
                    gas.velocity = gas.velocity * 0.3f + away * 0.6f;
                    gas.noGravity = true;
                    gas.shader = Terraria.Graphics.Shaders.GameShaders.Armor.GetSecondaryShader(Player.cShoe, Player);
                }

                Dust spark = Dust.NewDustDirect(at, 8, 8, DustID.FireworksRGB, 0f, 0f, 100,
                    Color.Lerp(Main.rand.NextBool() ? Red : Green, Color.White, Main.rand.NextFloat() * 0.25f), 0.7f);
                spark.noGravity = true;
                spark.velocity = spark.velocity * 0.25f - Player.velocity * 0.5f + away * 0.6f;
                spark.position += spark.velocity * 3f;
                spark.shader = Terraria.Graphics.Shaders.GameShaders.Armor.GetSecondaryShader(Player.cShoe, Player);

                if (Main.rand.NextBool(4))
                    spark.fadeIn = 0.9f;
            }

            if (--fartIn > 0)
                return;

            fartIn = Main.rand.Next(FartMin, FartMax + 1);
            SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.55f, PitchVariance = 0.3f }, Player.Bottom);

            Vector2 cloud = new Vector2(Player.Center.X - 16f, Player.position.Y + (Player.gravDir == -1f ? 0f : Player.height) - 16f);
            int gore = Gore.NewGore(Player.GetSource_FromThis(), cloud, -Player.velocity * 0.1f, Main.rand.Next(435, 438));

            if (gore >= 0 && gore < Main.maxGore)
                Main.gore[gore].velocity = Main.gore[gore].velocity * 0.1f - Player.velocity * 0.1f + new Vector2(0f, 0.6f * Player.gravDir);
        }
    }
}
