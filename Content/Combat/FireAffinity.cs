using System;
using System.Collections.Generic;
using PisAndDag.Content.Projectiles.Mage;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// What counts as fire: what Rage! Sweet Fire makes burn hotter (SweetFire).
    ///
    /// Fire is what burns. Vanilla sets a thing On Fire!, Hellfire or Daybreak in two places,
    /// and both were read in the 1.4.4 IL: Projectile.StatusNPC, by projectile type (and the
    /// Flask of Fire and the Magma Stone for melee), and Player.StatusToNPC, by item type for a
    /// swing (and the same two). Everything that does is here. So is the rest of the sun's
    /// weapons and tools, which do not all burn, and the Hellfire Arrow, which only explodes.
    /// Cursed Inferno and Shadowflame are not fire here (the user's call: dark), and neither is
    /// Frostburn, except that both flamethrowers count, the Elf Melter's cold flame included.
    ///
    /// OTHER MODS. Their fire is learned as it is used (FireAffinityPlayer): the first time one
    /// of their projectiles or a swing of one of their items sets what a player hits on fire, its
    /// type is fire from then on, for the rest of the session. (Vanilla's are exactly the lists
    /// here, and are never learned.) "On fire" is any of <see cref="Debuffs"/>: vanilla's
    /// three, and every other mod's debuff whose name says fire (<see cref="NamedFire"/>), which
    /// is found at load. And a mod can say so itself through Mod.Call (<see cref="Call"/>).
    ///
    /// Only the player's own: never an NPC's or a trap's projectile.
    /// </summary>
    public static class FireAffinity
    {
        /// <summary>Projectiles that are fire whoever throws them.</summary>
        public static readonly HashSet<int> Projectiles = new()
        {
            // burning, by StatusNPC
            ProjectileID.FireArrow, ProjectileID.BallofFire, ProjectileID.Flamarang, ProjectileID.Flamelash,
            ProjectileID.Sunfury, ProjectileID.Flames, ProjectileID.Flare, ProjectileID.BlueFlare,
            ProjectileID.SpelunkerFlare, ProjectileID.RainbowFlare, ProjectileID.ShimmerFlare,
            ProjectileID.InfernoFriendlyBolt, ProjectileID.InfernoFriendlyBlast, ProjectileID.ImpFireball,
            ProjectileID.MolotovCocktail, ProjectileID.MolotovFire, ProjectileID.MolotovFire2, ProjectileID.MolotovFire3,
            ProjectileID.Spark, ProjectileID.WandOfSparkingSpark, ProjectileID.Cascade, ProjectileID.HelFire,
            ProjectileID.DD2FlameBurstTowerT1Shot, ProjectileID.DD2FlameBurstTowerT2Shot, ProjectileID.DD2FlameBurstTowerT3Shot,
            ProjectileID.DD2PhoenixBowShot, ProjectileID.FireWhip, ProjectileID.FlamingMace, ProjectileID.Volcano,

            // the sun's
            ProjectileID.SolarWhipSword, ProjectileID.SolarWhipSwordExplosion, ProjectileID.Daybreak,
            ProjectileID.DaybreakExplosion, ProjectileID.SolarFlareChainsaw, ProjectileID.SolarFlareDrill,
            ProjectileID.SolarCounter,

            // fire in all but the burn
            ProjectileID.HellfireArrow,
        };

        /// <summary>Items whose swing is fire.</summary>
        public static readonly HashSet<int> Items = new()
        {
            // burning, by StatusToNPC
            ItemID.FieryGreatsword, ItemID.MoltenPickaxe, ItemID.MoltenHamaxe, ItemID.DD2SquireDemonSword, ItemID.WaffleIron,

            // the sun's
            ItemID.SolarFlareAxe, ItemID.SolarFlareHammer, ItemID.SolarFlarePickaxe, ItemID.LunarHamaxeSolar,
        };

        /// <summary>What "set on fire" means when a hit is watched: On Fire!, Hellfire, Daybreak, and other mods' fire.</summary>
        public static readonly HashSet<int> Debuffs = new()
        {
            BuffID.OnFire, BuffID.OnFire3, BuffID.Daybreak,
        };

        /// <summary>The Flask of Fire's imbue (Player.meleeEnchant).</summary>
        private const byte FlaskOfFire = 3;

        /// <summary>
        /// Once everything is loaded: the mod's own fire, and every other mod's debuff whose name
        /// says fire. Returns those debuffs' names, for the log.
        /// </summary>
        public static List<string> AddModded()
        {
            Projectiles.Add(ModContent.ProjectileType<MeagreFlameBall>());

            List<string> found = new();

            for (int type = BuffID.Count; type < BuffLoader.BuffCount; type++)
            {
                ModBuff buff = BuffLoader.GetBuff(type);

                if (buff == null || !Main.debuff[type] || !NamedFire(buff.Name))
                    continue;

                Debuffs.Add(type);
                found.Add($"{buff.Mod.Name}/{buff.Name}");
            }

            return found;
        }

        // ---- other mods' debuffs, by name -------------------------------------------------------

        /// <summary>A word that starts like fire: Fire, Flames, Burning, Blaze, Inferno, Embers, Scorched, Ignited, Magma, Lava, Pyro, Incinerate.</summary>
        private static readonly string[] Starts = { "fire", "flame", "burn", "blaz", "inferno", "ember", "scorch", "ignit", "magma", "lava", "pyro", "incinerat" };

        /// <summary>A word that ends like fire: Dragonfire, Hellfire, Soulflame, Sunburn.</summary>
        private static readonly string[] Ends = { "fire", "flame", "flames", "burn", "blaze" };

        /// <summary>A word that makes it not fire here: cold fire, and the dark kinds.</summary>
        private static readonly string[] Not = { "frost", "ice", "cold", "chill", "freez", "frozen", "cryo", "snow", "glacial", "curse", "shadow", "dark", "void" };

        /// <summary>
        /// Whether a debuff's internal name says fire: one of its words (split at capitals, digits
        /// and underscores) starts or ends like fire, and none is cold or dark.
        /// </summary>
        public static bool NamedFire(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            bool fire = false;

            foreach (string word in Words(name))
            {
                foreach (string not in Not)
                {
                    if (word.StartsWith(not, StringComparison.Ordinal))
                        return false;
                }

                foreach (string start in Starts)
                    fire |= word.StartsWith(start, StringComparison.Ordinal);

                foreach (string end in Ends)
                    fire |= word.EndsWith(end, StringComparison.Ordinal);
            }

            return fire;
        }

        /// <summary>A PascalCase name in lower-case words: BrimstoneFlames is brimstone, flames.</summary>
        private static List<string> Words(string name)
        {
            List<string> words = new();
            System.Text.StringBuilder word = new();

            void Cut()
            {
                if (word.Length > 0)
                    words.Add(word.ToString());

                word.Clear();
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (!char.IsLetter(c))
                {
                    Cut();
                    continue;
                }

                // A capital after a small letter starts a new word.
                if (char.IsUpper(c) && i > 0 && char.IsLower(name[i - 1]))
                    Cut();

                word.Append(char.ToLowerInvariant(c));
            }

            Cut();
            return words;
        }

        // ---- is it fire -------------------------------------------------------------------------

        /// <summary>A projectile of the player's that the Magma Stone or the Flask of Fire sets alight, exactly as vanilla does.</summary>
        private static bool Enchanted(Projectile projectile, Player owner)
        {
            if (projectile.noEnchantments)
                return false;

            bool melee = projectile.CountsAsClass(DamageClass.Melee);

            return (melee && owner.magmaStone)
                || ((melee || ProjectileID.Sets.IsAWhip[projectile.type]) && owner.meleeEnchant == FlaskOfFire);
        }

        /// <summary>A swing the Magma Stone or the Flask of Fire sets alight: any, as vanilla does.</summary>
        private static bool Enchanted(Player owner) => owner.magmaStone || owner.meleeEnchant == FlaskOfFire;

        /// <summary>A projectile of the player's: fire by its type (known or learned), or set alight by the player's Magma Stone or Flask of Fire.</summary>
        public static bool Is(Projectile projectile, Player owner)
        {
            if (projectile.npcProj || projectile.trap)
                return false;

            return Projectiles.Contains(projectile.type) || Enchanted(projectile, owner);
        }

        /// <summary>A swing of the player's: fire by the item (known or learned), or by the Magma Stone or the Flask of Fire.</summary>
        public static bool Is(Item item, Player owner) => Items.Contains(item.type) || Enchanted(owner);

        // ---- learning -----------------------------------------------------------------------------

        /// <summary>
        /// A projectile whose hit is worth watching: another mod's (vanilla's are exactly the list
        /// above), not yet known, the player's own, and nothing of the player's that could be the
        /// burn instead.
        /// </summary>
        public static bool Unknown(Projectile projectile, Player owner) =>
            projectile.ModProjectile != null && !projectile.npcProj && !projectile.trap
            && !Projectiles.Contains(projectile.type) && !Enchanted(projectile, owner);

        /// <summary>A swing worth watching: another mod's item, not yet known, and no Magma Stone or Flask of Fire on the player.</summary>
        public static bool Unknown(Item item, Player owner) =>
            item.ModItem != null && !Items.Contains(item.type) && !Enchanted(owner);

        /// <summary>How much fire is on it: every fire debuff's ticks, added up. A hit that sets it on fire raises this.</summary>
        public static int Burn(NPC npc)
        {
            int ticks = 0;

            for (int i = 0; i < NPC.maxBuffs; i++)
            {
                if (npc.buffTime[i] > 0 && Debuffs.Contains(npc.buffType[i]))
                    ticks += npc.buffTime[i];
            }

            return ticks;
        }

        // ---- other mods, saying so --------------------------------------------------------------

        /// <summary>
        /// Mod.Call: ("FireProjectile", type), ("FireItem", type) or ("FireDebuff", type). That
        /// projectile, the swing of that item, or that debuff counts as fire. True when taken.
        /// </summary>
        public static object Call(object[] args)
        {
            if (args == null || args.Length < 2 || args[0] is not string what)
                return false;

            int type;

            try
            {
                type = Convert.ToInt32(args[1]);
            }
            catch (Exception)
            {
                return false;
            }

            switch (what.ToLowerInvariant())
            {
                case "fireprojectile":
                    Projectiles.Add(type);
                    return true;

                case "fireitem":
                    Items.Add(type);
                    return true;

                case "firedebuff":
                    Debuffs.Add(type);
                    return true;

                default:
                    return false;
            }
        }
    }

    public class FireAffinitySystem : ModSystem
    {
        public override void PostSetupContent()
        {
            List<string> found = FireAffinity.AddModded();

            if (found.Count > 0)
                Mod.Logger.Info("Sweet Fire counts these debuffs of other mods as fire: " + string.Join(", ", found));
        }
    }

    /// <summary>
    /// Learns other mods' fire as the player uses it. Just before a hit of theirs lands, if what
    /// is hitting is not yet known, how much fire is on the target is noted; just after (these
    /// hooks run after the projectile's and the item's own OnHitNPC, where a mod sets things
    /// alight), if there is more, the projectile or the item is fire from then on. Only on the
    /// machine of the player whose hit it is, which is where the hit is worked out.
    /// </summary>
    public class FireAffinityPlayer : ModPlayer
    {
        /// <summary>The target of the hit being watched, or -1; and how much fire it had before.</summary>
        private int watching = -1;
        private int before;

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
        {
            watching = FireAffinity.Unknown(proj, Player) ? target.whoAmI : -1;
            before = watching >= 0 ? FireAffinity.Burn(target) : 0;
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (watching == target.whoAmI && FireAffinity.Burn(target) > before && FireAffinity.Projectiles.Add(proj.type))
                Mod.Logger.Info($"Sweet Fire: {Lang.GetProjectileName(proj.type).Value} ({proj.type}) sets things alight, so it counts as fire.");

            watching = -1;
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
        {
            watching = FireAffinity.Unknown(item, Player) ? target.whoAmI : -1;
            before = watching >= 0 ? FireAffinity.Burn(target) : 0;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (watching == target.whoAmI && FireAffinity.Burn(target) > before && FireAffinity.Items.Add(item.type))
                Mod.Logger.Info($"Sweet Fire: {Lang.GetItemNameValue(item.type)} ({item.type}) sets things alight, so its swing counts as fire.");

            watching = -1;
        }
    }
}
