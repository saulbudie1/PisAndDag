using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its body along its way --------------------------------------------------------------------------------------------
    //
    // By request: "the body of the death animation should follow the path all the way instead of having 80% of it frozen".
    // Vanilla pulls each segment toward the one ahead of it and no further, so a head that turns faster than its body is
    // long leaves most of the body lying still and cuts every corner. While its head is driven here (a scene, a move), the
    // way the head has come is kept, point by point, and every segment is put on it at its distance back along it (44
    // pixels a segment, as vanilla spaces them), facing the one ahead: the whole body goes where the head went.
    //
    // The way is begun from where the body lies when the driving begins (or from where it is laid out deep under the
    // ground before it rises), so nothing jumps. Each machine keeps its own, from its own head.

    public partial class Destroyer
    {
        /// <summary>The way its head has come, oldest first.</summary>
        private readonly List<Vector2> trail = new();

        /// <summary>This machine is laying its body along the way (begun from the body as it lay).</summary>
        private bool trailLive;

        /// <summary>Where each segment of its body is to be this tick, and its rotation, from the head back.</summary>
        private Vector2[] places = Array.Empty<Vector2>();
        private float[] facings = Array.Empty<float>();
        private int placed;

        /// <summary>
        /// How hard it shakes, in pixels, this tick (set by a scene or move each tick it wants it; none otherwise). Only
        /// seen: every part, head to tail, is drawn that far off where it really is, a new way each frame, and nothing moves
        /// (by request: "just a visual shake of all segments in place", where its body had followed its trembling head and
        /// curled up behind it).
        /// </summary>
        internal float shake;

        /// <summary>Where its head is drawn off by this frame, to be put back after.</summary>
        private Vector2 drawShift;

        /// <summary>A segment's place on the way this tick, if its head is driven and it has one.</summary>
        internal bool PlaceOf(int index, out Vector2 place, out float facing)
        {
            if (!Drives || index < 0 || index >= placed)
            {
                place = default;
                facing = 0f;
                return false;
            }

            place = places[index];
            facing = facings[index];
            return true;
        }

        /// <summary>The way begun from where its body lies now: its tail first, its head last.</summary>
        private void SeedTrail(NPC npc)
        {
            trail.Clear();

            for (int i = chain.Count - 1; i >= 0; i--)
                trail.Add(Main.npc[chain[i]].Center);

            trail.Add(npc.Center);
        }

        /// <summary>The way begun from where its body is laid out before it rises, so it comes up out of the ground along it.</summary>
        private void SeedLay(NPC npc)
        {
            trail.Clear();

            for (int i = chain.Count - 1; i >= 0; i--)
                trail.Add(LayAt(i, npc.scale));

            trail.Add(start);
        }

        /// <summary>Its head's place after this tick added to the way, and every segment put on the way behind it.</summary>
        private void LayBody(NPC npc)
        {
            int n = chain.Count;

            if (n == 0)
                return;

            Vector2 head = npc.Center + npc.velocity;

            if (Laying)
                SeedLay(npc);
            else if (!trailLive || trail.Count == 0)
                SeedTrail(npc);

            trailLive = true;

            if (Vector2.DistanceSquared(trail[trail.Count - 1], head) > 4f)
                trail.Add(head);

            if (places.Length < n)
            {
                places = new Vector2[n + 16];
                facings = new float[n + 16];
            }

            float spacing = (int)(44f * npc.scale);
            float want = spacing;
            float carried = 0f;
            Vector2 prev = head;
            int k = trail.Count - 1;
            int seg = 0;
            int guard = 0;

            while (seg < n && guard++ < 100000)
            {
                if (k < 0)
                {
                    // Past the oldest of the way: on along its last direction.
                    Vector2 back = trail.Count > 1 ? trail[0] - trail[1] : Vector2.UnitY;
                    back = back.SafeNormalize(Vector2.UnitY);
                    places[seg] = prev + back * (want - carried);
                    seg++;
                    want += spacing;
                    continue;
                }

                Vector2 next = trail[k];
                float length = Vector2.Distance(prev, next);

                if (length > 0.001f && carried + length >= want)
                {
                    places[seg] = Vector2.Lerp(prev, next, (want - carried) / length);
                    seg++;
                    want += spacing;
                    continue;
                }

                carried += length;
                prev = next;
                k--;
            }

            placed = seg;

            // What is older than the tail needs no keeping.
            int keepFrom = Math.Max(0, k - 2);

            if (keepFrom > 0)
                trail.RemoveRange(0, keepFrom);

            if (trail.Count > 6000)
                trail.RemoveRange(0, trail.Count - 6000);

            // Each faces the one ahead of it; trembling, if a move asks.
            for (int i = 0; i < placed; i++)
            {
                Vector2 ahead = i == 0 ? head : places[i - 1];
                facings[i] = (ahead - places[i]).ToRotation() + MathHelper.PiOver2;
            }

        }

        /// <summary>
        /// Its head steered toward a place, a worm's way: its heading turned toward it by at most <paramref name="turn"/>
        /// radians a tick, its speed eased toward <paramref name="speed"/> by <paramref name="ease"/> of the gap a tick.
        /// </summary>
        private static void Steer(NPC npc, Vector2 to, float speed, float turn, float ease = 0.08f)
        {
            float heading = npc.velocity.LengthSquared() > 0.04f ? npc.velocity.ToRotation() : npc.rotation - MathHelper.PiOver2;
            Vector2 want = to - npc.Center;

            if (want.LengthSquared() > 1f)
                heading = heading.AngleTowards(want.ToRotation(), turn);

            float now = MathHelper.Lerp(npc.velocity.Length(), speed, ease);
            npc.velocity = heading.ToRotationVector2() * now;
            npc.rotation = heading + MathHelper.PiOver2;
        }

        /// <summary>Its head on along its heading, turned toward a direction by at most <paramref name="turn"/> a tick, at <paramref name="speed"/>.</summary>
        private static void Glide(NPC npc, Vector2 toward, float speed, float turn, float ease = 0.08f) => Steer(npc, npc.Center + toward.SafeNormalize(Vector2.UnitY) * 4000f, speed, turn, ease);
    }
}
