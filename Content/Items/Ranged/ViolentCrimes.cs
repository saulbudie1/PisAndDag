using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// Violent Crimes, the Twins' weapon in a Legends world (by request: "Legends weapon: Violent Crimes dropped by twins.
    /// A flame thrower with hold out: Right click: spit flame like normal flame thrower. Hold right click for more than 5
    /// seconds: Shoot auto reuse flame laser interval of 60. Left click: spit flamable gas flamable only by flame thrower
    /// and flame laser of violent crimes. left click for more than 5 seconds: Auto reuse propel player at mouse direction
    /// for 30 ticks at high vel making it travel 35 tiles interval of 60 ticks"; asked after: "Laser replaces flame";
    /// "damaging non flamable gas along path, the flamable gas in the first 5 seconds is non damaging until lit";
    /// "consume gel and ranged"; drawn in code, "80x40").
    ///
    /// The Twins in one gun: a tank of fire (the Retinazer's) and a tank of gas (the Spazmatism's).
    ///
    ///   RIGHT, held   FIRE: a flamethrower's stream (ViolentCrimesFlame, as vanilla's flame goes: 32 tiles, through
    ///                 four enemies), in the new fire (PADFire). Past five seconds the stream stops and it fires a FLAME
    ///                 LASER every second instead (ViolentCrimesLaser: a column of fire two tiles wide to the first wall,
    ///                 90 tiles at most, through everything, nine times a flame's damage).
    ///   LEFT, held    GAS: clouds that slow, swell and hang ten seconds, harming nothing (ViolentCrimesGas). Its own
    ///                 fire and laser light them, and nothing else does; a cloud going up hurts what is in it (four and
    ///                 a half times a flame's damage) and lights the clouds beside it. Past five seconds it stops
    ///                 spraying and THROWS its holder 35 tiles toward the cursor every second (30 ticks of flight), on a
    ///                 jet of chlorine left along the way (ViolentCrimesChlorine: it hurts what stands in it and will
    ///                 not burn).
    ///
    /// It is in the hands for as long as it is the item held, and everything it does is done by the gun in the hands
    /// (ViolentCrimesHoldout); the item itself is never used. Ranged; a Gel every half second it works. Its fire gives
    /// enemies Industrial Flames, its chlorine Chlorine Poisoning (the Twins' own, 30 life a second for 3 seconds).
    ///
    /// Only in a Legends world: from the last of the Twins where they drop no bag, in their bag where they do (both
    /// <see cref="LegendsOnly"/>), every time. A "Legends" line under its tooltip, in their two colours.
    /// </summary>
    public class ViolentCrimes : ModItem
    {
        public const int Damage = 44;
        public const float Knockback = 0.4f;

        /// <summary>A flame's speed, an update (it moves three times a tick, as vanilla's).</summary>
        public const float FlameSpeed = 7.5f;

        /// <summary>Ticks a button is held before its second work begins (by request: "more than 5 seconds").</summary>
        public const int SwitchTicks = 300;

        /// <summary>Ticks between lasers, and between throws (by request: "interval of 60").</summary>
        public const int Every = 60;

        /// <summary>Ticks between flames, between clouds of gas, and between the Gels it takes.</summary>
        public const int FlameEvery = 6;
        public const int GasEvery = 6;
        public const int GelEvery = 30;

        /// <summary>What each does, of the gun's damage.</summary>
        public const float LaserDamage = 9f;
        public const float GasDamage = 4.5f;
        public const float ChlorineDamage = 0.8f;

        /// <summary>A throw: its ticks, how far it carries (by request: 30 ticks, 35 tiles), and the speed it ends at.</summary>
        public const int ThrowTicks = 30;
        public const float ThrowFar = 35f * 16f;
        public const float ThrowEnds = 6f;

        /// <summary>The most clouds of its gas one holder may have hanging.</summary>
        public const int MostGas = 72;

        /// <summary>How fast it is thrown this many ticks into a throw: quick off the mark, down to a run, <see cref="ThrowFar"/> in all.</summary>
        public static float ThrowSpeed(int tick)
        {
            float mean = ThrowFar / ThrowTicks;
            float begins = 2f * mean - ThrowEnds;
            return MathHelper.Lerp(begins, ThrowEnds, MathHelper.Clamp(tick / (float)(ThrowTicks - 1), 0f, 1f));
        }

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 80;
            Item.height = 40;

            Item.damage = Damage;
            Item.DamageType = DamageClass.Ranged;
            Item.knockBack = Knockback;

            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = FlameEvery;
            Item.useAnimation = FlameEvery;
            Item.autoReuse = true;
            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.shoot = ModContent.ProjectileType<ViolentCrimesFlame>();
            Item.shootSpeed = FlameSpeed;
            Item.useAmmo = AmmoID.Gel;

            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 8);
        }

        /// <summary>Never used as an item: the gun in the hands reads the buttons itself (ViolentCrimesHoldout).</summary>
        public override bool CanUseItem(Player player) => false;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Their two colours, turn about.
            float turn = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f);

            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = Color.Lerp(TwinsArt.FireBright, TwinsArt.ToxicBright, turn),
            });
        }
    }

    /// <summary>Violent Crimes from the last of the Twins itself, where they drop no bag (Classic), in a Legends world.</summary>
    public class ViolentCrimesDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.Retinazer || entity.type == NPCID.Spazmatism;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // As vanilla's own for them: only from the one that dies with its twin already gone.
            LeadingConditionRule last = new LeadingConditionRule(new Conditions.MissingTwin());
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<ViolentCrimes>()));
            last.OnSuccess(classic);
            npcLoot.Add(last);
        }
    }

    /// <summary>Violent Crimes in the Twins' bag (Expert and Master), in a Legends world.</summary>
    public class ViolentCrimesBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.TwinsBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<ViolentCrimes>()));
        }
    }
}
