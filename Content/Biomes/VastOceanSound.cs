using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using PisAndDag.Content.Subworlds;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// What is heard in the Vast Ocean's depths, by request ("after section 6 ... sound is also
    /// distorted and the music is distorted too like how it is realistic to a dive. After
    /// section 7 its pure quiet"), by where the player is (VastOcean's sections):
    ///
    ///   IN THE 7TH, as in a dive: every sound lower (two fifths of an octave down), quieter, and
    ///   muffled, its high end cut away as water cuts it (FNA's own low-pass filter on the sound's
    ///   voice, SoundEffectInstance.INTERNAL_applyLowPassFilter: FAudio's filter, and every voice
    ///   FNA makes is made to take one, SoundEffectInstance.Play). The music the same, where it
    ///   can be: a track played from a file (a resource pack's, a mod's) is lowered and muffled;
    ///   the game's own music is played through XACT (FAudio's own, not FNA's voices), which
    ///   takes neither here, so it is made quieter and swells and sinks slowly instead. The
    ///   game's ambient loops (rain and the like) at half.
    ///   FROM THE 8TH DOWN, nothing: no sound starts (SoundPlayer.Play), what was playing goes
    ///   silent, no ambient loop, and no music (the place asks for none, VastOcean.Music).
    ///
    /// But the sounds of the deep (Content/Sounds/Deep, <see cref="OfTheDeep"/>), by request
    /// ("Change the ambience for trench so its a eerie", "put much effort into the depth pressure
    /// system"): what is heard in the Abyssal Trench (VastOceanAmbience) and the player's own
    /// heart under the water's weight, and its crunch (VastOceanWeight). Those start and are
    /// heard at any depth, as they are made to be heard, neither lowered nor muffled here.
    ///
    /// Coming back up, everything is as the game has it again.
    /// </summary>
    public class VastOceanSound : ModSystem
    {
        /// <summary>How far down a sound goes in the 7th, in octaves, and how loud it is left.</summary>
        private const float Lower = -0.4f;
        private const float Muffled = 0.6f;

        /// <summary>The low-pass filter's cutoff in the 7th (XAudio2's measure: 2 sin(pi f / rate), so about 350 Hz), and its default, which lets everything by.</summary>
        private const float Cutoff = 0.05f;
        private const float Open = 1f;

        /// <summary>The music track's pitch in the 7th, in octaves.</summary>
        private const float MusicLower = -0.45f;

        private static MethodInfo lowPass;
        private static FieldInfo trackInstance;

        /// <summary>Where the sounds of the deep are: heard through the silence, and never muffled here.</summary>
        internal const string Deep = "PisAndDag/Content/Sounds/Deep/";

        /// <summary>The sounds and tracks muffled here, to be let go again.</summary>
        private static readonly ConditionalWeakTable<SoundEffectInstance, object> muffled = new();
        private static readonly HashSet<int> loweredTracks = new();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            lowPass = typeof(SoundEffectInstance).GetMethod("INTERNAL_applyLowPassFilter", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float) }, null);
            trackInstance = typeof(ASoundEffectBasedAudioTrack).GetField("_soundEffectInstance", BindingFlags.NonPublic | BindingFlags.Instance);

            if (lowPass == null)
                Mod.Logger.Warn("Vast Ocean: FNA's SoundEffectInstance.INTERNAL_applyLowPassFilter is not there. Sound in the 7th section will be lower and quieter, not muffled.");

            On_SoundPlayer.Play += NoSound;
            On_ActiveSound.Update += Heard;
            On_LegacyAudioSystem.UpdateCommonTrack += MusicHeard;
            On_LegacyAudioSystem.UpdateAmbientCueState += AmbientHeard;
        }

        public override void Unload()
        {
            lowPass = null;
            trackInstance = null;
            loweredTracks.Clear();
        }

        /// <summary>0 as the game has it, 1 as in a dive (the 7th section), 2 nothing (the 8th and under).</summary>
        internal static int Hearing()
        {
            if (Main.gameMenu || Main.dedServ)
                return 0;

            float row = VastOcean.RowOf(Main.LocalPlayer);

            return row >= VastOcean.QuietFrom ? 2 : row >= VastOcean.MurkFrom ? 1 : 0;
        }

        /// <summary>A sound of the deep: one of the mod's own from Content/Sounds/Deep.</summary>
        internal static bool OfTheDeep(in SoundStyle style) => style.SoundPath != null && style.SoundPath.StartsWith(Deep, StringComparison.Ordinal);

        private static SlotId NoSound(On_SoundPlayer.orig_Play orig, SoundPlayer self, ref SoundStyle style, Vector2? position, SoundUpdateCallback updateCallback)
        {
            if (Hearing() == 2 && !OfTheDeep(style))
                return SlotId.Invalid;

            return orig(self, ref style, position, updateCallback);
        }

        /// <summary>After the game has set a sound's loudness and pitch for this tick: as it is heard down here.</summary>
        private static void Heard(On_ActiveSound.orig_Update orig, ActiveSound self)
        {
            orig(self);

            // Off the game's own thread, the game has handed the update over to it and waited for
            // it (ActiveSound.Update): it was heard as it is down here there, and not again here.
            if (!Program.IsMainThread)
                return;

            SoundEffectInstance sound = self.Sound;

            // Gone, or stopped (its callback said so): nothing to hear.
            if (sound == null || sound.IsDisposed || sound.State == SoundState.Stopped)
                return;

            // A sound of the deep: as it is made to be heard, at any depth.
            if (OfTheDeep(self.Style))
            {
                if (muffled.TryGetValue(sound, out _))
                {
                    Filter(sound, Open);
                    muffled.Remove(sound);
                }

                return;
            }

            int hearing = Hearing();

            if (hearing == 2)
            {
                sound.Volume = 0f;
                return;
            }

            if (hearing == 1)
            {
                sound.Volume *= Muffled;
                sound.Pitch = MathHelper.Clamp(sound.Pitch + Lower, -1f, 1f);

                if (!muffled.TryGetValue(sound, out _))
                {
                    Filter(sound, Cutoff);
                    muffled.Add(sound, null);
                }

                return;
            }

            if (muffled.TryGetValue(sound, out _))
            {
                Filter(sound, Open);
                muffled.Remove(sound);
            }
        }

        /// <summary>The music's loudness as the game hands it to a track each tick: in the 7th, quieter and slowly swelling; under it, nothing.</summary>
        private static void MusicHeard(On_LegacyAudioSystem.orig_UpdateCommonTrack orig, LegacyAudioSystem self, bool active, int i, float totalVolume, ref float tempFade)
        {
            int hearing = Hearing();

            if (hearing == 1)
                totalVolume *= 0.45f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.6f);
            else if (hearing == 2)
                totalVolume = 0f;

            orig(self, active, i, totalVolume, ref tempFade);
        }

        private static void AmbientHeard(On_LegacyAudioSystem.orig_UpdateAmbientCueState orig, LegacyAudioSystem self, int i, bool gameIsActive, ref float trackVolume, float systemVolume)
        {
            int hearing = Hearing();

            orig(self, i, gameIsActive, ref trackVolume, hearing == 2 ? 0f : hearing == 1 ? systemVolume * 0.5f : systemVolume);
        }

        /// <summary>Every tick, on the player's own machine: the music track playing lowered and muffled in the 7th, where it is a file's; let go again out of it.</summary>
        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.audioSystem is not LegacyAudioSystem system || system.AudioTracks == null)
                return;

            bool diving = Hearing() == 1;
            int playing = Main.curMusic;

            foreach (int lowered in new List<int>(loweredTracks))
            {
                if (diving && lowered == playing)
                    continue;

                Track(system, lowered, 0f, Open);
                loweredTracks.Remove(lowered);
            }

            if (!diving || playing <= 0 || playing >= system.AudioTracks.Length)
                return;

            // Every tick while it plays: a track that starts over makes its voice anew.
            if (system.AudioTracks[playing] is ASoundEffectBasedAudioTrack)
            {
                Track(system, playing, MusicLower, Cutoff);
                loweredTracks.Add(playing);
            }
        }

        private static void Track(LegacyAudioSystem system, int index, float pitch, float cutoff)
        {
            if (index <= 0 || index >= system.AudioTracks.Length || system.AudioTracks[index] is not ASoundEffectBasedAudioTrack track)
                return;

            track.SetVariable("Pitch", pitch);

            if (trackInstance?.GetValue(track) is SoundEffectInstance instance && !instance.IsDisposed)
                Filter(instance, cutoff);
        }

        private static void Filter(SoundEffectInstance sound, float cutoff)
        {
            if (lowPass == null)
                return;

            try
            {
                lowPass.Invoke(sound, new object[] { cutoff });
            }
            catch (Exception)
            {
                // A voice FNA has let go of between the asking and the doing: nothing to filter.
            }
        }
    }
}
