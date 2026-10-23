using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The day stopped, by her (by request: "Time of day in game stop flowing with a sound effect"): from the moment of
    /// her change in Legends (EmpressOfLight.Change) until she is gone, the game's clock does not move: the sun or the moon
    /// hangs where it was, and the sky with it. The game's own way of stopping it (its rate of time put to nothing, as
    /// Journey's power to freeze time does), on every machine, so nothing is sent and nothing is left stopped: once no
    /// Empress holds it, the next tick it goes on.
    /// </summary>
    public class EmpressTime : ModSystem
    {
        /// <summary>Is the day held this tick (by any Empress that has stopped it)?</summary>
        public static bool Held
        {
            get
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];

                    if (npc.active && npc.type == NPCID.HallowBoss && (EmpressOfLight.Of(i)?.HoldsTime(npc) ?? false))
                        return true;
                }

                return false;
            }
        }

        public override void ModifyTimeRate(ref double timeRate, ref double tileUpdateRate, ref double eventUpdateRate)
        {
            if (Held)
                timeRate = 0.0;
        }
    }
}
