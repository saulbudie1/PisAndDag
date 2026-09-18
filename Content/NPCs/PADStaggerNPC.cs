using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Stagger. Terraria has no such thing, so this is it: while the timer runs the NPC's
    /// own AI does not, and it slides backwards a fixed distance whatever it is.
    ///
    /// Written for the Greatbow of a Pilgrim (PAD_Item_Plan.md) and deliberately kept
    /// general — anything in the mod that wants to stop something for a moment calls
    /// Stagger.
    ///
    /// The slide is a slide, not knockback. Every boss in the game resists knockback, and
    /// "even the Wall of Flesh goes backwards" is the whole point of the weapon, so the
    /// position is written directly and spread evenly over the stagger so it reads as
    /// being pushed rather than punted.
    /// </summary>
    public class PADStaggerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// Anything whose AI cannot survive being skipped. A worm segment that misses a
        /// tick loses its chain; a teleporter frozen mid-teleport never arrives.
        ///
        /// This is a starting list and it is expected to grow. Nothing tests itself.
        /// </summary>
        private static readonly HashSet<int> NeverStagger = new()
        {
            NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail,
            NPCID.DiggerHead, NPCID.DiggerBody, NPCID.DiggerTail,
            NPCID.WyvernHead, NPCID.WyvernBody, NPCID.WyvernBody2,
            NPCID.WyvernBody3, NPCID.WyvernLegs, NPCID.WyvernTail,
            NPCID.DukeFishron,          // phase transitions are timer driven
            NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead,
        };

        private int staggerTicks;
        private int slideTicks;
        private Vector2 slidePerTick;

        public bool Staggered => staggerTicks > 0;

        /// <summary>Stop it. Refreshes rather than stacks.</summary>
        public void Stagger(NPC npc, int ticks)
        {
            if (ticks <= 0 || NeverStagger.Contains(npc.type))
                return;

            staggerTicks = Math.Max(staggerTicks, ticks);
            npc.netUpdate = true;
        }

        /// <summary>Push it, evenly, over <paramref name="ticks"/>.</summary>
        public void Slide(Vector2 direction, float pixels, int ticks)
        {
            if (pixels <= 0f || ticks <= 0)
                return;

            slideTicks = ticks;
            slidePerTick = direction.SafeNormalize(Vector2.UnitX) * (pixels / ticks);
        }

        public override bool PreAI(NPC npc)
        {
            if (staggerTicks <= 0)
                return true;

            npc.velocity = Vector2.Zero;
            return false;   // its AI does not run at all this tick
        }

        public override void PostAI(NPC npc)
        {
            if (staggerTicks > 0)
                staggerTicks--;

            if (slideTicks > 0)
            {
                slideTicks--;

                // Written straight into position. The Wall of Flesh re-derives its X from
                // its own AI every tick, which is exactly why this has to be applied while
                // that AI is skipped — it resumes from wherever it has been put.
                npc.position += slidePerTick;
            }
        }

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((short)staggerTicks);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            staggerTicks = binaryReader.ReadInt16();
        }
    }
}
