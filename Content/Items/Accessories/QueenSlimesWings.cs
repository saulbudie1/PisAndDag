using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// Queen Slime's Wings, by request: "Also add Queen Slime's Wings; flight stuff equal that of empress of light's
    /// wings".
    ///
    /// Flight as the game gives its Empress Wings (wing 44, 1.4.4): 150 ticks in the air, 8 pixels a tick across at
    /// twice the acceleration (WingStatsInitializer), and the same climb (Player.WingMovement): 0.85 a tick back up
    /// when falling, 0.15 when rising, to at most 2.75 times the jump speed, 0.125 always, and with UP held a further
    /// push upward, with DOWN held (and not JUMP) a push downward; all of it in <see cref="VerticalWingSpeeds"/>,
    /// which the game asks at the same place in the same tick as it does its own.
    ///
    /// From the Queen Slime, as the Empress Wings are from the Empress (ItemDropDatabase): one time in fifteen when
    /// she is killed outside Expert, one in ten from her treasure bag (<see cref="QueenSlimesWingsDrop"/>). As every
    /// pair of wings in this world, they carry no one until the Empress has fallen (PisPlayer.AllowedWings).
    /// Pink, her own tier; 8 gold, the Empress Wings' worth. Its pictures (2x2 pixels): the item, and the four frames
    /// worn (QueenSlimesWings_Wings), crystal gel in her pink and blue.
    /// </summary>
    [AutoloadEquip(EquipType.Wings)]
    public class QueenSlimesWings : ModItem
    {
        public const int FlightTicks = 150;
        public const float FlightSpeed = 8f;
        public const float Acceleration = 2f;

        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
            ArmorIDs.Wing.Sets.Stats[Item.wingSlot] = new WingStats(FlightTicks, FlightSpeed, Acceleration);
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 8);
        }

        /// <summary>The Empress Wings' climb (Player.WingMovement, wingsLogic 44), number for number.</summary>
        public override void VerticalWingSpeeds(Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
            ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
        {
            ascentWhenFalling = 0.85f;
            ascentWhenRising = 0.15f;
            maxCanAscendMultiplier = 1f;
            maxAscentMultiplier = 2.75f;
            constantAscend = 0.125f;

            if (player.TryingToHoverUp)
            {
                player.velocity.Y -= 0.4f * player.gravDir;

                if (player.gravDir == 1f)
                {
                    if (player.velocity.Y > 0f)
                        player.velocity.Y -= 1f;
                    else if (player.velocity.Y > -Player.jumpSpeed)
                        player.velocity.Y -= 0.2f;

                    if (player.velocity.Y < -Player.jumpSpeed * 3f)
                        player.velocity.Y = -Player.jumpSpeed * 3f;
                }
                else
                {
                    if (player.velocity.Y < 0f)
                        player.velocity.Y += 1f;
                    else if (player.velocity.Y < Player.jumpSpeed)
                        player.velocity.Y += 0.2f;

                    if (player.velocity.Y > Player.jumpSpeed * 3f)
                        player.velocity.Y = Player.jumpSpeed * 3f;
                }
            }

            if (player.TryingToHoverDown && !player.controlJump && player.velocity.Y != 0f)
                player.velocity.Y += 0.4f;
        }

        /// <summary>A glint of her crystal now and then off the wings while they beat; the game animates them.</summary>
        public override bool WingUpdate(Player player, bool inUse)
        {
            if (inUse && !Main.dedServ && Main.rand.NextBool(3))
            {
                Vector2 back = new Vector2(player.Center.X - player.direction * 14f, player.Center.Y - 4f);
                Dust dust = Dust.NewDustDirect(back - new Vector2(12f, 10f), 24, 20, Main.rand.NextBool() ? DustID.PinkCrystalShard : DustID.BlueCrystalShard,
                    0f, 0f, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity = dust.velocity * 0.3f - player.velocity * 0.15f;
                dust.shader = Terraria.Graphics.Shaders.GameShaders.Armor.GetSecondaryShader(player.cWings, player);
            }

            return false;
        }
    }

    /// <summary>Where they come from: the Queen Slime, as the Empress Wings come from the Empress.</summary>
    public class QueenSlimesWingsDrop : GlobalNPC
    {
        /// <summary>One in this many when she is killed outside Expert (the Empress Wings': 15).</summary>
        public const int DropChance = 15;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.QueenSlimeBoss;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.ByCondition(new Conditions.NotExpert(), ModContent.ItemType<QueenSlimesWings>(), DropChance));
        }
    }

    /// <summary>And from her treasure bag, one in ten, not scaled by luck (the Empress's bag gives her wings so).</summary>
    public class QueenSlimesWingsBag : GlobalItem
    {
        public const int BagChance = 10;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.QueenSlimeBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<QueenSlimesWings>(), BagChance));
        }
    }
}
