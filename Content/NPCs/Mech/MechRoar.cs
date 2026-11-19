using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The mechanical bosses' roar, by request: "a reverbed and echoed and slowed version of the original roar, (use this
    /// sound for all mech bosses)", and asked after, for the summoning only (the Twins' change keeps the game's roar).
    /// Content/Sounds/Mech/MechRoar.ogg: the game's own boss roar (Sounds/Roar_0) slowed as a tape slows (to 0.68, pitch and
    /// time together), echoed off four far walls (0.34, 0.71, 1.12 and 1.6 seconds, each quieter, darker and to one side)
    /// and set in a great hall (a reverb of four seconds); seven seconds in all (made in code: scratchpad mech/roar).
    ///
    /// The summoning items roar where they are used (Player.ItemCheck_UseBossSpawners: Mechanical Eye, Worm and Skull, by
    /// night). While one of them is used, the game's roar is this one instead; the Mechanical Worm's is no roar at all,
    /// as the Destroyer roars when it rises (Destroyer.Rise), for everyone, with this.
    /// </summary>
    public class MechRoar : ModSystem
    {
        public static readonly SoundStyle Sound = new("PisAndDag/Content/Sounds/Mech/MechRoar")
        {
            Volume = 1f,
            MaxInstances = 2,
        };

        /// <summary>The summoning item being used this moment (0 if none).</summary>
        private static int summoning;

        public override void Load()
        {
            On_Player.ItemCheck_UseBossSpawners += Summoning;
            On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback += Swap;
        }

        public override void Unload()
        {
            On_Player.ItemCheck_UseBossSpawners -= Summoning;
            On_SoundEngine.PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback -= Swap;
            summoning = 0;
        }

        private static void Summoning(On_Player.orig_ItemCheck_UseBossSpawners orig, Player self, int onWhichPlayer, Item sItem)
        {
            bool mech = sItem.type == ItemID.MechanicalEye || sItem.type == ItemID.MechanicalWorm || sItem.type == ItemID.MechanicalSkull;

            if (!mech)
            {
                orig(self, onWhichPlayer, sItem);
                return;
            }

            summoning = sItem.type;

            try
            {
                orig(self, onWhichPlayer, sItem);
            }
            finally
            {
                summoning = 0;
            }
        }

        private static SlotId Swap(On_SoundEngine.orig_PlaySound_refSoundStyle_Nullable1_SoundUpdateCallback orig, ref SoundStyle style, Vector2? position, SoundUpdateCallback updateCallback)
        {
            if (summoning == 0 || style.SoundPath != SoundID.Roar.SoundPath)
                return orig(ref style, position, updateCallback);

            if (summoning == ItemID.MechanicalWorm)
                return SlotId.Invalid;

            SoundStyle mech = Sound;
            return orig(ref mech, position, updateCallback);
        }
    }
}
