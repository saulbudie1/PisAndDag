using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag
{
    /// <summary>
    /// World-scoped state for PisAndDag. Lives in the root namespace, so anything in the
    /// mod can read <c>PADWorld.Legends</c> without a using directive — every other
    /// namespace here sits underneath PisAndDag.
    /// </summary>
    public class PADWorld : ModSystem
    {
        /// <summary>
        /// Legends mode. Switched on for good by breaking the Game Changing Orb.
        /// There is deliberately no path that sets this back to false on a live world.
        /// </summary>
        public static bool Legends;

        /// <summary>
        /// Whether wings, boosters and flying mounts do anything at all.
        ///
        /// Reads the vanilla downed flag rather than a flag of our own, so a world that
        /// beat the Empress before this mod was installed is already unlocked. Making
        /// those players kill her a second time to get something they had already earned
        /// would be the wrong answer.
        /// </summary>
        public static bool FlightUnlocked => NPC.downedEmpressOfLight;

        /// <summary>
        /// Whether the unlock has been announced in chat.
        ///
        /// Runtime only, and deliberately not saved: it is rebuilt from the vanilla downed
        /// flag on load, which gives exactly the same answer a saved copy would without
        /// putting another key in the world file.
        /// </summary>
        public static bool FlightAnnounced;

        /// <summary>
        /// Whether the Megashark has been killed in this world. Set by PADNPCs.OnKill
        /// on the side that decides kills, saved, and sent to joining clients. Boss
        /// Checklist reads it (BossLogSupport).
        /// </summary>
        public static bool DownedMegashark;

        /// <summary>
        /// Whether the Abomanation has been killed in this world. Same shape as
        /// DownedMegashark: set by PADNPCs.OnKill on the side that decides kills, saved,
        /// and sent to joining clients. Boss Checklist reads it (BossChecklistSupport).
        /// </summary>
        public static bool DownedAbomanation;

        /// <summary>
        /// Small animals let go on the Wasted Mile and not seen again. The Abomanation
        /// comes when this reaches Abomanation.Offerings, and it goes back to zero. World
        /// state like the downed flags: kept across sessions, sent to joining clients,
        /// and never per player. Counted on the side that owns NPCs (AbomanationSummon).
        /// </summary>
        public static int MileOfferings;

        /// <summary>
        /// Whether Shimmer does anything.
        ///
        /// False — the normal state, and the state every world starts in — means the
        /// Archmage's second crime is still standing: §XI [LOCKED]. He did not hide the
        /// substance, he locked what it *does*. So the seams and the pools are all still
        /// there, they still shine, they can still be waded through and carried in a
        /// bucket, and nothing that goes into them changes. It is water that glitters.
        ///
        /// True means the lock is open and Shimmer asks its question again.
        ///
        /// Read the name as the subject rather than the condition: this is the Archmage's
        /// Shimmer, and the flag says whether it is working. Nothing sets it yet; §XI
        /// leaves it open whether the lock can be opened at all. Until something does,
        /// /archmageshimmer is the only way it moves.
        /// </summary>
        public static bool ArchmageShimmer;

        /// <summary>
        /// Runs whenever a world is loaded or generated, before LoadWorldData.
        /// Without this the static would leak between worlds inside one session — start a
        /// fresh character and it would still think Legends was on from your last world.
        /// </summary>
        public override void ClearWorld()
        {
            Legends = false;
            FlightAnnounced = false;
            DownedMegashark = false;
            DownedAbomanation = false;
            MileOfferings = 0;
            ArchmageShimmer = false;
        }

        /// <summary>
        /// Runs after the world's own flags have loaded, so the vanilla downed flag can be
        /// read here. A world that had already beaten the Empress has nothing to announce —
        /// the ground rules there changed before anyone was watching.
        /// </summary>
        public override void OnWorldLoad()
        {
            FlightAnnounced = NPC.downedEmpressOfLight;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            // Presence of the key is the value. Nothing is written while the flag is off,
            // so worlds that never had it stay untouched.
            if (Legends)
                tag["Legends"] = true;

            if (DownedMegashark)
                tag["DownedMegashark"] = true;

            if (DownedAbomanation)
                tag["DownedAbomanation"] = true;

            if (MileOfferings > 0)
                tag["MileOfferings"] = MileOfferings;

            if (ArchmageShimmer)
                tag["ArchmageShimmer"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Legends = tag.ContainsKey("Legends");
            DownedMegashark = tag.ContainsKey("DownedMegashark");
            DownedAbomanation = tag.ContainsKey("DownedAbomanation");
            MileOfferings = tag.ContainsKey("MileOfferings") ? tag.GetInt("MileOfferings") : 0;
            ArchmageShimmer = tag.ContainsKey("ArchmageShimmer");
        }

        // Brings joining clients in line with the server. Inert in singleplayer, but it
        // costs two lines and a world flag is exactly the thing that desyncs without it.
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Legends);
            writer.Write(DownedMegashark);
            writer.Write(DownedAbomanation);
            writer.Write(MileOfferings);
            writer.Write(ArchmageShimmer);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Legends = reader.ReadBoolean();
            DownedMegashark = reader.ReadBoolean();
            DownedAbomanation = reader.ReadBoolean();
            MileOfferings = reader.ReadInt32();
            ArchmageShimmer = reader.ReadBoolean();
        }
    }
}
