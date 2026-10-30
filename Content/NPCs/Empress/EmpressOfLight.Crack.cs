using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the mirror crack (Legends act) -------------------------------------------------------------------------------
    //
    // By request: "She cracks the world like a mirror with cracks of glistening rainbow of certain radius and after
    // 2 seconds it shatters and deal damage (... shader like doctor strange ...) and then it fixes itself." In the clip
    // asked for, the world itself broken into flat shards of mirror, each holding a piece of it a little moved, edges
    // bright; then thrown apart.
    //
    //     RAISE    30   her hand up toward the player; a glint where each player stands (up to three of them)
    //     SPLIT    24   the world cracks out from each glint to 32 tiles (22 at first: "bigger radius", by request),
    //                   cracks running out like a struck mirror's (eleven straight out from the middle, unevenly;
    //                   across each wedge its own four cracks round, not meeting the next wedge's), glistening the
    //                   rainbow
    //     STRAIN   96   each shard of it shifts and tips, a little more, trembling, the world in it moved with it
    //                   and seen nearer or further, as in a mirror a little turned; light runs along the cracks,
    //                   the panes catch the light, stars where cracks cross; the ring at the edge brightens: two
    //                   seconds from the first crack
    //     BURST    30   it shatters: the shards thrown out, turning, and where they were, the world gone dark; whoever
    //                   is inside is hurt (70 in a normal world, 45 in an expert one, doubled as the game doubles
    //                   any hostile shot)
    //     MEND     50   the shards fly back to their places and the cracks close: the world whole again
    //
    // The shards are the world itself, as drawn this frame (the game's own capture of the screen, the one its screen
    // effects use), cut and moved (EmpressMirror). With lighting that has no capture (Retro, Trippy), the cracks and
    // the shards' edges are drawn without the world moved in them.
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): the glint a drop of glass the world swells in (EmpressWarp); a wave through the world as it cracks and
    // a bigger one as it bursts; flecks of glass glinting inside it while it strains, the glass heard crinkling under
    // it, wavering; sparkles running home along the cracks as it mends, a shimmer of bells with them.
    //
    // Its breaking, remade by request ("remake the glass smash effect of phase 1 Empress of Light like this? Check
    // calamity's code but make it ours and more beautiful", after the Devourer of Gods' rift in Calamity), in
    // Content/Graphics/PrismShatter: as it strains, a star of light at its middle, up and down longer than across,
    // growing, and a ring of her colours closing in on it; three times the glass goes in deeper (a crack heard, lower to
    // higher, the world shaken harder each time, a flash and sparks); as it bursts, a flash over the screen, a white-hot
    // heart cut as a crystal, the long cross split into the rainbow with a lens's ghosts along it, spikes, glass cracks
    // thrown out with their web, two rings each split as a prism splits light, sparks and glinting flecks of glass.
    // Then, by request ("the main ask was to remove these sectioned outlines js a pure clean glass break"): no lines of
    // her colours round each shard and no ring at the edge; the cracks thin and clean, along the burst's own (not the
    // shards' grid), the world whole under them until it bursts (the shards no longer shift and tip as it strains),
    // then thrown, clear, with no wash of colour.

    public partial class EmpressOfLight
    {
        private const int CrackRaise = 0;
        private const int CrackSplit = 1;
        private const int CrackStrain = 2;
        private const int CrackBurst = 3;
        private const int CrackMend = 4;

        private const int CrackRaiseTicks = 30;
        private const int CrackSplitTicks = 24;
        private const int StrainTicks = 96;
        private const int CrackBurstTicks = 30;
        private const int MendTicks = 50;

        /// <summary>How far the crack reaches from its middle: 32 tiles.</summary>
        internal const float CrackRadius = 512f;

        /// <summary>Its cracks: out from the middle, and those round across them (as shares of its reach, each wedge's a little off).</summary>
        internal const int CrackRays = 11;
        internal static readonly float[] CrackRings = { 0f, 0.24f, 0.46f, 0.66f, 0.84f, 1f };

        private const int ShatterNormal = 70;
        private const int ShatterExpert = 45;

        /// <summary>Where it cracks: about her target, and about up to two more players near her.</summary>
        private void PlanCrack(NPC npc, Player target)
        {
            actCount = 0;
            actPoints[actCount++] = target.Center;

            for (int i = 0; i < Main.maxPlayers && actCount < actPoints.Length; i++)
            {
                Player p = Main.player[i];

                if (p.active && !p.dead && i != target.whoAmI && Vector2.Distance(p.Center, npc.Center) < 2400f)
                    actPoints[actCount++] = p.Center;
            }
        }

        private float CrackHand()
        {
            float outward = actSide > 0 ? 0f : MathHelper.Pi;
            float up = Up + (actSide > 0 ? 0.6f : -0.6f);
            float rest = outward + (actSide > 0 ? 1.1f : -1.1f);

            return beat switch
            {
                CrackRaise => LerpAngle(rest, up, Ease(timer / 18f)),
                CrackSplit or CrackStrain => up,
                CrackBurst => LerpAngle(up, outward + (actSide > 0 ? -0.2f : 0.2f), Ease(timer / 8f)),
                _ => LerpAngle(outward, rest, Ease(timer / (float)MendTicks)),
            };
        }

        private void RunCrack(NPC npc, Player who, bool entered)
        {
            int t = timer;

            // Up and to her side of the crack, past its reach: the world breaks below her, not her with it.
            float side = npc.Center.X < actPoints[0].X ? -1f : 1f;
            FlyTo(npc, actPoints[0] + new Vector2(side * 470f, -470f), 9f, 0.06f);

            switch (beat)
            {
                case CrackRaise:
                    if (entered)
                    {
                        Sound(SoundID.Item29, npc.Center, 0.8f, 0.6f);
                        Sound(SoundID.Item25, actPoints[0], 0.4f, 0.5f);

                        // Built up to the first crack: the shimmer played backwards, cut off as it breaks.
                        Sound(EmpressSounds.Swell, actPoints[0], 0.55f, 0.65f, 2);
                        Sound(EmpressSounds.Tingle, actPoints[0], 0.6f, 0.5f);
                    }

                    if (Authority && t >= CrackRaiseTicks)
                        Next(npc, CrackSplit);

                    break;

                case CrackSplit:
                    if (entered && !Main.dedServ)
                    {
                        for (int k = 0; k < actCount; k++)
                        {
                            Sound(SoundID.Item27, actPoints[k], 0.6f, 0.8f);
                            Sound(SoundID.Item27, actPoints[k], -0.7f, 0.8f);
                            Sound(SoundID.Tink, actPoints[k], 0.3f, 0.7f);
                            Sound(SoundID.Shatter, actPoints[k], 0.9f, 0.35f);
                            Sound(EmpressSounds.Crinkle, actPoints[k], 0.3f, 0.6f, 3);

                            // The world shaken by it: a wave out from the glint as far as the cracks run.
                            EmpressWarp.Ripple(actPoints[k], 22f, 46f, 9f, 24);
                        }
                    }

                    if (Authority && t >= CrackSplitTicks)
                        Next(npc, CrackStrain);

                    break;

                case CrackStrain:
                    // The glass going in deeper, three times (PrismShatter.DrawPulse draws it): a crack heard, lower to
                    // higher, a little wave through the world, the screen shaken harder each time.
                    // (Each once: the furthest mark the clock has reached, played if it has not been; a client's clock
                    // can be set back or forward by a packet.)
                    if (entered)
                        crackPulsed = -1;

                    int deeper = t >= PulseEvery * 2 ? 2 : t >= PulseEvery ? 1 : 0;

                    if (deeper > crackPulsed)
                        crackPulsed = deeper;
                    else
                        deeper = -1;

                    if (deeper >= 0 && !Main.dedServ)
                    {
                        for (int k = 0; k < actCount; k++)
                        {
                            Sound(EmpressSounds.Crack, actPoints[k], -0.6f + 0.35f * deeper, 0.55f + 0.15f * deeper, 3);
                            Sound(SoundID.Item27, actPoints[k], -0.5f + 0.3f * deeper, 0.35f);
                            EmpressWarp.Ripple(actPoints[k], 14f, 28f, 5f + 2f * deeper, 18);

                            if (Vector2.Distance(Main.LocalPlayer.Center, actPoints[k]) < CrackRadius * 3f)
                                Main.LocalPlayer.PIS().BumpScreenShake(3f + 2.5f * deeper, 10);
                        }
                    }

                    if (t % 24 == 0)
                        Sound(SoundID.Item15, actPoints[0], -0.4f + t / 96f, 0.35f);

                    // The glass crinkling under it, closer and higher as it goes; the strain wavering under that.
                    if (t % 32 == 0)
                        Sound(EmpressSounds.Crinkle, actPoints[0], -0.2f + 0.6f * t / StrainTicks, 0.45f + 0.25f * t / StrainTicks, 3);

                    if (t == 6)
                        Sound(EmpressSounds.Wobble, actPoints[0], -0.45f, 0.4f, 2);

                    // The glass under strain: creaks high and quick, groans low and slow, closer together as it goes.
                    if (!Main.dedServ && t % Math.Max(3, 7 - t / 24) == 0)
                    {
                        Vector2 at = actPoints[Main.rand.Next(actCount)] + Main.rand.NextVector2Circular(CrackRadius, CrackRadius) * 0.8f;
                        Sound(SoundID.Tink, at, Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(0.18f, 0.32f), 6);
                    }

                    if (t % 20 == 10)
                        Sound(SoundID.Item27, actPoints[0], -0.9f, 0.28f);

                    if (!Main.dedServ && t % 9 == 0)
                    {
                        Vector2 at = actPoints[Main.rand.Next(actCount)] + Main.rand.NextVector2Circular(CrackRadius, CrackRadius) * 0.9f;
                        Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Vector2.Zero, 0, EmpressArt.Prism(Main.rand.NextFloat()), 0.9f);
                        dust.noGravity = true;
                    }

                    if (Authority && t >= StrainTicks)
                        Next(npc, CrackBurst);

                    break;

                case CrackBurst:
                    if (entered)
                    {
                        Scream(npc, 0.2f, 0.85f);

                        for (int k = 0; k < actCount; k++)
                        {
                            Hitbox(npc, EmpressHitbox.Shape.Circle, actPoints[k], CrackRadius + 12f, Hurt(npc, ShatterNormal, ShatterExpert), 6);

                            if (Main.dedServ)
                                continue;

                            Sound(SoundID.Shatter, actPoints[k], -0.2f, 1f);
                            Sound(SoundID.Shatter, actPoints[k], 0.7f, 0.6f);
                            Sound(SoundID.Item27, actPoints[k], -0.3f, 0.9f);
                            Sound(SoundID.Item14, actPoints[k], 0.4f, 0.45f);
                            Sound(SoundID.Item14, actPoints[k], -0.8f, 0.9f);
                            Sound(EmpressSounds.Shards, actPoints[k], 0f, 0.85f, 3);
                            Sound(EmpressSounds.Shock, actPoints[k], -0.1f, 0.7f, 3);
                            Sound(EmpressSounds.Crack, actPoints[k], 0.2f, 0.9f, 3);

                            // The burst through the world: a wave out past the crack's edge. (Its light, the flash, the
                            // cracks, the rings, the sparks and the glass, is drawn: PrismShatter.DrawBurst; the bloom and
                            // the rainbow dust it had before were caught in the shards and thrown with them.)
                            EmpressWarp.Ripple(actPoints[k], 30f, 80f, 18f, 32);

                            if (Vector2.Distance(Main.LocalPlayer.Center, actPoints[k]) < CrackRadius * 3f)
                                Main.LocalPlayer.PIS().BumpScreenShake(14f, 26);
                        }
                    }

                    // The world about it lit by its flash, going.
                    if (!Main.dedServ && t < 24)
                    {
                        float lit = 1f - t / 24f;

                        for (int k = 0; k < actCount; k++)
                            Lighting.AddLight(actPoints[k], 1.8f * lit, 1.6f * lit, 2f * lit);
                    }

                    if (Authority && t >= CrackBurstTicks)
                        Next(npc, CrackMend);

                    break;

                case CrackMend:
                    if (entered)
                    {
                        for (int k = 0; k < actCount; k++)
                        {
                            Sound(SoundID.Item29, actPoints[k], -0.4f, 0.7f);
                            Sound(EmpressSounds.WhooshUp, actPoints[k], -0.2f, 0.55f, 2);
                        }
                    }

                    // Each shard home with a sparkle.
                    if (!Main.dedServ && t % 10 == 4 && t < MendTicks - 10)
                        Sound(EmpressSounds.Tingle, actPoints[0], -0.2f + t / (float)MendTicks, 0.45f);

                    // The shards coming home, each lot higher than the last.
                    for (int step = 1; step <= 4; step++)
                    {
                        if (Crossed(step * 8))
                            Sound(SoundID.Tink, actPoints[0], -0.6f + step * 0.35f, 0.45f);
                    }

                    if (Crossed(MendTicks - 6) && !Main.dedServ)
                    {
                        for (int k = 0; k < actCount; k++)
                            Sound(SoundID.Item4, actPoints[k], 0.5f, 0.5f);
                    }

                    if (Authority && t >= MendTicks)
                        EndAct(npc);

                    break;
            }
        }

        // ---- the crack's state, for drawing ---------------------------------------------------------------------

        /// <summary>How far the cracks have run (0 to 1).</summary>
        internal float CrackGrown => beat == CrackRaise ? 0f : beat == CrackSplit ? Ease(timer / (float)CrackSplitTicks) : beat == CrackMend ? 1f - Ease((timer - MendTicks * 0.5f) / (MendTicks * 0.5f)) : 1f;

        /// <summary>How far the shards have shifted (0 to 1): over the strain, held through the burst, back to nothing over the mend.</summary>
        internal float CrackStrainNow => beat switch
        {
            CrackStrain => timer / (float)StrainTicks,
            CrackBurst => 1f,
            CrackMend => 1f - Ease(timer / (float)MendTicks),
            _ => 0f,
        };

        /// <summary>How far the shards have flown (0 to 1): out over the burst, back over the mend.</summary>
        internal float CrackFlight => beat == CrackBurst ? Ease(timer / (float)CrackBurstTicks) : beat == CrackMend ? 1f - Ease(timer / (MendTicks * 0.8f)) : 0f;

        internal bool Mirroring => scene == Scene.Act && act == Act.Crack && beat >= CrackSplit;

        internal int CrackCount => actCount;

        internal Vector2 CrackPoint(int k) => actPoints[k];

        internal int CrackSeed => actSeed;

        private void DrawCrackHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float hand = CrackHand();
            float hangs = actSide > 0 ? MathHelper.Pi - 1.1f : 1.1f;
            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(actSide > 0 ? hangs : hand, actSide > 0 ? hand : hangs), 0f, 0.35f);
        }

        /// <summary>
        /// Over the players: her hand's light and the glints (a drop of glass the world swells in); while it strains, flecks
        /// of glass glinting in it; as it mends, sparkles running home along its cracks; the cracks themselves when the world
        /// cannot be moved in them.
        /// </summary>
        private void DrawCrackOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 palm = Palm(npc, actSide < 0, CrackHand());
            float hand = beat == CrackRaise ? Ease(timer / (float)CrackRaiseTicks) : beat == CrackSplit || beat == CrackStrain ? 1f : 0f;

            if (hand > 0.01f)
            {
                EmpressArt.DrawBloom(spriteBatch, palm, 70f * hand, EmpressArt.Additive(EmpressArt.Prism(time / 70f), 0.5f * hand));
                EmpressArt.DrawStar(spriteBatch, palm, 60f * hand, time * 0.04f, EmpressArt.Additive(Color.White, 0.7f * hand));
                EmpressArt.DrawStar(spriteBatch, palm, 36f * hand, -time * 0.06f + MathHelper.PiOver4, EmpressArt.Additive(EmpressArt.Sky, 0.6f * hand));
            }

            if (beat == CrackRaise)
            {
                float glint = Ease(timer / (float)CrackRaiseTicks);

                for (int k = 0; k < actCount; k++)
                {
                    EmpressArt.DrawStar(spriteBatch, actPoints[k], 110f * glint, time * 0.03f, EmpressArt.Additive(Color.White, 0.9f * glint));
                    EmpressArt.DrawStar(spriteBatch, actPoints[k], 70f * glint, -time * 0.05f + MathHelper.PiOver4, EmpressArt.Additive(EmpressArt.Pink, 0.6f * glint));
                    EmpressArt.DrawBloom(spriteBatch, actPoints[k], 60f * glint, EmpressArt.Additive(EmpressArt.Sky, 0.6f * glint));

                    // The world swelling in the glint as in a drop of glass, and turning a little.
                    EmpressWarp.Lens(actPoints[k], 190f * glint + 20f, 0.22f * glint, 0.25f * glint);
                }

                return;
            }

            if (beat == CrackStrain)
                DrawCrackFlecks(spriteBatch, time, CrackStrainNow);

            if (!EmpressMirror.WillDraw)
            {
                for (int k = 0; k < actCount; k++)
                    DrawCrackLight(spriteBatch, k);
            }
        }

        /// <summary>Ticks between the glass going in deeper as it strains (three times).</summary>
        private const int PulseEvery = 32;

        /// <summary>The last time it went in deeper that has been heard (0 to 2; -1 none yet).</summary>
        private int crackPulsed = -1;

        /// <summary>
        /// The light of crack <paramref name="k"/> (PrismShatter), the one place it is drawn: over the shards when they
        /// are drawn (EmpressMirror), with the world when they are not. Its cracks, clean (by request: "remove these
        /// sectioned outlines js a pure clean glass break"), running out as it splits; as it strains, brightening, the
        /// web across them opening, the star growing at its middle, the ring closing on it, and each time it goes in
        /// deeper a flash and sparks, bigger each time; as it bursts, the burst along those cracks, its last light going
        /// on into the mend; as it mends, sparkles running home along the cracks, and the cracks back a moment as the
        /// shards land, closing from their ends. All from the act's seed and its own clock: the same on every machine.
        /// </summary>
        internal void DrawCrackLight(SpriteBatch spriteBatch, int k)
        {
            if (k < 0 || k >= actCount)
                return;

            Vector2 at = actPoints[k];
            int seed = actSeed * 977 + k * 131;
            const PrismShatter.Look look = PrismShatter.Look.Empress;

            switch (beat)
            {
                case CrackSplit:
                    PrismShatter.DrawCrack(spriteBatch, at, seed, CrackRadius, CrackGrown, 0f, timer, look);
                    break;

                case CrackStrain:
                    float strain = timer / (float)StrainTicks;
                    float clock = CrackSplitTicks + timer;

                    PrismShatter.DrawCrack(spriteBatch, at, seed, CrackRadius, 1f, strain, clock, look);
                    PrismShatter.DrawCharge(spriteBatch, at, strain, clock, CrackRadius, look);

                    for (int deeper = 0; deeper * PulseEvery < StrainTicks; deeper++)
                        PrismShatter.DrawPulse(spriteBatch, at, seed + deeper, timer - deeper * PulseEvery, CrackRadius * (0.3f + 0.12f * deeper), look, 0.8f + 0.2f * deeper);

                    break;

                case CrackBurst:
                    PrismShatter.DrawBurst(spriteBatch, at, seed, timer, CrackRadius, look, 1f, cracked: true);
                    break;

                case CrackMend:
                    float mend = timer / (float)MendTicks;
                    float mendClock = CrackSplitTicks + StrainTicks + CrackBurstTicks + timer;

                    PrismShatter.DrawBurst(spriteBatch, at, seed, CrackBurstTicks + timer, CrackRadius, look, 1f, cracked: true);
                    PrismShatter.DrawCrack(spriteBatch, at, seed, CrackRadius, CrackGrown, 0.25f * (1f - mend), mendClock, look, MathHelper.Clamp((mend - 0.35f) * 4f, 0f, 1f));
                    PrismShatter.DrawHomecoming(spriteBatch, at, seed, CrackRadius, mend, mendClock, look);
                    break;
            }
        }

        /// <summary>Flecks of glass in the strained world, each glinting now and then in one of her colours, more of them as it strains.</summary>
        private void DrawCrackFlecks(SpriteBatch spriteBatch, float time, float strain)
        {
            UnifiedRandomish dice = new UnifiedRandomish(actSeed + 17);
            int flecks = (int)(18 + 26 * strain);

            for (int k = 0; k < actCount; k++)
            {
                for (int i = 0; i < flecks; i++)
                {
                    float a = dice.Next() * MathHelper.TwoPi;
                    float r = (float)Math.Sqrt(dice.Next()) * CrackRadius * 0.95f;
                    float rate = 0.05f + 0.07f * dice.Next();
                    float when = dice.Next() * MathHelper.TwoPi;
                    float glint = (float)Math.Sin(time * rate + when);

                    if (glint < 0.55f)
                        continue;

                    float shine = (glint - 0.55f) / 0.45f;
                    Vector2 at = actPoints[k] + a.ToRotationVector2() * r + new Vector2(0f, -6f * (float)Math.Sin(time * 0.02f + i));
                    Color colour = EmpressArt.Prism(i * 0.13f + time / 300f);

                    EmpressArt.DrawStar(spriteBatch, at, (14f + 10f * dice.Next()) * shine, time * 0.03f + i, EmpressArt.Additive(Color.White, 0.85f * shine));
                    EmpressArt.DrawBloom(spriteBatch, at, 12f * shine, EmpressArt.Additive(colour, 0.6f * shine));
                }
            }
        }
    }

    /// <summary>
    /// A crack's net, as struck glass breaks: straight cracks out from the middle, unevenly spaced, and across each
    /// wedge between two of them its own cracks round, not meeting its neighbours' (a struck mirror's, not a web's);
    /// the same on every machine for the same seed. The cells between are the shards.
    /// </summary>
    internal sealed class CrackNet
    {
        private static readonly Dictionary<long, CrackNet> made = new();

        /// <summary>Each crack out from the middle: its way, in order round (radians).</summary>
        public readonly float[] Ray = new float[EmpressOfLight.CrackRays];

        /// <summary>Across wedge i (from ray i round to the next), how far out each of its cracks round is: none at the middle, the reach at the rim.</summary>
        public readonly float[,] Cut = new float[EmpressOfLight.CrackRays, EmpressOfLight.CrackRings.Length];

        /// <summary>Each shard's own way of moving: its tip, its spin, how far it flies, and how much nearer or further the world in it looks.</summary>
        public readonly float[,] Tip = new float[EmpressOfLight.CrackRays, EmpressOfLight.CrackRings.Length];
        public readonly float[,] Spin = new float[EmpressOfLight.CrackRays, EmpressOfLight.CrackRings.Length];
        public readonly float[,] Throw = new float[EmpressOfLight.CrackRays, EmpressOfLight.CrackRings.Length];
        public readonly float[,] Zoom = new float[EmpressOfLight.CrackRays, EmpressOfLight.CrackRings.Length];

        public static CrackNet Of(int seed, int which)
        {
            long key = (long)seed * 8 + which;

            if (made.TryGetValue(key, out CrackNet net))
                return net;

            if (made.Count > 32)
                made.Clear();

            net = new CrackNet(seed * 31 + which * 7919);
            made[key] = net;
            return net;
        }

        private CrackNet(int seed)
        {
            var dice = new EmpressOfLight.UnifiedRandomish(seed);
            int rays = EmpressOfLight.CrackRays;
            float[] rings = EmpressOfLight.CrackRings;
            float turn = dice.Next() * MathHelper.TwoPi;

            // Each ray up to three tenths of a wedge off even: never crossing its neighbours.
            for (int i = 0; i < rays; i++)
                Ray[i] = turn + (i + (dice.Next() - 0.5f) * 0.6f) * MathHelper.TwoPi / rays;

            for (int i = 0; i < rays; i++)
            {
                for (int j = 0; j < rings.Length; j++)
                {
                    float r = rings[j] * EmpressOfLight.CrackRadius;

                    // Each wedge's cracks round a little in or out from their ring (at most a third of the way to the
                    // next ring either side): in order still, and not where the next wedge's are.
                    if (j > 0 && j < rings.Length - 1)
                    {
                        float room = Math.Min(rings[j] - rings[j - 1], rings[j + 1] - rings[j]) * EmpressOfLight.CrackRadius;
                        r += (dice.Next() - 0.5f) * 0.7f * room;
                    }

                    Cut[i, j] = r;
                    Tip[i, j] = (dice.Next() - 0.5f) * 0.09f;
                    Spin[i, j] = (dice.Next() - 0.5f) * 5f;
                    Throw[i, j] = 0.6f + 0.8f * dice.Next();
                    Zoom[i, j] = -0.06f + 0.24f * dice.Next();
                }
            }
        }

        /// <summary>
        /// The shard in wedge i between its cracks round j and j+1: its corners from the middle (three for the inmost),
        /// in order round it: out along ray i, across, back in along the next ray, and across (edges 0 and 2 run out
        /// from the middle, 1 and 3 across).
        /// </summary>
        public int Cell(int i, int j, Span<Vector2> corners)
        {
            int rays = EmpressOfLight.CrackRays;
            Vector2 a = Ray[i].ToRotationVector2();
            Vector2 b = (i + 1 < rays ? Ray[i + 1] : Ray[0] + MathHelper.TwoPi).ToRotationVector2();
            float inner = Cut[i, j];
            float outer = Cut[i, j + 1];

            if (j == 0)
            {
                corners[0] = Vector2.Zero;
                corners[1] = a * outer;
                corners[2] = b * outer;
                return 3;
            }

            corners[0] = a * inner;
            corners[1] = a * outer;
            corners[2] = b * outer;
            corners[3] = b * inner;
            return 4;
        }

        /// <summary>
        /// Where a shard is moved to this frame, about its own middle: shifted, tipped and trembling over the strain, the
        /// world in it looked at nearer or further; thrown out and turning over the burst, back over the mend.
        /// </summary>
        public void Moved(int i, int j, Vector2 middle, float strain, float flight, float time, out Vector2 shift, out float turn, out float zoom)
        {
            Vector2 out_ = middle.SafeNormalize(Vector2.UnitY);
            float far = middle.Length() / EmpressOfLight.CrackRadius;
            float tremble = (float)Math.Sin(time * 0.21f + i * 1.7f + j * 2.3f) * strain * strain;

            shift = out_ * (6f + 16f * far) * strain + out_.RotatedBy(MathHelper.PiOver2) * Tip[i, j] * 140f * strain;
            turn = Tip[i, j] * (2.4f * strain + 0.4f * tremble);
            zoom = 1f + Zoom[i, j] * strain;

            if (flight > 0f)
            {
                shift += out_ * (40f + 420f * (float)Math.Pow(flight, 1.3)) * Throw[i, j] * flight;
                turn += Spin[i, j] * flight;
            }
        }
    }
}
