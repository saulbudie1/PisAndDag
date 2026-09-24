using PisAndDag.Content.Combat;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The Wall's arrival.
    ///
    /// It comes after the Guide (GuideSacrifice): the doll burns, the camera goes to
    /// him, he smoulders and comes apart, the shot walks home — and on the frame it is
    /// home the Wall is spawned FAR away (GuideSacrifice.FarSpot). Then:
    ///
    ///     1s     pan to it, a small lean in — it is already taller than the screen
    ///     7s     the plate: a look, the name and its line rise, sit to be read, fly down, and on the
    ///            tick the name touches the bar the Wall roars and the ground goes
    ///     1s     home
    ///
    /// No hold shake. The only shake is the roar's, so the roar is the whole event.
    /// The spawn distance is worked out from Ticks below and the Wall's speed, so that
    /// when the camera is back on the player the Wall is still half a screen off.
    ///
    /// The line under the name is Lore Bible §XXVII: what the houses built the lock out
    /// of. It reads as nonsense the first time and as an explanation the moment the
    /// player watches three things spread through the world at once.
    /// </summary>
    public class WallOfFleshIntro : ModSystem
    {
        public const string Name = "Wall of Flesh";
        public const string Title = "The seal written in blood.";

        private const int Pan = 60;
        private const int Return = 60;

        /// <summary>The push in. Small: the Wall fills the screen already.</summary>
        private const float Zoom = 1.1f;

        /// <summary>Spawn to camera-home. What GuideSacrifice budgets the Wall's approach against. It has a line, so the seven-second plate.</summary>
        public const int Ticks = Pan + BossIntro.TitlePlateTicks + Return;

        // ---- the roar --------------------------------------------------------------

        /// <summary>
        /// The roar's length, in ticks (Cinematic.Roar). The King's eighty for the same
        /// sound at -0.4, lengthened as the lower pitch lengthens it: 80 x 2^0.15.
        /// </summary>
        private const int RoarLength = 89;

        /// <summary>The sound the old Breach used, so the Wall keeps the voice it had.</summary>
        private const float RoarVolume = 1f;
        private const float RoarPitch = -0.55f;

        public override void PostSetupContent()
        {
            BossPlate.Register(NPCID.WallofFlesh, Name, Title, new BossIntroProfile
            {
                PanTicks = Pan,
                ReturnTicks = Return,
                ZoomIn = Zoom,
                HoldShake = 0f,
                OnTouchdown = Roar,
            });

            // The Wall keeps coming through its own arrival. Its spawn distance is
            // budgeted (GuideSacrifice.FarSpot) so that it is still half a screen off
            // when the camera is home; held still, that budget would be wrong.
            BossPlate.NoWait(NPCID.WallofFlesh);
        }

        /// <summary>
        /// The name lands and the Wall roars (Cinematic.Roar): the rings out of its mouth,
        /// the dim, the shake, in its own voice and from where it is. The camera is on it
        /// at this tick, so it is felt and heard in full. If it is somehow gone, the voice
        /// alone, where the player is.
        /// </summary>
        private static void Roar()
        {
            if (Main.dedServ)
                return;

            SoundStyle voice = SoundID.ForceRoar with { Volume = RoarVolume, Pitch = RoarPitch };

            int wall = NPC.FindFirstNPC(NPCID.WallofFlesh);
            if (wall >= 0)
                Cinematic.Roar(Main.npc[wall], RoarLength, 1f, voice);
            else
                SoundEngine.PlaySound(voice, Main.LocalPlayer.Center);
        }
    }
}
