using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// Slimy Fortification, a spell from King Slime in a Legends world, by request ("Slimes fly from cursor to target
    /// NPCs nearest to cursor, encasing it in slime and making it fall onto the ground (given it is small npc) for 5
    /// seconds, takes build up").
    ///
    /// Each cast, three slimes come out of the air at the cursor and fly at whatever is nearest it (within
    /// <see cref="Seeks"/> of it; the cursor no further than <see cref="Reach"/> from the caster). Each that lands
    /// hurts a little and leaves slime on it (SlimyFortificationGlob, SlimeCasing): enough of it, and it is encased
    /// for five seconds (Encased). How much is enough goes by its size: one cast for something a player's size, a
    /// good few for something big. Encased, a small thing cannot move, act or hurt by touch, and drops to the ground;
    /// a bigger one is only slowed; a boss is never encased, only hurt. Slime that is not added to dries off.
    ///
    /// Conjuration on the chart of schools: it makes. Flora and Fauna: drawn from what lives.
    /// Mine: its numbers, and what "small" is (SlimeCasing.Small). Placeholder art (PlaceholderTextures.md).
    /// </summary>
    public class SlimyFortification : Castable
    {
        /// <summary>Slimes a cast; how far from the caster the cursor may be; how far from the cursor they look for something.</summary>
        public const int Count = 3;
        public const float Reach = 45f * 16f;
        public const float Seeks = 22f * 16f;

        public override CastKind Kind => CastKind.Spell;
        public override int CastDamage => 12;
        public override int Mana => 12;
        public override int UseTime => 30;
        public override int Shot => ModContent.ProjectileType<SlimyFortificationGlob>();
        public override string CastType => "Conjuration";
        public override string Element => "Flora and Fauna";
        public override float Speed => SlimyFortificationGlob.Speed;
        public override float Knockback => 1f;
        public override SoundStyle? CastSound => SoundID.Item154 with { Volume = 0.7f, PitchVariance = 0.2f };

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Green;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            base.ModifyTooltips(tooltips);
            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = new Color(96, 168, 255),
            });
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            // Out of the air at the cursor, kept within reach of the caster.
            Vector2 from = Main.MouseWorld;
            Vector2 off = from - player.MountedCenter;

            if (off.Length() > Reach)
                from = player.MountedCenter + off.SafeNormalize(Vector2.UnitX) * Reach;

            // Whatever is nearest the cursor.
            int target = -1;
            float nearest = Seeks;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                float d = Vector2.Distance(npc.Center, from);

                if (d < nearest && npc.CanBeChasedBy())
                {
                    nearest = d;
                    target = npc.whoAmI;
                }
            }

            for (int i = 0; i < Count; i++)
            {
                // Thrown up and out, each its own way; they turn onto what they are after.
                Vector2 out_ = new Vector2((i - (Count - 1) * 0.5f) * 2.4f + Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(3.5f, 5.5f));
                Projectile.NewProjectile(source, from + Main.rand.NextVector2Circular(6f, 6f), out_, Shot, damage, knockback, player.whoAmI, target, i * 4);
            }

            if (!Main.dedServ)
            {
                for (int d = 0; d < 10; d++)
                    Dust.NewDustPerfect(from, DustID.t_Slime, Main.rand.NextVector2Circular(3f, 3f), 150, new Color(86, 150, 255, 150), Main.rand.NextFloat(0.9f, 1.4f));
            }
        }
    }

    /// <summary>Slimy Fortification from King Slime himself, where he drops no bag (Classic), in a Legends world.</summary>
    public class SlimyFortificationDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.KingSlime;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<SlimyFortification>()));
            npcLoot.Add(classic);
        }
    }

    /// <summary>Slimy Fortification in King Slime's bag (Expert and Master), in a Legends world.</summary>
    public class SlimyFortificationBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.KingSlimeBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<SlimyFortification>()));
        }
    }
}
