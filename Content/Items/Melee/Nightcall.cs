using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Accessories;
using PisAndDag.Content.Items.Accessories.Tentochi;
using PisAndDag.Content.Items.Melee.Fists;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Melee;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>What one of Nightcall's hands is: Skeletron Prime's vice, cannon, saw or laser.</summary>
    public enum NightcallFist : byte
    {
        Vice,
        Cannon,
        Chainsaw,
        Laser,
    }

    /// <summary>
    /// Nightcall, Skeletron Prime's weapon in a Legends world (by request: "Nightcall - Legends: a boxing weapon where you
    /// can click the same button as reload button (bind together) to open a menu to choose element of left and right
    /// fist from 4 modes making 16 different combinations: Vice, Cannon, Chainsaw and Laser ... reload will open a UI where
    /// there is 2 circles next to player, click on one opens 4 options. default is vice on both").
    ///
    /// A fist weapon (FistWeapon): the left hand's blow (the front arm), then the right's (the back arm), each as its
    /// hand is set (NightcallPlayer, kept with the player; the menu: NightcallMenu, on the reload key):
    ///
    ///   VICE      ("2x close damage, larger dodge window after punch"; asked after: "Damage set by weapon so it
    ///             basically 2x", and "so it can dodge better"): twice the weapon's damage; what it lands on is held
    ///             off its player three times as long (FistPlayer), and its guard reaches half again as far and stays
    ///             up half a second after the blow is back.
    ///   CANNON    ("create an explosion dealing additional damage ontop of the fist damage"): a blast where it lands
    ///             (NightcallBlast), three tiles, three quarters of the weapon's damage to all in it.
    ///   CHAINSAW  ("Punch out and stuck hand inside (leaving the other hand punching) ... if its small then its stuck
    ///             at contact point so no hitbox collide, chipp damage exponential to a ceiling"; asked after: out when
    ///             the enemy dies or gets out of reach, or left click is let go): stuck where it lands (NightcallSaw),
    ///             a little way in if what it hit is big, at the spot if small; it saws only that one, each cut a sixth
    ///             of a second after the last and bigger than it, to a ceiling. The other hand keeps punching.
    ///   LASER     ("punch out laser pierce 1"): as the blow goes out, a laser down its line (NightcallLaser), through
    ///             one enemy into a second.
    ///
    /// Right click ("standstill Charge for 2 sec and explode radius 7 tiles and leaving radiation trail for 5 sec, throwing
    /// player to the direction with least hostile npc and projectile"): NightcallCharge.
    ///
    /// Mine: its numbers past what was asked (damage, reach, speeds, the cooldown), all here.
    ///
    /// Only in a Legends world: from Skeletron Prime where it drops no bag, in its bag where it does (both
    /// <see cref="LegendsOnly"/>), every time. A "Legends" line under its tooltip, in its green and its red.
    /// </summary>
    public class Nightcall : FistWeapon
    {
        public const int Damage = 60;

        // ---- each hand's ------------------------------------------------------------------------------------------------

        /// <summary>The vice: times the weapon's damage; how long what it hits is held off (FistPlayer.HeldOff is 30); its guard's reach, and how long it stays up after.</summary>
        public const float ViceDamage = 2f;
        public const int ViceHeldOff = 90;
        public const float ViceGuard = 1.5f;
        public const int ViceGuardAfter = 30;

        /// <summary>The cannon's blast: how far it reaches, and its share of the weapon's damage.</summary>
        public const float CannonReach = 3f * 16f;
        public const float CannonDamage = 1.1f;

        /// <summary>The laser: its share of the weapon's damage, its speed an update (three a tick), and its updates.</summary>
        public const float LaserDamage = 0.8f;
        public const float LaserSpeed = 11f;
        public const int LaserLasts = 90;

        /// <summary>
        /// The saw: ticks between its cuts; its first cut's share of the weapon's damage, and each next one's times the last's,
        /// to a ceiling (by request: "chipp damage exponential to a ceiling"): from a tenth to six tenths in about a second
        /// and a half. How far from the shoulder it can be before it comes out; when what it hit is big (both sides this
        /// many pixels or more), how far into it it goes.
        /// </summary>
        public const int ChipEvery = 6;
        public const float ChipStart = 0.1f, ChipGrowth = 1.12f, ChipCeiling = 0.6f;
        public const float SawLetGo = 100f;
        public const float SawBig = 48f, SawDepth = 16f;

        /// <summary>The share of the weapon's damage a cut of the saw does, after so many.</summary>
        public static float ChipShare(float cuts) => Math.Min(ChipCeiling, ChipStart * MathF.Pow(ChipGrowth, Math.Max(0f, cuts)));

        // ---- its right click ----------------------------------------------------------------------------------------------

        /// <summary>
        /// The charge, its blast (by request: two seconds, seven tiles), the blast's share of the weapon's damage; the throw
        /// (its ticks, how far, the speed it ends at); the trail of radiation behind it (by request: five seconds), its
        /// share of the damage and how often; the cooldown.
        /// </summary>
        public const int ChargeTicks = 120;
        public const float BlastReach = 7f * 16f;
        public const float BlastDamage = 6f;
        public const int ThrowTicks = 24;
        public const float ThrowFar = 22f * 16f;
        public const float ThrowEnds = 6f;
        public const int TrailLasts = 5 * 60;
        public const float TrailDamage = 0.5f;
        public const int TrailEvery = 30;
        public const int Cooldown = 12 * 60;

        /// <summary>How fast the throw goes this many ticks into it: quick off the mark, down to a run, <see cref="ThrowFar"/> in all.</summary>
        public static float ThrowSpeed(int tick)
        {
            float mean = ThrowFar / ThrowTicks;
            float begins = 2f * mean - ThrowEnds;
            return MathHelper.Lerp(begins, ThrowEnds, MathHelper.Clamp(tick / (float)(ThrowTicks - 1), 0f, 1f));
        }

        // ---- the blows ------------------------------------------------------------------------------------------------------

        /// <summary>The left hand's (the front arm), then the right's (the back arm): two straight blows, turn about.</summary>
        private static readonly FistMove[] Moves =
        {
            new FistMove(FistArc.Straight, 14, 0.42f, 1f, 0.6f, reach: 0.95f, step: 8f),
            new FistMove(FistArc.Straight, 15, 0.45f, 1f, 0.8f, reach: 1f, step: 10f, backArm: true),
        };

        public override FistMove[] Combo => Moves;

        public override float Reach => 48f;

        public override int ComboWindow => 30;

        public static string FistPath(NightcallFist fist) => "PisAndDag/Content/Items/Melee/Nightcall" + fist;

        public override string FistTexture => FistPath(NightcallFist.Vice);

        public override string ArmTexture => "PisAndDag/Content/Items/Melee/NightcallArm";

        public override Color Reaching => PrimeArt.Nuclear;

        public override bool HasAltUse => true;

        /// <summary>Which hand a step of the combo is: 0 the left (the front arm), 1 the right (the back arm).</summary>
        public static NightcallFist FistOf(Player player, int step) => player.GetModPlayer<NightcallPlayer>().Of(step);

        /// <summary>Each one's colour as it strikes.</summary>
        public static Color Colour(NightcallFist fist) => fist switch
        {
            NightcallFist.Cannon => new Color(255, 168, 74),
            NightcallFist.Chainsaw => new Color(255, 214, 120),
            NightcallFist.Laser => PrimeArt.Nuclear,
            _ => new Color(214, 220, 232),
        };

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetFistDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            Item.damage = Damage;
            Item.knockBack = 4.5f;
            Item.crit = 6;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 8);
        }

        public override string FistTextureFor(Player player, int step) => FistPath(FistOf(player, step));

        public override Color ReachingFor(Player player, int step) => Colour(FistOf(player, step));

        public override float BlowDamage(Player player, int step) => FistOf(player, step) == NightcallFist.Vice ? ViceDamage : 1f;

        public override int HeldOffFor(Player player, int step) => FistOf(player, step) == NightcallFist.Vice ? ViceHeldOff : FistPlayer.HeldOff;

        public override float GuardFor(Player player, int step) => FistOf(player, step) == NightcallFist.Vice ? ViceGuard : 1f;

        public override int GuardAfter(Player player, int step) => FistOf(player, step) == NightcallFist.Vice ? ViceGuardAfter : 0;

        /// <summary>A hand whose saw is stuck in something cannot be thrown: the other throws instead.</summary>
        public override int StepFor(Player player, int next)
        {
            FistPlayer fists = player.GetModPlayer<FistPlayer>();

            if (next == 0 && fists.FrontHeld)
                return 1;

            if (next == 1 && fists.BackHeld)
                return 0;

            return next;
        }

        /// <summary>A blow ends when its arm is taken (its own saw has stuck).</summary>
        public override bool EndBlow(Player player, int step)
        {
            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            return step == 0 ? fists.FrontHeld : fists.BackHeld;
        }

        /// <summary>Not with both saws stuck, nor while it charges.</summary>
        public override bool CanUseItem(Player player)
        {
            FistPlayer fists = player.GetModPlayer<FistPlayer>();

            if (fists.FrontHeld && fists.BackHeld)
                return false;

            if (player.ownedProjectileCounts[ModContent.ProjectileType<NightcallCharge>()] > 0)
                return false;

            return base.CanUseItem(player);
        }

        public override SoundStyle Swing(FistMove move, int step) =>
            SoundID.DD2_MonkStaffSwing with { Volume = 0.55f, Pitch = step == 0 ? 0.1f : -0.1f, PitchVariance = 0.12f };

        /// <summary>What a laser fires as it goes out (the owner's to make); its flash, seen by all.</summary>
        public override void OnStrike(Player player, Projectile blow, FistMove move, int step, Vector2 fist, Vector2 direction)
        {
            if (FistOf(player, step) != NightcallFist.Laser)
                return;

            if (player.whoAmI == Main.myPlayer)
            {
                int damage = (int)Math.Round(player.GetWeaponDamage(player.HeldItem) * LaserDamage);
                Projectile.NewProjectile(player.GetSource_ItemUse(player.HeldItem), fist + direction * 8f, direction * LaserSpeed,
                    ModContent.ProjectileType<NightcallLaser>(), damage, 1f, player.whoAmI);
            }

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.6f, Pitch = 0.35f, PitchVariance = 0.1f, MaxInstances = 4 }, fist);
            Prime.Add(new BloomGeneric(fist + direction * 6f, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 0.35f, 8));

            for (int i = 0; i < 3; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(fist + direction * 6f, direction.RotatedByRandom(0.5f) * Main.rand.NextFloat(4f, 9f), PrimeArt.NuclearHot, 10f, 2.5f, 30f,
                    Main.rand.Next(8, 13)));
        }

        /// <summary>What each sheds as it goes: the saw its sparks, the laser its green, the cannon a breath of smoke.</summary>
        public override void Shed(Player player, FistMove move, int step, Vector2 hand, Vector2 reach, float extension, bool striking)
        {
            if (Main.dedServ || extension <= 0.2f)
                return;

            switch (FistOf(player, step))
            {
                case NightcallFist.Chainsaw:
                    if (Main.rand.NextBool(2))
                        PADParticleHandler.SpawnParticle(new SparkShard(reach, Main.rand.NextVector2Circular(3f, 3f) + new Vector2(0f, -1f), new Color(255, 200, 110), 8f, 2f, 22f,
                            Main.rand.Next(6, 11), falls: true));
                    break;

                case NightcallFist.Laser:
                    if (Main.rand.NextBool(3))
                        Prime.Add(new HoneyMote(reach + Main.rand.NextVector2Circular(4f, 4f), Main.rand.NextVector2Circular(0.6f, 0.6f), PrimeArt.Nuclear, 0.4f, Main.rand.Next(12, 20), 0.01f));
                    break;

                case NightcallFist.Cannon:
                    if (striking && Main.rand.NextBool(3))
                        Prime.Add(new MuzzleSmoke(reach, Main.rand.NextVector2Circular(0.6f, 0.6f) + new Vector2(0f, -0.4f), Main.rand.Next(18, 28), 0.22f));
                    break;
            }
        }

        /// <summary>On the client that threw it: the cannon's blast, the saw stuck, the vice's clang.</summary>
        public override void OnFistHit(Player player, NPC target, FistMove move, int step, NPC.HitInfo hit, Vector2 at)
        {
            IEntitySource source = player.GetSource_ItemUse(player.HeldItem);
            int damage = player.GetWeaponDamage(player.HeldItem);

            switch (FistOf(player, step))
            {
                case NightcallFist.Cannon:
                    Projectile.NewProjectile(source, at, Vector2.Zero, ModContent.ProjectileType<NightcallBlast>(), (int)Math.Round(damage * CannonDamage),
                        player.GetWeaponKnockback(player.HeldItem, 3f), player.whoAmI, CannonReach, NightcallBlast.Cannon);
                    break;

                case NightcallFist.Chainsaw:
                    Stick(player, target, step, at, damage, source);
                    break;

                case NightcallFist.Vice:
                    if (Main.dedServ)
                        break;

                    SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.35f, PitchVariance = 0.1f, MaxInstances = 4 }, at);

                    for (int i = 0; i < 4; i++)
                        PADParticleHandler.SpawnParticle(new SparkShard(at, new Vector2(player.direction * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-3f, 1f)), Color.White, 8f, 2f, 24f,
                            Main.rand.Next(6, 10)));
                    break;
            }
        }

        /// <summary>
        /// The saw, stuck in what it hit (by request: "if its small then its stuck at contact point"): a little way in, toward
        /// its middle, if it is big; at the spot where it landed if it is small. Not a second time in the same hand.
        /// </summary>
        private static void Stick(Player player, NPC target, int step, Vector2 at, int damage, IEntitySource source)
        {
            if (!target.active || target.friendly || target.dontTakeDamage || target.life <= 0)
                return;

            FistPlayer fists = player.GetModPlayer<FistPlayer>();

            if (step == 0 ? fists.FrontHeld : fists.BackHeld)
                return;

            // One blow can land on several at once: the first it lands on has it.
            int sawType = ModContent.ProjectileType<NightcallSaw>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == sawType && (int)p.ai[1] == step)
                    return;
            }

            Vector2 stuck = at - target.Center;

            if (target.width >= SawBig && target.height >= SawBig)
            {
                Vector2 inward = (target.Center - at).SafeNormalize(Vector2.Zero);
                stuck += inward * Math.Min(SawDepth, Math.Min(target.width, target.height) * 0.25f);
            }

            Projectile.NewProjectile(source, target.Center + stuck, stuck, ModContent.ProjectileType<NightcallSaw>(), damage, 0f, player.whoAmI, target.whoAmI, step);
        }

        // ---- its right click ------------------------------------------------------------------------------------------------

        public override bool CanAltUse(Player player) =>
            !player.HasBuff(ModContent.BuffType<NightcallSpent>()) && player.ownedProjectileCounts[ModContent.ProjectileType<NightcallCharge>()] == 0;

        /// <summary>The charge: the blow in hand dropped, the saws pulled out, the player planted.</summary>
        public override bool AltUse(Player player, IEntitySource source)
        {
            int punch = ModContent.ProjectileType<FistPunch>(), saw = ModContent.ProjectileType<NightcallSaw>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && (p.type == punch || p.type == saw))
                    p.Kill();
            }

            int damage = player.GetWeaponDamage(player.HeldItem);
            float knockback = player.GetWeaponKnockback(player.HeldItem, 9f);
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<NightcallCharge>(), damage, knockback, player.whoAmI);
            NightcallMenu.Close();
            return true;
        }

        // ---- its tooltip ----------------------------------------------------------------------------------------------------

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            NightcallPlayer hands = Main.LocalPlayer.GetModPlayer<NightcallPlayer>();
            int at = tooltips.FindIndex(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));

            if (at < 0)
                at = tooltips.Count;

            string left = Language.GetTextValue("Mods.PisAndDag.Nightcall." + hands.Left);
            string right = Language.GetTextValue("Mods.PisAndDag.Nightcall." + hands.Right);

            tooltips.Insert(at, new TooltipLine(Mod, "NightcallHands", Language.GetTextValue("Mods.PisAndDag.Nightcall.Hands", left, right))
            {
                OverrideColor = Color.Lerp(PrimeArt.Nuclear, Color.White, 0.35f),
            });

            tooltips.Insert(at + 1, new TooltipLine(Mod, "NightcallKey", Language.GetTextValue("Mods.PisAndDag.Nightcall.Choose", TentochiItem.KeyName(PADKeybinds.Reload))));

            // Its green and its red, turn about.
            float turn = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f);

            tooltips.Add(new TooltipLine(Mod, "Legends", Language.GetTextValue("Mods.PisAndDag.Conditions.LegendsTag"))
            {
                OverrideColor = Color.Lerp(PrimeArt.Nuclear, PrimeArt.Red, turn),
            });
        }
    }

    /// <summary>Nightcall from Skeletron Prime itself, where it drops no bag (Classic), in a Legends world.</summary>
    public class NightcallDrop : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronPrime;

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            LeadingConditionRule classic = new LeadingConditionRule(new Conditions.NotExpert());
            classic.OnSuccess(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<Nightcall>()));
            npcLoot.Add(classic);
        }
    }

    /// <summary>Nightcall in Skeletron Prime's bag (Expert and Master), in a Legends world.</summary>
    public class NightcallBag : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.SkeletronPrimeBossBag;

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            itemLoot.Add(ItemDropRule.ByCondition(new LegendsOnly(), ModContent.ItemType<Nightcall>()));
        }
    }
}
