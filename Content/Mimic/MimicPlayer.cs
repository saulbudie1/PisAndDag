using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The Everclear Mirror's, on both sides of it.
    ///
    /// On the copy (MimicSystem): its life three times its owner's, worked out last of all (after every accessory, set
    /// and buff), and full the first time it is known; no hurt from a fall; at no life it does not die as a player
    /// dies, it breaks (<see cref="Broken"/>, which MimicSystem answers); the faint trail of itself it leaves when it
    /// moves (the game's own, as some armour leaves); and the jump the game gave it this tick, for its brain.
    ///
    /// On its owner: how long the mirror stays clouded after a copy broke (<see cref="Cloud"/>).
    /// </summary>
    public class MimicPlayer : ModPlayer
    {
        /// <summary>The copy's life, to its owner's (by request: "its HP is Triple player's HP").</summary>
        public const int LifeTimes = 3;

        /// <summary>The copy, just made: its life filled once it is known.</summary>
        internal bool Fresh;

        /// <summary>The copy's life ran out.</summary>
        internal bool Broken;

        /// <summary>The jump the game gave the copy this tick (Player.jumpSpeed and jumpHeight, the game's, set for each player as it is run).</summary>
        internal float JumpSpeed = 5.01f;

        internal int JumpTicks = 15;

        /// <summary>The owner's: ticks the mirror stays clouded.</summary>
        internal int Cloud;

        /// <summary>The owner's: the last thing they hit, and when (the copy goes for it too).</summary>
        internal int LastHit = -1;
        internal uint LastHitAt;

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Hit(target, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) => Hit(target, damageDone);

        /// <summary>A hit landed: the copy's teaches it its weapon (MimicSystem.Landed); its owner's tells the copy what they are fighting.</summary>
        private void Hit(NPC target, int damageDone)
        {
            if (MimicSystem.Is(Player))
            {
                MimicSystem.Landed(target, damageDone);
                return;
            }

            LastHit = target.whoAmI;
            LastHitAt = Main.GameUpdateCount;
        }

        /// <summary>The copy stood on the Empress's floor last tick (her walls hold it as they hold its owner).</summary>
        private bool walledFloor;

        /// <summary>
        /// The copy held inside the Empress's arena as its owner is: her arena holds the player at the keyboard alone
        /// (EmpressArenaPlayer), and the copy flies in there on its owner's favour (MimicSystem.Buffs). While her walls
        /// are up and it is inside them, out of her hand, nothing of its speed carries it through an edge, and it stands
        /// on her floor (Decagon.HoldInside, as for its owner).
        /// </summary>
        public override void PreUpdateMovement()
        {
            if (!MimicSystem.Is(Player))
                return;

            if (Player.dead || !EmpressArena.Up || !EmpressArena.Holds || Player.GetModPlayer<EmpressHeld>().Held
                || !Decagon.Inside(Player.Center, EmpressArena.Centre, EmpressArena.Size))
            {
                walledFloor = false;
                return;
            }

            Player.velocity = Decagon.HoldInside(Player.Center, Player.Size * 0.5f, Player.velocity, EmpressArena.Centre, EmpressArena.Size, walledFloor, out walledFloor);
        }

        public override void PostUpdateMiscEffects()
        {
            if (!MimicSystem.Is(Player))
                return;

            Player.statLifeMax2 *= LifeTimes;
            Player.noFallDmg = true;

            if (Fresh)
            {
                Player.statLife = Player.statLifeMax2;
                Player.statMana = Player.statManaMax2;
                Fresh = false;
            }
        }

        public override void PostUpdate()
        {
            if (MimicSystem.Is(Player))
            {
                JumpSpeed = Terraria.Player.jumpSpeed;
                JumpTicks = Terraria.Player.jumpHeight;
                return;
            }

            if (Cloud > 0)
                Cloud--;
        }

        public override void FrameEffects()
        {
            if (MimicSystem.Is(Player))
                Player.armorEffectDrawShadow = true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (!MimicSystem.Is(Player))
                return true;

            Broken = true;
            playSound = false;
            genDust = false;
            return false;
        }
    }
}
