using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the prism (Legends act) ------------------------------------------------------------------------------------
    //
    // By request: "she throws a glass prism (about 6 tiles tall) onto the center of the arena and flies onto the
    // opposite side of the player (basically use the diameter to seperate the two sides of the decagon), and shoots a
    // concentrated white ray of light (prob take 2 seconds to concentrate), and on the other side out shoots 7 rays of
    // rainbow colors. And she moves slowly either counter clockwise or clockwise and the 7 rays moves as well."
    //
    //     RAISE    24   a prism of glass forms in her raised hand
    //     THROW    36   she throws it, turning end over end, to the arena's middle, where it stands upright
    //     FLY      50   she flies across to the far side of the middle from the player (1000 px from it)
    //     CHARGE  120   two seconds: her hands at the prism, light running into them, a thin white ray flickering
    //                   on between her and it, thickening, the prism lighting up
    //     FIRE    450   the white ray at full, and out of the prism's far side seven rays: red, orange, yellow,
    //                   green, blue, indigo, violet, fanned 96 degrees from first to last (by request: "at least
    //                   90"), out past the arena's edge; she goes round the middle one way or the other, faster and
    //                   faster (by request: her first turn at the start, 1.7 times it half way, 3 times it at the end;
    //                   189 degrees round in the seven and a half seconds), and the rays go round with her, their hum
    //                   quickening and climbing with her
    //     BREAK    40   the ray stops; the prism cracks and bursts into light
    //
    // The white ray and the seven hurt (55 in a normal world, 36 in an expert one, doubled as the game doubles any
    // hostile shot).
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound effect"):
    // the world seen through the prism's glass, swollen in it (EmpressWarp); light glinting round its edges; two strands
    // of her colours twisting round the white ray as it gathers and fires; sparks running out along the seven and the
    // air over them shimmering; seven bells as they come out; glass, not stars, when it breaks, and a wave through the
    // world.

    public partial class EmpressOfLight
    {
        private const int PrismRaise = 0;
        private const int PrismThrow = 1;
        private const int PrismFly = 2;
        private const int PrismCharge = 3;
        private const int PrismFire = 4;
        private const int PrismBreak = 5;

        private const int PrismRaiseTicks = 24;
        private const int ThrowTicks = 36;
        private const int PrismFlyTicks = 50;
        private const int ChargeTicks = 120;
        private const int FireTicks = 450;
        private const int PrismBreakTicks = 40;

        /// <summary>The prism: six tiles tall. Her circle round the middle; her turn round it as the fire starts (radians a tick).</summary>
        private const float PrismHeight = 96f;
        private const float OrbitRadius = 1000f;
        private const float OrbitSpeed = 1.22f / 300f;

        /// <summary>The seven: how far apart (16 degrees, so 96 from first to last), how long, how wide; the white ray's width.</summary>
        private const float RayApart = 0.2793f;
        private const float RayLength = 3000f;
        private const float RayWidth = 24f;
        private const float WhiteWidth = 30f;

        /// <summary>The rays' hum: a note each time she has gone this far round her circle (a note every 18 ticks at first).</summary>
        private const float HumEvery = 0.075f;

        private const int RayNormal = 55;
        private const int RayExpert = 36;

        private static readonly Color[] Spectrum =
        {
            new Color(255, 60, 70), new Color(255, 150, 40), new Color(255, 236, 80), new Color(90, 255, 120),
            new Color(70, 160, 255), new Color(110, 90, 255), new Color(205, 100, 255),
        };

        /// <summary>The prism at the arena's middle; she goes to the far side of it from the player, and round it one way.</summary>
        private void PlanPrism(NPC npc, Player target)
        {
            actAt = arenaCentre;

            float player = (target.Center - arenaCentre).ToRotation();
            actTurn = player + MathHelper.Pi;
            actSide = Main.rand.NextBool() ? 1 : -1;
            actPoints[0] = npc.Center;
        }

        /// <summary>Her angle round the middle this tick: her place, then going round as the rays fire.</summary>
        private float OrbitAngle()
        {
            float round = beat == PrismFire ? OrbitRound(timer / (float)FireTicks) : beat == PrismBreak ? OrbitRound(1f) : 0f;
            return actTurn + actSide * round;
        }

        /// <summary>
        /// How far round she has gone at this share of the fire (0 to 1): her turn speeding up from her first (at the
        /// start) through 1.7 times it (half way) to 3 times it (at the end), as 1 + 0.8p + 1.2p², summed.
        /// </summary>
        private static float OrbitRound(float p)
        {
            p = MathHelper.Clamp(p, 0f, 1f);
            return OrbitSpeed * FireTicks * (p + 0.4f * p * p + 0.4f * p * p * p);
        }

        private Vector2 OrbitPlace() => actAt + OrbitAngle().ToRotationVector2() * OrbitRadius;

        /// <summary>Where the prism is this tick: in her hand, in the air, at the middle.</summary>
        private Vector2 PrismAt(NPC npc, out float turn)
        {
            turn = 0f;

            switch (beat)
            {
                case PrismRaise:
                    return Palm(npc, actSide < 0, PrismArm()) + new Vector2(0f, -30f);

                case PrismThrow:
                    {
                        float p = Ease(timer / (float)ThrowTicks);
                        Vector2 from = actPoints[1];
                        Vector2 at = Vector2.Lerp(from, actAt, p) + new Vector2(0f, -260f * 4f * p * (1f - p));
                        turn = p * MathHelper.TwoPi * 2f * actSide;
                        return at;
                    }

                default:
                    return actAt + new Vector2(0f, 6f * (float)Math.Sin(Main.GameUpdateCount * 0.05f));
            }
        }

        private float PrismArm()
        {
            float outward = actSide > 0 ? 0f : MathHelper.Pi;
            float up = Up + (actSide > 0 ? 0.35f : -0.35f);

            return beat switch
            {
                PrismRaise => LerpAngle(outward + (actSide > 0 ? 1.1f : -1.1f), up, Ease(timer / 16f)),
                PrismThrow => LerpAngle(up, outward + (actSide > 0 ? 0.3f : -0.3f), Ease(timer / 10f)),
                _ => outward + (actSide > 0 ? 1.1f : -1.1f),
            };
        }

        private void RunPrism(NPC npc, Player who, bool entered)
        {
            int t = timer;

            switch (beat)
            {
                case PrismRaise:
                    npc.velocity *= 0.9f;

                    if (entered)
                    {
                        Sound(SoundID.Item29, npc.Center, 0.6f, 0.6f);
                        Sound(SoundID.Item4, npc.Center, 0.8f, 0.5f);
                        Sound(SoundID.Item29, npc.Center, -0.5f, 0.4f);
                        Sound(EmpressSounds.Tingle, npc.Center, 0.3f, 0.6f);
                    }

                    if (Authority && t >= PrismRaiseTicks)
                    {
                        actPoints[1] = PrismAt(npc, out _);
                        Next(npc, PrismThrow);
                    }

                    break;

                case PrismThrow:
                    npc.velocity *= 0.9f;

                    if (entered)
                    {
                        Sound(SoundID.Item1, npc.Center, -0.3f, 0.8f);
                        Sound(SoundID.Item9, npc.Center, -0.5f, 0.6f);
                        Sound(EmpressSounds.WhooshUp, npc.Center, 0.1f, 0.7f, 2);
                    }

                    if (Crossed(ThrowTicks - 1) && !Main.dedServ)
                    {
                        Sound(SoundID.Item27, actAt, 0.5f, 0.7f);
                        Sound(SoundID.Item4, actAt, 0.3f, 0.5f);
                        Sound(SoundID.Item29, actAt, -0.6f, 0.7f);
                        Sound(EmpressSounds.Tingle, actAt, 0.1f, 0.7f);
                        Sound(EmpressSounds.Shock, actAt, 0.6f, 0.35f, 2);
                        PADParticleHandler.SpawnParticle(new BloomGeneric(actAt, Vector2.Zero, Color.White, 200f / 160f, 16));
                        EmpressWarp.Ripple(actAt, 16f, 44f, 10f, 26);
                    }

                    if (Authority && t >= ThrowTicks)
                        Next(npc, PrismFly);

                    break;

                case PrismFly:
                    FlyTo(npc, OrbitPlace(), 30f, 0.12f);

                    if (entered)
                    {
                        Sound(SoundID.Item9, npc.Center, -0.7f, 0.6f);
                        Sound(EmpressSounds.WhooshDown, npc.Center, -0.3f, 0.6f, 2);
                    }

                    if (Authority && t >= PrismFlyTicks)
                        Next(npc, PrismCharge);

                    break;

                case PrismCharge:
                    npc.Center = Vector2.Lerp(npc.Center, OrbitPlace(), 0.12f);
                    npc.velocity = Vector2.Zero;

                    if (entered)
                    {
                        Sound(SoundID.Item162, npc.Center, -0.4f, 0.8f);
                        Sound(EmpressSounds.Wobble, actAt, 0.2f, 0.4f, 2);
                    }

                    // The light gathering: a hum climbing from low to high over the two seconds; and the last second
                    // and a half the shimmer played backwards, cut off as the seven come out.
                    if (t % 15 == 0)
                        Sound(SoundID.Item15, npc.Center, MathHelper.Lerp(-0.9f, 0.9f, t / (float)ChargeTicks), MathHelper.Lerp(0.35f, 0.65f, t / (float)ChargeTicks));

                    if (Crossed(ChargeTicks - SwellTicks(-0.1f)))
                        Sound(EmpressSounds.Swell, actAt, -0.1f, 0.75f, 2);

                    if (!Main.dedServ)
                        Charging(npc, t);

                    if (Authority && t >= ChargeTicks)
                        Next(npc, PrismFire);

                    break;

                case PrismFire:
                    npc.Center = OrbitPlace();
                    npc.velocity = Vector2.Zero;

                    if (entered)
                    {
                        Hitbox(npc, EmpressHitbox.Shape.Rays, actAt, 0f, Hurt(npc, RayNormal, RayExpert), FireTicks + 5);

                        if (!Main.dedServ)
                        {
                            Sound(SoundID.Item163, npc.Center, 0.3f, 1f);
                            Sound(SoundID.Item12, actAt, -0.6f, 0.8f);
                            Sound(SoundID.Item14, actAt, -0.8f, 0.8f);
                            Sound(SoundID.Item67, actAt, 0.2f, 0.7f);

                            // Seven colours, seven bells; her glass laser; the world shaken by it.
                            Sound(EmpressSounds.Chime, actAt, 0f, 0.8f, 2);
                            Sound(EmpressSounds.Zap, actAt, -0.4f, 0.8f, 3);
                            Sound(EmpressSounds.Shock, actAt, 0f, 0.6f, 3);
                            Scream(npc, -0.35f, 0.8f);
                            PADParticleHandler.SpawnParticle(new BloomGeneric(actAt, Vector2.Zero, Color.White, 360f / 160f, 20));
                            EmpressWarp.Ripple(actAt, 24f, 70f, 16f, 34);
                        }
                    }

                    // The rays humming, a note each time she has gone a little further round (so quicker as she
                    // speeds up), wavering high and low and climbing as she goes; a shimmer now and then.
                    float share = t / (float)FireTicks;

                    if (t > 0 && (int)(OrbitRound(share) / HumEvery) != (int)(OrbitRound((t - 1) / (float)FireTicks) / HumEvery))
                        Sound(SoundID.Item15, actAt, -0.3f + 0.8f * share + 0.3f * (float)Math.Sin(t * 0.07f), 0.45f + 0.2f * share);

                    if (t % 45 == 20)
                        Sound(SoundID.Item29, actAt, Main.rand.NextFloat(0.5f, 0.9f), 0.3f);

                    // The air over the seven wavering, climbing with her.
                    if (t % 120 == 60)
                        Sound(EmpressSounds.Wobble, actAt, -0.2f + 0.6f * share, 0.35f, 2);

                    if (t % 36 == 18)
                        Sound(EmpressSounds.Tingle, actAt, 0.2f + 0.5f * share, 0.3f);

                    if (Authority && t >= FireTicks)
                        Next(npc, PrismBreak);

                    break;

                case PrismBreak:
                    npc.velocity *= 0.9f;

                    if (entered && !Main.dedServ)
                    {
                        Sound(SoundID.Shatter, actAt, 0.2f, 0.9f);
                        Sound(SoundID.Shatter, actAt, -0.6f, 0.7f);
                        Sound(SoundID.Item27, actAt, -0.1f, 0.8f);
                        Sound(SoundID.Item27, actAt, 0.9f, 0.5f);
                        Sound(EmpressSounds.Shards, actAt, 0.15f, 0.9f, 3);
                        Sound(EmpressSounds.Shock, actAt, 0.2f, 0.5f, 3);
                        PADParticleHandler.SpawnParticle(new BloomGeneric(actAt, Vector2.Zero, Color.White, 300f / 160f, 22));
                        EmpressWarp.Ripple(actAt, 20f, 60f, 14f, 30);

                        for (int i = 0; i < 40; i++)
                        {
                            Dust dust = Dust.NewDustPerfect(actAt + Main.rand.NextVector2Circular(30f, 48f), DustID.RainbowMk2, Main.rand.NextVector2Circular(7f, 7f), 0,
                                EmpressArt.Prism(Main.rand.NextFloat()), 1.5f);
                            dust.noGravity = true;
                            dust.fadeIn = 1.3f;
                        }
                    }

                    if (Authority && t >= PrismBreakTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>Light running into her hands as the ray gathers.</summary>
        private void Charging(NPC npc, int t)
        {
            if (t % 2 != 0)
                return;

            Vector2 hands = Hands(npc);
            Vector2 from = hands + Main.rand.NextVector2CircularEdge(160f, 160f);
            Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, (hands - from) / 16f, 0, Color.White, 1.2f);
            dust.noGravity = true;
            dust.fadeIn = 1f;
        }

        /// <summary>Between her two hands, held out at the prism.</summary>
        private Vector2 Hands(NPC npc)
        {
            float aim = (actAt - npc.Center).ToRotation();
            return Vector2.Lerp(Palm(npc, true, aim + 0.12f), Palm(npc, false, aim - 0.12f), 0.5f);
        }

        /// <summary>The way the light goes: from her, through the prism.</summary>
        private float LightWay(NPC npc) => (actAt - Hands(npc)).ToRotation();

        /// <summary>The rays' hitbox asks: is this body on the white ray or on any of the seven?</summary>
        internal bool PrismHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Prism || beat != PrismFire)
                return false;

            NPC npc = NpcOfThis();

            if (npc == null)
                return false;

            Vector2 hands = Hands(npc);

            if (EmpressHitbox.OnLine(box, hands, actAt, WhiteWidth))
                return true;

            float way = LightWay(npc);

            for (int k = 0; k < Spectrum.Length; k++)
            {
                float a = way + (k - 3) * RayApart;
                Vector2 from = actAt + a.ToRotationVector2() * 30f;

                if (EmpressHitbox.OnLine(box, from, from + a.ToRotationVector2() * RayLength, RayWidth))
                    return true;
            }

            return false;
        }

        // ---- drawing --------------------------------------------------------------------------------------------

        private void DrawPrismHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (beat == PrismRaise || beat == PrismThrow)
            {
                float arm = PrismArm();
                float hangs = actSide > 0 ? MathHelper.Pi - 1.1f : 1.1f;
                DrawHerActing(spriteBatch, npc, screenPos, Arms.At(actSide > 0 ? hangs : arm, actSide > 0 ? arm : hangs), 0f, 0.35f);
                return;
            }

            if (beat == PrismFly)
            {
                DrawHerActing(spriteBatch, npc, screenPos, Arms.Frames(5, 5), 0f, 0.3f);
                return;
            }

            // Both hands at the prism.
            float aim = (actAt - npc.Center).ToRotation();
            float charge = beat == PrismCharge ? Ease(timer / (float)ChargeTicks) : beat == PrismFire ? 1f : 1f - Ease(timer / (float)PrismBreakTicks);
            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(aim + 0.12f, aim - 0.12f), 0.25f * charge, 0.4f + 0.4f * charge);
        }

        private void DrawPrismOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 prism = PrismAt(npc, out float turn);
            bool whole = beat != PrismBreak;
            float lit = beat == PrismCharge ? Ease(timer / (float)ChargeTicks) : beat == PrismFire ? 1f : 0f;

            // The white ray: flickering on through the charge, full through the fire; two strands of her colours twisting
            // round it, the air along it shimmering.
            if (beat == PrismCharge || beat == PrismFire)
            {
                Vector2 hands = Hands(npc);
                float on = beat == PrismFire ? 1f : lit * (0.5f + 0.5f * (float)Math.Sin(time * 0.6f) * (1f - lit));
                float width = beat == PrismFire ? WhiteWidth : MathHelper.Lerp(2f, WhiteWidth * 0.6f, lit);

                EmpressArt.DrawLine(spriteBatch, hands, prism, width * 3f, EmpressArt.Additive(EmpressArt.Sky, 0.25f * on));
                EmpressArt.DrawLine(spriteBatch, hands, prism, width, EmpressArt.Additive(Color.White, 0.9f * on));
                EmpressArt.DrawBloom(spriteBatch, hands, 70f + 40f * lit, EmpressArt.Additive(Color.White, 0.7f * on));

                DrawHelix(spriteBatch, hands, prism, width * 0.9f + 10f, time, Math.Max(lit, 0.35f) * on);
                EmpressWarp.Haze(hands, prism, 70f, 2.5f * lit, 110f, 0.7f);
            }

            // The seven, out of the far side.
            if (beat == PrismFire || beat == PrismBreak)
            {
                float fade = beat == PrismFire ? Math.Min(1f, timer / 6f) : 1f - Ease(timer / 12f);
                float way = LightWay(npc);

                for (int k = 0; k < Spectrum.Length; k++)
                {
                    float a = way + (k - 3) * RayApart;
                    Vector2 from = actAt + a.ToRotationVector2() * 30f;
                    Vector2 to = from + a.ToRotationVector2() * RayLength;
                    float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 0.3f + k);

                    EmpressArt.DrawLine(spriteBatch, from, to, RayWidth * 3.2f, EmpressArt.Additive(Spectrum[k], 0.2f * fade * pulse));
                    EmpressArt.DrawLine(spriteBatch, from, to, RayWidth, EmpressArt.Additive(Spectrum[k], 0.85f * fade));
                    EmpressArt.DrawLine(spriteBatch, from, to, RayWidth * 0.3f, EmpressArt.Additive(Color.White, 0.55f * fade));

                    // Sparks running out along it, and the air over its first stretch shimmering.
                    Vector2 outward = a.ToRotationVector2();

                    for (int i = 0; i < 9; i++)
                    {
                        float along = (time * 16f + i * 170f + k * 61f) % 1530f;
                        float twinkle = 0.6f + 0.4f * (float)Math.Sin(time * 0.3f + i + k);
                        Vector2 spark = from + outward * along;

                        EmpressArt.DrawStar(spriteBatch, spark, 30f * twinkle, time * 0.05f + i, EmpressArt.Additive(Color.White, 0.75f * fade * twinkle));
                        EmpressArt.DrawBloom(spriteBatch, spark, 24f, EmpressArt.Additive(Spectrum[k], 0.6f * fade));
                    }

                    EmpressWarp.Haze(from, from + outward * 1500f, 56f, 1.6f * fade, 160f, k * 0.9f);
                }
            }

            if (whole)
            {
                DrawPrism(spriteBatch, prism, turn, lit);

                // The world seen through the glass: swollen in it, more as it fills with light.
                EmpressWarp.Lens(prism, PrismHeight * 0.95f, 0.26f + 0.16f * lit, 0.12f * (float)Math.Sin(time * 0.02f));
            }
            else
                DrawPrismShards(spriteBatch, actAt, timer / (float)PrismBreakTicks);
        }

        /// <summary>Ticks the backwards shimmer lasts at a pitch (the game plays a sound faster as it pitches it up: an octave, twice as fast).</summary>
        private static int SwellTicks(float pitch) => (int)(1.5f * 60f / (float)Math.Pow(2.0, pitch));

        /// <summary>Two strands of her colours twisting round a ray of light, from one point to another.</summary>
        private static void DrawHelix(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float width, float time, float strength)
        {
            Vector2 line = to - from;
            float length = line.Length();

            if (length < 8f || strength <= 0.01f)
                return;

            Vector2 u = line / length;
            Vector2 n = new Vector2(-u.Y, u.X);
            int steps = Math.Clamp((int)(length / 26f), 8, 60);

            for (int strand = 0; strand < 2; strand++)
            {
                Color colour = strand == 0 ? EmpressArt.Pink : EmpressArt.Sky;
                Vector2 previous = from;

                for (int i = 1; i <= steps; i++)
                {
                    float s = i / (float)steps;
                    float twist = s * length / 60f - time * 0.18f + strand * MathHelper.Pi;
                    Vector2 at = from + u * (length * s) + n * (float)Math.Sin(twist) * width * 0.5f;

                    // The far side of the twist dimmer than the near.
                    float near = 0.55f + 0.45f * (float)Math.Cos(twist);
                    EmpressArt.DrawLine(spriteBatch, previous, at, 3f, EmpressArt.Additive(colour, 0.75f * strength * near));
                    previous = at;
                }
            }
        }

        /// <summary>The prism: a triangle of glass, its edges bright, the light inside it.</summary>
        private static void DrawPrism(SpriteBatch spriteBatch, Vector2 at, float turn, float lit)
        {
            Vector2[] corner = new Vector2[3];

            for (int i = 0; i < 3; i++)
                corner[i] = at + (Up + turn + i * MathHelper.TwoPi / 3f).ToRotationVector2() * (PrismHeight * 2f / 3f);

            EmpressArt.DrawBloom(spriteBatch, at, 120f + 90f * lit, EmpressArt.Additive(EmpressArt.Sky, 0.25f + 0.35f * lit));

            for (int i = 0; i < 3; i++)
            {
                Vector2 a = corner[i];
                Vector2 b = corner[(i + 1) % 3];

                EmpressArt.DrawLine(spriteBatch, a, b, 10f, EmpressArt.Additive(EmpressArt.Sky, 0.4f + 0.3f * lit));
                EmpressArt.DrawLine(spriteBatch, a, b, 3f, EmpressArt.Additive(Color.White, 0.95f));
                EmpressArt.DrawStar(spriteBatch, a, 26f + 20f * lit, turn, EmpressArt.Additive(Color.White, 0.6f));
            }

            // The light inside: a rainbow running through the glass.
            for (int k = 0; k < Spectrum.Length; k++)
            {
                Vector2 p = Vector2.Lerp(corner[1], corner[2], (k + 0.5f) / Spectrum.Length);
                EmpressArt.DrawLine(spriteBatch, Vector2.Lerp(corner[0], at, 0.4f), p, 4f, EmpressArt.Additive(Spectrum[k], 0.2f + 0.5f * lit));
            }

            // Light glinting round its edges, each glint going round the glass.
            float time = (float)Main.timeForVisualEffects;

            for (int g = 0; g < 3; g++)
            {
                float u = (time * 0.012f + g / 3f) % 1f * 3f;
                int edge = (int)u;
                Vector2 glint = Vector2.Lerp(corner[edge % 3], corner[(edge + 1) % 3], u - edge);

                EmpressArt.DrawStar(spriteBatch, glint, 34f + 16f * lit, time * 0.06f + g, EmpressArt.Additive(Color.White, 0.8f));
                EmpressArt.DrawBloom(spriteBatch, glint, 26f, EmpressArt.Additive(Spectrum[(g * 2 + (int)(time / 40f)) % Spectrum.Length], 0.6f));
            }
        }

        /// <summary>The prism in pieces: shards of its glass, edged in light, turning as they fly out; sparks among them; gone.</summary>
        private void DrawPrismShards(SpriteBatch spriteBatch, Vector2 at, float p)
        {
            float fade = 1f - p;
            UnifiedRandomish dice = new UnifiedRandomish(actSeed);

            for (int i = 0; i < 18; i++)
            {
                float a = dice.Next() * MathHelper.TwoPi;
                float speed = 140f + 260f * dice.Next();
                Vector2 piece = at + a.ToRotationVector2() * speed * (float)Math.Pow(p, 0.7);
                float size = 10f + 14f * dice.Next();

                EmpressArt.DrawStar(spriteBatch, piece, size * fade, a + p * 4f, EmpressArt.Additive(i % 3 == 0 ? Color.White : Spectrum[i % Spectrum.Length], fade));
            }

            // Its glass: little triangles, each turning its own way as it flies.
            for (int i = 0; i < 14; i++)
            {
                float a = dice.Next() * MathHelper.TwoPi;
                float flight = (90f + 220f * dice.Next()) * (float)Math.Pow(p, 0.6);
                float size = 10f + 16f * dice.Next();
                float spin = (dice.Next() - 0.5f) * 14f * p + dice.Next() * MathHelper.TwoPi;
                Vector2 middle = at + a.ToRotationVector2() * flight + new Vector2(0f, 60f * p * p);
                Color edge = Spectrum[i % Spectrum.Length];

                for (int c = 0; c < 3; c++)
                {
                    Vector2 from = middle + (spin + c * MathHelper.TwoPi / 3f).ToRotationVector2() * size;
                    Vector2 to = middle + (spin + (c + 1) * MathHelper.TwoPi / 3f).ToRotationVector2() * size;

                    EmpressArt.DrawLine(spriteBatch, from, to, 5f, EmpressArt.Additive(edge, 0.5f * fade));
                    EmpressArt.DrawLine(spriteBatch, from, to, 1.6f, EmpressArt.Additive(Color.White, 0.9f * fade));
                }
            }

            EmpressArt.DrawBloom(spriteBatch, at, 200f * (1f + p), EmpressArt.Additive(Color.White, 0.6f * fade * fade));
        }

        /// <summary>A small, seeded run of numbers, 0 to 1: the same on every machine for the same seed.</summary>
        internal struct UnifiedRandomish
        {
            private uint state;

            public UnifiedRandomish(int seed) => state = (uint)seed * 2654435761u + 0x9E3779B9u;

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
