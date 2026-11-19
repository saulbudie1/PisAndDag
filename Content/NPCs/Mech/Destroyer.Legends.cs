using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- Legends ------------------------------------------------------------------------------------------------------------
    //
    // By request: "Legends: 2 phases, phase 2 start at 35%". Its element is lightning and energy, its colour #7fd4d1.
    //
    //     BETWEEN MOVES   vanilla's chase; its own warned bolts from a few segments now and then, in place of vanilla's
    //                     lasers (every 160-240 ticks; 110-170 in its second phase)
    //     PHASE ONE       PULSE (rare), STORM (every 30-40 s), VOLLEY
    //     SECOND WIND     at 35% (Wake, untouchable and harmless, the camera on it, skippable): it slows; from its tail
    //                     to its head every segment vents steam and its eye turns to its colour; its head last, with its
    //                     robotic roar
    //     PHASE TWO       its eyes in its colour, a trail behind each, steam out of every crease; PULSE (rare), STORM
    //                     (every 30-40 s), POP, BERSERK, CAGE, SWEEP (Destroyer.Moves); its Probes pair up and fly into
    //                     each other (LegendsProbe)
    //
    // The server picks; every machine plays it from what it is told (SendLegends).

    public partial class Destroyer
    {
        private enum Act : byte
        {
            None,
            Pulse,
            Storm,
            Volley,
            Pop,
            Berserk,
            Cage,
            Sweep,
        }

        /// <summary>Its second phase (from 35%).</summary>
        private bool phase2;
        private const float Phase2At = 0.35f;

        /// <summary>Ticks of vanilla's chase till its next move; till its storm; till its pulse laser may come again; till its next bolts.</summary>
        private int restFor = 240;
        private int stormIn = -1;
        private int pulseIn = 900;
        private int idleIn = 200;

        private Act lastAct;

        /// <summary>A move's places, turning way, count and side, and what its chances are worked out from.</summary>
        private Vector2 markA, markB;
        private float spin;
        private int count;
        private sbyte side;
        private int moveSeed;

        // ---- this machine's, not sent ------------------------------------------------------------------------------------

        /// <summary>Its charge's front (an index into its body), or below zero for none, this tick.</summary>
        private float chargeFront = -1f;

        /// <summary>Its body's yellow (its storm): steady, and a flash when it zaps.</summary>
        private float stormGlow, zapGlow;

        /// <summary>Its second wind: segments from this index back have their eyes in its colour.</summary>
        private int litFrom = int.MaxValue;

        /// <summary>Its beam: on, which way, how far in (0 to 1).</summary>
        internal bool beamOn;
        internal float beamAngle;
        internal float beamPower;
        private SlotId beamSound = SlotId.Invalid;
        private bool beamFading;

        public static readonly SoundStyle DashRoar = new("PisAndDag/Content/Sounds/Mech/DestroyerDashRoar") { Volume = 1f, MaxInstances = 2 };
        public static readonly SoundStyle ChargeSound = new("PisAndDag/Content/Sounds/Mech/DestroyerCharge") { Volume = 0.9f, MaxInstances = 2 };
        public static readonly SoundStyle BeamSound = new("PisAndDag/Content/Sounds/Mech/DestroyerBeam") { Volume = 0.95f, MaxInstances = 2 };
        public static readonly SoundStyle VentSound = new("PisAndDag/Content/Sounds/Mech/DestroyerVent") { Volume = 0.7f, MaxInstances = 4, PitchVariance = 0.12f };

        /// <summary>Its eyes are in its colour from this index of its body back (all of it in its second phase).</summary>
        internal int EyeLitFrom => phase2 ? 0 : scene == Scene.Wake ? litFrom : int.MaxValue;

        internal bool HeadLit => phase2 || scene == Scene.Wake && step == WakeEye;

        internal bool Phase2 => phase2;

        /// <summary>Whether any reworked Destroyer is in its second phase (Legends): its Probes pair up.</summary>
        internal static bool AnyInPhase2()
        {
            if (!PADWorld.Legends)
                return false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.type == NPCID.TheDestroyer && npc.TryGetGlobalNPC(out Destroyer d) && d.phase2)
                    return true;
            }

            return false;
        }

        // ---- between its moves --------------------------------------------------------------------------------------------

        /// <summary>Its clocks, its second wind, its bolts, and its next move. The server's (clients are told).</summary>
        private void LegendsBetween(NPC npc)
        {
            if (!Authority)
                return;

            if (stormIn < 0)
            {
                stormIn = Main.rand.Next(1800, 2401);
                npc.netUpdate = true;
            }

            if (restFor > 0)
                restFor--;

            if (stormIn > 0)
                stormIn--;

            if (pulseIn > 0)
                pulseIn--;

            npc.damage = npc.defDamage;

            if (!phase2 && npc.life <= npc.lifeMax * Phase2At)
            {
                BeginWake(npc);
                return;
            }

            Player p = Target(npc);

            if (p == null)
                return;

            float far = Vector2.Distance(p.Center, npc.Center);

            if (--idleIn <= 0)
            {
                idleIn = phase2 ? Main.rand.Next(110, 171) : Main.rand.Next(160, 241);

                if (far < 2600f)
                    IdleBolts(npc, p);
            }

            if (restFor > 0 || far > 3200f)
                return;

            BeginMove(npc, Pick());
        }

        /// <summary>Its next move: its storm when it is due; otherwise by weight, not the last again.</summary>
        private Act Pick()
        {
            if (stormIn <= 0)
                return Act.Storm;

            (Act act, float weight)[] pool = phase2
                ? new[] { (Act.Pop, 2f), (Act.Berserk, 2f), (Act.Cage, 2f), (Act.Sweep, 1.6f), (Act.Pulse, pulseIn <= 0 ? 1f : 0f) }
                : new[] { (Act.Volley, 3f), (Act.Pulse, pulseIn <= 0 ? 1.2f : 0f) };

            float total = 0f;

            foreach ((Act act, float weight) in pool)
            {
                if (act != lastAct)
                    total += weight;
            }

            if (total <= 0f)
                return phase2 ? Act.Pop : Act.Volley;

            float roll = Main.rand.NextFloat(total);

            foreach ((Act act, float weight) in pool)
            {
                if (act == lastAct || weight <= 0f)
                    continue;

                roll -= weight;

                if (roll <= 0f)
                    return act;
            }

            return phase2 ? Act.Pop : Act.Volley;
        }

        /// <summary>A few of its segments fire warned bolts at the player (its lasers, in Legends).</summary>
        private void IdleBolts(NPC npc, Player p)
        {
            int many = phase2 ? Main.rand.Next(6, 10) : Main.rand.Next(4, 8);

            for (int tries = 0; tries < many * 3 && many > 0 && chain.Count > 0; tries++)
            {
                NPC segment = Main.npc[chain[Main.rand.Next(chain.Count)]];

                if (Vector2.Distance(segment.Center, p.Center) > 1300f)
                    continue;

                DestroyerShots.Bolt(npc, segment, p, 40, DestroyerShots.BoltDamage(npc));
                many--;
            }
        }

        /// <summary>A move begins: its places decided and sent.</summary>
        private void BeginMove(NPC npc, Act act)
        {
            Player p = Target(npc);
            Vector2 at = p?.Center ?? npc.Center;

            lastAct = act;
            moveSeed = Main.rand.Next(int.MaxValue);
            side = (sbyte)(npc.Center.X >= at.X ? 1 : -1);
            spin = Main.rand.NextBool() ? 1f : -1f;
            count = 0;
            markA = at;
            markB = Vector2.Zero;

            int first = act switch
            {
                Act.Pulse => PulseCome,
                Act.Storm => StormUp,
                Act.Volley => VolleySlow,
                Act.Pop => PopCome,
                Act.Berserk => RageStart,
                Act.Cage => CageCome,
                Act.Sweep => SweepCome,
                _ => -1,
            };

            if (first < 0)
                return;

            Go(npc, Scene.Move, first);
        }

        /// <summary>A move over: vanilla's chase again, and its clocks set.</summary>
        private void EndMove(NPC npc)
        {
            StopBeam();
            npc.damage = npc.defDamage;

            if (!Authority)
                return;

            int at = step / 10;

            if (at == StormUp / 10)
                stormIn = Main.rand.Next(1800, 2401);

            if (at == PulseCome / 10 || at == SweepCome / 10)
                pulseIn = Main.rand.Next(1500, 2101);

            restFor = phase2 ? Main.rand.Next(140, 211) : Main.rand.Next(200, 301);
            idleIn = Math.Max(idleIn, 90);
            Go(npc, Scene.None, 0);
        }

        // ---- its second wind ----------------------------------------------------------------------------------------------

        private const int WakeSlow = 0;
        private const int WakeWave = 1;
        private const int WakeEye = 2;

        private const int WakeSlowTicks = 50;
        private const int WakeWaveTicks = 130;
        private const int WakeEyeTicks = 90;

        private bool wakeWatching;

        private void BeginWake(NPC npc)
        {
            StopBeam();
            npc.damage = npc.defDamage;
            Go(npc, Scene.Wake, WakeSlow);
        }

        private void RunWake(NPC npc, bool entered)
        {
            int t = timer;
            bool watching = Watching(npc.Center);

            // E held in the corner (single player): straight to its second phase.
            if (watching && step < WakeEye)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    if (!Main.dedServ)
                        PisPlayer.PanHome(npc.Center, 30);

                    phase2 = true;
                    EndWake(npc);
                    return;
                }
            }

            if (watching)
                wakeWatching = true;

            switch (step)
            {
                case WakeSlow:
                    {
                        Glide(npc, Facing(npc), 2f, 0.01f, 0.08f);

                        if (watching)
                            Shot(npc.Center, Smooth(t / 30f), MathHelper.Lerp(1f, 1.06f, Smooth(t / (float)WakeSlowTicks)));

                        shake = 1.5f * Smooth(t / (float)WakeSlowTicks);

                        if (Authority && t >= WakeSlowTicks)
                            Next(npc, WakeWave);

                        break;
                    }

                case WakeWave:
                    {
                        float u = Math.Min(1f, (t + 1f) / WakeWaveTicks);
                        float front = chain.Count * (1f - u);
                        litFrom = (int)Math.Ceiling(front);
                        Glide(npc, Facing(npc), 1.5f, 0.006f, 0.08f);
                        shake = 1.5f;

                        if (!Main.dedServ)
                        {
                            for (int k = litFrom - 1; k <= litFrom + 1; k++)
                            {
                                if (k < 0 || k >= chain.Count - 1)
                                    continue;

                                Vector2 at = Crease(npc, k, out Vector2 along);

                                if (!Seen(at, 80f))
                                    continue;

                                for (int i = 0; i < 2; i++)
                                    Steam(at, along, 1.6f);
                            }

                            if (litFrom >= 0 && litFrom < chain.Count)
                            {
                                NPC lit = Main.npc[chain[litFrom]];

                                if (Seen(lit.Center, 60f))
                                    Add(new Particles.BloomGeneric(lit.Center, lit.velocity, DestroyerArt.Teal * 0.7f, 0.45f, 10));

                                if (t % 10 == 0)
                                    Sound(VentSound, lit.Center, 4);
                            }

                            Shake(npc.Center, 1.6f, 6);
                        }

                        if (watching)
                        {
                            Vector2 front3 = litFrom >= 0 && litFrom < chain.Count ? Main.npc[chain[litFrom]].Center : npc.Center;
                            Shot(Vector2.Lerp(npc.Center, front3, 0.45f * (1f - u)), 1f, 1.06f);
                        }

                        if (Authority && t >= WakeWaveTicks)
                            Next(npc, WakeEye);

                        break;
                    }

                case WakeEye:
                    {
                        litFrom = 0;
                        float voice = Smooth(t / 8f) * (1f - Smooth((t - 50) / 40f));
                        Glide(npc, Facing(npc), 1.5f, 0.006f, 0.08f);

                        if (entered)
                        {
                            Bellow(npc, DashRoar, 1.3f);
                            HeadGlow(npc, 2f, flare: true);

                            if (watching && !Main.dedServ)
                                Cinematic.Flash(DestroyerArt.Teal, 0.15f);

                            // A breath of steam out of every crease on screen at once.
                            if (!Main.dedServ)
                            {
                                for (int i = -1; i < chain.Count - 1; i++)
                                {
                                    Vector2 at = Crease(npc, i, out Vector2 along);

                                    if (Seen(at, 60f))
                                        Steam(at, along, 1.4f);
                                }
                            }
                        }

                        BellowTick(npc, voice);
                        HeadGlow(npc, 1f + voice);

                        if (t < 50 && watching)
                            Shot(npc.Center, 1f, 1.1f);
                        else if (t == 50 && wakeWatching && !Main.dedServ)
                        {
                            PisPlayer.PanHome(npc.Center, 70);
                            wakeWatching = false;
                        }

                        if (Authority && t >= WakeEyeTicks)
                        {
                            phase2 = true;
                            EndWake(npc);
                        }

                        break;
                    }
            }
        }

        private void EndWake(NPC npc)
        {
            litFrom = int.MaxValue;
            restFor = 90;
            idleIn = 120;
            Go(npc, Scene.None, 0);
        }

        // ---- how it looks -------------------------------------------------------------------------------------------------

        /// <summary>Its glows faded a tick, its charge's front forgotten (asked again each tick), its beam's sound let go.</summary>
        private void Fade()
        {
            zapGlow *= 0.86f;
            shake = 0f;

            if (warnFade > 0)
                warnFade--;

            if (zapGlow < 0.01f)
                zapGlow = 0f;

            stormGlow = Math.Max(0f, stormGlow - 0.02f);
            chargeFront = -1f;

            if (beamFading && SoundEngine.TryGetActiveSound(beamSound, out ActiveSound sound))
            {
                sound.Volume -= 0.09f;

                if (sound.Volume <= 0.02f)
                {
                    sound.Stop();
                    beamFading = false;
                }
            }
            else
            {
                beamFading = false;
            }
        }

        /// <summary>Its second phase's look, every tick it is up (this machine's): steam out of its creases, its eye's light.</summary>
        private void LegendsLook(NPC npc)
        {
            if (Main.dedServ || !PADWorld.Legends || headGone)
                return;

            if (phase2)
            {
                float rate = 0.6f * Room;
                int whole = (int)rate;

                if (Main.rand.NextFloat() < rate - whole)
                    whole++;

                for (int i = 0; i < whole; i++)
                {
                    if (OnScreenCrease(npc, out Vector2 at, out Vector2 along))
                        Steam(at, along, 0.6f);
                }

                Vector2 eye = Front(npc);
                Lighting.AddLight(eye, 0.4f, 0.8f, 0.8f);
            }

            // Its storm's yellow lights its length.
            float yellow = Math.Max(stormGlow, zapGlow);

            if (yellow > 0.02f)
            {
                for (int i = 0; i < chain.Count; i += 3)
                {
                    NPC segment = Main.npc[chain[i]];

                    if (Seen(segment.Center, 100f))
                        Lighting.AddLight(segment.Center, 1f * yellow, 0.88f * yellow, 0.35f * yellow);
                }
            }
        }

        // ---- its beam -----------------------------------------------------------------------------------------------------

        /// <summary>Where its beam comes from: its mouth.</summary>
        internal Vector2 BeamStart(NPC npc) => npc.Center + beamAngle.ToRotationVector2() * 30f;

        private void StartBeam(NPC npc)
        {
            beamOn = true;
            beamPower = 0f;
            beamFading = false;

            if (!Main.dedServ)
            {
                beamSound = SoundEngine.PlaySound(BeamSound, Here(npc));
                Shake(npc.Center, 10f, 30);
            }
        }

        private void BeamTick(NPC npc)
        {
            beamPower = Math.Min(1f, beamPower + 0.14f);

            if (!Main.dedServ && SoundEngine.TryGetActiveSound(beamSound, out ActiveSound sound))
                sound.Position = Here(npc);

            if (!Main.dedServ)
            {
                Vector2 mouth = BeamStart(npc);
                Lighting.AddLight(mouth, 0.7f, 1.1f, 1.1f);
                Shake(mouth, 1.2f, 4);

                if (Main.rand.NextBool(3))
                    MouthCrackle(npc, 50f);
            }
        }

        private void StopBeam()
        {
            beamOn = false;
            beamPower = 0f;

            if (!Main.dedServ && beamSound.IsValid && SoundEngine.TryGetActiveSound(beamSound, out _))
                beamFading = true;
        }

        // ---- drawing --------------------------------------------------------------------------------------------------------

        /// <summary>Where vanilla would draw a segment of it (Main.DrawNPCDirect_Inner, for 134-136).</summary>
        private static Vector2 DrawPlace(NPC npc, Texture2D texture, Vector2 screenPos, out Vector2 half)
        {
            int frames = Math.Max(1, Main.npcFrameCount[npc.type]);
            float frameHigh = texture.Height / (float)frames;
            half = new Vector2(texture.Width / 2f, frameHigh / 2f);
            return new Vector2(npc.Center.X - screenPos.X,
                npc.position.Y - screenPos.Y + npc.height - frameHigh * npc.scale + 4f + half.Y * npc.scale + Main.NPCAddHeight(npc) + npc.gfxOffY);
        }

        /// <summary>
        /// A part drawn here instead of by vanilla, when it must look other than vanilla's: its eye in its colour (and a
        /// trail behind it), red as its charge runs through it, yellow in its storm. False: vanilla draws it as it is.
        /// </summary>
        private bool DrawPart(NPC npc, SpriteBatch batch, Vector2 screenPos, Color drawColor, bool lit, bool red, float yellow, int which)
        {
            if (!lit && !red && yellow < 0.01f)
                return false;

            Main.instance.LoadNPC(npc.type);
            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Texture2D glow = lit ? DestroyerArt.TealGlow(which) : TextureAssets.Dest[which].Value;
            Vector2 at = DrawPlace(npc, body, screenPos, out Vector2 half);
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float shown = 1f - npc.alpha / 255f;

            batch.Draw(body, at, npc.frame, npc.GetAlpha(drawColor), npc.rotation, half, npc.scale, flip, 0f);
            batch.Draw(glow, at, npc.frame, new Color(255, 255, 255, 0) * shown, npc.rotation, half, npc.scale, flip, 0f);

            if (red)
                batch.Draw(body, at, npc.frame, new Color(255, 60, 40, 0) * (0.55f * shown), npc.rotation, half, npc.scale, flip, 0f);

            if (yellow > 0.01f)
                batch.Draw(body, at, npc.frame, new Color(255, 226, 92, 0) * (0.7f * yellow * shown), npc.rotation, half, npc.scale, flip, 0f);

            return true;
        }

        /// <summary>Its head, when it must look other than vanilla's. True: drawn here.</summary>
        private bool DrawHead(NPC npc, SpriteBatch batch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsMechQueenUp)
                return false;

            DrawWarnings(npc, batch);

            bool lit = PADWorld.Legends && HeadLit;
            bool red = chargeFront >= 0f;
            float yellow = Math.Max(stormGlow, zapGlow);
            return DrawPart(npc, batch, screenPos, drawColor, lit, red, yellow, 0);
        }

        /// <summary>A segment of its body, when it must look other than vanilla's. True: drawn here.</summary>
        internal bool DrawSegment(NPC head, NPC npc, DestroyerPart part, SpriteBatch batch, Vector2 screenPos, Color drawColor)
        {
            bool lit = PADWorld.Legends && part.Index >= EyeLitFrom && part.Index >= 0;
            bool red = chargeFront >= 0f && part.Index >= chargeFront;
            float yellow = Math.Max(stormGlow, zapGlow);

            bool drawn = DrawPart(npc, batch, screenPos, drawColor, lit, red, yellow, npc.type == NPCID.TheDestroyerTail ? 2 : 1);

            if (lit)
                DrawEyeTrail(batch, npc, part);

            return drawn;
        }

        /// <summary>The trail behind a segment's eye, in its colour, smooth (by request: "each segment having trail in the center of the eye").</summary>
        private static void DrawEyeTrail(SpriteBatch batch, NPC npc, DestroyerPart part)
        {
            int kept = part.EyeKept;

            if (kept < 2)
                return;

            Vector2[] points = part.eyeTrail;
            float shown = 1f - npc.alpha / 255f;

            for (int k = kept - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)kept;
                DestroyerArt.DrawLine(batch, points[k], points[k - 1], 6f * f + 2f, DestroyerArt.Lit(DestroyerArt.Teal * (0.55f * f * shown)));
            }

            DestroyerArt.DrawGlow(batch, points[0], 26f, DestroyerArt.Lit(DestroyerArt.Teal * (0.5f * shown)));
            DestroyerArt.DrawGlow(batch, points[0], 10f, DestroyerArt.Lit(DestroyerArt.TealHot * (0.7f * shown)), hotCore: true);
        }
    }
}
