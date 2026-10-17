using System;
using System.Collections.Generic;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.NPCs.DonaQuinn;
using PisAndDag.Content.NPCs.Mors;
using PisAndDag.Content.NPCs.Pugream;
using PisAndDag.Content.Structures;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Subworlds
{
    /// <summary>
    /// What a world and its places have in common: the flags.
    ///
    /// A place is a world file of its own, and the game keeps a world's flags in its file.
    /// Left to itself a place would know nothing of the world it belongs to (no hard mode,
    /// no boss beaten, none of the mod's own story), and nothing done in one would be
    /// known in the world. So they are carried by hand, both ways:
    ///
    ///   INTO A PLACE, everything listed here, as it stands in the world the moment the
    ///   world is left (<see cref="Take"/>), put in place of whatever the place's file
    ///   said (<see cref="Give"/>). In a place they are the world's.
    ///
    ///   BACK TO THE WORLD, what was in force in the place when it was last saved
    ///   (SubworldKeeper writes it beside the place's file, and the world reads it the next
    ///   time it is loaded: through the door, or days later after a game left from inside
    ///   the place). What comes back can only add to the world: a flag set there is set
    ///   here, a count that went up there has gone up here, a name learnt there is known
    ///   here. Nothing is ever taken off the world by it, so that nothing that goes wrong
    ///   with a file can cost a world what it has done.
    ///
    /// THE GAME'S: every boss and invasion beaten; who has been saved, bought or let in;
    /// the books used; and, into a place only, hard mode and which evil the world has.
    /// Those two are the world's own doing (hard mode is the world changed, not a flag
    /// set): a place has the world's, and cannot give the world one it did not have.
    ///
    /// THE MOD'S: Legends, its own bosses beaten, and where each of its stories has got
    /// to, the count of deaths with them. What only ever changes on the world's own
    /// ground (the offerings on the Wasted Mile, the Guide's first words being owed) is
    /// taken into a place and not brought back.
    ///
    /// THE CLOCK, only where a place's clock runs (Subworld.ClockRuns: the Vast Ocean), both
    /// ways and as it stands, not only so far as it adds: the time of day and whether it is
    /// day, the moon's phase, a blood moon or an eclipse, the rain and the wind, and the
    /// day's fish for the Angler with who has brought it in. Into such a place with
    /// whoever goes; back to the world in the place's writing (a place whose clock stands
    /// still writes none, and the world's own is left as it is). A night spent there is a
    /// night gone in the world, and the Angler's fish is the same fish on both sides.
    ///
    /// NOT CARRIED AT ALL: where anything is (a place has none of the world's ground), the
    /// world's secret seed, any other event going on in it (an invasion, a party, a rain
    /// of slime), the bestiary and the kill counts, Journey's powers, and other mods' flags.
    ///
    /// A flag of the mod's that a place should have goes in a list here as well as in its
    /// own system's save.
    /// </summary>
    internal static class SubworldFlags
    {
        private readonly struct Flag
        {
            public readonly string Name;
            public readonly Func<bool> Get;
            public readonly Action<bool> Set;

            public Flag(string name, Func<bool> get, Action<bool> set)
            {
                Name = name;
                Get = get;
                Set = set;
            }
        }

        private readonly struct Count
        {
            public readonly string Name;
            public readonly Func<int> Get;
            public readonly Action<int> Set;

            public Count(string name, Func<int> get, Action<int> set)
            {
                Name = name;
                Get = get;
                Set = set;
            }
        }

        /// <summary>
        /// Both ways. Every one of them only ever goes from no to yes. The game's are all
        /// that it writes into a world's file of the kind (WorldFile.SaveWorldHeader and
        /// DD2Event.Save, 1.4.4).
        /// </summary>
        private static readonly Flag[] Flags =
        {
            // Bosses.
            new("downedSlimeKing", () => NPC.downedSlimeKing, v => NPC.downedSlimeKing = v),
            new("downedBoss1", () => NPC.downedBoss1, v => NPC.downedBoss1 = v),
            new("downedBoss2", () => NPC.downedBoss2, v => NPC.downedBoss2 = v),
            new("downedBoss3", () => NPC.downedBoss3, v => NPC.downedBoss3 = v),
            new("downedQueenBee", () => NPC.downedQueenBee, v => NPC.downedQueenBee = v),
            new("downedDeerclops", () => NPC.downedDeerclops, v => NPC.downedDeerclops = v),
            new("downedQueenSlime", () => NPC.downedQueenSlime, v => NPC.downedQueenSlime = v),
            new("downedMechBoss1", () => NPC.downedMechBoss1, v => NPC.downedMechBoss1 = v),
            new("downedMechBoss2", () => NPC.downedMechBoss2, v => NPC.downedMechBoss2 = v),
            new("downedMechBoss3", () => NPC.downedMechBoss3, v => NPC.downedMechBoss3 = v),
            new("downedMechBossAny", () => NPC.downedMechBossAny, v => NPC.downedMechBossAny = v),
            new("downedPlantBoss", () => NPC.downedPlantBoss, v => NPC.downedPlantBoss = v),
            new("downedGolemBoss", () => NPC.downedGolemBoss, v => NPC.downedGolemBoss = v),
            new("downedFishron", () => NPC.downedFishron, v => NPC.downedFishron = v),
            new("downedEmpressOfLight", () => NPC.downedEmpressOfLight, v => NPC.downedEmpressOfLight = v),
            new("downedAncientCultist", () => NPC.downedAncientCultist, v => NPC.downedAncientCultist = v),
            new("downedTowerSolar", () => NPC.downedTowerSolar, v => NPC.downedTowerSolar = v),
            new("downedTowerVortex", () => NPC.downedTowerVortex, v => NPC.downedTowerVortex = v),
            new("downedTowerNebula", () => NPC.downedTowerNebula, v => NPC.downedTowerNebula = v),
            new("downedTowerStardust", () => NPC.downedTowerStardust, v => NPC.downedTowerStardust = v),
            new("downedMoonlord", () => NPC.downedMoonlord, v => NPC.downedMoonlord = v),

            // Invasions and the moons.
            new("downedGoblins", () => NPC.downedGoblins, v => NPC.downedGoblins = v),
            new("downedClown", () => NPC.downedClown, v => NPC.downedClown = v),
            new("downedFrost", () => NPC.downedFrost, v => NPC.downedFrost = v),
            new("downedPirates", () => NPC.downedPirates, v => NPC.downedPirates = v),
            new("downedMartians", () => NPC.downedMartians, v => NPC.downedMartians = v),
            new("downedHalloweenTree", () => NPC.downedHalloweenTree, v => NPC.downedHalloweenTree = v),
            new("downedHalloweenKing", () => NPC.downedHalloweenKing, v => NPC.downedHalloweenKing = v),
            new("downedChristmasTree", () => NPC.downedChristmasTree, v => NPC.downedChristmasTree = v),
            new("downedChristmasSantank", () => NPC.downedChristmasSantank, v => NPC.downedChristmasSantank = v),
            new("downedChristmasIceQueen", () => NPC.downedChristmasIceQueen, v => NPC.downedChristmasIceQueen = v),
            new("DownedInvasionT1", () => DD2Event.DownedInvasionT1, v => DD2Event.DownedInvasionT1 = v),
            new("DownedInvasionT2", () => DD2Event.DownedInvasionT2, v => DD2Event.DownedInvasionT2 = v),
            new("DownedInvasionT3", () => DD2Event.DownedInvasionT3, v => DD2Event.DownedInvasionT3 = v),

            // Who has been saved.
            new("savedGoblin", () => NPC.savedGoblin, v => NPC.savedGoblin = v),
            new("savedWizard", () => NPC.savedWizard, v => NPC.savedWizard = v),
            new("savedMech", () => NPC.savedMech, v => NPC.savedMech = v),
            new("savedAngler", () => NPC.savedAngler, v => NPC.savedAngler = v),
            new("savedStylist", () => NPC.savedStylist, v => NPC.savedStylist = v),
            new("savedTaxCollector", () => NPC.savedTaxCollector, v => NPC.savedTaxCollector = v),
            new("savedGolfer", () => NPC.savedGolfer, v => NPC.savedGolfer = v),
            new("savedBartender", () => NPC.savedBartender, v => NPC.savedBartender = v),

            // Who has been let in, and what has been bought and used.
            new("unlockedMerchantSpawn", () => NPC.unlockedMerchantSpawn, v => NPC.unlockedMerchantSpawn = v),
            new("unlockedDemolitionistSpawn", () => NPC.unlockedDemolitionistSpawn, v => NPC.unlockedDemolitionistSpawn = v),
            new("unlockedPartyGirlSpawn", () => NPC.unlockedPartyGirlSpawn, v => NPC.unlockedPartyGirlSpawn = v),
            new("unlockedDyeTraderSpawn", () => NPC.unlockedDyeTraderSpawn, v => NPC.unlockedDyeTraderSpawn = v),
            new("unlockedTruffleSpawn", () => NPC.unlockedTruffleSpawn, v => NPC.unlockedTruffleSpawn = v),
            new("unlockedArmsDealerSpawn", () => NPC.unlockedArmsDealerSpawn, v => NPC.unlockedArmsDealerSpawn = v),
            new("unlockedNurseSpawn", () => NPC.unlockedNurseSpawn, v => NPC.unlockedNurseSpawn = v),
            new("unlockedPrincessSpawn", () => NPC.unlockedPrincessSpawn, v => NPC.unlockedPrincessSpawn = v),
            new("unlockedSlimeBlueSpawn", () => NPC.unlockedSlimeBlueSpawn, v => NPC.unlockedSlimeBlueSpawn = v),
            new("unlockedSlimeGreenSpawn", () => NPC.unlockedSlimeGreenSpawn, v => NPC.unlockedSlimeGreenSpawn = v),
            new("unlockedSlimeOldSpawn", () => NPC.unlockedSlimeOldSpawn, v => NPC.unlockedSlimeOldSpawn = v),
            new("unlockedSlimePurpleSpawn", () => NPC.unlockedSlimePurpleSpawn, v => NPC.unlockedSlimePurpleSpawn = v),
            new("unlockedSlimeRainbowSpawn", () => NPC.unlockedSlimeRainbowSpawn, v => NPC.unlockedSlimeRainbowSpawn = v),
            new("unlockedSlimeRedSpawn", () => NPC.unlockedSlimeRedSpawn, v => NPC.unlockedSlimeRedSpawn = v),
            new("unlockedSlimeYellowSpawn", () => NPC.unlockedSlimeYellowSpawn, v => NPC.unlockedSlimeYellowSpawn = v),
            new("unlockedSlimeCopperSpawn", () => NPC.unlockedSlimeCopperSpawn, v => NPC.unlockedSlimeCopperSpawn = v),
            new("boughtCat", () => NPC.boughtCat, v => NPC.boughtCat = v),
            new("boughtDog", () => NPC.boughtDog, v => NPC.boughtDog = v),
            new("boughtBunny", () => NPC.boughtBunny, v => NPC.boughtBunny = v),
            new("combatBookWasUsed", () => NPC.combatBookWasUsed, v => NPC.combatBookWasUsed = v),
            new("combatBookVolumeTwoWasUsed", () => NPC.combatBookVolumeTwoWasUsed, v => NPC.combatBookVolumeTwoWasUsed = v),
            new("peddlersSatchelWasUsed", () => NPC.peddlersSatchelWasUsed, v => NPC.peddlersSatchelWasUsed = v),

            // What has been done to the world, once.
            new("shadowOrbSmashed", () => WorldGen.shadowOrbSmashed, v => WorldGen.shadowOrbSmashed = v),
            new("afterPartyOfDoom", () => Main.afterPartyOfDoom, v => Main.afterPartyOfDoom = v),

            // The mod's.
            new("Legends", () => PADWorld.Legends, v => PADWorld.Legends = v),
            new("DownedMegashark", () => PADWorld.DownedMegashark, v => PADWorld.DownedMegashark = v),
            new("DownedAbomanation", () => PADWorld.DownedAbomanation, v => PADWorld.DownedAbomanation = v),
            new("ArchmageShimmer", () => PADWorld.ArchmageShimmer, v => PADWorld.ArchmageShimmer = v),
            new("GraceUnsealed", () => PADWorld.GraceUnsealed, v => PADWorld.GraceUnsealed = v),
            new("GuideSpokeOfLegends", () => PADWorld.GuideSpokeOfLegends, v => PADWorld.GuideSpokeOfLegends = v),
            new("MorsSaidGraveyard", () => MorsPresence.SaidGraveyard, v => MorsPresence.SaidGraveyard = v),
            new("MorsSaidMonument", () => MorsPresence.SaidMonument, v => MorsPresence.SaidMonument = v),
            new("MorsAsked", () => MorsErrand.Asked, v => MorsErrand.Asked = v),
            new("MorsAccepted", () => MorsErrand.Accepted, v => MorsErrand.Accepted = v),
            new("MorsPouchDropped", () => MorsErrand.Dropped, v => MorsErrand.Dropped = v),
            new("MorsPouchReturned", () => MorsErrand.Returned, v => MorsErrand.Returned = v),
        };

        /// <summary>Into a place only: the world's own doing, or changed nowhere but on the world's own ground.</summary>
        private static readonly Flag[] WorldsOwn =
        {
            new("hardMode", () => Main.hardMode, v => Main.hardMode = v),
            new("crimson", () => WorldGen.crimson, v => WorldGen.crimson = v),
            new("GuideIntroPending", () => GuideIntro.Pending, v => GuideIntro.Pending = v),
        };

        /// <summary>Both ways. Every one of them only ever goes up.</summary>
        private static readonly Count[] Counts =
        {
            new("GraveyardDeaths", () => GraveyardZone.Deaths, v => GraveyardZone.Deaths = v),
            new("GraveyardElsewhere", () => GraveyardZone.Elsewhere, v => GraveyardZone.Elsewhere = v),
            new("MorsStage", () => MorsPresence.Stage, v => MorsPresence.Stage = v),
            new("PugreamStage", () => PugreamPresence.Stage, v => PugreamPresence.Stage = v),
            new("DonaQuinnStage", () => DonaQuinnEncounter.Stage, v => DonaQuinnEncounter.Stage = v),
        };

        /// <summary>Into a place only.</summary>
        private static readonly Count[] WorldsOwnCounts =
        {
            new("MileOfferings", () => PADWorld.MileOfferings, v => PADWorld.MileOfferings = v),
        };

        private const string KnownKey = "PADIntroduced";
        private const string HeardKey = "PADTalkHeard";

        /// <summary>The clock, in one compound of its own: in a taking only where it is to be carried (<see cref="ClockGoes"/>).</summary>
        private const string ClockKey = "PADClock";
        private const string AnglerDoneKey = "PADAnglerDone";

        /// <summary>
        /// The clock goes with this taking: always from a world (it is only given to a place
        /// whose clock runs), and from a place only if its clock runs.
        /// </summary>
        private static bool ClockGoes => !Subworld.Active || Subworld.Current.ClockRuns;

        /// <summary>Everything listed, as it stands in the world loaded.</summary>
        public static TagCompound Take()
        {
            var tag = new TagCompound();

            foreach (Flag flag in Flags)
                tag[flag.Name] = flag.Get();

            foreach (Flag flag in WorldsOwn)
                tag[flag.Name] = flag.Get();

            foreach (Count count in Counts)
                tag[count.Name] = count.Get();

            foreach (Count count in WorldsOwnCounts)
                tag[count.Name] = count.Get();

            // Whose name the player has, and which answers have been given: the world's too.
            List<string> known = Copy(DialogueIntroductions.Known);
            List<string> heard = Copy(TalkMemory.Heard);

            if (known != null)
                tag[KnownKey] = known;

            if (heard != null)
                tag[HeardKey] = heard;

            if (ClockGoes)
                tag[ClockKey] = TakeClock();

            return tag;
        }

        /// <summary>The clock as it stands (the notes over this class say what that is).</summary>
        private static TagCompound TakeClock()
        {
            var clock = new TagCompound
            {
                ["time"] = Main.time,
                ["dayTime"] = Main.dayTime,
                ["moonPhase"] = Main.moonPhase,
                ["bloodMoon"] = Main.bloodMoon,
                ["eclipse"] = Main.eclipse,
                ["raining"] = Main.raining,
                ["rainTime"] = Main.rainTime,
                ["maxRaining"] = Main.maxRaining,
                ["windSpeedTarget"] = Main.windSpeedTarget,
                ["windSpeedCurrent"] = Main.windSpeedCurrent,
                ["anglerQuest"] = Main.anglerQuest,
                ["anglerQuestFinished"] = Main.anglerQuestFinished,
            };

            List<string> done = Copy(Main.anglerWhoFinishedToday);

            if (done != null)
                clock[AnglerDoneKey] = done;

            return clock;
        }

        /// <summary>The clock as it was taken, put in place of this world's. What a taking does not have is left as it is.</summary>
        private static void GiveClock(TagCompound clock)
        {
            if (clock.ContainsKey("time"))
                Main.time = clock.GetDouble("time");

            if (clock.ContainsKey("dayTime"))
                Main.dayTime = clock.GetBool("dayTime");

            if (clock.ContainsKey("moonPhase"))
                Main.moonPhase = clock.GetInt("moonPhase");

            if (clock.ContainsKey("bloodMoon"))
                Main.bloodMoon = clock.GetBool("bloodMoon");

            if (clock.ContainsKey("eclipse"))
                Main.eclipse = clock.GetBool("eclipse");

            if (clock.ContainsKey("raining"))
                Main.raining = clock.GetBool("raining");

            if (clock.ContainsKey("rainTime"))
                Main.rainTime = clock.GetDouble("rainTime");

            if (clock.ContainsKey("maxRaining"))
                Main.maxRaining = clock.GetFloat("maxRaining");

            if (clock.ContainsKey("windSpeedTarget"))
                Main.windSpeedTarget = clock.GetFloat("windSpeedTarget");

            if (clock.ContainsKey("windSpeedCurrent"))
                Main.windSpeedCurrent = clock.GetFloat("windSpeedCurrent");

            if (clock.ContainsKey("anglerQuest"))
                Main.anglerQuest = clock.GetInt("anglerQuest");

            if (clock.ContainsKey("anglerQuestFinished"))
                Main.anglerQuestFinished = clock.GetBool("anglerQuestFinished");

            if (clock.ContainsKey(AnglerDoneKey))
            {
                Main.anglerWhoFinishedToday.Clear();
                Main.anglerWhoFinishedToday.AddRange(clock.GetList<string>(AnglerDoneKey));
            }
        }

        /// <summary>
        /// A set as a list. A world is saved off the main thread, and the set may be added
        /// to under the copying: tried again once, and left out of this taking if it
        /// happens twice (what is not in a taking is not given).
        /// </summary>
        private static List<string> Copy(IEnumerable<string> set)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return new List<string>(set);
                }
                catch (InvalidOperationException)
                {
                }
            }

            return null;
        }

        /// <summary>
        /// What was taken, given to the world loaded. To a place (<paramref name="place"/>),
        /// all of it, in place of what was there: it is the world's word. To the world, what
        /// goes both ways only, and only so far as it adds: a flag that is set, a count that
        /// is higher, a name or an answer not yet known. What a taking does not have (one
        /// from before a flag was listed) is left as it is.
        /// </summary>
        public static void Give(TagCompound from, bool place)
        {
            if (from == null)
                return;

            foreach (Flag flag in Flags)
            {
                if (!from.ContainsKey(flag.Name))
                    continue;

                bool there = from.GetBool(flag.Name);

                if (place)
                    flag.Set(there);
                else if (there)
                    flag.Set(true);
            }

            foreach (Count count in Counts)
            {
                if (!from.ContainsKey(count.Name))
                    continue;

                int there = from.GetInt(count.Name);

                if (place || there > count.Get())
                    count.Set(there);
            }

            if (place)
            {
                foreach (Flag flag in WorldsOwn)
                {
                    if (from.ContainsKey(flag.Name))
                        flag.Set(from.GetBool(flag.Name));
                }

                foreach (Count count in WorldsOwnCounts)
                {
                    if (from.ContainsKey(count.Name))
                        count.Set(from.GetInt(count.Name));
                }
            }

            Fill(DialogueIntroductions.Known, from, KnownKey, place);
            Fill(TalkMemory.Heard, from, HeardKey, place);

            // The clock: into a place only if its clock runs; back to the world whenever the
            // place wrote one, which it did only if its clock runs.
            if (from.ContainsKey(ClockKey) && (!place || Subworld.Current?.ClockRuns == true))
                GiveClock(from.GetCompound(ClockKey));

            // What the mod works out from a flag as a world is loaded (PADWorld.OnWorldLoad),
            // worked out again from the flag as it now is: an Empress beaten on the other
            // side of a door has been announced there.
            PADWorld.FlightAnnounced = NPC.downedEmpressOfLight;
        }

        private static void Fill(HashSet<string> set, TagCompound from, string key, bool place)
        {
            if (!from.ContainsKey(key))
                return;

            if (place)
                set.Clear();

            set.UnionWith(from.GetList<string>(key));
        }
    }
}
