using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// Betsy's Flames, by request: "Type: Dragonic, grotesque transforms player's head into Betsy's and
    /// open her mouth and shoots flames at cursor for 5 seconds. Each sec consumes 30 mana. Same
    /// breathe as the game. Use game sounds too with roars effect and shakes shi. enough damage to rival
    /// blaze no upgrade. 1/20 chance to drop by betsy"
    ///
    /// Cast through a seal: the caster's head turns into Betsy's, she roars, and she breathes the game's
    /// own flame breath at the cursor for five seconds, then goes back. All of that is the breath's
    /// (<see cref="BetsysFlamesBreath"/>); this is the page.
    ///
    /// Its element is Draconic, as asked later: "the element of betsy flame is draconic, type you think."
    /// (Dragonic was first read as its type, and Flame as its element.) Its type is Transmutation
    /// (mine): on the chart of schools it turns one thing into another, and it turns the caster's head
    /// into Betsy's. Its breath is still fire to Rage! Sweet Fire, which goes by what hits, not by the
    /// element (FireAffinity).
    ///
    /// Mana: <see cref="Mana"/>, paid as it is cast, is the first second of breath; then 30 a second
    /// (BetsysFlamesBreath.ManaPerSecond), and it stops when that runs out.
    ///
    /// Damage: "blaze" first read as the game's Nebula Blaze; then: "no i mean nebula arcaenum". The
    /// Nebula Arcanum (Item.SetDefaults, 3476) throws an orb of 70 every 30 ticks. On one enemy, its
    /// orbs and the 35 sparks they throw off share the enemy's 10-tick immunity
    /// (Projectile.Damage: they do not keep their own), so no more than 6 of those land a second; and
    /// each orb ends in 10 to 16 shards of 45 (Projectile.Kill, 617) which pass any immunity, and of
    /// which the more land the bigger the enemy is. Worked out by the game's numbers: about 760 to 970
    /// a second to an enemy a player's size, about 975 to 1,185 to one of 40 by 56, about 1,265 to
    /// 1,475 to one of 160 by 160. The breath hurts each thing in it every 5 ticks
    /// (BetsysFlamesBreath.HitCooldown, asked lower: "Make the breathe have lower localhittime"), so
    /// <see cref="CastDamage"/> 84 is 1,008 a second to each, before either's bonuses.
    ///
    /// Where it comes from: Betsy, 1 in 20; from her treasure bag in expert and master, as the game drops
    /// her own weapons (<see cref="BetsysFlamesSource"/>, <see cref="BetsysFlamesBag"/>). Yellow and 5 gold
    /// to sell, as her own weapons are (mine). Its picture is a stand-in (PlaceholderTextures.md).
    /// </summary>
    public class BetsysFlames : Castable
    {
        /// <summary>One Betsy in this many drops it.</summary>
        public const int DropChance = 20;

        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 84;
        public override int Mana => (int)BetsysFlamesBreath.ManaPerSecond;
        public override int UseTime => 20;
        public override int Shot => ModContent.ProjectileType<BetsysFlamesBreath>();
        public override string CastType => "Transmutation";
        public override string Element => "Draconic";
        public override float Knockback => 0f;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 5);
        }

        /// <summary>One breath at a time, and not again until the head has gone back.</summary>
        public override bool CanCast(Player player) => player.ownedProjectileCounts[Shot] == 0;

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            // The aim starts at the cursor; ai[1] carries it from here on.
            float aim = (Main.MouseWorld - BetsysFlamesBreath.Pivot(player, player.direction)).ToRotation();
            Projectile.NewProjectile(source, player.MountedCenter, Vector2.Zero, Shot, damage, knockback, player.whoAmI, 0f, aim);
        }

        /// <summary>The mana line says what it costs: a second's worth, every second.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            base.ModifyTooltips(tooltips);

            TooltipLine mana = tooltips.Find(t => t.Mod == Mod.Name && t.Name == "PADMana");

            if (mana != null)
                mana.Text = Language.GetTextValue("Mods.PisAndDag.Magic.ManaPerSecond", Mana);
        }
    }

    /// <summary>Where it comes from: Betsy, 1 in 20, in classic (in expert and master her bag has it).</summary>
    public class BetsysFlamesSource : GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.DD2Betsy)
                npcLoot.Add(ItemDropRule.ByCondition(new Conditions.NotExpert(), ModContent.ItemType<BetsysFlames>(), BetsysFlames.DropChance));
        }
    }

    /// <summary>Betsy's treasure bag, 1 in 20.</summary>
    public class BetsysFlamesBag : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            if (item.type == ItemID.BossBagBetsy)
                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<BetsysFlames>(), BetsysFlames.DropChance));
        }
    }
}
