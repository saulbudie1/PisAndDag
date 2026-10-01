using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- her wingbeat ---------------------------------------------------------------------------
    //
    // A heavy wing, slow and bent, never straight for long (QueenSlimeWingbeat.png, 24 frames of
    // one beat). Every machine that draws her beats her wings on its own clock; nothing here is
    // sent or moves her, except the takeoff's, which the transition runs off its own ticks.
    //
    //     THE TOP     wings high, held a moment, her body at its lowest
    //     THE PUSH    down, the arm ahead of the hand: fast through the middle, the air thrown
    //                 down off them (streaks, the ground's dust if it is near, a deep beat of
    //                 sound), her gel pressed by it and her body thrown up
    //     THE BOTTOM  the tips curl down and in, slowly
    //     THE RISE    the wrist first, the hand hanging, the wing drawn in, the underside dark;
    //                 her body sinking
    //     GLIDE       every three to five beats, when she is not climbing or rushing, the push
    //                 slows into the spread and holds there, her body drifting down, and comes
    //                 on again the harder
    //
    // A beat takes 100 ticks hovering, 74 rushing, 64 climbing. Falling (vanilla's slam) the
    // wings go up quickly and stay up.

    public partial class LegendsQueenSlime
    {
        /// <summary>Ticks a beat takes: hovering, flying fast, climbing.</summary>
        private const int HoverBeat = 100;
        private const int FastBeat = 74;
        private const int ClimbBeat = 64;

        /// <summary>Where on the beat the push is at its hardest: the gust, the sound, her gel pressed.</summary>
        private const float PowerAt = 0.3f;

        /// <summary>How much of the beat a glide takes to slow into its hold, and to come out of it.</summary>
        private const float GlideEase = 0.08f;

        // Curves along the beat (phase 0 to 1): phase, value, slope per phase, a key at a time,
        // cubic Hermite between keys.

        /// <summary>Where on the sheet's 24 frames the wing is (0 to 1 a beat; wraps): slow at the top and the bottom, fast through the push.</summary>
        private static readonly float[] BeatPose =
        {
            0.00f, -0.03f, 0.30f,
            0.12f, 0.03f, 0.55f,
            0.24f, 0.17f, 1.9f,
            0.30f, 0.31f, 2.6f,
            0.36f, 0.45f, 1.7f,
            0.46f, 0.58f, 0.8f,
            0.56f, 0.64f, 0.75f,
            0.80f, 0.85f, 0.85f,
            1.00f, 0.97f, 0.30f,
        };

        /// <summary>Her body, down from where it is, pixels: lowest at the top of the beat, thrown up by the push.</summary>
        private static readonly float[] BeatBob =
        {
            0.00f, 4f, 0f,
            0.14f, 6f, 0f,
            0.30f, 2.5f, -45f,
            0.46f, -4.5f, -25f,
            0.60f, -6f, 0f,
            0.80f, -1f, 30f,
            1.00f, 4f, 0f,
        };

        /// <summary>Her gel's shape with it: pressed by the push, drawn out as she goes up.</summary>
        private static readonly float[] BeatGel =
        {
            0.00f, 0.02f, 0f,
            0.22f, 0f, 0f,
            0.33f, -0.055f, 0f,
            0.46f, -0.02f, 0f,
            0.60f, 0.035f, 0f,
            0.82f, 0f, 0f,
            1.00f, 0.02f, 0f,
        };

        /// <summary>How lit the wing is: the underside, dark, as it rises.</summary>
        private static readonly float[] BeatLit =
        {
            0.00f, 1f, 0f,
            0.40f, 1f, 0f,
            0.54f, 0.96f, 0f,
            0.70f, 0.84f, 0f,
            0.92f, 0.95f, 0f,
            1.00f, 1f, 0f,
        };

        /// <summary>Her colours in the wing: brightest as it turns into the push.</summary>
        private static readonly float[] BeatSheen =
        {
            0.00f, 0.10f, 0f,
            0.16f, 0.12f, 0f,
            0.25f, 0.24f, 0f,
            0.38f, 0.12f, 0f,
            1.00f, 0.10f, 0f,
        };

        /// <summary>The beat, 0 to 1.</summary>
        private float beatPhase;

        /// <summary>A glide on this beat: how long it holds, 0 none; whether it has; ticks left of it.</summary>
        private int beatGlide;
        private bool beatHeld;
        private int beatHold;

        private int beatCount;
        private int nextGlide = 3;

        /// <summary>How hard the next push is: harder out of a glide.</summary>
        private float beatStrength = 1f;

        /// <summary>Her body's rise and fall with the beat, eased in and out of flight.</summary>
        private float beatBob;

        /// <summary>The frames of the beat drawn the last seven ticks, five bits each, the newest lowest; and how many there are.</summary>
        private ulong beatTrail;
        private int beatTrailCount;

        private static float glideAt = -1f;

        /// <summary>Where on the beat the spread falls (frame BeatSpread): where a glide holds.</summary>
        private static float GlideAt
        {
            get
            {
                if (glideAt < 0f)
                {
                    float target = QueenSlimeArt.BeatSpread / (float)QueenSlimeArt.BeatFrames;
                    float lo = 0.12f, hi = 0.46f;

                    for (int i = 0; i < 40; i++)
                    {
                        float mid = (lo + hi) * 0.5f;

                        if (Keyed(BeatPose, mid) < target)
                            lo = mid;
                        else
                            hi = mid;
                    }

                    glideAt = (lo + hi) * 0.5f;
                }

                return glideAt;
            }
        }

        /// <summary>A curve of keys (phase, value, slope per phase) at <paramref name="phase"/>: cubic Hermite between keys, held past the ends.</summary>
        private static float Keyed(float[] keys, float phase)
        {
            int last = keys.Length / 3 - 1;

            for (int i = 0; i < last; i++)
            {
                float p0 = keys[i * 3], y0 = keys[i * 3 + 1], m0 = keys[i * 3 + 2];
                float p1 = keys[i * 3 + 3], y1 = keys[i * 3 + 4], m1 = keys[i * 3 + 5];

                if (phase > p1 && i < last - 1)
                    continue;

                float h = p1 - p0;
                float t = MathHelper.Clamp((phase - p0) / h, 0f, 1f);
                float t2 = t * t;
                float t3 = t2 * t;

                return (2f * t3 - 3f * t2 + 1f) * y0 + (t3 - 2f * t2 + t) * h * m0 + (-2f * t3 + 3f * t2) * y1 + (t3 - t2) * h * m1;
            }

            return keys[1];
        }

        /// <summary>Where on the sheet the wing is at this point of the beat, 0 to 1.</summary>
        private static float PoseAt(float phase) => Wrap(Keyed(BeatPose, Wrap(phase)));

        /// <summary>The frame of QueenSlimeWingbeat.png nearest a place on the sheet (0 to 1).</summary>
        private static int BeatFrameOf(float pose)
        {
            int frame = (int)Math.Floor(pose * QueenSlimeArt.BeatFrames + 0.5f);
            return (frame % QueenSlimeArt.BeatFrames + QueenSlimeArt.BeatFrames) % QueenSlimeArt.BeatFrames;
        }

        private static float Wrap(float phase) => phase - (float)Math.Floor(phase);

        /// <summary>The spread's place on the sheet.</summary>
        private static float SpreadPose => QueenSlimeArt.BeatSpread / (float)QueenSlimeArt.BeatFrames;

        /// <summary>Ticks a beat takes now: quicker the faster she goes, quicker still climbing.</summary>
        private static float BeatPeriod(NPC npc)
        {
            float speed = npc.velocity.Length();
            float period = MathHelper.Lerp(HoverBeat, FastBeat, Math.Min(1f, speed / 9f));
            float climb = Math.Max(0f, -npc.velocity.Y);

            return MathHelper.Lerp(period, ClimbBeat, Math.Min(1f, climb / 6f));
        }

        /// <summary>
        /// The beat on by a tick, in flight (not the transition's, which sets it from its own
        /// ticks). True the tick the push comes.
        /// </summary>
        private bool AdvanceBeat(NPC npc)
        {
            // Dropping (vanilla's slam): up quickly, and held there.
            bool falling = !npc.noGravity;

            if (falling)
            {
                beatHold = 0;
                beatGlide = 0;

                if (beatPhase < 0.1f)
                    return false;
            }
            else if (beatHold > 0)
            {
                beatHold--;
                return false;
            }

            float rate = 1f / BeatPeriod(npc);

            if (falling)
                rate *= 3f;
            else if (beatGlide > 0)
            {
                // Slowing into the spread, and out of it again.
                float ahead = GlideAt - beatPhase;

                if (!beatHeld && ahead > 0f && ahead < GlideEase)
                    rate *= 0.12f + 0.88f * ahead / GlideEase;
                else if (beatHeld && ahead <= 0f && -ahead < GlideEase)
                    rate *= 0.12f + 0.88f * -ahead / GlideEase;
            }

            float before = beatPhase;
            beatPhase += rate;

            if (beatGlide > 0 && !beatHeld && before < GlideAt && beatPhase >= GlideAt)
            {
                beatPhase = GlideAt;
                beatHold = beatGlide;
                beatHeld = true;
                beatStrength = 1.3f;
                return false;
            }

            // A beat that glides pushes as it comes out of the spread, the harder for it.
            float pushAt = beatGlide > 0 ? GlideAt + 0.02f : PowerAt;
            bool pushed = !falling && before < pushAt && beatPhase >= pushAt;

            if (beatPhase >= 1f)
            {
                beatPhase -= 1f;
                beatCount++;
                beatStrength = 1f;
                beatGlide = 0;
                beatHeld = false;

                if (!falling && beatCount >= nextGlide && npc.velocity.Y > -1.5f && npc.velocity.Length() < 7f)
                {
                    beatGlide = Main.rand.Next(30, 49);
                    nextGlide = beatCount + Main.rand.Next(3, 6);
                }
            }

            return pushed;
        }

        /// <summary>Holding the spread, gliding.</summary>
        private bool Gliding => beatHold > 0;

        /// <summary>Her gel's shape from the beat now: a slow breath while she glides.</summary>
        private float BeatGelNow() =>
            Gliding ? Keyed(BeatGel, GlideAt) + 0.015f * (float)Math.Sin(shapeClock * 0.05f) : Keyed(BeatGel, beatPhase);

        /// <summary>Her body down from where it is, from the beat now: drifting lower while she glides.</summary>
        private float BeatBobNow()
        {
            float bob = Keyed(BeatBob, beatPhase);

            if (Gliding && beatGlide > 0)
                bob += 3f * (float)Math.Sin((1f - beatHold / (float)beatGlide) * MathHelper.Pi);

            return bob;
        }

        /// <summary>The takeoff's beat at its tick <paramref name="t"/> from the first push on: the rest of that beat, quick, then climbing beats.</summary>
        private static float TakeoffPhase(int t)
        {
            int since = t - PushAt;

            if (since < PushBeatTicks)
                return PowerAt + (1f - PowerAt) * since / PushBeatTicks;

            return Wrap((since - PushBeatTicks) / (float)ClimbBeat);
        }

        /// <summary>Keeps the frame drawn this tick, for the shadows of a fast beat.</summary>
        private void KeepTrail(int frame)
        {
            frame = (frame % QueenSlimeArt.BeatFrames + QueenSlimeArt.BeatFrames) % QueenSlimeArt.BeatFrames;
            beatTrail = (beatTrail << 5) | (uint)frame;
            beatTrailCount = Math.Min(7, beatTrailCount + 1);
        }

        /// <summary>The frame drawn <paramref name="ago"/> ticks ago (0 to 6).</summary>
        private int TrailFrame(int ago) => (int)((beatTrail >> (5 * ago)) & 31);

        /// <summary>
        /// How strongly to shadow the beat where it was three and six ticks ago: only while it
        /// sweeps fast, 0 none.
        /// </summary>
        private float TrailStrength()
        {
            if (beatTrailCount < 7)
                return 0f;

            int step = (TrailFrame(0) - TrailFrame(3) + QueenSlimeArt.BeatFrames) % QueenSlimeArt.BeatFrames;

            return step >= 3 && step <= 12 ? Math.Min(1f, (step - 2) / 5f) : 0f;
        }

        /// <summary>
        /// The push of a beat: the air thrown down off both wings, a deep beat of sound, and the
        /// ground's dust blown out under her if it is near. <paramref name="strength"/> 1 an
        /// ordinary beat.
        /// </summary>
        private void WingGust(NPC npc, float strength)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.75f, Volume = 0.45f * strength, PitchVariance = 0.12f, MaxInstances = 3 }, npc.Center);

            if (strength > 1.1f)
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.4f, Volume = 0.3f, MaxInstances = 2 }, npc.Center);

            Pose pose = Posed(npc);

            for (int s = -1; s <= 1; s += 2)
            {
                // Under the middle of the wing.
                Vector2 under = pose.At(new Vector2(s * 140f, -npc.height * 0.55f));

                for (int i = 0; i < 2; i++)
                {
                    Vector2 at = under + Main.rand.NextVector2Circular(50f, 24f);
                    Vector2 velocity = new Vector2(s * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(5f, 8f)) * strength;

                    PADParticleHandler.SpawnParticle(new WindStreak(at, velocity, Main.rand.NextFloat(50f, 90f), Main.rand.NextFloat(5f, 8f),
                        Main.rand.Next(14, 22)));
                }
            }

            Downwash(npc, strength);
        }

        /// <summary>How far below her a beat still stirs the ground, in tiles.</summary>
        private const int DownwashTiles = 22;

        /// <summary>The ground under her, if it is near: its dust blown out both ways, more the nearer it is.</summary>
        private static void Downwash(NPC npc, float strength)
        {
            int x = (int)(npc.Center.X / 16f);
            int from = (int)(npc.Bottom.Y / 16f);

            for (int y = from; y < from + DownwashTiles; y++)
            {
                Tile tile = Framing.GetTileSafely(x, y);

                if (!tile.HasUnactuatedTile || !(Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                    continue;

                float floor = y * 16f;
                float weight = strength * (1f - (floor - npc.Bottom.Y) / (DownwashTiles * 16f));

                if (weight < 0.1f)
                    return;

                int count = (int)(10 * weight);

                for (int i = 0; i < count; i++)
                {
                    int side = Main.rand.NextBool() ? 1 : -1;
                    Vector2 at = new Vector2(npc.Center.X + side * Main.rand.NextFloat(0f, 70f), floor - Main.rand.NextFloat(2f, 10f));
                    Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, new Vector2(side * Main.rand.NextFloat(3f, 7f) * weight, Main.rand.NextFloat(-1.2f, 0f)),
                        80, default, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }

                LegendsKingSlime.GroundDust(npc.Center.X - 48f, npc.Center.X + 48f, floor, 0.25f * weight);
                return;
            }
        }
    }
}
