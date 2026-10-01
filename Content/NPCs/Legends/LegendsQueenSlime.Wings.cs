using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- her wings --------------------------------------------------------------------------------
    //
    // At half her life, in every world, before vanilla's second half starts:
    //
    //     LAND      ...   down to the ground, if she is not on it
    //     CROUCH     34   she gathers herself, lower and wider, trembling
    //     SPROUT     22   her wings stab up out of her back, folded, past their length in five
    //                     ticks and shuddering back: gel torn off her, a rush of air up, the
    //                     screen jolted, a flash of her colours
    //     SPREAD     40   they snap open in twelve, wide and big, past their size and back: the
    //                     air thrown off them both ways and out all round her, the dust blown
    //                     along the ground and the ground rolling, the screen shaken, a flash
    //     TAKEOFF    90   they lift high while she sinks into herself, come down hard, and up
    //                     she goes: the screen and the ground shake and the dust goes up with
    //                     her; a second beat carries her on up, and the beat goes on as she flies
    //
    // Untouchable and harmless throughout. Vanilla has her from the top of the climb, flying.

    public partial class LegendsQueenSlime
    {
        private const int WingsLand = 0;
        private const int WingsCrouch = 1;
        private const int WingSprout = 2;
        private const int WingsSpread = 3;
        private const int WingsTakeoff = 4;

        private const int LandMost = 90;
        private const int CrouchTicks = 34;
        private const int SproutTicks = 22;
        private const int SpreadTicks = 40;
        private const int TakeoffTicks = 90;

        /// <summary>Ticks into SPROUT when the wings are all the way out, and past it.</summary>
        private const int StabTicks = 5;

        /// <summary>How far past their length the wings stab (a share of it), and how far out they flick as they do (radians).</summary>
        private const float SproutPast = 0.4f;
        private const float SproutFlick = 0.25f;

        /// <summary>Ticks into SPREAD when they are all the way open.</summary>
        private const int SpreadOpen = 12;

        /// <summary>How big her wings are, spread wide, before they settle to her flight's size; and how far past it they snap.</summary>
        private const float SpreadSize = 1.25f;
        private const float SpreadSnap = 0.17f;

        /// <summary>Ticks into TAKEOFF when her wings are all the way up, when the first beat comes down, and how long the rest of that beat takes.</summary>
        private const int RaiseEnd = 14;
        private const int PushAt = 22;
        private const int PushBeatTicks = 34;

        /// <summary>The push of the first beat, and how quickly the climb dies away after it; the push of the second.</summary>
        private const float PushSpeed = 12f;
        private const float ClimbDrag = 0.93f;
        private const float SecondPush = 5f;

        /// <summary>The tick of TAKEOFF the second beat's push comes: a climbing beat after the first.</summary>
        private static int SecondPushAt => PushAt + PushBeatTicks + (int)Math.Ceiling(PowerAt * ClimbBeat);

        /// <summary>She was on the ground when the beat came down: the ground heaves and its dust goes up.</summary>
        private bool pushedOffGround;

        /// <summary>The server's: every world, at half her life. Whatever she was in the middle of is over.</summary>
        private void StartWings(NPC npc)
        {
            // A teleport half done leaves her small and hidden: whole again.
            if (npc.scale != 1f)
                Resize(npc, 1f);

            npc.hide = false;
            npc.rotation = 0f;
            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;

            Go(npc, Scene.Wings, npc.velocity.Y == 0f && !npc.noGravity ? WingsCrouch : WingsLand);
        }

        /// <summary>Her size, about her bottom middle, as vanilla does it (width 114, height 100 at 1).</summary>
        private static void Resize(NPC npc, float scale)
        {
            Vector2 bottom = npc.Bottom;

            npc.scale = scale;
            npc.width = (int)(114f * scale);
            npc.height = (int)(100f * scale);
            npc.Bottom = bottom;
        }

        private void RunWings(NPC npc, bool entered)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;

            Player target = Main.player[npc.target];

            if (target.active)
                npc.direction = target.Center.X < npc.Center.X ? -1 : 1;

            int t = timer;
            bool flying = beat == WingsTakeoff && t >= PushAt;

            npc.noGravity = flying;
            npc.noTileCollide = flying;

            switch (beat)
            {
                case WingsLand:
                    npc.velocity.X *= 0.9f;

                    if (Authority && ((t > 1 && npc.velocity.Y == 0f) || t >= LandMost))
                        Next(npc, WingsCrouch);

                    break;

                case WingsCrouch:
                    Settle(npc);

                    if (entered && !Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Pitch = -0.5f, Volume = 0.6f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.8f, Volume = 0.6f }, npc.Center);
                    }

                    // Gathering: gel runs down her, a light starts in her.
                    if (!Main.dedServ && Main.rand.NextBool(3))
                        Drip(npc, 1);

                    if (Authority && t >= CrouchTicks)
                        Next(npc, WingSprout);

                    break;

                case WingSprout:
                    Settle(npc);

                    if (entered)
                        Sprout(npc);

                    if (Authority && t >= SproutTicks)
                        Next(npc, WingsSpread);

                    break;

                case WingsSpread:
                    Settle(npc);

                    if (Crossed(SpreadOpen))
                        Spread(npc);

                    if (Authority && t >= SpreadTicks)
                        Next(npc, WingsTakeoff);

                    break;

                case WingsTakeoff:
                    if (entered)
                    {
                        beatHold = 0;
                        beatGlide = 0;
                        beatHeld = false;
                    }

                    if (t < PushAt)
                    {
                        // Wings going up, she going down into herself.
                        Settle(npc);

                        if (entered)
                            Kick(-0.14f, 0f, 0.1f);
                    }
                    else
                    {
                        if (Crossed(PushAt))
                        {
                            pushedOffGround = npc.velocity.Y == 0f;
                            npc.velocity.Y = -PushSpeed;
                            Push(npc);
                        }

                        npc.velocity.X *= 0.9f;
                        npc.velocity.Y *= ClimbDrag;

                        // The dust she raised goes up with her.
                        if (pushedOffGround && t < PushAt + 36)
                            Rising(npc, 1f - (t - PushAt) / 36f);

                        // The second beat: up again.
                        if (Crossed(SecondPushAt))
                        {
                            npc.velocity.Y -= SecondPush;
                            Kick(-0.065f, 0f, 0.05f);
                            WingGust(npc, 1.3f);
                        }
                    }

                    // The beat, off this scene's own ticks, so it goes on from here as she flies.
                    beatPhase = t >= PushAt ? TakeoffPhase(t) : 0f;

                    if (Authority && t >= TakeoffTicks)
                    {
                        transitioned = true;
                        Release(npc);
                    }

                    break;
            }
        }

        /// <summary>Stood where she is, sliding to a stop.</summary>
        private static void Settle(NPC npc)
        {
            if (npc.velocity.Y == 0f)
            {
                npc.velocity.X *= 0.8f;

                if (Math.Abs(npc.velocity.X) < 0.1f)
                    npc.velocity.X = 0f;
            }
        }

        /// <summary>
        /// They stab out of her: gel torn off her back and thrown high, a wet tearing and a deep
        /// thump, the rush of them up, a burst of light at each root, the screen jolted.
        /// </summary>
        private void Sprout(NPC npc)
        {
            Kick(0.32f, 0f, 0.75f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.35f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = 0.2f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_BookStaffCast with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, Volume = 0.55f }, npc.Center);

            float near = Near(npc.Center);

            if (near > 0f)
            {
                Main.LocalPlayer.PIS().BumpScreenShake(8f * near, 22);
                Cinematic.Flash(QueenSlimeArt.Shade(shapeClock * 0.02f), 0.12f * near);
            }

            Pose pose = Posed(npc);

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 root = pose.At(WingRootLocal(npc, s));

                for (int i = 0; i < 14; i++)
                {
                    Vector2 velocity = new Vector2(s * Main.rand.NextFloat(0.5f, 4f), Main.rand.NextFloat(-11f, -4f));
                    PADParticleHandler.SpawnParticle(new SlimeGlob(root, velocity, Main.rand.NextFloat(0.7f, 1.3f), 80, GelColour()));
                }

                SparkShard.Spray(root, Color.White, 10, 8f, 56f, 16, palette: QueenSlimeArt.Sparks);
                PADParticleHandler.SpawnParticle(new BloomGeneric(root, Vector2.Zero, QueenSlimeArt.Shade(shapeClock * 0.02f + s), 1.2f, 16));

                // The rush of them, up.
                for (int i = 0; i < 3; i++)
                {
                    Vector2 at = root + new Vector2(s * Main.rand.NextFloat(0f, 24f), -Main.rand.NextFloat(30f, 150f));
                    PADParticleHandler.SpawnParticle(new WindStreak(at, new Vector2(s * Main.rand.NextFloat(0f, 1.5f), -Main.rand.NextFloat(12f, 18f)),
                        Main.rand.NextFloat(110f, 190f), Main.rand.NextFloat(6f, 10f), Main.rand.Next(12, 18)));
                }
            }
        }

        /// <summary>
        /// Snapped open: the air thrown off them both ways and out all round her, the dust along
        /// the ground blown out and the ground rolling, light off each wing, the screen shaken, a
        /// flash of her colours, the boom and the rush of it.
        /// </summary>
        private void Spread(NPC npc)
        {
            ripple = Math.Min(1f, ripple + 0.5f);
            Kick(-0.16f, 0f, 0f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = 0f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.6f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.55f, Volume = 0.7f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.5f, Volume = 0.9f }, npc.Center);

            float near = Near(npc.Center);

            if (near > 0f)
            {
                Main.LocalPlayer.PIS().BumpScreenShake(10f * near, 26);
                Cinematic.Flash(QueenSlimeArt.Shade(shapeClock * 0.02f), 0.22f * near);
            }

            float floor = npc.position.Y + npc.height;
            Pose pose = Posed(npc);

            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 28; i++)
                {
                    Vector2 at = new Vector2(npc.Center.X + s * Main.rand.NextFloat(20f, 110f), floor - Main.rand.NextFloat(2f, 20f));
                    Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, new Vector2(s * Main.rand.NextFloat(6f, 14f), Main.rand.NextFloat(-2f, 0f)),
                        60, default, Main.rand.NextFloat(1.1f, 2f));
                    dust.noGravity = true;
                }

                for (int i = 0; i < 7; i++)
                {
                    Vector2 at = npc.Center + new Vector2(s * Main.rand.NextFloat(60f, 200f), Main.rand.NextFloat(-120f, 40f));
                    PADParticleHandler.SpawnParticle(new WindStreak(at, new Vector2(s * Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(-1.5f, 1.5f)),
                        Main.rand.NextFloat(110f, 220f), Main.rand.NextFloat(7f, 12f), Main.rand.Next(20, 32)));
                }

                // Light off the middle of each wing.
                Vector2 middle = pose.At(new Vector2(s * 170f, -npc.height * 0.95f));
                PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, QueenSlimeArt.Shade(shapeClock * 0.02f + s), 1.6f, 20));
                SparkShard.Spray(middle, Color.White, 6, 7f, 44f, 14, palette: QueenSlimeArt.Sparks);
            }

            // And out all round her.
            for (int i = 0; i < 14; i++)
            {
                Vector2 way = (MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(-0.15f, 0.15f)).ToRotationVector2();
                PADParticleHandler.SpawnParticle(new WindStreak(npc.Center + way * Main.rand.NextFloat(50f, 90f), way * Main.rand.NextFloat(10f, 15f),
                    Main.rand.NextFloat(80f, 140f), Main.rand.NextFloat(6f, 9f), Main.rand.Next(16, 24)));
            }

            LegendsKingSlime.GroundDust(npc.position.X - 80f, npc.position.X + npc.width + 80f, floor, 0.7f);
            GroundWave.Slam(new Vector2(npc.Center.X, floor), npc.width * 0.5f, 16, 6f, 30);
        }

        /// <summary>
        /// The first beat comes down and she goes up: the screen and the ground shake, the ground
        /// heaves out from under her, the dust is thrown up round her.
        /// </summary>
        private void Push(NPC npc)
        {
            Kick(0.26f, 0f, 0.4f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.8f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.3f, Volume = 0.9f }, npc.Center);

            float near = Near(npc.Center);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(12f * near, 36);

            if (!pushedOffGround)
                return;

            SoundEngine.PlaySound(SoundID.Item167 with { Pitch = -0.3f, Volume = 0.7f }, npc.Bottom);

            float floor = npc.position.Y + npc.height;

            for (int i = 0; i < 30; i++)
            {
                Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X - 30f, floor - 16f), npc.width + 60, 16, DustID.Smoke, 0f, 0f, 50, default,
                    Main.rand.NextFloat(1.3f, 2f));
                dust.noGravity = true;
                dust.velocity = new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-3f, -0.5f));
            }

            QueenDust(new Vector2(npc.position.X, floor - 10f), npc.width, 10, 24, 6f);
            LegendsKingSlime.GroundDust(npc.position.X - 40f, npc.position.X + npc.width + 40f, floor, 0.9f);
            GroundWave.Slam(new Vector2(npc.Center.X, floor), npc.width * 0.5f, 30, 20f, 36);

            for (int i = 0; i < 6; i++)
            {
                Vector2 at = new Vector2(npc.Center.X + Main.rand.NextFloat(-1f, 1f) * npc.width * 0.7f, floor - Main.rand.NextFloat(4f, 20f));
                PADParticleHandler.SpawnParticle(new DustFog(at, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-1.5f, -0.4f)),
                    new Color(170, 160, 175), Main.rand.Next(50, 80), Main.rand.NextFloat(0.8f, 1.3f)));
            }
        }

        /// <summary>Dust drawn up after her as she climbs, less and less.</summary>
        private static void Rising(NPC npc, float strength)
        {
            if (Main.dedServ || Main.rand.NextFloat() >= strength)
                return;

            for (int i = 0; i < 2; i++)
            {
                Vector2 at = npc.Bottom + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * npc.width, Main.rand.NextFloat(-10f, 30f));
                Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, npc.velocity * Main.rand.NextFloat(0.5f, 0.85f) + Main.rand.NextVector2Circular(1.2f, 0.6f),
                    60, default, Main.rand.NextFloat(1.1f, 1.6f));
                dust.noGravity = true;
            }

            if (Main.rand.NextBool(3))
            {
                Vector2 at = npc.Bottom + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * npc.width, Main.rand.NextFloat(0f, 24f));
                PADParticleHandler.SpawnParticle(new DustFog(at, npc.velocity * 0.6f + new Vector2(Main.rand.NextFloat(-1f, 1f), 0f),
                    new Color(170, 160, 175), Main.rand.Next(40, 60), Main.rand.NextFloat(0.6f, 1f)));
            }
        }

        /// <summary>A spring let go: 1 at <paramref name="since"/> 0, ringing down to nothing.</summary>
        private static float Ring(float since, float damping, float speed) =>
            (float)(Math.Exp(-damping * since) * Math.Cos(speed * since));

        /// <summary>How her wings are through the transition.</summary>
        private WingLook ScriptedWings()
        {
            switch (beat)
            {
                case WingSprout:
                    {
                        // Folded, out of her and up, fast, past their length; and shuddering back.
                        float size, raise;

                        if (timer < StabTicks)
                        {
                            float p = timer / (float)StabTicks;
                            float burst = 1f - (1f - p) * (1f - p);

                            size = MathHelper.Lerp(0.2f, 1f + SproutPast, burst);
                            raise = -SproutFlick * burst;
                        }
                        else
                        {
                            float ring = Ring(timer - StabTicks, 0.2f, 0.74f);

                            size = 1f + SproutPast * ring;
                            raise = -SproutFlick * ring;
                        }

                        return new WingLook(false, QueenSlimeArt.WingFolded, size, raise, Math.Min(1f, 0.5f + timer * 0.5f));
                    }

                case WingsSpread:
                    {
                        if (timer < SpreadOpen)
                        {
                            // Snapping open, and growing past their size as they go.
                            float p = timer / (float)SpreadOpen;
                            float open = 1f - (1f - p) * (1f - p) * (1f - p);
                            int frame = QueenSlimeArt.WingFolded + (int)(open * (QueenSlimeArt.WingSpread - QueenSlimeArt.WingFolded) + 0.5f);

                            return new WingLook(false, frame, MathHelper.Lerp(1f, SpreadSize + SpreadSnap, open));
                        }

                        // Open (the beat's spread, the same frame as the last of the unfolding),
                        // ringing down to their size.
                        float ring = Ring(timer - SpreadOpen, 0.16f, 0.55f);

                        return new WingLook(true, QueenSlimeArt.BeatSpread, SpreadSize + SpreadSnap * ring, 0.1f * ring);
                    }

                case WingsTakeoff:
                    {
                        float settle = Ease((timer - PushAt) / (float)(TakeoffTicks - PushAt));
                        float size = MathHelper.Lerp(SpreadSize, FlightWingSize, settle);

                        // Up from the spread to the top of the beat.
                        if (timer < RaiseEnd)
                            return new WingLook(true, BeatFrameOf(SpreadPose * (1f - Ease(timer / (float)RaiseEnd))), size);

                        // Down, harder and harder, to the push.
                        if (timer < PushAt)
                        {
                            float p = (timer - RaiseEnd) / (float)(PushAt - RaiseEnd);
                            return new WingLook(true, BeatFrameOf(PoseAt(PowerAt) * p * p), size);
                        }

                        float phase = TakeoffPhase(timer);

                        return new WingLook(true, BeatFrameOf(PoseAt(phase)), size, 0f, 1f, Keyed(BeatLit, phase), Keyed(BeatSheen, phase));
                    }

                default:
                    return new WingLook(false, QueenSlimeArt.WingFolded, 0f, 0f, 0f);
            }
        }

        /// <summary>How far down inside her a wing's root still is as it comes out: her own pixels.</summary>
        private float WingSink()
        {
            if (scene != Scene.Wings || beat != WingSprout)
                return 0f;

            float p = Math.Min(1f, timer / (float)StabTicks);

            return 34f * (1f - p) * (1f - p);
        }

        /// <summary>
        /// A glow behind her while her wings come: a burst as they stab out, strongest as they
        /// snap open, gone as she climbs.
        /// </summary>
        private float WingsBackLight()
        {
            switch (beat)
            {
                case WingSprout:
                    return MathHelper.Lerp(0.75f, 0.35f, Ease(timer / (float)SproutTicks)) * Math.Min(1f, (timer + 1) / 3f);

                case WingsSpread:
                    return timer < SpreadOpen
                        ? MathHelper.Lerp(0.35f, 0.85f, timer / (float)SpreadOpen)
                        : 0.35f + 0.5f * (float)Math.Exp(-(timer - SpreadOpen) / 9f);

                case WingsTakeoff:
                    return 0.35f * (1f - Ease(timer / (float)TakeoffTicks));

                default:
                    return 0f;
            }
        }
    }
}
