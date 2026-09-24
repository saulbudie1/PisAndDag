using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// Every number the Abomanation is made of, in one place.
    ///
    /// The six at the top are the ones asked for by name. They are static and writable
    /// rather than const so they can be turned at runtime — a command, a debug panel, a
    /// second difficulty — without a rebuild.
    ///
    /// Read <see cref="Speed"/> as a speed and <see cref="TurnSpeed"/> as a turn rate,
    /// because that is what they are. Desert Scourge's own code calls its acceleration
    /// "speed" and keeps the actual travel cap in a separate local, which is why nothing
    /// in that file does what its name says. Speed is pixels per tick travelled.
    ///
    /// NOTE: TurnSpeed CHANGED UNITS. It used to be pixels per tick of velocity change,
    /// which is an acceleration, and steering by accelerating each axis toward a target
    /// velocity is exactly what made the worm stop dead and reverse when you got behind
    /// it: a target pointing the opposite way drives both axes through zero on the same
    /// tick, so the head halts, every segment piles onto it, and the body turns inside
    /// out. It is now RADIANS PER TICK, the rate the heading may swing while the speed is
    /// held, which makes a turn an arc and a reversal a U turn.
    ///
    /// The turning circle is Speed / TurnSpeed pixels. A value set in the old unit means
    /// something entirely different here.
    /// </summary>
    public static class Abomanation
    {
        public const string Folder = "PisAndDag/Content/NPCs/Abomanation_Small/";

        // ------------------------------------------------------------- the six knobs --

        /// <summary>
        /// Ignore the ground entirely. False is the desert worm: it swims through tiles,
        /// and when it breaks into open air near you it arcs and falls like a thrown
        /// thing. True makes it a flier that steers the same everywhere, which is what
        /// you want for an arena fight with no sand in it.
        /// </summary>
        public static bool Fly = false;

        /// <summary>
        /// How fast it travels, in world pixels per tick.
        ///
        /// 6.5 is 24 tiles a second. For scale, a player runs 11 and Hermes Boots make 22,
        /// so it is just faster than a booted player and can be kited but not shaken off,
        /// which is about right for something met before hardmode.
        ///
        /// See the note on <see cref="TurnSpeed"/> before changing it, because the two are
        /// joined, and the note on <see cref="RushTimeout"/>, because a slower worm takes
        /// longer to finish an attack and the watchdog has to be let out to match.
        ///
        /// It is also faster than the old steering ever managed at the same setting. That
        /// one bled speed into every turn and spent most of a fight under its own cap; this
        /// one only ever changes speed along the heading, so it holds this number all the
        /// time. Same figure, quicker animal.
        /// </summary>
        public static float Speed = 6.5f;

        /// <summary>
        /// How hard it corners, in RADIANS PER TICK of heading. See the note on the class.
        ///
        /// The turning circle that falls out of this is Speed / TurnSpeed pixels, so 0.0275
        /// against a speed of 6.5 gives a radius of about 236, which is fifteen tiles: a U
        /// turn thirty tiles across, taking just under two seconds. Much higher and it stops
        /// reading as a worm and starts reading as a homing missile that happens to be long.
        ///
        /// SCALE IT WITH Speed. The two make one number between them and dropping Speed on
        /// its own does the opposite of what it sounds like: a slower worm at the same turn
        /// rate corners TIGHTER, because it covers less ground while swinging through the
        /// same angle. Halve Speed alone and the ten tile circle becomes five, which reads
        /// as more agile, not less. These pairs all hold the same ten tile circle:
        ///
        ///     Speed 9    TurnSpeed 0.0382   34 tiles/s
        ///     Speed 8    TurnSpeed 0.0339   30 tiles/s
        ///     Speed 7    TurnSpeed 0.0296   26 tiles/s
        ///     Speed 6.5  TurnSpeed 0.0275   24 tiles/s   <- here
        ///     Speed 5    TurnSpeed 0.0212   19 tiles/s
        ///
        /// To make it lazier as well as slower, go under the paired figure.
        /// </summary>
        public static float TurnSpeed = 0.0275f;

        /// <summary>
        /// How quickly it gets up to speed, in pixels per tick per tick. Only ever applied
        /// along the heading, never across it, which is what keeps a turn from costing it
        /// any speed.
        /// </summary>
        public static float SpeedChange = 0.35f;

        /// <summary>
        /// How far off the player it aims, in world pixels. At 0 it drives at your exact
        /// centre, which is both easy to sidestep and impossible to dodge at close range.
        /// The offset wanders rather than orbits (see AbomanationHead.StrayOffset), so
        /// there is no circle to learn.
        /// </summary>
        public static float Stray = 140f;

        /// <summary>Travel speed during the rush straight up, in pixels per tick.</summary>
        public static float RushSpeed = 15f;

        /// <summary>
        /// How far the rush climbs from the point it turned around, in TILES.
        ///
        /// It turns around at <see cref="DiveDepth"/> below you, so this number has to
        /// beat DiveDepth/16 for the rush to reach you at all. The default clears it by
        /// twelve tiles, which puts the head out of the sand and above your head.
        /// </summary>
        public static int Distance = 62;

        // --------------------------------------------------------------- the rest -----

        /// <summary>Body segments between the head and the tail.</summary>
        public const int BodySegments = 15;

        /// <summary>Distinct body textures, cycled in order along the body.</summary>
        public const int BodyTextures = 5;

        /// <summary>
        /// Bottom arms on ONE side. The body segments are the only thing that carries any,
        /// so this is the whole count for that side of the creature.
        ///
        /// Seventeen over fifteen segments averages just over one each, so most segments
        /// carry one, a few carry two or three and several carry none. That unevenness is
        /// the point: a fixed number per segment reads as a machine.
        /// </summary>
        public const int BottomArmsPerSide = 14;

        /// <summary>Most one segment may carry on one side.</summary>
        public const int BottomArmsPerSegment = 3;

        /// <summary>
        /// Centre to centre, head to first body segment.
        ///
        /// The head's sprite reaches 36 pixels behind its centre and a body segment's
        /// reaches 24 in front of its own, so this buys a 16 pixel overlap. It needs to be
        /// more than the body's own 10, because the head is 56 wide against the body's 48
        /// and the step between them shows as a notch in the neck at anything looser.
        /// </summary>
        public const float HeadSpacing = 44f;

        /// <summary>Centre to centre, body segment to body segment. Under the sprite height, so they overlap.</summary>
        public const float BodySpacing = 42f;

        /// <summary>Centre to centre, last body segment to tail.</summary>
        public const float TailSpacing = 46f;

        /// <summary>
        /// How much the chain bends per segment when it is laid out at spawn, in radians.
        ///
        /// It starts pointing straight down and turns by this much at every joint, so the
        /// whole body is an arc of radius BodySpacing / BuriedCurve. At 0.055 that is about
        /// 760 pixels, which bends the 678 pixel body through roughly fifty degrees: enough
        /// to read as something lying in the ground rather than a post driven into it, and
        /// not so much that it coils. Zero drops it straight down. The sign is rolled at
        /// spawn, so it leans either way.
        /// </summary>
        public const float BuriedCurve = 0.055f;

        /// <summary>How far below the player it digs before turning around, in world pixels.</summary>
        public const float DiveDepth = 800f;

        /// <summary>Ticks of chasing before the rush. Only counts while it is below you or well off to the side.</summary>
        public const int RushGate = 600;

        /// <summary>
        /// Ticks an attack is allowed to take before it is abandoned and the chase resumes.
        ///
        /// THIS HAS TO GROW WHEN Speed SHRINKS. The dive is a fixed DISTANCE, so a slower
        /// worm spends longer covering it, and an attack that outlasts this is thrown away
        /// half finished: the boss turns round mid dive and goes back to chasing, which
        /// looks like it forgot what it was doing. Longest attack measured over eight
        /// fights at each setting, with the watchdog switched off:
        ///
        ///     Speed 9     dive 165  rush 121  fall  78    needs 240
        ///     Speed 8     dive 179  rush 123  fall  92    needs 270
        ///     Speed 7     dive 269  rush 148  fall 105    needs 390
        ///     Speed 6.5   dive 370  rush 225  fall 129    needs 510   <- here
        ///     Speed 6     dive 353  rush 176  fall 121    needs 480
        ///
        /// It was 180. At Speed 6.5 the dive alone runs to 370, so 180 threw EVERY rush
        /// away part way down: the boss turned round mid dive and went back to chasing,
        /// which reads as it forgetting what it was doing. Measured over ten fights.
        /// </summary>
        public const int RushTimeout = 510;

        /// <summary>
        /// Past this distance it keeps swimming even in open air, so a worm crossing the
        /// map to reach you does not flop through the sky one parabola at a time.
        /// </summary>
        public const float AirSwimDistance = 1000f;

        // ------------------------------------------------------------- the pass by ----
        //
        // A worm does not stop on top of you, it goes THROUGH and comes back. Steering at
        // your centre right up to the moment of contact is what made it sit on your head
        // like a normal worm; these three make it pick a point on the far side of you and
        // commit to reaching that instead.

        /// <summary>
        /// How close it has to get before it stops aiming at you and locks a run past you,
        /// in world pixels.
        /// </summary>
        public const float PassTrigger = 300f;

        /// <summary>Nearest it will aim past you, in TILES beyond wherever you are.</summary>
        public const float MinPass = 8f;

        /// <summary>Furthest it will aim past you, in TILES beyond wherever you are.</summary>
        public const float MaxPass = 20f;

        /// <summary>
        /// Ticks a locked run is held before it gives up and re-aims. Only a safety net:
        /// the run normally ends because the point was reached.
        /// </summary>
        public const int PassHold = 240;

        /// <summary>
        /// Gravity on a worm that is out of the ground, in pixels per tick per tick.
        ///
        /// This is the ONLY thing done to a worm in the air. Nothing caps how fast it
        /// falls, nothing bleeds its sideways speed, nothing puts a floor under anything:
        /// it keeps exactly the velocity it left the ground with and this is added to the
        /// downward half of it, so the arc is a consequence of how fast and how steeply it
        /// came out, and a fast shallow exit throws it a long way while a slow steep one
        /// barely clears the sand.
        ///
        /// It used to have a drop cap and a sideways floor and a drag. The drag was the
        /// mistake: bleeding the sideways speed away meant a steep hop arrived at the top
        /// of its arc with both halves of the velocity at nothing, the head hung there,
        /// and everything else was scaffolding to hide it. Measured over six fights,
        /// taking the drag out drops that to zero on its own, with no floor and no cap:
        /// 192 arcs, median sideways speed at the top 5.65 px a tick, not one heading
        /// change above a third of a radian in a tick.
        ///
        /// How strong, measured at Speed 6.5 against a moving player, six fights each:
        ///
        ///     gravity   air time (median)   top of arc (median)   sideways (median)
        ///     0.20      0.63 s              4 tiles               11 tiles    <- "buries itself"
        ///     0.11      1.17 s              7 tiles               19 tiles    (vanilla worms)
        ///     0.09      1.3 s               8 tiles               22 tiles    <- here
        ///     0.06      1.47 s              9 tiles               28 tiles
        ///
        /// 0.2 was too much: most exits were a short flat hop that was back in the sand
        /// inside forty ticks, which reads as the worm diving on purpose the moment it
        /// touches air. This keeps the momentum it left the ground with for a real arc,
        /// and still comes down.
        /// </summary>
        public const float AirGravity = 0.09f;

        // --------------------------------------------------------------- the call ----

        /// <summary>
        /// Small animals let go on the Wasted Mile before it comes. Ten: the Clothier's
        /// "nest of ten". Each one is taken; the tenth brings it. PADWorld.MileOfferings
        /// is the count.
        /// </summary>
        public const int Offerings = 10;

        // --------------------------------------------------------------- arrival ----
        //
        // It does not appear under you. It is called up a long way off and comes to you
        // slowly, through the surface, in a wave, with the camera on it the whole way;
        // when the camera comes home it is close and the fight begins. Nothing in this
        // block touches the fight. AbomanationHead.Arrive drives it.

        /// <summary>How far from the player it surfaces, in tiles. Off screen at any zoom.</summary>
        public const int ArriveDistance = 120;

        /// <summary>
        /// Pixels a tick it travels toward the player while arriving. ArriveDistance less
        /// ArriveSpeed × ArriveTicks is where it is when the fight starts: 120 tiles less
        /// 3 × 540 / 16 = 101 tiles leaves it about twenty tiles out, close enough to be
        /// on you inside two seconds of the camera coming home.
        /// </summary>
        public const float ArriveSpeed = 3f;

        /// <summary>Half height of the wave it swims in, in pixels, centred on the ground line: this far out of the ash at a crest, this far under at a trough.</summary>
        public const float ArriveWave = 64f;

        /// <summary>Ticks per wave.</summary>
        public const int ArrivePeriod = 150;

        /// <summary>Ticks the camera takes to reach it.</summary>
        public const int ArrivePanOut = 60;

        /// <summary>Ticks the camera sits on it before the name rises.</summary>
        public const int ArriveLook = 90;

        /// <summary>Ticks the camera takes to come home at the end.</summary>
        public const int ArrivePanBack = 60;

        /// <summary>
        /// The screech: the last thing it does before the camera comes home, this many
        /// ticks of it, ending as the camera sets off. Its voice (ScreechSound) is two
        /// seconds, so it fills this; the growl and the breath after it go out with the camera.
        /// The screen takes it (Cinematic.Roar), the mouth is held wide, and its wave
        /// stands still for it (AbomanationHead.Arrive), so it stays up out of the ash
        /// rather than screeching its way back under.
        /// </summary>
        public const int ScreechTicks = 120;

        /// <summary>How much higher it rears while it screeches, in pixels, on top of where its wave had it.</summary>
        public const float ScreechRear = 24f;

        /// <summary>The arrival tick the screech starts on.</summary>
        public static int ScreechAt => ArriveTicks - ArrivePanBack - ScreechTicks;

        /// <summary>
        /// The screech's voice, made for it (Content/Sounds/AbomanationScreech.ogg): low and
        /// rough, a roar with a scream riding on it. A growl first, two seconds of voice
        /// falling away at the end, then a growl and a breath.
        /// </summary>
        public static readonly SoundStyle ScreechSound = new SoundStyle("PisAndDag/Content/Sounds/AbomanationScreech");

        /// <summary>
        /// The whole arrival: pan out, look, the name's flight (rise, read, fly, settle,
        /// the same lengths every boss with a line gets), pan back. 540 at the plate's
        /// current lengths.
        /// </summary>
        public static int ArriveTicks => ArrivePanOut + ArriveLook
            + BossIntro.TitleRise + BossIntro.TitleRead + BossIntro.TitleFly + BossIntro.TitleSettle
            + ArrivePanBack;

        // -------------------------------------------------------------- the whip ----
        //
        // Occasional. It comes for you at its ordinary pace until it is close, then the
        // head turns in a tight loop and the body follows it round into a coil, and then
        // the head drives straight out of the coil and the body comes after it with all
        // the turn still in it: link by link it unwinds, each link swinging faster than
        // the one before, and the tail leaves the end of the whole animal in one wide arc
        // at three or four tiles a tick. Then it swings back, less, and lies down.
        //
        // NOTHING HERE IS SCRIPTED. The body during the crack is a chain of links with
        // inertia (AbomanationChain.Swing): each link's angle is a damped spring toward
        // the link ahead, stiffer toward the tail, with a cap on how fast any link may
        // turn. Everything the lash does — the coil, the wave, the wide arc, the recoil —
        // falls out of the head's own two moves and that rule. Change the loop or the
        // run and the lash changes with them.
        //
        // Phases 4 (the run in), 5 (the loop), 6 (the crack). The head drives; the
        // segments swing.
        //
        // Two ways it was done before this, both wrong, recorded so they stay wrong:
        // a rigid rod turning about a fixed segment (a bar, not a whip), and the same
        // rod with a scripted wave down it (a wave through a bar). And one way that was
        // right and unusable: the body as a rope with momentum and a length constraint,
        // one pass a tick. That gave the true shape — the coil, the arc — and a tail at
        // 280 pixels a tick whatever the head did, because a chain that cannot stretch
        // and cannot bend concentrates everything into its tip, which is why real whips
        // crack. Slowing it down did not slow the tip. The angular chain keeps the shape
        // and puts a ceiling on the tip.

        /// <summary>One in this many completed rushes is followed by a whip.</summary>
        public const int WhipChance = 3;

        /// <summary>Least ticks between two whips. Twenty seconds.</summary>
        public const int WhipGap = 1200;

        /// <summary>Only rolls if the head is within this of the player, in pixels; a whip from across the map is a long run in.</summary>
        public const float WhipRange = 900f;

        /// <summary>
        /// How close the head gets before it loops, in TILES. The run in is ordinary
        /// chasing that gets faster the longer it takes (WhipChaseGain), so the approach
        /// reads as the fight carrying on rather than a cutscene starting.
        /// </summary>
        public const float WhipNear = 5f;

        /// <summary>Ticks of run in before it loops from wherever it is, or gives up. Stops the endless circling.</summary>
        public const int WhipRunIn = 150;

        /// <summary>Held in the coil after the loop, before the crack. A second, so it can be dodged.</summary>
        public const int WhipHold = 60;

        /// <summary>How fast it creeps round the coil while holding, as a fraction of the loop speed.</summary>
        public const float WhipHoldPace = 0.3f;

        /// <summary>
        /// How much faster the run in gets for every tick it lasts, as a fraction of
        /// Speed. It starts at the ordinary chase and climbs: a player who keeps away
        /// is run down, not followed. Turn rate climbs with it, so the circle it turns
        /// in stays the same size.
        /// </summary>
        public const float WhipChaseGain = 0.004f;

        /// <summary>Most the run in may reach, as a multiple of Speed. Two: about the rush.</summary>
        public const float WhipChaseMax = 2f;

        // ---- the loop -----------------------------------------------------------------
        //
        // The head goes round once. The body, following as it always does, is drawn into
        // the circle behind it: the coil. The circle has to be roomy — the body follows
        // by closing on whatever is ahead of it, which cuts every corner, and a circle
        // much under a hundred pixels is cut so short the body stays straight and only
        // the neck kinks. At a hundred the head passes back over its own body and the
        // coil is a thing you can see.

        /// <summary>Speed round the loop, pixels a tick.</summary>
        public const float WhipLoopSpeed = 9f;

        /// <summary>Radius of the loop, pixels. See the note above before shrinking it.</summary>
        public const float WhipLoopRadius = 100f;

        /// <summary>How many times round. One.</summary>
        public const float WhipLoopTurns = 1f;

        /// <summary>The loop, in ticks: 70 at the numbers above, a second and a bit. The tell.</summary>
        public static readonly int WhipLoopTicks =
            (int)Math.Round(WhipLoopTurns * MathHelper.TwoPi * WhipLoopRadius / WhipLoopSpeed);

        // ---- the crack ----------------------------------------------------------------

        /// <summary>Speed of the head's drive out of the coil, pixels a tick, straight along the heading it looped back to.</summary>
        public const float WhipRunSpeed = 10f;

        /// <summary>
        /// Ticks the head holds that line. The arc is thrown in the first thirty or so;
        /// after this the head goes back to chasing while the body finishes settling
        /// behind it, rather than driving fifty tiles in a straight line for the sake
        /// of a tail that has already stopped.
        /// </summary>
        public const int WhipRunStraight = 50;

        /// <summary>
        /// How long the body is left to swing before it is handed back to the follow
        /// chain. Measured: the tail is fast for the first forty six ticks and back to
        /// trailing the head by ninety three, so this is where the recoil has died.
        /// </summary>
        public const int WhipRunTicks = 95;

        /// <summary>The tick the tail is at its fastest, which is when the crack is heard. Measured: 24.</summary>
        public const int WhipCrackAt = 24;

        // ---- the chain ----------------------------------------------------------------
        //
        // Measured, head looping at 9 round 100 and running out at 10, six settings
        // tried; this one:
        //
        //     tail peak   60 px a tick, at tick 24 of the run
        //     over 25     46 ticks
        //     reach       the whole body, 42 tiles, straight out from the head at full arc
        //     settled     93 ticks
        //
        // A stiffer tail (0.45 / 0.18) peaks at 67 and takes longer to settle; a softer
        // one (0.30 / 0.13) peaks at 51. The peak lands where the cap puts it.

        /// <summary>Spring at the first link behind the head: how hard it is pulled toward the head's line, per tick, per radian off it.</summary>
        public const float WhipSpringHead = 0.05f;

        /// <summary>Spring at the tail. Higher than the head's: that is the taper, and the taper is what makes the tip the fast end.</summary>
        public const float WhipSpringTail = 0.38f;

        /// <summary>Per-tick retention of each link's turning speed while the arc is being thrown.</summary>
        public const float WhipDamping = 0.90f;

        /// <summary>What that falls to once the arc has gone, so the recoil is one swing and not a rope in the wind.</summary>
        public const float WhipSettleDamping = 0.60f;

        /// <summary>Tick of the run the damping starts falling, and how many ticks it takes to get there.</summary>
        public const int WhipSettleAt = 25;
        public const int WhipSettleOver = 30;

        /// <summary>Most any link may turn in a tick, radians. This is the ceiling on the tip: at the tail's reach it is about sixty pixels a tick.</summary>
        public const float WhipMaxTurn = 0.16f;

        /// <summary>Ticks the whole move may take before it is abandoned.</summary>
        public const int WhipTimeout = 600;

        // ----------------------------------------------------------------- the throw --
        //
        // A whip does not push, it carries. Anything the lash catches leaves along the
        // tip's own line of travel — the tangent, which is the swing — with a share of
        // straight out from the pivot on top of it, so a hit near the top of the arc
        // throws you up and away and a hit low throws you along the ground and off it.
        // It is written onto the player's velocity rather than handed to the knockback
        // system, so a Cobalt or Ankh Shield changes nothing: it is not knockback, it is
        // being hit by something moving fast.

        /// <summary>Share of the segment's own speed at the point of contact that the player leaves with.</summary>
        public const float WhipToss = 0.34f;

        /// <summary>And then this much again. Four: the tail at full swing throws you a screen and a half, and the bounces take it from there.</summary>
        public const float WhipTossBoost = 4f;

        /// <summary>Slowest a throw can be, in pixels a tick, so a brush from the neck still puts you down.</summary>
        public const float WhipTossMin = 8f;

        /// <summary>
        /// Least upward speed a throw carries. Zero: the throw goes exactly where the
        /// segment was going, into the ground if that is where it was going. Raise it if
        /// a hit should always take the air.
        /// </summary>
        public const float WhipTossLift = 0f;

        /// <summary>Upward speed past which the body tumbles in the air rather than rolls along the ground. See RagdollPlayer.</summary>
        public const float WhipSpinLift = 8f;

        /// <summary>Ticks on the ground after a whip hit, before you can get up. A second and a half.</summary>
        public const int WhipLie = 90;

        // -------------------------------------------------------------------- blood --
        //
        // There is no skin on this animal anywhere, so everything that lands on it lands
        // in meat. What comes off depends on what went in: a blade throws a sheet of it
        // off the edge, along the line of the cut and away from the arm that swung; a
        // shot pushes a narrow streak out the way it was travelling and spills the rest
        // around the hole. Both are BloodDroplet, which already falls, stretches with
        // speed and reads the local light.

        /// <summary>The colour of it. The same red the hit dust uses.</summary>
        public static readonly Color Blood = new Color(150, 8, 12);

        /// <summary>Damage that counts as a full-strength hit. Under it the spray scales down, over it up, within the clamp below.</summary>
        public const float BloodReference = 120f;

        /// <summary>Floor and ceiling on that scaling, so a pinprick still shows and a nuke does not fill the screen.</summary>
        public const float BloodLeast = 0.4f;
        public const float BloodMost = 2f;

        // ---- a blade ----

        /// <summary>Droplets thrown off the edge at full strength.</summary>
        public const int SlashDroplets = 14;

        /// <summary>How far along the blade the spray is spread, in pixels, for a blade of SlashReach. The length of the cut.</summary>
        public const float SlashSpan = 46f;

        /// <summary>Sprite length of an ordinary sword. Longer blades cut wider, shorter ones narrower, within the clamp below.</summary>
        public const float SlashReach = 40f;
        public const float SlashShortest = 0.6f;
        public const float SlashLongest = 1.8f;

        /// <summary>How fast it leaves the edge, in pixels a tick, before scaling.</summary>
        public const float SlashSpeed = 7.5f;

        /// <summary>Half-angle of the fan off the edge, in radians. Narrow: a sheet, not a cloud.</summary>
        public const float SlashFan = 0.38f;

        /// <summary>Droplets that run along the cut itself rather than off it, at a fraction of the speed.</summary>
        public const int SlashTrail = 4;

        // ---- a shot ----

        /// <summary>Droplets in the streak out the far side at full strength.</summary>
        public const int PierceDroplets = 10;

        /// <summary>How fast the streak leaves, before scaling. Faster than a slash: it is being pushed, not flung.</summary>
        public const float PierceSpeed = 9f;

        /// <summary>Share of the projectile's own speed added to that. A slug throws further than an arrow.</summary>
        public const float PierceCarry = 0.22f;

        /// <summary>Half-angle of the streak, in radians. Tight.</summary>
        public const float PierceCone = 0.16f;

        /// <summary>The rest of it, coming out around the hole rather than through it.</summary>
        public const int SpillDroplets = 8;

        /// <summary>How fast the spill leaves, before scaling.</summary>
        public const float SpillSpeed = 3.4f;

        /// <summary>Half-angle of the spill, in radians. Wide.</summary>
        public const float SpillCone = 0.95f;

        /// <summary>How far to either side of the entry the spill starts, in pixels.</summary>
        public const float SpillOffset = 9f;

        /// <summary>Droplets that come back out the way the shot went in.</summary>
        public const int BackSpatter = 3;

        /// <summary>How fast those leave, before scaling. Barely.</summary>
        public const float BackSpatterSpeed = 2.2f;

        // -------------------------------------------------------------- second wind --

        /// <summary>
        /// The roar at the change, on the screen: Cinematic.Roar, four seconds, and
        /// lighter than a cutscene's (1): it goes off in the middle of the fight and
        /// should not take the fight away. Its shake is the whole of the shake; the
        /// ground punch it used to have is gone.
        /// </summary>
        public const int RageRoarTicks = 240;
        public const float RageRoarStrength = 0.45f;

        // ---------------------------------------------------------------- the end ---
        //
        // It does not die when its life runs out. It stops, the camera goes to it, and
        // it rises: straight up, out of whatever it is in, into open air, and there it
        // climbs once round a loop the size of itself until its own tail is in its
        // mouth. Then it eats: a piece sits in its mouth a while, it bites, the piece
        // bursts, and it steps forward one link to the next, the body closing up behind
        // it and the loop drawing in by the link that is gone, quicker and quicker,
        // winding in on itself, until the last piece is its own neck and the head is all
        // there is. Then the head goes. The player watches. Nothing about this is a
        // fight.
        //
        // The body never follows the head through any of this. From the rise on, every
        // part is placed by the head: along the head's own path while it rises and
        // loops, so the body threads exactly where the head went and no link cuts a
        // corner; and on the loop once it is closed, this far behind the head round a
        // circle sized to what is left. So there is never a gap anywhere: the body is
        // always at the head's neck, and the last piece is always at its mouth.

        /// <summary>Ticks the head takes to come to a stop while the camera arrives. A second and a half; matches Cinematic.Pan.</summary>
        public const int DieStop = 90;

        /// <summary>The rise. It turns to face straight up at this rate, in radians a tick, and climbs at this speed until it is at the loop's height.</summary>
        public const float DieRiseSpeed = 11f;
        public const float DieRiseTurn = 0.1f;

        /// <summary>
        /// Room the loop needs. It rises until it finds open air at least DieRoom tiles
        /// tall above it (the surface, nearly always; a cave that tall will do), and the
        /// loop sits in that air with this much clearance, in pixels, over the floor and
        /// under the roof. Already in air that tall, at any height, and it loops where it is.
        /// </summary>
        public const float DieClearance = 28f;
        public const int DieRoom = (int)((DieLoopRadius * 2f + DieClearance * 2f) / 16f) + 1;

        /// <summary>The animal's length along the chain, head centre to tail centre.</summary>
        public const float ChainLength = HeadSpacing + BodySpacing * (BodySegments - 1) + TailSpacing;

        /// <summary>
        /// The mouth: how far ahead of the head's centre the piece being eaten sits. The
        /// same as the neck sits behind it, so the loop is the body plus one link and
        /// the tail meets the head exactly as the neck does.
        /// </summary>
        public const float DieMouth = HeadSpacing;

        /// <summary>The loop with the whole animal on it: its circumference and its radius. It shrinks a link a bite from here.</summary>
        public const float DieLoop = ChainLength + DieMouth;
        public const float DieLoopRadius = DieLoop / MathHelper.TwoPi;

        /// <summary>The lap round the loop: speed along it in pixels a tick, and over how many pixels at the end it slows, to this fraction, onto its tail.</summary>
        public const float DieLapSpeed = 9f;
        public const float DieLapEase = 140f;
        public const float DieLapCrawl = 0.35f;

        /// <summary>Which side the loop goes on: the side with fewer solid tiles in its way; a tie goes toward the player. Sampled every this many tiles.</summary>
        public const int DieSideStep = 2;

        /// <summary>The bite: over the last ticks of a hold the head lunges this far, in pixels, into the piece in its mouth, and nods this much, in radians.</summary>
        public const int DieBiteTicks = 8;
        public const float DieLunge = 10f;
        public const float DieBiteNod = 0.16f;

        /// <summary>
        /// Ticks each piece takes, whole: the first entries in order, then the last value
        /// for every one after. The mouth runs through its four frames in four equal
        /// quarters of it: neutral, opening, wide, and shut, which is the chomp. The
        /// piece bursts as the mouth shuts, and the head steps forward to the next one
        /// with it shut, over that last quarter. Two seconds for the first, down to a
        /// third of a second.
        /// </summary>
        public static readonly int[] DiePiece = { 120, 68, 40, 24, 20 };

        /// <summary>The head's sheet: four frames down. Neutral, mouth a little open, wide, shut.</summary>
        public const int HeadFrames = 4;
        public const int MouthNeutral = 0, MouthOpen = 1, MouthWide = 2, MouthShut = 3;

        /// <summary>The face held through the screech and the roar: frame 2 counting from 0, the wide one.</summary>
        public const int RoarMouth = MouthWide;

        /// <summary>Nearer than this to the player, or to an animal it is after, and the mouth opens a little.</summary>
        public const float MouthNear = 90f;

        /// <summary>Ticks the mouth stays shut after a bite lands.</summary>
        public const int ChompTicks = 20;

        /// <summary>Blood from the mouth while it eats: one drop every this many ticks.</summary>
        public const int DieDripEvery = 4;

        /// <summary>A beat, with nothing left but the head, before the head goes.</summary>
        public const int DieLast = 24;

        /// <summary>How much of the way the camera moves toward what it should be looking at each tick. It follows the head up and then settles on the loop, and stays there while the head goes round it.</summary>
        public const float DieLook = 0.07f;

        /// <summary>Droplets when a segment goes, and when the head goes.</summary>
        public const int DieBurst = 220;
        public const int DieBurstHead = 300;

        /// <summary>Slowest and fastest a burst droplet leaves, in pixels a tick.</summary>
        public const float DieBurstLeast = 2.5f;
        public const float DieBurstMost = 13f;

        /// <summary>Each piece goes with a death cry, this much pitch either side of true so no two are the same.</summary>
        public const float DieRoarVary = 0.25f;

        /// <summary>The head goes with the roar, dropped this far and this loud, and a shake: strength in pixels, cycles a second, ticks, and how far away it is still felt.</summary>
        public const float DieRoarPitch = -0.8f;
        public const float DieRoarVolume = 1.6f;
        public const float DieRoarShake = 40f;
        public const float DieRoarShakeRate = 4f;
        public const int DieRoarShakeTicks = 70;
        public const float DieRoarShakeReach = 3000f;

        /// <summary>The shake as each segment goes, and as the head goes.</summary>
        public const float DieShake = 7f;
        public const float DieShakeHead = 18f;
        public const int DieShakeTicks = 12;
        public const int DieShakeHeadTicks = 36;

        /// <summary>Contact damage multiplier during the crack at the first segment behind the head. It rises along the body to WhipTailDamage.</summary>
        public const float WhipDamage = 1.25f;

        /// <summary>Contact damage multiplier at the tail. The tip is what a whip hits with.</summary>
        public const float WhipTailDamage = 2f;

        // ------------------------------------------------------------ second wind ----
        //
        // Two halves to the fight, split on health. The first is everything above: the
        // chase, the rush now and then, the whip rarely. The second is the same animal
        // hurt: quicker, aiming at you rather than near you, and whipping as often as it
        // rushes. It announces the change once, low and slow.

        /// <summary>Further than this from the player, in pixels, and it dives and is gone.</summary>
        public const float LeaveRange = 1000*16f+500f;

        /// <summary>A rush aims at where the player was this many ticks ago, in every mode and both halves. Stand still and it is the same thing.</summary>
        public const int RushLag = 20;

        /// <summary>A rush that lands feeds it: this fraction of its full life back, per strike. In the second half it cannot climb back out of it: capped at RagePoint.</summary>
        public const float RushFeed = 1f / 30f;

        /// <summary>How far it will go for a bunny, in pixels, and how often it looks, in ticks.</summary>
        public const float PreyRange = 3200f;
        public const int PreyLook = 15;

        /// <summary>Legends: more of it to get through, and it hits harder. On top of the mode's numbers.</summary>
        public const float LegendsLife = 1.25f;
        public const float LegendsDamage = 1.3f;

        /// <summary>Health fraction the second half starts at.</summary>
        public const float RagePoint = 0.4f;

        /// <summary>Speed AND turn rate multiplier in the second half, so the turning circle is unchanged.</summary>
        public const float RageSpeed = 1.2f;

        /// <summary>Second half: a whip after every this many finished rushes. Counted, not rolled.</summary>
        public const int RageWhipEvery = 2;

        /// <summary>Least ticks between whips in the second half.</summary>
        public const int RageWhipGap = 240;

        /// <summary>
        /// The roar at the change: SoundID.Roar, twice at once. The deep voice an octave
        /// down, as low as a sound can be played, which draws its two seconds out to
        /// four; and under it the voice it has always had, this far down, for the body
        /// of it. Low, long and loud.
        /// </summary>
        public const float RageDeepPitch = -1f;
        public const float RagePitch = -0.7f;

        // ------------------------------------------------------------------ legends --
        //
        // Legends: the same fight, meaner. It is quicker; it whips more often in the
        // first half; and in the second half every rush is
        // followed by a whip, and a whip by a check: if you have got far away, it comes
        // for you at once, faster than it has all fight.

        /// <summary>Speed multiplier, chase and rush, on top of everything else.</summary>
        public const float LegendsSpeed = 1.12f;

        /// <summary>First half: ticks between whips, rolled fresh after each. Fourteen to seventeen seconds, and no coin: when it is time and you are in range, it whips.</summary>
        public const int LegendsWhipGapLeast = 840;
        public const int LegendsWhipGapMost = 1020;

        /// <summary>Second half: after every rush, this long, rolled, and then the whip. Four to six seconds.</summary>
        public const int LegendsRageWhipWaitLeast = 240;
        public const int LegendsRageWhipWaitMost = 360;

        /// <summary>Second half: this long after the whip it checks where you are, and if you are further than this it rushes at once.</summary>
        public const int LegendsAfterWhip = 60;
        public const float LegendsFarRush = 160f * 16f;

        /// <summary>That rush, and its dive, are this much faster again.</summary>
        public const float LegendsFarRushBoost = 1.5f;
    }

    /// <summary>
    /// The limbs. Drawn by the segments, owned by nobody.
    ///
    /// The arm sprites all run bottom to top: the narrow nub at the bottom of the canvas
    /// is where the limb meets the body, and the mass at the top is the far end. So the
    /// origin is the bottom of the sprite, an unrotated arm points along its own -Y, and
    /// a quarter turn puts it out to one side. Everything below is that one fact applied
    /// twice, once per side.
    ///
    /// Positions are in SPRITE pixels measured from the segment's centre, and are scaled
    /// by NPC.scale at draw time. Angles are in radians and are measured from "forward",
    /// meaning along the body toward the head, so a positive angle rakes an arm toward
    /// the tail on the right side and toward the head on the left.
    /// </summary>
    public static class AbomanationArms
    {
        // ------------------------------------------------------------ bottom arms -----

        /// <summary>
        /// How far out from the centre line the bottom arms are rooted.
        ///
        /// A body segment is 48 wide, so 16 puts the root 8 pixels inside its edge and the
        /// arm comes out from under the mass rather than off the side of it. Further out
        /// and the root sits on the silhouette, which reads as stuck on.
        /// </summary>
        public const float BottomInset = 16f;

        /// <summary>Half the span the bottom arms are spread over along a body segment.</summary>
        public const float BottomSpan = 14f;

        // ---------------------------------------------------------------- the mess ----
        //
        // Three scatters, all dealt from the head's arm seed, so a leg keeps its own
        // crookedness for the whole fight and picks a different one next summon.
        //
        // Spreading the legs evenly over the span was the right way to stop them landing
        // on top of each other and the wrong way to make an animal: every socket sat on
        // the same grid and the eye reads a grid instantly. These put every leg somewhere
        // slightly wrong instead, which is what stops fifteen segments of the same five
        // sprites looking stamped.

        /// <summary>How far along the body a leg may sit from where the spacing put it, in pixels.</summary>
        public const float BottomScatter = 11f;

        /// <summary>How far in or out of the body a leg may be rooted from <see cref="BottomInset"/>, in pixels.</summary>
        public const float BottomInsetScatter = 5f;

        /// <summary>How far a leg's resting angle may sit from the shared rake, in radians.</summary>
        public const float BottomAngleScatter = 0.55f;

        /// <summary>
        /// How far a leg's swing may sit out of step with the wave, in radians. Small on
        /// purpose: the travelling beat is the thing that reads as walking, and enough of
        /// this would grind it back into noise.
        /// </summary>
        public const float BottomSwingScatter = 0.5f;

        /// <summary>Radians the bottom arms are raked back toward the tail at rest.</summary>
        public const float BottomRake = 0.30f;

        /// <summary>Radians either side of the rake that a swinging arm travels.</summary>
        public const float Swing = 0.38f;

        /// <summary>Radians per second of the swing.</summary>
        public const float SwingSpeed = 8.2f;

        /// <summary>
        /// Phase added per SEGMENT LENGTH down the body. This is the whole trick: give
        /// every leg the same phase and the whole worm beats as one paddle, which reads as
        /// a mistake. Offset them by how far down the body they sit and the beat travels,
        /// which is what a centipede does and what the eye is looking for.
        ///
        /// Measured along the body rather than counted per row, so the wave stays
        /// continuous across segments that carry different numbers of legs.
        /// </summary>
        public const float SwingWave = 0.9f;

        // --------------------------------------------------------------- top arms -----

        /// <summary>How far out from the centre line the top arms are rooted, at most.</summary>
        public const float TopInset = 11f;

        /// <summary>Half the span the top arms are scattered over, along the body.</summary>
        public const float TopSpan = 13f;

        /// <summary>Radians the top arms are raked back toward the tail. Same meaning as <see cref="BottomRake"/>.</summary>
        public const float TopRake = 0.45f;

        /// <summary>Radians of scatter on a top arm's angle.</summary>
        public const float TopJitter = 0.50f;

        // ---------------------------------------------------------------- assets ------
        //
        // Requested on first draw rather than in SetStaticDefaults. Draw code never runs
        // on a server, so this is the one place the request can be made without also
        // having to guard for a machine that has no graphics device.

        // The TOP arm tables below are indexed [variant, side], side 0 being the LEFT of
        // the segment, with an unhanded sprite listed twice so a lookup never has to know
        // which ones those are. The BOTTOM arms are a flat list; see below.

        // All six leg sprites, as six separate shapes rather than three handed pairs, and
        // every one of them available to both sides.
        //
        // Treating them as pairs meant a side only ever saw three of the six: half the art
        // was on the other flank where you could not compare it. The mirrored copies are
        // not really the same limb seen from the other side anyway, because the arm they
        // are drawn on is rotated to point outward rather than flipped, so a mirrored
        // sprite reads as its own shape. Two of them hook the other way, which is the one
        // that looks like a hand doubling back.
        private static readonly string[] BottomNames =
        {
            "Arm_On_Bottom_1",
            "Arm_On_Bottom_1_L",
            "Arm_On_Bottom_2",
            "Arm_On_Bottom_2_L",
            "Arm_On_Bottom_3",
            "Arm_On_Bottom_3_L",
        };

        private static readonly string[,] TopNames =
        {
            { "Arm_On_Top_1", "Arm_On_Top_1" },
            { "Arm_On_Top_2", "Arm_On_Top_2" },
            { "Arm_On_Top_3_L", "Arm_On_Top_3" },
        };

        // Origins are the bottom middle of the DRAWN shape, read off each sprite pixel by
        // pixel, not the middle of the canvas. Four of the ten sit off-centre and three
        // sit two rows above the bottom of their canvas, so a nominal bottom-centre origin
        // would plant most of them a few pixels outside the body or hanging off it.
        //
        // Nor are the six the same length: the _1 and _2 shapes are 30 tall and the _2
        // pair is drawn on a canvas more than twice as wide, while _3 runs to 40 and so
        // reaches noticeably further. That spread is the point of using all six.
        private static readonly Vector2[] BottomOrigins =
        {
            new Vector2(5f, 30f),    // Arm_On_Bottom_1      14x30, nub at x4-5
            new Vector2(9f, 30f),    // Arm_On_Bottom_1_L    14x30, nub at x8-9
            new Vector2(15f, 30f),   // Arm_On_Bottom_2      30x30, foot at x10-19
            new Vector2(15f, 30f),   // Arm_On_Bottom_2_L    30x30, foot at x10-19
            new Vector2(5f, 40f),    // Arm_On_Bottom_3      14x40, foot at x0-9
            new Vector2(9f, 40f),    // Arm_On_Bottom_3_L    14x40, foot at x4-13
        };

        private static readonly Vector2[,] TopOrigins =
        {
            { new Vector2(5f, 22f), new Vector2(5f, 22f) },
            { new Vector2(5f, 22f), new Vector2(5f, 22f) },
            { new Vector2(7f, 28f), new Vector2(7f, 28f) },
        };

        /// <summary>How many bottom arm sprites there are to choose between.</summary>
        public static int BottomVariants => BottomNames.Length;

        /// <summary>How many top arm sprites there are to choose between.</summary>
        public static int TopVariants => TopNames.GetLength(0);

        private static Asset<Texture2D>[] bottomArt;
        private static Asset<Texture2D>[,] topArt;

        private static Asset<Texture2D> BottomArt(int which)
        {
            bottomArt ??= new Asset<Texture2D>[BottomVariants];

            which = which < 0 ? 0 : which % BottomVariants;

            // ImmediateLoad rather than the default async: an arm requested during a draw
            // is wanted by that draw. Asynchronously it is not there for the first frame
            // or two, which is a worm that grows its legs after it arrives.
            return bottomArt[which] ??= ModContent.Request<Texture2D>(
                Abomanation.Folder + BottomNames[which], AssetRequestMode.ImmediateLoad);
        }

        private static Asset<Texture2D> Art(ref Asset<Texture2D>[,] cache, string[,] names, int which, int side)
        {
            int variants = names.GetLength(0);

            cache ??= new Asset<Texture2D>[variants, 2];

            which = which < 0 ? 0 : which % variants;
            side &= 1;

            return cache[which, side] ??= ModContent.Request<Texture2D>(
                Abomanation.Folder + names[which, side], AssetRequestMode.ImmediateLoad);
        }

        /// <summary>
        /// Pulls every arm sprite in at load time, off the draw path entirely. Called from
        /// AbomanationBody.SetStaticDefaults; <see cref="Art"/> still works on its own if
        /// it is not.
        /// </summary>
        public static void Load()
        {
            if (Main.dedServ)
                return;

            for (int which = 0; which < BottomVariants; which++)
                BottomArt(which);

            for (int side = 0; side < 2; side++)
            {
                for (int which = 0; which < TopVariants; which++)
                    Art(ref topArt, TopNames, which, side);
            }
        }

        /// <summary>Called from AbomanationHead.Unload. Static asset handles outlive a mod reload otherwise.</summary>
        public static void Unload()
        {
            bottomArt = null;
            topArt = null;
            counts = null;
            firstArm = null;
            decks = null;
        }

        // ------------------------------------------------------------- how many -------
        //
        // How the legs are shared out along the body. Built once, read by every segment.

        private static int[][] counts;
        private static int[][] firstArm;

        private const int LeftSeed = 2;
        private const int RightSeed = 11;

        /// <summary>
        /// How far a leg may wander from its even spacing, as a fraction of one spacing.
        /// 0 is a comb. Above about 1.5 the legs start piling up again, which is the thing
        /// this is here to stop.
        /// </summary>
        private const float SpreadJitter = 1.2f;

        /// <summary>
        /// How far the right side is shifted along from the left, in spacings. Half a step
        /// interleaves the two sides so they alternate down the body instead of pairing
        /// off, which is both what a many legged thing does and what stops the eye finding
        /// rungs on a ladder.
        /// </summary>
        private const float RightOffset = 0.5f;

        /// <summary>
        /// Shares <paramref name="total"/> arms out over <paramref name="segments"/>
        /// segments, none of them carrying more than <paramref name="most"/>.
        ///
        /// Spaced evenly and then jogged, rather than drawn out of a bag. The bag was
        /// correct and looked wrong: a fair shuffle clumps, which is exactly what makes
        /// shuffled music sound broken, and over fifteen segments it was handing one
        /// segment three legs while leaving the two either side of it bare. Measured, the
        /// bag left six segments empty with a run of two, against three and a run of one
        /// here, at a third of the variance.
        ///
        /// Each leg starts at its own share of the body and is moved by up to
        /// <see cref="SpreadJitter"/> of a spacing, so nothing sits on a grid but nothing
        /// bunches either. The count comes out exact because the loop places one leg per
        /// turn and only ever chooses WHERE, never whether.
        ///
        /// The jog is the same hash everything else here uses, so this is stable across
        /// frames, across saves and across machines with nothing to sync.
        /// </summary>
        private static int[] Spread(int segments, int total, int most, int seed, float offset)
        {
            int[] made = new int[segments];

            if (segments <= 0 || total <= 0)
                return made;

            float step = segments / (float)total;

            for (int k = 0; k < total; k++)
            {
                float at = (k + 0.5f + offset) * step + (Hash01(seed, k) - 0.5f) * SpreadJitter * step;

                int seg = (int)Math.Floor(at);
                seg = seg < 0 ? 0 : (seg >= segments ? segments - 1 : seg);

                // Full, so walk outward to the nearest segment with room. It cannot fail to
                // find one, because there are never more legs than segments times most.
                if (made[seg] >= most)
                {
                    for (int d = 1; d < segments; d++)
                    {
                        int low = seg - d;
                        int high = seg + d;

                        if (low >= 0 && made[low] < most) { seg = low; break; }
                        if (high < segments && made[high] < most) { seg = high; break; }
                    }
                }

                if (made[seg] < most)
                    made[seg]++;
            }

            return made;
        }

        /// <summary>Bottom arms on one side of one body segment. 0 to <see cref="Abomanation.BottomArmsPerSegment"/>.</summary>
        public static int BottomCount(int place, int side)
        {
            Build();

            int[] row = counts[side & 1];

            return place >= 0 && place < row.Length ? row[place] : 0;
        }

        /// <summary>
        /// How many arms this side carries before this segment, so an arm can be numbered
        /// along the whole flank rather than only within its own segment. That number is
        /// what indexes the deck below.
        /// </summary>
        public static int FirstArm(int place, int side)
        {
            Build();

            int[] row = firstArm[side & 1];

            return place >= 0 && place < row.Length ? row[place] : 0;
        }

        private static void Build()
        {
            if (counts != null)
                return;

            counts = new[]
            {
                Spread(Abomanation.BodySegments, Abomanation.BottomArmsPerSide, Abomanation.BottomArmsPerSegment, LeftSeed, 0f),
                Spread(Abomanation.BodySegments, Abomanation.BottomArmsPerSide, Abomanation.BottomArmsPerSegment, RightSeed, RightOffset),
            };

            firstArm = new int[2][];

            for (int side = 0; side < 2; side++)
            {
                int[] row = counts[side];
                int[] running = new int[row.Length];
                int sum = 0;

                for (int i = 0; i < row.Length; i++)
                {
                    running[i] = sum;
                    sum += row[i];
                }

                firstArm[side] = running;
            }
        }

        // ------------------------------------------------------------------ deck ------
        //
        // Which sprite each arm along a side wears. Dealt rather than rolled per arm: a
        // roll per arm would use some sprites four times and miss others entirely, which
        // is the thing having six of them is meant to avoid.
        //
        // Keyed on a seed the head rolls at spawn and syncs, so it is the same on every
        // machine for the whole fight and different the next time the boss is summoned.

        private static int[][] decks;
        private static int deckSeed;
        private static bool deckBuilt;

        /// <summary>
        /// The run of sprite indices for one side, one entry per arm, in body order.
        ///
        /// Every sprite is dealt the same whole number of times first, and the arms left
        /// over after that go to that many DIFFERENT sprites picked at random, so with
        /// fourteen arms and six sprites everything is worn twice and two lucky ones are
        /// worn three times. Then the run is shuffled, so the order down the body is not
        /// the order of the table.
        /// </summary>
        public static int[] Deck(int seed, int side)
        {
            if (!deckBuilt || deckSeed != seed || decks == null)
            {
                decks = new[] { Deal(seed, 0), Deal(seed, 1) };
                deckSeed = seed;
                deckBuilt = true;
            }

            return decks[side & 1];
        }

        private static int[] Deal(int seed, int side)
        {
            int total = Abomanation.BottomArmsPerSide;
            int variants = BottomVariants;

            int[] run = new int[total < 0 ? 0 : total];

            if (run.Length == 0 || variants <= 0)
                return run;

            int each = total / variants;
            int spare = total - each * variants;
            int at = 0;

            for (int which = 0; which < variants; which++)
            {
                for (int n = 0; n < each; n++)
                    run[at++] = which;
            }

            // The spares go to different sprites, never twice to the same one, by shuffling
            // the list of sprites and taking off the front. Rolling each spare on its own
            // could hand both to the same sprite and leave the spread more lopsided than
            // dealing them was meant to make it.
            if (spare > 0)
            {
                int[] lucky = new int[variants];

                for (int i = 0; i < variants; i++)
                    lucky[i] = i;

                Shuffle(lucky, Hash(seed, 900 + side));

                for (int i = 0; i < spare && i < variants; i++)
                    run[at++] = lucky[i];
            }

            Shuffle(run, Hash(seed, 700 + side));

            return run;
        }

        private static void Shuffle(int[] list, uint salt)
        {
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = (int)(Hash((int)salt, i) % (uint)(i + 1));
                int swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }

        // ----------------------------------------------------------------- noise ------
        //
        // A hash rather than Main.rand, because the scatter has to be the same every
        // frame and the same on every machine. Rolling it once and keeping it in a field
        // would survive the frames but not the network: a client builds its own copy of
        // the NPC and would roll its own arms. A hash of the segment index is free, has
        // no state to sync, and gives the same answer everywhere forever.

        private static uint Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 2246822519u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A stable number in [0, 1) for this segment and slot.</summary>
        public static float Hash01(int a, int b) => (Hash(a, b) & 0xFFFFFFu) / 16777216f;

        /// <summary>A stable whole number in [0, n) for this segment and slot.</summary>
        public static int HashInt(int a, int b, int n) => n <= 1 ? 0 : (int)(Hash(a, b) % (uint)n);

        // ------------------------------------------------------------------ draw ------

        private static void One(SpriteBatch spriteBatch, Asset<Texture2D> asset, Vector2 origin,
            NPC npc, Vector2 screenPos, Color colour, Vector2 local, float localAngle)
        {
            if (asset is null || !asset.IsLoaded)
                return;

            Texture2D texture = asset.Value;

            // The offset is in the segment's own frame, so it turns with the segment. A
            // world-space offset would leave the legs standing still while the body rolled.
            Vector2 world = npc.Center + local.RotatedBy(npc.rotation) * npc.scale;

            spriteBatch.Draw(texture, world - screenPos, null, colour,
                npc.rotation + localAngle, origin, npc.scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The legs. Only the body segments have any; the tail does not call this.
        ///
        /// The two sides are given their counts separately rather than as a number of
        /// pairs, because they come out of <see cref="BottomCount"/> and are deliberately
        /// lopsided: a segment can carry two on one side and none on the other.
        ///
        /// However many a side has, they are spread evenly over <paramref name="span"/>
        /// either side of the segment's middle. Spread rather than scattered: one leg
        /// sits in the middle, two straddle it, three make a row, and none of the three
        /// can land on top of another.
        ///
        /// <paramref name="place"/> is how far down the body this segment sits. It only
        /// reaches the swing phase.
        /// </summary>
        public static void Bottom(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            int place, int leftCount, int rightCount, float span, bool swing, int armSeed)
        {
            for (int side = 0; side < 2; side++)
            {
                int count = side == 0 ? leftCount : rightCount;

                if (count <= 0)
                    continue;

                // Where this segment's arms start in the run dealt for this whole side.
                int[] deck = Deck(armSeed, side);
                int first = FirstArm(place, side);

                // -1 is the left of the segment, +1 the right. A quarter turn times this
                // points the arm out that way: rotating "up" by +PiOver2 gives right, by
                // -PiOver2 gives left.
                float sign = side == 0 ? -1f : 1f;

                for (int row = 0; row < count; row++)
                {
                    // Where this arm sits in the run dealt for this whole side. Everything
                    // crooked about it is keyed on this, so it is the same arm every frame
                    // and on every machine, and a different arm the next time it spawns.
                    int at = first + row;
                    int which = deck.Length > 0 ? deck[at % deck.Length] : 0;
                    int salt = 1000 + side * 64 + at;

                    float y = count == 1 ? 0f : MathHelper.Lerp(-span, span, row / (float)(count - 1));

                    // Off the grid, up or down the body.
                    y += (Hash01(armSeed, salt) - 0.5f) * BottomScatter;

                    // In or out of the flank, so the roots are not all on one line either.
                    float inset = BottomInset + (Hash01(armSeed, salt + 1) - 0.5f) * BottomInsetScatter;

                    // Its own resting angle, not the shared rake.
                    float angle = sign * (MathHelper.PiOver2 + BottomRake
                        + (Hash01(armSeed, salt + 2) - 0.5f) * BottomAngleScatter);

                    if (swing)
                    {
                        // Distance down the whole worm, in segment lengths, so a leg near
                        // the back of one segment is very nearly in phase with a leg near
                        // the front of the next.
                        float along = place + y / Abomanation.BodySpacing;

                        float phase = Main.GlobalTimeWrappedHourly * SwingSpeed
                            + along * SwingWave
                            + (Hash01(armSeed, salt + 3) - 0.5f) * BottomSwingScatter
                            + side * MathHelper.Pi;

                        angle += sign * (float)Math.Sin(phase) * Swing;
                    }

                    One(spriteBatch,
                        BottomArt(which),
                        BottomOrigins[which],
                        npc, screenPos, colour,
                        new Vector2(sign * inset, y), angle);
                }
            }
        }

        /// <summary>
        /// The growths on the back. One or two per segment, scattered, and dead still
        /// relative to the body: they are part of the mass, not limbs it is walking on.
        ///
        /// A second one is always put on the opposite side from the first, because two
        /// rolls of the same hash land on top of each other often enough to notice.
        /// </summary>
        public static void TopScattered(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour, int seed)
        {
            int count = 1 + HashInt(seed, 1, 2);
            float first = Hash01(seed, 2) < 0.5f ? -1f : 1f;

            for (int i = 0; i < count; i++)
            {
                float sign = i == 0 ? first : -first;

                Vector2 local = new Vector2(
                    sign * TopInset * (0.55f + 0.45f * Hash01(seed, 10 + i)),
                    MathHelper.Lerp(-TopSpan, TopSpan, Hash01(seed, 20 + i)));

                float angle = sign * (MathHelper.PiOver2 + TopRake)
                    + (Hash01(seed, 30 + i) - 0.5f) * TopJitter;

                TopOne(spriteBatch, npc, screenPos, colour, local, angle,
                    sign < 0f ? 0 : 1, HashInt(seed, 40 + i, TopVariants));
            }
        }

        /// <summary>
        /// The tail's growths. <paramref name="rows"/> pairs, evenly spaced, fixed: the
        /// tail is the one part of this that is meant to look deliberate.
        /// </summary>
        public static void TopPairs(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            int rows, float rowSpan, float inset)
        {
            for (int row = 0; row < rows; row++)
            {
                float y = rows == 1 ? 0f : MathHelper.Lerp(-rowSpan, rowSpan, row / (float)(rows - 1));

                for (int side = 0; side < 2; side++)
                {
                    float sign = side == 0 ? -1f : 1f;
                    float angle = sign * (MathHelper.PiOver2 + TopRake);

                    TopOne(spriteBatch, npc, screenPos, colour,
                        new Vector2(sign * inset, y), angle, side, row % TopVariants);
                }
            }
        }

        /// <summary>
        /// One top arm. <paramref name="which"/> picks the sprite and the tables handle the
        /// handedness, so nothing here has to know that only the third one is mirrored.
        /// </summary>
        private static void TopOne(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Color colour,
            Vector2 local, float angle, int side, int which)
        {
            which = which < 0 ? 0 : which % TopVariants;
            side &= 1;

            One(spriteBatch, Art(ref topArt, TopNames, which, side), TopOrigins[which, side],
                npc, screenPos, colour, local, angle);
        }
    }

    /// <summary>
    /// What every segment behind the head does, which is the same thing: sit a fixed
    /// distance behind whatever is in front of it, pointing at it.
    ///
    /// ai[1] is the segment ahead, ai[2] is the head, and BOTH are stored one higher than
    /// the index they mean, so 0 can be read as "not linked yet". Desert Scourge stores
    /// them raw and then tests them with <c>ai[1] &gt; 0f</c>, which quietly means a worm
    /// whose head landed in NPC slot zero has no leader and no realLife. It is rare and
    /// it is still wrong. <see cref="Ahead"/> and <see cref="Head"/> do the decoding.
    ///
    /// ai[3] is this segment's place in the chain, which picks its texture and seeds its
    /// arms.
    /// </summary>
    public static class AbomanationChain
    {
        /// <summary>The NPC index this segment follows, or -1 while it is unlinked.</summary>
        public static int Ahead(NPC npc) => (int)npc.ai[1] - 1;

        /// <summary>The head's NPC index, or -1 while it is unlinked.</summary>
        public static int Head(NPC npc) => (int)npc.ai[2] - 1;

        /// <summary>Writes the links. <paramref name="ahead"/> and <paramref name="head"/> are plain NPC indices.</summary>
        public static void Link(NPC npc, int ahead, int head, int place, bool flipped)
        {
            // ai[0] used to hold a backward link that nothing read. It now holds whether
            // this segment's sprite is drawn mirrored, which is a thing that IS read, and
            // which needs to survive the trip to every client. ai[] rides along with the
            // NPC sync packet, so putting it here costs nothing and needs no extra state.
            npc.ai[0] = flipped ? 1f : 0f;
            npc.ai[1] = ahead + 1;
            npc.ai[2] = head + 1;
            npc.ai[3] = place;
            npc.realLife = head;
        }

        /// <summary>Whether this segment draws its sprite mirrored. Rolled once, at spawn.</summary>
        public static bool Flipped(NPC npc) => npc.ai[0] != 0f;

        /// <summary>
        /// Drags the segment up behind its leader and points it that way.
        ///
        /// Written to position rather than velocity on purpose. A follower that steered
        /// would lag, and fifteen lagging followers turn a worm into an accordion; moving
        /// it to exactly the right place every tick is what keeps the body rigid at any
        /// speed the head can reach.
        /// </summary>
        public static void Follow(NPC npc, NPC leader, float spacing)
        {
            Vector2 delta = leader.Center - npc.Center;
            float distance = delta.Length();

            if (distance < 0.0001f)
                return;

            npc.rotation = delta.ToRotation() + MathHelper.PiOver2;

            npc.velocity = Vector2.Zero;
            npc.position += delta * ((distance - spacing) / distance);
        }

        /// <summary>
        /// The whip. While the head is cracking, every segment behind the pivot is laid
        /// on a straight rod out of the pivot at the head's crack angle instead of
        /// following, so the whole back end moves as one thing and the tip moves fast.
        /// The rod's step is the ordinary spacing, so when the crack ends and Follow
        /// takes over again there is nothing for it to correct.
        /// </summary>
        /// <summary>How hard this segment hits during the crack: rises from WhipDamage at the neck to WhipTailDamage at the tail.</summary>
        public static float LashDamage(NPC npc)
        {
            float along = (int)npc.ai[3] / (float)Abomanation.BodySegments;
            return MathHelper.Lerp(Abomanation.WhipDamage, Abomanation.WhipTailDamage, along);
        }

        /// <summary>The head this segment belongs to, as the boss, or null. Works for the head itself too.</summary>
        public static AbomanationHead Boss(NPC npc)
        {
            if (npc.ModNPC is AbomanationHead self)
                return self;

            int head = Head(npc);

            return head >= 0 && head < Main.maxNPCs ? Main.npc[head].ModNPC as AbomanationHead : null;
        }

        /// <summary>Whether any part of this animal is forbidden to touch the player right now: while it arrives, and while it dies.</summary>
        public static bool Harmless(NPC npc)
        {
            AbomanationHead boss = Boss(npc);

            return boss != null && (boss.Arriving || boss.Dying);
        }

        /// <summary>Whether this animal is eating itself.</summary>
        public static bool Dying(NPC npc) => Boss(npc)?.Dying ?? false;

        /// <summary>How far along the chain, from the head's centre, this part's centre sits.</summary>
        public static float Along(int place)
        {
            return place >= Abomanation.BodySegments
                ? Abomanation.ChainLength
                : Abomanation.HeadSpacing + Abomanation.BodySpacing * place;
        }

        /// <summary>How far along the chain the part before this one sits: the head's centre, for the neck.</summary>
        public static float AlongBefore(int place) => place <= 0 ? 0f : Along(place - 1);

        /// <summary>The part at this place in this head's chain, or null if it has gone.</summary>
        public static NPC PartAt(int head, int place)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (!other.active || Head(other) != head || (int)other.ai[3] != place)
                    continue;

                if (other.ModNPC is AbomanationBody || other.ModNPC is AbomanationTail)
                    return other;
            }

            return null;
        }

        /// <summary>
        /// The next part to go: the one furthest from the head that is still there.
        /// Eating runs tail first, so this is always the highest place left, and every
        /// client works it out the same way from the same synced state.
        /// </summary>
        public static NPC NextToGo(int head)
        {
            NPC best = null;
            int bestPlace = -1;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (!other.active || Head(other) != head)
                    continue;

                if (!(other.ModNPC is AbomanationBody) && !(other.ModNPC is AbomanationTail))
                    continue;

                int place = (int)other.ai[3];

                if (place > bestPlace)
                {
                    bestPlace = place;
                    best = other;
                }
            }

            return best;
        }

        /// <summary>
        /// A part comes apart. Blood in every direction, fast and slow together so it
        /// reads as a burst and not a ring, the vanilla dust on top for the clients that
        /// do not run particles, and a shake sized to the piece.
        /// </summary>
        public static void Burst(NPC npc, int droplets, float shake, int shakeTicks)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < droplets; i++)
            {
                Vector2 way = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                float speed = Main.rand.NextFloat(Abomanation.DieBurstLeast, Abomanation.DieBurstMost);

                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    npc.Center + way * Main.rand.NextFloat(npc.width * 0.4f),
                    way * speed,
                    Main.rand.Next(34, 66),
                    Main.rand.NextFloat(0.7f, 1.45f),
                    Abomanation.Blood));
            }

            for (int i = 0; i < droplets / 3; i++)
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, 0f, 0f, 0, default, 1.4f);

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(npc.Center,
                Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2(), shake, 8f, shakeTicks, 1600f, "Abomanation"));
        }

        /// <summary>Whether this segment is being thrown right now: its head is in the crack.</summary>
        public static bool Lashing(NPC npc)
        {
            AbomanationHead boss = Boss(npc);

            return boss != null && boss.Cracking;
        }

        /// <summary>
        /// A segment's tick of the tell: while it lashes, its hit goes through every
        /// shield, and CCBypass shows that. Quiet, so fifteen segments do not sound
        /// fifteen notes; the head sounds the one, at the loop.
        /// </summary>
        public static void Flag(NPC npc)
        {
            if (Lashing(npc))
                npc.PAD().CCBypass = true;
        }

        // ------------------------------------------------------------------ blood --

        /// <summary>
        /// How hard this hit was, as a multiple of an ordinary one. Everything the blood
        /// does is scaled by this, so the same code covers a copper shortsword and a
        /// post-Moon-Lord shot without either looking wrong.
        /// </summary>
        private static float Force(NPC.HitInfo hit)
        {
            return MathHelper.Clamp(hit.Damage / Abomanation.BloodReference,
                Abomanation.BloodLeast, Abomanation.BloodMost);
        }

        /// <summary>
        /// Where the hit landed, as near as can be worked out: the point on this
        /// segment's box closest to whatever hit it. Spawning at the centre instead puts
        /// the spray inside the animal, where half of it is never seen.
        /// </summary>
        private static Vector2 Contact(NPC npc, Vector2 from)
        {
            return Vector2.Clamp(from, npc.position, npc.position + npc.Size);
        }

        private static void Drop(Vector2 at, Vector2 velocity, float scale)
        {
            PADParticleHandler.SpawnParticle(new BloodDroplet(
                at, velocity, Main.rand.Next(26, 48), scale, Abomanation.Blood));
        }

        /// <summary>
        /// A blade landed. True melee only: OnHitByItem fires for the swung item itself,
        /// never for anything it threw, so there is nothing else to test for.
        ///
        /// The blade lies along the player's item rotation. What comes off it does not
        /// go along that line, it goes ACROSS it, off the edge, on the side away from the
        /// arm holding it, in a narrow fan spread along the length of the cut. That is
        /// what makes it read as a slash rather than a puff: the droplets start in a line
        /// and leave together.
        /// </summary>
        public static void Slashed(NPC npc, Player player, Item item, NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            float force = Force(hit);
            Vector2 at = Contact(npc, player.Center);

            // A shortsword does not open the same wound a greatsword does. The sprite's
            // long side is the blade.
            float span = Abomanation.SlashSpan * MathHelper.Clamp(
                Math.Max(item.width, item.height) / Abomanation.SlashReach,
                Abomanation.SlashShortest, Abomanation.SlashLongest);

            // Along the blade, and across it.
            Vector2 cut = player.itemRotation.ToRotationVector2();
            Vector2 fling = new Vector2(-cut.Y, cut.X);

            // The edge throws outward, away from the one swinging. Which of the two
            // perpendiculars that is depends on the swing, so it is decided by where the
            // animal is rather than by guessing at the animation.
            if (Vector2.Dot(fling, npc.Center - player.Center) < 0f)
                fling = -fling;

            int count = (int)(Abomanation.SlashDroplets * force);

            for (int i = 0; i < count; i++)
            {
                // Spread along the cut, thickest in the middle of it.
                float along = (Main.rand.NextFloat() + Main.rand.NextFloat() - 1f) * span * 0.5f;

                float angle = Main.rand.NextFloat(-Abomanation.SlashFan, Abomanation.SlashFan);
                float speed = Abomanation.SlashSpeed * force * Main.rand.NextFloat(0.6f, 1.25f);

                Drop(at + cut * along,
                     fling.RotatedBy(angle) * speed,
                     Main.rand.NextFloat(0.55f, 1f));
            }

            // A few that run along the cut instead of off it, slower, so the line of the
            // wound is still readable after the sheet has gone.
            for (int i = 0; i < Abomanation.SlashTrail; i++)
            {
                float side = Main.rand.NextBool() ? 1f : -1f;

                Drop(at + cut * Main.rand.NextFloat(-span * 0.5f, span * 0.5f),
                     cut * side * Abomanation.SlashSpeed * 0.35f * force + fling * Main.rand.NextFloat(0.5f, 1.5f),
                     Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        /// <summary>
        /// Something was shot into it. The streak goes the way the projectile was going,
        /// times how hard it hit and a share of how fast it was travelling. Around that,
        /// the spill: slower, much wider, started a little to either side of the hole
        /// rather than in it, which is what stops the whole thing reading as one jet. A
        /// little comes back out the way the shot went in.
        /// </summary>
        public static void Pierced(NPC npc, Projectile projectile, NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            float force = Force(hit);

            Vector2 way = projectile.velocity.SafeNormalize(Vector2.UnitX);
            float carried = projectile.velocity.Length() * Abomanation.PierceCarry;

            // The hole is where the projectile is, pulled onto the segment it hit.
            Vector2 at = Contact(npc, projectile.Center);
            Vector2 across = new Vector2(-way.Y, way.X);

            int count = (int)(Abomanation.PierceDroplets * force);

            for (int i = 0; i < count; i++)
            {
                float angle = Main.rand.NextFloat(-Abomanation.PierceCone, Abomanation.PierceCone);
                float speed = (Abomanation.PierceSpeed + carried) * force * Main.rand.NextFloat(0.7f, 1.3f);

                Drop(at, way.RotatedBy(angle) * speed, Main.rand.NextFloat(0.5f, 0.95f));
            }

            int spill = (int)(Abomanation.SpillDroplets * force);

            for (int i = 0; i < spill; i++)
            {
                float side = Main.rand.NextFloat(-1f, 1f);

                float angle = Main.rand.NextFloat(-Abomanation.SpillCone, Abomanation.SpillCone);
                float speed = Abomanation.SpillSpeed * force * Main.rand.NextFloat(0.5f, 1.4f);

                Drop(at + across * side * Abomanation.SpillOffset,
                     way.RotatedBy(angle) * speed,
                     Main.rand.NextFloat(0.4f, 0.8f));
            }

            for (int i = 0; i < Abomanation.BackSpatter; i++)
            {
                float angle = Main.rand.NextFloat(-Abomanation.SpillCone, Abomanation.SpillCone);
                float speed = Abomanation.BackSpatterSpeed * force * Main.rand.NextFloat(0.5f, 1.2f);

                Drop(at, (-way).RotatedBy(angle) * speed, Main.rand.NextFloat(0.35f, 0.6f));
            }
        }

        /// <summary>A body segment or the tail: the parts that wear the outline.</summary>
        private static bool Part(NPC npc) => npc.ModNPC is AbomanationBody || npc.ModNPC is AbomanationTail;

        /// <summary>
        /// Vanilla's own cull: an NPC more than 800 pixels off screen is not drawn at
        /// all, so its PreDraw never runs. Mirrors Main.DrawNPCs exactly.
        /// </summary>
        private static bool Drawn(NPC npc)
        {
            Rectangle view = new Rectangle((int)Main.screenPosition.X - 800, (int)Main.screenPosition.Y - 800,
                Main.screenWidth + 1600, Main.screenHeight + 1600);

            return view.Intersects(new Rectangle((int)npc.position.X, (int)npc.position.Y, npc.width, npc.height));
        }

        /// <summary>
        /// Whether this part is the one the draw loop reaches first this frame, among
        /// the animal's parts that will be drawn at all. Vanilla draws NPCs from slot
        /// 199 downward, so it is the highest slot that is on screen. The whole outline
        /// is drawn from that part, before any part's sprite, which is what makes it one
        /// outline round the animal rather than a ring round each piece.
        /// </summary>
        public static bool DrawsFirst(NPC npc)
        {
            int head = Head(npc);

            for (int i = Main.maxNPCs - 1; i > npc.whoAmI; i--)
            {
                NPC other = Main.npc[i];

                if (other.active && !other.hide && Part(other) && Head(other) == head && Drawn(other))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The outline of the whole animal in one pass: every part, legs, flesh and
        /// growths, in the warning colour with no alpha, eight times round a ring of
        /// OutlineReach pixels. Every sprite of every part is drawn over it afterwards
        /// by the ordinary draw, so only what sticks out past the union of them shows:
        /// one continuous edge round the body, the arms and the tail, and nothing
        /// between the segments.
        /// </summary>
        public static void Silhouette(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            int head = Head(npc);

            Color glow = CCBypass.Warning * CCBypass.OutlineGlow;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * CCBypass.OutlineReach;

                // Moving the screen the other way moves every part by the offset.
                Vector2 shifted = screenPos - offset;

                for (int j = 0; j < Main.maxNPCs; j++)
                {
                    NPC other = Main.npc[j];

                    if (!other.active || other.alpha > 0 || !Part(other) || Head(other) != head)
                        continue;

                    if (other.ModNPC is AbomanationBody body)
                        body.DrawWhole(spriteBatch, shifted, glow);
                    else if (other.ModNPC is AbomanationTail tail)
                        tail.DrawWhole(spriteBatch, shifted, glow);
                }
            }
        }

        // localAI is per NPC, unsynced, and free: the three slots the swing keeps.
        private const int LashAngle = 0;
        private const int LashSpin = 1;
        private const int LashSerial = 2;

        /// <summary>
        /// One link of the whip, for one tick. Replaces Follow while the head cracks.
        ///
        /// The link is the line from the segment ahead to this one, and what is kept is
        /// its ANGLE and how fast that angle is turning. Each tick the angle is pulled
        /// toward the link ahead's angle — a spring, stiffer the further down the body —
        /// the turning speed is damped, capped, and applied, and the segment is put at
        /// the end of the link. The length never changes, so the body cannot stretch,
        /// and the segment ahead has already moved this tick, so the pass runs down the
        /// chain in one go, head to tail, the way the engine already orders it.
        ///
        /// That is the whole whip. The head loops, the angles wind up round the coil;
        /// the head runs, the first link straightens, then the next, each faster than the
        /// last because each is stiffer, and the tail comes off the end of it in one arc.
        /// Nothing tells the tail where to go.
        ///
        /// Written to velocity, not position: the engine moves the segment, so
        /// oldPosition is the true last position and the swept hit test below is honest.
        ///
        /// The first tick of a crack reads the angle off where the body actually is, so
        /// the swing starts from the coil the loop made and nothing jumps.
        /// </summary>
        public static void Swing(NPC npc, NPC leader, float spacing)
        {
            AbomanationHead boss = Boss(npc);

            if (boss is null)
                return;

            int place = (int)npc.ai[3];

            // The angle of the link ahead: the head's own line for the first segment,
            // otherwise whatever the segment ahead just settled on.
            float ahead = place == 0
                ? boss.RunHeading + MathHelper.Pi
                : leader.localAI[LashAngle];

            if ((int)npc.localAI[LashSerial] != boss.WhipSerial)
            {
                npc.localAI[LashAngle] = (npc.Center - leader.Center).ToRotation();
                npc.localAI[LashSpin] = 0f;
                npc.localAI[LashSerial] = boss.WhipSerial;
            }

            float angle = npc.localAI[LashAngle];
            float spin = npc.localAI[LashSpin];

            float spring = MathHelper.Lerp(Abomanation.WhipSpringHead, Abomanation.WhipSpringTail,
                place / (float)Abomanation.BodySegments);

            spin += spring * MathHelper.WrapAngle(ahead - angle);
            spin *= boss.LashDamping;
            spin = MathHelper.Clamp(spin, -Abomanation.WhipMaxTurn, Abomanation.WhipMaxTurn);
            angle += spin;

            npc.localAI[LashAngle] = angle;
            npc.localAI[LashSpin] = spin;

            Vector2 at = leader.Center + angle.ToRotationVector2() * spacing;

            npc.velocity = at - npc.Center;

            // Pointing at the one ahead of it, as always.
            npc.rotation = angle + MathHelper.Pi + MathHelper.PiOver2;
        }

        /// <summary>
        /// Contact for a segment mid-crack.
        ///
        /// A point test is no good here. The tail crosses sixty pixels in a tick at the
        /// top of the arc and the player is twenty wide, so a still player would be
        /// missed about half the time — the lash would pass through them between two
        /// frames. This tests the LINE the segment travelled along this tick instead, as
        /// a thick line against the player's box, so the whole swept path counts and
        /// nothing tunnels.
        /// </summary>
        public static bool Sweeping(NPC npc, Player target, float radius)
        {
            if (npc.alpha > 0 || Harmless(npc))
                return false;

            float where = 0f;
            Vector2 was = npc.oldPosition + npc.Size * 0.5f;

            return Collision.CheckAABBvLineCollision(
                target.position, target.Size, was, npc.Center, radius * 2f * npc.scale, ref where);
        }

        /// <summary>
        /// What being hit by the lash does.
        ///
        /// Not knockback. Knockback is a push with a direction and a number, and every
        /// shield in the game turns it off; this is the end of something heavy moving
        /// at speed, and what it does to you is written straight onto your velocity. A
        /// Cobalt or Ankh Shield makes no difference, which is the point.
        ///
        /// The direction is the unit tangent of the segment's own path this tick, and
        /// the speed is its real speed along it times WhipToss times WhipTossBoost, so
        /// the tail at full swing throws you most of a screen and the neck barely moves
        /// you. No cap on top: full strength is full strength.
        ///
        /// What happens to the body after that — the tumble, the landing, the time on
        /// the ground — is RagdollPlayer's, and it is handed the velocity, the style and
        /// how long you stay down.
        /// </summary>
        public static void Throw(NPC npc, Player target)
        {
            // Player velocity belongs to the client that owns the player. On a server
            // this would be overwritten by the owner's next update and read as a stutter.
            if (target.whoAmI != Main.myPlayer)
                return;

            AbomanationHead boss = Boss(npc);

            if (boss is null || !boss.Cracking)
                return;

            Vector2 motion = npc.Center - (npc.oldPosition + npc.Size * 0.5f);
            float speed = motion.Length();

            if (speed < 0.5f)
                return;

            // The unit tangent of the segment's path, times how fast it was going along
            // it, times the intensity. Nothing else: the direction IS the swing.
            Vector2 tangent = motion / speed;

            Vector2 throwVelocity = tangent
                * Math.Max(speed * Abomanation.WhipToss * Abomanation.WhipTossBoost, Abomanation.WhipTossMin);

            // Only a floor on the lift when one is asked for. At zero this must not
            // touch the throw, or a downward tangent loses its downward half and the
            // player is shoved along the ground instead of into it.
            if (Abomanation.WhipTossLift > 0f && throwVelocity.Y > -Abomanation.WhipTossLift)
                throwVelocity.Y = -Abomanation.WhipTossLift;

            // A high throw tumbles; a low one, from the near end of the arc, rolls.
            RagdollStyle style = throwVelocity.Y < -Abomanation.WhipSpinLift
                ? RagdollStyle.Spin
                : RagdollStyle.Roll;

            target.GetModPlayer<RagdollPlayer>().Throw(throwVelocity, style, Abomanation.WhipLie);
        }

        /// <summary>
        /// Keeps a segment's health, damage immunity and fade in step with the head, and
        /// takes it off the board when the head is gone.
        ///
        /// The leader is checked by type as well as by index, because an NPC slot is
        /// reused the moment its occupant dies. A segment holding a stale index would
        /// otherwise start following whatever bat spawned into that slot next.
        /// </summary>
        /// <returns>False when the segment has just been removed and its AI should stop.</returns>
        public static bool Tether(NPC npc, int headType, int bodyType)
        {
            int head = Head(npc);
            int ahead = Ahead(npc);

            if (head >= 0)
                npc.realLife = head;

            bool gone = !NPC.AnyNPCs(headType) || ahead < 0 || ahead >= Main.maxNPCs;

            if (!gone)
            {
                NPC check = Main.npc[ahead];

                // A leader at no life is a dead leader, except during the end, when the
                // whole animal is held at one and a part can be a tick behind: vanilla
                // writes the head's life onto whichever segment took the killing blow
                // BEFORE the head's CheckDead pins it back to one, so that segment sits
                // at zero or below until the sweep in CheckDead reaches it. Reading that
                // as death here took the entire body off the board on the first frame.
                bool dead = check.life <= 0 && !Dying(npc);

                gone = !check.active || dead || (check.type != headType && check.type != bodyType);
            }

            if (gone)
            {
                npc.life = 0;
                npc.HitEffect(0, 10.0);
                npc.checkDead();
                npc.active = false;
                return false;
            }

            NPC leader = Main.npc[ahead];

            if (npc.life > leader.life)
                npc.life = leader.life;

            npc.dontTakeDamage = leader.dontTakeDamage;
            npc.canDisplayBuffs = leader.canDisplayBuffs;

            // The whole body surfaces together. Following the leader's alpha rather than
            // the head's means the fade runs down the body a segment at a time.
            if (leader.alpha < 128)
            {
                npc.alpha -= 42;
                if (npc.alpha < 0)
                    npc.alpha = 0;
            }
            else
            {
                npc.alpha = leader.alpha;
            }

            return true;
        }

        /// <summary>
        /// The narrow contact test every segment shares. The hitbox has to be wide enough
        /// to be shot at; this is what decides whether it actually touched you, and it is
        /// a circle because the sprite is one.
        /// </summary>
        public static bool Touching(NPC npc, Player target, float radius)
        {
            if (npc.alpha > 0)
                return false;

            // The arrival touches nothing. Not dontTakeDamage: the whip is untouchable
            // too, and the whip is supposed to hurt.
            if (Harmless(npc))
                return false;

            Rectangle box = target.Hitbox;

            float nearest = Vector2.Distance(npc.Center, box.TopLeft());
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.TopRight()));
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.BottomLeft()));
            nearest = Math.Min(nearest, Vector2.Distance(npc.Center, box.BottomRight()));

            return nearest <= radius * npc.scale;
        }
    }
}
