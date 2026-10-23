using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the mirage (Legends act, chosen from those offered: "Mirage") ---------------------------------------------
    //
    // As offered: "She refracts into three of herself; only the real one glints. The copies cast prismatic bolts,
    // the real one strikes."
    //
    //     SPLIT    40   a flash, and two of her peel off her, refracted; the three go to three places round the
    //                   player, a third of a turn apart, 440 px off
    //     FEINT   150   they keep their places round the player as the player moves; the copies cast her
    //                   prismatic bolts (vanilla's own) in turn; the real one does not, and a glint of light runs
    //                   over her crown every two thirds of a second
    //     STRIKE   44   all three come through where the player was: the copies are light, and go through; the real
    //                   one takes whoever she meets, as her dash does (EmpressOfLight.Grab)
    //     FADE     26   the copies break up into her colours and are gone
    //
    // Her copies are drawn exactly as she is: the glint, and the bolts, are how to tell.
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): a wave through the world as she splits, the copies peeling off her fringed red and blue as light split
    // by glass; the air about all three shimmering alike, a mirage's (EmpressWarp: no tell in it); a whispered rush
    // as they come through; the copies coming apart into lacewings of light, a flutter of wings.

    public partial class EmpressOfLight
    {
        private const int MirageSplit = 0;
        private const int MirageFeint = 1;
        private const int MirageStrike = 2;
        private const int MirageFade = 3;

        private const int SplitTicks = 40;
        private const int FeintTicks = 150;
        private const int StrikeTicks = 44;
        private const int FadeTicks = 26;

        /// <summary>How far round the player they stand; how fast they come through; a bolt every this many ticks.</summary>
        private const float MirageReach = 440f;
        private const float StrikePace = 26f;
        private const int BoltEvery = 24;

        /// <summary>Her glint: every this many ticks, for this many.</summary>
        private const int GlintEvery = 40;
        private const int GlintTicks = 12;

        private const int BoltNormal = 45;
        private const int BoltExpert = 30;

        /// <summary>Three places round the player (as offsets from them), and which is hers.</summary>
        private void PlanMirage(NPC npc, Player target)
        {
            float first = (npc.Center - target.Center).ToRotation();
            actCount = 3;

            for (int k = 0; k < 3; k++)
                actPoints[k] = (first + k * MathHelper.TwoPi / 3f).ToRotationVector2() * MirageReach;

            actN = Main.rand.Next(3);
            actAt = npc.Center;
        }

        /// <summary>Where the one at place k is this tick (k == actN is her own place).</summary>
        private Vector2 MirageAt(int k, Player who, NPC npc)
        {
            Vector2 place = who.Center + actPoints[k];

            switch (beat)
            {
                case MirageSplit:
                    return Vector2.Lerp(actAt, place, Ease(timer / (float)SplitTicks));

                case MirageFeint:
                    return place;

                case MirageStrike:
                case MirageFade:
                    {
                        // Each from its place at the strike, straight through where the player was then.
                        Vector2 from = actPoints[k] + strikeOrigin;
                        Vector2 way = (strikeOrigin - from).SafeNormalize(Vector2.UnitX);
                        float t = beat == MirageStrike ? timer : StrikeTicks + timer * 0.4f;
                        return from + way * StrikePace * t;
                    }
            }

            return place;
        }

        /// <summary>Where the player was when the strike began (every machine's: the server sends it in actAt).</summary>
        private Vector2 strikeOrigin;

        private void RunMirage(NPC npc, Player who, bool entered)
        {
            int t = timer;

            npc.damage = 0;

            switch (beat)
            {
                case MirageSplit:
                    if (entered && !Main.dedServ)
                    {
                        Sound(SoundID.Item161, npc.Center, 0.5f, 0.6f);
                        Sound(SoundID.Item29, npc.Center, 0.4f, 0.7f);
                        Sound(SoundID.Item25, npc.Center, 0.9f, 0.6f);
                        Sound(SoundID.Item9, npc.Center, -0.7f, 0.6f);
                        Sound(EmpressSounds.WhooshUp, npc.Center, 0.3f, 0.7f, 2);
                        Sound(EmpressSounds.Tingle, npc.Center, 0.5f, 0.6f);
                        PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, Color.White, 300f / 160f, 18));
                        EmpressWarp.Ripple(npc.Center, 18f, 50f, 12f, 26);
                    }

                    npc.Center = MirageAt(actN, who, npc);
                    npc.velocity = Vector2.Zero;

                    if (Authority && t >= SplitTicks)
                        Next(npc, MirageFeint);

                    break;

                case MirageFeint:
                    npc.Center = Vector2.Lerp(npc.Center, MirageAt(actN, who, npc), 0.2f);
                    npc.velocity = Vector2.Zero;

                    if (Authority && t % BoltEvery == 0)
                    {
                        // The copies in turn, never her.
                        int turn = t / BoltEvery % 2;
                        int k = (actN + 1 + turn) % 3;
                        Vector2 from = MirageAt(k, who, npc);
                        Vector2 way = (who.Center - from).SafeNormalize(Vector2.UnitY).RotatedByRandom(0.6) * 7f;

                        Projectile.NewProjectile(npc.GetSource_FromAI(), from, way, ProjectileID.HallowBossRainbowStreak, Hurt(npc, BoltNormal, BoltExpert), 0f,
                            Main.myPlayer, who.whoAmI, Main.rand.NextFloat());
                    }

                    // Each copy's bolt in its own voice, one high, one low.
                    if (!Main.dedServ && t % BoltEvery == 0)
                    {
                        int turn = t / BoltEvery % 2;
                        int k = (actN + 1 + turn) % 3;
                        Sound(SoundID.Item164, MirageAt(k, who, npc), turn == 0 ? 0.6f : -0.2f, 0.45f);
                    }

                    // Her glint heard: a high tink at her crown, hers alone.
                    if (Main.GameUpdateCount % GlintEvery == 0)
                        Sound(SoundID.Tink, npc.Center + new Vector2(0f, -66f), 0.9f, 0.35f);

                    // The air about the three wavering, from all of them alike.
                    if (t == 10 && !Main.dedServ)
                        Sound(EmpressSounds.Wobble, who.Center, 0.35f, 0.38f, 2);

                    if (Authority && t >= FeintTicks)
                    {
                        actAt = who.Center;
                        strikeOrigin = who.Center;
                        Next(npc, MirageStrike);
                    }

                    break;

                case MirageStrike:
                    if (entered)
                    {
                        strikeOrigin = actAt;

                        // All three scream as they come, three voices: hers in the middle, her copies above and below it.
                        if (!Main.dedServ)
                        {
                            Scream(npc, 0.35f, 1f);

                            for (int k = 0; k < 3; k++)
                            {
                                if (k != actN)
                                    Sound(SoundID.Item160, MirageAt(k, who, npc), k == (actN + 1) % 3 ? 0.9f : -0.3f, 0.6f);
                            }

                            Sound(SoundID.Item9, npc.Center, -0.4f, 0.7f);
                            Sound(EmpressSounds.WhooshDown, who.Center, 0.2f, 0.9f, 2);
                            Sound(EmpressSounds.Zap, who.Center, 0.5f, 0.45f, 3);
                        }
                    }

                    npc.Center = MirageAt(actN, who, npc);
                    npc.velocity = Vector2.Zero;

                    // Hers takes, as her dash does; the copies are light.
                    if (Authority && LegendsHalf(npc))
                    {
                        Rectangle her = npc.Hitbox;
                        her.Inflate(-8, -8);

                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player p = Main.player[i];

                            if (p.active && !p.dead && !p.immune && p.Hitbox.Intersects(her))
                            {
                                BeginGrab(npc, p);
                                return;
                            }
                        }
                    }

                    if (Authority && t >= StrikeTicks)
                        Next(npc, MirageFade);

                    break;

                case MirageFade:
                    npc.Center = MirageAt(actN, who, npc);

                    if (entered && !Main.dedServ)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            if (k == actN)
                                continue;

                            Vector2 at = MirageAt(k, who, npc);
                            Sound(SoundID.Item27, at, k == (actN + 1) % 3 ? 0.7f : 0.1f, 0.5f);
                            Sound(EmpressSounds.Flutter, at, k == (actN + 1) % 3 ? 0.3f : -0.1f, 0.6f, 2);

                            // The copy come apart into lacewings of light, flying off.
                            for (int b = 0; b < 5; b++)
                            {
                                float way = Main.rand.NextFloat(MathHelper.TwoPi);
                                PADParticleHandler.SpawnParticle(new LacewingFlight(at + Main.rand.NextVector2Circular(30f, 40f), way.ToRotationVector2() * Main.rand.NextFloat(4f, 8f),
                                    EmpressArt.Prism(Main.rand.NextFloat()), Main.rand.Next(80, 120), Main.rand.NextFloat(0.9f, 1.2f)));
                            }

                            for (int d = 0; d < 26; d++)
                            {
                                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(50f, 60f), DustID.RainbowMk2, Main.rand.NextVector2Circular(5f, 5f), 0,
                                    EmpressArt.Prism(Main.rand.NextFloat()), 1.4f);
                                dust.noGravity = true;
                                dust.fadeIn = 1.2f;
                            }
                        }
                    }

                    if (Authority && t >= FadeTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The three of her: the copies first, then her, each as the game draws her.</summary>
        private void DrawMirageHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Player who = actWho >= 0 && actWho < Main.maxPlayers ? Main.player[actWho] : Main.LocalPlayer;
            int arms = beat == MirageFeint ? 2 + (timer / 12 % 2) : beat == MirageStrike ? 6 : 0;

            for (int k = 0; k < 3; k++)
            {
                if (k == actN)
                    continue;

                float opacity = beat switch
                {
                    MirageSplit => Ease(timer / 14f),
                    MirageFade => 1f - Ease(timer / 8f),
                    _ => 1f,
                };

                if (opacity <= 0.01f)
                    continue;

                Vector2 at = MirageAt(k, who, npc);

                if (beat == MirageStrike)
                    Trail(spriteBatch, npc, at, screenPos);

                // Peeling off her, split as light through glass: red one way, blue the other, coming together.
                if (beat == MirageSplit)
                {
                    float apart = 10f * (1f - Ease(timer / (float)SplitTicks));
                    EmpressArt.DrawShape(spriteBatch, npc, at + new Vector2(-apart, 0f), screenPos, 1f, EmpressArt.Additive(new Color(255, 60, 90), 0.35f * opacity), arms, arms);
                    EmpressArt.DrawShape(spriteBatch, npc, at + new Vector2(apart, 0f), screenPos, 1f, EmpressArt.Additive(new Color(60, 170, 255), 0.35f * opacity), arms, arms);
                }

                DrawHerActing(spriteBatch, npc, screenPos, Arms.Frames(arms, arms), 0f, 0.3f, opacity, at);
            }

            if (beat == MirageStrike)
                Trail(spriteBatch, npc, npc.Center, screenPos);

            DrawHerActing(spriteBatch, npc, screenPos, Arms.Frames(arms, arms), beat == MirageSplit ? 0.6f * (1f - timer / (float)SplitTicks) : 0f, 0.3f);
        }

        /// <summary>Images of her left behind along the strike.</summary>
        private void Trail(SpriteBatch spriteBatch, NPC npc, Vector2 at, Vector2 screenPos)
        {
            Vector2 way = (strikeOrigin - at).SafeNormalize(Vector2.UnitX);

            for (int i = 1; i <= 4; i++)
                EmpressArt.DrawShape(spriteBatch, npc, at - way * StrikePace * i * 1.6f, screenPos, 1f, EmpressArt.Additive(EmpressArt.Prism(i * 0.2f), 0.18f * (1f - i / 5f)), 6, 6);
        }

        /// <summary>Her glint: a star over her crown, every so often, hers alone; and the air about all three shimmering alike.</summary>
        private void DrawMirageOver(NPC npc, SpriteBatch spriteBatch)
        {
            if (beat != MirageFeint && beat != MirageSplit)
                return;

            Player who = actWho >= 0 && actWho < Main.maxPlayers ? Main.player[actWho] : Main.LocalPlayer;
            float shimmer = beat == MirageSplit ? Ease(timer / (float)SplitTicks) : 1f;

            for (int k = 0; k < 3; k++)
            {
                Vector2 at = k == actN ? npc.Center : MirageAt(k, who, npc);
                EmpressWarp.Haze(at + new Vector2(0f, -150f), at + new Vector2(0f, 150f), 130f, 2.6f * shimmer, 70f, k * 2.1f);
            }

            int phase = (int)(Main.GameUpdateCount % GlintEvery);

            if (phase >= GlintTicks)
                return;

            float p = phase / (float)GlintTicks;
            float shine = (float)Math.Sin(p * MathHelper.Pi);
            Vector2 crown = npc.Center + new Vector2(0f, -66f);

            EmpressArt.DrawStar(spriteBatch, crown, 70f * shine + 10f, p * 1.5f, EmpressArt.Additive(Color.White, 0.95f * shine));
            EmpressArt.DrawBloom(spriteBatch, crown, 40f * shine, EmpressArt.Additive(EmpressArt.Gold, 0.6f * shine));
        }
    }
}
