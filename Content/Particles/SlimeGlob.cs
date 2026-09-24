using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A glob of slime: thrown, it falls, stretches along the way it is going, and
    /// splats flat where it hits the ground; told to go home, it is pulled back into
    /// an NPC or a point and gone the moment it arrives. The King's arrival throws them
    /// off the pile as it goes in, off him as he comes out of it (and pulls every one
    /// back), and off the top of him when he roars.
    ///
    /// Drawn in the alpha pass, lit by the world like any slime, a little see-through.
    /// The sprite is grey so the tint is the colour.
    /// </summary>
    public class SlimeGlob : PADParticle
    {
        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        /// <summary>The King's blue, a shade lighter so a glob reads against him.</summary>
        public static readonly Color Blue = new Color(80, 150, 255);

        /// <summary>How much of what is behind shows through.</summary>
        private const float Opacity = 0.85f;

        /// <summary>Ticks a splat stays on the ground before it is gone.</summary>
        private const int SplatTicks = 24;

        private readonly Color tint;
        private readonly bool falls;

        private int homeAt = -1;
        private int homeNpc = -1;
        private int homeType;
        private Vector2 homePoint;

        private bool stuck;
        private int stuckAt;

        /// <param name="scale">1 is the sprite's own size, twelve pixels across.</param>
        /// <param name="falls">False for a glob that drifts to a stop rather than falling: one being pulled in.</param>
        public SlimeGlob(Vector2 position, Vector2 velocity, float scale, int lifetime, Color tint, bool falls = true)
        {
            Position = position;
            Velocity = velocity;
            Scale = scale;
            Lifetime = Math.Max(2, lifetime);
            Color = tint;

            this.tint = tint;
            this.falls = falls;

            Rotation = velocity.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>After this many ticks it is pulled back into this NPC, and is gone when it gets there.</summary>
        public SlimeGlob HomeTo(NPC npc, int after)
        {
            homeNpc = npc.whoAmI;
            homeType = npc.type;
            homeAt = Math.Max(0, after);
            Lifetime = Math.Max(Lifetime, homeAt + 90);
            return this;
        }

        /// <summary>After this many ticks it is pulled back to this point, and is gone when it gets there.</summary>
        public SlimeGlob HomeTo(Vector2 point, int after)
        {
            homeNpc = -1;
            homePoint = point;
            homeAt = Math.Max(0, after);
            Lifetime = Math.Max(Lifetime, homeAt + 90);
            return this;
        }

        private bool Home(out Vector2 target)
        {
            target = homePoint;

            if (homeAt < 0 || Time < homeAt)
                return false;

            if (homeNpc < 0)
                return true;

            NPC npc = Main.npc[homeNpc];

            if (!npc.active || npc.type != homeType)
            {
                // What it was going back to is gone. It falls instead.
                homeAt = -1;
                return false;
            }

            target = npc.Center;
            return true;
        }

        public override void Update()
        {
            if (Home(out Vector2 target))
            {
                stuck = false;

                Vector2 to = target - Position;
                float distance = to.Length();

                if (distance < 6f + 5f * Scale)
                {
                    Kill();
                    return;
                }

                // Pulled harder the longer it has been going, so nothing orbits.
                float speed = 5f + (Time - homeAt) * 0.7f;
                Velocity = Vector2.Lerp(Velocity, to / distance * Math.Min(distance, speed), 0.22f);
            }
            else if (stuck)
            {
                Velocity = Vector2.Zero;

                if (Time - stuckAt >= SplatTicks)
                {
                    Kill();
                    return;
                }
            }
            else if (falls)
            {
                Velocity.X *= 0.985f;
                Velocity.Y = Math.Min(Velocity.Y + 0.32f, 14f);

                // Where it comes down on something solid, it stays, flat.
                if (Velocity.Y > 0f && Solid(Position + new Vector2(0f, 3f)))
                {
                    stuck = true;
                    stuckAt = Time;
                    Velocity = Vector2.Zero;
                }
            }
            else
            {
                Velocity *= 0.93f;
            }

            if (!stuck && Velocity.LengthSquared() > 0.01f)
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        private static bool Solid(Vector2 at)
        {
            Tile tile = Framing.GetTileSafely((int)(at.X / 16f), (int)(at.Y / 16f));

            return tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = PADParticleHandler.GetTexture(Type);

            if (texture == null)
                return;

            float fade;
            Vector2 scale;
            float rotation;

            if (stuck)
            {
                fade = 1f - (Time - stuckAt) / (float)SplatTicks;
                scale = new Vector2(1.4f, 0.55f) * Scale;
                rotation = 0f;
            }
            else
            {
                // Drawn out along the way it is going, the faster the longer.
                float stretch = MathHelper.Clamp(Velocity.Length() / 12f, 0f, 1f) * 0.9f;

                fade = 1f - Math.Max(0f, (LifetimeCompletion - 0.8f) / 0.2f);
                scale = new Vector2(1f - stretch * 0.35f, 1f + stretch) * Scale;
                rotation = Rotation;
            }

            if (fade <= 0f)
                return;

            // Lit by where it is, as slime is, and never quite black.
            float light = (float)Math.Sqrt(Lighting.Brightness((int)(Position.X / 16f), (int)(Position.Y / 16f)));
            light = MathHelper.Clamp(light, 0.45f, 1f);

            Color lit = new Color((int)(tint.R * light), (int)(tint.G * light), (int)(tint.B * light));

            spriteBatch.Draw(texture, Position - Main.screenPosition, null, lit * (Opacity * fade), rotation,
                texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
