using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Golem's pace while its head is on.
    ///
    /// The game reads a Golem below full life as further into its fight: its fists fly home
    /// and punch faster and wait a tenth as long between punches (NPC.AI_047_GolemFist), and
    /// its own count toward a jump goes up faster (NPC.AI_045_Golem). In the game itself that
    /// cannot begin before the head is off, since nothing can hurt the body until then. A
    /// parried fist can (GolemFistParry), and would set all of it going at the first one
    /// sent back.
    ///
    /// So while the head is on, those two AIs are shown a Golem at full life: its life is
    /// put by before the AI of the Golem or of either fist runs, and put back straight
    /// after, less whatever it lost in between. Nothing else sees the full life: the hits on
    /// it, its bar, what is sent to the other machines and what is drawn all come outside
    /// those AIs. Once the head is off nothing is put by, and the Golem is as fast as the
    /// life it has left makes it.
    ///
    /// "The head is on" is the game's own word for it: NPC.dontTakeDamage on the body, which
    /// its AI sets each tick to whether a Golem Head is alive. The head's own AI while it is
    /// on (aiStyle 46) reads only where the body is, never its life, so it needs none of
    /// this.
    /// </summary>
    public class GolemPace : GlobalNPC
    {
        /// <summary>Whose life is put by (its place in Main.npc), or -1; what it really is; what its AI is shown in its place.</summary>
        private static int held = -1;
        private static int real;
        private static int shown;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Golem || entity.type == NPCID.GolemFistLeft || entity.type == NPCID.GolemFistRight;
        }

        public override bool PreAI(NPC npc)
        {
            // Never two put by at once, whatever cut the last AI short.
            Restore();

            NPC core = npc;

            if (npc.type != NPCID.Golem)
            {
                int index = NPC.golemBoss;

                if (index < 0 || index >= Main.maxNPCs)
                    return true;

                core = Main.npc[index];
            }

            if (!core.active || core.type != NPCID.Golem || !core.dontTakeDamage || core.life <= 0 || core.life >= core.lifeMax)
                return true;

            held = core.whoAmI;
            real = core.life;
            shown = core.lifeMax;
            core.life = shown;

            return true;
        }

        public override void PostAI(NPC npc)
        {
            Restore();
        }

        /// <summary>
        /// The Golem's life put back as it really is, less anything it lost while it was put
        /// by. Does nothing if none is. Anything that is about to hurt the Golem from inside
        /// an AI's hooks asks this first, or the hurt would be lost when it is put back.
        /// </summary>
        internal static void Restore()
        {
            if (held < 0)
                return;

            NPC core = Main.npc[held];

            held = -1;

            // Still there: one killed while put by is left as it is. Never put back at
            // nothing while it lives: the next hit is what ends it then.
            if (core.active && core.type == NPCID.Golem)
                core.life = System.Math.Max(1, real + (core.life - shown));
        }

        public override void Unload()
        {
            held = -1;
        }
    }
}
