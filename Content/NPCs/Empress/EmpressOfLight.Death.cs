using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- her death --------------------------------------------------------------------------------
    //
    // By request ("Death Animation: Cinematic"), the blow that would kill her starts this instead
    // (CheckDead), in every world:
    //
    //     SLOW      80   she slows, over the player, who hangs still in the air; the camera goes to her
    //     EYES     100   light comes out of her eyes; she speaks
    //     CRACKS   ...   cracks run through her, body, arms and legs, here and there, with light
    //                    shining out of them; she speaks again
    //     CLOSE    180   the border closes in on her and the player under her, the camera on both,
    //                    and the world shakes: three seconds of it (in from wherever it stood to
    //                    the edges of the shot at once, unseen, then slowly, all of it seen)
    //     BURST     30   it is thrown out, and gone
    //     SHRINK    70   she gets smaller and smaller, all light, and bursts into butterflies (by request:
    //                    "a clutter of 20 ish, flying in different directions, updown left right, sorta
    //                    like malenia", and "butterfly critters not butterfly particles"): twenty of the
    //                    game's own, real ones a net can catch, round her colours as they go round the
    //                    ring, thrown out every way and fluttering off, her light on them for a while
    //                    (EmpressButterflies); her things fall where she was; the camera walks home
    //
    // What she says (by request, the pauses as given): "Thy kind are exceptional toy|.|.|." and then
    // "I shall observe thee |.|.|. every||where." Said, not answered (Dialogue.Aside): the panel comes up
    // over the bars, and goes down by itself.
    //
    // Untouchable throughout. The server stops her and ends her; every machine plays it.

    public partial class EmpressOfLight
    {
        private const int DeathSlow = 0;
        private const int DeathEyes = 1;
        private const int DeathCracks = 2;
        private const int DeathClose = 3;
        private const int DeathBurst = 4;
        private const int DeathShrink = 5;

        private const int SlowTicks = 80;
        private const int EyesTicks = 100;
        private const int CloseTicks = 180;
        private const int BurstTicks = 30;
        private const int ShrinkTicks = 70;

        /// <summary>Ticks into SHRINK she is a butterfly; and the size she has shrunk to by then.</summary>
        private const int ButterflyAt = 56;
        private const float SmallestShe = 0.03f;

        /// <summary>Ticks the camera takes home from her butterflies: slower than from her coming, to see them go.</summary>
        private const int ButterfliesHome = 110;

        /// <summary>Where she stops, over the player.</summary>
        private const float DeathAbove = 360f;

        /// <summary>
        /// The border closing: first, in this many ticks, from where it stood to just past the corners of
        /// the shot (out of sight, so quickly); then the rest of the three seconds slowly, to this, from
        /// the middle of it to a corner: round her and the player under her, with room.
        /// </summary>
        private const int CloseRush = 30;
        private const float CloseReach = 1100f;
        private const float CloseSize = 440f;

        /// <summary>What the border closes in on, and the camera with it: halfway between her and the player under her.</summary>
        private Vector2 CloseAt => deathAt + new Vector2(0f, DeathAbove * 0.5f);

        /// <summary>By request, word for word.</summary>
        private const string LineOne = "Thy kind are exceptional toy|.|.|.";
        private const string LineTwo = "I shall observe thee |.|.|. every||where.";

        /// <summary>How fast she speaks: slowly. Letters a tick.</summary>
        private const float Letters = 0.5f;

        /// <summary>Between her first line all said and her second begun.</summary>
        private const int LineGap = 50;

        /// <summary>
        /// Ticks a line of hers takes to all arrive on the panel (Dialogue: a letter at a time at her
        /// pace, a beat for every pause). The scene is timed by it, on the server too, which has no panel.
        /// </summary>
        private static int LineTicks(string text)
        {
            int letters = 0;
            int pauses = 0;

            foreach (char c in text)
            {
                if (c == Dialogue.Pause)
                    pauses++;
                else
                    letters++;
            }

            return (int)Math.Ceiling(letters / Letters) + pauses * Dialogue.PauseTicks;
        }

        /// <summary>Her first line, all of it said, from when it starts: the panel's fade up and then the line.</summary>
        private static readonly int LineOneTicks = Dialogue.FadeTicks + LineTicks(LineOne);

        /// <summary>Ticks into CRACKS her second line starts, and the length of the cracking (her second line all said, and a breath).</summary>
        private static readonly int LineTwoAt = LineOneTicks + LineGap - EyesTicks;
        private static readonly int CracksTicks = LineTwoAt + LineTicks(LineTwo) + 30;

        /// <summary>Where she stops, over the player (and the border closes in on).</summary>
        private Vector2 deathAt;

        /// <summary>This machine's player was in her death's shot on the last tick: the camera is walked home for them when she goes.</summary>
        private bool deathWatching;

        /// <summary>The death has started, on whichever machine the killing blow was dealt; the server's is the one that holds.</summary>
        private void BeginDeath(NPC npc)
        {
            dying = true;

            // Whatever act she was in is over (its light, its curtains, its saw; the one in her hand let go).
            act = Act.None;
            sawing = false;
            sawFade = 0;
            grabbed = -1;

            npc.damage = 0;
            npc.chaseable = false;
            npc.dontTakeDamage = true;

            npc.TargetClosest(false);
            Player target = Main.player[npc.target];
            deathAt = (target.active && !target.dead ? target.Center : npc.Center + new Vector2(0f, DeathAbove)) + new Vector2(0f, -DeathAbove);

            // What she had thrown is gone with her attention.
            if (Authority)
                ClearHerLight(npc);

            Go(npc, Scene.Death, DeathSlow);
        }

        /// <summary>Everything she has thrown, taken back, in a glint each. The server's.</summary>
        private static void ClearHerLight(NPC npc)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];

                if (!p.active || !p.hostile)
                    continue;

                bool hers = p.type == ProjectileID.HallowBossLastingRainbow || p.type == ProjectileID.HallowBossRainbowStreak
                    || p.type == ProjectileID.HallowBossDeathAurora || p.type == ProjectileID.HallowBossSplitShotCore
                    || p.type == ProjectileID.FairyQueenLance || p.type == ProjectileID.FairyQueenSunDance
                    || p.type == ModContent.ProjectileType<EmpressHitbox>() || p.type == ModContent.ProjectileType<EmpressArrow>();

                if (hers)
                    p.Kill();
            }
        }

        private void RunDeath(NPC npc, bool entered)
        {
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.damage = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.rotation = 0f;

            int t = timer;

            // Her sky stays until the border goes, for whoever is in her death or near her arena.
            if (beat < DeathBurst && (InDeath(npc) || NearArena()))
                EmpressSky.Darken(1f);

            // Everybody near hangs where they are, in her favour, till she is gone.
            if (InDeath(npc))
                EmpressArenaPlayer.Hover();

            switch (beat)
            {
                case DeathSlow:
                    {
                        // She slows (what speed she had runs out, and the game moves her by what is left),
                        // and is drawn to rest over the player.
                        npc.velocity *= 0.86f;
                        npc.Center += (deathAt - npc.Center) * (0.045f + 0.06f * Ease(t / (float)SlowTicks));
                        npc.Opacity = 1f;

                        DeathShot(npc, Ease(t / 60f), MathHelper.Lerp(1f, 1.12f, Ease(t / (float)SlowTicks)));

                        if (entered)
                            Stricken(npc);

                        if (Authority && t >= SlowTicks)
                        {
                            npc.velocity = Vector2.Zero;
                            Next(npc, DeathEyes);
                        }

                        break;
                    }

                case DeathEyes:
                    npc.velocity = Vector2.Zero;
                    npc.Center = deathAt + new Vector2(0f, Bob());
                    DeathShot(npc, 1f, MathHelper.Lerp(1.12f, 1.28f, Ease(t / 50f)));

                    if (entered)
                        EyesOpen(npc);

                    EyesBurning(npc, t, 1f);

                    if (Authority && t >= EyesTicks)
                        Next(npc, DeathCracks);

                    break;

                case DeathCracks:
                    {
                        npc.velocity = Vector2.Zero;
                        npc.Center = deathAt + new Vector2(0f, Bob());
                        DeathShot(npc, 1f, MathHelper.Lerp(1.28f, 1.15f, Ease(t / 60f)));

                        EyesBurning(npc, t, 1f);
                        Cracking(npc, t / (float)CracksTicks);

                        if (Crossed(LineTwoAt))
                            Speak(npc, LineTwo, Dialogue.AsideLinger);

                        if (Authority && t >= CracksTicks)
                            Next(npc, DeathClose);

                        break;
                    }

                case DeathClose:
                    {
                        npc.velocity = Vector2.Zero;
                        npc.Center = deathAt + new Vector2(0f, Bob() * 0.5f) + Tremble(t / (float)CloseTicks);

                        float p = t / (float)CloseTicks;
                        DeathShot(npc, 1f, MathHelper.Lerp(1.15f, 1f, Ease(t / 40f)), Vector2.Lerp(npc.Center, CloseAt, Ease(t / 40f)));

                        if (entered)
                            ClosingIn(npc);

                        Closing(npc, t, p);
                        EyesBurning(npc, t, 1f + p);

                        if (Authority && t >= CloseTicks)
                            Next(npc, DeathBurst);

                        break;
                    }

                case DeathBurst:
                    npc.velocity = Vector2.Zero;
                    npc.Center = deathAt;
                    DeathShot(npc, 1f, 1f, CloseAt);

                    if (entered)
                        Thrown(npc);

                    if (Authority && t >= BurstTicks)
                        Next(npc, DeathShrink);

                    break;

                case DeathShrink:
                    npc.velocity = Vector2.Zero;
                    npc.Center = deathAt;
                    npc.Opacity = t < ButterflyAt ? 1f : Math.Max(0f, 1f - (t - ButterflyAt) / 6f);

                    if (t < ButterflyAt)
                        DeathShot(npc, 1f, MathHelper.Lerp(1f, 1.3f, Ease(t / (float)ButterflyAt)), Vector2.Lerp(CloseAt, npc.Center, Ease(t / 30f)));
                    else
                        DeathShotHome(npc);

                    Shrinking(npc, t);

                    if (Crossed(ButterflyAt))
                        Butterfly(npc);

                    if (Authority && t >= ShrinkTicks)
                        End(npc);

                    break;
            }
        }

        /// <summary>The arena as her death has it: standing, then closing in on her, then thrown out and gone.</summary>
        private void DeathArena(NPC npc)
        {
            switch (beat)
            {
                case DeathSlow:
                case DeathEyes:
                case DeathCracks:
                    EmpressArena.Keep(arenaCentre, EmpressArena.Radius, true, true);
                    break;

                case DeathClose:
                    {
                        float rush = Ease(Math.Min(1f, timer / (float)CloseRush));
                        float slow = Ease(Math.Max(0f, (timer - CloseRush) / (float)(CloseTicks - CloseRush)));
                        float size = timer < CloseRush ? MathHelper.Lerp(EmpressArena.Radius, CloseReach, rush) : MathHelper.Lerp(CloseReach, CloseSize, slow);
                        float throb = (0.2f + 0.2f * (float)Math.Sin(timer * 0.5f)) * slow;

                        // Seen, not felt: a wall coming in this fast and this far would drag whoever it met
                        // through the ground (the walls put players back without asking the tiles).
                        EmpressArena.Keep(Vector2.Lerp(arenaCentre, CloseAt, rush), size, false, true);
                        EmpressArena.Appear(1f, 1f, throb);
                        break;
                    }

                case DeathBurst:
                    {
                        float p = MathHelper.Clamp(timer / (float)BurstTicks, 0f, 1f);
                        float out_ = 1f - (float)Math.Pow(1f - p, 3);

                        EmpressArena.Keep(CloseAt, MathHelper.Lerp(CloseSize, EmpressArena.Radius * 3f, out_), false, true);
                        EmpressArena.Appear(1f - p, 1f - Ease(p * 1.4f), 1f - p);
                        break;
                    }
            }
        }

        /// <summary>This machine's player is near enough to be in her death.</summary>
        private bool InDeath(NPC npc)
        {
            if (Main.dedServ)
                return false;

            Player me = Main.LocalPlayer;

            return me.active && !me.dead && Vector2.Distance(me.Center, npc.Center) <= SceneRange;
        }

        /// <summary>
        /// Her death's shot, for a player near: the camera <paramref name="blend"/> of the way over to her
        /// (or to <paramref name="on"/>), the bars, the lean, and the player held. The interface stays: she
        /// speaks from it.
        /// </summary>
        private void DeathShot(NPC npc, float blend, float lean, Vector2? on = null)
        {
            deathWatching = InDeath(npc);

            if (!deathWatching)
                return;

            PisPlayer.BlendCamera(on ?? npc.Center, blend);
            Cinematic.Hold();
            Cinematic.Frame();
            Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)));
        }

        /// <summary>
        /// She is gone: the camera walked home from her, once, for a player who was in the shot, slowly, the butterflies she
        /// became going out round it. The bars and the lean go by themselves.
        /// </summary>
        private void DeathShotHome(NPC npc)
        {
            if (deathWatching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, ButterfliesHome);

            deathWatching = false;
        }

        /// <summary>A line of hers, on this machine's panel, for a player near enough to hear it.</summary>
        private void Speak(NPC npc, string line, int linger)
        {
            if (!InDeath(npc))
                return;

            Dialogue.Aside(npc, line, Letters, linger);
        }

        /// <summary>The blow that would have killed her: a sound gone wrong in her, her colours thrown off her; her bow, if she had it, broken in her hand.</summary>
        private void Stricken(NPC npc)
        {
            if (Main.dedServ)
                return;

            if (unveiled && npc.AI_120_HallowBoss_IsInPhase2())
            {
                Vector2 hand = Palm(npc, BowLeft(npc), bowArmNow);
                SoundEngine.PlaySound(EmpressSounds.Shatter with { Pitch = 0.2f, Volume = 0.8f }, hand);
                SparkShard.Spray(hand, Color.White, 24, 10f, 90f, 24, palette: EmpressArt.Sparks);
                PADParticleHandler.SpawnParticle(new BloomGeneric(hand, Vector2.Zero, Color.White, 260f / 160f, 18));
            }

            SoundEngine.PlaySound(SoundID.Item160 with { Pitch = -0.55f, Volume = 0.7f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.6f, Volume = 0.8f }, npc.Center);
            Shake(npc.Center, 5f, 16);

            for (int i = 0; i < 40; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(50f, 50f), DustID.RainbowMk2, Main.rand.NextVector2Circular(6f, 6f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f), Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        /// <summary>Light comes out of her eyes: the sound of it, a flash at each, her first line.</summary>
        private void EyesOpen(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
            Cinematic.Flash(EmpressArt.Sky, 0.18f * Near(npc.Center));

            foreach (Vector2 eye in new[] { EmpressArt.LeftEye, EmpressArt.RightEye })
                PADParticleHandler.SpawnParticle(new CrownGlint(npc.Center + eye * npc.scale, 70f, 34, Color.White));

            Speak(npc, LineOne, 400);
        }

        /// <summary>The light in her eyes, held: a hum now and then, sparks off it.</summary>
        private void EyesBurning(NPC npc, int t, float strength)
        {
            if (Main.dedServ)
                return;

            if (t % 45 == 0)
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.4f, Volume = 0.25f * strength, MaxInstances = 2 }, npc.Center);

            if (Main.rand.NextFloat() < 0.35f * strength)
            {
                Vector2 eye = npc.Center + (Main.rand.NextBool() ? EmpressArt.LeftEye : EmpressArt.RightEye) * npc.scale;
                Dust dust = Dust.NewDustPerfect(eye, DustID.RainbowMk2, Main.rand.NextVector2Circular(1.5f, 1.5f) + new Vector2(0f, 1f), 0, Color.Lerp(Color.White, EmpressArt.Sky, Main.rand.NextFloat()), 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        /// <summary>The cracks opening, as far as <paramref name="p"/> of the cracking: each the sound of it as it goes, and its light.</summary>
        private void Cracking(NPC npc, float p)
        {
            if (Main.dedServ)
                return;

            List<EmpressArt.Crack> cracks = EmpressArt.Cracks(npc.AI_120_HallowBoss_IsInPhase2());

            if (cracks == null)
                return;

            float before = Math.Max(0f, (timer - 1) / (float)CracksTicks);
            int sounded = 0;

            foreach (EmpressArt.Crack crack in cracks)
            {
                if (before >= crack.At || p < crack.At || sounded >= 2)
                    continue;

                sounded++;

                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = Main.rand.NextFloat(0.1f, 0.8f), Volume = 0.45f, MaxInstances = 4 }, npc.Center);

                if (Main.rand.NextBool(3))
                    SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Pitch = Main.rand.NextFloat(0f, 0.6f), Volume = 0.35f, MaxInstances = 3 }, npc.Center);

                Shake(npc.Center, 1.5f + 2.5f * p, 6);

                // Sparks out of it as it opens.
                Vector2 at = EmpressArt.OnHer(crack.Points[0], npc.Center, npc.scale, EmpressArt.Effects(npc));
                SparkShard.Spray(at, Color.White, 4, 3.5f, 26f, 14, palette: EmpressArt.Sparks);
            }

            Lighting.AddLight(npc.Center, new Vector3(1f, 0.9f, 1f) * (0.5f + p));
        }

        /// <summary>The border begins to close: the deep sound of it, and the world starts to shake.</summary>
        private void ClosingIn(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.8f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -1f, Volume = 0.8f }, npc.Center);
        }

        /// <summary>
        /// The border closing in, three seconds of it: the world shaking harder and harder, booms under it,
        /// the glass of it groaning; light pouring off her cracks.
        /// </summary>
        private void Closing(NPC npc, int t, float p)
        {
            if (Main.dedServ)
                return;

            float near = Near(npc.Center);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake((3f + 9f * p) * near, 3);

            if (t % 30 == 0)
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.8f, Volume = 0.35f + 0.3f * p, MaxInstances = 3 }, npc.Center);

            if (t % 20 == 10)
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.7f + 0.3f * p, Volume = 0.3f + 0.2f * p, MaxInstances = 3 }, npc.Center);

            // Light off her into the closing dark.
            if (Main.rand.NextFloat() < 0.4f + 0.5f * p)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                PADParticleHandler.SpawnParticle(new SparkShard(npc.Center + way * Main.rand.NextFloat(20f, 80f), way * Main.rand.NextFloat(4f, 10f),
                    EmpressArt.Prism(Main.rand.NextFloat()), 30f, 22f, 90f, 20));
            }

            Lighting.AddLight(npc.Center, new Vector3(1f, 0.95f, 1f) * (1.5f + p));
        }

        /// <summary>She shakes as it closes: harder at the end.</summary>
        private Vector2 Tremble(float p)
        {
            float k = 1f + 3f * p * p;
            return new Vector2((float)Math.Sin(Main.GameUpdateCount * 2.3f), (float)Math.Cos(Main.GameUpdateCount * 3.1f)) * k;
        }

        /// <summary>
        /// The border thrown out, and gone: white over everything, a ring of light out of her, the sound of
        /// the arena breaking and her own death's.
        /// </summary>
        private void Thrown(NPC npc)
        {
            if (Main.dedServ)
                return;

            float near = Near(npc.Center);

            if (near > 0f)
            {
                Cinematic.Flash(Color.White, 0.9f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(16f * near, 36);
            }

            Cinematic.Roar(npc, 50, 0.9f, quiet: true, rim: EmpressArt.Sky);

            // Her own death's sound, which vanilla would have played at the end (End silences its copy), here.
            SoundEngine.PlaySound(SoundID.NPCDeath65, npc.Center);
            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.3f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.3f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.8f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(EmpressSounds.Shock with { Pitch = -0.4f, Volume = 1f, MaxInstances = 2 }, npc.Center);
            SoundEngine.PlaySound(EmpressSounds.Shards with { Pitch = -0.2f, Volume = 0.8f, MaxInstances = 2 }, npc.Center);

            // The world itself shaken by it: a great wave going out.
            EmpressWarp.Ripple(npc.Center, 34f, 110f, 22f, 34);

            SparkShard.Spray(npc.Center, Color.White, 40, 14f, 200f, 30, palette: EmpressArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, Color.White, 700f / 160f, 34));
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Pink, 1000f / 160f, 40));

            for (int i = 0; i < 90; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center, DustID.RainbowMk2, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(6f, 20f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.65f), Main.rand.NextFloat(1.4f, 2.4f));
                dust.noGravity = true;
                dust.fadeIn = 1.6f;
            }
        }

        /// <summary>How big she is, SHRINK going on: down and down to almost nothing as she becomes the butterfly.</summary>
        private float DeathSize()
        {
            if (scene != Scene.Death || beat != DeathShrink)
                return 1f;

            // Slowly at first, then faster and faster, into nothing.
            float p = MathHelper.Clamp(timer / (float)ButterflyAt, 0f, 1f);
            return MathHelper.Lerp(1f, SmallestShe, (float)Math.Pow(p, 1.5));
        }

        /// <summary>Getting smaller: chimes going up as she goes, light drawn into her.</summary>
        private void Shrinking(NPC npc, int t)
        {
            if (Main.dedServ || t >= ButterflyAt)
                return;

            if (t % 12 == 0)
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.2f + t * 0.018f, Volume = 0.5f, MaxInstances = 3 }, npc.Center);

            for (int i = 0; i < 2; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 from = npc.Center + way * Main.rand.NextFloat(80f, 200f) * DeathSize();
                Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, -way * Main.rand.NextFloat(3f, 6f), 0, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f), 1.1f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        /// <summary>
        /// She is butterflies: twenty of the game's own, real ones (the server makes them; EmpressButterflies flies them
        /// out every way, her light on them, and lets them go); a soft light where she was, a wave through the world, a
        /// rush of wings and a run of bells.
        /// </summary>
        private void Butterfly(NPC npc)
        {
            if (Authority)
                EmpressButterflies.Burst(npc.GetSource_Death(), npc.Center);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.9f, Volume = 0.8f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item25 with { Pitch = 0.3f, Volume = 0.8f }, npc.Center);
            SoundEngine.PlaySound(EmpressSounds.Flutter with { Volume = 0.9f, MaxInstances = 2 }, npc.Center);
            SoundEngine.PlaySound(EmpressSounds.Chime with { Pitch = 0.25f, Volume = 0.7f, MaxInstances = 2 }, npc.Center);

            EmpressWarp.Ripple(npc.Center, 14f, 50f, 10f, 30);

            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Lilac, 160f / 160f, 30));
            PADParticleHandler.SpawnParticle(new CrownGlint(npc.Center, 50f, 30, Color.White));

            for (int i = 0; i < 20; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center, DustID.RainbowMk2, Main.rand.NextVector2Circular(3f, 3f), 0, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f), 0.9f);
                dust.noGravity = true;
                dust.fadeIn = 1.1f;
            }
        }
    }
}
