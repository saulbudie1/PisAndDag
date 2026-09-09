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
        /// Runs whenever a world is loaded or generated, before LoadWorldData.
        /// Without this the static would leak between worlds inside one session — start a
        /// fresh character and it would still think Legends was on from your last world.
        /// </summary>
        public override void ClearWorld()
        {
            Legends = false;
            FlightAnnounced = false;
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
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Legends = tag.ContainsKey("Legends");
        }

        // Brings joining clients in line with the server. Inert in singleplayer, but it
        // costs two lines and a world flag is exactly the thing that desyncs without it.
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Legends);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Legends = reader.ReadBoolean();
        }
    }
}
