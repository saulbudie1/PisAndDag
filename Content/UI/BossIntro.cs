using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The camera side of one arrival. The plate itself — the look, the name rising,
    /// the flight, the settle — is shared by every boss (see BossIntro's beats) and is
    /// not here to change; a profile only says how the camera gets to the boss, what
    /// it does while there, and how it gets home. (One exception, Brisk: a name-only
    /// plate with its read cut short, for a scene that ends on its name.) The universal
    /// arrival does NOT hide the interface; a boss's own cutscene may.
    ///
    ///     PAN      camera leaves the player for the boss, zoom creeping in
    ///     [plate]  LOOK · RISE · READ · FLY · SETTLE — shared: 3s plain, 7s with a line
    ///     RETURN   camera walks home, zoom lets go
    /// </summary>
    public sealed class BossIntroProfile
    {
        public int PanTicks = 90;
        public int ReturnTicks = 60;

        /// <summary>How far in the camera pushes. Vanilla clamps its own zoom to 1..2; 1 is none.</summary>
        public float ZoomIn = 1.22f;

        /// <summary>Continuous shake through the LOOK. 0 for none.</summary>
        public float HoldShake = 3.4f;

        /// <summary>One burst on the tick the name lands. 0 for none.</summary>
        public float TouchdownShake = 0f;
        public int TouchdownShakeTicks = 0;

        /// <summary>Runs once on the tick the name lands — a roar, a flash, whatever the boss does.</summary>
        public Action OnTouchdown;

        /// <summary>
        /// Take the long plate — the one with a READ beat — even with no line under the
        /// name. For a boss whose own cutscene has earned the time (the King's rite).
        /// </summary>
        public bool FullPlate;

        /// <summary>
        /// For a name-only run whose own scene has held the shot long enough and ends on
        /// its name (the Empress: "waiting too long between the scream and the nameplate,
        /// also cinematic should end as boss name plate roll in"). The READ is cut to
        /// BossIntro.BriskRead, and the shot is let go as the name starts down to the bar:
        /// the bars go, the zoom eases back over the flight, and the run is over on the
        /// touchdown. The rise and the flight are the plate's own. No effect on Play.
        /// </summary>
        public bool Brisk;

        /// <summary>The universal arrival: 1.5s out, the plate, 1s home.</summary>
        public static BossIntroProfile Default => new();
    }


    /// <summary>
    /// The arrival every boss gets when it hasn't written its own, and the machine that
    /// runs the ones that have.
    ///
    /// The universal arrival leaves the interface up. A boss's own cutscene may hide
    /// it; the bar draws through that regardless (BossPlate), so the name always has
    /// something to land on.
    ///
    /// Every boss gets an arrival. One with its own camera work says so with
    /// NPC.PAD().OwnIntro = true in its SetDefaults (BossPlate.HasOwnIntro for a
    /// vanilla type); the pan, hold and return then stay out, and BossPlate fires the
    /// name-only flight for it once its cutscene has the boss centred — or the
    /// cutscene calls PlayNameOnly itself at its own moment. A name-only run still
    /// borrows the zoom: in over the rise, held to the end of the settle, let go over
    /// the profile's ReturnTicks, so a boss's own cutscene gets the same small push in
    /// the universal one has. A boss that wants the universal machine with different
    /// timing hands a BossIntroProfile to BossPlate.Register, or calls Play(index,
    /// profile) itself.
    ///
    /// All of it is local. Nothing here is synced, because a cutscene is something each
    /// player watches rather than something that happens to the world.
    /// </summary>
    public static class BossIntro
    {
        // ============================================================
        // THE PLATE — shared by every boss; two lengths
        // ============================================================
        //
        // Held on the boss, the name rises under it, is left there to be read, flies
        // to the bar, and sits. Universal on purpose: a boss's own animation wraps
        // around this rather than changing it. Seven seconds when there is a line under
        // the name — the READ is for the line, and a name alone does not need one —
        // three when there is not.
        //
        //                 with a line     plain
        //     LOOK        1.5s    90      54      on the boss, nothing yet
        //     RISE        1.0s    60      36      name (and line) fade up under it
        //     READ        2.0s   120       0      both sit still, full, for the reading
        //     FLY         1.0s    60      36      down to the bar; the line goes on touchdown
        //     SETTLE      1.5s    90      54      name home, shot held
        //                 7.0s   420     180

        public const int TitleLook = 90, TitleRise = 60, TitleRead = 120, TitleFly = 60, TitleSettle = 90;
        public const int PlainLook = 54, PlainRise = 36, PlainRead = 0, PlainFly = 36, PlainSettle = 54;

        /// <summary>Look + rise + read + fly + settle, for a boss with a line. What its own camera hold has to cover.</summary>
        public const int TitlePlateTicks = TitleLook + TitleRise + TitleRead + TitleFly + TitleSettle;

        /// <summary>The same, for a boss with the name alone.</summary>
        public const int PlainPlateTicks = PlainLook + PlainRise + PlainRead + PlainFly + PlainSettle;

        /// <summary>The plate this boss gets, decided when the arrival starts from whether it has a line.</summary>
        public static int PlateTicks(bool hasTitle) => hasTitle ? TitlePlateTicks : PlainPlateTicks;

        /// <summary>A brisk plate's READ (BossIntroProfile.Brisk): a breath, enough to see the line whole before it goes.</summary>
        public const int BriskRead = 30;

        /// <summary>
        /// The whole arrival under a profile, spawn to camera home. Pure — reads no
        /// state — so the server can use it too (BossArrivalWait holds a boss still
        /// for exactly this long).
        /// </summary>
        public static int Length(BossIntroProfile p, bool hasTitle) => p.PanTicks + PlateTicks(hasTitle || p.FullPlate) + p.ReturnTicks;

        // ============================================================
        // THE NAME'S FLIGHT
        // ============================================================

        /// <summary>
        /// Where the name rises, as a fraction of the screen. The camera is centred on
        /// the boss by then, so "below the boss" and "below screen centre" are the same
        /// place — and this way there is no world-to-screen conversion to get wrong
        /// while the view matrix is mid-zoom.
        /// </summary>
        private const float RiseHeight = 0.60f;

        /// <summary>The name is larger while it is out in the middle of the screen.</summary>
        private const float RiseScale = 1.45f;

        /// <summary>
        /// The title's last ticks: it fades over this many ticks at the END of the flight,
        /// reaching zero on the tick the name touches the bar. 0 makes it a hard cut on
        /// touchdown instead.
        /// </summary>
        private const int TitleVanishTicks = 8;

        // ============================================================
        // STATE
        // ============================================================

        private static BossIntroProfile profile;
        private static int npcIndex = -1;
        private static int npcType = -1;
        private static int frame;

        // The run's beat boundaries, fixed when it starts from the profile and from
        // whether the boss has a line. Nothing reads the profile's lengths after that.
        private static int riseTicks, flyTicks;
        private static int panEnd, holdEnd, riseEnd, readEnd, flyEnd, settleEnd, total;

        /// <summary>
        /// Name and title only — no camera, no zoom, no shake, no UI hiding. For a boss
        /// that runs its own arrival and wants the name to land on the bar the same way.
        /// </summary>
        private static bool nameOnly;

        /// <summary>This run is a brisk one (BossIntroProfile.Brisk, name-only): the shot let go as the name goes down.</summary>
        private static bool brisk;

        /// <summary>The player's own zoom, borrowed for the shot and given back.</summary>
        private static float savedZoom = 1f;
        private static bool holdingZoom;

        public static bool Running => npcIndex >= 0;

        /// <summary>
        /// The boss whose name was last put up (by Play or PlayNameOnly), kept after its plate is down: so the
        /// plate is never put up a second time for a boss that named itself before its bar was first seen
        /// (BossPlate's wait for a name: a boss's own arrival can hide the interface past its own plate's end).
        /// Forgotten when that fight is over.
        /// </summary>
        private static int namedIndex = -1;
        private static int namedType = -1;

        /// <summary>Has this boss had its name put up already, this fight?</summary>
        public static bool Named(NPC npc) => npc != null && namedIndex == npc.whoAmI && namedType == npc.type;

        /// <summary>Its fight is over: the next boss in its place is a new one, to be named again.</summary>
        public static void ForgetNamed()
        {
            namedIndex = -1;
            namedType = -1;
        }

        /// <summary>True while the player's own zoom is being borrowed.</summary>
        internal static bool HoldingZoom => holdingZoom;

        /// <summary>The player's real zoom, kept for the moment it has to go back.</summary>
        internal static float SavedZoom => savedZoom;

        /// <summary>Whether the boss about to arrive has a line under its name.</summary>
        private static bool HasTitle()
        {
            string title = BossPlate.Current?.Title?.Invoke();

            return !string.IsNullOrWhiteSpace(title);
        }

        /// <summary>
        /// Lays the run out: the profile's pan and return around the plate this boss gets.
        /// Called with nameOnly already set for the run (a brisk plate is a name-only one's).
        /// </summary>
        private static void Layout(BossIntroProfile p)
        {
            bool titled = HasTitle() || p.FullPlate;

            brisk = nameOnly && p.Brisk;

            int look = titled ? TitleLook : PlainLook;
            riseTicks = titled ? TitleRise : PlainRise;
            int read = titled ? TitleRead : PlainRead;
            flyTicks = titled ? TitleFly : PlainFly;
            int settle = titled ? TitleSettle : PlainSettle;

            // Brisk: a breath to read it, and over when it lands (the zoom has come back over the flight).
            if (brisk)
            {
                read = Math.Min(read, BriskRead);
                settle = 0;
            }

            panEnd = p.PanTicks;
            holdEnd = panEnd + look;
            riseEnd = holdEnd + riseTicks;
            readEnd = riseEnd + read;
            flyEnd = readEnd + flyTicks;
            settleEnd = flyEnd + settle;
            total = settleEnd + (brisk ? 0 : p.ReturnTicks);
        }

        /// <summary>
        /// Start the arrival for this NPC with the given profile (null = Default). Called
        /// by BossPlate.Track when a named boss appears; safe to call by hand for a boss
        /// that wants it on a delay or with its own timing.
        /// </summary>
        public static void Play(int index, BossIntroProfile with = null)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (!Main.npc.IndexInRange(index) || !Main.npc[index].active)
                return;

            profile = with ?? BossIntroProfile.Default;
            npcIndex = index;
            npcType = Main.npc[index].type;
            namedIndex = index;
            namedType = npcType;
            frame = 0;
            nameOnly = false;
            Layout(profile);

            if (!holdingZoom)
            {
                savedZoom = Main.GameZoomTarget;
                holdingZoom = true;
            }
        }

        /// <summary>
        /// Just the name's arrival — rise below the boss, fly to the bar, title along for
        /// the ride and gone on touchdown — with none of the panning. Call it from a
        /// boss's own animation at the moment its name should appear. The camera is left
        /// wherever that animation has it; the rise point is screen-relative, so it still
        /// reads as "below the boss" for any shot that has the boss centred. OnTouchdown
        /// and the touchdown shake still fire if the profile has them. The zoom is the
        /// one thing borrowed: in over the rise, held through the settle, released over
        /// ReturnTicks — so the run outlives the landing by settle + return, and
        /// Running says so. A Brisk profile reads for BriskRead and lets the zoom go
        /// over the flight instead: that run is over on the landing.
        /// </summary>
        public static void PlayNameOnly(int index, BossIntroProfile with = null)
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (!Main.npc.IndexInRange(index) || !Main.npc[index].active)
                return;

            profile = with ?? BossIntroProfile.Default;
            npcIndex = index;
            npcType = Main.npc[index].type;
            namedIndex = index;
            namedType = npcType;
            nameOnly = true;
            Layout(profile);

            // Skip straight to the rise. Tick increments before anything reads the
            // frame, so starting AT holdEnd makes the first tick the rise's first frame.
            frame = holdEnd;

            if (!holdingZoom)
            {
                savedZoom = Main.GameZoomTarget;
                holdingZoom = true;
            }
        }

        /// <summary>Cut it short — the player died, the boss died, the world unloaded.</summary>
        public static void Stop()
        {
            if (npcIndex < 0)
                return;

            npcIndex = -1;
            npcType = -1;
            frame = 0;
            nameOnly = false;
            brisk = false;
            profile = null;

            ReleaseZoom();
        }

        public static void Clear()
        {
            npcIndex = -1;
            npcType = -1;
            ForgetNamed();
            frame = 0;
            nameOnly = false;
            brisk = false;
            profile = null;
            ReleaseZoom();
        }

        /// <summary>
        /// GameZoomTarget is a SAVED SETTING — Main.SaveSettings writes it to the config
        /// file — so a zoom left behind would follow the player into every world they
        /// own. It is put back here, on unload, and around every save (see the hook at
        /// the foot of this file).
        /// </summary>
        private static void ReleaseZoom()
        {
            if (!holdingZoom)
                return;

            Main.GameZoomTarget = savedZoom;
            holdingZoom = false;
        }


        // ============================================================
        // THE RUN
        // ============================================================

        internal static void Tick()
        {
            if (npcIndex < 0 || profile == null)
                return;

            if (Main.dedServ || Main.gameMenu)
            {
                Stop();
                return;
            }

            NPC npc = Main.npc[npcIndex];
            Player player = Main.LocalPlayer;

            // The boss left, or something else took its slot. No shot without a subject.
            if (!npc.active || npc.type != npcType || player == null || !player.active || player.dead)
            {
                Stop();
                return;
            }

            frame++;

            // Every arrival is a shot, universal or the boss's own: the screen narrows for it.
            // A brisk one lets go of the shot as the name starts down to the bar.
            if (!brisk || frame <= readEnd)
                Combat.Cinematic.Frame();

            BossIntroProfile p = profile;

            // The landing is the one event with a sound attached, in either mode.
            if (frame == flyEnd)
                Touchdown(player, p);

            // Name-only runs have no shot to manage, only the zoom: in over the rise,
            // held to the end of the settle, let go over the return (a brisk one: held
            // through the read, let go over the flight). The name itself is the bar's
            // again from the landing (NamePlacement).
            if (nameOnly)
            {
                float t;

                if (frame <= riseEnd)
                    t = Ease((frame - holdEnd) / (float)Math.Max(riseTicks, 1));
                else if (brisk)
                    t = 1f - Ease((frame - readEnd) / (float)Math.Max(flyTicks, 1));
                else if (frame <= settleEnd)
                    t = 1f;
                else
                    t = 1f - Ease((frame - settleEnd) / (float)Math.Max(p.ReturnTicks, 1));

                Main.GameZoomTarget = MathHelper.Lerp(savedZoom, p.ZoomIn, t);

                if (frame >= total)
                    Stop();

                return;
            }

            // No UI hiding here, on purpose: the universal arrival keeps the interface
            // up. A boss's own cutscene hides it if it wants to.

            if (frame <= panEnd)
            {
                // Out to the boss. Ease supplies the curve; the spring in
                // ModifyScreenPosition smooths whatever lands.
                float t = Ease(frame / (float)Math.Max(p.PanTicks, 1));

                PisPlayer.BlendCamera(npc.Center, t);
                Main.GameZoomTarget = MathHelper.Lerp(savedZoom, p.ZoomIn, t);
            }
            else if (frame <= settleEnd)
            {
                PisPlayer.BlendCamera(npc.Center, 1f);
                Main.GameZoomTarget = p.ZoomIn;

                // Shake through the look only. Duration 2 rather than 1 for the same
                // reason BlendCamera keeps two frames: draws and ticks do not line up.
                // BumpScreenShake, not Add, so the touchdown burst is never cut short
                // by a hold tick landing after it.
                if (frame <= holdEnd && p.HoldShake > 0f)
                    player.PIS().BumpScreenShake(p.HoldShake, 2);
            }
            else if (frame == settleEnd + 1)
            {
                // One call, and the walk owns the camera the rest of the way — it keeps
                // running even if the boss stops existing mid-pan.
                PisPlayer.PanHome(npc.Center, Math.Max(p.ReturnTicks, 1));
            }

            if (frame > settleEnd)
            {
                float t = Ease((frame - settleEnd) / (float)Math.Max(p.ReturnTicks, 1));

                Main.GameZoomTarget = MathHelper.Lerp(p.ZoomIn, savedZoom, t);
            }

            if (frame >= total)
                Stop();
        }

        /// <summary>The name has touched the bar. The boss gets its one loud moment.</summary>
        private static void Touchdown(Player player, BossIntroProfile p)
        {
            if (p.TouchdownShake > 0f && p.TouchdownShakeTicks > 0)
                player.PIS().BumpScreenShake(p.TouchdownShake, p.TouchdownShakeTicks);

            p.OnTouchdown?.Invoke();
        }


        // ============================================================
        // THE NAME
        // ============================================================

        /// <summary>
        /// Where BossPlate should put the name this frame, while the intro owns it.
        /// False means the intro has no opinion and the name belongs on the bar.
        /// </summary>
        internal static bool NamePlacement(Vector2 rest, out Vector2 pos, out float alpha,
            out float titleAlpha, out float scale, out bool centred)
        {
            pos = rest;
            alpha = 0f;
            titleAlpha = 0f;
            scale = 1f;
            centred = false;

            if (npcIndex < 0 || profile == null)
                return false;

            if (frame <= holdEnd)
            {
                // Before the name is due, the intro still owns it — as invisible. Without
                // this the name would sit on the bar through the pan and the hold.
                return true;
            }

            Vector2 risePoint = new Vector2(Main.screenWidth * 0.5f, Main.screenHeight * RiseHeight);

            if (frame <= riseEnd)
            {
                // Rising: centred under the boss, fading up, no movement. The title
                // fades up with it, at the same rate.
                pos = risePoint;
                alpha = (frame - holdEnd) / (float)riseTicks;
                titleAlpha = alpha;
                scale = RiseScale;
                centred = true;
                return true;
            }

            if (frame <= readEnd)
            {
                // Reading: everything up, nothing moving. This beat exists so the line
                // can be read; a boss without one has it at zero length.
                pos = risePoint;
                alpha = 1f;
                titleAlpha = 1f;
                scale = RiseScale;
                centred = true;
                return true;
            }

            if (frame <= flyEnd)
            {
                // Flying: mid-screen to the bar's left end, shrinking as it goes. The
                // anchor swaps from centred to left-aligned at the END of the flight,
                // so it lands where it will sit rather than sliding sideways on arrival.
                float t = Ease((frame - readEnd) / (float)flyTicks);

                pos = Vector2.Lerp(risePoint, rest + CentreOffset(), t);
                alpha = 1f;
                scale = MathHelper.Lerp(RiseScale, 1f, t);
                centred = true;

                // The title rides the whole way and is gone on the tick of touchdown:
                // full until the last TitleVanishTicks, then straight down to zero at
                // frame == FlyEnd. With TitleVanishTicks at 0 it is simply cut there.
                int left = flyEnd - frame;
                titleAlpha = TitleVanishTicks > 0
                    ? MathHelper.Clamp(left / (float)TitleVanishTicks, 0f, 1f)
                    : (left > 0 ? 1f : 0f);

                return true;
            }

            // Landed. Hand it back to the bar; the title does not come along.
            return false;
        }

        /// <summary>
        /// The flight is drawn centre-anchored the whole way, so its destination has to
        /// be the rest position expressed as a centre rather than a left edge — measured
        /// at the scale the bar draws the name at, or the two would not meet.
        /// </summary>
        private static Vector2 CentreOffset()
        {
            BossPlateEntry entry = BossPlate.Current;

            if (entry == null)
                return Vector2.Zero;

            float w = Terraria.GameContent.FontAssets.DeathText.Value.MeasureString(entry.Name).X;

            return new Vector2(w * BossPlate.NameScale * 0.5f, 0f);
        }

        /// <summary>Smoothstep. Zero slope at both ends, so every move starts and stops softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            return t * t * (3f - 2f * t);
        }
    }


    /// <summary>
    /// Keeps the borrowed zoom out of the player's config file, and stops a cutscene
    /// surviving a world change. Same shape as GraveyardSilence's guard around
    /// ambientVolume, and for the same reason: both are saved settings.
    /// </summary>
    public class BossIntroSystem : ModSystem
    {
        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.SaveSettings += RestoreAroundSave;
        }

        private static bool RestoreAroundSave(On_Main.orig_SaveSettings orig)
        {
            bool holding = BossIntro.HoldingZoom;

            // Put the player's real zoom back for the duration of the write, then take
            // the borrowed one again so a save mid-cutscene does not cut the shot.
            float borrowed = Main.GameZoomTarget;

            if (holding)
                Main.GameZoomTarget = BossIntro.SavedZoom;

            bool result = orig();

            if (holding)
                Main.GameZoomTarget = borrowed;

            return result;
        }

        public override void OnWorldUnload() => BossIntro.Clear();

        public override void PreSaveAndQuit() => BossIntro.Clear();
    }
}
