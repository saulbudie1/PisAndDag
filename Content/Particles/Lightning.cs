using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{ 
    public class Lightning : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>
        /// Subdivision passes. Each one doubles the point count, so this is the difference
        /// between a bent line and a proper crackle: 5 gives 33 points per strand.
        /// </summary>
        private const int Detail = 5;

        /// <summary>
        /// First displacement as a fraction of the arc's total length. Raise it for a
        /// wilder, more scattered bolt; drop it toward 0.03 for a taut one.
        /// </summary>
        private const float Jaggedness = 0.13f;

        /// <summary>
        /// How much of the displacement survives into the next pass. Below 0.5 the fine
        /// detail vanishes and the bolt reads as a smooth curve; above about 0.65 the
        /// small kinks rival the big ones and it turns to mush.
        /// </summary>
        private const float Persistence = 0.55f;

        /// <summary>How far the interior points twitch each frame, in pixels.</summary>
        private const float Flicker = 2.4f;

        /// <summary>
        /// How much of the base width survives to the far end. Real discharge is fattest
        /// where it leaves and thins as it goes, but it never tapers to a point — the tip
        /// keeps a good fraction of the trunk.
        /// </summary>
        private const float TipFraction = 0.45f;

        /// <summary>
        /// Curve of the taper. Below 1 the arc holds its thickness for a while before
        /// narrowing, which reads far heavier than a straight linear falloff.
        /// </summary>
        private const float TaperCurve = 0.75f;

        /// <summary>Width of a fork relative to the trunk where it branches off.</summary>
        private const float ForkWidth = 0.7f;

        /// <summary>How far a fork itself tapers, relative to its own base.</summary>
        private const float ForkTipFraction = 0.4f;

        /// <summary>Settled shape of each strand. Index 0 is the main arc.</summary>
        private readonly List<Vector2[]> strands = new List<Vector2[]>();

        /// <summary>The per-frame flickered copy that actually gets drawn.</summary>
        private readonly List<Vector2[]> live = new List<Vector2[]>();

        /// <summary>Half-width of each strand where it starts, in pixels.</summary>
        private readonly List<float> baseWidths = new List<float>();

        /// <summary>Half-width where it ends. Always the thinner of the two.</summary>
        private readonly List<float> tipWidths = new List<float>();

        /// <summary>
        /// Where each fork hangs off the main arc, as an index into strand 0, or -1 for
        /// the main arc itself. Forks track the parent's LIVE point rather than its
        /// settled one, so they stay welded on while the whole thing twitches.
        /// </summary>
        private readonly List<int> attach = new List<int>();

        private readonly Color glow;
        private float brightness = 1f;

        private Lightning(Vector2 start, Vector2 end, Color color, float width, int lifetime, int forks)
        {
            Position = start;
            Velocity = Vector2.Zero;
            Lifetime = lifetime;
            Scale = 1f;
            Color = color;
            glow = color;

            float tipWidth = width * TipFraction;

            Vector2[] trunk = BuildPath(start, end, Detail, Jaggedness);
            AddStrand(trunk, width, tipWidth, -1);

            for (int i = 0; i < forks; i++)
            {
                // Never off the last fifth: a fork sprouting a pixel from the impact
                // point looks like a glitch rather than a branch.
                int index = Main.rand.Next(trunk.Length / 5, trunk.Length * 4 / 5);

                Vector2 from = trunk[index];
                Vector2 heading = trunk[trunk.Length - 1] - from;

                if (heading.LengthSquared() < 1f)
                    continue;

                float veer = MathHelper.ToRadians(Main.rand.NextFloat(20f, 55f)) * (Main.rand.NextBool() ? 1f : -1f);
                float reach = heading.Length() * Main.rand.NextFloat(0.3f, 0.65f);

                Vector2 to = from + heading.SafeNormalize(Vector2.UnitX).RotatedBy(veer) * reach;

                // A fork is sized against the trunk AT THE POINT IT LEAVES, not against
                // the bolt's base, so one splitting off near the tip comes out slender and
                // one splitting off early comes out heavy — which is what makes the whole
                // thing read as branching rather than as separate bolts drawn together.
                float here = WidthAt(width, tipWidth, index / (float)(trunk.Length - 1));
                float forkBase = here * ForkWidth;

                AddStrand(BuildPath(from, to, Detail - 1, Jaggedness * 1.5f),
                    forkBase, forkBase * ForkTipFraction, index);
            }
        }

        private void AddStrand(Vector2[] points, float baseWidth, float tipWidth, int attachIndex)
        {
            strands.Add(points);
            live.Add((Vector2[])points.Clone());
            baseWidths.Add(baseWidth);
            tipWidths.Add(tipWidth);
            attach.Add(attachIndex);
        }

        /// <summary>Half-width some fraction of the way along a strand.</summary>
        private static float WidthAt(float baseWidth, float tipWidth, float progress)
        {
            return MathHelper.Lerp(baseWidth, tipWidth, MathF.Pow(progress, TaperCurve));
        }

        /// <summary>
        /// Throws an arc from <paramref name="start"/> to <paramref name="end"/>.
        /// </summary>
        /// <param name="color">Tint of the glow. The core always burns white on top of it.</param>
        /// <param name="width">Half-width at the ORIGIN. The arc tapers from here to the
        /// destination, and forks are sized off the trunk where they branch.</param>
        /// <param name="lifetime">Ticks it lasts. Real discharge is brief — 12 to 24 reads best.</param>
        /// <param name="forks">Branches sprouting off the main arc. 0 for a clean bolt.</param>
        public static void Spawn(Vector2 start, Vector2 end, Color color, float width = 26f,
            int lifetime = 18, int forks = 2)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new Lightning(start, end, color, width, lifetime, forks));
        }

        /// <summary>Aquamarine arc, the default electric look.</summary>
        public static void Spawn(Vector2 start, Vector2 end)
        {
            Spawn(start, end, new Color(120, 235, 255));
        }

        /// <summary>
        /// Recursive midpoint displacement. Each pass inserts a point between every pair
        /// and shoves it perpendicular to that pair's own direction, so the detail follows
        /// the bend rather than a fixed axis.
        /// </summary>
        private static Vector2[] BuildPath(Vector2 start, Vector2 end, int passes, float jaggedness)
        {
            List<Vector2> points = new List<Vector2> { start, end };
            float offset = Vector2.Distance(start, end) * jaggedness;

            for (int pass = 0; pass < passes; pass++)
            {
                List<Vector2> next = new List<Vector2>(points.Count * 2);

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 b = points[i + 1];

                    Vector2 along = b - a;
                    Vector2 across = new Vector2(-along.Y, along.X).SafeNormalize(Vector2.UnitY);

                    next.Add(a);
                    next.Add((a + b) * 0.5f + across * Main.rand.NextFloat(-offset, offset));
                }

                // The final point is never displaced, which is what pins the arc to its
                // destination however many passes run.
                next.Add(points[points.Count - 1]);

                points = next;
                offset *= Persistence;
            }

            return points.ToArray();
        }

        public override void Update()
        {
            float life = LifetimeCompletion;

            for (int s = 0; s < strands.Count; s++)
            {
                Vector2[] settled = strands[s];
                Vector2[] current = live[s];

                for (int i = 1; i < settled.Length - 1; i++)
                    current[i] = settled[i] + Main.rand.NextVector2Circular(Flicker, Flicker);

                // Ends stay put. A fork's start is the parent's live point, so it stays
                // welded on instead of drifting off the trunk as everything twitches.
                current[0] = attach[s] < 0 ? settled[0] : live[0][attach[s]];
                current[current.Length - 1] = settled[settled.Length - 1];
            }

            // A hard flash that decays, sputtering frame to frame. Electricity does not
            // fade smoothly, and a linear ramp reads as a laser instead.
            brightness = (1f - life) * Main.rand.NextFloat(0.6f, 1f);

            Vector2[] trunk = live[0];
            Vector3 light = glow.ToVector3() * brightness * 0.7f;

            // Every fourth point is plenty; lighting is per tile anyway.
            for (int i = 0; i < trunk.Length; i += 4)
                Lighting.AddLight(trunk[i], light);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            // Left-middle, so a segment is drawn by anchoring at its first point and
            // stretching toward the second.
            Vector2 origin = new Vector2(0f, texture.Height * 0.5f);

            // Alpha is NOT zeroed here, and that matters. PADParticleHandler draws the
            // additive bucket with BlendState.Additive, which is SourceAlpha x One — the
            // source is multiplied by its own alpha, so an alpha of 0 contributes exactly
            // nothing and the whole bolt renders invisible. Scaling the Color wholesale
            // dims every channel together and keeps alpha positive, which is what every
            // other additive particle here already does.
            Color halo = glow * (0.55f * brightness);
            Color core = Color.White * brightness;

            for (int s = 0; s < live.Count; s++)
            {
                Vector2[] points = live[s];

                // Guard the divide: a two-point strand has exactly one segment.
                float lastSegment = Math.Max(1, points.Length - 2);

                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector2 a = points[i];
                    Vector2 delta = points[i + 1] - a;

                    float length = delta.Length();
                    if (length < 0.01f)
                        continue;

                    float width = WidthAt(baseWidths[s], tipWidths[s], i / lastSegment);

                    float rotation = delta.ToRotation();
                    Vector2 draw = a - Main.screenPosition;

                    // Slight overlap on the length, or the joins between segments show as
                    // a dotted line wherever the arc turns sharply.
                    float span = (length + 1f) / texture.Width;

                    spriteBatch.Draw(texture, draw, null, halo, rotation, origin,
                        new Vector2(span, width * 2.8f / texture.Height), SpriteEffects.None, 0f);

                    spriteBatch.Draw(texture, draw, null, core, rotation, origin,
                        new Vector2(span, width / texture.Height), SpriteEffects.None, 0f);
                }
            }

            // Flares at both ends of the main arc. Discharge dumps its energy where it
            // lands, and without these the bolt reads as a drawn line rather than
            // something that struck.
            Vector2[] trunk = live[0];
            Flare(spriteBatch, texture, trunk[0], baseWidths[0] * 1.9f, halo, core);
            Flare(spriteBatch, texture, trunk[trunk.Length - 1], tipWidths[0] * 3.0f, halo, core);
        }

        /// <summary>
        /// A cross-shaped burst at a point: the same strip drawn horizontally and
        /// vertically. Cheaper than carrying a second texture, and additive blending
        /// makes the overlap burn out into a hot centre on its own.
        /// </summary>
        private static void Flare(SpriteBatch spriteBatch, Texture2D texture, Vector2 world,
            float size, Color halo, Color core)
        {
            Vector2 draw = world - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            Vector2 wide = new Vector2(size * 2.4f / texture.Width, size / texture.Height);
            Vector2 tall = new Vector2(size / texture.Width, size * 2.4f / texture.Height);

            spriteBatch.Draw(texture, draw, null, halo, 0f, origin, wide, SpriteEffects.None, 0f);
            spriteBatch.Draw(texture, draw, null, halo, 0f, origin, tall, SpriteEffects.None, 0f);

            Vector2 hot = new Vector2(size * 0.55f / texture.Width, size * 0.55f / texture.Height);
            spriteBatch.Draw(texture, draw, null, core, 0f, origin, hot, SpriteEffects.None, 0f);
        }
    }
}
