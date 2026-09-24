using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Eye of Cthulhu's roar as it turns.
    ///
    /// Halfway down its life the Eye spins up, and at the top of the spin vanilla throws
    /// off the gore of its first face, bares the mouth, and plays its roar (AI_004:
    /// ai[0] going from 1 to 2). At that moment the screen takes the roar too
    /// (Cinematic.Roar): the rings, the dark, the shake. No camera, and no second sound;
    /// vanilla's is the voice. Every world, every Eye.
    ///
    /// Read off ai[0], which every machine runs and the server sends, so each client
    /// sees it once, however it heard of the turn.
    /// </summary>
    public class EyeRoar : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.EyeofCthulhu;

        /// <summary>The roar's length, in ticks. Inside the spin down that follows it, which is a hundred.</summary>
        private const int Length = 90;

        private float lastFace = -1f;
        private bool roared;

        public override void PostAI(NPC npc)
        {
            float face = npc.ai[0];

            if (!roared && lastFace == 1f && face == 2f)
            {
                roared = true;
                Cinematic.Roar(npc, Length, 1f, quiet: true);
            }

            lastFace = face;
        }
    }
}
