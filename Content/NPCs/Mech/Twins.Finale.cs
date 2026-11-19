using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- their end --------------------------------------------------------------------------------------------------------
    //
    // By request: "Death of the second: pans to it, it Cease in place, Flashing its respective color for 3 sec with a buzzer
    // like sound; then rotate around facing direction of its other twin, pans to the fallen twin, it shakes then moves then
    // hover up, rotate to the player, pans to player then have indicator from both the eyes to the player, player has 2 sec
    // to move before both of them lock onto the position, the both fly somehow to reach that position at the same time in 1
    // second, creating a giant fire ball explosion of flamable gas and fire." Asked after: the fireball a big hit, dodgeable.
    //
    //     CEASE   180   the camera goes to the last one up; held at one life, untouchable, it stops where it is, flashing
    //                   its colour (lit 18 ticks, dark 12, six times) to a buzzer (TwinsBuzzer); the player is held, and
    //                   nothing touches them. (It waits on here, if need be, till its twin is on the ground.)
    //     TURN     45   it turns to face its twin
    //     PAN      70   the camera goes to its twin, on the ground
    //     SHAKE    60   its twin shakes, harder and harder, sparking
    //     STIR     36   it rolls upright with a jolt, scraping
    //     RISE     80   and rises, eleven tiles, its eye coming back on
    //     AIM      40   both turn onto the player; the camera frames them both
    //     HOME     60   the camera comes home to the player
    //     WARN    120   the player is let go. A line from each eye to them, following them, brightening; the Spazmatism's
    //                   gas leaking out of its mouth, the Retinazer's going red-hot
    //     CHARGE   60   their mark locks where the player is; both fly at it, quicker and quicker, to meet there in one
    //                   second, gas and fire trailing; where they meet, the fireball (TwinsBlast, nine tiles of it hurts), the
    //                   gas they trailed catching after it; and both are gone, the last of them with the loot.
    //
    // Skipped (E held, single player): on to WARN, both in their places.

    public partial class Twins
    {
        private const int FinCease = 0;
        private const int FinTurn = 1;
        private const int FinPan = 2;
        private const int FinShake = 3;
        private const int FinStir = 4;
        private const int FinRise = 5;
        private const int FinAim = 6;
        private const int FinHome = 7;
        private const int FinWarn = 8;
        private const int FinCharge = 9;

        private const int CeaseTicks = 180;
        private const int CeaseLongest = 780;
        private const int TurnTicks = 45;
        private const int PanTicks = 70;
        private const int ShakeTicks = 60;
        private const int StirTicks = 36;
        private const int RiseTicks = 80;
        private const int AimTicks = 40;
        private const int HomeTicks = 60;
        private const int WarnTicks = 120;
        private const int ChargeTicks = 60;

        /// <summary>A flash: lit this many ticks of every thirty.</summary>
        private const int FlashLit = 18;

        /// <summary>How high its twin rises off the ground.</summary>
        private const float RiseHigh = 11f * 16f;

        /// <summary>How far the fireball hurts (by request: a big hit, dodgeable).</summary>
        public const float BlastReach = 9f * 16f;

        public static readonly SoundStyle Buzzer = new("PisAndDag/Content/Sounds/Mech/TwinsBuzzer")
        {
            Volume = 1f,
            MaxInstances = 1,
        };

        /// <summary>Where the last one up stopped; where the fireball will be.</summary>
        private Vector2 holdAt, lockAt;

        /// <summary>Where each began its charge (this machine's).</summary>
        private Vector2 chargeFrom;

        /// <summary>This machine's player was in the shot: the camera is walked home for them.</summary>
        private bool endWatching;

        /// <summary>The way the Spazmatism came at the mark, its gas behind it (this machine's), for the fireball to catch.</summary>
        internal static readonly System.Collections.Generic.List<Vector2> GasTrail = new();

        private void BeginFinale(NPC npc)
        {
            if (waiting)
            {
                waiting = false;
                npc.ai[0] = 3f;
            }

            holdAt = npc.Center + npc.velocity * 3f;
            seed = Main.rand.Next(int.MaxValue);
            ClearShots();
            Go(npc, Scene.Finale, FinCease);
        }

        /// <summary>Where its fallen twin hangs once it has risen.</summary>
        private static Vector2 RisenAt(Twins fallen) => fallen.restAt + new Vector2(0f, -RiseHigh);

        private void RunFinale(NPC npc, bool entered, NPC other, Twins twin)
        {
            int t = timer;
            Player p = Target(npc);
            Vector2 player = p?.Center ?? holdAt + new Vector2(0f, 300f);
            bool haveTwin = other != null && twin != null && (twin.scene == Scene.Down || twin.scene == Scene.Fall);
            Vector2 fallen = haveTwin ? other.Center : holdAt;
            bool watching = Watching(npc.Center) || haveTwin && Watching(other.Center);

            if (watching && step <= FinHome)
                endWatching = true;

            // Its twin gone before the end (it should not be): it goes up alone, here and now.
            if (!haveTwin && step < FinCharge && step > FinCease)
            {
                if (Authority)
                {
                    lockAt = npc.Center;
                    Blast(npc, null);
                }

                return;
            }

            // E held in the corner (single player): on to the marking, both in their places.
            if (watching && step < FinWarn)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take() && haveTwin && twin.scene == Scene.Down)
                {
                    other.Center = RisenAt(twin);
                    Next(npc, FinWarn);

                    if (!Main.dedServ)
                        PisPlayer.PanHome(Main.LocalPlayer.Center, 30);

                    endWatching = false;
                    return;
                }
            }

            switch (step)
            {
                case FinCease:
                    {
                        // It stops where it is, flashing its colour, a buzzer going.
                        Move(npc, Vector2.Lerp(npc.Center, holdAt, 0.3f));
                        bool lit = t < CeaseTicks && t % 30 < FlashLit;
                        shake = lit ? 1.6f : 0.5f;

                        if (entered && !Main.dedServ)
                            SoundEngine.PlaySound(Buzzer, npc.Center);

                        if (t < CeaseTicks && t % 30 == 0)
                            FlashFx(npc);

                        Smoke(npc, 0.6f);
                        EyeLight(npc, lit ? 1.4f : 0.3f);

                        if (watching)
                            Shot(npc.Center, Smooth(t / 40f), MathHelper.Lerp(1f, 1.1f, Smooth(t / 60f)), spare: true);

                        bool ready = !haveTwin || twin.scene == Scene.Down && twin.timer > 20;

                        if (Authority && t >= CeaseTicks && (ready || t >= CeaseLongest))
                            Next(npc, FinTurn);

                        break;
                    }

                case FinTurn:
                    {
                        Move(npc, holdAt);
                        Turn(npc, fallen - npc.Center, 0.08f + 0.1f * Smooth(t / (float)TurnTicks));
                        Smoke(npc, 0.4f);
                        EyeLight(npc, 0.8f);

                        if (entered)
                            Whir(npc, 0.5f);

                        if (watching)
                            Shot(npc.Center, 1f, 1.1f, spare: true);

                        if (Authority && t >= TurnTicks)
                            Next(npc, FinPan);

                        break;
                    }

                case FinPan:
                    {
                        Move(npc, holdAt);
                        Turn(npc, fallen - npc.Center, 0.2f);
                        Smoke(npc, 0.3f);
                        EyeLight(npc, 0.8f);

                        if (watching)
                            Shot(Vector2.Lerp(npc.Center, fallen, Smooth(t / (float)PanTicks)), 1f, 1.1f, spare: true);

                        if (Authority && t >= PanTicks)
                            Next(npc, FinShake);

                        break;
                    }

                case FinShake:
                case FinStir:
                case FinRise:
                    {
                        Move(npc, holdAt + new Vector2(0f, 4f * (float)Math.Sin(t * 0.07f)));
                        Turn(npc, fallen - npc.Center, 0.2f);
                        Smoke(npc, 0.25f);
                        EyeLight(npc, 0.8f);

                        if (watching)
                            Shot(fallen, 1f, step == FinRise ? MathHelper.Lerp(1.1f, 1.04f, Smooth(t / (float)RiseTicks)) : 1.12f, spare: true);

                        int length = step == FinShake ? ShakeTicks : step == FinStir ? StirTicks : RiseTicks;

                        if (Authority && t >= length)
                            Next(npc, step + 1);

                        break;
                    }

                case FinAim:
                    {
                        Move(npc, holdAt + new Vector2(0f, 4f * (float)Math.Sin(t * 0.07f)));
                        Turn(npc, player - npc.Center, 0.1f + 0.15f * Smooth(t / (float)AimTicks));
                        EyeLight(npc, 1f);

                        if (entered)
                            Whir(npc, 0.8f);

                        if (watching)
                            Shot(Vector2.Lerp(fallen, (fallen + npc.Center) * 0.5f, Smooth(t / (float)AimTicks)), 1f, MathHelper.Lerp(1.04f, 1f, Smooth(t / (float)AimTicks)), spare: true);

                        if (Authority && t >= AimTicks)
                            Next(npc, FinHome);

                        break;
                    }

                case FinHome:
                    {
                        Move(npc, holdAt + new Vector2(0f, 4f * (float)Math.Sin(t * 0.07f)));
                        Turn(npc, player - npc.Center, 0.25f);
                        EyeLight(npc, 1f);

                        if (watching)
                            Shot(Vector2.Lerp((fallen + npc.Center) * 0.5f, player, Smooth(t / (float)HomeTicks)), 1f, 1f, spare: true);

                        if (t >= HomeTicks - 1 && endWatching && !Main.dedServ)
                        {
                            endWatching = false;
                            PisPlayer.PanHome(Main.LocalPlayer.Center, 24);
                        }

                        if (Authority && t >= HomeTicks)
                            Next(npc, FinWarn);

                        break;
                    }

                case FinWarn:
                    {
                        // The player let go: marked, two seconds.
                        Move(npc, holdAt + new Vector2(0f, 6f * (float)Math.Sin(t * 0.09f)));
                        Turn(npc, player - npc.Center, 0.3f);
                        Charging(npc, t / (float)WarnTicks);

                        if (Authority && t >= WarnTicks)
                        {
                            lockAt = player;
                            Next(npc, FinCharge);
                        }

                        break;
                    }

                case FinCharge:
                    {
                        if (entered)
                        {
                            chargeFrom = npc.Center;
                            Locked(npc);
                        }

                        Charge(npc, chargeFrom, t);

                        if (Authority && t >= ChargeTicks - 1)
                            Blast(npc, haveTwin ? other : null);

                        break;
                    }
            }
        }

        /// <summary>Its fallen twin, moved by the end of the one still up (on every machine, from that one's step and clock).</summary>
        private void FollowFinale(NPC npc, NPC other, Twins twin)
        {
            int t = twin.timer;
            Player p = Target(other);
            Vector2 player = p?.Center ?? npc.Center + new Vector2(0f, 300f);
            Vector2 risen = RisenAt(this);

            switch (twin.step)
            {
                case FinCease:
                case FinTurn:
                case FinPan:
                    Lie(npc, timer);
                    break;

                case FinShake:
                    {
                        float u = t / (float)ShakeTicks;
                        Move(npc, restAt);
                        npc.rotation = restRot;
                        shake = 0.8f + 4.2f * u * u;
                        Smoke(npc, 0.5f + u);

                        if (t % 9 == 0)
                            Rattle(npc, 0.4f + 0.5f * u);

                        if (t % 17 == 5)
                            Spark(npc, Main.rand.Next(), 0.6f + 0.5f * u);

                        EyeLight(npc, 0.25f + 0.5f * u, flicker: 0.8f);
                        break;
                    }

                case FinStir:
                    {
                        // A jolt, a roll upright toward its twin, a hop and a scrape.
                        float u = t / (float)StirTicks;
                        float hop = -14f * (float)Math.Sin(MathHelper.Clamp(u * 1.6f, 0f, 1f) * MathHelper.Pi);
                        float scrape = spin * -10f * Smooth(u);
                        Move(npc, restAt + new Vector2(scrape, hop));
                        npc.rotation = MathHelper.Lerp(restRot, LookAlong(other.Center - npc.Center), Smooth(u * 1.3f)) + 0.25f * (float)Math.Sin(t * 0.9f) * (1f - u);
                        shake = 2.5f * (1f - u);
                        Smoke(npc, 0.8f);

                        if (t == 0)
                            Stir(npc);

                        EyeLight(npc, 0.6f + 0.3f * u);
                        break;
                    }

                case FinRise:
                    {
                        float u = Smooth(t / (float)RiseTicks);
                        Vector2 from = restAt + new Vector2(spin * -10f, 0f);
                        Move(npc, Vector2.Lerp(from, risen, u) + new Vector2(0f, 3f * (float)Math.Sin(t * 0.2f) * (1f - u)));
                        Turn(npc, other.Center - npc.Center, 0.1f);
                        Smoke(npc, 0.6f, falling: false);

                        if (t == 0)
                            Whir(npc, 0.9f);

                        EyeLight(npc, 0.6f + 0.6f * u);
                        break;
                    }

                case FinAim:
                case FinHome:
                    Move(npc, risen + new Vector2(0f, 4f * (float)Math.Sin(t * 0.07f + 1.3f)));
                    Turn(npc, player - npc.Center, twin.step == FinAim ? 0.1f + 0.15f * Smooth(t / (float)AimTicks) : 0.25f);
                    Smoke(npc, 0.25f);
                    EyeLight(npc, 1f);
                    break;

                case FinWarn:
                    Move(npc, risen + new Vector2(0f, 6f * (float)Math.Sin(t * 0.09f + 1.3f)));
                    Turn(npc, player - npc.Center, 0.3f);
                    Charging(npc, t / (float)WarnTicks);
                    break;

                case FinCharge:
                    if (t == 0 || chargeFrom == Vector2.Zero)
                    {
                        chargeFrom = npc.Center;
                        lockAt = twin.lockAt;
                        Locked(npc);
                    }

                    Charge(npc, chargeFrom, t, twin.lockAt);
                    break;
            }
        }

        /// <summary>At its mark: from where it began, quicker and quicker, to arrive at the last tick.</summary>
        private void Charge(NPC npc, Vector2 from, int t, Vector2? mark = null)
        {
            Vector2 to = mark ?? lockAt;
            float u = Math.Min(1f, (t + 1f) / ChargeTicks);
            Move(npc, Vector2.Lerp(from, to, (float)Math.Pow(u, 2.2f)));
            npc.rotation = npc.rotation.AngleLerp(LookAlong(to - from), 0.35f);
            ChargeTrail(npc, u);
        }

        /// <summary>The fireball where they meet, and both gone: its twin first, so the last of them carries the loot. The server's.</summary>
        private void Blast(NPC npc, NPC other)
        {
            TwinsBlast.Spawn(npc, lockAt);

            if (other != null && other.TryGetGlobalNPC(out Twins twin))
                twin.End(other);

            End(npc);
        }

        // ---- drawn over it ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Over its picture: its flash in its colour (its end), its sheen and shield while it waits on its twin, and in its end its
        /// line to the player (smooth, its colour, brightening as the mark nears; fading out once they lock).
        /// </summary>
        private void DrawOver(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            // Waiting on its twin: a faint sheen of its colour on it, under its shield (drawn with the rest of what
            // Legends draws: Twins.Legends).
            if (waiting)
            {
                float pulse = 0.1f + 0.06f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
                Tint(npc, spriteBatch, screenPos, ElementOf(npc) * pulse);
            }

            DrawLegends(npc, spriteBatch, screenPos);

            if (scene == Scene.Finale && step == FinCease && timer < CeaseTicks && timer % 30 < FlashLit)
            {
                float k = 1f - (timer % 30) / (float)FlashLit * 0.35f;
                Tint(npc, spriteBatch, screenPos, ElementOf(npc) * k);
                Tint(npc, spriteBatch, screenPos, Color.White * (0.25f * k));
            }

            // Its line: its own end's, or its twin's (lying here, it is moved by its twin's).
            Twins clock = this;
            NPC aimer = npc;

            if (scene == Scene.Down && Partner(npc, out NPC other, out Twins twin) && twin.scene == Scene.Finale)
            {
                clock = twin;
                aimer = other;
            }
            else if (scene != Scene.Finale)
            {
                return;
            }

            if (clock.step != FinWarn && clock.step != FinCharge)
                return;

            Player p = Target(aimer);
            Vector2 from = Front(npc, 64f);
            Vector2 to;
            float ready, alpha;

            if (clock.step == FinWarn)
            {
                if (p == null)
                    return;

                to = p.Center;
                ready = clock.timer / (float)WarnTicks;
                alpha = Smooth(clock.timer / 12f);
            }
            else
            {
                to = clock.lockAt;
                ready = 1f;
                alpha = 1f - clock.timer / (float)DestroyerArt.WarningFade;

                if (alpha <= 0f)
                    return;
            }

            Vector2 way = to - from;
            float length = way.Length() + 140f;
            DrawMark(spriteBatch, from, way.ToRotation(), length, ready, alpha, ElementOf(npc));
        }

        /// <summary>
        /// A mark line, smooth, straight into the world's batch, in a colour: thin, flickering, brightening toward white as
        /// the lock nears; full for three quarters of its length, fading to nothing at its end (as the Destroyer's are).
        /// </summary>
        private static void DrawMark(SpriteBatch batch, Vector2 from, float angle, float length, float ready, float alpha, Color tint)
        {
            if (Main.dedServ || alpha <= 0.01f)
                return;

            float flick = 0.8f + 0.2f * (float)Math.Sin(Main.GameUpdateCount * 0.9f + from.X * 0.01f);
            Color colour = Color.Lerp(tint, Color.White, ready * ready * 0.8f) * ((0.35f + 0.55f * ready) * flick * alpha);
            float width = 2f + 3f * ready;
            Vector2 dir = angle.ToRotationVector2();
            float full = length * 0.75f;
            const int steps = 12;

            DestroyerArt.DrawLine(batch, from, from + dir * full, width * 4f, DestroyerArt.Lit(colour * 0.3f));
            DestroyerArt.DrawLine(batch, from, from + dir * full, width, DestroyerArt.Lit(colour));

            for (int i = 0; i < steps; i++)
            {
                float a = full + (length - full) * i / steps;
                float b = full + (length - full) * (i + 1) / steps;
                float left = 1f - (i + 0.5f) / steps;
                DestroyerArt.DrawLine(batch, from + dir * a, from + dir * b, width * 4f, DestroyerArt.Lit(colour * (0.3f * left)));
                DestroyerArt.DrawLine(batch, from + dir * a, from + dir * b, width, DestroyerArt.Lit(colour * left));
            }
        }
    }
}
