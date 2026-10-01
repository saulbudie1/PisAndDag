using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Being inside the Queen Slime (Legends: her dash, LegendsQueenSlime.Moves).
    ///
    /// Held in the middle of her, tipped as she moves, unable to move or use anything, and fifty
    /// life out of you a second through any armour, however long she keeps you (four seconds of
    /// her dash). She asks every tick (Hold); the tick she stops, you are out, thrown clear, with a
    /// moment's grace, as the King lets go (SlimeGrabPlayer).
    ///
    /// Her body is drawn over you, so you are seen inside her (LegendsQueenSlime.DrawHolding).
    /// </summary>
    public class QueenSlimeGrab : ModPlayer
    {
        /// <summary>The Queen holding this player, or -1.</summary>
        public int HeldBy = -1;

        private int heldTicks;
        private int drain;

        /// <summary>Taken each second, through armour.</summary>
        public const int DrainPerSecond = 50;

        /// <summary>Grace once out, so the way out of her is not another hit.</summary>
        private const int ReleaseGrace = 60;

        /// <summary>
        /// The drain's own immunity slot: nothing else's immunity stops it and it gives none, so
        /// every second of it lands, and whatever else hits in the meantime is judged as ever.
        /// </summary>
        private const int DrainSlot = ImmunityCooldownID.WrongBugNet;

        public bool Held =>
            HeldBy >= 0
            && HeldBy < Main.maxNPCs
            && Main.npc[HeldBy].active
            && Main.npc[HeldBy].type == NPCID.QueenSlimeBoss
            && heldTicks > 0
            && !Player.dead;

        /// <summary>Held by this Queen for the next few ticks. Asked every tick for as long as she has them.</summary>
        public void Hold(int npcIndex)
        {
            if (HeldBy != npcIndex || heldTicks <= 0)
            {
                HeldBy = npcIndex;
                drain = 0;
            }

            heldTicks = 3;
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

            NPC queen = Main.npc[HeldBy];

            // Behind her movement, as a thing in jelly is, and turning with it; her gel's own
            // slow working round them on top.
            Vector2 lag = new Vector2(-queen.velocity.X * 1.5f, -queen.velocity.Y * 0.8f);
            float working = (float)Math.Sin(Main.GameUpdateCount * 0.08f);

            Player.Center = queen.Center + new Vector2(0f, 6f + 2f * working) + lag;
            Player.velocity = Vector2.Zero;

            Player.fullRotation = MathHelper.Clamp(queen.velocity.X * 0.05f, -0.4f, 0.4f) + 0.08f * working;
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole of her flight counts as a fall the moment she lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (++drain < 60)
                return;

            drain = 0;

            // The player's own client deals it, as vanilla deals contact damage; Hurt tells the rest.
            if (Player.whoAmI != Main.myPlayer)
                return;

            Player.hurtCooldowns[DrainSlot] = 0;
            Player.Hurt(PlayerDeathReason.ByNPC(HeldBy), DrainPerSecond, 0, cooldownCounter: DrainSlot, dodgeable: false, scalingArmorPenetration: 1f, knockback: 0f);
        }

        /// <summary>Out: thrown clear of her, upright again, and a moment untouchable.</summary>
        private void Release()
        {
            Player.fullRotation = 0f;

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, ReleaseGrace);

            if (HeldBy >= 0 && HeldBy < Main.maxNPCs && Main.npc[HeldBy].active)
            {
                NPC queen = Main.npc[HeldBy];
                Vector2 away = (Player.Center - queen.Center).SafeNormalize(new Vector2(queen.direction == 0 ? 1 : queen.direction, 0f));

                Player.velocity = away * 7f + new Vector2(0f, -6f);
            }
        }
    }
}
