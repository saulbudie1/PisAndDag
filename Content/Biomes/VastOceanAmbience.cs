using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// What is heard in the Vast Ocean besides the game's own sounds, by request ("added a 5 min
    /// long surf sample and 4 min long wind, please dissect it and retune it and play randomly a
    /// piece from that for the ambiance with variations, diff distances, Yes duke's long, low
    /// roar can be good. Added whale hums too, theres a 10 min long piece do the same.").
    ///
    /// THE PIECES (Content/Sounds/Ambiance/Pieces), cut from the user's recordings beside them,
    /// each where the sound was quietest between one wave, gust or song and the next, so that each
    /// is one whole: Surf1 to Surf32 (6.6 to 13.3 seconds each), Wind1 to Wind13 (13.6 to 18.2),
    /// and Hum1 to Hum23: eighteen songs from the long recording (8.1 to 14.3 seconds) and the
    /// five short calls as they were given (Whale1 to Whale5, now Hum19 to Hum23). Each was
    /// cleared of the rumble under it and evened out (the surf a little less bright, the songs
    /// cut above 3.5 kHz), faded in and out, and made as loud as the rest of its kind; each is
    /// played as loud again as half the difference it had in the recording, so the calm stays
    /// calmer than the rough. Each is marked by what it is: the surf calm, middling or rough, the
    /// wind soft or strong, a hum a song or a call (<see cref="Surf"/>, <see cref="Wind"/>,
    /// <see cref="Hums"/>).
    ///
    /// HOW THEY ARE PLAYED, a piece at a time, chosen at random among those of the kind the moment
    /// asks for and not one of the last few played, each at its own pitch (a little higher or
    /// lower, so no two hearings of a piece are the same) and from a place of its own:
    ///   - THE SURF along the water's surface, two pieces at once and three in rough weather, the
    ///     next begun as one draws to its end so they run into each other: each from a point on
    ///     the surface a little way off to one side (a third of them), further off (two fifths),
    ///     or far away (the rest), fixed there as the player moves; the further, the duller.
    ///     Rougher pieces the rougher the weather (wind, and rain). Heard to 160 rows over the
    ///     water and 120 under it, muffled at once under it as water muffles what is over it.
    ///   - THE WIND, one piece at once and two in a strong wind, from where the wind comes and
    ///     going by on the way it blows, over the length of the piece; stronger pieces and a
    ///     higher pitch the stronger it blows; from the waterline up, louder the higher. The game
    ///     has a wind of its own over the surface from 20 mph (Main.UpdateAudio): there this one
    ///     gives way a little to it.
    ///   - THE WHALES, under the water only, from six rows down: now and then (every 14 to 40
    ///     seconds) a song, or one time in four a call, mostly far off (more than half of them
    ///     at the edge of hearing), some nearer, now and then near, from anywhere about and below;
    ///     lower the deeper the listener; and one time in three another answers from the other
    ///     side a few seconds after.
    ///   - THE DUKE: every two and a half to six minutes, while no Duke Fishron is about, a long,
    ///     low roar from far down in the sea: the game's own roar of his (SoundID.Zombie20, as he
    ///     roars, NPC.AI_069_DukeFishron) an octave down, three times over a second and a half,
    ///     each swelling in, as one long roar rolling out of the dark; heard in the water, and
    ///     faintly over it.
    ///
    /// THE TRENCH, by request ("Change the ambience for trench so its a eerie; long version of
    /// the whale sounds. And some clicking too so its like rocks clashing under the mantle"): in
    /// the Abyssal Trench only (the 10th section down, swelling in over its first thirty rows,
    /// by the player's middle as the biome goes), through the silence that holds from the 8th
    /// (they are sounds of the deep, Content/Sounds/Deep/Trench, VastOceanSound):
    ///   - THE LONG SONGS: the whales above made long and low (Song1 to Song11, 17 to 48
    ///     seconds: eight of the songs three times their length, half as long again by being
    ///     slowed, seven semitones down, and twice that again at that pitch; three of the calls
    ///     four times theirs, an octave down; an octave under each laid in quietly; the sea's bed
    ///     under them thinned, a slow swell, the highs cut, a long dark echo). One every 28 to 70
    ///     seconds, so that two now and then overlap; mostly far, from anywhere below.
    ///   - THE ROCKS: knocks, clacks, a grinding, a crack (Rock1 to Rock10, 3.6 to 8.2 seconds,
    ///     made of damped tones and noise in a great dark echo, nothing recorded): one every 2.5
    ///     to 10 seconds from somewhere under the listener, near or far, lower or higher, and one
    ///     time in four another close to it a moment after.
    ///
    /// All of it at the game's Ambient volume (Settings), heard as the game hears any sound from
    /// where it is (ActiveSound: quieter the further, to nothing at 2500 pixels, and to the left
    /// or right of the screen); paused with the game; in the 7th section as in a dive, and from
    /// the 8th not at all (VastOceanSound), but the trench's. The pieces are loaded in the
    /// background as the place is come into (about 34 MB as they play), the trench's the first
    /// time the player goes down past the top of the 8th section (about 21 MB more), and each is
    /// played only once it is in.
    /// </summary>
    public class VastOceanAmbience : ModSystem
    {
        private const string Folder = "PisAndDag/Content/Sounds/Ambiance/Pieces/";

        /// <summary>The trench's, among the sounds of the deep (heard through the silence, VastOceanSound).</summary>
        private const string TrenchFolder = VastOceanSound.Deep + "Trench/";

        /// <summary>What a voice is. The trench's two are the last, from <see cref="SongVoice"/> on.</summary>
        private const int SurfVoice = 0, WindVoice = 1, HumVoice = 2, RoarVoice = 3, SongVoice = 4, RockVoice = 5;

        /// <summary>
        /// A kind of piece: the stem of their names (Surf1, Surf2...), their lengths in seconds,
        /// what each is (0, 1, 2: calm, middling, rough; the wind soft or strong; a hum a song or a
        /// call), how loud each is played, and how many of the last played are not played again.
        /// </summary>
        private sealed class Kind
        {
            public readonly string Where;
            public readonly string Stem;
            public readonly float[] Seconds;
            public readonly byte[] Class;
            public readonly float[] Gain;
            public Asset<SoundEffect>[] Assets;
            public SoundStyle[] Styles;
            private readonly int[] recent;
            private int recentAt;

            public Kind(string stem, float[] seconds, byte[] classes, float[] gain, int remember, string where = Folder)
            {
                Where = where;
                Stem = stem;
                Seconds = seconds;
                Class = classes;
                Gain = gain;
                recent = new int[remember];
                Array.Fill(recent, -1);
            }

            public int Count => Seconds.Length;

            /// <summary>Every piece asked for in the background; a piece not there is never played.</summary>
            public void Request()
            {
                Assets = new Asset<SoundEffect>[Count];
                Styles = new SoundStyle[Count];

                for (int i = 0; i < Count; i++)
                {
                    string path = Where + Stem + (i + 1);

                    Styles[i] = new SoundStyle(path, SoundType.Ambient) { MaxInstances = 0, PauseBehavior = PauseBehavior.PauseWithGame };

                    if (!ModContent.RequestIfExists(path, out Assets[i], AssetRequestMode.AsyncLoad))
                        Assets[i] = null;
                }
            }

            public void Forget()
            {
                Assets = null;
                Styles = null;
            }

            /// <summary>A piece loaded and ready, by the weight given its class, not one of the last few played if any other will do; -1 if none is in yet.</summary>
            public int Pick(float[] classWeight)
            {
                if (Assets == null)
                    return -1;

                for (int pass = 0; pass < 2; pass++)
                {
                    float total = 0f;

                    for (int i = 0; i < Count; i++)
                        total += Weight(i, classWeight, pass == 0);

                    if (total <= 0f)
                        continue;

                    float roll = Main.rand.NextFloat() * total;

                    for (int i = 0; i < Count; i++)
                    {
                        roll -= Weight(i, classWeight, pass == 0);

                        if (roll < 0f)
                            return i;
                    }
                }

                return -1;
            }

            private float Weight(int i, float[] classWeight, bool notRecent)
            {
                if (Assets[i] == null || !Assets[i].IsLoaded)
                    return 0f;

                if (notRecent && Array.IndexOf(recent, i) >= 0)
                    return 0f;

                return classWeight[Class[i]];
            }

            public void Played(int piece)
            {
                recent[recentAt] = piece;
                recentAt = (recentAt + 1) % recent.Length;
            }
        }

        /// <summary>The surf: lengths, calm (0), middling (1) or rough (2), and loudness as played.</summary>
        private static readonly Kind Surf = new Kind("Surf",
            new[] { 10.300f, 11.600f, 12.900f, 7.850f, 8.800f, 11.450f, 11.900f, 8.600f, 7.350f, 10.700f, 11.100f, 6.950f, 8.850f, 7.700f, 7.150f, 10.850f,
                    12.750f, 8.550f, 10.050f, 9.550f, 13.300f, 6.600f, 9.750f, 10.200f, 10.600f, 10.500f, 8.800f, 7.150f, 8.100f, 10.350f, 8.700f, 7.450f },
            new byte[] { 2, 1, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 1, 1, 0, 1, 1, 1, 2, 2, 2, 2, 1, 2, 0, 1, 2, 2, 1, 2 },
            new[] { 0.871f, 0.813f, 0.944f, 0.790f, 0.813f, 0.754f, 0.704f, 0.759f, 0.737f, 0.653f, 0.754f, 0.585f, 0.776f, 0.832f, 0.808f, 0.827f,
                    0.772f, 0.822f, 0.818f, 0.822f, 0.972f, 0.861f, 0.972f, 0.881f, 0.804f, 0.866f, 0.729f, 0.804f, 0.912f, 1.000f, 0.827f, 0.928f },
            6);

        /// <summary>The wind: lengths, soft (0) or strong (1), and loudness as played.</summary>
        private static readonly Kind Wind = new Kind("Wind",
            new[] { 18.150f, 16.650f, 17.700f, 16.550f, 15.850f, 16.950f, 17.300f, 17.450f, 14.250f, 16.000f, 14.350f, 13.600f, 17.350f },
            new byte[] { 0, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1, 0 },
            new[] { 0.804f, 0.881f, 0.837f, 0.933f, 1.000f, 0.955f, 0.776f, 0.891f, 0.896f, 0.684f, 0.886f, 0.961f, 0.837f },
            3);

        /// <summary>The whales: lengths, a song (0) or a call (1); all as loud.</summary>
        private static readonly Kind Hums = new Kind("Hum",
            new[] { 11.795f, 9.546f, 10.145f, 13.444f, 8.796f, 9.146f, 12.744f, 9.995f, 10.595f, 9.296f, 10.995f, 8.496f, 8.146f, 12.095f, 14.344f, 8.946f,
                    11.545f, 9.696f, 3.700f, 3.127f, 3.147f, 6.420f, 7.838f },
            new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 },
            new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f },
            6);

        /// <summary>The trench's long songs: lengths, a song (0) or a call (1); all as loud.</summary>
        private static readonly Kind Songs = new Kind("Song",
            new[] { 44.832f, 47.531f, 42.733f, 40.784f, 39.884f, 39.134f, 37.485f, 36.285f, 17.088f, 30.180f, 35.852f },
            new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 },
            new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f },
            4, TrenchFolder);

        /// <summary>The rocks under the mantle: lengths, and loudness as played (each brought to the quietest as it is heard: by its loudest tenth of a second and its peak together, as clicks are heard).</summary>
        private static readonly Kind Rocks = new Kind("Rock",
            new[] { 4.076f, 3.638f, 5.733f, 3.823f, 6.565f, 7.263f, 7.017f, 4.811f, 8.234f, 7.519f },
            new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 1.000f, 0.521f, 0.517f, 0.821f, 0.516f, 0.569f, 0.518f, 0.540f, 0.453f, 0.524f },
            4, TrenchFolder);

        /// <summary>One sound being played here, and how it is to be heard this tick.</summary>
        private sealed class Voice
        {
            public int What;
            public SlotId Slot;

            /// <summary>Where it is in the world; for the wind, where it is from the ear as it begins.</summary>
            public Vector2 Where;

            /// <summary>The wind's way past the ear, over its length.</summary>
            public Vector2 Drift;

            public float Gain;

            /// <summary>Its length as played, at its pitch.</summary>
            public float Seconds;
            public uint Born;

            /// <summary>Seconds it swells in over (the roar); none for the rest.</summary>
            public float Swell;

            public float Level;
            public float Cutoff = 1f;
            public float Fade = 1f;
            public bool LetGo;
            public SoundUpdateCallback Callback;

            /// <summary>Which stay in the place it was begun in (<see cref="stay"/>): a sound of an earlier one is stopped.</summary>
            public int Stay;

            public float Age => (ticks - Born) / 60f;
        }

        /// <summary>Something to be played a moment from now: a whale's answer, or the roar's later two.</summary>
        private struct Later
        {
            public uint Due;
            public int What;
            public Vector2 Where;
            public float Pitch;
            public float Gain;
            public int Side;
        }

        private static readonly List<Voice> voices = new();
        private static readonly List<Later> later = new();

        /// <summary>FNA's own low-pass filter on a sound's voice (as VastOceanSound uses it), taken once.</summary>
        private static Action<SoundEffectInstance, float> lowPass;

        private static bool requested;
        private static bool trenchRequested;
        private static bool running;

        /// <summary>Counted up each time the place is left: a sound begun in an earlier stay there stops (<see cref="Heard"/>).</summary>
        private static int stay;

        /// <summary>Ticks of the game in the place, and when each kind is next begun.</summary>
        private static uint ticks;
        private static uint nextSurf, nextWind, nextHum, nextRoar, nextSong, nextRock;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            MethodInfo method = typeof(SoundEffectInstance).GetMethod("INTERNAL_applyLowPassFilter", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(float) }, null);

            if (method != null)
                lowPass = (Action<SoundEffectInstance, float>)Delegate.CreateDelegate(typeof(Action<SoundEffectInstance, float>), method, false);

            if (lowPass == null)
                Mod.Logger.Warn("Vast Ocean: FNA's SoundEffectInstance.INTERNAL_applyLowPassFilter is not there. Far and underwater sounds will not be muffled.");
        }

        public override void Unload()
        {
            Quiet();
            lowPass = null;
            requested = false;
            trenchRequested = false;
            Surf.Forget();
            Wind.Forget();
            Hums.Forget();
            Songs.Forget();
            Rocks.Forget();
        }

        public override void OnWorldUnload() => Quiet();

        /// <summary>Everything of this let go: each sound stops the next time the game looks at it (its callback says so).</summary>
        private static void Quiet()
        {
            running = false;
            stay++;
            voices.Clear();
            later.Clear();
            ticks = 0;
            nextSurf = nextWind = nextHum = nextRoar = nextSong = nextRock = 0;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            if (SubworldKeeper.Current is not VastOcean || Main.gameMenu)
            {
                if (running)
                    Quiet();

                return;
            }

            if (!requested)
            {
                requested = true;
                Surf.Request();
                Wind.Request();
                Hums.Request();
            }

            // The trench's only once they may soon be wanted: past the top of the 8th.
            if (!trenchRequested && VastOcean.RowOf(Main.LocalPlayer) >= VastOcean.QuietFrom)
            {
                trenchRequested = true;
                Songs.Request();
                Rocks.Request();
            }

            if (!running)
            {
                running = true;
                ticks = 1;
                nextSurf = 1;
                nextWind = 30;
                nextHum = Seconds(6f, 15f);
                nextRoar = Seconds(60f, 180f);
                nextSong = Seconds(3f, 8f);
                nextRock = Seconds(1f, 3f);
            }

            ticks++;

            Vector2 ear = Main.Camera.Center;
            float above = VastOcean.Surface + 0.5f - ear.Y / 16f;
            float under = -above;
            int hearing = VastOceanSound.Hearing();

            // The weather: how hard the wind blows, and how rough the sea is (by the wind and the rain, as it is seen: VastOceanSky).
            float wind = Math.Abs(Main.windSpeedCurrent);
            float windy = Smooth(0.15f, 0.75f, wind);
            float rough = VastOceanSky.Rough();

            // How much of each is heard from where the ear is.
            float surfHeard = above >= 0f ? 1f - Smooth(12f, 160f, above) : 1f - Smooth(2f, 120f, under);
            float windHeard = Smooth(-3f, 4f, above) * (0.45f + 0.55f * Smooth(0f, 220f, above));
            float deepHeard = Smooth(6f, 40f, under);
            float roarHeard = under > 0f ? 0.4f + 0.6f * deepHeard : 0.4f * (1f - Smooth(10f, 120f, above));

            if (hearing == 2)
                surfHeard = windHeard = deepHeard = roarHeard = 0f;

            // The trench's, by the player's middle as the biome goes, swelling in over its first rows.
            float trenchHeard = Smooth(VastOcean.AbyssalTrenchFrom - 6f, VastOcean.AbyssalTrenchFrom + 30f, VastOcean.RowOf(Main.LocalPlayer));

            // The game's own wind, over the surface line and half a screen, from 20 mph (Main.UpdateAudio): this one gives way to it a little.
            bool gameWind = wind >= 0.4f && Main.LocalPlayer.position.Y < Main.worldSurface * 16.0 + Main.screenHeight / 2;
            float giveWay = gameWind ? 1f - 0.35f * Smooth(0.4f, 0.55f, wind) : 1f;

            // Over the water from under it: muffled at once, more so the deeper.
            float underwater = under > 0f ? MathHelper.Lerp(0.5f, 0.06f, Smooth(0f, 12f, under)) : 1f;

            for (int i = voices.Count - 1; i >= 0; i--)
            {
                Voice v = voices[i];

                if (!SoundEngine.TryGetActiveSound(v.Slot, out _))
                {
                    voices.RemoveAt(i);
                    continue;
                }

                if (v.LetGo)
                    v.Fade -= 1f / 30f;

                float far = Vector2.Distance(v.What == WindVoice ? ear + v.Where : v.Where, ear);

                switch (v.What)
                {
                    case SurfVoice:
                        v.Level = surfHeard * (0.7f + 0.3f * rough);
                        v.Cutoff = Math.Min(MathHelper.Lerp(1f, 0.22f, Smooth(300f, 2300f, far)), underwater);

                        // Left far behind (a fast mount): let go, and another begun nearer.
                        if (Math.Abs(v.Where.X - ear.X) > 2600f)
                            v.LetGo = true;

                        break;

                    case WindVoice:
                        v.Level = windHeard * (0.45f + 0.55f * windy) * giveWay;
                        v.Cutoff = underwater;
                        break;

                    case HumVoice:
                        v.Level = deepHeard * 0.9f;
                        v.Cutoff = MathHelper.Lerp(1f, 0.3f, Smooth(300f, 2300f, far));
                        break;

                    case SongVoice:
                        v.Level = trenchHeard * 0.9f;
                        v.Cutoff = MathHelper.Lerp(0.6f, 0.2f, Smooth(400f, 2300f, far));
                        break;

                    case RockVoice:
                        v.Level = trenchHeard;
                        v.Cutoff = MathHelper.Lerp(1f, 0.28f, Smooth(250f, 2000f, far));
                        break;

                    default:
                        v.Level = roarHeard;
                        v.Cutoff = under > 0f ? 0.16f : 0.08f;
                        break;
                }

                if (v.Level <= 0.001f && !v.LetGo)
                    v.LetGo = true;
            }

            // The surf: two at once, three when it is rough, the next as one draws to its end.
            int surfWanted = surfHeard > 0.02f ? (rough > 0.55f ? 3 : 2) : 0;

            if (Going(SurfVoice) < surfWanted && ticks >= nextSurf && StartSurf(ear, rough))
                nextSurf = ticks + Seconds(0.6f, 2.2f);

            // The wind: one, two when it blows hard.
            int windWanted = windHeard > 0.02f ? (windy > 0.5f ? 2 : 1) : 0;

            if (Going(WindVoice) < windWanted && ticks >= nextWind && StartWind(windy))
                nextWind = ticks + Seconds(1.5f, 4f);

            // The whales, now and then, and one answering now and then.
            if (ticks >= nextHum)
            {
                nextHum = ticks + Seconds(14f, 40f);

                if (deepHeard > 0.05f && hearing < 2)
                {
                    int side = Main.rand.NextBool() ? 1 : -1;

                    StartHum(ear, under, side, Main.rand.NextFloat() < 0.25f ? 1 : 0);

                    if (Main.rand.NextFloat() < 0.3f)
                        later.Add(new Later { Due = ticks + Seconds(2.5f, 7f), What = HumVoice, Side = -side });
                }
            }

            // The Duke's roar, now and then, while he is not about.
            if (ticks >= nextRoar)
            {
                if (roarHeard > 0.05f && hearing < 2 && !NPC.AnyNPCs(NPCID.DukeFishron))
                {
                    nextRoar = ticks + Seconds(150f, 360f);

                    Vector2 where = ear + new Vector2((Main.rand.NextBool() ? 1 : -1) * Main.rand.NextFloat(300f, 900f), Main.rand.NextFloat(1300f, 1800f));

                    StartRoar(where, -1f, 1f);
                    later.Add(new Later { Due = ticks + 42, What = RoarVoice, Where = where, Pitch = -0.9f, Gain = 0.75f });
                    later.Add(new Later { Due = ticks + 90, What = RoarVoice, Where = where, Pitch = -1f, Gain = 0.5f });
                }
                else
                {
                    nextRoar = ticks + Seconds(20f, 40f);
                }
            }

            // The trench: a long song now and then, the rocks often, one time in four two close together.
            if (ticks >= nextSong)
            {
                nextSong = ticks + Seconds(28f, 70f);

                if (trenchHeard > 0.05f)
                    StartSong(ear);
            }

            if (ticks >= nextRock)
            {
                nextRock = ticks + Seconds(2.5f, 10f);

                if (trenchHeard > 0.05f && StartRock(ear, out Vector2 rock) && Main.rand.NextFloat() < 0.25f)
                    later.Add(new Later { Due = ticks + Seconds(0.4f, 1.6f), What = RockVoice, Where = rock + new Vector2(Main.rand.NextFloat(-220f, 220f), Main.rand.NextFloat(-120f, 120f)) });
            }

            for (int i = later.Count - 1; i >= 0; i--)
            {
                Later next = later[i];

                if (ticks < next.Due)
                    continue;

                later.RemoveAt(i);

                if (next.What == RockVoice)
                {
                    if (trenchHeard > 0.05f)
                        StartRock(next.Where);
                }
                else if (next.What == HumVoice)
                {
                    if (deepHeard > 0.05f && hearing < 2)
                        StartHum(ear, under, next.Side, Main.rand.NextFloat() < 0.25f ? 1 : 0);
                }
                else if (roarHeard > 0.05f && hearing < 2)
                {
                    StartRoar(next.Where, next.Pitch, next.Gain);
                }
            }
        }

        /// <summary>How many of a kind are playing and not yet near their end (the last quarter), nor let go.</summary>
        private static int Going(int what)
        {
            int count = 0;

            foreach (Voice v in voices)
            {
                if (v.What == what && !v.LetGo && v.Age < v.Seconds * 0.75f)
                    count++;
            }

            return count;
        }

        private static bool StartSurf(Vector2 ear, float rough)
        {
            int piece = Surf.Pick(new[] { MathHelper.Lerp(1f, 0.1f, rough), 0.7f, MathHelper.Lerp(0.12f, 1.2f, rough) });

            if (piece < 0)
                return false;

            // A third a little way off, two fifths further, the rest far: each from a point on the surface to one side.
            float band = Main.rand.NextFloat();
            float far = band < 0.35f ? Main.rand.NextFloat(200f, 650f) : band < 0.75f ? Main.rand.NextFloat(800f, 1400f) : Main.rand.NextFloat(1600f, 2300f);
            Vector2 where = new Vector2(ear.X + (Main.rand.NextBool() ? far : -far), VastOcean.Surface * 16f + 8f);

            return Start(SurfVoice, Surf, piece, where, Vector2.Zero, Main.rand.NextFloat(-0.08f, 0.06f), 1f, 0f);
        }

        private static bool StartWind(float windy)
        {
            int piece = Wind.Pick(new[] { 1f - 0.8f * windy, 0.2f + windy });

            if (piece < 0)
                return false;

            // From where the wind comes, going by on its way, over the length of the piece; in a calm, either way.
            int way = Math.Abs(Main.windSpeedCurrent) < 0.1f ? (Main.rand.NextBool() ? 1 : -1) : Math.Sign(Main.windSpeedCurrent);
            float reach = 250f + 450f * windy;
            Vector2 from = new Vector2(-way * reach, -Main.rand.NextFloat(60f, 260f));
            float pitch = Main.rand.NextFloat(-0.12f, 0.08f) + 0.12f * windy;

            return Start(WindVoice, Wind, piece, from, new Vector2(2f * way * reach, 0f), pitch, 1f, 0f);
        }

        /// <summary>A song or a call: mostly far off, some nearer, now and then near; from anywhere about and below; lower the deeper the listener.</summary>
        private static void StartHum(Vector2 ear, float under, int side, int call)
        {
            int piece = Hums.Pick(call == 1 ? new[] { 0.15f, 1f } : new[] { 1f, 0.15f });

            if (piece < 0)
                return;

            float band = Main.rand.NextFloat();
            float far = band < 0.55f ? Main.rand.NextFloat(1500f, 2300f) : band < 0.9f ? Main.rand.NextFloat(800f, 1500f) : Main.rand.NextFloat(300f, 800f);
            float angle = Main.rand.NextFloat(-0.5f, 0.8f);
            Vector2 where = ear + new Vector2(side * far * (float)Math.Cos(angle), far * (float)Math.Sin(angle));

            // In the sea, not over it.
            where.Y = Math.Max(where.Y, (VastOcean.Surface + 10) * 16f);

            float pitch = -(0.05f + 0.25f * Smooth(0f, 6f * VastOcean.SectionRows, under)) + Main.rand.NextFloat(-0.08f, 0.08f);

            Start(HumVoice, Hums, piece, where, Vector2.Zero, pitch, 1f, 0f);
        }

        /// <summary>A long song from the trench: mostly far, from anywhere below the listener; a little higher or lower each time.</summary>
        private static void StartSong(Vector2 ear)
        {
            int piece = Songs.Pick(new[] { 1f, 0.45f });

            if (piece < 0)
                return;

            float band = Main.rand.NextFloat();
            float far = band < 0.6f ? Main.rand.NextFloat(1400f, 2300f) : Main.rand.NextFloat(800f, 1400f);
            float angle = Main.rand.NextFloat(-1.3f, 1.3f);
            Vector2 where = ear + new Vector2(far * (float)Math.Sin(angle), far * (float)Math.Cos(angle));

            Start(SongVoice, Songs, piece, where, Vector2.Zero, Main.rand.NextFloat(-0.12f, 0.04f), 1f, 0f);
        }

        /// <summary>The rocks: from somewhere under the listener, near or far; a big rock lower, a small one higher.</summary>
        private static bool StartRock(Vector2 ear, out Vector2 where)
        {
            where = ear + new Vector2((Main.rand.NextBool() ? 1 : -1) * Main.rand.NextFloat(150f, 1700f), Main.rand.NextFloat(500f, 1600f));
            return StartRock(where);
        }

        private static bool StartRock(Vector2 where)
        {
            int piece = Rocks.Pick(new[] { 1f });

            if (piece < 0)
                return false;

            return Start(RockVoice, Rocks, piece, where, Vector2.Zero, Main.rand.NextFloat(-0.3f, 0.12f), Main.rand.NextFloat(0.6f, 1f), 0f);
        }

        /// <summary>The Duke's roar, an octave down (or near it), swelling in over a third of a second.</summary>
        private static void StartRoar(Vector2 where, float pitch, float gain)
        {
            SoundStyle roar = SoundID.Zombie20 with
            {
                Volume = 1f,
                Pitch = pitch,
                PitchVariance = 0f,
                MaxInstances = 0,
                Type = SoundType.Ambient,
                PauseBehavior = PauseBehavior.PauseWithGame,
            };

            // 1.22 seconds as the game has it (Zombie_20), twice that an octave down.
            Play(new Voice { What = RoarVoice, Where = where, Gain = gain, Seconds = 1.222f / (float)Math.Pow(2.0, pitch), Born = ticks, Swell = 0.35f }, roar);
        }

        private static bool Start(int what, Kind kind, int piece, Vector2 where, Vector2 drift, float pitch, float gain, float swell)
        {
            pitch = MathHelper.Clamp(pitch, -1f, 1f);

            SoundStyle style = kind.Styles[piece] with { Pitch = pitch, PitchVariance = 0f };
            Voice voice = new Voice
            {
                What = what,
                Where = where,
                Drift = drift,
                Gain = kind.Gain[piece] * gain,
                Seconds = kind.Seconds[piece] / (float)Math.Pow(2.0, pitch),
                Born = ticks,
                Swell = swell,
            };

            if (!Play(voice, style))
                return false;

            kind.Played(piece);
            return true;
        }

        private static bool Play(Voice voice, SoundStyle style)
        {
            voice.Stay = stay;
            voice.Callback = sound => Heard(voice, sound);

            // Heard from where it is: the wind from about the ear as it is now.
            Vector2 at = voice.What == WindVoice ? Main.Camera.Center + voice.Where : voice.Where;

            // Silent until its first look (below): nothing heard of it before its loudness is set.
            voice.Level = 0f;

            SlotId slot = SoundEngine.PlaySound(style, at, voice.Callback);

            if (!slot.IsValid)
                return false;

            voice.Slot = slot;
            voices.Add(voice);
            return true;
        }

        /// <summary>
        /// Each tick, as the game looks at each of these sounds (ActiveSound.Update, before it sets
        /// how loud it is from where it is): stopped if it is let go and faded, or the place is
        /// left; the wind where it has got to; its loudness; and muffled as far off or underwater
        /// asks, except in a dive (the 7th section), where VastOceanSound muffles everything.
        /// </summary>
        private static bool Heard(Voice v, ActiveSound sound)
        {
            if (!running || v.Stay != stay || v.Fade <= 0f || SubworldKeeper.Current is not VastOcean || Main.gameMenu)
                return false;

            if (v.What == WindVoice)
                sound.Position = Main.Camera.Center + v.Where + v.Drift * MathHelper.Clamp(v.Age / Math.Max(1f, v.Seconds), 0f, 1f);

            float swell = v.Swell > 0f ? Smooth(0f, v.Swell, v.Age) : 1f;

            sound.Volume = MathHelper.Clamp(v.Level * v.Gain * Math.Max(v.Fade, 0f) * swell, 0f, 1f);

            SoundEffectInstance instance = sound.Sound;

            // Muffled by distance here, but in a dive (VastOceanSound muffles everything then): the
            // trench's always, as VastOceanSound leaves the sounds of the deep alone.
            if (lowPass != null && instance != null && !instance.IsDisposed && (VastOceanSound.Hearing() == 0 || v.What >= SongVoice))
                lowPass(instance, v.Cutoff);

            return true;
        }

        private static uint Seconds(float least, float most) => (uint)(Main.rand.NextFloat(least, most) * 60f);

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }
}
