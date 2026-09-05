using System.IO;
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
        /// Runs whenever a world is loaded or generated, before LoadWorldData.
        /// Without this the static would leak between worlds inside one session — start a
        /// fresh character and it would still think Legends was on from your last world.
        /// </summary>
        public override void ClearWorld()
        {
            Legends = false;
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
