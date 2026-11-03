using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Tentochi;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories.Tentochi
{
    /// <summary>
    /// Where Made By Tentochi comes from. Enterprise's Complimentary out of crates, by request; the rest, none being asked
    /// for, mine: what washes up with a fisher's crate, and what the Cyborg ("cyborg is of that origin") keeps to sell.
    ///
    /// <list type="bullet">
    /// <item>Any fishing crate: Enterprise's Complimentary 1 in 10, Exo Air I 1 in 15.</item>
    /// <item>A hardmode crate as well: Back Rocket 1 in 25, Calamity Module 1 in 25, Adam Kit I 1 in 30, Exo Air II 1 in 30.</item>
    /// <item>The Cyborg sells all six, at their price.</item>
    /// </list>
    /// </summary>
    public class TentochiCrates : GlobalItem
    {
        public const int ComplimentaryChance = 10;
        public const int ExoAirIChance = 15;
        public const int RocketChance = 25;
        public const int ModuleChance = 25;
        public const int KitChance = 30;
        public const int ExoAirIIChance = 30;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
            entity.type > ItemID.None && entity.type < ItemID.Sets.IsFishingCrate.Length && ItemID.Sets.IsFishingCrate[entity.type];

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<EnterprisesComplimentary>(), ComplimentaryChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<ExoAirI>(), ExoAirIChance));

            if (!ItemID.Sets.IsFishingCrateHardmode[item.type])
                return;

            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<BackRocket>(), RocketChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<CalamityModule>(), ModuleChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<AdamKitI>(), KitChance));
            itemLoot.Add(ItemDropRule.NotScalingWithLuck(ModContent.ItemType<ExoAirII>(), ExoAirIIChance));
        }
    }

    /// <summary>The Cyborg's stock of his makers' things: all six, at their price.</summary>
    public class TentochiCyborg : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType != NPCID.Cyborg)
                return;

            shop.Add<EnterprisesComplimentary>();
            shop.Add<ExoAirI>();
            shop.Add<ExoAirII>();
            shop.Add<BackRocket>();
            shop.Add<CalamityModule>();
            shop.Add<AdamKitI>();
        }
    }

    /// <summary>
    /// The Cyborg walks at 175% of the town's pace (by request: "Make cyborg walk speed rate 40% faster", then "Make
    /// cyborg 175% as fast as normal"): the legs
    /// he was built with, the safe kind. The game's town walk (NPC.AI_007_TownEntities) keeps its pace in a number of its
    /// own, so his way along is given the extra after it has had its say (PostAI) and handed back as it was before it
    /// next does (PreAI): it never sees its own extra, and the move between them (NPC.UpdateCollision) carries him the
    /// further. Whatever else moved him meanwhile (a wall, a blow) is left as it is. On every machine that runs him.
    /// </summary>
    public class TentochiCyborgPace : GlobalNPC
    {
        public const float Pace = 1.75f;

        /// <summary>His way along as it was given the extra, last tick; NaN when it was not.</summary>
        private float paced = float.NaN;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Cyborg;

        public override bool PreAI(NPC npc)
        {
            if (!float.IsNaN(paced) && System.Math.Abs(npc.velocity.X - paced) < 0.01f)
                npc.velocity.X /= Pace;

            paced = float.NaN;
            return true;
        }

        public override void PostAI(NPC npc)
        {
            npc.velocity.X *= Pace;
            paced = npc.velocity.X;
        }
    }

    /// <summary>
    /// The Cyborg fires his makers' rocket (by request: "he shoots 3 consecutive back rockets in succession with a 15
    /// degrees offset instead of three types of explosives"). Where the game would have him pick a rocket, a grenade or a
    /// mine (NPC.AI_007_TownEntities), he fires the Back Rocket's rocket, three in a row, a little apart, out of the tube on
    /// his back: the first 15 degrees above his aim, the next on it, the last 15 below. Each finds its own enemy and bursts
    /// 4 tiles wide, as the Back Rocket's do; 20 each (the game's Rocket I for him), scaled as all his shots are.
    ///
    /// The first is the game's own shot, turned and moved as it is made (OnSpawn: before it is sent to anyone); the other
    /// two follow from him, <see cref="Gap"/> ticks apart, on the world's machine (single player, or the server), inside
    /// the second he stands to fire. Nobody's but his: the same rocket out of anyone else is left as it is.
    /// </summary>
    public class TentochiCyborgVolley : GlobalNPC
    {
        public const int Shots = 3;

        /// <summary>Ticks between one rocket and the next.</summary>
        public const int Gap = 8;

        /// <summary>Between one rocket's line and the next: 15 degrees.</summary>
        public const float Spread = MathHelper.Pi / 12f;

        public const int Damage = 20;
        public const float Knockback = 4f;
        public const float Speed = 10f;

        /// <summary>What his second and third are made with, so they are not taken for a new first.</summary>
        public const string Context = "TentochiVolley";

        /// <summary>Rockets of this volley still to come, and the ticks until the next.</summary>
        private int left;
        private int wait;

        /// <summary>The volley as the game gave it: its line, its speed, what each carries, the way he faced.</summary>
        private Vector2 aim;
        private float speed;
        private int damage;
        private float knockback;
        private int side;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Cyborg;

        public override void TownNPCAttackStrength(NPC npc, ref int damage, ref float knockback)
        {
            damage = Damage;
            knockback = Knockback;
        }

        public override void TownNPCAttackCooldown(NPC npc, ref int cooldown, ref int randExtraCooldown)
        {
            // The game's own for his rocket.
            cooldown = 20;
            randExtraCooldown = 10;
        }

        public override void TownNPCAttackProj(NPC npc, ref int projType, ref int attackDelay)
        {
            projType = ModContent.ProjectileType<TentochiRocket>();
            attackDelay = 1;
        }

        public override void TownNPCAttackProjSpeed(NPC npc, ref float multiplier, ref float gravityCorrection, ref float randomOffset)
        {
            // Straight at what he aims at: the spread is the volley's, not chance's.
            multiplier = Speed;
            gravityCorrection = 0f;
            randomOffset = 0f;
        }

        /// <summary>The volley's first, the game's own shot, as it is made: on the first line, out of his back, finding its own enemy.</summary>
        public void Began(NPC npc, Projectile first)
        {
            side = npc.spriteDirection == 0 ? 1 : npc.spriteDirection;
            speed = first.velocity.Length();
            aim = first.velocity.SafeNormalize(new Vector2(side, 0f));
            damage = first.damage;
            knockback = first.knockBack;

            first.velocity = Line(0);
            first.Center = Tube(npc);
            first.rotation = first.velocity.ToRotation();
            first.ai[0] = -1f;

            left = Shots - 1;
            wait = Gap;
        }

        /// <summary>The rocket's way out, by its place in the volley: from 15 degrees above the aim to 15 below.</summary>
        private Vector2 Line(int index) => aim.RotatedBy((index - (Shots - 1) * 0.5f) * Spread * side) * speed;

        /// <summary>The tube on his back, at the shoulder.</summary>
        private Vector2 Tube(NPC npc) => npc.Top + new Vector2(-side * 4f, 8f);

        public override void PostAI(NPC npc)
        {
            if (left <= 0 || Main.netMode == NetmodeID.MultiplayerClient || --wait > 0)
                return;

            int index = Shots - left;
            int made = Projectile.NewProjectile(npc.GetSource_FromAI(Context), Tube(npc), Line(index), ModContent.ProjectileType<TentochiRocket>(),
                damage, knockback, Main.myPlayer, -1f);

            if (made >= 0 && made < Main.maxProjectiles)
            {
                // As the game marks his own shots: his, not the player's (no harm to players, nothing dropped).
                Main.projectile[made].npcProj = true;
                Main.projectile[made].noDropItem = true;
            }

            left--;
            wait = Gap;
        }
    }

    /// <summary>Tells the Cyborg his volley has begun: the rocket the game makes for him, as it is made.</summary>
    public class TentochiCyborgVolleyStart : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ModContent.ProjectileType<TentochiRocket>();

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_Parent { Entity: NPC npc } parent && parent.Context != TentochiCyborgVolley.Context
                && npc.TryGetGlobalNPC(out TentochiCyborgVolley volley))
                volley.Began(npc, projectile);
        }
    }
}
