using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// An enemy in the Mantrap's jaws (MantrapJaws). For as long as the jaws say they hold it
    /// (every tick, from their own AI on every machine) its own AI is not run and nothing of its own
    /// moves it; the game still lets it fall. It can still hurt whoever touches it: it is caught by
    /// the leg, not stunned. Its AI is stopped, not replaced, so it goes on from where it was when
    /// the jaws let go (the Meat Hook's way, MeatHookNPC).
    /// </summary>
    public class MantrapNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Ticks of the jaws' hold left: two, said again every tick they hold.</summary>
        private int held;

        public bool Held => held > 0;

        /// <summary>The jaws hold it this tick.</summary>
        public void Hold() => held = 2;

        public override bool PreAI(NPC npc) => held <= 0;

        public override void PostAI(NPC npc)
        {
            if (held <= 0)
                return;

            held--;

            // Nothing of its own moves it. The game adds the fall after this, to one with weight.
            npc.velocity = new Vector2(0f, npc.noGravity ? 0f : -npc.gravity);
        }
    }

    /// <summary>
    /// A player with a foot in their own Mantrap: for <see cref="Held"/> ticks they cannot walk, jump,
    /// drop through, hook or mount. Set on the owner's own machine, where the jaws find the foot.
    /// </summary>
    public class MantrapSnare : ModPlayer
    {
        public int Held;

        public void Snare(int ticks)
        {
            if (ticks > Held)
                Held = ticks;
        }

        public override void SetControls()
        {
            if (Held <= 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
            Player.controlDown = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void PreUpdateMovement()
        {
            if (Held > 0)
                Player.velocity.X = 0f;
        }

        public override void PostUpdate()
        {
            if (Held > 0)
                Held--;
        }

        public override void UpdateDead()
        {
            Held = 0;
        }
    }
}
