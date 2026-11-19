using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// Being in Skeletron Prime's vice (Legends: its grapple, PrimeArm.Legends; its hold, PrimeArm.Group).
    ///
    /// Put where its jaws are and turned as it turns them, unable to move or use anything, for as long as the vice asks
    /// (every tick: <see cref="Hold"/>). The tick it stops asking, they are out, going the way it said they would (its
    /// throw: set, not pushed, so nothing that resists a knockback resists it: asked after) and a moment untouchable.
    /// Whirled, nothing else hurts them (their place is the vice's, not theirs to dodge from); held for its other arms
    /// to fire on, everything does.
    /// </summary>
    public class PrimeGrip : ModPlayer
    {
        /// <summary>The vice holding this player, or -1.</summary>
        public int HeldBy = -1;

        private int heldTicks;
        private Vector2 at, letGo;
        private float turn;
        private bool spared;

        /// <summary>Grace once out, so the way out of it is not another hit.</summary>
        private const int ReleaseGrace = 40;

        public bool Held =>
            HeldBy >= 0
            && HeldBy < Main.maxNPCs
            && Main.npc[HeldBy].active
            && Main.npc[HeldBy].type == NPCID.PrimeVice
            && heldTicks > 0
            && !Player.dead;

        /// <summary>
        /// Held by this vice for the next few ticks, at a place, turned so, to be let go at this speed; spared what
        /// else would hurt them, or not. Asked every tick for as long as it has them.
        /// </summary>
        public void Hold(int npcIndex, Vector2 place, float rotation, Vector2 release, bool spare)
        {
            HeldBy = npcIndex;
            at = place;
            turn = rotation;
            letGo = release;
            spared = spare;
            heldTicks = 3;
        }

        /// <summary>Its jaws shut on them: its touch, once, through whatever would have dodged it (this player's own machine deals it).</summary>
        public void Clench(NPC vice)
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            Player.Hurt(PlayerDeathReason.ByNPC(vice.whoAmI), vice.damage, 0, cooldownCounter: -1, dodgeable: false, knockback: 0f);
        }

        public override void ResetEffects()
        {
            if (!Held)
            {
                if (HeldBy >= 0)
                    Release();

                HeldBy = -1;
                heldTicks = 0;
                return;
            }

            heldTicks--;

            // Every item, including the ones that do not go through the controls below.
            Player.noItems = true;
        }

        public override void SetControls()
        {
            if (!Held)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override void PreUpdateMovement()
        {
            if (!Held)
                return;

            // Nothing of their own holds them anywhere else.
            Player.RemoveAllGrapplingHooks();

            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.Center = at;
            Player.velocity = Vector2.Zero;
            Player.fullRotation = turn;
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole of it counts as a fall the moment it lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (spared)
            {
                Player.immune = true;
                Player.immuneNoBlink = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 2);
            }
        }

        /// <summary>Out: going the way it let them go, upright again, and a moment untouchable.</summary>
        private void Release()
        {
            Player.fullRotation = 0f;
            Player.velocity = letGo;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.immune = true;
            Player.immuneNoBlink = false;
            Player.immuneTime = Math.Max(Player.immuneTime, ReleaseGrace);
        }
    }
}
