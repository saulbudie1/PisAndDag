using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// The Super Smash mod (BattleRoyaleMod, by Volknet_Online) sets creatures fighting each other: marked onto sides, each
    /// one's turn has this machine's player stood where its enemy is (TargetSelectNPC / TargetSelectProj), untouchable, so
    /// that whatever goes for a player goes for that enemy; and it turns what would have struck the player, a body's touch
    /// (npc.damage) or a hurting projectile's (Projectile.Damage), onto the enemy instead.
    ///
    /// Our bosses' blows are neither: Foigh's sword and hook, Dona Quinn's lance, her donkey, the Gilded Knight's blade and
    /// shots, Amalgaermis' steam each look for this machine's player themselves and hurt them (Player.Hurt). Here, so they
    /// strike under that battle too (by request: "only inflict under battle of that mod"): such a blow, landing while the
    /// player is stood in for an enemy, lands on that enemy instead, and the player is left untouched, as the battle has
    /// them. Its rules kept: the side's grace on the one struck (NPCImmuneSystem.ImmuneForEvE), its damage settings
    /// (MeleeDamageModifier for a body's blow, ProjDamageModifier for a thrown thing's, and the one set for that creature).
    ///
    /// Read from the mod by name, not referenced: nothing of this runs, or is looked for, without it, and anything of it
    /// that is not where it was (another version) leaves every blow as it was. Single player only, as the mod is.
    /// </summary>
    public class BattleRoyaleSupport : ModSystem
    {
        /// <summary>
        /// A creature of ours that can turn a blow aside in the battle (Foigh): asked before one of our creatures' blows lands
        /// on it, with the striker's side, whether the blow is a thrown thing's, and where it came from. True: turned, it
        /// does not land (and that side waits <see cref="Grace"/> ticks before it can try again).
        /// </summary>
        public interface IGuard
        {
            bool Turns(int side, bool thrown, Vector2 from);
        }

        /// <summary>Ticks a side's blow leaves the one it struck before that side can strike them again.</summary>
        public const int Grace = 10;

        /// <summary>How near the player stands to the enemy's middle when stood in for them (they are put exactly there).</summary>
        private const float Standing = 4f;

        private static bool ready;

        private static FieldInfo battleStart;      // BattleRoyaleMod.BattleStart
        private static FieldInfo config;           // BattleRoyaleMod.Gconfig
        private static FieldInfo meleeModifier;    // CustomConfig.MeleeDamageModifier
        private static FieldInfo projModifier;     // CustomConfig.ProjDamageModifier
        private static FieldInfo currentSource;    // SourceMarkerSystem.CurrentSource
        private static FieldInfo currentFraction;  // SourceMarkerSystem.CurrentFraction
        private static FieldInfo npcStandIn;       // TargetSelectNPC.OldPos
        private static FieldInfo projStandIn;      // TargetSelectProj.OldPos
        private static FieldInfo fraction;         // SourceMarkNPC.Fraction
        private static FieldInfo shotFraction;     // SourceMarkProj.Fraction
        private static FieldInfo shotSource;       // SourceMarkProj.Source
        private static FieldInfo immune;           // NPCImmuneSystem.ImmuneForEvE
        private static MethodInfo creatureModifier; // ConfigDataUtils.GetDamageModifier(NPC)
        private static GlobalNPC marks;
        private static GlobalNPC graces;
        private static GlobalProjectile shotMarks;

        public override void PostSetupContent()
        {
            ready = false;

            if (!ModLoader.TryGetMod("BattleRoyaleMod", out Mod mod) || mod.Code == null)
                return;

            try
            {
                Assembly code = mod.Code;
                const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

                Type main = code.GetType("BattleRoyaleMod.BattleRoyaleMod");
                Type custom = code.GetType("BattleRoyaleMod.CustomConfig");
                Type markers = code.GetType("BattleRoyaleMod.SourceMarkerSystem");
                Type npcSelect = code.GetType("BattleRoyaleMod.System.TargetSelectNPC");
                Type projSelect = code.GetType("BattleRoyaleMod.System.TargetSelectProj");
                Type mark = code.GetType("BattleRoyaleMod.SourceMarkNPC");
                Type shotMark = code.GetType("BattleRoyaleMod.SourceMarkProj");
                Type grace = code.GetType("BattleRoyaleMod.System.NPCImmuneSystem");
                Type utils = code.GetType("BattleRoyaleMod.ConfigDataUtils");

                battleStart = main?.GetField("BattleStart", All);
                config = main?.GetField("Gconfig", All);
                meleeModifier = custom?.GetField("MeleeDamageModifier", All);
                projModifier = custom?.GetField("ProjDamageModifier", All);
                currentSource = markers?.GetField("CurrentSource", All);
                currentFraction = markers?.GetField("CurrentFraction", All);
                npcStandIn = npcSelect?.GetField("OldPos", All);
                projStandIn = projSelect?.GetField("OldPos", All);
                fraction = mark?.GetField("Fraction", All);
                shotFraction = shotMark?.GetField("Fraction", All);
                shotSource = shotMark?.GetField("Source", All);
                immune = grace?.GetField("ImmuneForEvE", All);
                creatureModifier = utils?.GetMethod("GetDamageModifier", All, null, new[] { typeof(NPC) }, null);

                marks = Template<GlobalNPC>(mod, mark);
                graces = Template<GlobalNPC>(mod, grace);

                // A shot's side and maker (Foigh.Smash); without them, shots are only not answered.
                shotMarks = shotFraction?.FieldType == typeof(int) && shotSource?.FieldType == typeof(int)
                    ? Template<GlobalProjectile>(mod, shotMark) : null;

                // What every blow needs; the damage settings are taken as 1 where they are not found.
                ready = battleStart?.FieldType == typeof(bool)
                    && currentFraction?.FieldType == typeof(int)
                    && npcStandIn?.FieldType == typeof(Vector2?) && projStandIn?.FieldType == typeof(Vector2?)
                    && fraction?.FieldType == typeof(int)
                    && marks != null;
            }
            catch (Exception e)
            {
                Mod.Logger.Warn("Super Smash support left off: " + e.Message);
                ready = false;
            }
        }

        public override void Unload()
        {
            ready = false;
            battleStart = config = meleeModifier = projModifier = currentSource = currentFraction = null;
            npcStandIn = projStandIn = fraction = immune = shotFraction = shotSource = null;
            creatureModifier = null;
            marks = graces = null;
            shotMarks = null;
        }

        private static T Template<T>(Mod mod, Type type) where T : class
        {
            if (type == null)
                return null;

            foreach (ILoadable content in mod.GetContent())
            {
                if (content is T global && content.GetType() == type)
                    return global;
            }

            return null;
        }

        /// <summary>Whether the battle is on.</summary>
        public static bool Battle => ready && (bool)battleStart.GetValue(null);

        /// <summary>The side an NPC is marked onto, or -1.</summary>
        public static int Side(NPC npc)
        {
            if (!ready || npc == null || !npc.TryGetGlobalNPC(marks, out GlobalNPC mark))
                return -1;

            return (int)fraction.GetValue(mark);
        }

        /// <summary>The side a shot is on (the side of whoever made it), or -1.</summary>
        public static int Side(Projectile projectile)
        {
            if (!ready || shotMarks == null || projectile == null || !projectile.TryGetGlobalProjectile(shotMarks, out GlobalProjectile mark))
                return -1;

            return (int)shotFraction.GetValue(mark);
        }

        /// <summary>The creature that made a shot, while it lives; or null.</summary>
        public static NPC Maker(Projectile projectile)
        {
            if (!ready || shotMarks == null || projectile == null || !projectile.TryGetGlobalProjectile(shotMarks, out GlobalProjectile mark))
                return null;

            int index = (int)shotSource.GetValue(mark);
            return index >= 0 && index < Main.maxNPCs && Main.npc[index].active ? Main.npc[index] : null;
        }

        /// <summary>Gives an NPC the battle's grace from one side, this many ticks at least: that side's touches and blows wait it out.</summary>
        public static void Shield(NPC npc, int side, int ticks)
        {
            int[] grace = Graces(npc);

            if (grace != null && side > 0 && side - 1 < grace.Length && grace[side - 1] < ticks)
                grace[side - 1] = ticks;
        }

        /// <summary>
        /// The enemy this machine's player is stood in for right now, while one of a side's creatures or shots takes its
        /// turn in the battle: or null, the player being only themselves.
        /// </summary>
        public static NPC StandIn(Player me)
        {
            if (!Battle || Main.netMode != NetmodeID.SinglePlayer || me == null || !ReferenceEquals(me, Main.LocalPlayer))
                return null;

            if (npcStandIn.GetValue(null) == null && projStandIn.GetValue(null) == null)
                return null;

            int side = (int)currentFraction.GetValue(null);

            if (side <= 0)
                return null;

            NPC found = null;
            float nearest = Standing;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                int theirs = Side(npc);

                if (theirs <= 0 || theirs == side)
                    continue;

                float distance = Vector2.Distance(npc.Center, me.Center);

                if (distance <= nearest)
                {
                    found = npc;
                    nearest = distance;
                }
            }

            return found;
        }

        /// <summary>The creature the last blow through <see cref="Hurt"/> landed on instead of the player; null if it landed on none.</summary>
        public static NPC Struck { get; private set; }

        /// <summary>
        /// Player.Hurt for our creatures' blows: the same, except that while the battle stands the player in for an enemy,
        /// the blow lands on that enemy (<paramref name="onCreatures"/> times as hard: Foigh's twice; and, with
        /// <paramref name="pastReduction"/>, past whatever makes it take less of every hit: Foigh's) and the player is not
        /// hurt (0, as the battle has it). What it landed on is <see cref="Struck"/> until the next blow.
        /// </summary>
        public static double Hurt(Player me, PlayerDeathReason reason, int damage, int direction, bool pvp = false, bool quiet = false,
            int cooldownCounter = -1, bool dodgeable = true, float armorPenetration = 0f, float scalingArmorPenetration = 0f, float knockback = 4.5f,
            float onCreatures = 1f, bool pastReduction = false)
        {
            Struck = null;
            NPC enemy = StandIn(me);

            if (enemy == null)
                return me.Hurt(reason, damage, direction, pvp, quiet, cooldownCounter, dodgeable, armorPenetration, scalingArmorPenetration, knockback);

            if (Strike(enemy, damage * onCreatures, direction, armorPenetration, scalingArmorPenetration, knockback, pastReduction))
                Struck = enemy;

            return 0.0;
        }

        /// <summary>The blow on the enemy: through their defence as it would have gone through the player's, then their side's grace. True if it landed.</summary>
        private static bool Strike(NPC enemy, float damage, int direction, float armorPenetration, float scalingArmorPenetration, float knockback, bool pastReduction)
        {
            int side = (int)currentFraction.GetValue(null);

            if (enemy.dontTakeDamage || enemy.dontTakeDamageFromHostiles || enemy.immortal)
                return false;

            int[] grace = Graces(enemy);

            if (grace != null && side - 1 < grace.Length && grace[side - 1] > 0)
                return false;

            // Turned aside by one that can (Foigh): it does not land.
            if (enemy.ModNPC is IGuard guard && guard.Turns(side, projStandIn.GetValue(null) != null, From(enemy, direction)))
            {
                Shield(enemy, side, Grace);
                return false;
            }

            NPC.HitModifiers modifiers = enemy.GetIncomingStrikeModifiers(DamageClass.Default, direction == 0 ? 1 : direction);
            modifiers.ArmorPenetration += armorPenetration;
            modifiers.ScalingArmorPenetration += scalingArmorPenetration;

            if (pastReduction)
                Unreduced(ref modifiers);

            NPC.HitInfo hit = modifiers.ToHitInfo(damage * Modifier(), false, knockback);
            enemy.StrikeNPC(hit, false, true);
            enemy.netUpdate = true;

            if (grace != null && side - 1 < grace.Length)
                grace[side - 1] = Grace;

            return true;
        }

        /// <summary>
        /// Every creature of another side than the one whose turn it is, within <paramref name="reach"/> of a point (to the
        /// nearest of it), struck once now, whatever grace they had, through all their defence (Foigh's crowded plunge);
        /// a many-part one once, for all its parts. Not one that cannot be hurt now. The battle's damage settings as for
        /// any blow; and the side's grace given. The ones struck.
        /// </summary>
        public static List<NPC> StrikeAround(Vector2 at, float reach, float damage, float knockback)
        {
            List<NPC> struck = new List<NPC>();
            int side = ready ? (int)currentFraction.GetValue(null) : -1;

            if (!Battle || side <= 0 || Main.netMode != NetmodeID.SinglePlayer)
                return struck;

            HashSet<int> wholes = new HashSet<int>();
            float modifier = Modifier();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                int theirs = Side(npc);

                if (theirs <= 0 || theirs == side || npc.dontTakeDamage || npc.dontTakeDamageFromHostiles || npc.immortal)
                    continue;

                if (Vector2.Distance(Vector2.Clamp(at, npc.TopLeft, npc.BottomRight), at) > reach)
                    continue;

                if (!wholes.Add(npc.realLife >= 0 ? npc.realLife : npc.whoAmI))
                    continue;

                int direction = npc.Center.X >= at.X ? 1 : -1;
                NPC.HitModifiers modifiers = npc.GetIncomingStrikeModifiers(DamageClass.Default, direction);
                modifiers.ScalingArmorPenetration += 1f;
                Unreduced(ref modifiers);

                npc.StrikeNPC(modifiers.ToHitInfo(damage * modifier, false, knockback), false, true);
                npc.netUpdate = true;
                Shield(npc, side, Grace);
                struck.Add(npc);
            }

            return struck;
        }

        /// <summary>
        /// A blow past whatever makes the one it lands on take less of every hit (by request: "make foighs attack bypass
        /// calamity damage reduction", the Devourer of Gods' body taking 7.5% of it): everything that has made the final
        /// damage less is undone (CalamityGlobalNPC.ApplyDR, and the game's own takenDamageMultiplier under 1, both work on
        /// it), and what makes it more is kept. A cap on one hit (NPC.HitModifiers.SetMaxDamage) is kept.
        /// </summary>
        private static void Unreduced(ref NPC.HitModifiers modifiers)
        {
            StatModifier final = modifiers.FinalDamage;

            modifiers.FinalDamage = new StatModifier(Math.Max(1f, final.Additive), Math.Max(1f, final.Multiplicative),
                Math.Max(0f, final.Flat), Math.Max(0f, final.Base));
        }

        /// <summary>Where the blow came from: the creature whose turn it is (or whose shot's), or the side it struck from.</summary>
        private static Vector2 From(NPC enemy, int direction)
        {
            int source = currentSource?.FieldType == typeof(int) ? (int)currentSource.GetValue(null) : -1;

            if (source >= 0 && source < Main.maxNPCs && Main.npc[source].active && source != enemy.whoAmI)
                return Main.npc[source].Center;

            return enemy.Center - new Vector2((direction == 0 ? 1 : direction) * (enemy.width * 0.5f + 16f), 0f);
        }

        /// <summary>The battle's damage settings for this blow: a body's or a thrown thing's, and the striker's own.</summary>
        private static float Modifier()
        {
            float modifier = 1f;
            object settings = config?.GetValue(null);
            FieldInfo kind = projStandIn.GetValue(null) != null ? projModifier : meleeModifier;

            if (settings != null && kind?.FieldType == typeof(int))
                modifier *= Math.Max(1, (int)kind.GetValue(settings));

            if (settings != null && creatureModifier != null && currentSource?.FieldType == typeof(int))
            {
                int source = (int)currentSource.GetValue(null);

                if (source >= 0 && source < Main.maxNPCs && Main.npc[source].active && creatureModifier.Invoke(null, new object[] { Main.npc[source] }) is float own)
                    modifier *= own;
            }

            return modifier;
        }

        /// <summary>The enemy's grace from each side, as the battle keeps it; null if it keeps none here.</summary>
        private static int[] Graces(NPC npc)
        {
            if (graces == null || immune == null || !npc.TryGetGlobalNPC(graces, out GlobalNPC global))
                return null;

            return immune.GetValue(global) as int[];
        }
    }
}
