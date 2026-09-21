using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A leaf of chlorophyte thrown off where a rifle round lands.
    ///
    /// It lives two lives. Thrown clear of the impact it behaves like BloodDroplet — fast,
    /// streaked along its travel, rotation locked to velocity. Then the air catches it and
    /// it becomes a leaf: gravity takes hold and it begins to sway side to side, rock
    /// against the sway, and turn edge-on and back. Until that moment it is weightless,
    /// coasting out on the throw alone. The handover is on a timer, for reasons the
    /// FlutterStartTick comment goes into.
    ///
    /// It is also not decoration. A leaf flagged to purify converts every tile it crosses,
    /// so the clean ground is literally the path the spray drifted across — and because a
    /// fluttering leaf wanders, that path is wider and more irregular than the throw.
    /// </summary>
    public class ChlorophyteSpill : PADParticle
    {
        public override bool SetLifetime => true;

        public override bool UseCustomDraw => true;

        /// <summary>
        /// Shrink per tick.
        ///
        /// NOT BloodDroplet's 0.985. That figure is tuned to blood's 28-46 tick life and
        /// leaves a droplet at 57% of its starting size when it dies; applied to this
        /// particle's much longer life it would reach 10%, which is gone long before the
        /// fade catches up. 0.9963 over a 150-tick life lands on the same 57%, so the
        /// shrink reads identically at a quarter of the speed.
        /// </summary>
        private const float ShrinkPerTick = 0.9963f;

        /// <summary>Downward pull per tick.</summary>
        private const float Gravity = 0.14f;

        /// <summary>Horizontal damping per tick. Brisk — a leaf does not sail.</summary>
        private const float Drag = 0.952f;

        /// <summary>
        /// Vertical speed cap while still flying clear.
        ///
        /// Set above the fastest throw the cone produces (about 9.4px/tick) so it never
        /// clips the throw itself — during this phase the cap is only a safety rail, and
        /// drag is what actually slows the fragment down.
        /// </summary>
        private const float MaxFall = 12f;

        /// <summary>
        /// Terminal speed once it is fluttering.
        ///
        /// Much lower, because this is the whole reason a leaf falls the way it does: a
        /// broad flat thing presenting its face to the air simply cannot fall fast. Leave
        /// it near MaxFall and the sway just looks like a wobble on a dropping stone.
        /// </summary>
        private const float LeafFall = 1.15f;

        /// <summary>
        /// Ticks over which the fragment becomes a leaf.
        ///
        /// Before this it is weightless: no gravity, no sway, drag on both axes, so the
        /// throw simply coasts out and slows. Gravity and the swinging arrive together at
        /// the start of the ramp — one moment where the air catches it, rather than the
        /// leaf drifting sideways while still falling like a stone.
        ///
        /// Driven by AGE, not by speed. Speed was the obvious choice and it deadlocks:
        /// slowing the fall is what the ramp DOES, but the un-slowed fall sits above any
        /// sensible speed threshold, so a leaf at rest still measured "fast", never
        /// crossed over, and fell like a stone forever. The sway force fed the same loop —
        /// settling at 3.6px/tick against this drag, on its own enough to suppress the
        /// flutter that produced it. Age has no feedback path at all.
        /// </summary>
        private const int FlutterStartTick = 45;
        private const int FlutterFullTick = 85;

        /// <summary>
        /// How far the leaf swings to each side, in pixels.
        ///
        /// Applied as a POSITION offset, not an acceleration. An acceleration integrates:
        /// any imbalance accumulates into velocity and the leaf slides off across the
        /// world instead of swinging about a line. Offsetting position by a sine is
        /// bounded by construction — it cannot drift however long the leaf lives.
        /// </summary>
        private const float SwayAmplitude = 7f;

        /// <summary>How far the leaf rocks away from its travel angle, in radians.</summary>
        private const float RockRadians = 0.9f;

        /// <summary>Tiles one leaf may convert before it stops working. Bounds the cost.</summary>
        private const int MaxConversions = 16;

        /// <summary>Leaves per burst allowed to purify. The rest are visual only.</summary>
        private const int PurifyingLeaves = 20;

        /// <summary>
        /// How hard the centre of the cone is thrown, as a fraction of the reach in tiles.
        ///
        /// Lifted out of the spawn loop so throw strength is tunable from up here. It was
        /// buried in a Lerp, which is why multiplying the direction vector at the call
        /// site looked like it should work and did nothing — SpawnBurst normalises that
        /// argument, so only its angle ever survived. The speedScale parameter is the
        /// honest version of that idea.
        /// </summary>
        private const float ThrowStrength = 0.75f;

        /// <summary>
        /// Half-angle of the spray, in degrees.
        ///
        /// Wide, because this is a round coming apart on impact rather than a jet: too
        /// narrow and the spray reads as a second projectile continuing through the
        /// target. It still opens along the shot, so the spill throws forward.
        /// </summary>
        private const float ConeDegrees = 40f;

        /// <summary>
        /// Chlorophyte's three greens, exactly as specified.
        ///
        /// Public because the leaves are not the only thing in the mod that is made of
        /// chlorophyte, and a second green typed out somewhere else is a second green
        /// that drifts. Anything that wants to match the spill reads it from here.
        /// </summary>
        public static readonly Color[] Greens =
        {
            new Color(58, 214, 47),
            new Color(85, 201, 8),
            new Color(67, 224, 75),
        };

        /// <summary>One of the three, rolled. The same roll a leaf makes for itself.</summary>
        public static Color RandomGreen => Greens[Main.rand.Next(Greens.Length)];

        private readonly Color initialColor;
        private readonly float spin;

        // Every leaf gets its own phase, rate and strength. Shared values would have the
        // whole spray sway in lockstep, which reads as one sheet of cloth rather than
        // forty separate leaves — the single most important detail in the whole effect.
        private float swayPhase;
        private readonly float swayRate;
        private readonly float swayStrength;

        /// <summary>Last tick's sway offset, so only the delta is applied.</summary>
        private float swayPrev;

        /// <summary>0 while weightless and flying clear, 1 once it is a leaf in the air.</summary>
        private float flutter;

        private readonly bool purifies;
        private int converted;
        private Point lastTile;

        public ChlorophyteSpill(Vector2 position, Vector2 velocity, int lifetime, float scale, bool purifies)
        {
            Position = position;
            Velocity = velocity;
            Lifetime = lifetime;
            Scale = scale;

            initialColor = RandomGreen;
            Color = initialColor;

            spin = Main.rand.NextFloat(-0.02f, 0.02f);

            swayPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swayRate = Main.rand.NextFloat(0.06f, 0.11f);
            swayStrength = Main.rand.NextFloat(0.7f, 1.35f);

            this.purifies = purifies;
            lastTile = new Point((int)(position.X / 16f), (int)(position.Y / 16f));
        }

        public override void Update()
        {
            // Purely a function of age — see the constant.
            flutter = Utils.GetLerpValue(FlutterStartTick, FlutterFullTick, Time, true);

            swayPhase += swayRate;

            Velocity.X *= Drag;

            // Weightless while flying clear: drag applies to the vertical too, so the
            // throw coasts and settles instead of arcing. The vertical drag fades out as
            // gravity fades in, so there is no tick where both are acting at full.
            Velocity.Y *= MathHelper.Lerp(Drag, 1f, flutter);

            float terminal = MathHelper.Lerp(MaxFall, LeafFall, flutter);
            Velocity.Y = MathHelper.Clamp(Velocity.Y + Gravity * flutter, -terminal, terminal);

            // The swing itself. Only the change since last tick is applied, so this is a
            // pure offset about the leaf's path rather than a push that accumulates.
            float swayNow = MathF.Sin(swayPhase) * SwayAmplitude * swayStrength * flutter;
            Position.X += swayNow - swayPrev;
            swayPrev = swayNow;

            Scale *= ShrinkPerTick;

            // Rocking is ADDED to the travel angle rather than lerped toward, because
            // lerping between two angles wraps badly at the +-pi seam and makes a leaf
            // spin a full turn whenever it drifts across it.
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2
                     + MathF.Sin(swayPhase) * RockRadians * flutter
                     + spin * Time;

            // Cubed completion holds the leaf visible for most of its life, then drops it
            // quickly. Lerped from initialColor every tick, never from Color: multiplying
            // the already-faded colour compounds, and over this lifetime that collapses to
            // invisible by half way.
            float fade = LifetimeCompletion * LifetimeCompletion * LifetimeCompletion;
            Color = Color.Lerp(initialColor, Color.Transparent, fade);

            TrailPurify();
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            // Preloaded by the handler at PostSetupContent — a dictionary lookup, not an
            // asset request, and it can't hand back an empty path the way Texture could.
            Texture2D texture = PADParticleHandler.GetTexture(Type);
            if (texture == null)
                return;

            // Stretch along travel direction once moving fast — the thing Dust cannot do.
            float stretch = MathHelper.Clamp(Velocity.Length() / 14f, 0f, 1f) * 1.6f;
            Vector2 drawScale = new Vector2(1f - stretch * 0.25f, 1f + stretch) * Scale;

            // Fake the third dimension: as the leaf swings it turns its face away and
            // comes back, so squeeze the width on the same phase that drives the sway.
            // Two pixels of maths, and it is the difference between a leaf tumbling and a
            // sprite sliding sideways. Never reaches zero — a leaf perfectly edge-on for a
            // frame just looks like it blinked out.
            float face = MathHelper.Lerp(1f, MathF.Abs(MathF.Cos(swayPhase)) * 0.75f + 0.25f, flutter);
            drawScale.X *= face;

            // Sample the light where the leaf is, so a spray in a dark cave stays dark.
            float brightness = (float)Math.Pow(
                Lighting.Brightness((int)(Position.X / 16f), (int)(Position.Y / 16f)), 0.4);
            brightness = MathHelper.Clamp(brightness, 0.25f, 1f);

            spriteBatch.Draw(texture, Position - Main.screenPosition, null,
                Color * brightness, Rotation, texture.Size() * 0.5f,
                drawScale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Converts the tile this leaf has just entered.
        ///
        /// Gated on actually crossing a tile boundary rather than running every tick: a
        /// leaf drifting a couple of pixels a frame would otherwise re-convert the same
        /// tile twenty times over, at twenty times the cost, for no visible difference.
        /// </summary>
        private void TrailPurify()
        {
            if (!purifies || converted >= MaxConversions)
                return;

            int tx = (int)(Position.X / 16f);
            int ty = (int)(Position.Y / 16f);

            if (tx == lastTile.X && ty == lastTile.Y)
                return;

            lastTile = new Point(tx, ty);
            converted++;

            ChlorophyteBloom.Convert(tx, ty);
        }

        /// <summary>
        /// Throws a spray forward along the shot.
        /// </summary>
        /// <param name="position">Impact point, in world coordinates.</param>
        /// <param name="direction">The round's travel direction. The cone opens along it.</param>
        /// <param name="reachTiles">How far the spray should carry, in blocks.</param>
        /// <param name="purify">
        /// True only on the client that fired the shot. Every client draws its own spray
        /// from its own RNG, so letting all of them convert would have several machines
        /// writing different tiles for one hit.
        /// </param>
        /// <param name="speedScale">
        /// Multiplies how hard the spray is thrown. The magnitude of <paramref name="direction"/>
        /// does NOT do this — it is normalised on the way in — so this is the knob.
        /// </param>
        public static void SpawnBurst(Vector2 position, Vector2 direction, float reachTiles,
            bool purify, float speedScale = 1f)
        {
            if (Main.dedServ)
                return;

            Vector2 baseDir = direction.SafeNormalize(-Vector2.UnitY);

            // Count follows reach so a 3-block spill is a spatter and a 10-block one is a
            // cloud, rather than the same handful of leaves stretched thinner.
            int count = (int)MathHelper.Clamp(reachTiles * 5f, 12f, 60f);

            for (int i = 0; i < count; i++)
            {
                float offset = MathHelper.ToRadians(Main.rand.NextFloat(-ConeDegrees, ConeDegrees));

                // Leaves near the centre of the cone travel fastest, so the spray reads as
                // a jet off the impact rather than an even fan. Blood's trick exactly.
                float centreness = 1f - Math.Abs(offset) / MathHelper.ToRadians(ConeDegrees);
                float speed = MathHelper.Lerp(1.4f, reachTiles * ThrowStrength, centreness)
                            * Main.rand.NextFloat(0.7f, 1.25f)
                            * speedScale;

                PADParticleHandler.SpawnParticle(new ChlorophyteSpill(
                    position + baseDir.RotatedBy(offset) * Main.rand.NextFloat(0f, 6f),
                    baseDir.RotatedBy(offset) * speed,
                    Main.rand.Next(110, 190),
                    Main.rand.NextFloat(0.5f, 0.95f),
                    purify && i < PurifyingLeaves));
            }
        }
    }

    /// <summary>
    /// Collects the tile conversions the spray asks for and syncs them in one go.
    ///
    /// It exists for the multiplayer case. Converting is cheap; announcing is not, and a
    /// spray can touch a few hundred tiles in a second. Sending a packet per tile would
    /// bury the connection, so every conversion in a tick widens one rectangle and a
    /// single tile square goes out at the end of it.
    /// </summary>
    public class ChlorophyteBloom : ModSystem
    {
        /// <summary>
        /// Purity in the conversion tables.
        ///
        /// A literal, not BiomeConversionID.Purity: that class arrived in 1.4.5 and does
        /// not exist in 1.4.4. The numbering matches in both, so the literal is correct
        /// now and stays correct if the mod moves forward.
        /// </summary>
        private const int ConversionPurity = 0;

        private static bool dirty;
        private static int minX, minY, maxX, maxY;

        /// <summary>
        /// Converts one tile and notes it for the next sync.
        ///
        /// WorldGen.Convert is called with size 0 on purpose. Its four-argument form looks
        /// like it converts a radius, but the inner loop is gated on a hardcoded
        ///     Math.Abs(k - i) + Math.Abs(l - j) &lt; 6
        /// so it can never reach past about five tiles however large a size is passed.
        /// Size 0 collapses that loop to the single centre tile, which is the one honest
        /// way to use it — and here the shape should come from where the leaves actually
        /// drifted, not from a radius at all.
        /// </summary>
        public static void Convert(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            WorldGen.Convert(x, y, ConversionPurity, 0);

            if (!dirty)
            {
                dirty = true;
                minX = maxX = x;
                minY = maxY = y;
                return;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        public override void PostUpdateEverything()
        {
            if (!dirty)
                return;

            dirty = false;

            if (Main.netMode == NetmodeID.SinglePlayer)
                return;

            // Converting on the firing client and announcing the result is how vanilla
            // does it too — the Clentaminator's solution converts locally and sends a
            // tile square, which the server relays.
            NetMessage.SendTileSquare(-1, minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        public override void ClearWorld()
        {
            dirty = false;
        }
    }
}
