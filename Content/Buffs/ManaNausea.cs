using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Mana Nausea: what a mana potion does to the eyes. Every mana potion drunk gives it
    /// (ManaDrinkPlayer), by strength: I for a lesser or an ordinary potion, II for a greater,
    /// III for a super. One drink gives <see cref="Durations"/>; each more adds that again, up
    /// to <see cref="MaxDurations"/> (three drinks).
    ///
    ///   I    the edges of the screen swim, slightly.
    ///   II   everything swims, hard, but a circle round the player.
    ///   III  everything swims, the player too, and it runs on into the trip
    ///        (ManaNauseaScreen, ManaPhantoms).
    ///
    /// Each holds at full until the last <see cref="FadeTicks"/> of it and dies away over
    /// those.
    /// </summary>
    public abstract class ManaNausea : ModBuff
    {
        /// <summary>How long one drink gives, in ticks, by strength (index 1 to 3).</summary>
        public static readonly int[] Durations = { 0, 30 * 60, 45 * 60, 90 * 60 };

        /// <summary>The most it can be drunk up to, in ticks, by strength.</summary>
        public static readonly int[] MaxDurations = { 0, 80 * 60, 100 * 60, 180 * 60 };

        /// <summary>The last stretch of it, in ticks, over which it dies away, by strength.</summary>
        public static readonly int[] FadeTicks = { 0, 10 * 60, 10 * 60, 5 * 60 };

        public abstract int Tier { get; }

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        /// <summary>The buff for a strength, 1 to 3.</summary>
        public static int TypeFor(int tier) => tier switch
        {
            1 => ModContent.BuffType<ManaNausea1>(),
            2 => ModContent.BuffType<ManaNausea2>(),
            _ => ModContent.BuffType<ManaNausea3>(),
        };
    }

    public class ManaNausea1 : ManaNausea
    {
        public override int Tier => 1;
    }

    public class ManaNausea2 : ManaNausea
    {
        public override int Tier => 2;
    }

    public class ManaNausea3 : ManaNausea
    {
        public override int Tier => 3;
    }

    /// <summary>
    /// What Mana Nausea does to the screen, worked out from the local player's buffs every
    /// tick: the numbers for the filter (Effects/ManaNausea.fx), the camera, and, in III,
    /// the trip.
    ///
    /// Each strength has its own look at full (<see cref="Looks"/>). The strengths present
    /// are laid over each other weakest first, each by how far it has come on and not yet
    /// died away, so when a stronger one ends the one under it is already there and the
    /// screen eases from one to the other instead of jumping.
    ///
    /// THE TRIP (III). <see cref="Trip"/> runs up over <see cref="TripRunIn"/> and down with
    /// the buff's own end. At full:
    ///   the colours come apart and roll (the shader);
    ///   the slices: the screen cut into eight equal slices about its middle, the
    ///     kaleidoscope. It breathes on a slow cycle: in, held whole, drawn back to
    ///     <see cref="SliceLow"/>, held there, in again;
    ///   the turn: while the slices are whole and nothing else shows, the view underneath
    ///     eases round half a turn. When they draw back the world is the other way up, and
    ///     the next time they close it goes round again. Always the same way round, never
    ///     back. For the last <see cref="ReturnTicks"/> it heads for upright whatever the
    ///     slices are doing, and the filter stays on until it gets there;
    ///   the camera wanders and leans in and out;
    ///   every other screen filter (the graveyard's, the Wasted Mile's, a blood moon's)
    ///     fades out under it, by as much as the trip is on;
    ///   and things that are not there come and go at the sides (ManaPhantoms).
    /// Everything follows <see cref="Trip"/> or a spring, so nothing snaps.
    /// </summary>
    public class ManaNauseaScreen : ModSystem
    {
        public const string FilterKey = "PisAndDag:ManaNausea";

        /// <summary>Set by the Mod class once the effect has actually loaded.</summary>
        internal static bool Registered;

        /// <summary>How far into the trip the local player is, 0 to 1, this tick.</summary>
        public static float Trip { get; private set; }

        /// <summary>
        /// One strength's look at full. Warp is how far the picture swims, in screen heights.
        /// Clear and Full are where the swim starts and where it is whole, from the player;
        /// Edge 0 measures that in screen heights round them, Edge 1 toward the edges of the
        /// screen (1 is the middle of each edge). Trip is whether it runs on into the trip.
        /// </summary>
        private readonly struct Look
        {
            public readonly float Warp, Clear, Full, Edge, Trip;

            public Look(float warp, float clear, float full, float edge, float trip)
            {
                Warp = warp;
                Clear = clear;
                Full = full;
                Edge = edge;
                Trip = trip;
            }
        }

        private static readonly Look[] Looks =
        {
            default,
            new Look(warp: 0.006f, clear: 0.55f, full: 1.05f, edge: 1f, trip: 0f),
            new Look(warp: 0.022f, clear: 0.14f, full: 0.30f, edge: 0f, trip: 0f),
            new Look(warp: 0.035f, clear: -1f, full: -0.5f, edge: 0f, trip: 1f),
        };

        /// <summary>Ticks each strength takes to come on, so a fresh one does not snap in.</summary>
        private const int ComeOn = 30;

        /// <summary>Ticks III takes to run on from the swim into the trip.</summary>
        private const int TripRunIn = 10 * 60;

        // ---- the slices -------------------------------------------------------------

        /// <summary>One breath of the slices, in ticks: in, held whole, drawn back, held back.</summary>
        private const int SliceIn = 6 * 60, SliceWhole = 14 * 60, SliceOut = 6 * 60, SliceBack = 10 * 60;

        private const int SliceCycle = SliceIn + SliceWhole + SliceOut + SliceBack;

        /// <summary>How much of the slices there is when they are drawn back.</summary>
        private const float SliceLow = 0.2f;

        /// <summary>How fast the slices turn on their own, radians a second.</summary>
        private const float SliceSpin = 0.12f;

        // ---- the turn ---------------------------------------------------------------

        /// <summary>The spring the turn eases on, a tick: about eight seconds to go half round.</summary>
        private const float TurnRate = 0.011f;

        /// <summary>For this last stretch of III, in ticks, the turn heads for upright.</summary>
        private const int ReturnTicks = 15 * 60;

        // ---- the camera -------------------------------------------------------------

        /// <summary>How far the camera wanders at the height of the trip, world pixels.</summary>
        private const float WanderX = 170f, WanderY = 95f;

        /// <summary>How far it leans in: this much always, and up to this much more, and back.</summary>
        private const float LeanBase = 0.08f, LeanSwing = 0.14f;

        /// <summary>Ticks each strength has been on the local player, in a row. Index 1 to 3.</summary>
        private static readonly int[] held = new int[4];

        /// <summary>Ticks since III came on.</summary>
        private static int tripAge;

        /// <summary>The turn, in half turns (1 is upside down), how fast it is going, and where it is going.</summary>
        private static float flip, flipSpeed;
        private static int flipGoal;

        private static bool wasWhole;

        private static FieldInfo activeFilters;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            activeFilters = typeof(FilterManager).GetField("_activeFilters", BindingFlags.Instance | BindingFlags.NonPublic);
            On_FilterManager.EndCapture += DrownOtherFilters;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            Player player = Main.LocalPlayer;
            bool alive = player != null && player.active;

            bool any = false;
            float warp = 0f, clear = Looks[3].Clear, full = Looks[3].Full, edge = 0f, trip = 0f;
            int tripIndex = -1;

            for (int tier = 1; tier <= 3; tier++)
            {
                int index = alive ? player.FindBuffIndex(ManaNausea.TypeFor(tier)) : -1;
                if (index < 0)
                {
                    held[tier] = 0;
                    continue;
                }

                held[tier]++;
                if (tier == 3)
                    tripIndex = index;

                Look look = Looks[tier];
                float weight = Smooth(player.buffTime[index] / (float)ManaNausea.FadeTicks[tier])
                    * Smooth(held[tier] / (float)ComeOn);
                float lookTrip = look.Trip * Smooth(held[tier] / (float)TripRunIn);

                // The first strength present fades in its own shape.
                if (!any)
                {
                    clear = look.Clear;
                    full = look.Full;
                    edge = look.Edge;
                    any = true;
                }

                warp = MathHelper.Lerp(warp, look.Warp, weight);
                clear = MathHelper.Lerp(clear, look.Clear, weight);
                full = MathHelper.Lerp(full, look.Full, weight);
                edge = MathHelper.Lerp(edge, look.Edge, weight);
                trip = MathHelper.Lerp(trip, lookTrip, weight);
            }

            Trip = trip;

            // III's own clock, kept while the turn is still on its way home.
            bool settled = flip == 0f && flipGoal == 0;
            if (tripIndex >= 0)
                tripAge++;
            else if (settled)
                tripAge = 0;

            // The slices breathe.
            int inCycle = tripAge % SliceCycle;
            float breath;
            if (inCycle < SliceIn)
                breath = MathHelper.Lerp(SliceLow, 1f, Smooth(inCycle / (float)SliceIn));
            else if (inCycle < SliceIn + SliceWhole)
                breath = 1f;
            else if (inCycle < SliceIn + SliceWhole + SliceOut)
                breath = MathHelper.Lerp(1f, SliceLow, Smooth((inCycle - SliceIn - SliceWhole) / (float)SliceOut));
            else
                breath = SliceLow;

            float slices = trip * breath;

            // The turn: half round each time the slices close, and home at the end.
            bool ending = tripIndex < 0 || player.buffTime[tripIndex] < ReturnTicks;
            bool whole = !ending && trip > 0.9f && inCycle >= SliceIn && inCycle < SliceIn + SliceWhole;
            if (whole && !wasWhole)
                flipGoal++;
            wasWhole = whole;

            // Ending, it goes on round to the next upright, never back the way it came; a turn
            // asked for that has not started yet is simply dropped.
            if (ending)
                flipGoal = NextEven(flip);

            float pull = TurnRate * TurnRate * (flipGoal - flip) - 2f * TurnRate * flipSpeed;
            flipSpeed += pull;
            flip += flipSpeed;

            // A spring only ever nears where it is going; this close, it is there.
            if (flipGoal - flip < 0.0005f)
            {
                flip = flipGoal;
                flipSpeed = 0f;
            }

            // A whole turn is no turn: upright again is the same as never having turned.
            while (flip >= 2f)
            {
                flip -= 2f;
                flipGoal -= 2;
            }

            settled = flip == 0f && flipGoal == 0;

            if (!Registered)
                return;

            Filter filter = Filters.Scene[FilterKey];

            if (!any && settled)
            {
                if (filter.IsActive())
                    Filters.Scene.Deactivate(FilterKey);

                return;
            }

            if (!filter.IsActive())
                Filters.Scene.Activate(FilterKey, alive ? player.Center : Main.screenPosition);

            float time = Main.GlobalTimeWrappedHourly;
            float turn = MathHelper.Pi * (flip % 2f) + trip * 0.06f * (float)Math.Sin(time * 0.7f);
            float spin = (tripAge / 60f * SliceSpin) % MathHelper.TwoPi;

            filter.GetShader()
                .UseIntensity(warp)
                .UseColor(clear, full, trip)
                .UseSecondaryColor(slices, edge, turn)
                .UseDirection(new Vector2(spin, 0f))
                .UseTargetPosition(alive ? player.Center : Main.screenPosition);
        }

        /// <summary>The camera wanders, slowly, as far as the trip is on.</summary>
        public override void ModifyScreenPosition()
        {
            if (Main.gameMenu || Trip <= 0f)
                return;

            float t = Main.GlobalTimeWrappedHourly;
            var wander = new Vector2(
                (float)(Math.Sin(t * 0.37f) * 0.7 + Math.Sin(t * 0.13f + 1.1f) * 0.3) * WanderX,
                (float)(Math.Sin(t * 0.29f + 0.6f) * 0.7 + Math.Sin(t * 0.11f + 2.3f) * 0.3) * WanderY);

            Main.screenPosition += wander * Trip;
        }

        /// <summary>And leans in and back out, on this frame's view only (see Cinematic's lean for why).</summary>
        public override void ModifyTransformMatrix(ref SpriteViewMatrix transform)
        {
            if (Main.gameMenu || Trip <= 0f)
                return;

            float t = Main.GlobalTimeWrappedHourly;
            float lean = LeanBase + LeanSwing * (0.5f + 0.5f * (float)Math.Sin(t * 0.27f));
            transform.Zoom *= 1f + Trip * lean;
        }

        /// <summary>
        /// Every other screen filter fades out under the trip. The filter manager hands each
        /// filter its fade (Opacity) as the shader's global opacity when it updates; here,
        /// just before the frame's filters are drawn, that is scaled down by the trip. A
        /// filter scaled to nothing is not drawn at all (Filter.IsVisible). The next update
        /// gives every filter its own value back, so nothing is left changed.
        /// </summary>
        private static void DrownOtherFilters(On_FilterManager.orig_EndCapture orig, FilterManager self,
            RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor)
        {
            float keep = 1f - Trip;

            if (keep < 1f && activeFilters?.GetValue(self) is LinkedList<Filter> list)
            {
                Filter mine = Registered ? Filters.Scene[FilterKey] : null;
                foreach (Filter filter in list)
                {
                    if (filter != mine)
                        filter.GetShader().UseGlobalOpacity(filter.Opacity * keep);
                }
            }

            orig(self, finalTexture, screenTarget1, screenTarget2, clearColor);
        }

        public override void OnWorldUnload()
        {
            for (int i = 0; i < held.Length; i++)
                held[i] = 0;

            Trip = 0f;
            tripAge = 0;
            flip = 0f;
            flipSpeed = 0f;
            flipGoal = 0;
            wasWhole = false;

            if (!Main.dedServ && Registered && Filters.Scene[FilterKey].IsActive())
                Filters.Scene.Deactivate(FilterKey);
        }

        /// <summary>The first even number of half turns at or past this one: the next time it is upright.</summary>
        private static int NextEven(float halfTurns) => 2 * (int)Math.Ceiling(halfTurns / 2f - 0.0001f);

        /// <summary>0 to 1, eased at both ends.</summary>
        private static float Smooth(float x)
        {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }
    }
}
