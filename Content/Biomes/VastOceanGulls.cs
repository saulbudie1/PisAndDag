using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using PisAndDag.Content.Tiles.Lighthouse;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// Gulls over the Vast Ocean, by request ("for ambiance: need more seagulls to fly in patches
    /// prob patches of 5- 13 across the sea or at the light house"): flocks of five to thirteen,
    /// for the looks only, on each player's own machine (nothing of them is sent, and nothing can
    /// hit them or catch them), in the game's own picture of a gull on the wing (its ambient sky's,
    /// Images/Backgrounds/Ambience/Seagull: nine pictures of a wingbeat, the one the game draws its
    /// flocks far off over the sea with), lit as the game lights whatever is where they are, and
    /// never darker than the sun's light (they are out in it, in front of a wall or not). Near,
    /// the picture is drawn half as big again, three pixels to each of its own: about the size of
    /// the game's gulls on its beaches; far, as it is.
    ///
    /// ACROSS THE SEA: now and then (six to twenty-one seconds apart while the sky over the water
    /// is on the screen, three flocks at a time at most), a flock comes in from off one side of
    /// the screen, goes across it and off the other: mostly with the wind, as the game's own
    /// flocks go, now and then against it, slower; anywhere from three tiles over the water to
    /// well over the lighthouse, within what is on the screen. A loose patch, each gull drifting
    /// about its place in it, beating its wings and gliding by turns, the whole flock rising and
    /// falling a little as it goes. Six flocks in ten near, four far off (smaller and slower). All
    /// drawn behind the world's tiles and walls: they pass behind the platform, the lighthouse and
    /// its island, and the player.
    ///
    /// AT THE LIGHTHOUSE: while the player is within 320 columns of it (and not down in the sea),
    /// a flock wheels round the tower somewhere from over its dome down to its middle storeys: a
    /// loose patch going round together, 26 to 40 tiles out, its circle tilted a little, rising
    /// and falling, mostly soaring, a few wingbeats now and then. On the near half of the circle
    /// a gull is drawn in front of the tower, on the far half behind it, going from one to the
    /// other only where nothing of the world is about it, so that none of them is seen to jump in
    /// front of anything or behind it. Now and then a second flock wheels at another height.
    /// After a minute and a half to four minutes a flock breaks off and goes away over the sea, and
    /// in a while another comes. One that gathers while the lighthouse is on the screen flies in
    /// from off the side of the screen it is on; one that gathers out of sight is there.
    ///
    /// WHEN: as the game has its gulls (its flocks in the sky behind, AmbienceServer.IsSunnyDay;
    /// its beach gulls cry by day only): by day, not in the rain, not in an eclipse, and fewer in
    /// the first and last hour of the day. When the day ends or the rain comes, the flocks at the
    /// lighthouse go away over the sea and no more come. None is ever taken away where it can be
    /// seen.
    ///
    /// THEIR CRY: the game's own gull (SoundID.Seagull, its beach gulls' cry) from one on or near
    /// the screen now and then: one gull in 3600 a tick (the game's beach gulls, one in 1200), at
    /// most one cry every half second; about one in six seconds with ten gulls in sight.
    /// </summary>
    public class VastOceanGulls : ModSystem
    {
        /// <summary>The game's picture: nine of a wingbeat one under another, each 28 by 30 (its file is 28 by 270), the gull facing right; two of the file's pixels to each of the picture's own.</summary>
        private const int FrameWide = 28, FrameHigh = 30, Frames = 9;

        /// <summary>The pictures a gull holds to glide: its wings out level, or lifted a little (soaring).</summary>
        private const int Level = 7, Lifted = 2;

        /// <summary>How big a gull is drawn near (three pixels to each of the picture's own, as even as two) and far.</summary>
        private const float Near = 1.5f, Far = 1f;

        private const int MostFlocks = 6, MostBirds = 80, MostCrossing = 3, MostWheels = 2;

        /// <summary>How many gulls a flock is: five to thirteen.</summary>
        private const int Fewest = 5, Most = 13;

        /// <summary>A flock crossing the sea keeps between this many tiles over the water and this many, all of it.</summary>
        private const float LowestTiles = 3f, HighestTiles = 170f;

        /// <summary>The least room between two gulls' places in a patch, in pixels, drawn as the picture is.</summary>
        private const float Apart = 24f;

        /// <summary>How near the player must be to the lighthouse, in columns, for its flocks to wheel there.</summary>
        private const float WheelNear = 320f;

        /// <summary>How a gull keeps to its place: the part of the way to it made up in a tick, and the most its speed changes in one (pixels a tick).</summary>
        private const float Pull = 0.025f, MostTurn = 0.12f;

        /// <summary>One gull in this many cries in a tick; and the fewest ticks from one cry to the next.</summary>
        private const int CryOdds = 3600, CryApart = 30;

        /// <summary>One hour of the game's clock, in its ticks.</summary>
        private const double Hour = 3600.0;

        private enum Way : byte
        {
            /// <summary>Going across the sea: a patch about a middle that moves.</summary>
            Crossing,
            /// <summary>Wheeling round the lighthouse: a patch going round a circle about the tower.</summary>
            Wheeling,
            /// <summary>Broken off from the lighthouse, going away over the sea as a patch.</summary>
            Leaving,
        }

        private struct Flock
        {
            public bool Alive;
            public Way Way;

            /// <summary>The middle of the patch (going), or of its circle (wheeling), in the world's pixels.</summary>
            public Vector2 Centre;

            /// <summary>Going: how the patch goes, pixels a tick; the height it keeps, and where its rising and falling is.</summary>
            public Vector2 Drift;
            public float Cruise, Phase;

            /// <summary>Wheeling: where round its circle the patch is and how fast it goes round (radians a tick, the sign the way round), half the circle's width and its tilt (pixels), and its rising and falling.</summary>
            public float Angle, Turn, Across, Tilt, Bob, BobRate, BobPhase;

            /// <summary>Ticks since it came, or since it broke off; and how long it wheels before it goes.</summary>
            public int Age, Life;
        }

        private struct Bird
        {
            public bool Alive;
            public int Flock;
            public Vector2 At, Velocity;

            /// <summary>Its place in the patch from the middle (wheeling: along the circle and up), and its drifting about that place.</summary>
            public Vector2 Place;
            public float WanderX, WanderY, RateX, RateY, PhaseX, PhaseY;

            /// <summary>Its wings: the picture, the ticks on it and to a picture, the wingbeats left before it glides, the ticks left gliding, and the picture it glides on.</summary>
            public int Frame, Tick, Beat, Flaps, Hold, Rest;

            /// <summary>Which way it faces: 1 right, as the picture is, -1 left.</summary>
            public sbyte Face;

            /// <summary>Drawn in front of the world's tiles (else behind them), and which it should be.</summary>
            public bool Front, WantFront;

            /// <summary>How big it is drawn (<see cref="Near"/> or <see cref="Far"/>), and how much of the light where it is it shows (a little less on the far side of a circle).</summary>
            public float Scale, Shade;
        }

        private static readonly Flock[] flocks = new Flock[MostFlocks];
        private static readonly Bird[] birds = new Bird[MostBirds];
        private static readonly Vector2[] placed = new Vector2[Most];
        private static int birdCount;
        private static bool here;
        private static uint nextCrossing, nextWheel, nextCry;
        private static Asset<Texture2D> gull;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            gull = ModContent.Request<Texture2D>("Terraria/Images/Backgrounds/Ambience/Seagull");
            On_OverlayManager.Draw += DrawBehind;
        }

        public override void Unload()
        {
            gull = null;
            Clear();
        }

        public override void OnWorldUnload() => Clear();

        private static void Clear()
        {
            Array.Clear(flocks);
            Array.Clear(birds);
            birdCount = 0;
            here = false;
            nextCrossing = nextWheel = nextCry = 0;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (SubworldKeeper.Current is not VastOcean)
            {
                if (here)
                    Clear();

                return;
            }

            here = true;
            Tick();
        }

        // ---- every tick ------------------------------------------------------------------------

        /// <summary>The screen, in the world's pixels (zoomed in, it shows less, never more).</summary>
        private static Rectangle View() => new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);

        /// <summary>The game's sunny day, as it has its own flocks in the sky (AmbienceServer.IsSunnyDay): by day, not raining, no eclipse.</summary>
        private static bool Fair() => Main.dayTime && !Main.IsItRaining && !Main.eclipse;

        /// <summary>0 at dawn rising to 1 an hour after it, 1 through the day, and back to 0 at dusk over the last hour; 0 at night.</summary>
        private static float DayShare()
        {
            if (!Main.dayTime)
                return 0f;

            float t = MathHelper.Clamp((float)(Math.Min(Main.time, Main.dayLength - Main.time) / Hour), 0f, 1f);

            return t * t * (3f - 2f * t);
        }

        /// <summary>The lighthouse's middle, at its lens (where LighthouseBeam draws its beams from).</summary>
        private static Vector2 Axis()
        {
            Point lens = VastOceanIsland.LensAt;

            return new Vector2((lens.X + LighthouseLens.Wide * 0.5f) * 16f, (lens.Y + 1.75f) * 16f);
        }

        private static void Tick()
        {
            uint now = Main.GameUpdateCount;
            Rectangle view = View();
            float day = DayShare();
            bool fair = Fair() && day > 0f;

            Vector2 axis = Axis();
            Vector2 me = Main.LocalPlayer.Center;
            bool near = Math.Abs(me.X - axis.X) < WheelNear * 16f && me.Y < (VastOcean.Surface + 40) * 16f;

            if (fair)
            {
                Cross(now, day, view);

                if (near && day >= 0.5f)
                    Gather(now, view, axis);
            }

            for (int f = 0; f < MostFlocks; f++)
            {
                ref Flock flock = ref flocks[f];
                if (!flock.Alive)
                    continue;

                flock.Age++;

                if (flock.Way == Way.Wheeling)
                {
                    if (!near && !Seen(f, view, 240))
                    {
                        Drop(f);
                        continue;
                    }

                    if (!fair || day < 0.15f || !near || flock.Age > flock.Life)
                    {
                        Leave(f, now);
                        continue;
                    }

                    // Round: the patch going round its circle, rising and falling a little.
                    flock.Angle = MathHelper.WrapAngle(flock.Angle + flock.Turn);
                    flock.BobPhase = MathHelper.WrapAngle(flock.BobPhase + flock.BobRate);
                    continue;
                }

                // Going: keeping its height, rising and falling a little as it goes.
                float aim = flock.Cruise + 14f * (float)Math.Sin(flock.Age * 0.004f + flock.Phase);
                flock.Drift.Y = MathHelper.Clamp((aim - flock.Centre.Y) * 0.02f, -0.35f, 0.35f);
                flock.Centre += flock.Drift;
            }

            for (int i = 0; i < MostBirds; i++)
            {
                if (birds[i].Alive)
                    Fly(ref birds[i], ref flocks[birds[i].Flock]);
            }

            for (int f = 0; f < MostFlocks; f++)
            {
                if (flocks[f].Alive && flocks[f].Way != Way.Wheeling && Gone(f, view))
                    Drop(f);
            }

            Cry(now, view);
        }

        // ---- flocks coming and going -------------------------------------------------------------

        /// <summary>
        /// Now and then a flock across the sea, near or far off, in from off the side of the screen
        /// it is going away from, at a height that is on the screen (in the sky over the water,
        /// between three tiles over it and 170), if there is that much of the sky on it.
        /// </summary>
        private static void Cross(uint now, float day, Rectangle view)
        {
            if (now < nextCrossing)
                return;

            // Tried again in a second if none goes now.
            nextCrossing = now + 60;

            if (Count(Way.Crossing) >= MostCrossing)
                return;

            int n = Main.rand.Next(Fewest, Most + 1);
            int f = FreeFlock();
            if (f < 0 || MostBirds - birdCount < n)
                return;

            float scale = Main.rand.NextFloat() < 0.6f ? Near : Far;

            // The patch's size; its middle so high that all of it is on the screen and over the
            // lowest it may fly, with room for its gulls' drifting and its own rising and falling.
            float spanX = (26f + 10f * n) * scale, spanY = (8f + 4f * n) * scale;
            float room = 8f * scale + 14f + FrameHigh * scale * 0.5f;
            float sea = VastOcean.Surface * 16f;
            float top = Math.Max(view.Top + 48f, sea - HighestTiles * 16f) + spanY + room;
            float bottom = Math.Min(view.Bottom - 48f, sea - LowestTiles * 16f) - spanY - room;
            if (bottom < top)
                return;

            float wind = Main.WindForVisuals;
            int downwind = Math.Abs(wind) < 0.05f ? (Main.rand.NextBool() ? 1 : -1) : Math.Sign(wind);
            bool against = Main.rand.NextFloat() < 0.25f;
            int dir = against ? -downwind : downwind;
            float strength = Math.Min(Math.Abs(wind), 1f);
            float speed = against ? Main.rand.NextFloat(1.8f, 2.6f) - 0.4f * strength : Main.rand.NextFloat(2.2f, 3.2f) + strength;
            speed *= scale == Near ? 1f : 0.75f;

            float x = dir > 0 ? view.Left - spanX - Main.rand.NextFloat(80f, 400f) : view.Right + spanX + Main.rand.NextFloat(80f, 400f);
            var centre = new Vector2(x, Main.rand.NextFloat(top, bottom));

            flocks[f] = new Flock
            {
                Alive = true,
                Way = Way.Crossing,
                Centre = centre,
                Drift = new Vector2(dir * speed, 0f),
                Cruise = centre.Y,
                Phase = Main.rand.NextFloat(MathHelper.TwoPi),
            };

            Patch(n, spanX, spanY, Apart * scale);

            for (int k = 0; k < n; k++)
            {
                ref Bird b = ref birds[FreeBird()];

                b = new Bird
                {
                    Alive = true,
                    Flock = f,
                    Place = placed[k],
                    Rest = Level,
                    Face = (sbyte)dir,
                    Scale = scale,
                    Shade = 1f,
                };
                Wander(ref b, scale);
                b.At = centre + b.Place + new Vector2(b.WanderX * (float)Math.Sin(b.PhaseX), b.WanderY * (float)Math.Sin(b.PhaseY));
                b.Velocity = flocks[f].Drift;
                Wings(ref b);
                birdCount++;
            }

            nextCrossing = now + (uint)(Main.rand.Next(360, 1261) / Math.Max(day, 0.25f));
        }

        /// <summary>
        /// A flock to wheel round the lighthouse, when there is none (and now and then a second at
        /// another height): its circle's middle over the lantern by up to 16 tiles, or down to 30
        /// under it (its lowest gulls a good ten tiles over the flagstaff and the tree on the
        /// plateau). Out of sight its gulls are put in their places; in sight they fly in from off
        /// the side of the screen the lighthouse is on.
        /// </summary>
        private static void Gather(uint now, Rectangle view, Vector2 axis)
        {
            int wheels = Count(Way.Wheeling);
            if (wheels >= MostWheels || now < nextWheel)
                return;

            if (wheels == 1)
            {
                nextWheel = now + (uint)Main.rand.Next(2400, 7201);

                if (Main.rand.NextFloat() >= 0.4f)
                    return;
            }

            int n = Main.rand.Next(Fewest, Most + 1);
            int f = FreeFlock();
            if (f < 0 || MostBirds - birdCount < n)
                return;

            // A second flock at least fourteen tiles above or below the first.
            float rows = Main.rand.NextFloat(-16f, 30f);
            for (int other = 0; other < MostFlocks; other++)
            {
                if (!flocks[other].Alive || flocks[other].Way != Way.Wheeling)
                    continue;

                float theirs = (flocks[other].Centre.Y - axis.Y) / 16f;
                for (int tries = 0; tries < 8 && Math.Abs(rows - theirs) < 14f; tries++)
                    rows = Main.rand.NextFloat(-16f, 30f);

                if (Math.Abs(rows - theirs) < 14f)
                    rows = theirs < 8f ? theirs + 14f : theirs - 14f;
            }

            var centre = axis + new Vector2(Main.rand.NextFloat(-2f, 2f) * 16f, rows * 16f);
            float across = 16f * Main.rand.NextFloat(26f, 40f);

            flocks[f] = new Flock
            {
                Alive = true,
                Way = Way.Wheeling,
                Centre = centre,
                Angle = Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi),
                Turn = (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(2.2f, 2.8f) / across,
                Across = across,
                Tilt = across * Main.rand.NextFloat(0.08f, 0.2f) * (Main.rand.NextBool() ? 1f : -1f),
                Bob = 16f * Main.rand.NextFloat(1f, 3f),
                BobRate = Main.rand.NextFloat(0.003f, 0.006f),
                BobPhase = Main.rand.NextFloat(-MathHelper.Pi, MathHelper.Pi),
                Life = 60 * Main.rand.Next(90, 241),
            };

            // The patch: spread along the circle and up and down.
            float spanX = (30f + 12f * n) * Near, spanY = (8f + 3f * n) * Near;
            Patch(n, spanX, spanY, Apart * Near);

            // Where its gulls can be: the circle and the patch's spread out from it (at most 46
            // tiles either side), and up or down 3 for the rising and falling, 5 for the patch and
            // 8 for the tilt.
            var circles = new Rectangle((int)(centre.X - 48 * 16), (int)(centre.Y - 18 * 16), 96 * 16, 36 * 16);
            Rectangle seen = view;
            seen.Inflate(64, 64);
            bool flyIn = circles.Intersects(seen);
            int side = centre.X < view.X + view.Width / 2 ? -1 : 1;
            float edge = side < 0 ? view.Left - spanX - Main.rand.NextFloat(60f, 240f) : view.Right + spanX + Main.rand.NextFloat(60f, 240f);
            int spin = Math.Sign(flocks[f].Turn);

            for (int k = 0; k < n; k++)
            {
                ref Bird b = ref birds[FreeBird()];

                b = new Bird
                {
                    Alive = true,
                    Flock = f,
                    Place = placed[k],
                    Rest = Main.rand.NextFloat() < 0.6f ? Lifted : Level,
                    Scale = Near,
                };
                Wander(ref b, Near);
                Circle(ref b, ref flocks[f], out Vector2 slot, out Vector2 slotVelocity);

                if (flyIn)
                {
                    b.At = new Vector2(edge + b.Place.X * 0.6f, centre.Y + b.Place.Y);
                    b.Velocity = new Vector2(-side * 3f, 0f);
                }
                else
                {
                    b.At = slot;
                    b.Velocity = slotVelocity;
                }

                b.Front = b.WantFront;
                b.Face = (sbyte)(b.Velocity.X < 0f ? -1 : b.Velocity.X > 0f ? 1 : spin);
                Wings(ref b);
                birdCount++;
            }

            if (wheels == 0)
                nextWheel = now + (uint)Main.rand.Next(2400, 7201);
        }

        /// <summary>
        /// A flock at the lighthouse breaking off: away over the sea with the wind (either way if
        /// there is none), climbing a little, drawing in to a patch as it goes; each gull staying in
        /// front of the world's tiles or behind them as it was. Another may gather in twenty
        /// seconds to a minute.
        /// </summary>
        private static void Leave(int f, uint now)
        {
            ref Flock flock = ref flocks[f];
            Vector2 mid = Vector2.Zero;
            int n = 0;

            for (int i = 0; i < MostBirds; i++)
            {
                if (birds[i].Alive && birds[i].Flock == f)
                {
                    mid += birds[i].At;
                    n++;
                }
            }

            if (n == 0)
            {
                flock.Alive = false;
                return;
            }

            mid /= n;

            float wind = Main.WindForVisuals;
            int dir = Math.Abs(wind) < 0.05f ? (Main.rand.NextBool() ? 1 : -1) : Math.Sign(wind);

            flock.Way = Way.Leaving;
            flock.Centre = mid;
            flock.Drift = new Vector2(dir * Main.rand.NextFloat(2.4f, 3.2f), 0f);
            flock.Cruise = mid.Y - 16f * Main.rand.NextFloat(2f, 10f);
            flock.Phase = Main.rand.NextFloat(MathHelper.TwoPi);
            flock.Age = 0;

            for (int i = 0; i < MostBirds; i++)
            {
                ref Bird b = ref birds[i];
                if (!b.Alive || b.Flock != f)
                    continue;

                Vector2 place = (b.At - mid) * 0.5f;
                b.Place = new Vector2(MathHelper.Clamp(place.X, -240f, 240f), MathHelper.Clamp(place.Y, -90f, 90f));
                b.WantFront = b.Front;
            }

            nextWheel = Math.Max(nextWheel, now + (uint)Main.rand.Next(1200, 3601));
        }

        /// <summary>A flock going across or away that is off the screen on the side it is going to, or long out of sight, or far from it.</summary>
        private static bool Gone(int f, Rectangle view)
        {
            ref Flock flock = ref flocks[f];
            float least = float.MaxValue, most = float.MinValue;
            bool any = false;

            for (int i = 0; i < MostBirds; i++)
            {
                if (!birds[i].Alive || birds[i].Flock != f)
                    continue;

                any = true;
                least = Math.Min(least, birds[i].At.X);
                most = Math.Max(most, birds[i].At.X);
            }

            if (!any)
                return true;

            if (flock.Drift.X > 0f && least > view.Right + 240)
                return true;

            if (flock.Drift.X < 0f && most < view.Left - 240)
                return true;

            if (Seen(f, view, 240))
                return false;

            var middle = new Vector2(view.X + view.Width * 0.5f, view.Y + view.Height * 0.5f);

            return flock.Age > 3600 || Vector2.Distance(flock.Centre, middle) > 6000f;
        }

        /// <summary>Whether any gull of the flock is on the screen, or within so many pixels of it.</summary>
        private static bool Seen(int f, Rectangle view, int margin)
        {
            Rectangle wide = view;
            wide.Inflate(margin, margin);

            for (int i = 0; i < MostBirds; i++)
            {
                if (birds[i].Alive && birds[i].Flock == f && wide.Contains((int)birds[i].At.X, (int)birds[i].At.Y))
                    return true;
            }

            return false;
        }

        private static void Drop(int f)
        {
            flocks[f].Alive = false;

            for (int i = 0; i < MostBirds; i++)
            {
                if (birds[i].Alive && birds[i].Flock == f)
                {
                    birds[i].Alive = false;
                    birdCount--;
                }
            }
        }

        private static int Count(Way way)
        {
            int n = 0;

            for (int f = 0; f < MostFlocks; f++)
            {
                if (flocks[f].Alive && flocks[f].Way == way)
                    n++;
            }

            return n;
        }

        private static int FreeFlock()
        {
            for (int f = 0; f < MostFlocks; f++)
            {
                if (!flocks[f].Alive)
                    return f;
            }

            return -1;
        }

        /// <summary>A gull not in use (there is one: whoever asks has counted).</summary>
        private static int FreeBird()
        {
            for (int i = 0; i < MostBirds; i++)
            {
                if (!birds[i].Alive)
                    return i;
            }

            return -1;
        }

        /// <summary>Places for n gulls in a loose patch: in an oval so wide and so high about the middle, none nearer another than so far where room allows.</summary>
        private static void Patch(int n, float spanX, float spanY, float apart)
        {
            for (int k = 0; k < n; k++)
            {
                Vector2 p = Vector2.Zero;

                for (int tries = 0; tries < 30; tries++)
                {
                    float a = Main.rand.NextFloat(MathHelper.TwoPi), r = (float)Math.Sqrt(Main.rand.NextFloat());
                    p = new Vector2((float)Math.Cos(a) * r * spanX, (float)Math.Sin(a) * r * spanY);

                    bool clear = true;
                    for (int j = 0; j < k && clear; j++)
                        clear = Vector2.DistanceSquared(p, placed[j]) >= apart * apart;

                    if (clear)
                        break;
                }

                placed[k] = p;
            }
        }

        /// <summary>A gull's drifting about its place in a patch: a few pixels each way, slowly, out of step with the others.</summary>
        private static void Wander(ref Bird b, float scale)
        {
            b.WanderX = Main.rand.NextFloat(4f, 12f) * scale;
            b.WanderY = Main.rand.NextFloat(3f, 8f) * scale;
            b.RateX = Main.rand.NextFloat(0.008f, 0.025f);
            b.RateY = Main.rand.NextFloat(0.008f, 0.025f);
            b.PhaseX = Main.rand.NextFloat(MathHelper.TwoPi);
            b.PhaseY = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        /// <summary>A new gull's wings, out of step with the rest: a beat of three to five ticks a picture (the game's flocks, four), somewhere in it, or gliding.</summary>
        private static void Wings(ref Bird b)
        {
            b.Beat = Main.rand.Next(3, 6);
            b.Tick = Main.rand.Next(b.Beat);
            b.Frame = Main.rand.Next(Frames);
            b.Flaps = Main.rand.Next(1, 4);

            if (Main.rand.NextBool())
            {
                b.Frame = b.Rest;
                b.Hold = Main.rand.Next(10, 90);
            }
        }

        // ---- a gull ------------------------------------------------------------------------------

        /// <summary>
        /// Where a wheeling gull's place is and how it is moving: its flock's place on the circle,
        /// moved along the circle by the gull's place in the patch (and a little in or out by its
        /// height in it), and up or down by its height; which side of the tower it is on (the near
        /// half of the circle is the half with the sine over 0), and its shade.
        /// </summary>
        private static void Circle(ref Bird b, ref Flock flock, out Vector2 slot, out Vector2 slotVelocity)
        {
            float t = flock.Age;
            float ax = t * b.RateX + b.PhaseX, ay = t * b.RateY + b.PhaseY;
            float along = b.Place.X + b.WanderX * (float)Math.Sin(ax), up = b.Place.Y + b.WanderY * (float)Math.Sin(ay);
            float alongTurn = b.WanderX * b.RateX * (float)Math.Cos(ax), upTurn = b.WanderY * b.RateY * (float)Math.Cos(ay);

            float radius = flock.Across + 0.5f * b.Place.Y;
            float angle = flock.Angle + along / flock.Across, turn = flock.Turn + alongTurn / flock.Across;
            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
            float bob = flock.Bob * (float)Math.Sin(flock.BobPhase), bobTurn = flock.Bob * flock.BobRate * (float)Math.Cos(flock.BobPhase);

            slot = flock.Centre + new Vector2(radius * cos, up + bob + flock.Tilt * sin);
            slotVelocity = new Vector2(-radius * sin * turn, upTurn + bobTurn + flock.Tilt * cos * turn);

            b.WantFront = sin > 0f;
            b.Shade = 0.86f + 0.07f * (sin + 1f);
        }

        private static void Fly(ref Bird b, ref Flock flock)
        {
            Vector2 slot, slotVelocity;
            bool wheeling = flock.Way == Way.Wheeling;
            float top;

            if (wheeling)
            {
                Circle(ref b, ref flock, out slot, out slotVelocity);
                top = 4.5f;
            }
            else
            {
                float t = flock.Age;
                float ax = t * b.RateX + b.PhaseX, ay = t * b.RateY + b.PhaseY;

                slot = flock.Centre + b.Place + new Vector2(b.WanderX * (float)Math.Sin(ax), b.WanderY * (float)Math.Sin(ay));
                slotVelocity = flock.Drift + new Vector2(b.WanderX * b.RateX * (float)Math.Cos(ax), b.WanderY * b.RateY * (float)Math.Cos(ay));
                top = 6f;
            }

            // To its place: as fast as the place moves, and closing the gap; no faster than it can,
            // and turning no harder than it can.
            Vector2 to = slot - b.At;
            Vector2 want = slotVelocity + to * Pull;
            float wantLength = want.Length();
            if (wantLength > top)
                want *= top / wantLength;

            Vector2 change = want - b.Velocity;
            float changeLength = change.Length();
            if (changeLength > MostTurn)
                change *= MostTurn / changeLength;

            b.Velocity += change;
            b.At += b.Velocity;

            if (b.Velocity.X > 0.3f)
                b.Face = 1;
            else if (b.Velocity.X < -0.3f)
                b.Face = -1;

            if (b.Front != b.WantFront && Open(b.At))
                b.Front = b.WantFront;

            // Well short of its place it beats its wings; going across, climbing too. Soaring round
            // the lighthouse it rises on its circle without them.
            Beat(ref b, wheeling, to.LengthSquared() > 64f * 64f || !wheeling && b.Velocity.Y < -0.6f);
        }

        /// <summary>
        /// Its wings: a picture every few ticks through the wingbeat; after its wingbeats, gliding
        /// on its glide picture a while (soaring at the lighthouse, longer), then beating again.
        /// While it must work (<paramref name="labour"/>) it beats its wings and does not glide.
        /// </summary>
        private static void Beat(ref Bird b, bool soaring, bool labour)
        {
            if (b.Hold > 0)
            {
                if (!labour)
                {
                    b.Hold--;
                    b.Frame = b.Rest;
                    return;
                }

                b.Hold = 0;
                b.Flaps = Math.Max(b.Flaps, 1);
            }

            if (++b.Tick < b.Beat)
                return;

            b.Tick = 0;
            b.Frame = (b.Frame + 1) % Frames;

            if (b.Frame == 0)
                b.Flaps--;

            if (b.Flaps <= 0 && !labour && b.Frame == b.Rest)
            {
                b.Hold = soaring ? Main.rand.Next(120, 421) : Main.rand.Next(40, 151);
                b.Flaps = soaring ? Main.rand.Next(1, 3) : Main.rand.Next(2, 6);
            }
        }

        /// <summary>Nothing of the world (a tile of any kind, or a wall) within two tiles of the gull, all its picture covers: it can go in front of the tiles or behind them here without being seen to.</summary>
        private static bool Open(Vector2 at)
        {
            int x = (int)Math.Floor(at.X / 16f), y = (int)Math.Floor(at.Y / 16f);
            if (!WorldGen.InWorld(x, y, 3))
                return true;

            for (int i = x - 2; i <= x + 2; i++)
            {
                for (int j = y - 2; j <= y + 2; j++)
                {
                    Tile t = Main.tile[i, j];
                    if (t.HasTile || t.WallType != 0)
                        return false;
                }
            }

            return true;
        }

        /// <summary>Now and then one on the screen or near it cries.</summary>
        private static void Cry(uint now, Rectangle view)
        {
            if (now < nextCry || birdCount == 0)
                return;

            Rectangle wide = view;
            wide.Inflate(400, 400);

            for (int i = 0; i < MostBirds; i++)
            {
                ref Bird b = ref birds[i];
                if (!b.Alive || !wide.Contains((int)b.At.X, (int)b.At.Y) || !Main.rand.NextBool(CryOdds))
                    continue;

                SoundEngine.PlaySound(SoundID.Seagull, b.At);
                nextCry = now + CryApart;
                return;
            }
        }

        // ---- drawing -----------------------------------------------------------------------------

        /// <summary>The gulls in front of the world's tiles: after them, before the players.</summary>
        public override void PostDrawTiles()
        {
            if (!here || birdCount == 0 || gull == null || !gull.IsLoaded || SubworldKeeper.Current is not VastOcean)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            try
            {
                Draw(Main.spriteBatch, true);
            }
            finally
            {
                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// The gulls behind the world's tiles: in the game's own layer for what is behind the walls
        /// (RenderLayers.Background, drawn after the sky, the sea behind and VastOceanSky's water
        /// and haze, and before the walls and tiles). The game's batch is open there as it is
        /// begun for that layer (in the world's pixels with the camera's zoom, sampling the nearest
        /// texel), so they are drawn into it as they are.
        /// </summary>
        private static void DrawBehind(On_OverlayManager.orig_Draw orig, OverlayManager self, SpriteBatch spriteBatch, RenderLayers layer, bool beginSpriteBatch)
        {
            orig(self, spriteBatch, layer, beginSpriteBatch);

            if (layer == RenderLayers.Background && !beginSpriteBatch && here && birdCount > 0 && ReferenceEquals(self, Overlays.Scene)
                && gull != null && gull.IsLoaded && SubworldKeeper.Current is VastOcean)
                Draw(spriteBatch, false);
        }

        /// <summary>
        /// Each gull on the screen on this side of the tiles, in the light where it is or the sun's,
        /// its picture's corner on a whole pixel (so that each of the picture's own pixels is drawn as
        /// an even square), turned to face the way it goes.
        /// </summary>
        private static void Draw(SpriteBatch batch, bool front)
        {
            Texture2D picture = gull.Value;
            Rectangle view = View();
            view.Inflate(FrameWide * 2, FrameHigh * 2);

            for (int i = 0; i < MostBirds; i++)
            {
                ref Bird b = ref birds[i];
                if (!b.Alive || b.Front != front || !view.Contains((int)b.At.X, (int)b.At.Y))
                    continue;

                // In the light where it is, and never darker than the sun's (Main.tileColor, the light
                // the game gives whatever the sky shines on): one going by in front of the tower's
                // walls, where the sky's light does not reach, is still out in the sun.
                Color light = Lighting.GetColor((int)(b.At.X / 16f), (int)(b.At.Y / 16f)), sun = Main.tileColor;
                var colour = new Color((byte)(Math.Max(light.R, sun.R) * b.Shade), (byte)(Math.Max(light.G, sun.G) * b.Shade),
                    (byte)(Math.Max(light.B, sun.B) * b.Shade), (byte)255);
                float x = b.At.X - Main.screenPosition.X - FrameWide * b.Scale * 0.5f, y = b.At.Y - Main.screenPosition.Y - FrameHigh * b.Scale * 0.5f;

                batch.Draw(picture, new Vector2((float)Math.Floor(x), (float)Math.Floor(y)), new Rectangle(0, b.Frame * FrameHigh, FrameWide, FrameHigh), colour,
                    0f, Vector2.Zero, b.Scale, b.Face < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }
    }
}
