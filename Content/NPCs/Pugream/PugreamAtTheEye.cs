using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// Pugream on the night Of The Eye: the man who loosed the arrow, standing a long
    /// way off, seen once, and gone before the player can get to him. He cannot be
    /// spoken to (that is PugreamStanding, later, on the shore). He is the scene's
    /// clock: EyeFinale runs from his AI, because the Eye is dead halfway through it
    /// and something has to still be there to finish the shot.
    ///
    /// ai[0] the Eye's index, ai[1] the player it was killing, ai[2] ticks into the
    /// scene, ai[3] which way he faces. Where the Eye was, and how it was moving, when
    /// the scene took it, are sent with him.
    ///
    /// He is placed, not walked: no gravity, no tiles. EyeFinale puts his feet on the
    /// ground when it can find some, and when it cannot, he stands where it says.
    /// </summary>
    public class PugreamAtTheEye : PugreamBody
    {
        public int EyeIndex => (int)NPC.ai[0];
        public int VictimIndex => (int)NPC.ai[1];

        public int Tick
        {
            get => (int)NPC.ai[2];
            set => NPC.ai[2] = value;
        }

        public int Faces => NPC.ai[3] < 0f ? -1 : 1;

        /// <summary>Where the Eye was when the scene took it, and how it was moving.</summary>
        public Vector2 From;
        public Vector2 FromVelocity;

        // Each machine's own, worked out as the scene reaches it.
        internal Vector2 KillPoint;
        internal Vector2 KillFocus;
        internal IchorArrowFlight Flight;
        internal bool Fired;
        internal bool Whistled;
        internal bool Struck;
        internal bool PannedHome;

        /// <summary>The Eye is counted beaten and he is owed to the shore. The server's.</summary>
        internal bool Finished;

        /// <summary>The wind is his whatever the depth: the scene sets it, and the scene wants it seen.</summary>
        protected override float Wind => Main.windSpeedCurrent;

        public override void SetDefaults()
        {
            base.SetDefaults();

            NPC.noGravity = true;
            NPC.noTileCollide = true;

            // He is a long way from everyone, and every client has to have him.
            NPC.netAlways = true;
        }

        /// <summary>He stays for the length Of The shot however far away everyone is. EyeFinale takes him away.</summary>
        public override bool CheckActive() => false;

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(From);
            writer.WriteVector2(FromVelocity);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            From = reader.ReadVector2();
            FromVelocity = reader.ReadVector2();
        }

        public override void AI()
        {
            Facing = Faces;
            NPC.direction = Facing;
            NPC.spriteDirection = Facing;
            NPC.velocity = Vector2.Zero;

            EyeFinale.Run(this);
        }
    }
}
