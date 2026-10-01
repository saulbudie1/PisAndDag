using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// The Fly Swatter: short, iron and cobweb, swung. 12 damage. A bee or a hornet dies in
    /// one swat, whatever its life (NPCID.Bee, BeeSmall, and the jungle's hornets of every
    /// kind and size); the Queen Bee takes it as a 70-damage weapon. 3 Iron or Lead Bars and
    /// 2 Cobwebs at an anvil.
    ///
    /// It goes through: one swing hits everything it touches (<see cref="SwatThrough"/>).
    /// </summary>
    public class FlySwatter : ModItem
    {
        public const int Damage = 12;

        /// <summary>What the Queen Bee takes at base, in place of <see cref="Damage"/>.</summary>
        public const int QueenBeeDamage = 70;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Melee;
            Item.knockBack = 3f;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.autoReuse = true;
            Item.UseSound = SoundID.Item1;

            Item.rare = ItemRarityID.White;
            Item.value = Item.sellPrice(silver: 3);
        }

        /// <summary>A bee or a hornet: dies in one swat. Not the Queen Bee. The hornets' sizes share these types.</summary>
        public static bool Swattable(int type) => type switch
        {
            NPCID.Bee or NPCID.BeeSmall => true,
            NPCID.Hornet or NPCID.MossHornet => true,
            NPCID.HornetFatty or NPCID.HornetHoney or NPCID.HornetLeafy or NPCID.HornetSpikey or NPCID.HornetStingy => true,
            _ => false,
        };

        /// <summary>The instant kill is carried in the strike itself, so it holds in multiplayer.</summary>
        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (Swattable(target.type))
                modifiers.SetInstantKill();
            else if (target.type == NPCID.QueenBee)
                modifiers.SourceDamage *= QueenBeeDamage / (float)Damage;
        }

        public override void Load()
        {
            On_Player.ItemCheck_MeleeHitNPCs += SwatThrough;
        }

        /// <summary>
        /// Vanilla's swing hits one thing, then waits a third of the swing before the next
        /// (Player.attackCD, checked for every NPC in its loop). The swatter does not wait: the
        /// loop again, the wait cleared, for as long as something new is hit. Each NPC is still
        /// hit once a swing (vanilla's own per-NPC melee cooldown).
        /// </summary>
        private static void SwatThrough(On_Player.orig_ItemCheck_MeleeHitNPCs orig, Player self, Item sItem, Rectangle itemRectangle, int originalDamage, float knockBack)
        {
            if (sItem.type != ModContent.ItemType<FlySwatter>())
            {
                orig(self, sItem, itemRectangle, originalDamage, knockBack);
                return;
            }

            for (int pass = 0; pass < Main.maxNPCs; pass++)
            {
                self.attackCD = 0;
                orig(self, sItem, itemRectangle, originalDamage, knockBack);

                if (self.attackCD == 0)
                    break;
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.IronBar, 3)
                .AddIngredient(ItemID.Cobweb, 2)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
