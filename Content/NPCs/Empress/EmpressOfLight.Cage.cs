using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the cage of curtains (Legends act, her second half) -----------------------------------------------------
    //
    // By request: "Drags the curtains into a square decagon radius of 7 tiles and moves around"; as chosen, a cage about
    // the player that drifts through the arena, its walls hurting.
    //
    //     CALL    40   her free hand lifted; her curtains stir all round the arena's border, and where they will close is
    //                  shown about the player: a small decagon of thin light
    //     DRAG    54   she drags them in, the arena's whole round of them, closing to a decagon seven tiles from its middle
    //                  to a corner about where the player was; its walls hold them in as the arena's do
    //     DRIFT  330   it goes wandering through the arena, the player kept in it, her hand leading it; its walls hurt
    //     OPEN    36   let go: the curtains thrown back out to the border, and gone
    //
    // The walls hurt whoever touches them once it has closed, held or not (50 in a normal world, 34 in an expert one,
    // doubled as the game doubles any hostile shot), but for its floor and the slopes beside it, which are stood on. Only
    // the one it closed on is held in it (EmpressArena.Cage).

    public partial class EmpressOfLight
    {
        private const int CageCall = 0;
        private const int CageDrag = 1;
        private const int CageDrift = 2;
        private const int CageOpen = 3;

        private const int CageCallTicks = 40;
        private const int CageDragTicks = 54;
        private const int CageDriftTicks = 330;
        private const int CageOpenTicks = 36;

        /// <summary>Middle to a corner (seven tiles); how far in from its walls they hurt; how far its curtains hang out from them.</summary>
        private const float CageSmall = 112f;
        private const float CageWall = 12f;
        private const float CageHang = 120f;

        /// <summary>A wall facing down more than this (its normal's y) is floor, or a slope of it, and does not hurt.</summary>
        private const float CageFloorLeans = 0.75f;

        /// <summary>How far from where it closed it wanders, and how fast its wandering turns (radians a tick).</summary>
        private const float CageWander = 440f;
        private const float CageTurnA = 0.0055f;
        private const float CageTurnB = 0.0089f;

        private const int CageNormal = 50;
        private const int CageExpert = 34;

        private void PlanCage(NPC npc, Player target)
        {
            // Where it closes: about the player, well in from the arena's walls.
            Vector2 none = Vector2.Zero;
            actAt = Decagon.PushInside(target.Center, arenaCentre, EmpressArena.Radius, CageSmall + 140f, ref none);
        }

        /// <summary>The cage this tick: its middle and its size (a corner's reach), and how strongly its curtains show.</summary>
        private void CageNow(out Vector2 centre, out float size, out float strength)
        {
            float t = timer;

            switch (beat)
            {
                case CageCall:
                    centre = arenaCentre;
                    size = EmpressArena.Radius;
                    strength = 0.3f * Ease(t / CageCallTicks);
                    return;

                case CageDrag:
                    {
                        float p = Smoother(t / CageDragTicks);
                        centre = Vector2.Lerp(arenaCentre, actAt, p);
                        size = MathHelper.Lerp(EmpressArena.Radius, CageSmall, p);
                        strength = MathHelper.Lerp(0.3f, 1f, p);
                        return;
                    }

                case CageDrift:
                    centre = CageWandered(t);
                    size = CageSmall;
                    strength = 1f;
                    return;

                default:
                    {
                        float p = Ease(t / CageOpenTicks);
                        centre = Vector2.Lerp(CageWandered(CageDriftTicks), arenaCentre, p);
                        size = MathHelper.Lerp(CageSmall, EmpressArena.Radius * 1.05f, p);
                        strength = 1f - p;
                        return;
                    }
            }
        }

        /// <summary>Where the cage has wandered to, so many ticks into its drift: two slow turnings each way, coming in from nothing, kept off the arena's walls.</summary>
        private Vector2 CageWandered(float t)
        {
            UnifiedRandomish dice = new UnifiedRandomish(actSeed);
            float a = dice.Next() * MathHelper.TwoPi;
            float b = dice.Next() * MathHelper.TwoPi;
            float c = dice.Next() * MathHelper.TwoPi;
            float d = dice.Next() * MathHelper.TwoPi;
            float reach = CageWander * Ease(t / 70f) * 0.5f;

            // Each turning starts at its own zero, so the drift starts where the cage closed.
            Vector2 off = new Vector2(
                (float)(Math.Sin(t * CageTurnA + a) - Math.Sin(a)) + (float)(Math.Sin(t * CageTurnB + b) - Math.Sin(b)),
                (float)(Math.Sin(t * CageTurnB * 0.83f + c) - Math.Sin(c)) + (float)(Math.Sin(t * CageTurnA * 1.21f + d) - Math.Sin(d))) * reach;

            Vector2 none = Vector2.Zero;
            return Decagon.PushInside(actAt + off, arenaCentre, EmpressArena.Radius, CageSmall + 60f, ref none);
        }

        private void RunCage(NPC npc, Player who, bool entered)
        {
            int t = timer;
            CageNow(out Vector2 centre, out float size, out float strength);

            // Her free hand: lifted to call them, then leading the cage.
            float toCage = (centre - npc.Center).ToRotation();

            if (beat == CageCall)
                FreeArmFrame(4);
            else
                FreeArmAt(toCage);

            // She keeps off to one side of it, above, as she leads it.
            if (beat >= CageDrag)
            {
                Vector2 place = centre + new Vector2(actSide * -420f, -300f);
                FlyTo(npc, place, 9f, 0.06f);
            }
            else
            {
                npc.velocity *= 0.9f;
            }

            // The walls stand from the drag on: holding the one it closes on, hurting whoever touches them.
            if (beat == CageDrag || beat == CageDrift || beat == CageOpen && t < 6)
                EmpressArena.Cage(centre, size, actWho);

            switch (beat)
            {
                case CageCall:
                    if (entered && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Tingle, npc.Center, -0.1f, 0.7f);
                        Sound(EmpressSounds.Hum, actAt, 0.2f, 0.5f, 2);
                    }

                    if (Authority && t >= CageCallTicks)
                        Next(npc, CageDrag);

                    break;

                case CageDrag:
                    if (entered)
                    {
                        Sound(EmpressSounds.Fold, actAt, 0f, 1f, 2);
                        Sound(EmpressSounds.WhooshDown, npc.Center, -0.3f, 0.7f, 2);
                        Hitbox(npc, EmpressHitbox.Shape.Cage, actAt, 0f, Hurt(npc, CageNormal, CageExpert), CageDragTicks + CageDriftTicks + 20);
                    }

                    if (Crossed(CageDragTicks - 4) && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Lock, actAt, -0.2f, 0.8f, 2);
                        EmpressWarp.Ripple(actAt, 9f, 40f, 8f, 26);
                    }

                    if (Authority && t >= CageDragTicks)
                        Next(npc, CageDrift);

                    break;

                case CageDrift:
                    if (!Main.dedServ && t % 90 == 0)
                        Sound(EmpressSounds.Hum, centre, -0.2f + 0.1f * (t / 90), 0.45f, 2);

                    if (!Main.dedServ && Main.rand.NextBool(3))
                    {
                        int edge = Main.rand.Next(Decagon.Sides);
                        Vector2 at = Vector2.Lerp(Decagon.Corner(centre, size, edge), Decagon.Corner(centre, size, edge + 1), Main.rand.NextFloat());
                        Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Decagon.Normal(edge) * Main.rand.NextFloat(0.5f, 2f) + new Vector2(0f, -0.8f), 0,
                            Color.Lerp(AuroraFoot, AuroraTop, Main.rand.NextFloat()), 0.9f);
                        dust.noGravity = true;
                    }

                    if (Authority && t >= CageDriftTicks)
                        Next(npc, CageOpen);

                    break;

                case CageOpen:
                    if (entered && !Main.dedServ)
                    {
                        Sound(EmpressSounds.WhooshUp, centre, 0.1f, 0.8f, 2);
                        Sound(EmpressSounds.Chime, centre, -0.2f, 0.5f, 2);
                    }

                    if (Authority && t >= CageOpenTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>
        /// The walls' hitbox asks: does this body touch them (from inside or out)? Once it has closed (its walls sweeping
        /// in through the drag would catch some and miss others), and its own box against each wall, not its middle. Its
        /// floor and the slopes either side of it are stood on, as the arena's are, and do not hurt: the walls that do are
        /// the ones a body is carried into as it drifts, or that it leaps at.
        /// </summary>
        internal bool CageHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Cage || beat != CageDrift)
                return false;

            CageNow(out Vector2 centre, out float size, out _);
            Vector2 p = box.Center.ToVector2() - centre;
            Vector2 half = new Vector2(box.Width, box.Height) * 0.5f;
            float wall = size * Decagon.Apothem;
            float halfEdge = size * (float)Math.Sin(Math.PI / Decagon.Sides);

            for (int j = 0; j < Decagon.Sides; j++)
            {
                Vector2 n = Decagon.Normal(j);

                if (n.Y > CageFloorLeans)
                    continue;

                Vector2 along = new Vector2(-n.Y, n.X);
                float reach = Math.Abs(n.X) * half.X + Math.Abs(n.Y) * half.Y;
                float side = Math.Abs(along.X) * half.X + Math.Abs(along.Y) * half.Y;
                float out_ = Vector2.Dot(p, n);

                // Across the wall's band (its line, CageWall either side), and along its length.
                if (out_ + reach > wall - CageWall && out_ - reach < wall + CageWall && Math.Abs(Vector2.Dot(p, along)) < halfEdge + side)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The curtains (Strip: no batch open): a band of the aurora's light hanging out from every wall, green at the wall
        /// rising through blue to violet and gone, wavering; brightest at the wall.
        /// </summary>
        private void DrawCageBands(NPC npc)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            CageNow(out Vector2 centre, out float size, out float strength);

            if (strength <= 0.003f)
                return;

            float time = (float)Main.timeForVisualEffects;
            Texture2D line = EmpressArt.Line.Value;

            // Each wall's band, out from it: the foot, then up through the middle to the crown. Each runs on a little
            // past both its corners, fading as it goes, into its neighbours' (the bands lean out each their own way,
            // and would leave a gap of dark at every corner).
            for (int edge = 0; edge < Decagon.Sides; edge++)
            {
                Vector2 a = Decagon.Corner(centre, size, edge);
                Vector2 b = Decagon.Corner(centre, size, edge + 1);
                Vector2 n = Decagon.Normal(edge);
                float length = Vector2.Distance(a, b);
                int steps = Math.Clamp((int)(length / 18f), 4, 120);
                float hang = CageHang * (0.6f + 0.4f * Math.Min(1f, CageSmall * 2f / size));
                float past = Math.Min(hang * 0.45f, length * 0.5f) / Math.Max(1f, length);
                int pastSteps = Math.Max(2, (int)Math.Ceiling(past * steps));

                for (int band = 0; band < 3; band++)
                {
                    float from = band == 0 ? 0f : band == 1 ? 0.25f : 0.6f;
                    float to = band == 0 ? 0.25f : band == 1 ? 0.6f : 1f;
                    Color inner = band == 0 ? EmpressArt.Additive(Color.Lerp(Color.White, AuroraFoot, 0.5f), 0.85f) : band == 1 ? EmpressArt.Additive(AuroraFoot, 0.55f) : EmpressArt.Additive(AuroraMid, 0.35f);
                    Color outer = band == 0 ? EmpressArt.Additive(AuroraFoot, 0.55f) : band == 1 ? EmpressArt.Additive(AuroraMid, 0.35f) : Color.Transparent;

                    if (band == 2)
                        outer = EmpressArt.Additive(AuroraTop, 0f);

                    Strip.Begin();

                    for (int i = -pastSteps; i <= steps + pastSteps; i++)
                    {
                        // Along the wall (0 to 1 its corners), and past them.
                        float s = i < 0 ? i / (float)pastSteps * past : i > steps ? 1f + (i - steps) / (float)pastSteps * past : i / (float)steps;
                        Vector2 at = Vector2.Lerp(a, b, s);

                        // Wavering out from the wall, each wall its own way, more toward its middle; let go past its corners.
                        float ends = (float)Math.Sin(MathHelper.Clamp(s, 0f, 1f) * MathHelper.Pi);
                        float waver = 1f + 0.25f * (float)Math.Sin(s * 9f + time * 0.06f + edge * 1.7f) * ends + 0.12f * (float)Math.Sin(s * 23f - time * 0.11f + edge);
                        float beyond = s < 0f ? -s / past : s > 1f ? (s - 1f) / past : 0f;
                        float letGo = 1f - beyond * beyond * (3f - 2f * beyond);
                        float shine = strength * (0.8f + 0.2f * (float)Math.Sin(s * 13f + time * 0.08f + edge * 2.3f)) * letGo;

                        Vector2 p1 = at + n * hang * from * waver;
                        Vector2 p2 = at + n * hang * to * waver;
                        Strip.Pair(p1, p2, inner * shine, outer * shine, s, FlatRow, FlatRow);
                    }

                    Strip.Draw(line, SamplerState.LinearClamp);
                }
            }
        }

        private void DrawCageOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            CageNow(out Vector2 centre, out float size, out float strength);

            // As she calls them: where it will close, thin and flickering.
            if (beat == CageCall)
            {
                float show = Ease(timer / 16f);
                float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 0.5f);

                for (int k = 0; k < Decagon.Sides; k++)
                {
                    Vector2 a = Decagon.Corner(actAt, CageSmall, k);
                    Vector2 b = Decagon.Corner(actAt, CageSmall, k + 1);
                    EmpressArt.DrawLine(spriteBatch, a, b, 8f, EmpressArt.Additive(AuroraFoot, 0.3f * show * flicker));
                    EmpressArt.DrawLine(spriteBatch, a, b, 1.6f, EmpressArt.Additive(Color.White, 0.7f * show));
                }

                return;
            }

            if (strength <= 0.003f)
                return;

            // The walls themselves: a hard line of light along each, corners glinting, the air along them shimmering.
            for (int k = 0; k < Decagon.Sides; k++)
            {
                Vector2 a = Decagon.Corner(centre, size, k);
                Vector2 b = Decagon.Corner(centre, size, k + 1);
                Color colour = Color.Lerp(AuroraFoot, AuroraTop, 0.5f + 0.5f * (float)Math.Sin(time * 0.03f + k));

                EmpressArt.DrawLine(spriteBatch, a, b, 14f, EmpressArt.Additive(colour, 0.35f * strength));
                EmpressArt.DrawLine(spriteBatch, a, b, 3f, EmpressArt.Additive(Color.White, 0.9f * strength));

                float twinkle = 0.7f + 0.3f * (float)Math.Sin(time * 0.2f + k * 1.3f);
                EmpressArt.DrawStar(spriteBatch, a, 30f * twinkle, time * 0.04f + k, EmpressArt.Additive(Color.White, 0.8f * strength));

                if (size < CageSmall * 3f)
                    EmpressWarp.Haze(a, b, 40f, 1.8f * strength, 80f, k * 0.7f);
            }

            // Her hand leading it: a thread of her light from her palm to it.
            if (beat == CageDrift || beat == CageDrag)
            {
                Vector2 palm = Palm(npc, !BowLeft(npc), freeAngleNow);
                Vector2 near = centre + (palm - centre).SafeNormalize(Vector2.UnitY) * size * Decagon.Apothem;

                EmpressArt.DrawBeam(spriteBatch, palm, near, 8f, EmpressArt.Additive(AuroraMid, 0.25f * strength));
                EmpressArt.DrawBeam(spriteBatch, palm, near, 1.6f, EmpressArt.Additive(Color.White, 0.6f * strength));
                EmpressArt.DrawBloom(spriteBatch, palm, 60f, EmpressArt.Additive(AuroraFoot, 0.6f * strength));
            }
        }
    }
}
