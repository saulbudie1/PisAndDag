using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Shaders;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// Things in the Vast Ocean's water that move and are kept from tick to tick, on each player's
    /// own machine, for the looks only:
    ///
    /// BUBBLES, by request ("3, nice add this": bubbles from the player while under the water, from
    /// creatures, and columns of them rising from the dark):
    ///   - from a player under the water, a breath let out every three to four seconds or so, four
    ///     to nine bubbles of a size or two from about the mouth (none from one with Gills or a
    ///     merfolk's form, who do not hold their breath); and from one swimming fast a bubble now
    ///     and then from about them. The game has its own bubble now and then from a player under
    ///     water besides (Player.CheckDrowning); these are more;
    ///   - from a creature in the water, a bubble now and then, more when it goes fast;
    ///   - from far below, now and then, a column of them rising from the bottom of the screen for
    ///     a few seconds, from out of the dark into what light there is;
    ///   - from the seeps on the floor, gas let out of each crack a few bubbles at a time
    ///     (SeaLifeEffects);
    ///   - each rising faster the bigger it is, wobbling, and bursting where it meets the surface
    ///     (the big ones with a ring in the game's water) or the underside of anything solid.
    ///   They are the game's own pictures of its bubbles (its breath bubble, Dust 34, and its two
    ///   bubbles Gore 411 and 412), in the light of the water where they are (the sea's own colour
    ///   at that depth, by day and night, VastOceanSky) or what other light is on them, whichever is
    ///   brighter: so they come out of the black as they rise into the blue.
    ///
    /// THE WAKE, by request ("5, excellent": a glowing wake of plankton by night): where it is
    /// dark in the water (by night near the surface, and in the dark of the deep at any hour, to
    /// the end of the 6th section), a player or a creature moving through it leaves a trail of
    /// small lights, blue and green, flickering and going out over a second or two behind it.
    /// Not where there is light enough to drown it out.
    /// </summary>
    public class VastOceanMotes : ModSystem
    {
        private const int MaxBubbles = 400;
        private const int MaxSparks = 700;

        /// <summary>A bubble: where, how fast, what picture (0 the breath bubble small, 1 big, 2 the small Gore, 3 the big), its scale, its wobble, how long it has been, and the ticks left of its bursting.</summary>
        private struct Bubble
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public byte Kind;
            public float Scale;
            public float Wobble;
            public int Age;
            public int Burst;
        }

        /// <summary>A light in the wake: where, drifting how, how long it has been and will be, its size, its colour (blue to green) and its flicker.</summary>
        private struct Spark
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public int Age;
            public int Life;
            public float Size;
            public float Hue;
            public float Flicker;
        }

        /// <summary>A column rising from the dark: at this column of the world, for so many ticks more, so many bubbles a tick.</summary>
        private struct Column
        {
            public float X;
            public int Left;
            public float Rate;
        }

        private static readonly Bubble[] bubbles = new Bubble[MaxBubbles];
        private static readonly Spark[] sparks = new Spark[MaxSparks];
        private static readonly Column[] columns = new Column[3];
        private static int bubbleCount;
        private static int sparkCount;

        /// <summary>When each player next breathes out (ticks of the game).</summary>
        private static readonly uint[] nextBreath = new uint[Main.maxPlayers];
        private static uint nextColumn;

        private static Asset<Texture2D> glow;

        /// <summary>A bubble's top speed up, in pixels a tick, by its picture: the bigger the faster.</summary>
        private static readonly float[] Rise = { 1.1f, 1.5f, 1.9f, 2.8f };

        public override void Load()
        {
            if (!Main.dedServ)
                glow = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");
        }

        public override void Unload()
        {
            glow = null;
        }

        public override void OnWorldUnload() => Clear();

        private static void Clear()
        {
            bubbleCount = 0;
            sparkCount = 0;
            Array.Clear(columns);
            Array.Clear(nextBreath);
            nextColumn = 0;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (SubworldKeeper.Current is not VastOcean)
            {
                if (bubbleCount > 0 || sparkCount > 0)
                    Clear();

                return;
            }

            Move();
            FromPlayers();
            FromCreatures();
            FromTheDark();
        }

        // ---- moving --------------------------------------------------------------------------------

        private static void Move()
        {
            WaterShaderData water = Filters.Scene["WaterDistortion"]?.GetShader() as WaterShaderData;

            for (int i = bubbleCount - 1; i >= 0; i--)
            {
                ref Bubble b = ref bubbles[i];

                b.Age++;

                if (b.Burst > 0)
                {
                    if (--b.Burst == 0)
                        bubbles[i] = bubbles[--bubbleCount];

                    continue;
                }

                // Up, faster and faster to its own speed; wobbling side to side as it goes.
                float top = -Rise[b.Kind] * b.Scale;

                b.Velocity.Y += (top - b.Velocity.Y) * 0.06f;
                b.Velocity.X *= 0.94f;
                b.Position += b.Velocity;
                b.Position.X += (float)Math.Sin(b.Age * 0.16f + b.Wobble) * 0.25f * (1f + b.Kind * 0.4f);

                int x = (int)(b.Position.X / 16f);
                int y = (int)(b.Position.Y / 16f);

                if (!WorldGen.InWorld(x, y, 2) || b.Age > 900)
                {
                    b.Burst = 1;
                    continue;
                }

                Tile tile = Main.tile[x, y];

                // Against the underside of something solid, or out of the water: burst. In a tile of
                // water not full, the water's top is that much down the tile.
                bool solid = tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
                bool inWater = tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water
                    && b.Position.Y >= y * 16f + (255 - tile.LiquidAmount) / 255f * 16f;

                if (solid || !inWater)
                {
                    b.Burst = 6;

                    if (!solid && b.Kind >= 2 && water != null)
                        water.QueueRipple(b.Position, 0.15f + 0.1f * b.Kind, new Vector2(12f + 8f * b.Kind, 8f), RippleShape.Circle, 0f);
                }
            }

            for (int i = sparkCount - 1; i >= 0; i--)
            {
                ref Spark s = ref sparks[i];

                if (++s.Age >= s.Life)
                {
                    sparks[i] = sparks[--sparkCount];
                    continue;
                }

                s.Position += s.Velocity;
                s.Velocity *= 0.95f;
            }
        }

        // ---- where they come from ------------------------------------------------------------------

        private static void FromPlayers()
        {
            uint now = Main.GameUpdateCount;
            Vector2 near = Main.LocalPlayer.Center;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];

                if (!player.active || player.dead || !InWater(player) || Vector2.DistanceSquared(player.Center, near) > 2600f * 2600f)
                {
                    nextBreath[p] = 0;
                    continue;
                }

                // A breath let out: four to nine bubbles from about the mouth.
                if (nextBreath[p] == 0)
                {
                    nextBreath[p] = now + (uint)Main.rand.Next(60, 180);
                }
                else if (now >= nextBreath[p])
                {
                    nextBreath[p] = now + (uint)Main.rand.Next(155, 255);

                    if (!player.gills && !player.merman)
                    {
                        Vector2 mouth = new Vector2(player.Center.X + 6f * player.direction, player.gravDir > 0f ? player.position.Y + 8f : player.position.Y + player.height - 8f);
                        int count = Main.rand.Next(4, 10);

                        for (int k = 0; k < count; k++)
                        {
                            Vector2 push = new Vector2(player.direction * Main.rand.NextFloat(0.3f, 1.1f), -Main.rand.NextFloat(0.1f, 0.8f)) + player.velocity * 0.3f;

                            AddBubble(mouth + Main.rand.NextVector2Circular(4f, 3f), push, (byte)(Main.rand.NextFloat() < 0.75f ? Main.rand.Next(2) : 2));
                        }
                    }
                }

                float speed = player.velocity.Length();

                // Swimming fast: a bubble now and then from about them.
                if (speed > 3f && Main.rand.NextFloat() < Math.Min(0.35f, 0.05f * (speed - 3f)))
                    AddBubble(Main.rand.NextVector2FromRectangle(player.Hitbox), -player.velocity * 0.15f, (byte)Main.rand.Next(2));

                Wake(player.Hitbox, player.velocity, 0.5f, 4);
            }
        }

        private static void FromCreatures()
        {
            Rectangle seen = new Rectangle((int)Main.screenPosition.X - 600, (int)Main.screenPosition.Y - 600, Main.screenWidth + 1200, Main.screenHeight + 1200);

            for (int n = 0; n < Main.maxNPCs; n++)
            {
                NPC npc = Main.npc[n];

                if (!npc.active || !npc.wet || npc.lavaWet || npc.honeyWet || npc.shimmerWet || !seen.Intersects(npc.Hitbox))
                    continue;

                float speed = npc.velocity.Length();

                // A bubble now and then, more the faster it goes; bigger from a bigger creature.
                if (Main.rand.NextFloat() < 0.01f + 0.015f * speed)
                {
                    byte kind = (byte)(npc.width > 60 && Main.rand.NextBool(3) ? 3 : npc.width > 30 ? Main.rand.Next(1, 3) : Main.rand.Next(2));

                    AddBubble(new Vector2(npc.Center.X + Main.rand.NextFloat(-0.3f, 0.3f) * npc.width, npc.position.Y + npc.height * 0.3f), npc.velocity * 0.2f, kind);
                }

                Wake(npc.Hitbox, npc.velocity, 0.3f, 3);
            }
        }

        /// <summary>
        /// Now and then (every ten to twenty eight seconds), while the screen shows the water half a
        /// section down or more, a column of bubbles rising from the bottom of the screen for three to
        /// eight seconds, six to twelve a second, mostly small; never more than three at once, none
        /// under the 7th section, none where the bottom of the screen is not water.
        /// </summary>
        private static void FromTheDark()
        {
            float bottomRow = (Main.screenPosition.Y + Main.screenHeight) / 16f;
            float rows = bottomRow - VastOcean.Surface;
            uint now = Main.GameUpdateCount;

            if (nextColumn == 0)
                nextColumn = now + (uint)Main.rand.Next(300, 900);

            if (now >= nextColumn)
            {
                nextColumn = now + (uint)Main.rand.Next(600, 1680);

                if (rows > VastOcean.SectionRows * 0.5f && bottomRow < VastOcean.QuietFrom)
                {
                    for (int c = 0; c < columns.Length; c++)
                    {
                        if (columns[c].Left > 0)
                            continue;

                        columns[c] = new Column
                        {
                            X = Main.screenPosition.X + Main.rand.NextFloat(0.1f, 0.9f) * Main.screenWidth,
                            Left = Main.rand.Next(180, 480),
                            Rate = Main.rand.NextFloat(0.1f, 0.2f),
                        };
                        break;
                    }
                }
            }

            float from = Main.screenPosition.Y + Main.screenHeight + 24f;

            for (int c = 0; c < columns.Length; c++)
            {
                if (columns[c].Left <= 0)
                    continue;

                columns[c].Left--;

                if (Main.rand.NextFloat() >= columns[c].Rate)
                    continue;

                Vector2 at = new Vector2(columns[c].X + Main.rand.NextFloat(-6f, 6f), from);
                Tile tile = Framing(at);

                if (tile.LiquidAmount < 200 || tile.LiquidType != LiquidID.Water)
                    continue;

                float roll = Main.rand.NextFloat();
                byte kind = (byte)(roll < 0.45f ? 0 : roll < 0.8f ? 1 : roll < 0.96f ? 2 : 3);

                AddBubble(at, new Vector2(0f, -Rise[kind]), kind);
            }
        }

        private static Tile Framing(Vector2 at)
        {
            int x = Utils.Clamp((int)(at.X / 16f), 0, Main.maxTilesX - 1);
            int y = Utils.Clamp((int)(at.Y / 16f), 0, Main.maxTilesY - 1);

            return Main.tile[x, y];
        }

        private static bool InWater(Entity entity) => entity.wet && !entity.lavaWet && !entity.honeyWet && !entity.shimmerWet;

        /// <summary>
        /// A bubble of gas let out by the floor here (a seep's crack, SeaLifeEffects): small mostly,
        /// now and then a bigger one, starting slow and rising as the others do.
        /// </summary>
        internal static void Seep(Vector2 at)
        {
            if (SubworldKeeper.Current is not VastOcean)
                return;

            float roll = Main.rand.NextFloat();
            byte kind = (byte)(roll < 0.55f ? 0 : roll < 0.9f ? 1 : 2);

            AddBubble(at + Main.rand.NextVector2Circular(3f, 1f), new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), -Rise[kind] * 0.4f), kind);
        }

        private static void AddBubble(Vector2 at, Vector2 velocity, byte kind)
        {
            if (bubbleCount >= MaxBubbles)
                return;

            bubbles[bubbleCount++] = new Bubble
            {
                Position = at,
                Velocity = velocity,
                Kind = kind,
                Scale = kind == 3 ? Main.rand.NextFloat(0.6f, 1f) : Main.rand.NextFloat(0.8f, 1.25f),
                Wobble = Main.rand.NextFloat(MathHelper.TwoPi),
            };
        }

        /// <summary>
        /// The wake: from a thing moving through dark water, lights about it, more the faster it
        /// goes (this many a tick for each pixel a tick of its speed, up to so many), drifting back
        /// from it and going out over one to two seconds.
        /// </summary>
        private static void Wake(Rectangle body, Vector2 velocity, float perSpeed, int most)
        {
            float speed = velocity.Length();

            if (speed < 1f)
                return;

            float dark = Dark(body.Center.ToVector2());

            if (dark < 0.05f)
                return;

            float want = Math.Min(most, speed * perSpeed) * dark;
            int count = (int)want + (Main.rand.NextFloat() < want - (int)want ? 1 : 0);

            for (int k = 0; k < count && sparkCount < MaxSparks; k++)
            {
                sparks[sparkCount++] = new Spark
                {
                    Position = Main.rand.NextVector2FromRectangle(body),
                    Velocity = -velocity * 0.08f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    Life = Main.rand.Next(50, 110),
                    Size = Main.rand.NextFloat(1.5f, 3.5f),
                    Hue = Main.rand.NextFloat(),
                    Flicker = Main.rand.NextFloat(MathHelper.TwoPi),
                };
            }
        }

        /// <summary>
        /// How dark the water is here for a glow to be seen, 0 to 1: by the light on it (the game's,
        /// and whatever lights it), and none at all from the end of the 6th section down, nor over
        /// the water.
        /// </summary>
        private static float Dark(Vector2 at)
        {
            float rows = at.Y / 16f - VastOcean.Surface;
            float black = VastOcean.BlackAt - VastOcean.Surface;

            if (rows < 1f || rows > black)
                return 0f;

            Vector3 light = Lighting.GetColor((int)(at.X / 16f), (int)(at.Y / 16f)).ToVector3();
            float brightest = Math.Max(light.X, Math.Max(light.Y, light.Z));

            return (1f - Smooth(0.08f, 0.35f, brightest)) * (1f - Smooth(black - VastOcean.SectionRows * 0.5f, black, rows));
        }

        // ---- drawing (VastOceanSky's layer in front, in the Vast Ocean) ----------------------------------

        /// <summary>The wake's lights, added as light (VastOceanSky's batch in front: drawn as light, sampled smoothly).</summary>
        internal static void DrawGlow(SpriteBatch batch)
        {
            if (sparkCount == 0 || glow == null || !glow.IsLoaded)
                return;

            Texture2D dot = glow.Value;
            Vector2 origin = dot.Size() * 0.5f;
            float t = Main.GlobalTimeWrappedHourly;
            Rectangle screen = new Rectangle((int)Main.screenPosition.X - 16, (int)Main.screenPosition.Y - 16, Main.screenWidth + 32, Main.screenHeight + 32);

            for (int i = 0; i < sparkCount; i++)
            {
                ref Spark s = ref sparks[i];

                if (!screen.Contains((int)s.Position.X, (int)s.Position.Y))
                    continue;

                float life = s.Age / (float)s.Life;
                float strength = Smooth(0f, 0.08f, life) * (1f - Smooth(0.35f, 1f, life)) * (0.65f + 0.35f * (float)Math.Sin(t * 9f + s.Flicker));
                Color hue = Color.Lerp(new Color(80, 205, 255), new Color(110, 255, 190), s.Hue);
                Vector2 at = s.Position - Main.screenPosition;

                // A small bright point, and a soft halo about it.
                batch.Draw(dot, at, null, Lit(hue, 0.9f * strength), 0f, origin, s.Size / dot.Width, SpriteEffects.None, 0f);
                batch.Draw(dot, at, null, Lit(hue, 0.25f * strength), 0f, origin, s.Size * 3.5f / dot.Width, SpriteEffects.None, 0f);
            }
        }

        /// <summary>The bubbles, the game's own pictures of them, in the water's light where each is (VastOceanSky's batch for them: laid over, pixel for pixel).</summary>
        internal static void DrawBubbles(SpriteBatch batch)
        {
            if (bubbleCount == 0)
                return;

            Main.instance.LoadGore(411);
            Main.instance.LoadGore(412);

            Texture2D dust = TextureAssets.Dust.Value;
            Texture2D small = TextureAssets.Gore[411].Value;
            Texture2D big = TextureAssets.Gore[412].Value;
            Rectangle screen = new Rectangle((int)Main.screenPosition.X - 24, (int)Main.screenPosition.Y - 24, Main.screenWidth + 48, Main.screenHeight + 48);

            for (int i = 0; i < bubbleCount; i++)
            {
                ref Bubble b = ref bubbles[i];

                if (!screen.Contains((int)b.Position.X, (int)b.Position.Y))
                    continue;

                Texture2D texture;
                Rectangle frame;

                switch (b.Kind)
                {
                    // The game's breath bubble (Dust 34): its small frame, or one of its two round ones.
                    case 0:
                        texture = dust;
                        frame = new Rectangle(340, 0, 10, 10);
                        break;

                    case 1:
                        texture = dust;
                        frame = new Rectangle(340, (b.Wobble > MathHelper.Pi ? 10 : 20), 10, 10);
                        break;

                    case 2:
                        texture = small;
                        frame = small.Bounds;
                        break;

                    default:
                        texture = big;
                        frame = big.Bounds;
                        break;
                }

                // In the water's own light where it is, or whatever else lights it, whichever is brighter.
                float rows = b.Position.Y / 16f - VastOcean.Surface;
                Vector3 sea = VastOceanSky.SeaGlow(rows) * 2.2f;
                Vector3 lit = Vector3.Max(sea, Lighting.GetColor((int)(b.Position.X / 16f), (int)(b.Position.Y / 16f)).ToVector3());
                float opacity = 0.9f * Smooth(0f, 6f, b.Age);
                float scale = b.Scale;

                if (b.Burst > 0)
                {
                    // Bursting: a little bigger and gone over six ticks.
                    float through = 1f - b.Burst / 6f;

                    scale *= 1f + 0.5f * through;
                    opacity *= 1f - through;
                }

                Color colour = new Color(Math.Min(lit.X, 1f), Math.Min(lit.Y, 1f), Math.Min(lit.Z, 1f)) * opacity;

                batch.Draw(texture, b.Position - Main.screenPosition, frame, colour, 0f, frame.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private static Color Lit(Color colour, float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)0);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }
}
