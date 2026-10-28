using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- her change (Legends, at half her life) -----------------------------------------------------------------
    //
    // By request: "second phase transition: cinematic camera pans and zoom to her and she warps the reality and bends it
    // onto her bust and hips, so its see thru ... this lasts for 3 seconds and make it dramatic. The hand on her left
    // raise, and a bow forms out of pure light (the eventide but bigger), she uses it in her second phase. Time of day in
    // game stop flowing with a sound effect. She says something like malenia and morgott and godrick."
    //
    // In place of vanilla's own (its state 10), the moment it would begin, three seconds, and then (by request: "Her
    // transition should last longer, like maybe slower animation, 10 seconds") stretched to ten: the beats below are its
    // own count, each 3.33 ticks (ChangeSlow). Then her speech (by request, her own words, long): two stretches are drawn
    // out to hold it (ChangeKeyTicks), the bending until her third line is read, and the stillness for two and a half
    // seconds of silence (her lines there were cut from the script), so the change lands at about 13.3 seconds, the day
    // stops at about 18, she screams at about 20.6, and it ends at about 22:
    //
    //     0   she stops, and cries out (vanilla's change, her scream under it); the camera goes to her and leans in, the
    //         bars come; whoever is near hangs where they are
    //    16   the world bends: drawn in to her bust and to her hips, turning, closer and closer, and the light of
    //         everything round her pulled in after it in threads; she speaks (ChangeBendWords), and as "At me!" is read
    //    64   she changes (her second half's body, its streamers and skirt) under a flash of white; the bends let go in a
    //         wave through the world, and where they were the veil tears through her (EmpressVeil), its edges burning
    //    92   her left hand rises (she faces left through all of it: the bow's hand is the one on her left)
    //   100   light spirals in to it and is a bow (EmpressBow), its string set with a thrum at 140; behind her, a clock of
    //         her light, its hands going round and round, slower and slower
    //   150   the day stops (EmpressTime): the clock's hands with it; a ring through the world; a held silence
    //         (ChangeStillHold), the clock held behind her, the bow held up
    //   158   her scream, ending it; the bow lowered
    //   170   the camera walks home
    //   180   vanilla's again, in its second half (its pause before choosing), and her first act of it the moored volley
    //
    // Untouchable throughout; whoever is near is held, and kept from harm, as in her coming and her death. Every machine
    // plays it from the server's clock; what it changes of her (her half, the day) each machine changes as it gets there,
    // and the server's word follows.

    public partial class EmpressOfLight
    {
        /// <summary>
        /// Her change runs slower than its beats are counted (by request: "Her transition should last longer, like maybe
        /// slower animation, 10 seconds"): each beat this many ticks. What is timed by a sound's own length (the day's
        /// stopping, the bow's chord) is led by it in ticks, to land on its beat.
        /// </summary>
        private const float ChangeSlow = 600f / 180f;

        /// <summary>
        /// Two stretches of it are drawn out: the bending, to hold her speech (ChangeBendWords), from its first line to her
        /// change, which comes as "At me!" is read at the end of the third (ChangeAtMe ticks into it: the same share of
        /// the line's time as it had before "fleshy mortal" went into it); and the stillness, from the day stopping to her
        /// scream, held <see cref="ChangeStillHold"/> in silence (or, with lines in ChangeStillWords, until the last
        /// goes). The rest at ChangeSlow. The beats (ChangeKeys) and the ticks they fall on (ChangeKeyTicks); worked out
        /// once, the same on every machine.
        /// </summary>
        private const int ChangeAtMe = 208;

        /// <summary>The stillness held in silence, the day stopped and her bow up, before her scream: two and a half seconds.</summary>
        private const int ChangeStillHold = 150;

        private static readonly int[] ChangeKeys = { 0, 16, 64, 150, 158, 180 };
        private static int[] changeKeyTicks;

        private static int[] ChangeKeyTicks
        {
            get
            {
                if (changeKeyTicks != null)
                    return changeKeyTicks;

                int bend = Taunt.TicksFor(new[] { ChangeBendWords[0], ChangeBendWords[1] }) + ChangeAtMe;
                int still = ChangeStillWords.Length > 0 ? Taunt.TicksFor(ChangeStillWords) - Taunt.FadeOut - 8 : ChangeStillHold;
                int[] ticks = new int[ChangeKeys.Length];

                for (int k = 1; k < ChangeKeys.Length; k++)
                {
                    int span = (int)Math.Round((ChangeKeys[k] - ChangeKeys[k - 1]) * ChangeSlow);

                    if (ChangeKeys[k] == ChangeBecome)
                        span = Math.Max(span, bend);
                    else if (ChangeKeys[k] == ChangeScream)
                        span = Math.Max(span, still);

                    ticks[k] = ticks[k - 1] + span;
                }

                return changeKeyTicks = ticks;
            }
        }

        /// <summary>The tick a beat of her change falls on.</summary>
        private static int At(int beat)
        {
            int[] ticks = ChangeKeyTicks;

            for (int k = 1; k < ChangeKeys.Length; k++)
            {
                if (beat <= ChangeKeys[k] || k == ChangeKeys.Length - 1)
                {
                    float s = (beat - ChangeKeys[k - 1]) / (float)(ChangeKeys[k] - ChangeKeys[k - 1]);
                    return (int)Math.Round(MathHelper.Lerp(ticks[k - 1], ticks[k], s));
                }
            }

            return 0;
        }

        /// <summary>How far through her change, in its beats.</summary>
        private float ChangeNow
        {
            get
            {
                int[] ticks = ChangeKeyTicks;

                for (int k = 1; k < ChangeKeys.Length; k++)
                {
                    if (timer <= ticks[k] || k == ChangeKeys.Length - 1)
                    {
                        float s = (timer - ticks[k - 1]) / (float)Math.Max(1, ticks[k] - ticks[k - 1]);
                        return MathHelper.Lerp(ChangeKeys[k - 1], ChangeKeys[k], s);
                    }
                }

                return 0f;
            }
        }

        private const int ChangeTicks = 180;
        private const int ChangeWarpFrom = 16;
        private const int ChangeBecome = 64;
        private const int ChangeRaise = 92;
        private const int ChangeBowFrom = 100;
        private const int ChangeBowSet = 140;
        private const int ChangeStop = 150;
        private const int ChangeScream = 158;
        private const int ChangeHome = 170;

        /// <summary>
        /// The day's stopping is heard ahead of it: its ticks, slowing, and then the frozen chord, which lands this many ticks
        /// after the sound starts (EmpressSounds.TimeStop: 0.86 seconds in), on the moment the clock's hands stop.
        /// </summary>
        private const int TimeStopLead = 52;

        /// <summary>
        /// The bow's forming is heard ahead of it too: light spiralling in, and then its chord and the string's thrum, which
        /// land this many ticks after the sound starts (EmpressSounds.BowForm: 0.95 seconds in), on the moment its string
        /// is set.
        /// </summary>
        private const int BowFormLead = 57;

        /// <summary>The shimmer played backwards, at its own pitch (1.5 seconds), ending on the moment she changes.</summary>
        private const int ChangeSwellTicks = 90;

        /// <summary>Her bust and her hips, from her middle, at size 1: where the world is bent into her, and her veil tears.</summary>
        private static readonly Vector2 Bust = new Vector2(0f, -20f);
        private static readonly Vector2 Hips = new Vector2(0f, 17f);

        /// <summary>The threads of light pulled in: how many to each place, and how much of the way in each is long.</summary>
        private const int Threads = 20;
        private const float ThreadLength = 0.14f;

        /// <summary>A point on a thread's way in to <paramref name="at"/>: far out at 0, there at 1, going round as it comes.</summary>
        private static Vector2 Spiral(Vector2 at, float from, float turn, float run, float warp)
        {
            run = MathHelper.Clamp(run, 0f, 1f);
            float far = MathHelper.Lerp(760f, 24f, (float)Math.Pow(run, 0.8)) * (1.15f - 0.35f * warp);
            return at + (from + turn * run * 2.4f).ToRotationVector2() * far;
        }

        /// <summary>The bow arm through her change: at rest, then raised high over her, the bow held up level.</summary>
        private const float RaisedArm = -2.3f;
        private const float RaisedAim = MathHelper.Pi;

        /// <summary>The clock behind her: how far from her middle its marks are; how fast its long hand goes round at first.</summary>
        private const float ClockRadius = 330f;
        private const float ClockSpin = 0.55f;

        /// <summary>This machine's player was in her change's shot on the last tick: the camera is walked home for them.</summary>
        private bool changeWatching;

        /// <summary>
        /// Her change begins: the server's, at vanilla's choice of its own (LegendsAfter). Whatever of her first half was
        /// still going is put down.
        /// </summary>
        private void BeginChange(NPC npc)
        {
            KillHitboxes(npc);
            sawing = false;
            act = Act.None;
            grabbed = -1;
            npc.velocity *= 0.4f;

            Go(npc, Scene.Change, 0);
        }

        private void RunChange(NPC npc, bool entered)
        {
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.damage = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.rotation = 0f;
            npc.Opacity = 1f;

            // Facing left all through: the bow forms in the hand on her left.
            npc.spriteDirection = npc.direction = -1;

            int t = timer;
            float c = ChangeNow;

            // She stops, and hangs, breathing.
            npc.velocity *= 0.82f;

            if (npc.velocity.LengthSquared() < 0.04f)
                npc.velocity = new Vector2(0f, (float)Math.Sin(t * 0.06f) * 0.25f);

            if (NearArena())
                EmpressSky.Darken(1f);

            if (InDeath(npc))
                EmpressArenaPlayer.Hover();

            // E held in the corner: straight to her second half (single player; CinematicSkip).
            if (InDeath(npc))
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    SkipChange(npc);
                    return;
                }
            }

            ChangeShot(npc, t, c);
            ChangeArms(c);

            if (entered)
                ChangeBegins(npc);

            if (Crossed(At(ChangeWarpFrom)))
                ChangeLine(npc, 0);

            // The backwards shimmer, at its own pitch, swelling to end on her change.
            if (Crossed(At(ChangeBecome) - ChangeSwellTicks) && !Main.dedServ)
                Sound(EmpressSounds.Swell, npc.Center, 0f, 0.8f, 2);

            if (c >= ChangeWarpFrom && c < ChangeBecome)
                Warping(npc, t);

            if (Crossed(At(ChangeBecome)))
                Become(npc);

            if (c >= ChangeBecome && c < ChangeBecome + 50)
                Tearing(npc, t - At(ChangeBecome));

            if (Crossed(At(ChangeRaise)))
                Raising(npc);

            if (Crossed(At(ChangeStop) - TimeStopLead))
                Sound(EmpressSounds.TimeStop, npc.Center, 0f, 1f, 1);

            if (Crossed(At(ChangeBowSet) - BowFormLead))
                Sound(EmpressSounds.BowForm, npc.Center, 0f, 0.95f, 1);

            if (c >= ChangeBowFrom && c < ChangeBowSet)
                BowForming(npc, t);

            if (Crossed(At(ChangeBowSet)))
                BowSet(npc);

            if (Crossed(At(ChangeStop)))
                TimeStops(npc);

            if (Crossed(At(ChangeScream)))
                Scream(npc, -0.15f, 1f, rings: true);

            if (Crossed(At(ChangeHome)))
                ChangeShotHome(npc);

            if (Authority && c >= ChangeTicks)
                EndChange(npc);
        }

        /// <summary>Over: vanilla's again (its pause before choosing, its second half), her first act to come the moored volley. The server's.</summary>
        private void EndChange(NPC npc)
        {
            unveiled = true;
            newNext = true;
            firstSecond = true;

            Release(npc);
        }

        /// <summary>
        /// Her change, skipped (CinematicSkip; single player): what it does to her done at once (her second half, her veil,
        /// the day stopped), under a flash of white; her speech cut; the camera home; her second half's fight.
        /// </summary>
        private void SkipChange(NPC npc)
        {
            if (npc.ai[3] == 0f)
                npc.ai[3] = 1f;
            else if (npc.ai[3] == 2f)
                npc.ai[3] = 3f;

            npc.netUpdate = true;

            Taunt.Hush(npc);

            if (!Main.dedServ)
                Cinematic.Flash(Color.White, 0.35f * Near(npc.Center));

            ChangeShotHome(npc);
            EndChange(npc);
        }

        // ---- the shot ---------------------------------------------------------------------------------------------

        /// <summary>For a player near: the camera to her, leaning in, closer as the day stops; the bars; held.</summary>
        private void ChangeShot(NPC npc, int t, float c)
        {
            if (c >= ChangeHome)
                return;

            changeWatching = InDeath(npc);

            if (!changeWatching)
                return;

            float lean = MathHelper.Lerp(1f, 1.45f, Ease(c / 70f)) + 0.12f * Ease((c - ChangeBowFrom) / 50f) - 0.06f * Ease((c - ChangeStop) / 10f);

            PisPlayer.BlendCamera(npc.Center + new Vector2(0f, -12f), Smoother(t / 34f));
            Cinematic.Hold();
            Cinematic.Frame();
            Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)), 0.1f);
        }

        /// <summary>The camera walked home from her, once, for a player who was in the shot.</summary>
        private void ChangeShotHome(NPC npc)
        {
            if (changeWatching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center + new Vector2(0f, -12f), 50);

            changeWatching = false;
        }

        /// <summary>Her bow arm and her other arm through the change.</summary>
        private void ChangeArms(float t)
        {
            float raise = Ease((t - ChangeRaise) / 18f) * (1f - Ease((t - ChangeScream) / 16f));

            HoldBowUp(LerpAngle(MathHelper.Pi - RestArm, RaisedArm, raise), LerpAngle(MathHelper.Pi - RestAim, RaisedAim, raise));

            // Her other hand up and out as the world is drawn in; at her side after.
            FreeArmFrame(t < ChangeWarpFrom ? 2 : t < ChangeBecome + 8 ? 3 : 0);
        }

        // ---- how far it has come (drawn) ----------------------------------------------------------------------------

        /// <summary>How far the world is bent into her (0 to 1, and gone at once as she changes).</summary>
        private float ChangeWarp() => scene == Scene.Change && ChangeNow < ChangeBecome ? Ease((ChangeNow - ChangeWarpFrom) / (float)(ChangeBecome - ChangeWarpFrom)) : 0f;

        /// <summary>
        /// Her veil: whole the moment she changes (never her second body bare), its streaks white at first and slowly
        /// going clear (ChangeVeilWhite).
        /// </summary>
        private float ChangeVeil() => ChangeNow < ChangeBecome ? 0f : 1f;

        private float ChangeVeilWhite() => ChangeNow < ChangeBecome ? 0f : 1f - Ease((ChangeNow - ChangeBecome - 10f) / 70f);

        private float ChangeVeilFlare() => ChangeNow < ChangeBecome ? 0f : 1f - Ease((ChangeNow - ChangeBecome - 26f) / 50f);

        private float ChangeBowForm() => Ease((ChangeNow - ChangeBowFrom) / (float)(ChangeBowSet - ChangeBowFrom));

        /// <summary>The clock's long hand: round and round, slower and slower, and still from the moment the day stops.</summary>
        private float ClockHand()
        {
            float s = MathHelper.Clamp((ChangeNow - ChangeRaise) / (float)(ChangeStop - ChangeRaise), 0f, 1f);
            float span = ChangeStop - ChangeRaise;

            // Its speed falling to nothing as the square of what is left: gone round this far by then.
            return -MathHelper.PiOver2 + ClockSpin * span / 3f * (1f - (float)Math.Pow(1f - s, 3));
        }

        /// <summary>How much of the clock shows: coming up with the bow, held, and gone after the day has stopped.</summary>
        private float ClockShown() => Ease((ChangeNow - ChangeRaise) / 14f) * (1f - Ease((ChangeNow - ChangeStop - 8f) / 22f));

        // ---- what is seen and heard ------------------------------------------------------------------------------

        /// <summary>It begins: her change's cry, her scream, a backwards shimmer swelling to the moment she changes.</summary>
        private void ChangeBegins(NPC npc)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Item161, npc.Center, 0f, 1f);
            Scream(npc, -0.3f, 1f, rings: true);

            // A low hum under it all; the shimmer played backwards comes later, to end on her change (RunChange).
            Sound(EmpressSounds.Hum, npc.Center, -0.5f, 0.7f, 2);
            Shake(npc.Center, 4f, 14);
        }

        /// <summary>Her speech, at the foot of the screen, for whoever is near: the first as the world bends, the second as the day stops.</summary>
        private void ChangeLine(NPC npc, int line)
        {
            string[] words = line == 0 ? ChangeBendWords : ChangeStillWords;

            if (Main.dedServ || !NearArena() || words.Length == 0)
                return;

            Taunt.Speech(npc, words, EmpressArt.Pink, EmpressArt.Sky, reach: 0f);
        }

        /// <summary>The world bending into her bust and hips; threads of light pulled in; a wobble climbing.</summary>
        private void Warping(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            float w = ChangeWarp();

            if (t % 16 == 0)
                Sound(EmpressSounds.Wobble, npc.Center, MathHelper.Lerp(-0.6f, 0.4f, w), 0.35f + 0.3f * w, 2);

            // Light out of the world about her, drawn in.
            for (int i = 0; i < 2; i++)
            {
                Vector2 to = npc.Center + (i == 0 ? Bust : Hips) * npc.scale;
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 from = to + way * Main.rand.NextFloat(220f, 520f);
                Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, (to - from) * Main.rand.NextFloat(0.05f, 0.08f), 0, EmpressArt.Prism(Main.rand.NextFloat()), 1.1f + w);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }

            Lighting.AddLight(npc.Center, new Vector3(1f, 0.85f, 1f) * (0.6f + w));
        }

        /// <summary>She changes: her second half; white, a wave through the world, glass, the light of her torn.</summary>
        private void Become(NPC npc)
        {
            // Her half, on every machine as it comes here (the server's is sent).
            if (npc.ai[3] == 0f)
                npc.ai[3] = 1f;
            else if (npc.ai[3] == 2f)
                npc.ai[3] = 3f;

            if (Authority)
                npc.netUpdate = true;

            if (Main.dedServ)
                return;

            float near = Near(npc.Center);

            Sound(EmpressSounds.Shock, npc.Center, -0.2f, 1f, 2);
            Sound(EmpressSounds.Shards, npc.Center, 0.2f, 0.85f, 2);
            Sound(EmpressSounds.Crinkle, npc.Center, 0.3f, 0.7f, 2);
            Sound(SoundID.Item29, npc.Center, -0.5f, 0.8f);

            Cinematic.Flash(Color.White, 0.4f * near);
            Shake(npc.Center, 10f, 22);
            EmpressWarp.Ripple(npc.Center, 30f, 110f, 26f, 40);

            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, Color.White, 520f / 160f, 26));
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Pink, 760f / 160f, 34));

            foreach (Vector2 place in new[] { Bust, Hips })
            {
                Vector2 at = npc.Center + place * npc.scale;
                PADParticleHandler.SpawnParticle(new CrownGlint(at, 110f, 30, Color.White));
                SparkShard.Spray(at, Color.White, 16, 9f, 90f, 22, palette: EmpressArt.Sparks);
            }
        }

        /// <summary>The veil tearing through her, its edges burning: glass crackling off them.</summary>
        private void Tearing(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            if (t % 9 == 0)
                Sound(SoundID.Item27, npc.Center, Main.rand.NextFloat(0.2f, 0.8f), 0.35f, 3);

            if (Main.rand.NextBool(2))
            {
                Vector2 at = npc.Center + (Main.rand.NextBool() ? Bust : Hips) * npc.scale + Main.rand.NextVector2Circular(46f, 10f);
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, Color.Lerp(Color.White, EmpressArt.Prism(Main.rand.NextFloat()), 0.5f), 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        /// <summary>Her left hand up: a bright note, a glint in the palm.</summary>
        private void Raising(NPC npc)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Item29, npc.Center, 0.6f, 0.6f);
            Sound(EmpressSounds.WhooshUp, npc.Center, 0.2f, 0.6f, 2);
            PADParticleHandler.SpawnParticle(new CrownGlint(Palm(npc, true, RaisedArm), 70f, 24, Color.White));
        }

        /// <summary>The bow forming: light spiralling in to her hand.</summary>
        private void BowForming(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            float p = (ChangeNow - ChangeBowFrom) / (float)(ChangeBowSet - ChangeBowFrom);
            Vector2 hand = Palm(npc, true, bowArmNow);

            for (int i = 0; i < 2; i++)
            {
                float a = t * 0.31f + i * MathHelper.Pi;
                float r = MathHelper.Lerp(260f, 60f, p) + Main.rand.NextFloat(-20f, 20f);
                Vector2 from = hand + a.ToRotationVector2() * r;
                Vector2 swirl = (hand - from).RotatedBy(1.1) * 0.07f;
                Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, swirl, 0, EmpressArt.Prism(t / 40f + i * 0.5f), 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        /// <summary>The bow made: its string set with a thrum, a flash in her hand.</summary>
        private void BowSet(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 hand = Palm(npc, true, bowArmNow);

            Sound(SoundID.Item5, hand, -0.6f, 0.9f);
            Sound(EmpressSounds.Chime, hand, 0.4f, 0.5f, 2);
            bowTwang = 0.8f;

            PADParticleHandler.SpawnParticle(new BloomGeneric(hand, Vector2.Zero, Color.White, 260f / 160f, 18));
            PADParticleHandler.SpawnParticle(new CrownGlint(hand, 130f, 26, Color.White));
            SparkShard.Spray(hand, Color.White, 12, 7f, 60f, 18, palette: EmpressArt.Sparks);
        }

        /// <summary>The day stops: the clock's hands with it; a pale flash, a slow ring through the world; her second line.</summary>
        private void TimeStops(NPC npc)
        {
            unveiled = true;

            if (Main.dedServ)
                return;

            float near = Near(npc.Center);

            Cinematic.Flash(EmpressArt.Sky, 0.3f * near);
            Shake(npc.Center, 6f, 18);
            EmpressWarp.Ripple(npc.Center, 22f, 150f, 16f, 60);

            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Sky, 900f / 160f, 40));
            PADParticleHandler.SpawnParticle(new CrownGlint(npc.Center, 260f, 40, Color.White));

            ChangeLine(npc, 1);
        }

        // ---- drawing ----------------------------------------------------------------------------------------------

        private void DrawChangeHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float t = ChangeNow;

            // White over her as she changes (hiding the moment it is done), and her light thrown up as the day stops.
            float white = Ease((t - (ChangeBecome - 12f)) / 12f) * (1f - Ease((t - ChangeBecome) / 22f));
            float bright = 0.35f * ChangeWarp() + 0.5f * (1f - Ease((t - ChangeStop) / 20f)) * Ease((t - ChangeStop + 4f) / 4f);
            float glow = 0.45f + 0.5f * ChangeWarp() + 0.4f * ClockShown();

            // The clock of her light behind her.
            Restart(spriteBatch, true);
            DrawClock(npc, spriteBatch, ClockShown());
            Restart(spriteBatch, false);

            DrawSecondHer(npc, spriteBatch, screenPos, bright, glow, 1f, null, white);
        }

        /// <summary>
        /// A clock of her light, behind her: a ring, twelve marks round it (stars), and two hands from her middle, the long
        /// one going round and round and the short one slowly; slowing, and stopped with the day.
        /// </summary>
        private void DrawClock(NPC npc, SpriteBatch spriteBatch, float shown)
        {
            if (shown <= 0.003f)
                return;

            float time = (float)Main.timeForVisualEffects;
            Vector2 middle = npc.Center;
            float stopped = ChangeNow >= ChangeStop ? 1f : 0f;
            float pulse = 1f + 0.35f * stopped * (1f - Ease((ChangeNow - ChangeStop) / 12f));

            EmpressArt.DrawBloom(spriteBatch, middle, ClockRadius * 2.6f, EmpressArt.Additive(EmpressArt.Sky, 0.12f * shown));
            DrawRing(spriteBatch, middle, ClockRadius * pulse, 3f, EmpressArt.Additive(Color.White, 0.5f * shown));
            DrawRing(spriteBatch, middle, ClockRadius * pulse, 12f, EmpressArt.Additive(EmpressArt.Prism(time / 300f), 0.22f * shown));
            DrawRing(spriteBatch, middle, ClockRadius * 0.82f * pulse, 2f, EmpressArt.Additive(EmpressArt.Lilac, 0.3f * shown));

            for (int k = 0; k < 12; k++)
            {
                float a = -MathHelper.PiOver2 + k * MathHelper.TwoPi / 12f;
                Vector2 mark = middle + a.ToRotationVector2() * ClockRadius * pulse;
                bool hour = k % 3 == 0;
                float twinkle = stopped > 0f ? 1f : 0.75f + 0.25f * (float)Math.Sin(time * 0.15f + k);

                EmpressArt.DrawStar(spriteBatch, mark, (hour ? 56f : 30f) * twinkle, a, EmpressArt.Additive(Color.White, 0.85f * shown));
                EmpressArt.DrawBloom(spriteBatch, mark, hour ? 40f : 24f, EmpressArt.Additive(EmpressArt.Prism(k / 12f), 0.55f * shown));
            }

            float hand = ClockHand();
            float shortHand = -MathHelper.PiOver2 + (hand + MathHelper.PiOver2) / 12f;

            EmpressArt.DrawBeam(spriteBatch, middle, middle + hand.ToRotationVector2() * ClockRadius * 0.92f, 10f, EmpressArt.Additive(EmpressArt.Pink, 0.35f * shown));
            EmpressArt.DrawBeam(spriteBatch, middle, middle + hand.ToRotationVector2() * ClockRadius * 0.92f, 2.4f, EmpressArt.Additive(Color.White, 0.9f * shown));
            EmpressArt.DrawBeam(spriteBatch, middle, middle + shortHand.ToRotationVector2() * ClockRadius * 0.55f, 14f, EmpressArt.Additive(EmpressArt.Sky, 0.35f * shown));
            EmpressArt.DrawBeam(spriteBatch, middle, middle + shortHand.ToRotationVector2() * ClockRadius * 0.55f, 3.2f, EmpressArt.Additive(Color.White, 0.9f * shown));

            // A faint smear behind the long hand while it still turns.
            if (stopped <= 0f)
            {
                for (int k = 1; k <= 5; k++)
                {
                    float back = hand - k * 0.05f * (1f - Ease((ChangeNow - ChangeRaise) / (float)(ChangeStop - ChangeRaise)));
                    EmpressArt.DrawBeam(spriteBatch, middle, middle + back.ToRotationVector2() * ClockRadius * 0.92f, 6f, EmpressArt.Additive(EmpressArt.Prism(k / 6f), 0.12f * shown * (1f - k / 6f)));
                }
            }
        }

        /// <summary>Over the players: the bends at her bust and hips, the threads of light pulled in to them, the bow's light coming.</summary>
        private void DrawChangeOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            float w = ChangeWarp();

            if (w > 0.003f)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 at = npc.Center + (i == 0 ? Bust : Hips) * npc.scale;
                    float turn = i == 0 ? 1f : -1f;

                    // The world drawn in to it, turning.
                    // Both turned the same way: turned opposite ways, the two (one just over the other) summed to the world
                    // slid sideways between them, and the whole bend sat off her middle.
                    EmpressWarp.Lens(at, MathHelper.Lerp(560f, 300f, w), -0.62f * w, 1.5f * w);

                    // Threads of light, out of everything round her, spiralling in: each a curve, its head brightest.
                    for (int k = 0; k < Threads; k++)
                    {
                        float a = k / (float)Threads * MathHelper.TwoPi + i * 0.15f;
                        float run = (time * 0.02f + k * 0.618f + i * 0.5f) % 1f;
                        Color colour = EmpressArt.Prism(k / (float)Threads + time / 200f);
                        Vector2 previous = Spiral(at, a, turn, run - ThreadLength, w);

                        for (int s = 1; s <= 5; s++)
                        {
                            float along = run - ThreadLength + ThreadLength * s / 5f;
                            Vector2 next = Spiral(at, a, turn, along, w);
                            float head = s / 5f;

                            EmpressArt.DrawLine(spriteBatch, previous, next, 2f + 6f * head, EmpressArt.Additive(colour, 0.5f * w * head * Math.Min(1f, run * 3f)));
                            EmpressArt.DrawLine(spriteBatch, previous, next, 0.8f + 1.2f * head, EmpressArt.Additive(Color.White, 0.85f * w * head * Math.Min(1f, run * 3f)));
                            previous = next;
                        }

                        EmpressArt.DrawStar(spriteBatch, previous, 22f, time * 0.1f + k, EmpressArt.Additive(Color.White, 0.8f * w * Math.Min(1f, run * 3f)));
                    }

                    EmpressArt.DrawBloom(spriteBatch, at, 120f + 140f * w, EmpressArt.Additive(EmpressArt.Pink, 0.35f * w));
                    EmpressArt.DrawStar(spriteBatch, at, 90f + 120f * w, time * 0.04f * turn, EmpressArt.Additive(Color.White, 0.7f * w));
                }
            }

            // The bow's light, spiralling in.
            if (ChangeNow >= ChangeBowFrom && ChangeNow < ChangeBowSet + 6)
            {
                float p = Ease((ChangeNow - ChangeBowFrom) / (float)(ChangeBowSet - ChangeBowFrom));
                Vector2 hand = Palm(npc, true, bowArmNow);

                for (int k = 0; k < 10; k++)
                {
                    float a = k / 10f * MathHelper.TwoPi + time * 0.09f + p * 6f;
                    float r = MathHelper.Lerp(300f, 20f, p) * (0.8f + 0.2f * (float)Math.Sin(k * 1.7f + time * 0.1f));
                    Vector2 mote = hand + a.ToRotationVector2() * r;
                    Vector2 trail = hand + (a - 0.35f).ToRotationVector2() * (r + 30f * (1f - p));

                    EmpressArt.DrawBeam(spriteBatch, trail, mote, 5f, EmpressArt.Additive(EmpressArt.Prism(k / 10f + time / 150f), 0.55f * (1f - p * 0.6f)));
                    EmpressArt.DrawStar(spriteBatch, mote, 24f, a, EmpressArt.Additive(Color.White, 0.8f * (1f - p * 0.5f)));
                }

                EmpressArt.DrawBloom(spriteBatch, hand, 80f + 200f * p, EmpressArt.Additive(Color.White, 0.5f * p));
            }
        }
    }
}
