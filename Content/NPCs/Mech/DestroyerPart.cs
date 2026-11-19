using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The Destroyer's body, reworked (Destroyer). While its head is driven (a scene, a Legends move) it is laid along the
    /// way its head has come (Destroyer.Path) instead of vanilla's pull; otherwise vanilla's AI moves it. In a cinematic it
    /// cannot be struck; it touches nobody while its head touches nobody; it throws up the ground where it crosses it. In
    /// Legends vanilla's lasers are its head's to fire (its own bolts), its eye turns to its colour at its second wind,
    /// with a trail behind it, and it glows yellow in its storm. At its end it is blown up a segment at a time
    /// (Exploded), and is not drawn from then on.
    /// </summary>
    public class DestroyerPart : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.TheDestroyerBody || entity.type == NPCID.TheDestroyerTail;

        /// <summary>Where it is in its body, from the segment after its head (0), and how long its body is (its head tells it).</summary>
        internal int Index = -1;
        internal int Count;

        /// <summary>Blown up, at its end: not drawn, touches nobody.</summary>
        internal bool Exploded;

        /// <summary>Made untouchable by its head's scene (and so to be given back when it ends).</summary>
        private bool shielded;

        /// <summary>Whether it was in the ground last tick (this machine's).</summary>
        private bool wasSolid, known;

        /// <summary>Where it is drawn off by this frame (its head's shake), to be put back after.</summary>
        private Vector2 drawShift;

        /// <summary>Where its eye has been, newest first (this machine's), for its trail.</summary>
        internal readonly Vector2[] eyeTrail = new Vector2[8];
        private int eyeKept;

        public override void SetDefaults(NPC entity)
        {
            if (PADWorld.Legends)
                entity.damage = (int)(entity.damage * Destroyer.DamageScale);
        }

        public override bool PreAI(NPC npc)
        {
            if (NPC.IsMechQueenUp || !Destroyer.HeadOf(npc, out _, out Destroyer rework))
            {
                Unshield(npc);
                return true;
            }

            // Legends: vanilla's lasers are not its own (it counts to them here); its head fires its bolts.
            if (PADWorld.Legends)
                npc.localAI[0] = 0f;

            if (rework.InScene)
            {
                npc.localAI[0] = 0f;
                npc.dontTakeDamage = true;
                shielded = true;
                npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
            }
            else
            {
                Unshield(npc);
            }

            // Driven: on the way its head has come, facing the one ahead.
            if (rework.Drives && Index >= 0 && rework.PlaceOf(Index, out Vector2 place, out float facing))
            {
                npc.velocity = place - npc.Center;
                npc.rotation = facing;
                npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

                if (npc.alpha > 0)
                    npc.alpha = Math.Max(0, npc.alpha - 42);

                return false;
            }

            return true;
        }

        private void Unshield(NPC npc)
        {
            if (!shielded)
                return;

            shielded = false;
            npc.dontTakeDamage = false;
        }

        public override void PostAI(NPC npc)
        {
            if (Main.dedServ)
                return;

            // Its eye's way, for its trail.
            Vector2 eye = npc.Center + npc.velocity;

            for (int i = eyeTrail.Length - 1; i > 0; i--)
                eyeTrail[i] = eyeTrail[i - 1];

            eyeTrail[0] = eye;
            eyeKept = Math.Min(eyeTrail.Length, eyeKept + 1);

            if (Exploded || NPC.IsMechQueenUp || !Destroyer.HeadOf(npc, out _, out Destroyer rework) || !rework.Drives || rework.Laying)
            {
                known = false;
                return;
            }

            // Where it goes through the ground: a third of them, or it is a wall of dust.
            bool solid = Destroyer.InGround(npc.Center + npc.velocity);

            if (known && solid != wasSolid && Index % 3 == 0)
                Destroyer.Spill(npc, !solid);

            wasSolid = solid;
            known = true;
        }

        /// <summary>How many points of its eye's way are known on this machine.</summary>
        internal int EyeKept => eyeKept;

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (Exploded)
                return false;

            return !(Destroyer.HeadOf(npc, out _, out Destroyer rework) && rework.Harmless && !NPC.IsMechQueenUp);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Exploded)
                return false;

            if (NPC.IsMechQueenUp || !Destroyer.HeadOf(npc, out NPC head, out Destroyer rework))
                return true;

            // Its head's shake, seen only, each part its own way: put back in PostDraw (which always follows).
            if (rework.shake > 0.05f)
            {
                drawShift = Main.rand.NextVector2Circular(rework.shake, rework.shake);
                npc.position += drawShift;
            }

            return !rework.DrawSegment(head, npc, this, spriteBatch, screenPos, drawColor);
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            npc.position -= drawShift;
            drawShift = Vector2.Zero;
        }
    }
}
