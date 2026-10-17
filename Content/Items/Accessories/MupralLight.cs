using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.Subworlds;
using ReLogic.Content;
using ReLogic.Threading;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The M.U.P.R.A.L's light, by request ("Use light physics to make it realistic", "tiles in front
    /// will hinder scan rays", "remember to dim it the more it goes out", "Make it pretty"; then
    /// "change the viewing beam distance to 15 tiles normal and 11 tiles under water, make the rays
    /// super fine and dont light it block by block, any thing passed by the ray is lit up, the
    /// solid blocks will be lit up too but only the part within the path of the ray. Change beam
    /// angle to 30 degrees"), on every player's machine, for every player wearing one.
    ///
    /// HOW IT GOES. The light leaves the lamp's glass in straight lines, and fine ones: across the
    /// beam's 30 degrees one every eighth of a degree (241); all about the lamp, for the little
    /// glow left with the beam, one every degree (360); for the glow, one every half degree (720).
    /// Each goes through the world as a line goes through a grid, from where it goes into a tile to
    /// where it comes out, split where it goes into the water of a tile part filled (<see cref="Walk"/>).
    /// Whatever it passes is lit. It passes what is not solid, platforms too; a solid block it goes
    /// into, the part of the block in its path lit, dimming to nothing a tile deep along its way,
    /// and no further: what is behind is in that block's shadow. A half block or a slope it goes
    /// into only where it crosses the solid part. Along each ray the light is dimmer the further it
    /// has gone, three ways:
    ///   - spreading: the light falls off as the square of the distance from the lamp (the glow's
    ///     from two tiles out; the beam's, gathered by its reflector, from six), and the eye sees
    ///     brightness as the square root of that (Stevens' law), so what is seen falls as the
    ///     distance, not its square;
    ///   - the way it goes through: each pixel of the way uses up a share of how far the light can
    ///     go, the share of what was asked for in air (6.1 tiles for the glow) where it is in air
    ///     and of what was asked for in water (3.7) where it is in water; the beam's 68 and 50
    ///     wherever the cursor is (by request: "Remake the light beam mupral to be max 50 in
    ///     water. (it is like that every where the ray wont reduce if i bring my mouse in) (like
    ///     image is enough)": the beam as it was drawn out to 50 for a far cursor, the picture
    ///     asked from; through air as much further as it always was, 15 to 11; <see cref="Spot"/>);
    ///     the light is taken away along that as water and air take light away (exponentially), so
    ///     far that at the end of its reach in air it is the least the eye can see in the dark
    ///     (<see cref="Least"/>), and over the last 15 hundredths of its reach it fades out
    ///     altogether: it ends where it was asked to end, in air, in water, or partly each;
    ///   - water takes the red of it first and the blue last: the further it has gone through
    ///     water, the less red and a little less green, so its far end under the sea is cooler.
    /// The beam is brightest at its middle and falls off toward its edges to a little under half,
    /// and ends there, the last ray: 30 degrees across. The beige: (255, 227, 184).
    ///
    /// THE HEATR'S LAMP (MupralKit.Suit, by request: "include the MUPRAL functions with upgraded
    /// version(radial for 30 tiles and same beam with 45 degree angle continuously so no
    /// swittch)", then "radial radius still need be 2/3 tho"): both at once, always, no
    /// switching: the glow all about, to 20 tiles through water (two thirds of the 30 first asked
    /// for) and as much further through air as the glow goes (6.1 to 3.7): 33
    /// (<see cref="Wide"/>); and the same beam, 45 degrees across.
    ///
    /// AS LIGHT, AND AS SEEN. The game's light is one light to a tile, spread over the tile as the
    /// tile is drawn, and the game draws its tiles, what stands on them, its walls, its water and
    /// the backdrop under the ground into pictures of its own, each made again every fourth frame
    /// as lit then. So the lamp's light goes to the game twice:
    ///   - as light (<see cref="AfterBlur"/>): into the game's map of light, after the game has
    ///     spread its own lights and before it shows them (On_LightingEngine.ProcessBlur), each
    ///     tile at least as bright as the lamp can make any of it before the game looks again (the
    ///     lamp where its wearer is taking it, its beam where it is turning, and a tile more about
    ///     that); so it lights the creatures, players and things it falls on, and the map, as the
    ///     game's lights do. Never spread from tile to tile: it has gone where it goes already;
    ///   - as seen, to the pixel: before anything is drawn in a frame (<see cref="Prepare"/>), a
    ///     picture of how much of the game's light each pixel about the lamp keeps: the light the
    ///     game has there besides the lamp's, and the lamp's as its rays lay it there, over all the
    ///     light the game has there. It is laid over each of the game's pictures of the world as
    ///     the game makes it (<see cref="Bake"/>), so the lamp's light stays only where its rays
    ///     go, and as bright as they lay it: the part of a block out of their path, and all in a
    ///     shadow, keep none of it; what stands about (creatures, players) is lit by the tile it is
    ///     in, as the game lights everything.
    /// The game spreads a tile's light over it three ways (walls, water and backdrop smoothly from
    /// corner to corner, blocks in nine pieces near enough the same, what stands on the blocks one
    /// light to a tile): the picture is made for the first and put right, for what stands on the
    /// blocks, tile by tile (<see cref="objectsTarget"/>). Under the game's two old lightings
    /// (Retro, Trippy) it is handed to the game as its lights are (Lighting.AddLight), and spread
    /// and seen as theirs are, tile by tile.
    ///
    /// IN THE VAST OCEAN the sea is drawn over the game's own water behind everything
    /// (VastOceanSky), which hides the light the game's water there would show; so the water its
    /// rays pass is lit on the sea too, to the pixel (<see cref="DrawLitWater"/>), and the game's
    /// water over everything, the water between the eye and all else, keeps the lamp's light only
    /// where its rays go, as everything does. While a lit lamp's light can be on the screen the
    /// depths' dark gives way to it (VastOceanSky: its murk, silt and the depths' tint), by request
    /// ("murpal needs to be able to shine at this depth bruh", "at every depth").
    ///
    /// THE LAMP SEEN (<see cref="Front"/>, drawn in front of everything, the water too): the glass
    /// bright, with a little glow about it; and in the water the motes drifting there glitter where
    /// its rays fall on them, none in a shadow (<see cref="DrawMotes"/>). The lamp itself on the
    /// shoulder is <see cref="MupralLayer"/>.
    ///
    /// THE SAME LIGHT FOR LESS WORK, by request ("HEATR makes everything super laggy, maybe its the
    /// fine light system. Please optimize it HEATR and MUPRAL", at 12 frames a second where 60 is
    /// usual): the light as it was, worked out with a small part of the work.
    ///   - Each ray's way is kept as where it changes (into or out of water, into solid), not tile
    ///     by tile. Every other ray is traced, and one between two that go alike (the same way
    ///     through, into no solid, to the same end) goes as they do: nothing solid fits between
    ///     two rays that close without touching one (<see cref="Group"/>).
    ///   - Next rays that go alike, so are lit alike, are laid as one, up to twelve pixels across;
    ///     in halves where they stop going alike; two by two only where they part
    ///     (<see cref="LayFans"/>). Each corner is made once and shared by the pieces about it, and
    ///     the pieces are drawn three-cornered, straight into the pictures, in as few draws as can
    ///     be (<see cref="Mesh"/>): where the rays end the light ends, with no crack between pieces
    ///     and none laid over another.
    ///   - Each light's falling off is reckoned once and looked up (<see cref="Look"/>); the
    ///     corners are coloured for the pictures on as many cores as the game spreads its own
    ///     light over; the tiles about the lamp are drawn the same way, each corner reckoned once;
    ///     the game's map is given the HEATR's glow along rays a degree apart (every tile still
    ///     crossed), and the lamp is looked for ahead only as it goes or turns.
    /// Some twelve to twenty times less work a frame for the HEATR's lamp, two to five for the
    /// M.U.P.R.A.L's, and the pictures the same to a pixel at the edges.
    /// </summary>
    public class MupralLight : ModSystem
    {
        /// <summary>As far as each light goes, in tiles, through air and through water (asked for), and the beam's breadth in degrees (asked for).</summary>
        public const float GlowAir = 6.1f;
        public const float GlowWater = 3.7f;
        public const float NearGlow = 1.2f;
        public const float BeamWater = 50f;
        public const float BeamAir = BeamWater * 15f / 11f;
        public const float BeamDegrees = 30f;

        /// <summary>The HEATR's lamp: its glow all about (20 tiles through water, two thirds of the 30 first asked for; through air as the glow goes, 6.1 to 3.7) and its beam's breadth.</summary>
        public const float SuitGlowWater = 20f;
        public const float SuitGlowAir = SuitGlowWater * GlowAir / GlowWater;
        public const float SuitBeamDegrees = 45f;

        /// <summary>The furthest any of its lights goes, in tiles: the beam's through air.</summary>
        public const float MostReach = BeamAir;

        /// <summary>The least light the eye picks out in the dark, at the end of a light's reach through air: a sixteenth or so of full (the game's light runs from 0 to about 1).</summary>
        private const float Least = 0.06f;

        /// <summary>What is left of a long light's light at the end of its reach through air: more than <see cref="Least"/>, so that it stays bright along the way (<see cref="Spot"/>, <see cref="Wide"/>).</summary>
        private const float FarLeast = 0.15f;

        /// <summary>The beige of its light, and of its glass.</summary>
        public static readonly Vector3 Beige = new Vector3(1f, 0.89f, 0.72f);
        private static readonly Color BeigeColour = new Color(255, 227, 184);

        /// <summary>
        /// One of its lights: how bright at the lamp, how near the lamp its spreading begins to tell
        /// (tiles), how far it goes through air and through water (tiles), and how fast the way it
        /// goes takes it away, worked out so that what is left at the end of its reach in air is
        /// the least that can be seen (<see cref="Least"/>, or as given); never so that it grows.
        /// </summary>
        private readonly struct Shape
        {
            /// <summary>Which of the lights it is (<see cref="GlowShape"/> and the rest): its falling off, reckoned once and kept, is looked up by it.</summary>
            public readonly int Kind;
            public readonly float Bright;
            public readonly float Near;
            public readonly float Air;
            public readonly float Water;
            public readonly float Thinning;

            public Shape(int kind, float bright, float near, float air, float water, float least = Least)
            {
                Kind = kind;
                Bright = bright;
                Near = near;
                Air = air;
                Water = water;
                Thinning = Math.Max(0f, (float)Math.Log(bright * Spread(air, near) / least));
            }
        }

        private static readonly Shape Glow = new Shape(GlowShape, 0.7f, 2f, GlowAir, GlowWater);
        private static readonly Shape Near = new Shape(NearShape, 0.35f, 0.8f, NearGlow, NearGlow);

        /// <summary>
        /// The beam's light: the beam as it was when drawn out to 50 tiles through water for a far
        /// cursor, the picture asked from (its spreading beginning as much further out than the
        /// first beam's six tiles at eleven, and keeping <see cref="FarLeast"/> of its light to its
        /// end), now wherever the cursor is. The same for the HEATR's wider beam.
        /// </summary>
        private static readonly Shape Spot = new Shape(SpotShape, 1.2f, 6f * BeamWater / 11f, BeamAir, BeamWater, FarLeast);

        /// <summary>The HEATR's glow all about: the glow drawn out to 20 tiles through water the same way (its spreading beginning as much further out than the glow's two tiles at 3.7).</summary>
        private static readonly Shape Wide = new Shape(WideShape, 0.7f, 2f * SuitGlowWater / GlowWater, SuitGlowAir, SuitGlowWater, FarLeast);

        private static Shape ShapeOf(int shape) => shape switch
        {
            GlowShape => Glow,
            NearShape => Near,
            WideShape => Wide,
            _ => Spot,
        };

        private const int GlowShape = 0;
        private const int NearShape = 1;
        private const int SpotShape = 2;
        private const int WideShape = 3;

        /// <summary>
        /// Each light's falling off, reckoned once and kept, as it is looked up very often (every
        /// corner laid, every tile crossed for the game's map; by <see cref="Shape.Kind"/>): its
        /// spreading every pixel out, to past the furthest any ray goes; how much is left as its
        /// reach is used up, every 1/4096 of its reach to twice it; and the red and the green the
        /// water takes away, every 1/64 of a tile to 128 tiles. Read between steps along a straight
        /// line (<see cref="Look"/>): the curves are so gentle there that it is the same to a millionth.
        /// </summary>
        private const int SpreadSteps = 2048;
        private const float UsedStep = 1f / 4096f;
        private const int UsedSteps = 8192;
        private const float WetStep = 1f / 64f;
        private const int WetSteps = 8192;

        private static readonly float[][] spreads = { SpreadTable(Glow), SpreadTable(Near), SpreadTable(Spot), SpreadTable(Wide) };
        private static readonly float[][] thinnings = { ThinningTable(Glow), ThinningTable(Near), ThinningTable(Spot), ThinningTable(Wide) };
        private static readonly float[] reds = WetTable(0.045f);
        private static readonly float[] greens = WetTable(0.012f);

        private static float[] SpreadTable(Shape shape)
        {
            float[] table = new float[SpreadSteps + 1];

            for (int i = 0; i <= SpreadSteps; i++)
                table[i] = Spread(i / 16f, shape.Near);

            return table;
        }

        private static float[] ThinningTable(Shape shape)
        {
            float[] table = new float[UsedSteps + 1];

            for (int i = 0; i <= UsedSteps; i++)
                table[i] = (float)Math.Exp(-shape.Thinning * (i * UsedStep));

            return table;
        }

        private static float[] WetTable(float rate)
        {
            float[] table = new float[WetSteps + 1];

            for (int i = 0; i <= WetSteps; i++)
                table[i] = (float)Math.Exp(-rate * (i * WetStep));

            return table;
        }

        /// <summary>A table read <paramref name="at"/> steps along (between two steps, along the straight line between them; before the first, the first; past the last, the last).</summary>
        private static float Look(float[] table, float at)
        {
            if (!(at > 0f))
                return table[0];

            int i = (int)at;

            if (i >= table.Length - 1)
                return table[table.Length - 1];

            return table[i] + (table[i + 1] - table[i]) * (at - i);
        }

        /// <summary>The fine rays (as seen, every frame): across the beam an eighth of a degree apart; all about the lamp for the little glow left with the beam, a degree apart; for the glow, half a degree apart.</summary>
        private const int SpotRays = (int)(BeamDegrees * 8f) + 1;
        private const int SuitSpotRays = (int)(SuitBeamDegrees * 8f) + 1;
        private const int NearRays = 360;
        private const int GlowRays = 720;

        /// <summary>The HEATR's glow all about: a quarter of a degree apart (at 33 tiles out, a ray every 2.3 pixels), and a degree apart for the game's map (at the 40 tiles it is followed for that, a ray every 0.7 tiles: every tile crossed, as before).</summary>
        private const int WideRays = 1440;
        private const int WideRaysGame = 360;

        /// <summary>
        /// The rays the game's map is given its light along (<see cref="AfterBlur"/>), coarser, but
        /// close enough that every tile is crossed: across the beam half a degree apart and a degree
        /// past each edge; all about, three degrees apart for the little glow and one for the glow.
        /// They go on past where the light ends (to this share of its reach), and do not fade.
        /// </summary>
        private const float SpotStepGame = 0.5f;
        private const float SpotPastGame = 1f;
        private const int NearRaysGame = 120;
        private const int GlowRaysGame = 360;
        private const float ReachGame = 1.2f;
        private const float NearReachGame = 2f;

        /// <summary>How many ticks ahead the lamp is looked for, as its wearer goes and its beam turns, for the light given to the game's map: it is shown for up to four frames, and the game's pictures made with it for up to four more.</summary>
        private static readonly int[] Ahead = { 0, 3, 6 };

        /// <summary>The tiles about a lamp its light can reach, as given to the game (the beam's furthest reach a fifth more, and the tile it stops in, and a tile more each way, and its wearer's going).</summary>
        private const int Reach = (int)(MostReach * ReachGame) + 4;

        /// <summary>The furthest any ray is followed, in pixels: the beam's furthest, as given to the game, and a little more.</summary>
        private const float Longest = (MostReach * ReachGame + 4f) * 16f;

        // ---- set up ----------------------------------------------------------------------------

        private static FieldInfo workingMap;
        private static FieldInfo workingArea;
        private static FieldInfo activeMap;
        private static Asset<Texture2D> dot;

        /// <summary>The game's lighting, as last seen working (<see cref="AfterBlur"/>).</summary>
        private static LightingEngine engine;

        /// <summary>Set after the first failure, so a fault costs one line in the log rather than one a frame; the light is then the game's own, tile by tile.</summary>
        private static bool broken;

        /// <summary>
        /// Blending the pictures: each pixel of the game's own kept in proportion (times the
        /// picture's colour); kept in proportion twice over (times twice the colour, so a share of up
        /// to two); and the brighter of the two kept.
        /// </summary>
        private static BlendState keep;
        private static BlendState keepTwice;
        private static BlendState brighter;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

            workingMap = typeof(LightingEngine).GetField("_workingLightMap", Private);
            workingArea = typeof(LightingEngine).GetField("_workingProcessedArea", Private);
            activeMap = typeof(LightingEngine).GetField("_activeLightMap", Private);

            keep = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.Zero,
                ColorDestinationBlend = Blend.SourceColor,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.One,
            };

            keepTwice = new BlendState
            {
                ColorBlendFunction = BlendFunction.Add,
                ColorSourceBlend = Blend.DestinationColor,
                ColorDestinationBlend = Blend.SourceColor,
                AlphaBlendFunction = BlendFunction.Add,
                AlphaSourceBlend = Blend.Zero,
                AlphaDestinationBlend = Blend.One,
            };

            brighter = new BlendState
            {
                ColorBlendFunction = BlendFunction.Max,
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.One,
                AlphaBlendFunction = BlendFunction.Max,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.One,
            };

            if (workingMap == null || workingArea == null || activeMap == null)
            {
                Mod.Logger.Warn("M.U.P.R.A.L: LightingEngine._workingLightMap, _workingProcessedArea or _activeLightMap is not there. "
                    + "Its light will be handed to the game as any light is, and spread and seen as the game does them, tile by tile.");
            }
            else
            {
                On_LightingEngine.ProcessBlur += AfterBlur;

                On_Main.RenderTiles += InTiles;
                On_Main.RenderTiles2 += InObjects;
                On_Main.RenderWalls += InWalls;
                On_Main.RenderBackground += InBackground;
                On_Main.RenderWater += InWater;

                On_Main.DrawTileEntities += AfterTileEntities;
                On_Main.DrawWalls += AfterWalls;
                On_Main.DrawBackground += AfterBackground;
                On_Main.DrawWaters += AfterWaters;
            }

            Main.OnPreDraw += Prepare;

            dot = ModContent.Request<Texture2D>("PisAndDag/Content/Tiles/WaygateLight");

            On_OverlayManager.Draw += Front;
        }

        public override void Unload()
        {
            Main.OnPreDraw -= Prepare;

            RenderTarget2D[] old = { cutTarget, objectsTarget, litTarget };

            cutTarget = null;
            objectsTarget = null;
            litTarget = null;
            cutReady = false;
            litReady = false;
            workingMap = null;
            workingArea = null;
            activeMap = null;
            engine = null;
            dot = null;
            keep = null;
            keepTwice = null;
            brighter = null;
            broken = false;

            foreach (LightRecord record in records)
                record.Clear();

            fans.Clear();
            fanCount = 0;

            if (Array.Exists(old, target => target != null))
            {
                Main.QueueMainThreadAction(() =>
                {
                    foreach (RenderTarget2D target in old)
                        target?.Dispose();
                });
            }
        }

        private void Fail(Exception e)
        {
            broken = true;
            cutReady = false;
            litReady = false;
            Mod.Logger.Error("M.U.P.R.A.L: its light to the pixel failed and is off until the mod reloads; the light is the game's own, tile by tile, till then.", e);
        }

        // ---- where the lamp is ----------------------------------------------------------------

        /// <summary>
        /// Where the lamp's head turns, in the world: on the front shoulder (the game's own for the
        /// front arm is four pixels behind the middle and two up, Player.GetFrontHandPosition), a
        /// pixel under that; with the body as it rises and falls in a stride (Main.OffsetsPlayerHeadgear),
        /// and as the game smooths a step up (gfxOffY).
        /// </summary>
        public static Vector2 Pivot(Player player)
        {
            Vector2 at = player.MountedCenter + new Vector2(-4f * player.direction, -1f * player.gravDir);
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;

            if (frame >= 0 && frame < Main.OffsetsPlayerHeadgear.Length)
                at.Y += Main.OffsetsPlayerHeadgear[frame].Y * player.gravDir;

            at.Y += player.gfxOffY;
            return at;
        }

        /// <summary>Which way the lamp's head points: to the beam's aim, or the way its wearer faces.</summary>
        public static float Facing(Player player, MupralPlayer lamp) => lamp.Aiming ? lamp.Aim : player.direction < 0 ? MathHelper.Pi : 0f;

        /// <summary>The lamp's glass, in the world: six and a half pixels out from where it turns, the way it points.</summary>
        public static Vector2 Lens(Player player, MupralPlayer lamp) => Pivot(player) + Facing(player, lamp).ToRotationVector2() * 6.5f;

        /// <summary>Where each player's lamp was last tick and which way it pointed, and how fast each was changing (per tick, eased): where it will be, looked for ahead (<see cref="Ahead"/>).</summary>
        private static readonly Vector2[] lastLens = new Vector2[Main.maxPlayers];
        private static readonly float[] lastAim = new float[Main.maxPlayers];
        private static readonly Vector2[] lensGoing = new Vector2[Main.maxPlayers];
        private static readonly float[] aimTurning = new float[Main.maxPlayers];
        private static readonly bool[] known = new bool[Main.maxPlayers];

        // ---- a ray ------------------------------------------------------------------------------

        /// <summary>One piece of a ray: the tile it is in, from and to how far along (pixels), whether in water, how much of the light's reach was used up and how much water it had come through where it begins, and how deep in a solid block it is there (pixels; less than none out of one).</summary>
        private struct Piece
        {
            public int X;
            public int Y;
            public float From;
            public float To;
            public float Used;
            public float Wet;
            public float Deep;
            public bool Water;
        }

        /// <summary>How deep a ray goes into the solid it meets, along its way (pixels), dimming to nothing: a tile.</summary>
        private const float Into = 16f;

        private static readonly Piece[] pieces = new Piece[1024];

        /// <summary>
        /// One ray from a point, the way given (a unit vector), tile by tile as a line goes through a
        /// grid: each tile it crosses, from where it goes in to where it comes out, split where it
        /// goes into or out of the water of a tile part filled (the water fills a tile from its foot)
        /// and where it goes into a solid block. It ends where it has used up <paramref name="upTo"/>
        /// of the light's reach; or, once it has gone into the solid of a block (<see cref="SolidFrom"/>),
        /// a tile's depth further along its way through solid (<see cref="Into"/>), or where it comes
        /// out of the solid first: it does not come out the other side. Its pieces are left in
        /// <see cref="pieces"/>; returns how many.
        /// </summary>
        private static int Walk(Vector2 from, Vector2 way, in Shape shape, float upTo)
        {
            int cx = (int)Math.Floor(from.X / 16f);
            int cy = (int)Math.Floor(from.Y / 16f);
            int sx = way.X > 0f ? 1 : -1;
            int sy = way.Y > 0f ? 1 : -1;
            float stepX = way.X != 0f ? 16f / Math.Abs(way.X) : float.MaxValue;
            float stepY = way.Y != 0f ? 16f / Math.Abs(way.Y) : float.MaxValue;
            float nextX = way.X > 0f ? ((cx + 1) * 16f - from.X) / way.X : way.X < 0f ? (cx * 16f - from.X) / way.X : float.MaxValue;
            float nextY = way.Y > 0f ? ((cy + 1) * 16f - from.Y) / way.Y : way.Y < 0f ? (cy * 16f - from.Y) / way.Y : float.MaxValue;
            float airRate = 1f / (16f * shape.Air);
            float waterRate = 1f / (16f * shape.Water);
            float t = 0f;
            float used = 0f;
            float wet = 0f;
            float solid = -1f;
            int n = 0;

            while (n < pieces.Length - 4 && t < Longest)
            {
                if (!WorldGen.InWorld(cx, cy, 1))
                    break;

                float leave = Math.Min(nextX, nextY);
                Tile tile = Main.tile[cx, cy];
                float enter = SolidFrom(tile, cx, cy, from, way, t, leave);

                // Out of the solid it went into: no further.
                if (solid >= 0f && enter < 0f)
                    break;

                if (solid < 0f && enter >= 0f)
                    solid = enter;

                if (solid < 0f || enter > t)
                {
                    // The open part of this tile: in water under the top of the water, if there is any.
                    float open = solid >= 0f ? enter : leave;
                    float wetFrom = float.MaxValue;
                    float wetTo = float.MinValue;

                    if (tile.LiquidAmount == 255)
                    {
                        // Full: all of it, with no sliver of dry from reckoning where the top of the water is.
                        wetFrom = t;
                        wetTo = open;
                    }
                    else if (tile.LiquidAmount > 0)
                    {
                        float top = cy * 16f + (255 - tile.LiquidAmount) / 255f * 16f;

                        if (way.Y > 0f)
                        {
                            wetFrom = Math.Max(t, (top - from.Y) / way.Y);
                            wetTo = open;
                        }
                        else if (way.Y < 0f)
                        {
                            wetFrom = t;
                            wetTo = Math.Min(open, (top - from.Y) / way.Y);
                        }
                        else if (from.Y >= top)
                        {
                            wetFrom = t;
                            wetTo = open;
                        }
                    }

                    bool ended = wetFrom < wetTo
                        ? Add(t, wetFrom, false, -1f) || Add(wetFrom, wetTo, true, -1f) || Add(wetTo, open, false, -1f)
                        : Add(t, open, false, -1f);

                    if (ended)
                        break;
                }

                if (solid >= 0f)
                {
                    // Into the solid, a tile's depth at most.
                    float from_ = Math.Max(t, solid);
                    float to = Math.Min(leave, solid + Into);

                    if (Add(from_, to, false, from_ - solid) || to < leave)
                        break;
                }

                t = leave;

                if (nextX < nextY)
                {
                    cx += sx;
                    nextX += stepX;
                }
                else
                {
                    cy += sy;
                    nextY += stepY;
                }
            }

            return n;

            // A piece of the ray in this tile; true if the light's reach is used up in it.
            bool Add(float a, float b, bool water, float deep)
            {
                if (b - a <= 0.0001f)
                    return false;

                float rate = water ? waterRate : airRate;
                float left = (upTo - used) / rate;
                bool last = b - a >= left;
                float end = last ? a + left : b;

                pieces[n++] = new Piece { X = cx, Y = cy, From = a, To = end, Used = used, Wet = wet, Deep = deep, Water = water };
                used += (end - a) * rate;

                if (water)
                    wet += (end - a) / 16f;

                return last;
            }
        }

        /// <summary>
        /// Where a ray's path through a tile, from <paramref name="a"/> to <paramref name="b"/> along it,
        /// goes into the solid of a solid block there (less than none if it does not): not a
        /// platform, nor anything not solid, nor a block turned off by an actuator; of a half block
        /// its lower half; of a slope the half on the solid side of its slant (a slope named for the
        /// corner that stays solid: down-left keeps the lower left). Each is one side of a straight
        /// line in the tile, so a straight path goes in once at most.
        /// </summary>
        private static float SolidFrom(Tile tile, int cx, int cy, Vector2 from, Vector2 way, float a, float b)
        {
            if (!tile.HasTile || tile.IsActuated || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
                return -1f;

            float x = from.X + way.X * a - cx * 16f;
            float y = from.Y + way.Y * a - cy * 16f;

            if (tile.IsHalfBlock)
                return Crossing(y - 8f, way.Y, a, b);

            switch (tile.Slope)
            {
                case SlopeType.SlopeDownLeft:
                    return Crossing(y - x, way.Y - way.X, a, b);
                case SlopeType.SlopeDownRight:
                    return Crossing(x + y - 16f, way.X + way.Y, a, b);
                case SlopeType.SlopeUpLeft:
                    return Crossing(16f - x - y, -way.X - way.Y, a, b);
                case SlopeType.SlopeUpRight:
                    return Crossing(x - y, way.X - way.Y, a, b);
                default:
                    return a;
            }
        }

        /// <summary>Where along [a, b] a straight path whose measure is <paramref name="at"/> at a and grows by <paramref name="growing"/> a pixel first reaches none or more (inside); less than none if it never does.</summary>
        private static float Crossing(float at, float growing, float a, float b)
        {
            if (at >= 0f)
                return a;

            if (growing <= 0f)
                return -1f;

            float t = a - at / growing;

            return t <= b ? t : -1f;
        }

        /// <summary>A light's brightness and colour this far along a ray (pixels), with this much of its reach used up and through this much water (tiles); faded over the last of its reach, or not (as given to the game's map, which must be as bright as it can be).</summary>
        private static Vector3 LightOf(in Shape shape, float share, float along, float used, float wet, bool fade)
        {
            float bright = shape.Bright * share * Look(spreads[shape.Kind], along) * Look(thinnings[shape.Kind], used / UsedStep);

            if (fade)
                bright *= 1f - Smooth(0.85f, 1f, used);

            float w = wet / WetStep;
            return Beige * bright * new Vector3(Look(reds, w), Look(greens, w), 1f);
        }

        /// <summary>How much of a ray's light is left this deep in the solid it went into (pixels; less than none out of it): dimming evenly to nothing a tile in.</summary>
        private static float Dim(float deep) => deep < 0f ? 1f : Math.Max(0f, 1f - deep / Into);

        /// <summary>A beam's share across it (this many degrees off its middle, of this breadth): brightest in its middle, falling off toward its edges to a little under half (0.45 and a breath at its edge); past them only as given to the game's map.</summary>
        private static float Across(float degrees, float breadth)
        {
            float u = degrees / (breadth * 0.225f);

            return 0.45f + 0.55f * (float)Math.Exp(-u * u);
        }

        // ---- the light as the game has it ----------------------------------------------------------

        /// <summary>The light given to one of the game's maps: which map, over what (the tiles the game was working on), how much in each tile, and the tiles there is any in.</summary>
        private sealed class LightRecord
        {
            public LightMap Map;
            public Rectangle Area;
            public Vector3[] Light = Array.Empty<Vector3>();
            public Rectangle Box;
            public bool Any;

            public void Clear()
            {
                Map = null;
                Any = false;
                Box = Rectangle.Empty;
            }

            public Vector3 At(int x, int y)
            {
                if (!Any || x < Area.X || y < Area.Y || x >= Area.Right || y >= Area.Bottom)
                    return Vector3.Zero;

                return Light[x - Area.X + (y - Area.Y) * Area.Width];
            }
        }

        /// <summary>The last three maps given light (the game has two it turns between; the newest given each is the one in it).</summary>
        private static readonly LightRecord[] records = { new LightRecord(), new LightRecord(), new LightRecord() };
        private static int newest = -1;

        private static LightRecord RecordOf(LightMap map)
        {
            if (map == null)
                return null;

            for (int k = 0; k < records.Length; k++)
            {
                LightRecord record = records[((newest - k) % records.Length + records.Length) % records.Length];

                if (ReferenceEquals(record.Map, map))
                    return record.Any ? record : null;
            }

            return null;
        }

        /// <summary>One lamp's light for the game's map, tile by tile about it, and the same a tile further each way (<see cref="Lamp"/>).</summary>
        private const int LampSide = Reach * 2 + 1;
        private static readonly Vector3[] lampLight = new Vector3[LampSide * LampSide];
        private static readonly Vector3[] lampWider = new Vector3[LampSide * LampSide];
        private static int lampX;
        private static int lampY;

        /// <summary>The cells of those any light was laid in, from and to (both kept): only these are gone over after. None while the first is past the last.</summary>
        private static int litLeft, litTop, litRight, litBottom;

        /// <summary>
        /// After the game has spread its own lights: every lamp's light, added where it falls, before
        /// the game shows it; and what was added, kept, for the pictures that keep only what the rays
        /// lay (<see cref="Prepare"/>).
        /// </summary>
        private static void AfterBlur(On_LightingEngine.orig_ProcessBlur orig, LightingEngine self)
        {
            orig(self);

            engine = self;
            newest = (newest + 1) % records.Length;

            LightRecord record = records[newest];
            record.Clear();

            if (Main.gameMenu || workingMap.GetValue(self) is not LightMap map || workingArea.GetValue(self) is not Rectangle area || area.Width <= 0 || area.Height <= 0)
                return;

            record.Map = map;
            record.Area = area;

            int size = area.Width * area.Height;

            if (record.Light.Length < size)
                record.Light = new Vector3[size];
            else
                Array.Clear(record.Light, 0, size);

            int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];

                if (!player.active || player.dead)
                    continue;

                MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();

                if (!lamp.Lit)
                    continue;

                Vector2 lens = Lens(player, lamp);

                if (!area.Intersects(new Rectangle((int)(lens.X / 16f) - Reach, (int)(lens.Y / 16f) - Reach, LampSide, LampSide)))
                    continue;

                Lamp(player, lamp, true);

                for (int gy = litTop; gy <= litBottom; gy++)
                {
                    int y = lampY + gy - area.Y;

                    if (y < 0 || y >= area.Height)
                        continue;

                    for (int gx = litLeft; gx <= litRight; gx++)
                    {
                        Vector3 light = lampWider[gx + gy * LampSide];

                        if (light.X + light.Y + light.Z < 0.002f)
                            continue;

                        int x = lampX + gx - area.X;

                        if (x < 0 || x >= area.Width)
                            continue;

                        record.Light[x + y * area.Width] += light;
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }
            }

            if (right < left)
                return;

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    Vector3 light = record.Light[x + y * area.Width];

                    if (light.X + light.Y + light.Z >= 0.002f)
                        map[x, y] = map[x, y] + light;
                }
            }

            record.Box = new Rectangle(area.X + left, area.Y + top, right - left + 1, bottom - top + 1);
            record.Any = true;
        }

        /// <summary>
        /// One lamp's light, tile by tile about it (<see cref="lampLight"/>, from <see cref="lampX"/>,
        /// <see cref="lampY"/>): each tile as bright as any of its rays is anywhere in it (a ray is
        /// brightest where it goes into a tile). For the game's map (<paramref name="ahead"/>): along
        /// the coarser rays, past where it ends, unfaded, the beam a degree past its edges, and as
        /// it will be in a few ticks too (<see cref="Ahead"/>), the brightest of each tile kept; and
        /// then a tile more each way (<see cref="lampWider"/>), so that the game has it everywhere
        /// it can be seen in the next few frames. Otherwise (under the game's old lightings) as it
        /// is, and the same in both.
        /// </summary>
        private static void Lamp(Player player, MupralPlayer lamp, bool ahead)
        {
            Vector2 lens = Lens(player, lamp);
            int who = player.whoAmI;

            lampX = (int)(lens.X / 16f) - Reach;
            lampY = (int)(lens.Y / 16f) - Reach;
            Array.Clear(lampLight);
            litLeft = litTop = LampSide;
            litRight = litBottom = -1;

            int states = ahead ? Ahead.Length : 1;
            Vector2 lastFrom = default;
            float lastAim = 0f;

            for (int s = 0; s < states; s++)
            {
                int ticks = ahead ? Ahead[s] : 0;
                Vector2 from = lens + (known[who] ? lensGoing[who] * ticks : Vector2.Zero);
                float aim = lamp.Aim + (known[who] ? aimTurning[who] * ticks : 0f);

                // As it was a few ticks before (not going, not turning): what is laid already is all there is.
                bool still = s > 0 && Vector2.DistanceSquared(from, lastFrom) < 0.01f;
                bool steady = still && Math.Abs(aim - lastAim) < 0.0001f;

                lastFrom = from;
                lastAim = aim;

                switch (lamp.Kit)
                {
                    case MupralKit.Glow:
                    {
                        if (still)
                            break;

                        int rays = ahead ? GlowRaysGame : GlowRays;

                        for (int i = 0; i < rays; i++)
                            Lay(from, MathHelper.TwoPi * i / rays, Glow, 1f, ahead ? ReachGame : 1f, !ahead);

                        break;
                    }
                    case MupralKit.Beam:
                    {
                        int near = ahead ? NearRaysGame : NearRays;

                        if (!still)
                        {
                            for (int i = 0; i < near; i++)
                                Lay(from, MathHelper.TwoPi * i / near, Near, 1f, ahead ? NearReachGame : 1f, !ahead);
                        }

                        if (!steady)
                            LayBeam(from, aim, BeamDegrees, ahead);

                        break;
                    }
                    default:
                    {
                        int wide = ahead ? WideRaysGame : WideRays;

                        if (!still)
                        {
                            for (int i = 0; i < wide; i++)
                                Lay(from, MathHelper.TwoPi * i / wide, Wide, 1f, ahead ? ReachGame : 1f, !ahead);
                        }

                        if (!steady)
                            LayBeam(from, aim, SuitBeamDegrees, ahead);

                        break;
                    }
                }
            }

            if (!ahead)
            {
                Array.Copy(lampLight, lampWider, lampLight.Length);
                return;
            }

            // A tile more each way, a twentieth brighter: never less than the rays lay anywhere it can
            // be seen. Over the cells lit and a cell about them; none else has any.
            Array.Clear(lampWider);

            if (litRight < litLeft)
                return;

            litLeft = Math.Max(0, litLeft - 1);
            litTop = Math.Max(0, litTop - 1);
            litRight = Math.Min(LampSide - 1, litRight + 1);
            litBottom = Math.Min(LampSide - 1, litBottom + 1);

            for (int gy = litTop; gy <= litBottom; gy++)
            {
                for (int gx = litLeft; gx <= litRight; gx++)
                {
                    Vector3 most = Vector3.Zero;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int y = gy + dy;

                        if (y < 0 || y >= LampSide)
                            continue;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = gx + dx;

                            if (x >= 0 && x < LampSide)
                                most = Vector3.Max(most, lampLight[x + y * LampSide]);
                        }
                    }

                    lampWider[gx + gy * LampSide] = most * 1.05f;
                }
            }
        }

        /// <summary>A beam's rays into <see cref="lampLight"/>: an eighth of a degree apart (for the game's map, half a degree, and a degree past each edge).</summary>
        private static void LayBeam(Vector2 from, float aim, float breadth, bool ahead)
        {
            float half = breadth * 0.5f + (ahead ? SpotPastGame : 0f);
            float step = ahead ? SpotStepGame : 1f / 8f;
            int spot = (int)Math.Round(half * 2f / step) + 1;

            for (int i = 0; i < spot; i++)
            {
                float degrees = -half + i * step;

                Lay(from, aim + MathHelper.ToRadians(degrees), Spot, Across(degrees, breadth), ahead ? ReachGame : 1f, !ahead);
            }
        }

        /// <summary>One ray's light into <see cref="lampLight"/>, each tile it crosses as bright as it is where it goes in.</summary>
        private static void Lay(Vector2 from, float angle, in Shape shape, float share, float upTo, bool fade)
        {
            int n = Walk(from, angle.ToRotationVector2(), shape, upTo);

            for (int s = 0; s < n; s++)
            {
                int gx = pieces[s].X - lampX;
                int gy = pieces[s].Y - lampY;

                if (gx < 0 || gy < 0 || gx >= LampSide || gy >= LampSide)
                    continue;

                ref Vector3 cell = ref lampLight[gx + gy * LampSide];
                cell = Vector3.Max(cell, LightOf(shape, share, pieces[s].From, pieces[s].Used, pieces[s].Wet, fade) * Dim(pieces[s].Deep));
                litLeft = Math.Min(litLeft, gx);
                litTop = Math.Min(litTop, gy);
                litRight = Math.Max(litRight, gx);
                litBottom = Math.Max(litBottom, gy);
            }
        }

        /// <summary>
        /// Every tick: where each lit lamp is and points, and how fast that is changing (eased, a
        /// jump of more than four tiles in a tick taken for a teleport, not a going); and under the
        /// game's two old lightings (or if its engine is not as known), the light handed to the game
        /// as its own lights are, to be spread as they are.
        /// </summary>
        public override void PostUpdatePlayers()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            bool handed = !Lighting.UsingNewLighting || workingMap == null || workingArea == null || activeMap == null;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];
                MupralPlayer lamp = player.active && !player.dead ? player.GetModPlayer<MupralPlayer>() : null;

                if (lamp == null || !lamp.Lit)
                {
                    known[p] = false;
                    continue;
                }

                Vector2 lens = Lens(player, lamp);
                float aim = lamp.Aim;

                if (known[p])
                {
                    Vector2 moved = lens - lastLens[p];

                    if (moved.LengthSquared() > 64f * 64f)
                        moved = Vector2.Zero;

                    lensGoing[p] = Vector2.Lerp(lensGoing[p], moved, 0.5f);
                    aimTurning[p] = MathHelper.Clamp(MathHelper.Lerp(aimTurning[p], MathHelper.WrapAngle(aim - lastAim[p]), 0.5f), -0.2f, 0.2f);
                }
                else
                {
                    lensGoing[p] = Vector2.Zero;
                    aimTurning[p] = 0f;
                    known[p] = true;
                }

                lastLens[p] = lens;
                lastAim[p] = aim;

                if (!handed)
                    continue;

                Lamp(player, lamp, false);

                for (int gy = litTop; gy <= litBottom; gy++)
                {
                    for (int gx = litLeft; gx <= litRight; gx++)
                    {
                        Vector3 light = lampLight[gx + gy * LampSide];

                        if (light.X + light.Y + light.Z >= 0.003f)
                            Lighting.AddLight(lampX + gx, lampY + gy, light.X, light.Y, light.Z);
                    }
                }
            }
        }

        // ---- the fine rays, every frame ------------------------------------------------------

        /// <summary>
        /// One lamp's fine rays as they go this frame (<see cref="Prepare"/>): its glass, its rays in
        /// one or two groups (the glow or the little glow all about; and the beam), each ray's angle,
        /// share and end, and along each where it goes into each tile, and into and out of water
        /// (how far, how much of its reach used, how much water come through, and whether in water
        /// from there).
        /// </summary>
        private sealed class Fan
        {
            public int Who;
            public Vector2 Lens;

            /// <summary>Its lights (the lamp's kit), its beam's breadth in degrees (none without one), and where it points.</summary>
            public MupralKit Kit;
            public float Degrees;
            public float Aim;

            /// <summary>The tiles its rays reach, the glass among them.</summary>
            public Rectangle Box;

            public int Groups;
            public readonly int[] GroupFirst = new int[2];
            public readonly int[] GroupCount = new int[2];
            public readonly int[] GroupShape = new int[2];
            public readonly bool[] GroupAbout = new bool[2];
            public readonly float[] GroupAngle = new float[2];
            public readonly float[] GroupStep = new float[2];

            /// <summary>Room for the most rays a lamp has: the HEATR's, 1440 about and 361 across its beam (<see cref="MostRays"/>).</summary>
            public int Rays;
            public float[] Angle = new float[MostRays];
            public Vector2[] Way = new Vector2[MostRays];
            public float[] Share = new float[MostRays];
            public float[] End = new float[MostRays];
            public int[] First = new int[MostRays];
            public int[] Count = new int[MostRays];

            /// <summary>Where each ray was last looked up along its nodes (<see cref="RayLight"/>).</summary>
            public int[] Cursor = new int[MostRays];

            /// <summary>How far each ray goes as it began before that changes (into or out of water, into solid, or its end; none if it began in a solid), and whether it began in water: as far as that, every ray that began alike is lit alike.</summary>
            public float[] Same = new float[MostRays];
            public bool[] WetFirst = new bool[MostRays];

            /// <summary>Whether each ray goes into a solid.</summary>
            public bool[] Hits = new bool[MostRays];

            public int Nodes;
            public float[] Along = new float[32768];
            public float[] Used = new float[32768];
            public float[] Wet = new float[32768];
            public float[] Deep = new float[32768];
            public bool[] Water = new bool[32768];

            public void Room(int more)
            {
                if (Nodes + more <= Along.Length)
                    return;

                int size = Math.Max(Along.Length * 2, Nodes + more);
                Array.Resize(ref Along, size);
                Array.Resize(ref Used, size);
                Array.Resize(ref Wet, size);
                Array.Resize(ref Deep, size);
                Array.Resize(ref Water, size);
            }
        }

        /// <summary>The most rays a lamp has.</summary>
        private const int MostRays = 2048;

        private static readonly List<Fan> fans = new List<Fan>();
        private static int fanCount;

        private static Fan FanOf(int who)
        {
            for (int f = 0; f < fanCount; f++)
            {
                if (fans[f].Who == who)
                    return fans[f];
            }

            return null;
        }

        /// <summary>One lamp's fine rays, for this frame.</summary>
        private static Fan Trace(Player player, MupralPlayer lamp)
        {
            if (fanCount == fans.Count)
                fans.Add(new Fan());

            Fan fan = fans[fanCount++];
            fan.Who = player.whoAmI;
            fan.Lens = Lens(player, lamp);
            fan.Kit = lamp.Kit;
            fan.Degrees = lamp.Kit == MupralKit.Suit ? SuitBeamDegrees : lamp.Kit == MupralKit.Beam ? BeamDegrees : 0f;
            fan.Aim = lamp.Aim;
            fan.Rays = 0;
            fan.Nodes = 0;
            fan.Groups = 0;

            int lx = (int)(fan.Lens.X / 16f), ly = (int)(fan.Lens.Y / 16f);
            fan.Box = new Rectangle(lx, ly, 1, 1);

            switch (fan.Kit)
            {
                case MupralKit.Glow:
                    Group(fan, GlowShape, 0f, MathHelper.TwoPi / GlowRays, GlowRays, true);
                    break;
                case MupralKit.Beam:
                    Group(fan, NearShape, 0f, MathHelper.TwoPi / NearRays, NearRays, true);
                    Group(fan, SpotShape, lamp.Aim - MathHelper.ToRadians(BeamDegrees * 0.5f), MathHelper.ToRadians(BeamDegrees) / (SpotRays - 1), SpotRays, false);
                    break;
                default:
                    Group(fan, WideShape, 0f, MathHelper.TwoPi / WideRays, WideRays, true);
                    Group(fan, SpotShape, lamp.Aim - MathHelper.ToRadians(SuitBeamDegrees * 0.5f), MathHelper.ToRadians(SuitBeamDegrees) / (SuitSpotRays - 1), SuitSpotRays, false);
                    break;
            }

            return fan;
        }

        /// <summary>
        /// One group of a lamp's rays: every other ray traced (and the last of a beam), then each one
        /// between: as the two either side where those two go alike, from the glass through the
        /// same, into no solid, to the same end; else traced as well. No solid's shape fits between
        /// two rays that close and touches neither (they are under five pixels apart where they go
        /// furthest, a half block eight across at its thinnest, a slope eleven), so the one between
        /// goes as they do.
        /// </summary>
        private static void Group(Fan fan, int shapeKind, float first, float step, int count, bool about)
        {
            int g = fan.Groups++;
            Shape shape = ShapeOf(shapeKind);
            int start = fan.Rays;

            fan.GroupFirst[g] = start;
            fan.GroupCount[g] = count;
            fan.GroupShape[g] = shapeKind;
            fan.GroupAbout[g] = about;
            fan.GroupAngle[g] = first;
            fan.GroupStep[g] = step;
            fan.Rays += count;

            for (int i = 0; i < count; i += 2)
                TraceRay(fan, shape, i);

            if (!about && count % 2 == 0)
                TraceRay(fan, shape, count - 1);

            for (int i = 1; i < count; i += 2)
            {
                if (!about && i == count - 1)
                    continue;

                int left = start + i - 1, right = start + (i + 1) % count;

                if (Plain(fan, left) && Plain(fan, right) && fan.WetFirst[left] == fan.WetFirst[right] && Math.Abs(fan.End[left] - fan.End[right]) <= 0.75f)
                    LikeRay(fan, i, left);
                else
                    TraceRay(fan, shape, i);
            }
        }

        /// <summary>Whether a ray goes from the glass through the same to its end, into no solid.</summary>
        private static bool Plain(Fan fan, int ray) => fan.Count[ray] == 2 && fan.Same[ray] >= fan.End[ray];

        /// <summary>The way, angle and share of the ray counted <paramref name="i"/> of the group being made (the last).</summary>
        private static int Aim(Fan fan, int i, out Vector2 way)
        {
            int g = fan.Groups - 1;
            int count = fan.GroupCount[g];
            int ray = fan.GroupFirst[g] + i;
            float angle = fan.GroupAngle[g] + fan.GroupStep[g] * i;

            way = angle.ToRotationVector2();
            fan.Angle[ray] = angle;
            fan.Way[ray] = way;
            fan.Share[ray] = fan.GroupAbout[g] ? 1f : Across((i - (count - 1) * 0.5f) / (count - 1) * fan.Degrees, fan.Degrees);
            return ray;
        }

        /// <summary>The ray counted <paramref name="i"/> going as <paramref name="like"/> does (its own way, angle and share).</summary>
        private static void LikeRay(Fan fan, int i, int like)
        {
            int ray = Aim(fan, i, out Vector2 way);

            fan.First[ray] = fan.First[like];
            fan.Count[ray] = fan.Count[like];
            fan.Cursor[ray] = fan.First[like];
            fan.End[ray] = fan.End[like];
            fan.Same[ray] = fan.Same[like];
            fan.WetFirst[ray] = fan.WetFirst[like];
            fan.Hits[ray] = fan.Hits[like];

            Vector2 reach = fan.Lens + way * fan.End[ray];
            fan.Box = Rectangle.Union(fan.Box, new Rectangle((int)(reach.X / 16f), (int)(reach.Y / 16f), 1, 1));
        }

        /// <summary>The ray counted <paramref name="i"/> traced (<see cref="Walk"/>), its pieces kept as nodes.</summary>
        private static void TraceRay(Fan fan, in Shape shape, int i)
        {
            int ray = Aim(fan, i, out Vector2 way);
            int n = Walk(fan.Lens, way, shape, 1f);

            fan.Room(n + 1);
            fan.First[ray] = fan.Nodes;
            fan.Cursor[ray] = fan.Nodes;

            // A node where it goes into or out of water, or into solid: between, it goes on
            // through the same, and its light is reckoned from the node's start just as from the
            // start of each tile's piece (the pieces follow on one from another).
            fan.Hits[ray] = false;

            for (int s = 0; s < n; s++)
            {
                fan.Hits[ray] |= pieces[s].Deep >= 0f;

                if (s > 0 && pieces[s].Water == pieces[s - 1].Water && pieces[s].Deep >= 0f == pieces[s - 1].Deep >= 0f)
                    continue;

                int node = fan.Nodes++;
                fan.Along[node] = pieces[s].From;
                fan.Used[node] = pieces[s].Used;
                fan.Wet[node] = pieces[s].Wet;
                fan.Deep[node] = pieces[s].Deep;
                fan.Water[node] = pieces[s].Water;
            }

            int nodes = fan.Nodes - fan.First[ray];
            fan.Count[ray] = nodes + 1;

            // Where it ends.
            float end = 0f, used = 0f, wet = 0f;

            if (n > 0)
            {
                Piece last = pieces[n - 1];
                float length = last.To - last.From;

                end = last.To;
                used = last.Used + length / (16f * (last.Water ? shape.Water : shape.Air));
                wet = last.Wet + (last.Water ? length / 16f : 0f);
            }

            int tail = fan.Nodes++;
            fan.Along[tail] = end;
            fan.Used[tail] = used;
            fan.Wet[tail] = wet;
            fan.Deep[tail] = -1f;
            fan.Water[tail] = false;
            fan.End[ray] = end;

            // As far as it goes as it began: plainly from the glass, out of any solid.
            int start = fan.First[ray];
            bool plain = nodes > 0 && fan.Along[start] < 0.001f && fan.Deep[start] < 0f;
            fan.WetFirst[ray] = nodes > 0 && fan.Water[start];
            fan.Same[ray] = !plain ? 0f : nodes > 1 ? fan.Along[start + 1] : end;

            Vector2 reach = fan.Lens + way * end;
            fan.Box = Rectangle.Union(fan.Box, new Rectangle((int)(reach.X / 16f), (int)(reach.Y / 16f), 1, 1));
        }

        /// <summary>A ray's light this far along it (pixels, no further than it goes), walking its nodes from <paramref name="node"/> (kept between calls, as the distance only grows).</summary>
        private static Vector3 RayLight(Fan fan, int ray, in Shape shape, float along, ref int node)
        {
            NodeAt(fan, ray, along, ref node);

            float gone = Math.Max(0f, along - fan.Along[node]);
            bool water = fan.Water[node];
            float used = fan.Used[node] + gone / (16f * (water ? shape.Water : shape.Air));
            float wet = fan.Wet[node] + (water ? gone / 16f : 0f);
            float deep = fan.Deep[node];

            return LightOf(shape, fan.Share[ray], along, used, wet, true) * Dim(deep < 0f ? deep : deep + gone);
        }

        /// <summary>The node of a ray that holds this far along it (from <paramref name="node"/>, kept between calls, back or on).</summary>
        private static void NodeAt(Fan fan, int ray, float along, ref int node)
        {
            int first = fan.First[ray];
            int last = first + fan.Count[ray] - 1;

            if (node < first || node >= last)
                node = first;

            while (node > first && fan.Along[node] > along)
                node--;

            while (node < last - 1 && fan.Along[node + 1] <= along)
                node++;
        }

        /// <summary>One group's light at a point: the nearest of its rays, if it reaches that far (none in a shadow, none past the beam's edges).</summary>
        private static Vector3 GroupLight(Fan fan, int g, Vector2 at)
        {
            Vector2 off = at - fan.Lens;
            float far = off.Length();
            int count = fan.GroupCount[g];
            int i;

            if (fan.GroupAbout[g])
            {
                float angle = far > 0.001f ? off.ToRotation() : 0f;

                if (angle < 0f)
                    angle += MathHelper.TwoPi;

                i = (int)Math.Round(angle / fan.GroupStep[g]) % count;
            }
            else
            {
                if (far < 0.001f)
                    return Vector3.Zero;

                float k = MathHelper.WrapAngle(off.ToRotation() - fan.GroupAngle[g]) / fan.GroupStep[g];

                if (k < -0.5f || k > count - 0.5f)
                    return Vector3.Zero;

                i = Math.Clamp((int)Math.Round(k), 0, count - 1);
            }

            int ray = fan.GroupFirst[g] + i;

            if (far > fan.End[ray])
                return Vector3.Zero;

            int node = fan.First[ray];
            return RayLight(fan, ray, ShapeOf(fan.GroupShape[g]), far, ref node);
        }

        /// <summary>A lamp's light at a point: the brighter of its groups there (the beam and the little glow left with it are not added, as before).</summary>
        private static Vector3 FanLight(Fan fan, Vector2 at, int except = -1)
        {
            Vector3 light = Vector3.Zero;

            for (int g = 0; g < fan.Groups; g++)
            {
                if (g != except)
                    light = Vector3.Max(light, GroupLight(fan, g, at));
            }

            return light;
        }

        /// <summary>Every other lamp's light at a point, added.</summary>
        private static Vector3 OthersLight(Fan fan, Vector2 at)
        {
            Vector3 light = Vector3.Zero;

            for (int f = 0; f < fanCount; f++)
            {
                if (fans[f] != fan)
                    light += FanLight(fans[f], at);
            }

            return light;
        }

        // ---- the pictures, every frame -------------------------------------------------------------

        /// <summary>
        /// The pictures, world-anchored (one pixel to a pixel of the world, from <see cref="anchor"/>),
        /// each over the same tiles: how much of the game's light each pixel keeps, for the game's
        /// blocks, walls, backdrop and water (<see cref="cutTarget"/>); the same put right for what
        /// stands on the blocks (<see cref="objectsTarget"/>); and the Vast Ocean's water lit by the
        /// rays, to be added over its sea (<see cref="litTarget"/>).
        /// </summary>
        private static RenderTarget2D cutTarget;
        private static RenderTarget2D objectsTarget;
        private static RenderTarget2D litTarget;
        private static Vector2 anchor;
        private static int usedWide;
        private static int usedHigh;
        private static bool cutReady;
        private static bool litReady;

        /// <summary>The tiles the pictures are of (world), and about them: all the game's light in each tile, the lamps' in it, and at each corner the mean of the four tiles about it, of all and of the lamps', as the game spreads light smoothly over a tile (Lighting.GetCornerColors).</summary>
        private static Rectangle box;
        private static Vector3[] tileAll = Array.Empty<Vector3>();
        private static Vector3[] tileOurs = Array.Empty<Vector3>();
        private static Vector3[] cornerAll = Array.Empty<Vector3>();
        private static Vector3[] cornerOurs = Array.Empty<Vector3>();

        /// <summary>The game's brightness, which it draws all its light at (Lighting.GlobalBrightness: 1.2, or 1 for the blind).</summary>
        private static float brightness = 1.2f;

        /// <summary>The largest the pictures are made, a side (pixels).</summary>
        private const int Largest = 2048;

        /// <summary>
        /// Pieces of three corners to draw into one of the pictures, as the game draws its tiles
        /// (Main.tileBatch begun first, which sets the picture's states and the effect its sprites
        /// are drawn with): each corner where it is in the picture and its colour, and for the rays'
        /// corners where it is in the world and its light; the pieces, three corners each, counted
        /// from the first corner of their run; and the runs, no more corners to each than one draw
        /// can count (<see cref="MostInRun"/>). A corner made in a run before is copied into the
        /// one being filled when a piece wants it.
        /// </summary>
        private sealed class Mesh
        {
            public VertexPositionColorTexture[] Corners = new VertexPositionColorTexture[8192];
            public Vector2[] At = new Vector2[8192];
            public Vector3[] Light = new Vector3[8192];
            public int Count;
            public short[] Pieces = new short[24576];
            public int PieceCorners;
            public readonly List<Run> Runs = new List<Run>();
            private int runCorner;
            private int runPiece;

            public void Clear()
            {
                Count = 0;
                PieceCorners = 0;
                Runs.Clear();
                runCorner = 0;
                runPiece = 0;
            }

            /// <summary>A corner: where in the picture, where in the world and its light (the rays'), and its colour.</summary>
            public int Add(Vector2 position, Vector2 at, Vector3 light, Color colour)
            {
                if (Count - runCorner >= MostInRun)
                    End();

                if (Count == Corners.Length)
                {
                    Array.Resize(ref Corners, Corners.Length * 2);
                    Array.Resize(ref At, At.Length * 2);
                    Array.Resize(ref Light, Light.Length * 2);
                }

                Corners[Count].Position = new Vector3(position.X, position.Y, 0f);
                Corners[Count].Color = colour;
                Corners[Count].TextureCoordinate = Vector2.Zero;
                At[Count] = at;
                Light[Count] = light;
                return Count++;
            }

            public void Piece(int a, int b, int c)
            {
                if (Count - runCorner > MostInRun - 3)
                    End();

                a = Use(a);
                b = Use(b);
                c = Use(c);

                if (PieceCorners + 3 > Pieces.Length)
                    Array.Resize(ref Pieces, Pieces.Length * 2);

                Pieces[PieceCorners++] = (short)(a - runCorner);
                Pieces[PieceCorners++] = (short)(b - runCorner);
                Pieces[PieceCorners++] = (short)(c - runCorner);
            }

            /// <summary>Four corners as two pieces, split as the game splits its tiles' (top left, top right, bottom left; top right, bottom right, bottom left).</summary>
            public void Quad(int topLeft, int topRight, int bottomLeft, int bottomRight)
            {
                Piece(topLeft, topRight, bottomLeft);
                Piece(topRight, bottomRight, bottomLeft);
            }

            private int Use(int corner)
            {
                if (corner >= runCorner)
                    return corner;

                int copy = Add(new Vector2(Corners[corner].Position.X, Corners[corner].Position.Y), At[corner], Light[corner], Corners[corner].Color);
                return copy;
            }

            /// <summary>The run being filled ended, and the next begun.</summary>
            public void End()
            {
                if (PieceCorners > runPiece)
                    Runs.Add(new Run { Vertex = runCorner, Vertices = Count - runCorner, Index = runPiece, Triangles = (PieceCorners - runPiece) / 3 });

                runCorner = Count;
                runPiece = PieceCorners;
            }

            /// <summary>Into the picture being drawn (its batch begun), with the white pixel.</summary>
            public void Draw(GraphicsDevice device, Texture2D pixel)
            {
                device.Textures[0] = pixel;

                foreach (Run run in Runs)
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, Corners, run.Vertex, run.Vertices, Pieces, run.Index, run.Triangles);
            }
        }

        private struct Run
        {
            public int Vertex;
            public int Vertices;
            public int Index;
            public int Triangles;
        }

        private const int MostInRun = 32000;

        /// <summary>Every lamp's fine rays laid this frame (<see cref="LayFans"/>), and the tiles' pieces of the picture being made (<see cref="DrawKept"/> and the rest).</summary>
        private static readonly Mesh laid = new Mesh();
        private static readonly Mesh tiles = new Mesh();

        /// <summary>
        /// Every frame, before anything is drawn (Main.OnPreDraw, before the game makes its pictures of
        /// the world): every lit lamp's fine rays near the screen; and the pictures of how much light
        /// each pixel keeps, from the light the game was given for the map it will draw with, and of
        /// the sea's water lit. Nothing has been drawn to the screen this frame yet, so the pictures
        /// can be drawn into here (FNA clears a picture of the game's kind each time one is set to be
        /// drawn into, the screen among them).
        /// </summary>
        private static void Prepare(GameTime time)
        {
            cutReady = false;
            litReady = false;
            fanCount = 0;

            if (Main.dedServ || Main.gameMenu || broken)
                return;

            GraphicsDevice device = Main.instance.GraphicsDevice;
            RenderTargetBinding[] previous = null;

            try
            {
                Rectangle seen = SeenTiles();

                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player player = Main.player[p];

                    if (!player.active || player.dead)
                        continue;

                    MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();

                    if (!lamp.Lit)
                        continue;

                    Vector2 lens = Lens(player, lamp);

                    if (seen.Intersects(new Rectangle((int)(lens.X / 16f) - Reach, (int)(lens.Y / 16f) - Reach, LampSide, LampSide)))
                        Trace(player, lamp);
                }

                // The game draws its pictures of the world only under its newer lightings (Color, White); under the
                // two old ones it draws straight to the screen, and the lamps' light is the game's, tile by tile.
                LightRecord record = engine != null && activeMap != null && !Main.drawToScreen ? RecordOf(activeMap.GetValue(engine) as LightMap) : null;
                bool sea = SubworldKeeper.Current is VastOcean;
                Rectangle cut = record != null ? Rectangle.Intersect(Inflate(record.Box, 1), seen) : Rectangle.Empty;
                Rectangle lit = Rectangle.Empty;

                if (sea)
                {
                    for (int f = 0; f < fanCount; f++)
                        lit = lit.IsEmpty ? fans[f].Box : Rectangle.Union(lit, fans[f].Box);

                    lit = Rectangle.Intersect(Inflate(lit, 1), seen);
                }

                box = cut.IsEmpty ? lit : lit.IsEmpty ? cut : Rectangle.Union(cut, lit);

                if (box.IsEmpty)
                    return;

                // No larger than the pictures are made: about the screen's middle.
                int most = Largest / 16;

                if (box.Width > most || box.Height > most)
                {
                    Point middle = new Point((int)((Main.screenPosition.X + Main.screenWidth * 0.5f) / 16f), (int)((Main.screenPosition.Y + Main.screenHeight * 0.5f) / 16f));
                    box = Rectangle.Intersect(box, new Rectangle(middle.X - most / 2, middle.Y - most / 2, most, most));

                    if (box.IsEmpty)
                        return;
                }

                anchor = new Vector2(box.X * 16f, box.Y * 16f);
                usedWide = box.Width * 16;
                usedHigh = box.Height * 16;
                brightness = Lighting.GlobalBrightness;

                if (record != null && !cut.IsEmpty)
                {
                    Ensure(ref cutTarget, device);
                    Ensure(ref objectsTarget, device);
                }

                if (sea && fanCount > 0)
                    Ensure(ref litTarget, device);

                Corners(record);
                LayFans();

                previous = device.GetRenderTargets();
                Texture2D pixel = TextureAssets.MagicPixel.Value;

                if (record != null && !cut.IsEmpty)
                {
                    // How much each pixel keeps: out of the rays, what the game's light there is
                    // besides the lamps'; in them, that and the lamps' as the rays lay it.
                    device.SetRenderTarget(cutTarget);
                    device.Clear(Color.White);
                    Main.tileBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    DrawKept(device, pixel);
                    Main.tileBatch.End();
                    Main.tileBatch.Begin(SpriteSortMode.Deferred, brighter, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    DrawLaid(device, pixel, false);
                    Main.tileBatch.End();

                    // The same for what stands on the blocks, put right for its one light to a tile.
                    device.SetRenderTarget(objectsTarget);
                    device.Clear(Color.White);
                    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    Main.spriteBatch.Draw(cutTarget, Vector2.Zero, new Rectangle(0, 0, usedWide, usedHigh), Color.White);
                    Main.spriteBatch.End();
                    Main.tileBatch.Begin(SpriteSortMode.Deferred, keepTwice, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    DrawTileByTile(device, pixel);
                    Main.tileBatch.End();

                    cutReady = true;
                }

                if (sea && fanCount > 0)
                {
                    // The sea's water the rays pass, lit; nothing where there is no water.
                    device.SetRenderTarget(litTarget);
                    device.Clear(Color.Transparent);
                    Main.tileBatch.Begin(SpriteSortMode.Deferred, brighter, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    DrawLaid(device, pixel, true);
                    Main.tileBatch.End();
                    Main.tileBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    DrawDry(device, pixel);
                    Main.tileBatch.End();

                    litReady = true;
                }
            }
            catch (Exception e)
            {
                ModContent.GetInstance<MupralLight>().Fail(e);
            }
            finally
            {
                if (previous != null)
                    device.SetRenderTargets(previous);
            }
        }

        /// <summary>The tiles the game's pictures of the world can cover this frame: the screen and the game's margin about it (Main.offScreenRange), and a little more for the camera's going.</summary>
        private static Rectangle SeenTiles()
        {
            int margin = Main.offScreenRange + 64;

            return new Rectangle((int)((Main.screenPosition.X - margin) / 16f), (int)((Main.screenPosition.Y - margin) / 16f),
                (Main.screenWidth + 2 * margin) / 16 + 2, (Main.screenHeight + 2 * margin) / 16 + 2);
        }

        private static Rectangle Inflate(Rectangle area, int by) => area.IsEmpty ? area : new Rectangle(area.X - by, area.Y - by, area.Width + 2 * by, area.Height + 2 * by);

        private static void Ensure(ref RenderTarget2D target, GraphicsDevice device)
        {
            if (target != null && !target.IsDisposed && target.Width >= usedWide && target.Height >= usedHigh)
                return;

            int wide = Math.Max(target != null && !target.IsDisposed ? target.Width : 0, (usedWide + 127) / 128 * 128);
            int high = Math.Max(target != null && !target.IsDisposed ? target.Height : 0, (usedHigh + 127) / 128 * 128);

            target?.Dispose();
            target = new RenderTarget2D(device, Math.Min(wide, Largest), Math.Min(high, Largest), false, SurfaceFormat.Color, DepthFormat.None);
        }

        /// <summary>The light in and about <see cref="box"/>: all the game has in each tile (the map it will draw with), the lamps' in it, and at each corner the mean of the four tiles about it.</summary>
        private static void Corners(LightRecord record)
        {
            int wide = box.Width + 2, high = box.Height + 2;
            int tiles = wide * high, corners = (box.Width + 1) * (box.Height + 1);

            if (tileAll.Length < tiles)
            {
                tileAll = new Vector3[tiles];
                tileOurs = new Vector3[tiles];
            }

            if (cornerAll.Length < corners)
            {
                cornerAll = new Vector3[corners];
                cornerOurs = new Vector3[corners];
            }

            for (int j = 0; j < high; j++)
            {
                for (int i = 0; i < wide; i++)
                {
                    int x = box.X - 1 + i, y = box.Y - 1 + j;

                    tileAll[i + j * wide] = record != null && engine != null ? engine.GetColor(x, y) : Vector3.Zero;
                    tileOurs[i + j * wide] = record != null ? record.At(x, y) : Vector3.Zero;
                }
            }

            for (int j = 0; j <= box.Height; j++)
            {
                for (int i = 0; i <= box.Width; i++)
                {
                    int a = i + j * wide, b = a + 1, c = a + wide, d = c + 1;

                    cornerAll[i + j * (box.Width + 1)] = (tileAll[a] + tileAll[b] + tileAll[c] + tileAll[d]) * 0.25f;
                    cornerOurs[i + j * (box.Width + 1)] = (tileOurs[a] + tileOurs[b] + tileOurs[c] + tileOurs[d]) * 0.25f;
                }
            }
        }

        /// <summary>All the game's light at a point and the lamps' in it, spread smoothly between the corners as the game spreads it.</summary>
        private static void LightThere(Vector2 at, out Vector3 all, out Vector3 ours)
        {
            float fx = at.X / 16f - box.X, fy = at.Y / 16f - box.Y;

            if (fx < 0f || fy < 0f || fx >= box.Width || fy >= box.Height)
            {
                all = Vector3.Zero;
                ours = Vector3.Zero;
                return;
            }

            int i = (int)fx, j = (int)fy;
            float u = fx - i, v = fy - j;
            int row = box.Width + 1;
            int a = i + j * row, b = a + 1, c = a + row, d = c + 1;

            all = Vector3.Lerp(Vector3.Lerp(cornerAll[a], cornerAll[b], u), Vector3.Lerp(cornerAll[c], cornerAll[d], u), v);
            ours = Vector3.Lerp(Vector3.Lerp(cornerOurs[a], cornerOurs[b], u), Vector3.Lerp(cornerOurs[c], cornerOurs[d], u), v);
        }

        /// <summary>Light as the game shows it: at its brightness, no more than full.</summary>
        private static Vector3 Shown(Vector3 light) => Vector3.Min(light * brightness, Vector3.One);

        /// <summary>How much of what the game shows (<paramref name="all"/>) a pixel keeps to show <paramref name="wanted"/> instead, colour by colour: no more than all of it.</summary>
        private static Color Keep(Vector3 wanted, Vector3 all)
        {
            Vector3 shown = Shown(all), want = Shown(wanted);

            return new Color(Kept(want.X, shown.X), Kept(want.Y, shown.Y), Kept(want.Z, shown.Z), 1f);
        }

        private static float Kept(float wanted, float shown) => shown < 0.001f ? 1f : MathHelper.Clamp(wanted / shown, 0f, 1f);

        /// <summary>Every tile about the lamps with any of their light at a corner (<see cref="KeptPieces"/>), into the picture being drawn (its batch begun).</summary>
        private static void DrawKept(GraphicsDevice device, Texture2D pixel)
        {
            KeptPieces();
            tiles.Draw(device, pixel);
        }

        /// <summary>
        /// Every tile about the lamps with any of their light at a corner: kept, smoothly from corner
        /// to corner, only the light the game has there besides theirs; each corner reckoned once and
        /// shared by the tiles about it.
        /// </summary>
        private static void KeptPieces()
        {
            int row = box.Width + 1;

            tiles.Clear();
            NextTileStamp(row * (box.Height + 1));

            for (int j = 0; j < box.Height; j++)
            {
                for (int i = 0; i < box.Width; i++)
                {
                    int a = i + j * row, b = a + 1, c = a + row, d = c + 1;

                    if (!Some(cornerOurs[a]) && !Some(cornerOurs[b]) && !Some(cornerOurs[c]) && !Some(cornerOurs[d]))
                        continue;

                    tiles.Quad(KeptCorner(a, i, j), KeptCorner(b, i + 1, j), KeptCorner(c, i, j + 1), KeptCorner(d, i + 1, j + 1));
                }
            }

            tiles.End();
        }

        /// <summary>The corners of the tiles' pieces made this time (by <see cref="tileStamp"/>), one at each corner of <see cref="box"/>'s tiles.</summary>
        private static int[] tileCorner = Array.Empty<int>();
        private static int[] tileCornerStamp = Array.Empty<int>();
        private static int tileStamp;

        private static void NextTileStamp(int corners)
        {
            if (tileCorner.Length < corners)
            {
                tileCorner = new int[corners];
                tileCornerStamp = new int[corners];
                tileStamp = 0;
            }

            if (++tileStamp == int.MaxValue)
            {
                Array.Clear(tileCornerStamp);
                tileStamp = 1;
            }
        }

        private static int KeptCorner(int corner, int i, int j)
        {
            if (tileCornerStamp[corner] == tileStamp)
                return tileCorner[corner];

            int made = tiles.Add(new Vector2(i * 16f, j * 16f), Vector2.Zero, Vector3.Zero, Keep(Besides(cornerAll[corner], cornerOurs[corner]), cornerAll[corner]));
            tileCornerStamp[corner] = tileStamp;
            tileCorner[corner] = made;
            return made;
        }

        private static bool Some(Vector3 light) => light.X + light.Y + light.Z > 0.0001f;

        /// <summary>
        /// For what stands on the blocks, which the game lights one light to a tile (the tile's own,
        /// TileDrawing: Lighting.GetColor): each tile with any of the lamps' light about it, kept
        /// as much more or less again as the smooth light at each of its corners is to that one light
        /// (drawn at half and doubled, so up to twice).
        /// </summary>
        private static void DrawTileByTile(GraphicsDevice device, Texture2D pixel)
        {
            TileByTilePieces();
            tiles.Draw(device, pixel);
        }

        private static void TileByTilePieces()
        {
            int row = box.Width + 1, wide = box.Width + 2;

            tiles.Clear();

            for (int j = 0; j < box.Height; j++)
            {
                for (int i = 0; i < box.Width; i++)
                {
                    int a = i + j * row, b = a + 1, c = a + row, d = c + 1;

                    if (!Some(cornerOurs[a]) && !Some(cornerOurs[b]) && !Some(cornerOurs[c]) && !Some(cornerOurs[d]))
                        continue;

                    Vector3 own = Shown(tileAll[i + 1 + (j + 1) * wide]);
                    float x = i * 16f, y = j * 16f;

                    int topLeft = tiles.Add(new Vector2(x, y), Vector2.Zero, Vector3.Zero, Again(cornerAll[a], own));
                    int topRight = tiles.Add(new Vector2(x + 16f, y), Vector2.Zero, Vector3.Zero, Again(cornerAll[b], own));
                    int bottomLeft = tiles.Add(new Vector2(x, y + 16f), Vector2.Zero, Vector3.Zero, Again(cornerAll[c], own));
                    int bottomRight = tiles.Add(new Vector2(x + 16f, y + 16f), Vector2.Zero, Vector3.Zero, Again(cornerAll[d], own));
                    tiles.Quad(topLeft, topRight, bottomLeft, bottomRight);
                }
            }

            tiles.End();
        }

        private static Color Again(Vector3 corner, Vector3 own)
        {
            Vector3 smooth = Shown(corner);

            return new Color(Half(smooth.X, own.X), Half(smooth.Y, own.Y), Half(smooth.Z, own.Z), 1f);
        }

        private static float Half(float smooth, float own) => own < 0.001f ? 0.5f : MathHelper.Clamp(smooth / own, 0f, 2f) * 0.5f;

        /// <summary>
        /// Every tile about the lit water with no water in it, or a solid block, blacked out; and the
        /// part over the top of the water in a tile part filled. (What is not water takes no light
        /// in it from the sea's water lit.)
        /// </summary>
        private static void DrawDry(GraphicsDevice device, Texture2D pixel)
        {
            DryPieces();
            tiles.Draw(device, pixel);
        }

        /// <summary>The same, next tiles in a row as dry as each other blacked out as one.</summary>
        private static void DryPieces()
        {
            tiles.Clear();

            for (int j = 0; j < box.Height; j++)
            {
                int from = -1;
                float alike = 0f;

                for (int i = 0; i <= box.Width; i++)
                {
                    float dry = 0f;
                    int x = box.X + i, y = box.Y + j;

                    if (i < box.Width && WorldGen.InWorld(x, y))
                    {
                        Tile tile = Main.tile[x, y];
                        dry = tile.LiquidAmount == 0 || WorldGen.SolidTile(x, y) ? 16f : (255 - tile.LiquidAmount) / 255f * 16f;
                    }

                    if (from >= 0 && (dry <= 0.01f || dry != alike))
                    {
                        float left = from * 16f, right = i * 16f, top = j * 16f;
                        tiles.Quad(tiles.Add(new Vector2(left, top), Vector2.Zero, Vector3.Zero, Color.Black), tiles.Add(new Vector2(right, top), Vector2.Zero, Vector3.Zero, Color.Black),
                            tiles.Add(new Vector2(left, top + alike), Vector2.Zero, Vector3.Zero, Color.Black), tiles.Add(new Vector2(right, top + alike), Vector2.Zero, Vector3.Zero, Color.Black));
                        from = -1;
                    }

                    if (from < 0 && dry > 0.01f)
                    {
                        from = i;
                        alike = dry;
                    }
                }
            }

            tiles.End();
        }

        /// <summary>
        /// The longest a piece of a ray is laid in, near the lamp (the little glow) and further out
        /// (pixels), and further still, a share of how far out it is (the light changes the more
        /// slowly the further out). Every piece ends on the same rings about the lamp
        /// (<see cref="RingsNear"/>, <see cref="RingsFar"/>), but where a ray ends, so that pieces
        /// laid side by side meet corner to corner.
        /// </summary>
        private const float NearPiece = 4f;
        private const float FarPiece = 8f;
        private const float PieceShare = 0.02f;

        /// <summary>
        /// Next rays that go just alike (from the glass through the same, none yet into or out of
        /// water, nor into solid, nor ended) are lit just alike, and are laid as one: no wider than
        /// this at the far end of each piece (pixels), nor across more than this (degrees); the
        /// beam's, its light changing across it, across no more than <see cref="MostAsOne"/> rays.
        /// Where they stop going alike they are laid in halves, and so on, down to each two next
        /// rays, laid one by one as far as the longer goes (past the shorter, with the light the
        /// longer lays).
        /// </summary>
        private const float WidestAsOne = 12f;
        private const float MostDegreesAsOne = 8f;
        private const int MostAsOne = 16;

        /// <summary>Rays laid as two next rays are, if they are no wider apart than this where the furthest of them ends (pixels): what lies between can not be told apart.</summary>
        private const float Thinnest = 1f;

        /// <summary>The rings the pieces end on (pixels from the lamp): each a piece's length further out than the last, to past the furthest any ray goes.</summary>
        private static readonly float[] RingsNear = Rings(NearPiece);
        private static readonly float[] RingsFar = Rings(FarPiece);

        private static float[] Rings(float piece)
        {
            List<float> rings = new List<float> { 0f };

            while (rings[rings.Count - 1] <= Longest)
                rings.Add(rings[rings.Count - 1] + Math.Max(piece, rings[rings.Count - 1] * PieceShare));

            return rings.ToArray();
        }

        /// <summary>The corner made on each ray at each ring, for the lamp being laid (by <see cref="stamp"/>): made once, shared by the pieces either side.</summary>
        private const int RingsKept = 160;
        private static int[] ringCorner = Array.Empty<int>();
        private static int[] ringStamp = Array.Empty<int>();
        private static int stamp;

        /// <summary>The lamp and the group of its rays being laid: their shape, rings, how many, the first, and how far apart.</summary>
        private static Fan layFan;
        private static Shape layShape;
        private static float[] layRings;
        private static int layCount;
        private static int layFirst;
        private static float layStep;

        /// <summary>
        /// Every lamp's fine rays laid out (<see cref="laid"/>), in pieces between each ray and the
        /// next, from the lamp out, ring by ring: next rays that go alike laid as one, in halves
        /// where they stop going alike, and each two next rays at last one by one. Each corner has
        /// the light of its own ray there (past the end of the shorter of two, the longer's), and
        /// any other lamp's there. A lamp's two lights (its beam and the glow about it) are each
        /// laid so, and where they cross the brighter is kept (as the pictures are drawn:
        /// <see cref="brighter"/>).
        /// </summary>
        private static void LayFans()
        {
            laid.Clear();

            if (ringCorner.Length == 0)
            {
                ringCorner = new int[MostRays * RingsKept];
                ringStamp = new int[MostRays * RingsKept];
                stamp = 0;
            }

            for (int f = 0; f < fanCount; f++)
            {
                Fan fan = fans[f];

                if (++stamp == int.MaxValue)
                {
                    Array.Clear(ringStamp);
                    stamp = 1;
                }

                for (int g = 0; g < fan.Groups; g++)
                {
                    int count = fan.GroupCount[g];
                    bool about = fan.GroupAbout[g];
                    int sectors = about ? count : count - 1;

                    layFan = fan;
                    layShape = ShapeOf(fan.GroupShape[g]);
                    layRings = fan.GroupShape[g] == NearShape ? RingsNear : RingsFar;
                    layCount = count;
                    layFirst = fan.GroupFirst[g];
                    layStep = fan.GroupStep[g];

                    // As many next rays as one as there are in some degrees (the beam's, some rays).
                    int most = Math.Max(1, (int)(MathHelper.ToRadians(MostDegreesAsOne) / layStep + 0.01f));

                    if (!about)
                        most = Math.Min(most, MostAsOne);

                    for (int a = 0; a < sectors; a += most)
                    {
                        int b = Math.Min(a + most, sectors);
                        Span(a, b, 0, 0f, Own(a, 0), Own(b, 0));
                    }
                }
            }

            laid.End();
            layFan = null;
        }

        /// <summary>The ray counted <paramref name="i"/> in the group being laid (counted round for a group all about).</summary>
        private static int Ray(int i) => layFirst + i % layCount;

        /// <summary>
        /// The rays counted <paramref name="a"/> to <paramref name="b"/> laid from ring <paramref name="k"/>
        /// (<paramref name="from"/> pixels out, their corners there given) out: as one while they go
        /// alike, else in halves; two next rays one by one (<see cref="Single"/>).
        /// </summary>
        private static void Span(int a, int b, int k, float from, int nearA, int nearB)
        {
            if (b - a == 1)
            {
                Single(a, b, k, from, nearA, nearB);
                return;
            }

            Fan fan = layFan;
            int ra = Ray(a), rb = Ray(b);

            // As far as all of them go alike (none if they did not all begin alike), and where they end.
            bool wet = fan.WetFirst[ra];
            float same = float.MaxValue, least = float.MaxValue, most = 0f;

            for (int i = a; i <= b; i++)
            {
                int ray = Ray(i);

                same = fan.WetFirst[ray] == wet ? Math.Min(same, fan.Same[ray]) : 0f;
                least = Math.Min(least, fan.End[ray]);
                most = Math.Max(most, fan.End[ray]);
            }

            // No wider than a pixel even where the furthest of them ends, and that one of the outer two:
            // what lies between them can not be told apart, and they are laid as two next rays are.
            if ((b - a) * layStep * most <= Thinnest && most <= Math.Max(fan.End[ra], fan.End[rb]) + 0.75f)
            {
                Single(a, b, k, from, nearA, nearB);
                return;
            }

            // Alike to their ends, and all ending together: laid as one to the furthest end.
            bool together = most - least <= 0.75f && same >= least - 0.01f;
            float halfSin = (float)Math.Sin((b - a) * layStep * 0.5f);

            while (true)
            {
                if (together && from >= most - 0.01f)
                    return;

                int next = k + 1;

                if (next >= layRings.Length)
                    return;

                float to = layRings[next];
                bool grid = true;

                if (together && to >= most - 0.01f)
                {
                    grid = to == most;
                    to = most;
                }

                // Too wide here, or past where they go alike and not lit alike enough there: in halves.
                if (2f * to * halfSin > WidestAsOne || (!together && to > same && (to > least || !Smooth(a, b, to))))
                {
                    // But if all of them end before the next ring, none in a solid, as on a straight
                    // line from the one outer end to the other: the last piece, to their ends.
                    if (!together && to > least && most <= to && 2f * most * halfSin <= WidestAsOne && Slanted(a, b))
                    {
                        Quad(nearA, nearB, At(a, rb, next, fan.End[ra], false), At(b, ra, next, fan.End[rb], false));
                        return;
                    }

                    break;
                }

                int farA = At(a, rb, next, to, grid);
                int farB = At(b, ra, next, to, grid);
                Quad(nearA, nearB, farA, farB);

                if (!grid)
                    return;

                k = next;
                from = to;
                nearA = farA;
                nearB = farB;
            }

            // In halves at the middle ray, from this ring; the sliver between this one's far side
            // (straight from corner to corner) and the halves' near sides (to the middle ray's corner) filled.
            int m = (a + b) / 2;
            int nearM = At(m, Ray(m), k, from, true);

            if (from > 0f)
                Triangle(nearA, nearM, nearB);

            Span(a, m, k, from, nearA, nearM);
            Span(m, b, k, from, nearM, nearB);
        }

        /// <summary>
        /// How far from what lies between the outer two of rays laid as one a ray's way through may
        /// be, and the rays still be laid as one: of its reach used (a fifth of a hundredth: under
        /// a step of the picture's colour), and of water come through (tiles).
        /// </summary>
        private const float SmoothUsed = 0.002f;
        private const float SmoothWet = 0.05f;

        /// <summary>
        /// Whether the rays counted <paramref name="a"/> to <paramref name="b"/>, not all gone alike so
        /// far (one into the water before another), are lit alike enough <paramref name="along"/>
        /// pixels out to be laid as one there: none in a solid there, and each one's way through so
        /// far (how much of its reach used, how much water come through) as near what lies between
        /// the outer two's as can be told apart (<see cref="SmoothUsed"/>, <see cref="SmoothWet"/>).
        /// Each going that far is asked first.
        /// </summary>
        private static bool Smooth(int a, int b, float along)
        {
            Fan fan = layFan;
            Through(fan, Ray(a), along, out float usedA, out float wetA, out bool solidA);
            Through(fan, Ray(b), along, out float usedB, out float wetB, out bool solidB);

            if (solidA || solidB)
                return false;

            for (int i = a + 1; i < b; i++)
            {
                Through(fan, Ray(i), along, out float used, out float wet, out bool solid);
                float t = (i - a) / (float)(b - a);

                if (solid || Math.Abs(used - (usedA + (usedB - usedA) * t)) > SmoothUsed || Math.Abs(wet - (wetA + (wetB - wetA) * t)) > SmoothWet)
                    return false;
            }

            return true;
        }

        /// <summary>Whether the rays counted <paramref name="a"/> to <paramref name="b"/> go into no solid and end as on a straight line from the one outer end to the other (to within <see cref="EndsAlike"/>).</summary>
        private static bool Slanted(int a, int b)
        {
            Fan fan = layFan;
            float endA = fan.End[Ray(a)], endB = fan.End[Ray(b)];

            for (int i = a; i <= b; i++)
            {
                int ray = Ray(i);

                if (fan.Hits[ray] || Math.Abs(fan.End[ray] - (endA + (endB - endA) * (i - a) / (b - a))) > EndsAlike)
                    return false;
            }

            return true;
        }

        /// <summary>How far apart (pixels) the ends of rays laid as one may be from ending all together, or as on a straight line.</summary>
        private const float EndsAlike = 0.75f;

        /// <summary>A ray of the group being laid, this far along it: how much of its reach used, how much water come through (tiles), and whether in a solid.</summary>
        private static void Through(Fan fan, int ray, float along, out float used, out float wet, out bool solid)
        {
            ref int node = ref fan.Cursor[ray];
            NodeAt(fan, ray, along, ref node);

            float gone = Math.Max(0f, along - fan.Along[node]);
            bool water = fan.Water[node];

            used = fan.Used[node] + gone / (16f * (water ? layShape.Water : layShape.Air));
            wet = fan.Wet[node] + (water ? gone / 16f : 0f);
            solid = fan.Deep[node] >= 0f;
        }

        /// <summary>Two next rays from ring <paramref name="k"/> (or just past it, <paramref name="from"/> pixels out) as far as the longer goes, in pieces ending on the rings, and where the shorter ends.</summary>
        private static void Single(int a, int b, int k, float from, int nearA, int nearB)
        {
            Fan fan = layFan;
            int ra = Ray(a), rb = Ray(b);
            float shorter = Math.Min(fan.End[ra], fan.End[rb]);
            float longer = Math.Max(fan.End[ra], fan.End[rb]);

            if (longer < 0.5f)
                return;

            while (from < longer - 0.01f)
            {
                int next = k + 1;

                while (next < layRings.Length && layRings[next] <= from + 0.01f)
                    next++;

                float to = next < layRings.Length ? Math.Min(layRings[next], longer) : longer;

                if (from < shorter - 0.01f && to > shorter)
                    to = shorter;

                bool grid = next < layRings.Length && to == layRings[next];
                int farA = At(a, rb, next, to, grid);
                int farB = At(b, ra, next, to, grid);
                Quad(nearA, nearB, farA, farB);

                from = to;
                k = grid ? next : next - 1;
                nearA = farA;
                nearB = farB;
            }
        }

        /// <summary>The corner on the ray counted <paramref name="i"/>, <paramref name="along"/> pixels out (ring <paramref name="k"/>, if <paramref name="grid"/>): its own light, or past its end the other's (<paramref name="other"/>, a ray of the lamp's).</summary>
        private static int At(int i, int other, int k, float along, bool grid)
        {
            int ray = Ray(i);

            if (along > layFan.End[ray])
                return Corner(ray, other, along);

            return grid && k < RingsKept ? Own(i, k) : Corner(ray, ray, along);
        }

        /// <summary>The corner on the ray counted <paramref name="i"/> at ring <paramref name="k"/>, with its own light: made the first time it is asked for, and kept.</summary>
        private static int Own(int i, int k)
        {
            int ray = Ray(i);
            int key = ray * RingsKept + k;

            if (ringStamp[key] == stamp)
                return ringCorner[key];

            int corner = Corner(ray, ray, layRings[k]);
            ringStamp[key] = stamp;
            ringCorner[key] = corner;
            return corner;
        }

        /// <summary>A corner on the lamp's ray <paramref name="ray"/>, <paramref name="along"/> pixels out, lit as its ray <paramref name="lightRay"/> is that far out (none past its end), and any other lamp there.</summary>
        private static int Corner(int ray, int lightRay, float along)
        {
            Fan fan = layFan;
            Vector3 light = along > fan.End[lightRay] ? Vector3.Zero : RayLight(fan, lightRay, layShape, along, ref fan.Cursor[lightRay]);

            if (fanCount > 1)
                light += OthersLight(fan, fan.Lens + fan.Way[lightRay] * along);

            Vector2 at = fan.Lens + fan.Way[ray] * along;
            return laid.Add(at - anchor, at, light, default);
        }

        private static void Quad(int nearA, int nearB, int farA, int farB) => laid.Quad(nearA, nearB, farA, farB);

        private static void Triangle(int a, int b, int c) => laid.Piece(a, b, c);

        /// <summary>
        /// The corners' colours for one of the pictures: how much each pixel keeps, the light the
        /// game has there besides the lamps' and theirs as the rays lay it, over all the game has
        /// there; or (<paramref name="water"/>) the sea's water lit there, the colour of clear sea
        /// water lit by a white light as it is seen (<see cref="WaterLit"/>) times the light, drawn
        /// as much brighter as the game's water over it takes away (VastOceanSky.ThroughWater).
        /// </summary>
        private static void ColourLaid(bool water)
        {
            object which = water ? ForWater : ForKeeping;

            if (laid.Count < InParts)
                ColourCorners(0, laid.Count, which);
            else
                FastParallel.For(0, laid.Count, colourCorners, which);
        }

        /// <summary>
        /// The corners coloured in parts at once, each corner on its own (on as many of the
        /// machine's cores as the game spreads its own light over, ReLogic's FastParallel), when
        /// there are this many or more.
        /// </summary>
        private const int InParts = 2048;
        private static readonly ParallelForAction colourCorners = ColourCorners;
        private static readonly object ForWater = true;
        private static readonly object ForKeeping = false;

        private static void ColourCorners(int from, int to, object water)
        {
            bool sea = (bool)water;

            for (int v = from; v < to; v++)
                laid.Corners[v].Color = sea ? Lit(laid.Light[v]) : KeepLaid(laid.At[v], laid.Light[v]);
        }

        /// <summary>Every piece of the rays into the picture being drawn (its batch begun), coloured for it (<see cref="ColourLaid"/>).</summary>
        private static void DrawLaid(GraphicsDevice device, Texture2D pixel, bool water)
        {
            ColourLaid(water);
            laid.Draw(device, pixel);
        }

        private static Color KeepLaid(Vector2 at, Vector3 light)
        {
            LightThere(at, out Vector3 all, out Vector3 ours);

            return Keep(Besides(all, ours) + light, all);
        }

        /// <summary>The game's light besides the lamps', none less than none.</summary>
        private static Vector3 Besides(Vector3 all, Vector3 ours) => Vector3.Max(all - ours, Vector3.Zero);

        /// <summary>The colour of clear sea water lit by a white light, as it is seen (times the lamp's light where it is, which the water has already made bluer the further it has come).</summary>
        private static readonly Vector3 WaterLit = new Vector3(0.40f, 0.52f, 0.56f);

        private static Color Lit(Vector3 light)
        {
            Vector3 drawn = Vector3.Clamp(light * WaterLit * VastOceanSky.ThroughWater, Vector3.Zero, Vector3.One);

            return new Color(drawn.X, drawn.Y, drawn.Z, 1f);
        }

        // ---- laid over the game's pictures as it makes them ----------------------------------------

        /// <summary>Which of the game's pictures of the world it is making (Main.RenderTiles and the rest), while it makes it.</summary>
        private enum Making
        {
            None,
            Tiles,
            Objects,
            Walls,
            Background,
            Water,
        }

        private static Making making;

        private static void InTiles(On_Main.orig_RenderTiles orig, Main self) => While(Making.Tiles, () => orig(self));

        private static void InObjects(On_Main.orig_RenderTiles2 orig, Main self) => While(Making.Objects, () => orig(self));

        private static void InWalls(On_Main.orig_RenderWalls orig, Main self) => While(Making.Walls, () => orig(self));

        private static void InBackground(On_Main.orig_RenderBackground orig, Main self) => While(Making.Background, () => orig(self));

        private static void InWater(On_Main.orig_RenderWater orig, Main self) => While(Making.Water, () => orig(self));

        private static void While(Making now, Action render)
        {
            Making was = making;
            making = now;

            try
            {
                render();
            }
            finally
            {
                making = was;
            }
        }

        /// <summary>The blocks (Main.RenderTiles) and what stands on them (Main.RenderTiles2) are drawn, the game's batches ended, its picture still being drawn into: the last thing it draws there is its tile entities.</summary>
        private static void AfterTileEntities(On_Main.orig_DrawTileEntities orig, Main self, bool solidLayer, bool overRenderTargets, bool intoRenderTargets)
        {
            orig(self, solidLayer, overRenderTargets, intoRenderTargets);

            if (!intoRenderTargets)
                return;

            if (making == Making.Tiles && solidLayer)
                Bake(cutTarget, cutReady);
            else if (making == Making.Objects && !solidLayer)
                Bake(objectsTarget, cutReady);
        }

        /// <summary>The walls are drawn (Main.RenderWalls), its two batches still open (the sprites' begun as Deferred and AlphaBlend, the tiles' as they begin).</summary>
        private static void AfterWalls(On_Main.orig_DrawWalls orig, Main self)
        {
            orig(self);

            if (making != Making.Walls || !cutReady)
                return;

            Main.tileBatch.End();
            Main.spriteBatch.End();
            Bake(cutTarget, true);
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Main.tileBatch.Begin();
        }

        /// <summary>The backdrop under the ground is drawn (Main.RenderBackground), its two batches still open as they begin.</summary>
        private static void AfterBackground(On_Main.orig_DrawBackground orig, Main self)
        {
            orig(self);

            if (making != Making.Background || !cutReady)
                return;

            Main.tileBatch.End();
            Main.spriteBatch.End();
            Bake(cutTarget, true);
            Main.spriteBatch.Begin();
            Main.tileBatch.Begin();
        }

        /// <summary>The water behind everything (Main.RenderBackground, its two batches open) or over everything (Main.RenderWater, the sprites' batch open) is drawn: it keeps the lamps' light only where the rays lay it, as everything does.</summary>
        private static void AfterWaters(On_Main.orig_DrawWaters orig, Main self, bool isBackground)
        {
            orig(self, isBackground);

            if (!cutReady)
                return;

            if (making == Making.Background && isBackground)
            {
                Main.tileBatch.End();
                Main.spriteBatch.End();
                Bake(cutTarget, true);
                Main.spriteBatch.Begin();
                Main.tileBatch.Begin();
            }
            else if (making == Making.Water && !isBackground)
            {
                Main.spriteBatch.End();
                Bake(cutTarget, true);
                Main.spriteBatch.Begin();
            }
        }

        /// <summary>
        /// One of the pictures of how much light is kept laid over the one of the game's it is making,
        /// each of the game's pixels kept in that proportion. The game draws its pictures from the
        /// screen's corner and its margin (Main.offScreenRange), the screen's place in whole pixels.
        /// </summary>
        private static void Bake(RenderTarget2D kept, bool ready)
        {
            if (!ready || kept == null || kept.IsDisposed)
                return;

            Vector2 corner = new Vector2((int)Main.screenPosition.X - Main.offScreenRange, (int)Main.screenPosition.Y - Main.offScreenRange);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, keep, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            Main.spriteBatch.Draw(kept, anchor - corner, new Rectangle(0, 0, usedWide, usedHigh), Color.White);
            Main.spriteBatch.End();
        }

        // ---- as seen ----------------------------------------------------------------------------

        /// <summary>
        /// At the game's layer in front of its water (RenderLayers.ForegroundWater), in any world:
        /// each lamp's glass and its glow, and the motes in its light. Added as light (a colour with
        /// no alpha, blended as the game blends, adds itself).
        /// </summary>
        private static void Front(On_OverlayManager.orig_Draw orig, OverlayManager self, SpriteBatch spriteBatch, RenderLayers layer, bool beginSpriteBatch)
        {
            if (layer == RenderLayers.ForegroundWater && !beginSpriteBatch && ReferenceEquals(self, Overlays.Scene) && !Main.gameMenu && dot != null && dot.IsLoaded && AnyLit())
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

                try
                {
                    for (int p = 0; p < Main.maxPlayers; p++)
                    {
                        Player player = Main.player[p];

                        if (player.active && !player.dead && player.GetModPlayer<MupralPlayer>() is { Lit: true } lamp)
                            Draw(spriteBatch, player, lamp);
                    }
                }
                finally
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                }
            }

            orig(self, spriteBatch, layer, beginSpriteBatch);
        }

        private static bool AnyLit()
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                if (Main.player[p].active && Main.player[p].GetModPlayer<MupralPlayer>().Lit)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a lamp is lit whose light can be on the screen (its glass within its light's
        /// reach of it, the beam's as far as it reaches now): what the dark of the Vast Ocean's
        /// depths gives way to (VastOceanSky, by request: "murpal needs to be able to shine at this
        /// depth bruh", "at every depth").
        /// </summary>
        internal static bool LitOnScreen()
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];

                if (!player.active || player.dead)
                    continue;

                MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();

                if (!lamp.Lit)
                    continue;

                int reach = (int)((lamp.Aiming ? BeamAir : GlowAir) * 16f);
                Rectangle near = new Rectangle((int)Main.screenPosition.X - reach, (int)Main.screenPosition.Y - reach, Main.screenWidth + 2 * reach, Main.screenHeight + 2 * reach);
                Vector2 lens = Lens(player, lamp);

                if (near.Contains((int)lens.X, (int)lens.Y))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The water its rays pass, lit, to the pixel: in the Vast Ocean the sea is drawn over the
        /// game's own water behind everything (VastOceanSky), which hides the light the game's water
        /// there would show; so, over the sea, the picture of the sea's water lit
        /// (<see cref="litTarget"/>, made this frame from every lamp's fine rays), added: none in a
        /// shadow, none past where the light ends, nothing over what is not water, and only as high
        /// as the water in a tile part filled. From VastOceanSky at the layer of the water behind,
        /// with the game's batch ended.
        /// </summary>
        internal static void DrawLitWater()
        {
            if (!litReady || litTarget == null || litTarget.IsDisposed)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            Main.spriteBatch.Draw(litTarget, anchor - Main.screenPosition, new Rectangle(0, 0, usedWide, usedHigh), Color.White);
            Main.spriteBatch.End();
        }

        private static void Draw(SpriteBatch batch, Player player, MupralPlayer lamp)
        {
            Vector2 lens = Lens(player, lamp);
            Rectangle seen = new Rectangle((int)Main.screenPosition.X - 400, (int)Main.screenPosition.Y - 400, Main.screenWidth + 800, Main.screenHeight + 800);

            if (!seen.Contains((int)lens.X, (int)lens.Y))
                return;

            Texture2D soft = dot.Value;
            Vector2 origin = soft.Size() * 0.5f;
            Vector2 at = lens - Main.screenPosition;
            int lx = (int)(lens.X / 16f), ly = (int)(lens.Y / 16f);
            bool underwater = WorldGen.InWorld(lx, ly) && Main.tile[lx, ly].LiquidAmount > 128;
            float t = Main.GlobalTimeWrappedHourly;

            // What its light falls on in the water: motes drifting there, lit as its rays light them.
            if (underwater && FanOf(player.whoAmI) is Fan fan)
                DrawMotes(batch, soft, origin, fan, t);

            if (!lamp.Shown)
                return;

            // The glass: a bright point, a little glow about it, brighter for the beam.
            float flicker = 0.95f + 0.05f * (float)Math.Sin(t * 13f + player.whoAmI);
            float strength = lamp.Aiming ? 1f : 0.7f;

            batch.Draw(soft, at, null, Lit(Color.White, 0.9f * strength * flicker), 0f, origin, 3.5f / soft.Width, SpriteEffects.None, 0f);
            batch.Draw(soft, at, null, Lit(BeigeColour, 0.45f * strength * flicker), 0f, origin, 9f / soft.Width, SpriteEffects.None, 0f);
            batch.Draw(soft, at, null, Lit(BeigeColour, 0.10f * strength * flicker), 0f, origin, 26f / soft.Width, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The motes in the water its light falls on, by request ("im more fond of whatever is in the
        /// path of light is lit", "more natural"): one in each cell of 24 pixels about the lamp,
        /// drifting and sinking slowly, lit as brightly as its rays light the water where each is
        /// (the lamp's fine rays, this frame: <see cref="FanLight"/>), glimmering and now and then
        /// flashing; none in a shadow, none past where its light ends. Beige, cooler the more water
        /// the light has come through.
        /// </summary>
        private static void DrawMotes(SpriteBatch batch, Texture2D soft, Vector2 origin, Fan fan, float t)
        {
            const float Cell = 24f;

            // As far as its lights go through water: the beam's, or the glow's (the HEATR's wide glow is shorter than its beam).
            float water = (fan.Kit == MupralKit.Glow ? GlowWater : BeamWater) * 16f;
            Vector2 lens = fan.Lens;

            // Over what of that is on the screen; past the little light about the lamp, only in the
            // beam (a hair wider than it, for the motes at its edges).
            float left = Math.Max(lens.X - water, Main.screenPosition.X - Cell), right = Math.Min(lens.X + water, Main.screenPosition.X + Main.screenWidth + Cell);
            float upper = Math.Max(lens.Y - water, Main.screenPosition.Y - Cell), lower = Math.Min(lens.Y + water, Main.screenPosition.Y + Main.screenHeight + Cell);

            if (right <= left || lower <= upper)
                return;

            int first = (int)Math.Floor(left / Cell);
            int last = (int)Math.Ceiling(right / Cell);
            int top = (int)Math.Floor(upper / Cell);
            int bottom = (int)Math.Ceiling(lower / Cell);
            float about = (fan.Kit == MupralKit.Suit ? SuitGlowWater : NearGlow) * 16f;
            float halfBeam = MathHelper.ToRadians(fan.Degrees * 0.5f + 1f);

            for (int cx = first; cx <= last; cx++)
            {
                for (int cy = top; cy <= bottom; cy++)
                {
                    int seed = cx * 7349 + cy * 1931;
                    float h1 = Hash(seed), h2 = Hash(seed + 7), h3 = Hash(seed + 13);
                    float sink = (t * (2f + 3f * h3) + h2 * Cell) % Cell;
                    Vector2 mote = new Vector2((cx + h1) * Cell + 5f * (float)Math.Sin(t * (0.3f + 0.4f * h3) + h2 * 9f), cy * Cell + sink);
                    float far = Vector2.Distance(mote, lens);

                    // Too near the glass to tell from it, or past where the water lets its light go.
                    if (far < 8f || far > water)
                        continue;

                    // Out of the beam, past the little light about the lamp: none of its light there.
                    if (fan.Kit != MupralKit.Glow && far > about && Math.Abs(MathHelper.WrapAngle((mote - lens).ToRotation() - fan.Aim)) > halfBeam)
                        continue;

                    int mx = (int)(mote.X / 16f), my = (int)(mote.Y / 16f);

                    if (!WorldGen.InWorld(mx, my) || Main.tile[mx, my].LiquidAmount < 128)
                        continue;

                    // As bright as its rays are there: none in a shadow, none past their ends.
                    Vector3 light = FanLight(fan, mote);
                    float bright = Math.Max(light.X, Math.Max(light.Y, light.Z));

                    if (bright < 0.005f)
                        continue;

                    float glimmer = 0.5f + 0.5f * (float)Math.Sin(t * (1.3f + 1.5f * h1) + h2 * 37f);
                    float flash = (float)Math.Pow(Math.Max(0.0, Math.Sin(t * (0.45f + 0.6f * h2) + h3 * 19f)), 20.0);
                    float strength = bright * (0.3f + 0.5f * glimmer + 1.2f * flash);

                    if (strength < 0.01f)
                        continue;

                    Vector3 tint = light / bright;
                    Color colour = new Color(tint.X, tint.Y, tint.Z);

                    batch.Draw(soft, mote - Main.screenPosition, null, Lit(colour, strength), 0f, origin, (1.2f + 1.2f * h1) / soft.Width, SpriteEffects.None, 0f);
                }
            }
        }

        // ---- reckoning ------------------------------------------------------------------------

        /// <summary>How much of a light is seen this far out (tiles), from how near its falling off begins: the square of the distance's falling off, seen by its square root.</summary>
        private static float Spread(float distance, float near)
        {
            float d = distance / near;

            return 1f / (float)Math.Sqrt(1f + d * d);
        }

        private static float Smooth(float from, float to, float value)
        {
            float t = MathHelper.Clamp((value - from) / (to - from), 0f, 1f);

            return t * t * (3f - 2f * t);
        }

        /// <summary>A colour at this strength with no alpha: drawn blended as usual, it is added to what is behind it.</summary>
        private static Color Lit(Color colour, float strength)
        {
            strength = MathHelper.Clamp(strength, 0f, 1f);

            return new Color((byte)(colour.R * strength), (byte)(colour.G * strength), (byte)(colour.B * strength), (byte)0);
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
    }

    /// <summary>
    /// The lamp on its wearer's front shoulder: its clamp and strap, turned with the wearer, and its
    /// head on the clamp, turned to point the beam or the way its wearer faces (MupralWorn.png, two
    /// pictures of 12 by 12 one over the other: the head, the clamp; drawn at the game's 2x, as its
    /// characters are). Over everything on the body, the arm in front of it too. In the light that
    /// is where it is, and a little of its own. Not drawn when its slot's picture is hidden.
    /// </summary>
    public class MupralLayer : PlayerDrawLayer
    {
        /// <summary>Where the head turns, on both pictures: the middle of the head's body.</summary>
        private static readonly Vector2 Turn = new Vector2(5f, 5f);

        private static Asset<Texture2D> picture;

        public override void Load()
        {
            if (!Main.dedServ)
                picture = ModContent.Request<Texture2D>("PisAndDag/Content/Items/Accessories/MupralWorn");
        }

        public override void Unload()
        {
            picture = null;
        }

        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && !drawInfo.headOnlyRender && drawInfo.drawPlayer.GetModPlayer<MupralPlayer>() is { Lit: true, Shown: true };

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (picture == null || !picture.IsLoaded)
                return;

            Player player = drawInfo.drawPlayer;
            MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();
            Texture2D texture = picture.Value;
            Rectangle head = new Rectangle(0, 0, 12, 12);
            Rectangle clamp = new Rectangle(0, 12, 12, 12);
            Vector2 pivot = MupralLight.Pivot(player);
            Vector2 at = (pivot - Main.screenPosition).Floor();

            Color around = Lighting.GetColor((int)(pivot.X / 16f), (int)(pivot.Y / 16f));
            Color colour = new Color(Math.Max(around.R, (byte)70), Math.Max(around.G, (byte)64), Math.Max(around.B, (byte)56));

            bool left = player.direction < 0;
            bool upside = player.gravDir < 0f;
            SpriteEffects clampWay = (left ? SpriteEffects.FlipHorizontally : SpriteEffects.None) | (upside ? SpriteEffects.FlipVertically : SpriteEffects.None);
            Vector2 clampTurn = new Vector2(left ? clamp.Width - Turn.X : Turn.X, upside ? clamp.Height - Turn.Y : Turn.Y);

            drawInfo.DrawDataCache.Add(new DrawData(texture, at, clamp, colour, 0f, clampTurn, 1f, clampWay, 0f));

            float facing = MupralLight.Facing(player, lamp);
            bool back = Math.Cos(facing) < 0;
            Vector2 headTurn = new Vector2(Turn.X, back ? head.Height - Turn.Y : Turn.Y);

            drawInfo.DrawDataCache.Add(new DrawData(texture, at, head, colour, facing, headTurn, 1f, back ? SpriteEffects.FlipVertically : SpriteEffects.None, 0f));
        }
    }
}
