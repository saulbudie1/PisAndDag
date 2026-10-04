using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Development helper for the parry, which classes every enemy by its own numbers
    /// (ParryRules): this says what it made of one, and why, so a wrong answer about some
    /// other mod's enemy can be seen and put right in the config (ParryConfig).
    ///
    ///   /parry        the enemy nearest the cursor, as it is this moment: what a parry does to
    ///                 it, and why
    ///   /parry list   every kind of enemy that hurts by touch, and every shot the rules take
    ///                 for solid, written to the log (client.log), a line each
    ///
    /// A Chat command: each machine's own, since it reads that machine's cursor.
    /// </summary>
    public class ParryCommand : ModCommand
    {
        private static readonly Color Said = new Color(214, 226, 240);

        /// <summary>How far from the cursor an enemy is still the one meant, in pixels.</summary>
        private const float Near = 240f;

        public override CommandType Type => CommandType.Chat;

        public override string Command => "parry";

        private const string Use = "/parry [list]";

        public override string Usage => Use;

        public override string Description => "The parry: what it does to the enemy nearest the cursor and why, or every kind of enemy written to the log.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string what = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            switch (what)
            {
                case "list":
                    caller.Reply(List(), Said);
                    return;

                case "":
                    caller.Reply(UnderCursor(), Said);
                    return;

                default:
                    caller.Reply("Usage: " + Use, Color.OrangeRed);
                    return;
            }
        }

        /// <summary>The enemy nearest the cursor, as it is now.</summary>
        private static string UnderCursor()
        {
            Vector2 cursor = Main.MouseWorld;
            NPC nearest = null;
            float best = Near * Near;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.friendly && npc.damage <= 0)
                    continue;

                Vector2 at = Vector2.Clamp(cursor, npc.TopLeft, npc.BottomRight);
                float far = Vector2.DistanceSquared(at, cursor);

                if (far < best)
                {
                    best = far;
                    nearest = npc;
                }
            }

            if (nearest == null)
                return "No enemy within " + (int)Near + " pixels of the cursor. Usage: " + Use;

            ParryClass kind = Parry.Of(nearest, out ParryWhy why);

            return $"{nearest.GivenOrTypeName} ({Numbers(nearest)}): {Says(nearest.type, kind, why, PADStaggerNPC.CanReel(nearest), Parry.StaggersEvery(nearest))}";
        }

        /// <summary>Every kind, to the log; the counts, to the chat.</summary>
        private static string List()
        {
            Mod mod = ModContent.GetInstance<ParrySystem>().Mod;
            List<string> lines = new List<string>();
            int light = 0, heavy = 0, giant = 0, never = 0;

            foreach (KeyValuePair<int, NPC> pair in ContentSamples.NpcsByNetId)
            {
                NPC sample = pair.Value;

                if (pair.Key <= 0 || sample == null || sample.friendly || (sample.damage <= 0 && !Parry.Spoken(pair.Key)))
                    continue;

                ParryClass kind = Parry.OfKind(sample, out ParryWhy why);

                switch (kind)
                {
                    case ParryClass.Light: light++; break;
                    case ParryClass.Heavy: heavy++; break;
                    case ParryClass.Giant: giant++; break;
                    default: never++; break;
                }

                string own = Parry.Spoken(pair.Key) ? " The mod speaks for it: only in the moves it says." : "";

                lines.Add($"{NPCID.Search.GetName(pair.Key)} ({Numbers(sample)}): {Says(pair.Key, kind, why, PADStaggerNPC.CanReel(sample), 0)}{own}");
            }

            lines.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> shots = new List<string>();

            foreach (KeyValuePair<int, Projectile> pair in ContentSamples.ProjectilesByType)
            {
                if (pair.Key > ProjectileID.None && pair.Value != null && ParryRules.Solid(pair.Value))
                    shots.Add(ProjectileID.Search.GetName(pair.Key));
            }

            shots.Sort(StringComparer.OrdinalIgnoreCase);

            mod.Logger.Info($"Parry: {lines.Count} kinds of enemy that hurt by touch.");

            foreach (string line in lines)
                mod.Logger.Info("Parry: " + line);

            mod.Logger.Info($"Parry: {shots.Count} shots the rules take for solid (broken by a parry when they are an enemy's; the config's and other mods' lists are on top of these): "
                + string.Join(", ", shots));

            return $"Written to the log: {lines.Count} kinds of enemy ({light + heavy} turned aside by a parry, {heavy} of them not knocked back by the game, {giant} too big to turn aside, "
                + $"{never} not parried) and {shots.Count} solid shots.";
        }

        /// <summary>Its size, and how much of a knockback it takes.</summary>
        private static string Numbers(NPC npc) => $"{npc.width} by {npc.height}, takes {npc.knockBackResist:0.##} of a knockback";

        /// <summary>What a parry does to it, in words: the mod's own for this kind when it has given some (Parry.AddNPC) and it is parried at all, the usual ones if not.</summary>
        private static string Says(int type, ParryClass kind, ParryWhy why, bool reels, int every)
        {
            string told = Parry.Told(type);

            if (told != null && (kind == ParryClass.Light || kind == ParryClass.Heavy))
                return told;

            return Says(kind, why, reels, every);
        }

        /// <summary>
        /// What a parry does to it, in words. <paramref name="every"/> is how many parries
        /// stagger it as it is now; none, for a kind not in the world, whose weight only its
        /// AI settles.
        /// </summary>
        private static string Says(ParryClass kind, ParryWhy why, bool reels, int every)
        {
            switch (kind)
            {
                case ParryClass.Light:
                case ParryClass.Heavy:
                    string weight = kind == ParryClass.Heavy ? " The game does not let it be knocked back." : "";

                    if (!reels)
                        return "a parry turns it aside, strikes it and pushes it back. Never staggered: its AI cannot be stopped." + weight;

                    string which = every == Parry.FlyingParries ? "every second parry staggers it (it flies)."
                        : every == Parry.GroundParries ? "every third parry staggers it."
                        : "every second parry staggers it if it flies, every third if not.";

                    return "a parry turns it aside, strikes it and pushes it back; " + which + weight;

                case ParryClass.Giant:
                    return "too big to turn aside. Its hit lands, and so does the parry's blow on it.";
            }

            switch (why)
            {
                case ParryWhy.NoThreat: return "not parried: it does nothing by touch, or is nobody's enemy.";
                case ParryWhy.Shot: return "not parried: it is a shot in an enemy's place, and not a solid one.";
                case ParryWhy.Boss: return "not parried: it is a boss.";
                case ParryWhy.Limb: return "not parried: it is a part of a boss.";
                case ParryWhy.Unhurt: return "not parried: nothing can hurt it as it is.";
                case ParryWhy.Bomb: return "not parried: it blows up.";
                case ParryWhy.Ghost: return "not parried: it goes through walls.";
                case ParryWhy.Star: return "not parried: it wears the yellow star.";
                case ParryWhy.Listed: return "not parried: it is on a list of things never parried.";
                case ParryWhy.NotNow: return "not parried now: the mod speaks for it, and it is not in a move that can be.";
                case ParryWhy.Reeling: return "staggered already: the next hit on it is the riposte, and a parry is not one.";
                default: return "not parried.";
            }
        }
    }
}
