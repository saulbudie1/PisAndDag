using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A splinter of light thrown outward, thinning and lengthening as it slows.
    ///
    /// Calamity's GlowSparkParticle, ported, and named for what it is rather than where
    /// it came from: this is the generic splinter, and anything wanting shards thrown
    /// off a point should reach for it.
    ///
    /// **It is measured in world pixels, not in scale.** That is the one place this
    /// departs from the original, and it departs on purpose. A scale multiplier means
    /// nothing on its own — it is a number against a sprite nobody has in front of them
    /// — and the first version of this file asked for 0.95 against a 128px sprite that
    /// then stretched 1.2x every tick, which put six-hundred-pixel rays across the
    /// screen. Nobody could have read that off the call. Length and Width are in the
    /// same units as Player.height, so a caller can say "two thirds of a player" and get
    /// two thirds of a player.
    ///
    /// The rest is Calamity's and is worth keeping straight, because the shape degrades
    /// into ordinary dust if any of it is dropped:
    ///
    ///   **It points where it is going.** Rotation is read off Velocity every tick, not
    ///   set once at spawn, so a shard that is slowed or pulled by gravity turns as it
    ///   goes instead of sliding sideways. The sprite runs down its own +Y, so the angle
    ///   is the velocity's plus a quarter turn.
    ///
    ///   **Length and width move apart.** Growing one while shrinking the other is what
    ///   reads as the thing being drawn out to a thread — the silhouette a spark leaves
    ///   on a long exposure. Scaling both together only makes it bigger.
    ///
    ///   **It fades on a cube.** A linear fade reads as a light being turned down. Cubed,
    ///   it holds its colour almost to the end and then goes, which reads as leaving.
    ///
    /// Additive, so a handful overlapping build a hot centre on their own. The second
    /// draw pass is the same sprite narrowed and in white: the colour is the shard, the
    /// white down the middle is the light coming off it.
    /// </summary>
    public class SparkShard : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>Drawn length, in world pixels. Grows toward <see cref="maxLength"/>.</summary>
        private float length;

        /// <summary>Drawn width, in world pixels.</summary>
        private float width;

        /// <summary>The ceiling the length grows to and stops at.</summary>
        private readonly float maxLength;

        /// <summary>The colour it was born with. The fade is measured from this, not from last frame.</summary>
        private readonly Color born;

        private readonly bool falls;
        private readonly bool spine;

        /// <summary>How hard it thins and lengthens per tick. 0 leaves the shape alone.</summary>
        private readonly float drawOut;

        public SparkShard(Vector2 position, Vector2 velocity, Color colour,
            float length, float width, float maxLength, int lifetime,
            bool falls = false, bool spine = true, float drawOut = 1f)
        {
            Position = position;
            Velocity = velocity;
            Color = born = colour;
            Scale = 1f;
            Lifetime = lifetime;

            this.length = Math.Max(1f, length);
            this.width = Math.Max(0.5f, width);

            // A ceiling below the starting length would shrink it on its first tick,
            // which is not what any caller passing a smaller number could have meant.
            this.maxLength = Math.Max(maxLength, this.length);

            this.falls = falls;
            this.spine = spine;
            this.drawOut = drawOut;

            Rotation = velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void Update()
        {
            Velocity *= 0.95f;

            Color = Color.Lerp(born, Color.Transparent, (float)Math.Pow(LifetimeCompletion, 3D));

            if (drawOut > 0f)
            {
                width *= 1f - 0.2f * drawOut;
                length = Math.Min(length * (1f + 0.2f * drawOut), maxLength);
            }

            // Once it stops growing it starts going, so the tail does not sit at full
            // length for the second half of its life.
            if (length >= maxLength)
                length *= 0.94f;

            // Only once it is slow enough for a fall to read as a fall. One still
            // travelling at speed would just bend, and a whole spray would sag.
            if (falls && Velocity.Length() < 12f)
            {
                Velocity.X *= 0.94f;
                Velocity.Y += 0.25f;
            }

            // Velocity decays every tick, and a zero vector has no direction — one that
            // ran out of speed would snap to pointing right on its last frame. Keep the
            // last angle it had instead.
            if (Velocity.LengthSquared() > 0.0001f)
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture is null || texture.Width == 0 || texture.Height == 0)
                return;

            // Pixels to sprite scale, here rather than at spawn, because the texture is
            // not loaded yet when the constructor runs.
            Vector2 size = new Vector2(width / texture.Width, length / texture.Height);

            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;

            spriteBatch.Draw(texture, drawPosition, null, Color, Rotation, origin, size, SpriteEffects.None, 0f);

            if (!spine)
                return;

            Color core = Color.Lerp(Color.White, Color.Transparent, (float)Math.Pow(LifetimeCompletion, 3D));

            spriteBatch.Draw(texture, drawPosition, null, core, Rotation, origin,
                size * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// A fan of them off one point, which is how this is nearly always wanted.
        ///
        /// <paramref name="maxLength"/> is the headline number and everything else is
        /// sized off it, so a caller only has to decide how long the longest ray is
        /// allowed to get and the burst stays in proportion at any size.
        ///
        /// Angles are rolled rather than stepped. Evenly spaced shards read as a wheel
        /// and the eye finds the pattern at once; rolled ones bunch and thin, which is
        /// what makes it look thrown rather than drawn.
        /// </summary>
        /// <param name="palette">
        /// When given, each shard rolls its own colour out of this instead of all of them
        /// sharing <paramref name="colour"/> — which is what stops a spray of a dozen
        /// reading as one flat shape.
        /// </param>
        public static void Spray(Vector2 position, Color colour, int count, float speed, float maxLength, int lifetime,
            bool falls = false, Color[] palette = null)
        {
            if (Main.dedServ)
                return;

            bool rolled = palette != null && palette.Length > 0;

            // Half-grown at birth, so there is somewhere for the draw-out to go.
            float born = maxLength * 0.5f;

            // Twelve times the hairline this started at.
            //
            // The number looks absurd written down and it is not: the sprite is a
            // spindle whose alpha falls off as (1 - d)^1.6 from its own centre line, so
            // most of its 32px canvas is nearly transparent and the part that actually
            // reads as solid is a fraction of whatever width it is drawn at. Asking for
            // eight pixels of sprite gets about two pixels of spark. This is the width
            // of the CANVAS, not of the light in it.
            //
            // At a full streak that canvas is now a little wider than the ray is long.
            // The per-tick thinning below takes it back to a thread within a few frames,
            // so what this really sets is how hard the first moment lands.
            float width = Math.Max(18f, maxLength * 1.08f);

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f)
                    * speed * Main.rand.NextFloat(0.5f, 1.2f);

                PADParticleHandler.SpawnParticle(new SparkShard(
                    position + Main.rand.NextVector2Circular(4f, 4f),
                    velocity,
                    rolled ? palette[Main.rand.Next(palette.Length)] : colour,
                    born * Main.rand.NextFloat(0.7f, 1.1f),
                    width * Main.rand.NextFloat(0.8f, 1.2f),
                    maxLength * Main.rand.NextFloat(0.65f, 1f),
                    lifetime + Main.rand.Next(-4, 5),
                    falls: falls));
            }
        }
    }
}
