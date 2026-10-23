using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the constellation (Legends act, her second half) --------------------------------------------------------
    //
    // By request: "Throws 6 targets randomly on the arena, it got indicator where one target connects to the other 5
    // before shoots lasers thru those guidelines. (1 sec indicator)".
    //
    //     THROW   40   she flings her free hand out: six marks of light fly out of it, one after another, to six places
    //                  about the arena (two of them near the player), and set there turning
    //     LINK    60   from each to each of the others a guideline of light, fifteen of them, brightening: one second
    //     FIRE    26   lasers down every one of them at once
    //     FADE    30   the marks burst, small, and are gone
    //
    // The lasers hurt (60 in a normal world, 40 in an expert one, doubled as the game doubles any hostile shot), and the
    // marks' bursts a little. Where the marks go is the same on every machine (the act's seed, the player's place as it
    // began, the arena).

    public partial class EmpressOfLight
    {
        private const int ConThrow = 0;
        private const int ConLink = 1;
        private const int ConFire = 2;
        private const int ConFade = 3;

        private const int ConThrowTicks = 40;
        private const int ConLinkTicks = 60;
        private const int ConFireTicks = 26;
        private const int ConFadeTicks = 30;

        /// <summary>Six marks; each thrown this long after the last, this long in flight.</summary>
        private const int ConMarks = 6;
        private const int ConEvery = 5;
        private const int ConFlight = 14;

        /// <summary>The lasers: how wide they hurt; how long they hurt from the fire's start.</summary>
        private const float ConWidth = 30f;
        private const int ConHurts = 16;

        /// <summary>The marks: kept this far in from the walls, this far apart; this many near the player, within this.</summary>
        private const float ConMargin = 220f;
        private const float ConApart = 300f;
        private const int ConNear = 2;
        private const float ConNearReach = 560f;
        private const float ConBurst = 70f;

        private const int ConNormal = 60;
        private const int ConExpert = 40;
        private const int ConBurstNormal = 40;
        private const int ConBurstExpert = 28;

        private readonly Vector2[] marks = new Vector2[ConMarks];
        private int marksFor = -1;

        private void PlanConstellation(NPC npc, Player target)
        {
        }

        /// <summary>The six marks' places: worked out once for this act's seed, the same everywhere.</summary>
        private Vector2[] Marks()
        {
            int key = actSeed ^ (int)actAt.X * 31 ^ (int)actAt.Y * 17;

            if (marksFor == key)
                return marks;

            marksFor = key;
            UnifiedRandomish dice = new UnifiedRandomish(actSeed);
            float radius = EmpressArena.Radius;

            for (int i = 0; i < ConMarks; i++)
            {
                Vector2 best = arenaCentre;
                float bestRoom = -1f;

                // A few tries each: the place furthest from the marks already set, inside, near the player for the first ones.
                for (int tries = 0; tries < 24; tries++)
                {
                    Vector2 at = i < ConNear
                        ? actAt + new Vector2(dice.Next() * 2f - 1f, dice.Next() * 2f - 1f) * ConNearReach
                        : arenaCentre + new Vector2(dice.Next() * 2f - 1f, dice.Next() * 2f - 1f) * radius;

                    if (!Decagon.Inside(at, arenaCentre, radius, ConMargin))
                        continue;

                    float room = float.MaxValue;

                    for (int j = 0; j < i; j++)
                        room = Math.Min(room, Vector2.Distance(at, marks[j]));

                    if (room >= ConApart)
                    {
                        best = at;
                        bestRoom = room;
                        break;
                    }

                    if (room > bestRoom)
                    {
                        best = at;
                        bestRoom = room;
                    }
                }

                marks[i] = best;
            }

            return marks;
        }

        /// <summary>Where mark i is this tick: flying out of her hand to its place over the throw, there after.</summary>
        private Vector2 MarkAt(NPC npc, int i)
        {
            Vector2[] places = Marks();

            if (beat != ConThrow)
                return places[i];

            float p = Ease((timer - i * ConEvery) / (float)ConFlight);

            if (p <= 0f)
                return Palm(npc, !BowLeft(npc), freeAngleNow);

            Vector2 from = Palm(npc, !BowLeft(npc), freeAngleNow);
            Vector2 to = places[i];
            Vector2 across = (to - from).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * (i % 2 == 0 ? 1f : -1f);
            return Vector2.Lerp(from, to, p) + across * 140f * (float)Math.Sin(p * MathHelper.Pi);
        }

        private void RunConstellation(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.88f;

            switch (beat)
            {
                case ConThrow:
                    {
                        // Her hand flung out at the arena, sweeping as the marks go.
                        float toward = (actAt - npc.Center).ToRotation();
                        FreeArmAt(toward + (BowLeft(npc) ? -1f : 1f) * MathHelper.Lerp(1.2f, -0.6f, Ease(t / (float)ConThrowTicks)));

                        for (int i = 0; i < ConMarks; i++)
                        {
                            if (Crossed(i * ConEvery))
                                Sound(EmpressSounds.WhooshUp, npc.Center, 0.2f + i * 0.08f, 0.45f, 3);

                            if (Crossed(i * ConEvery + ConFlight))
                                MarkSet(Marks()[i], i);
                        }

                        if (Authority && t >= ConThrowTicks)
                            Next(npc, ConLink);

                        break;
                    }

                case ConLink:
                    FreeArmFrame(4);

                    if (entered)
                    {
                        Sound(EmpressSounds.Charge, npc.Center, 0.3f, 0.6f, 2);
                        Hitbox(npc, EmpressHitbox.Shape.Constellation, arenaCentre, 0f, Hurt(npc, ConNormal, ConExpert), ConLinkTicks + ConFireTicks + 10);
                    }

                    // Each mark's lines come, one mark after another, with a ping each.
                    for (int i = 0; i < ConMarks; i++)
                    {
                        if (Crossed(i * 6))
                            Sound(EmpressSounds.Lock, Marks()[i], 0.1f * i, 0.55f, 4);
                    }

                    if (Authority && t >= ConLinkTicks)
                        Next(npc, ConFire);

                    break;

                case ConFire:
                    FreeArmFrame(3);

                    if (entered)
                        ConstellationFires(npc);

                    if (Authority && t >= ConFireTicks)
                        Next(npc, ConFade);

                    break;

                case ConFade:
                    FreeArmFrame(0);

                    if (entered)
                    {
                        for (int i = 0; i < ConMarks; i++)
                        {
                            Vector2 at = Marks()[i];
                            Hitbox(npc, EmpressHitbox.Shape.Circle, at, ConBurst, Hurt(npc, ConBurstNormal, ConBurstExpert), 4);

                            if (!Main.dedServ)
                            {
                                SparkShard.Spray(at, Color.White, 8, 7f, 50f, 16, palette: EmpressArt.Sparks);
                                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, EmpressArt.Prism(i / 6f), 160f / 160f, 16));
                            }
                        }

                        Sound(EmpressSounds.Shards, npc.Center, 0.3f, 0.6f, 2);
                    }

                    if (Authority && t >= ConFadeTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>A mark set in its place: a glint and a tinkle.</summary>
        private static void MarkSet(Vector2 at, int i)
        {
            if (Main.dedServ)
                return;

            Sound(EmpressSounds.Tingle, at, 0.1f * i, 0.4f);
            PADParticleHandler.SpawnParticle(new CrownGlint(at, 70f, 18, Color.White));
        }

        /// <summary>The lasers: the zap of them, many; the world shaken; light along every line.</summary>
        private void ConstellationFires(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2[] places = Marks();
            Vector2 middle = Vector2.Zero;

            foreach (Vector2 p in places)
                middle += p / ConMarks;

            Sound(EmpressSounds.Beam, middle, 0f, 1f, 3);
            Sound(EmpressSounds.Beam, middle, -0.4f, 0.8f, 3);
            Sound(EmpressSounds.Zap, middle, 0.3f, 0.6f, 3);
            Shake(middle, 7f, 16);

            for (int i = 0; i < ConMarks; i++)
            {
                for (int j = i + 1; j < ConMarks; j++)
                    EmpressWarp.Ripple(places[i], 10f, 30f, 6f, 16, places[j]);
            }
        }

        /// <summary>The lasers' hitbox asks: is this body on any of the fifteen, while they burn?</summary>
        internal bool ConstellationHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Constellation || beat != ConFire || timer > ConHurts)
                return false;

            Vector2[] places = Marks();

            for (int i = 0; i < ConMarks; i++)
            {
                for (int j = i + 1; j < ConMarks; j++)
                {
                    if (EmpressHitbox.OnLine(box, places[i], places[j], ConWidth))
                        return true;
                }
            }

            return false;
        }

        private void DrawConstellationOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2[] places = Marks();

            // The guidelines, then the lasers down them.
            if (beat == ConLink || beat == ConFire)
            {
                for (int i = 0; i < ConMarks; i++)
                {
                    for (int j = i + 1; j < ConMarks; j++)
                    {
                        Color colour = EmpressArt.Prism((i + j) / 10f + time / 300f);

                        if (beat == ConLink)
                        {
                            // Each mark's lines come with it, and brighten to the fire.
                            float shown = Ease((timer - i * 6) / 12f) * (0.35f + 0.65f * Ease(timer / (float)ConLinkTicks));
                            float flicker = 0.7f + 0.3f * (float)Math.Sin(time * 0.6f + i + j * 2f);

                            EmpressArt.DrawBeam(spriteBatch, places[i], places[j], 10f, EmpressArt.Additive(colour, 0.2f * shown * flicker));
                            EmpressArt.DrawBeam(spriteBatch, places[i], places[j], 1.6f, EmpressArt.Additive(Color.White, 0.65f * shown));
                        }
                        else
                        {
                            float p = timer / (float)ConFireTicks;
                            float fade = 1f - Ease(p);
                            float width = MathHelper.Lerp(ConWidth * 1.6f, ConWidth * 0.5f, Ease(p));

                            EmpressArt.DrawBeam(spriteBatch, places[i], places[j], width * 2.6f, EmpressArt.Additive(colour, 0.35f * fade));
                            EmpressArt.DrawBeam(spriteBatch, places[i], places[j], width, EmpressArt.Additive(colour, 0.85f * fade));
                            EmpressArt.DrawBeam(spriteBatch, places[i], places[j], width * 0.3f, EmpressArt.Additive(Color.White, 0.95f * fade));
                            // The air shimmering along them: along the marks' own chain only (each haze is a pass
                            // over the bent world, and fifteen at once cost more than they show).
                            if (j == i + 1)
                                EmpressWarp.Haze(places[i], places[j], 50f, 2f * fade, 90f, i + j);
                        }
                    }
                }
            }

            // The marks: a target of light each, turning.
            for (int i = 0; i < ConMarks; i++)
            {
                float shown = beat == ConThrow ? Ease((timer - i * ConEvery) / 4f) : beat == ConFade ? 1f - Ease(timer / 12f) : 1f;

                if (shown <= 0.003f)
                    continue;

                Vector2 at = MarkAt(npc, i);
                float turn = time * 0.03f * (i % 2 == 0 ? 1f : -1f);
                float size = beat == ConFire ? 1.3f : 1f;
                Color colour = EmpressArt.Prism(i / 6f + time / 400f);

                EmpressArt.DrawBloom(spriteBatch, at, 90f * size, EmpressArt.Additive(colour, 0.45f * shown));
                DrawRing(spriteBatch, at, 34f * size, 2.4f, EmpressArt.Additive(Color.White, 0.85f * shown));
                DrawRing(spriteBatch, at, 52f * size, 1.6f, EmpressArt.Additive(colour, 0.6f * shown));

                for (int k = 0; k < 4; k++)
                {
                    Vector2 way = (turn + k * MathHelper.PiOver2).ToRotationVector2();
                    EmpressArt.DrawBeam(spriteBatch, at + way * 40f * size, at + way * 66f * size, 2.4f, EmpressArt.Additive(Color.White, 0.8f * shown));
                }

                EmpressArt.DrawStar(spriteBatch, at, 46f * size, -turn * 2f, EmpressArt.Additive(Color.White, 0.9f * shown));
            }
        }
    }
}
