using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// Drawing him, and everything that is only seen or heard: how he holds himself this
    /// tick, what his blade leaves behind it, what he leaves behind him when he is gone from
    /// one place to another, and the sparks, dust and sounds of each move.
    ///
    /// He is seven pictures (FoighRig), each turned about its own joint; from his second
    /// phase the sword is another picture, its sharp side, and from the passage into his
    /// third a third, bloodied (and it burns: Foigh.Third). Seen turning, all
    /// of him is drawn narrower across, about the upright line through his feet, and his
    /// sword shorter along its own length as it points at the eye or away: a sprite batch
    /// cannot do either to one picture, so those are drawn through a view of their own
    /// (<see cref="Through"/>), and only those, only then.
    /// </summary>
    public partial class Foigh : ICCBypassOutline
    {
        private static readonly Asset<Texture2D>[] pieces = new Asset<Texture2D>[FoighRig.Pieces];
        private static Asset<Texture2D> sharp;
        private static Asset<Texture2D> bloody;
        private static Asset<Texture2D> stone;
        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> softLine;

        /// <summary>Steel, and the pale edge of it; his eye.</summary>
        private static readonly Color Steel = new Color(214, 226, 240);
        private static readonly Color Dusk = new Color(52, 44, 60);
        private static readonly Color Eye = new Color(255, 70, 92);
        private static readonly Color[] Sparks = { new Color(255, 255, 255), new Color(214, 226, 240), new Color(255, 214, 160) };

        /// <summary>The same steel, dusk and sparks, for what is drawn of him out of his own drawing (FoighLine's white cuts).</summary>
        internal static Color SteelColour => Steel;
        internal static Color DuskColour => Dusk;
        internal static Color[] SparkColours => Sparks;

        /// <summary>Ticks a pose takes to come out of the one before it.</summary>
        private const int BlendTicks = 5;

        /// <summary>What the blade leaves behind it: how many ticks of its way, and in how many steps.</summary>
        private const float TrailTicks = 5.5f;
        private const int TrailSteps = 18;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < FoighRig.Pieces; i++)
                pieces[i] = ModContent.Request<Texture2D>(FoighRig.Folder + FoighRig.Names[i]);

            sharp = ModContent.Request<Texture2D>(FoighRig.Folder + FoighRig.SharpSword);
            bloody = ModContent.Request<Texture2D>(FoighRig.Folder + FoighRig.BloodSword);
            stone = ModContent.Request<Texture2D>(FoighRig.Folder + FoighRig.Whetstone);

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");

            LoadHook();
        }

        public override void Unload()
        {
            UnloadHook();

            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = null;

            sharp = null;
            bloody = null;
            stone = null;
            smear = null;
            softLine = null;
        }

        /// <summary>The sword's picture, its blunt side, its sharp one, or bloodied: for what draws it out of his hand. Null until it is loaded.</summary>
        internal static Texture2D SwordPicture(bool sharpSide, bool bloodied = false)
        {
            Asset<Texture2D> asset = bloodied && bloody != null && bloody.IsLoaded ? bloody
                : sharpSide && sharp != null && sharp.IsLoaded ? sharp
                : pieces[FoighRig.Sword];

            return asset != null && asset.IsLoaded ? asset.Value : null;
        }

        // ============================================================
        // HOW HE IS SHOWN THIS TICK
        // ============================================================

        /// <summary>How he holds himself this tick, as he is drawn, and where that puts his joints and his sword.</summary>
        private FoighRig.Pose pose = M.Await;
        private FoighRig.Frame frame;

        /// <summary>The pose he was in when he went into this move, and the ticks left of coming out of it.</summary>
        private FoighRig.Pose blendFrom;
        private int blend;

        /// <summary>Ticks left of turning about, and the way he faced last tick.</summary>
        private int about;
        private int facedWas;

        /// <summary>How much of his walk is in his stance: 0 standing, 1 walking.</summary>
        private float stride;

        /// <summary>How strong the blade's trail is, 0 to 1: up while it cuts, and dying after.</summary>
        private float trail;

        /// <summary>His posture as a bar of it would show it, 0 to 1: eased after what it is; all of it while he is broken.</summary>
        private float postureShown;

        /// <summary>Where his feet have been, tick by tick, newest first: what the trail is laid along (burning, it reaches a little over eight ticks back).</summary>
        private readonly Vector2[] feetLog = new Vector2[10];
        private int logged;

        /// <summary>One of him left behind: how he stood, where, which way, and how much of it is left.</summary>
        private struct Ghost
        {
            public FoighRig.Pose Pose;
            public Vector2 Feet;
            public int Facing;
            public float Left;
        }

        private readonly List<Ghost> ghosts = new List<Ghost>();

        /// <summary>
        /// The first tick this machine sees of a move: what picks up where another left off
        /// is built, the pose he was in is kept to come out of, and what the change itself
        /// looks and sounds like happens.
        /// </summary>
        private void Entered(Move from)
        {
            struck = 0;
            struckCopy = 0;
            since = 0;
            guardsSeen = guards;

            switch (State)
            {
                case Move.TwoFree:
                    free = M.Free(Arg);
                    break;

                case Move.TwoLanded:
                    landed = M.Landed(Arg);
                    break;

                case Move.Reel:
                    reel = M.Reel(pose);
                    break;

                case Move.Spent:
                    spent = M.Spent(pose);
                    PassageBegins();
                    break;

                // His second phase's own. Caught, and it is this machine's player (or the
                // Everclear Mirror's copy, carried here as they are): they are pulled in to
                // his hand from where they are now.
                case Move.Caught:
                    snatched = M.SnatchTicks;

                    if (!Main.dedServ && held >= 0 && held < Main.maxPlayers && (held == Main.myPlayer || MimicSystem.Is(Main.player[held])))
                    {
                        snatchedFrom = Main.player[held].Center;
                        snatched = 0;
                    }
                    break;

                // Each of these from the pose he is seen in as it begins.
                case Move.Pierce:
                    pierce = M.Pierce(pose);
                    hoist = null;

                    if (from != Move.Caught)
                        snatched = M.SnatchTicks;
                    break;

                case Move.Hoist:
                    hoist = M.Hoist(pose);
                    break;

                case Move.Fling:
                    flung = from == Move.Hoist || from == Move.Pierce || from == Move.Caught ? M.FlingOff(pose) : null;
                    break;

                case Move.Miss:
                    empty = from == Move.Spring || from == Move.Caught ? M.Empty(pose) : null;
                    break;

                // The cut up, from the pose he ran in; and nothing of the ground torn yet.
                case Move.Upcut:
                    upcut = M.Upcut(pose);
                    furrow = -1;
                    break;

                // Down out of the air, from the pose he fell in.
                case Move.Alight:
                    alight = M.Alight(pose);
                    break;

                // Out of the passage into his third phase: the sword out of the ground and
                // behind him again, from the pose the cut left him in. (Out of the other
                // passage he is in his stance already.)
                case Move.Resume:
                    leaving = from == Move.Sever ? M.Leaving(pose, FadeOutTicks) : null;
                    break;

                // His end: rocked by the blow, from the pose he was in (as a passage begins).
                case Move.Dying:
                    spent = M.Spent(pose);
                    break;
            }

            // His end (Foigh.End): nothing of it shown or asked yet on this machine, from the
            // first part of it this machine sees; and nothing of each part, as it begins.
            if (Ending)
            {
                if (!OfEnd(from))
                    EndBegins();

                EndPart();
            }

            // One his cut up threw into the air is his to strike down only while he is on
            // his way to do it: the cut, the step up, the moment beside them, the blow (in
            // the air, or the one that ends on the ground).
            if (State != Move.Upcut && State != Move.Flash && State != Move.Hang && State != Move.Smite && State != Move.MillEnd)
            {
                liftedMe = 0;
                liftedCopy = 0;
            }

            // Not out of a flash step, which is a cut; not on the road, where every pose is
            // already the last one's end; not onto his knee or over his sword, which are
            // under the black; and never the first he is seen in.
            bool cutTo = (int)from < 0 || from == Move.Flash || State == Move.Flash || State <= Move.Begin || from <= Move.Begin
                || State == Move.Knee || State == Move.Shudder;

            blendFrom = pose;
            blend = cutTo ? 0 : BlendTicks;

            if (!Main.dedServ)
                EnteredLooks(from);
        }

        /// <summary>Every tick, every machine: the pose for this tick of the move he is in, and where it puts him.</summary>
        private void Show()
        {
            float speed = Math.Abs(NPC.velocity.X);

            if (State == Move.Walk)
                gait += speed / M.Stride;
            else if (State == Move.FourRun || State == Move.Rush || State == Move.Scrape || State == Move.Charge)
                gait += speed / M.RunStride;

            stride = MathHelper.Lerp(stride, State == Move.Walk && speed > 0.2f ? 1f : 0f, 0.14f);
            postureShown = MathHelper.Lerp(postureShown, !Fighting || Passing ? 0f : State == Move.Broken ? 1f : Posture, 0.25f);

            Aim();

            if (State == Move.Guard)
            {
                if (guards != guardsSeen)
                {
                    guardsSeen = guards;
                    since = 0;
                }
                else
                {
                    since++;
                }
            }

            FoighRig.Pose shown = PoseOf(State, clock);

            if (!hasSword && State != Move.Draw)
                shown.Hand = 0;

            // Turned to face the other way while he walks or stands: the whole of him comes round.
            if (facing != facedWas)
            {
                if (facedWas != 0 && TurnsAbout)
                    about = M.AboutTicks;

                facedWas = facing;
            }

            if (about > 0)
            {
                shown = M.About(shown, 1f - about / (float)M.AboutTicks);
                about--;
            }

            if (blend > 0)
            {
                // Only between two ways of holding the same thing in the same hand.
                if (blendFrom.Hand == shown.Hand && blendFrom.Air == shown.Air)
                    shown = Blended(blendFrom, shown, 1f - blend / (float)(BlendTicks + 1));

                blend--;
            }

            if (LaysBlade(shown))
                FoighRig.Ground(ref shown);

            pose = shown;
            frame = FoighRig.Build(pose, Feet, facing);

            // His end: where his hand lets the sword go (Foigh.End).
            if (Ending)
                EndShow();

            // The trail: up at once while the blade cuts (or is swung in the passage into
            // his third phase), gone a few ticks after; burning, it hangs longer.
            trail = hasSword && (Cuts(clock, out _, out _) || Sweeps) ? 1f : Math.Max(0f, trail - (0.22f - 0.1f * flame));

            ShowBlade();

            if (!Main.dedServ)
                ShowLine();
        }

        // ---- the late cut's line ----

        /// <summary>The late cut's line as it is shown this tick: its two ends, and how strongly (not at all, at nothing).</summary>
        private Vector2 lineFrom;
        private Vector2 lineTo;
        private float lineShown;

        /// <summary>The two ends of the line he has taken, as it lies: from where he stood to a little past where he stands (where his sword is), a little over the ground.</summary>
        private Vector2 LineFrom => stepFrom + new Vector2(NPC.width * 0.5f, NPC.height - LateHigh);

        private Vector2 LineTo => stepTo + new Vector2(NPC.width * 0.5f + facing * LatePast, NPC.height - LateHigh);

        /// <summary>
        /// Every tick, on a machine with a screen: the line of the late cut. Sunk low, it
        /// is where the cut would go as they stand now, and faint; gone along it and
        /// standing past them, it is where he went, and brighter by the tick until it
        /// opens.
        /// </summary>
        private void ShowLine()
        {
            lineShown = 0f;

            switch (State)
            {
                case Move.Poise:
                {
                    Player target = Target();

                    if (now < M.PoiseGleams || target == null || !LateLine(target, out Vector2 feet))
                        break;

                    lineFrom = Feet + new Vector2(0f, -LateHigh);
                    lineTo = feet + new Vector2(facing * LatePast, -LateHigh);
                    lineShown = 0.5f * MathHelper.Clamp((clock - M.PoiseGleams) / 10f, 0f, 1f);
                    break;
                }

                case Move.Flash when then == Move.Wake:
                    lineFrom = LineFrom;
                    lineTo = LineTo;
                    lineShown = 0.6f;
                    break;

                case Move.Wake when clock < M.WakeCuts:
                    lineFrom = LineFrom;
                    lineTo = LineTo;
                    lineShown = 0.6f + 0.35f * clock / M.WakeCuts;
                    break;
            }
        }

        /// <summary>Between the pose he was in and the one he is going into, the short way round for whatever turns.</summary>
        private static FoighRig.Pose Blended(FoighRig.Pose from, FoighRig.Pose to, float t)
        {
            from.Sword = FoighTrack.Nearest(from.Sword, to.Sword);
            from.NearArm = FoighTrack.Nearest(from.NearArm, to.NearArm);
            from.FarArm = FoighTrack.Nearest(from.FarArm, to.FarArm);

            // A turn is not come out of by degrees: he would be seen spinning.
            from.Turn = to.Turn;
            from.HeadTurn = to.HeadTurn;
            from.Reach = to.Reach;
            from.Slide = to.Slide;

            return FoighTrack.Blend(from, to, Easing.Smoother(t));
        }

        /// <summary>Where the sword points in the air, eased: along the way he falls in the leap, at them in the throw.</summary>
        private void Aim()
        {
            if (State == Move.TwoAir)
            {
                float wanted = (float)Math.Atan2(Math.Max(NPC.velocity.Y, 2f), Math.Abs(NPC.velocity.X) + 0.5f);

                wanted = MathHelper.Clamp(wanted, M.AimShallowest, M.AimSteepest);
                aimShown = fresh ? wanted : MathHelper.Lerp(aimShown, wanted, 0.2f);
            }
            else if (State == Move.FourAir && hasSword)
            {
                Player target = Target();

                if (target == null)
                    return;

                Vector2 d = target.Center - (Feet + new Vector2(0f, -40f));
                float wanted = MathHelper.Clamp((float)Math.Atan2(d.Y, Math.Abs(d.X)), 0.2f, 1.4f);

                aimShown = fresh ? wanted : MathHelper.Lerp(aimShown, wanted, 0.2f);
            }
            else if (State == Move.TwoStuck || State == Move.TwoFree || State == Move.TwoLanded)
            {
                aimShown = Arg;
            }
        }

        /// <summary>
        /// Whether a change of the way he faces is seen as him turning about: on his feet,
        /// not in the middle of a blow. (Between the three cuts of his third phase's own
        /// move he looks for them again with the sword still: there too.)
        /// </summary>
        private bool TurnsAbout => State == Move.Walk || State == Move.Guard || State == Move.Knives || State == Move.Leave || State == Move.Rend || State == Move.Gale;

        /// <summary>
        /// Whether the blade is kept out of the ground in this pose: on his feet on level
        /// ground, in anything but the moves that put it there on purpose.
        /// </summary>
        private bool LaysBlade(in FoighRig.Pose of)
        {
            // (Down out of the mill's last blow he is on the floor a tick before his speed
            // says so: the game stops a fall by cutting the speed to what was left of the
            // way, not to nothing, and marks that it did: NPC.Collision_MoveWhileDry. That
            // tick is the one the sword is down in.)
            bool down = grounded || (State == Move.MillEnd && NPC.collideY && NPC.oldVelocity.Y > 0f);

            if (of.Air || of.Hand == 0 || !down)
                return false;

            switch (State)
            {
                case Move.TwoStuck:
                case Move.TwoAir:
                case Move.FourAir:
                case Move.FourFall:
                case Move.Flash:
                    return false;

                case Move.TwoFree:
                    return now > M.FreeRips + 3;

                case Move.Draw:
                    return now >= M.DrawRips + 6;

                case Move.Knee:
                    return false;

                case Move.Whet:
                    return now >= M.WhetOut;

                // The cut up goes under him: into the ground behind, forward through it,
                // and out of it ahead of him. Only after that is it kept out of it.
                case Move.Upcut:
                    return now > M.UpcutCuts + 1;

                // The passage into his third phase: in the ground while he holds on to it
                // and until it is torn out; and into it again where the cut behind them
                // ends (he lets go of it there).
                case Move.Shudder:
                    return false;

                case Move.Gut:
                    return now >= M.GutOut;

                case Move.Sever:
                    return now < M.SeverUp;

                default:
                    return true;
            }
        }

        /// <summary>The blade is in the ground: the tiles are drawn over what is in it.</summary>
        private bool BladeInGround
        {
            get
            {
                switch (State)
                {
                    case Move.Await:
                    case Move.Turn:
                    case Move.Talk:
                    case Move.TwoStuck:
                        return true;

                    case Move.Draw:
                        return Timer < M.DrawRips + 8;

                    case Move.TwoFree:
                        return Timer <= M.FreeRips + 3;

                    case Move.Knee:
                        return true;

                    case Move.Whet:
                        return Timer < M.WhetOut;

                    case Move.Upcut:
                        return Timer <= M.UpcutCuts + 1;

                    case Move.Shudder:
                        return true;

                    case Move.Gut:
                        return Timer < M.GutOut;

                    case Move.Sever:
                        return Timer >= M.SeverCuts - 1;

                    // The sword stands where he left it until the picture has gone.
                    case Move.Resume:
                        return leaving != null && Timer <= FadeOutTicks + 1;

                    default:
                        return false;
                }
            }
        }

        /// <summary>The end of a tick: where his feet were, for the next one.</summary>
        private void Remember()
        {
            for (int i = feetLog.Length - 1; i > 0; i--)
                feetLog[i] = feetLog[i - 1];

            feetLog[0] = Feet;
            logged = Math.Min(logged + 1, feetLog.Length);

            feetWas = Feet;
            groundedWas = grounded;

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];

                ghost.Left -= 0.1f;

                if (ghost.Left <= 0f)
                    ghosts.RemoveAt(i);
                else
                    ghosts[i] = ghost;
            }
        }

        /// <summary>Where his feet were this many ticks ago (between two, the place between).</summary>
        private Vector2 FeetAgo(float ticks)
        {
            if (logged == 0)
                return Feet;

            float at = MathHelper.Clamp(ticks, 0f, logged - 1);
            int i = (int)at;
            int j = Math.Min(i + 1, logged - 1);

            return Vector2.Lerp(feetLog[i], feetLog[j], at - i);
        }

        private void LeaveGhost(float strength = 1f)
        {
            if (Main.dedServ || ghosts.Count >= 12)
                return;

            ghosts.Add(new Ghost { Pose = pose, Feet = Feet, Facing = facing, Left = strength });
        }

        // ============================================================
        // WHAT EACH MOVE LOOKS AND SOUNDS LIKE
        // ============================================================

        private bool Near(float within = 1100f) => Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < within;

        private void Shake(float power, int ticks)
        {
            if (Near())
                Main.LocalPlayer.PIS().BumpScreenShake(power, ticks);
        }

        private static void Sound(in SoundStyle style, Vector2 at, float pitch = 0f, float volume = 1f)
        {
            SoundEngine.PlaySound(style with { Pitch = pitch, Volume = volume, PitchVariance = 0.08f }, at);
        }

        /// <summary>A light off something: his eye before a blow, the point before a thrust.</summary>
        private static void Glint(Vector2 at, Color colour, float size, int ticks = 14)
        {
            PADParticleHandler.SpawnParticle(new CrownGlint(at, size, ticks, colour));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, colour * 0.5f, size / 110f, ticks));
        }

        /// <summary>Dust off the ground under something, thrown both ways.</summary>
        private static void Puff(Vector2 at, int count, float push, float rise = 1.4f)
        {
            Color ground = GroundWave.FogColour((int)(at.X / 16f), (int)((at.Y + 4f) / 16f));

            for (int i = 0; i < count; i++)
            {
                float side = Main.rand.NextBool() ? -1f : 1f;

                PADParticleHandler.SpawnParticle(new DustFog(at + new Vector2(Main.rand.NextFloat(-8f, 8f), -Main.rand.NextFloat(0f, 6f)),
                    new Vector2(side * Main.rand.NextFloat(0.4f, push), -Main.rand.NextFloat(0.1f, rise)), ground,
                    Main.rand.Next(50, 90), Main.rand.NextFloat(0.4f, 0.8f), Main.rand.NextFloat(0.22f, 0.36f)));
            }
        }

        /// <summary>The sword into the ground: steel on stone, sparks, the ground's own dust, and for a hard one the ground heaving.</summary>
        private void Impact(Vector2 at, float hard)
        {
            Sound(SoundID.DD2_MonkStaffGroundImpact, at, -0.2f - 0.2f * hard, 0.7f + 0.2f * hard);
            Sound(SoundID.Dig, at, -0.5f, 0.8f);

            SparkShard.Spray(at + new Vector2(0f, -4f), Steel, (int)(7 + 6 * hard), 5f + 3f * hard, 22f + 8f * hard, 16, falls: true, palette: Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Steel * 0.5f, 0.3f + 0.15f * hard, 10));

            Puff(at, (int)(8 + 8 * hard), 2.5f + 2f * hard);

            if (hard >= 1.5f)
                GroundWave.Slam(at, 12f, 8, 9f, 30);
            else
                GroundWave.Slam(at, 8f, 3, 4f, 20);

            // Burning, it leaves more than dust where it lands.
            if (Burning)
                Scorch(at, hard);

            Shake(4f + 3.5f * hard, 10 + (int)(6 * hard));
        }

        /// <summary>Air along the blade: a thrust, a throw.</summary>
        private void Wind(Vector2 from, Vector2 to, int count)
        {
            Vector2 along = (to - from).SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < count; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, Main.rand.NextFloat()) + new Vector2(-along.Y, along.X) * Main.rand.NextFloat(-10f, 10f);

                PADParticleHandler.SpawnParticle(new WindStreak(at, along * Main.rand.NextFloat(3f, 8f), Main.rand.NextFloat(26f, 54f), 5f, Main.rand.Next(8, 14)));
            }
        }

        /// <summary>The whole of him round with the sword out: the air it pushes.</summary>
        private void Whirl()
        {
            Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.25f, 0.9f);
            PADParticleHandler.SpawnParticle(new ParryRing(NPC.Center + new Vector2(0f, -8f), 24f, 118f, 14, Steel * 0.35f, NPC));
        }

        /// <summary>Ticks before steel is heard again for something turned aside in a stream.</summary>
        private int wardSound;

        /// <summary>
        /// One thing of many turned aside (Foigh.Hold): a few shards off the place; and,
        /// no oftener than it can be told apart, steel heard and a glint. (A shot struck
        /// out of the air by itself has the parry's own spark.)
        /// </summary>
        private void Ward(Vector2 at)
        {
            if (Main.dedServ)
                return;

            SparkShard.Spray(at, Steel, 3, 5f, 20f, 12, falls: true, palette: Sparks);

            if (wardSound > 0)
                return;

            wardSound = 7;

            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.5f, PitchVariance = 0.2f, Volume = 0.6f }, at);
            PADParticleHandler.SpawnParticle(new CrownGlint(at, 32f, 10, Steel));
        }

        /// <summary>What a change of move looks and sounds like, the tick this machine sees it.</summary>
        private void EnteredLooks(Move from)
        {
            Vector2 feet = Feet;

            // Out of a flash step: he is there.
            if (from == Move.Flash)
            {
                Sound(SoundID.Item1, NPC.Center, -0.6f, 0.9f);
                Puff(feet, 7, 3.2f, 0.8f);
                PADParticleHandler.SpawnParticle(new ParryRing(NPC.Center, 14f, 70f, 10, Steel * 0.3f, NPC));
                Glint(frame.EyeAt, Eye, 22f, 10);
            }

            switch (State)
            {
                case Move.Flash:
                    Sound(SoundID.DoubleJump, NPC.Center, -0.5f, 0.9f);
                    Puff(feet, 6, 2.6f, 0.8f);
                    Glint(frame.EyeAt, Eye, 26f, 10);
                    break;

                case Move.TwoStuck:
                    Impact(new Vector2(frame.Tip.X, feet.Y), 1.6f);
                    break;

                case Move.TwoLanded:
                    Impact(new Vector2(MathHelper.Lerp(feet.X, frame.Tip.X, 0.6f), feet.Y), 1f);
                    break;

                case Move.FourTouch:
                    Sound(SoundID.Dig, feet, -0.7f, 0.8f);
                    Puff(feet, 8, 2.6f);
                    Shake(3f, 8);
                    break;

                case Move.FourRound:
                    Sound(SoundID.Item71, NPC.Center, -0.3f, 0.9f);
                    break;

                case Move.Guard:
                    Sound(SoundID.Item1, NPC.Center, 0.2f, 0.5f);
                    break;

                case Move.Lift:
                    Sound(SoundID.Item1, frame.FarFist, -0.5f, 0.8f);
                    PADParticleHandler.SpawnParticle(new ParryRing(frame.FarFist, 6f, 40f, 9, CCBypass.Warning * 0.4f));
                    break;

                case Move.Spitted:
                    Sound(SoundID.DD2_GhastlyGlaivePierce, frame.Tip, -0.2f, 1f);
                    Sound(SoundID.NPCHit1, frame.Tip, -0.4f, 0.9f);
                    BloodDroplet.SpawnBurst(Vector2.Lerp(frame.Guard, frame.Tip, 0.6f), new Vector2(-facing * 0.3f, -1f), 16, 50f);
                    Impale();
                    break;

                // ---- his second phase's own ----

                case Move.Raise:
                    Sound(SoundID.Item71, NPC.Center, -0.45f, 0.8f);
                    Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.Rush:
                    Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);
                    Puff(feet, 6, 2.8f, 0.8f);
                    break;

                case Move.Crouch:
                    Sound(SoundID.Dig, feet, -0.8f, 0.6f);
                    Puff(feet, 4, 1.6f, 0.6f);
                    break;

                case Move.Spring:
                    Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.1f, 0.9f);
                    Puff(feet, 9, 3f);
                    break;

                case Move.Caught:
                    Sound(SoundID.Item1, frame.FarFist, -0.5f, 0.9f);
                    PADParticleHandler.SpawnParticle(new ParryRing(frame.FarFist, 6f, 40f, 9, CCBypass.Warning * 0.4f));

                    if (held == Main.myPlayer)
                        Main.LocalPlayer.PIS().BumpScreenShake(4f, 8);
                    break;

                case Move.Pierce:
                    // Down, with them.
                    Sound(SoundID.Dig, feet, -0.7f, 0.8f);
                    Puff(feet, 8, 2.4f);
                    Shake(3f, 8);
                    break;

                case Move.Hoist:
                    Sound(SoundID.Item71, NPC.Center, -0.6f, 0.6f);
                    break;

                case Move.Miss when from == Move.Spring:
                    // Down, with nobody.
                    Sound(SoundID.Dig, feet, -0.6f, 0.9f);
                    Puff(feet, 9, 3f);
                    Shake(3f, 8);
                    break;

                // ---- and again: the late cut, the mill, the cut up, in the air ----

                case Move.Poise:
                    Sound(SoundID.Item1, NPC.Center, -0.8f, 0.6f);
                    Puff(feet, 4, 1.6f, 0.6f);
                    break;

                case Move.Wake:
                    // Past them: the air he went through, all the way along.
                    Wind(LineFrom, LineTo, 14);
                    Sound(SoundID.Item71, NPC.Center, 0.4f, 0.45f);
                    break;

                case Move.Level:
                    Sound(SoundID.Item71, NPC.Center, -0.5f, 0.7f);
                    Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.Mill:
                    Puff(feet, 5, 2.4f, 0.8f);
                    break;

                case Move.MillEnd when Arg <= 0.5f:
                    // Up, out of the last turn.
                    Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.3f, 0.9f);
                    Puff(feet, 7, 2.6f);
                    break;

                case Move.Scrape:
                    Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);
                    Sound(SoundID.Tink, frame.Tip, -0.7f, 0.5f);
                    Puff(feet, 5, 2.4f, 0.8f);
                    break;

                case Move.Upcut:
                    Sound(SoundID.Dig, feet, -0.7f, 0.7f);
                    Puff(feet, 5, 2f, 0.6f);
                    break;

                case Move.Smite:
                    Sound(SoundID.Item71, NPC.Center, -0.35f, 1f);
                    break;

                case Move.Alight:
                    Sound(SoundID.Dig, feet, -0.7f, 0.9f);
                    Puff(feet, 9, 2.8f);
                    Shake(3f, 8);
                    break;

                // ---- his third phase's own ----

                case Move.Rend:
                    Sound(SoundID.Item71, NPC.Center, -0.45f, 0.8f);
                    Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.Reel:
                    Sound(SoundID.NPCHit4, NPC.Center, -0.3f, 0.7f);
                    Puff(feet, 6, 2.4f, 0.8f);
                    break;

                case Move.Stagger:
                    Sound(SoundID.Dig, feet, -0.8f, 0.9f);
                    Puff(feet, 8, 2f);
                    break;

                // His posture worn through: the sword is beaten down, and he with it.
                case Move.Broken:
                    Sound(SoundID.Item37, NPC.Center, -0.5f, 1f);
                    Sound(SoundID.NPCHit4, NPC.Center, -0.5f, 0.9f);
                    Sound(SoundID.Dig, feet, -0.8f, 1f);
                    ParryEffects.Clash(Vector2.Lerp(frame.Guard, frame.Tip, 0.4f), new Vector2(-facing, -0.4f));
                    Puff(feet, 10, 2.4f);
                    Shake(6f, 16);
                    break;

                // The run at them behind the sword.
                case Move.Charge:
                    Sound(SoundID.Item71, NPC.Center, 0.1f, 0.9f);
                    Glint(frame.EyeAt, Eye, 26f);
                    Puff(feet, 6, 2.8f, 0.8f);
                    break;

                case Move.Spent:
                    Sound(SoundID.NPCHit1, NPC.Center, -0.6f, 0.9f);
                    Shake(5f, 14);
                    break;

                case Move.Knee:
                case Move.Shudder:
                    // Under the black: the sword driven into the ground, heard and not seen.
                    Sound(SoundID.DD2_MonkStaffGroundImpact, feet, -0.3f, 0.8f);
                    Sound(SoundID.Dig, feet, -0.7f, 0.9f);
                    break;

            }

            // His end (Foigh.End).
            if (Ending)
                EndEntered();

            // The wind, and the hook (Foigh.Reach).
            ReachEntered(feet);

            // His third phase's wheel (Foigh.Wild).
            WildEntered(feet);
        }

        /// <summary>
        /// With this machine's player in his hand, in his second phase's own move: the
        /// camera in close on the two of them, for as long as he has them. Asked every
        /// tick, the ones he goes from one part of it to the next in too: a tick it is not
        /// asked in is a tick the camera starts back out.
        /// </summary>
        private void LeanIn()
        {
            if (held == Main.myPlayer && (State == Move.Caught || State == Move.Pierce || State == Move.Hoist))
                Cinematic.Lean(HeldZoom, HeldZoomRate);
        }

        /// <summary>Every tick, on a machine with a screen: the sparks, dust and sounds that belong to this tick of the move he is in, and whoever he has hold of put where he has them.</summary>
        private void Looks()
        {
            Vector2 feet = Feet;

            Carry();
            Smoulder();

            // The wind, and the hook (Foigh.Reach).
            ReachLooks(feet);

            // His third phase's wheel, and the sword driven deeper (Foigh.Wild).
            WildLooks(feet);

            switch (State)
            {
                case Move.Walk:
                    // The dull side on the ground: it scrapes.
                    if (grounded && hasSword && Math.Abs(NPC.velocity.X) > 0.5f)
                    {
                        if (Main.rand.NextBool(4))
                        {
                            PADParticleHandler.SpawnParticle(new SparkShard(frame.Tip, new Vector2(-facing * Main.rand.NextFloat(0.5f, 2.5f), -Main.rand.NextFloat(0.5f, 2f)),
                                Sparks[Main.rand.Next(Sparks.Length)], 5f, 9f, 11f, Main.rand.Next(8, 14), falls: true));
                        }

                        if (Main.GameUpdateCount % 23 == 0)
                            Sound(SoundID.Tink, frame.Tip, -0.85f, 0.16f);
                    }
                    break;

                case Move.Draw:
                    if (At(M.DrawTakes)) Sound(SoundID.Item1, frame.NearFist, -0.6f, 0.5f);

                    if (At(M.DrawRips))
                    {
                        Sound(SoundID.Dig, PlantedGrip, -0.6f, 0.9f);
                        Sound(SoundID.Tink, PlantedGrip, -0.7f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 9, 2.4f);
                        Shake(3f, 8);
                    }

                    if (At(M.DrawRips + 6)) Sound(SoundID.Item71, NPC.Center, -0.5f, 0.9f);
                    if (At(M.DrawHigh)) Glint(frame.Tip, Steel, 34f, 18);
                    if (At(M.DrawFalls + 6)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.8f);

                    if (At(M.DrawLands))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                case Move.One:
                    if (At(8)) Glint(frame.EyeAt, Eye, 26f);
                    if (At(M.OneHold)) Sound(SoundID.Item71, NPC.Center, -0.35f, 1f);
                    if (At(M.OneDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 1f);
                    if (At(M.OneRest + 1)) Sound(SoundID.Item71, NPC.Center, 0f, 0.9f);
                    if (At(M.OneChambered - 4)) Glint(frame.Tip, Steel, 30f);

                    if (At(M.OneChambered + 2))
                    {
                        Sound(SoundID.DD2_GhastlyGlaivePierce, frame.Tip, -0.1f, 1f);
                        Wind(frame.Guard, frame.Tip, 7);
                    }

                    if (now > M.OneChambered && now <= M.OneThrust + 2)
                        LeaveGhost(0.6f);

                    if (At(M.OneLeap + 2))
                    {
                        Sound(SoundID.Item1, NPC.Center, -0.5f, 0.9f);
                        Puff(feet, 6, 2.6f, 0.8f);
                    }

                    if (now > M.OneLeap + 6 && grounded && !groundedWas)
                        Puff(feet, 6, 2.2f, 0.8f);
                    break;

                case Move.TwoGather:
                    if (At(2)) Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.TwoAir:
                    if (At(1))
                    {
                        Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.4f, 0.9f);
                        Puff(feetWas, 8, 2.8f);
                    }

                    if (Share(now) > 0.42f && now % 2 == 0)
                        Wind(frame.Tip - new Vector2(0f, 26f), frame.Tip, 1);
                    break;

                // (In his third phase he does not pull at it: Foigh.WildLooks.)
                case Move.TwoStuck when phase < 3:
                    if (At(M.FirstTug) || At(M.SecondTug))
                    {
                        Sound(SoundID.Tink, frame.Grip, -0.8f, 0.5f);
                        Puff(new Vector2(MathHelper.Lerp(frame.Grip.X, frame.Tip.X, 0.45f), feet.Y), 3, 1.4f, 0.6f);
                    }
                    break;

                case Move.TwoFree:
                    if (At(M.FreeRips - 2))
                    {
                        Sound(SoundID.Dig, frame.Grip, -0.5f, 0.9f);
                        Sound(SoundID.Item71, NPC.Center, -0.2f, 0.9f);
                        Puff(new Vector2(MathHelper.Lerp(frame.Grip.X, frame.Tip.X, 0.45f), feet.Y), 9, 2.6f);
                    }
                    break;

                case Move.Three:
                    if (At(M.ThreeTakes)) Glint(frame.EyeAt, Eye, 28f);
                    if (At(M.ThreeHold - 6)) Glint(frame.Tip, Steel, 38f, 16);
                    if (At(M.ThreeHold)) Sound(SoundID.Item71, NPC.Center, -0.6f, 1f);
                    if (At(M.ThreeDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 2f);
                    break;

                case Move.ThreeRound:
                    if (At(1)) Whirl();
                    break;

                case Move.ThreeEnd:
                    if (At(M.ThreeKnife - M.ThreeSpin)) Sound(SoundID.Item1, frame.NearFist, 0.55f, 0.8f);
                    if (At(M.ThreeBack - M.ThreeSpin + 6)) Sound(SoundID.Item1, NPC.Center, -0.6f, 0.7f);
                    break;

                case Move.FourGather:
                    if (At(2)) Glint(frame.EyeAt, Eye, 26f);
                    break;

                case Move.FourAir:
                    if (At(1))
                    {
                        Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.2f, 0.8f);
                        Puff(feetWas, 8, 2.8f);
                    }

                    if (At(FourWound - 6)) Glint(frame.Tip, CCBypass.Warning, 34f, 12);

                    if (At(FourLetsGo))
                    {
                        Sound(SoundID.DD2_JavelinThrowersAttack, NPC.Center, -0.5f, 1f);
                        Sound(SoundID.Item1, NPC.Center, -0.7f, 1f);
                    }
                    break;

                case Move.FourTake:
                    if (At(M.TakeTicks - 2))
                    {
                        Sound(SoundID.Dig, frame.NearFist, -0.5f, 0.9f);
                        Puff(new Vector2(frame.NearFist.X, feet.Y), 7, 2.4f);
                    }
                    break;

                case Move.FourRound:
                    if (At(1) || At(M.RoundTicks + 1)) Whirl();
                    break;

                case Move.Knives:
                {
                    int count = Throws;

                    for (int k = 0; k < count; k++)
                    {
                        if (At(k * M.KnifeEvery + M.KnifeBack - 3)) Glint(frame.FarFist, Steel, 16f, 8);
                        if (At(M.KnifeAt(k))) Sound(SoundID.Item1, frame.FarFist, 0.6f, 0.75f);
                    }
                    break;
                }

                case Move.Flash:
                    if (now >= FlashGather)
                        LeaveGhost();
                    break;

                case Move.Behind:
                    if (At(1))
                    {
                        Glint(frame.Tip, Steel, 34f, 20);
                        Sound(SoundID.Item37, frame.Tip, 0.5f, 0.45f);
                    }

                    if (At(M.BehindWait)) Sound(SoundID.Item71, NPC.Center, 0.1f, 1f);
                    if (now > M.BehindWait && now <= M.BehindCut + 2) LeaveGhost(0.5f);
                    break;

                case Move.Clash:
                    if (At(1)) Sound(SoundID.Item71, NPC.Center, 0.2f, 0.7f);
                    break;

                case Move.Grab:
                    if (At(M.GrabCloses)) Sound(SoundID.Item1, frame.FarFist, -0.2f, 0.9f);
                    break;

                case Move.Lift:
                    if (At(M.LiftThrows)) Sound(SoundID.Item1, frame.FarFist, -0.6f, 1f);
                    break;

                case Move.Spit:
                    if (At(M.SpitUp - 5)) Sound(SoundID.Item71, NPC.Center, 0.3f, 0.9f);
                    break;

                case Move.Spitted:
                    if (now % 9 == 0)
                        BloodDroplet.SpawnBurst(Vector2.Lerp(frame.Guard, frame.Tip, 0.55f), new Vector2(0f, 1f), 2, 30f);
                    break;

                case Move.Fling:
                    if (At(M.FlingBack)) Sound(SoundID.Item71, NPC.Center, -0.4f, 1f);

                    if (At(M.FlingLets))
                        BloodDroplet.SpawnBurst(flung != null ? OnSword() : Vector2.Lerp(frame.Guard, frame.Tip, 0.6f), new Vector2(facing, -0.3f), 12, 40f);

                    if (At(M.FlingDown + 1)) Impact(new Vector2(frame.Tip.X, feet.Y), 0.7f);
                    break;

                // ---- his second phase's own ----

                case Move.Rush:
                case Move.Charge:
                    if (grounded && now % 6 == 0)
                        Puff(feet, 2, 1.8f, 0.5f);
                    break;

                case Move.Hew:
                    if (At(M.HewHold)) Sound(SoundID.Item71, NPC.Center, -0.5f, 1f);
                    if (At(M.HewDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 2f);
                    if (At(M.HewRest + 1)) Sound(SoundID.Item71, NPC.Center, 0.05f, 0.9f);
                    break;

                case Move.Crouch:
                    if (At(2)) Glint(frame.EyeAt, Eye, 28f);
                    if (At(M.CrouchTicks - 6)) Glint(frame.FarFist, CCBypass.Warning, 22f, 10);
                    break;

                case Move.Spring:
                    if (now % 2 == 0)
                        Wind(frame.FarFist - new Vector2(facing * 30f, -8f), frame.FarFist, 1);
                    break;

                case Move.Pierce:
                    // The sword over his shoulder, to point at them.
                    if (At(M.PierceLands + 3)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.6f);

                    // Its point is in them: the one blow of it, and from here it goes on through, slowly.
                    if (At(M.PierceBites))
                    {
                        Vector2 at = InHand();

                        Sound(SoundID.DD2_GhastlyGlaivePierce, at, -0.35f, 0.9f);
                        Sound(SoundID.NPCHit1, at, -0.5f, 0.8f);
                        BloodDroplet.SpawnBurst(at, new Vector2(facing, -0.4f), 10, 40f);
                        Impale("FoighHeld");
                    }

                    if (now > M.PierceBites && now % 7 == 0)
                        BloodDroplet.SpawnBurst(InHand() + new Vector2(facing * 8f, 0f), new Vector2(facing * 0.5f, 1f), 2, 26f);
                    break;

                case Move.Hoist:
                    if (now % 9 == 0)
                        BloodDroplet.SpawnBurst(OnSword(), new Vector2(0f, 1f), 2, 30f);
                    break;

                // ---- and again: the late cut ----

                case Move.Poise:
                    // He looks along it: his eye, and a light up the blade (ShowBlade).
                    if (At(M.PoiseGleams))
                    {
                        Glint(frame.EyeAt, Eye, 28f, 16);
                        Sound(SoundID.Item37, frame.Guard, 0.5f, 0.4f);
                    }
                    break;

                case Move.Wake:
                    if (At(M.WakeCuts))
                        LateCut();

                    // The flick of the blade where it is.
                    if (At(M.WakeFlick + 2))
                    {
                        Sound(SoundID.Item1, frame.Tip, 0.45f, 0.6f);
                        Glint(frame.Tip, Steel, 20f, 8);
                    }

                    if (At(M.WakeRises + 8)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);

                    if (At(M.WakeTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                // ---- the mill ----

                case Move.Level:
                    if (At(M.LevelThere))
                    {
                        Glint(frame.Tip, Steel, 34f, 14);
                        Sound(SoundID.Item37, frame.Tip, 0.3f, 0.4f);
                    }
                    break;

                case Move.Mill:
                    // Each turn, as it begins: the air it pushes. And what he leaves behind him, turning.
                    // (By the turn crossed, not by the tick's number: his third phase's clock does not land on every tick.)
                    for (int turn = 0; turn < MillTurns; turn++)
                    {
                        if (At(turn * M.MillTurn + 1)) Whirl();
                    }

                    if (now % 3 == 0) LeaveGhost(0.4f);

                    if (grounded && now % 5 == 0)
                        Puff(feet, 2, 2f, 0.5f);
                    break;

                case Move.MillEnd:
                    if (At(M.MillEndUp)) Glint(frame.Tip, Arg > 0.5f ? CCBypass.Warning : Steel, 36f, 14);
                    if (At(M.MillEndDown - 4)) Sound(SoundID.Item71, NPC.Center, -0.6f, 1f);
                    if (At(M.MillEndDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 2f);
                    if (At(M.MillEndDown + 20)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);

                    if (At(M.MillEndTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                // ---- the cut up ----

                case Move.Scrape:
                    // The point on the ground at a run: sparks off it, back and up, and the sound of it.
                    if (grounded && hasSword && Math.Abs(NPC.velocity.X) > 0.5f)
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            PADParticleHandler.SpawnParticle(new SparkShard(frame.Tip, new Vector2(-facing * Main.rand.NextFloat(0.8f, 4.5f), -Main.rand.NextFloat(0.6f, 3.4f)),
                                Sparks[Main.rand.Next(Sparks.Length)], 5f, 9f, Main.rand.NextFloat(11f, 17f), Main.rand.Next(9, 16), falls: true));
                        }

                        if (now % 4 == 0) Sound(SoundID.Tink, frame.Tip, -0.75f, 0.3f);
                        if (now % 5 == 0) Puff(feet, 2, 1.8f, 0.5f);
                    }
                    break;

                case Move.Upcut:
                    if (At(M.UpcutSets + 1)) Sound(SoundID.Item71, NPC.Center, -0.2f, 1f);

                    // Forward through the ground, which heaves over it; and where its point comes up out of it.
                    if (now <= M.UpcutCuts + 1 && InGround(out Vector2 torn))
                        Plough(torn, false, torn.X);

                    if (At(M.UpcutCuts - 1))
                        Plough(new Vector2(frame.Tip.X, feet.Y), true, frame.Tip.X);

                    if (now >= M.UpcutCuts - 1 && now <= M.UpcutOver)
                        LeaveGhost(0.4f);

                    if (At(M.UpcutTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                // ---- in the air ----

                case Move.Hang:
                    if (At(2)) Glint(frame.EyeAt, Eye, 26f);

                    // The sword is up: what it is about to do goes through any shield, and is told as that is.
                    if (At((Arg > 0.5f ? HangShort : HangTicks) - 3)) Glint(frame.Tip, CCBypass.Warning, 32f, 10);
                    break;

                case Move.Smite:
                    if (now > M.SmiteCuts && now <= M.SmiteDown + 3)
                        LeaveGhost(0.45f);
                    break;

                case Move.Drop:
                    if (now % 3 == 0)
                        Wind(feet + new Vector2(0f, -46f), feet + new Vector2(0f, -8f), 1);
                    break;

                // ---- his third phase's own: three cuts, each let go of (what each sounds like as it goes is its own: FoighSlash) ----

                case Move.Rend:
                    // The blade is fed before the first of them.
                    if (At(M.RendFlares)) Flare();

                    if (At(M.RendHold)) Sound(SoundID.Item71, NPC.Center, -0.35f, 1f);
                    if (At(M.RendDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 1f);

                    // Sunk over it: a light on the point, and up.
                    if (At(M.RendSinks)) Glint(frame.Tip, Cinder, 30f, 12);
                    if (At(M.RendCoiled + 1)) Sound(SoundID.Item71, NPC.Center, 0f, 0.9f);

                    // In both hands, over his head.
                    if (At(M.RendHigh))
                    {
                        Glint(frame.Tip, Cinder, 38f, 16);
                        Sound(SoundID.Item37, frame.Tip, 0.3f, 0.4f);
                    }

                    if (At(M.RendHeld + 2)) Sound(SoundID.Item71, NPC.Center, -0.6f, 1f);
                    if (At(M.RendSmash)) Impact(new Vector2(frame.Tip.X, feet.Y), 2f);
                    if (At(M.RendStays + 9)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);

                    if (At(M.RendTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                case Move.Knee:
                case Move.Whet:
                    PassageLooks();
                    break;

                case Move.Shudder:
                case Move.Gut:
                case Move.Bled:
                case Move.Sever:
                case Move.Resume:
                    ThirdLooks();
                    break;

            }

            // His end (Foigh.End).
            if (Ending)
                EndLooks();
        }

        /// <summary>His sword on this machine's player: the cut seen, the shake, the sound.</summary>
        private void HitLooks(Player me, float power, FoighRig.Frame blade)
        {
            Vector2 along = (blade.Tip - blade.Guard).SafeNormalize(Vector2.UnitX);
            Vector2 across = new Vector2(-along.Y, along.X);

            Sound(SoundID.Item1, me.Center, -0.5f - 0.2f * power, 1f);
            Sound(SoundID.NPCHit18, me.Center, -0.3f, 0.6f);

            if (Burning)
            {
                BurnLooks(me, power, across);
            }
            else
            {
                PADParticleHandler.SpawnParticle(new SlashStreak(me.Center, across, 46f + 14f * power, 8f + 3f * power, 14));
                BloodDroplet.SpawnBurst(me.Center, across * (Main.rand.NextBool() ? 1f : -1f), (int)(6 + 4 * power), 40f);
            }

            me.PIS().BumpScreenShake(5f + 3f * power, 10 + (int)(4 * power));
        }

        /// <summary>This machine's player thrown up by the cut: the ground they left, and the air they go up through.</summary>
        private void LiftLooks(Player me)
        {
            Puff(me.Bottom, 8, 2.6f, 2.2f);
            Wind(me.Bottom, me.Bottom + new Vector2(facing * 12f, -150f), 9);
            Sound(SoundID.DD2_MonkStaffSwing, me.Center, 0.1f, 0.8f);
        }

        /// <summary>This machine's player sent down out of the air: the air they go down through, and a light on it.</summary>
        private void SpikeLooks(Player me)
        {
            Wind(me.Center, me.Center + new Vector2(facing * 16f, 170f), 10);
            PADParticleHandler.SpawnParticle(new ParryRing(me.Center, 10f, 86f, 12, Steel * 0.4f));
            Sound(SoundID.DD2_MonkStaffGroundImpact, me.Center, 0.1f, 0.7f);

            // (The flash is the screen's, the player's: not for the copy.)
            if (!MimicSystem.Is(me))
                Cinematic.Flash(new Color(236, 244, 255), 0.1f);
        }

        /// <summary>
        /// The late cut's line opens, on a machine with a screen: seen and heard by whoever
        /// is near, all the way along it; and against this machine's own player, as his
        /// sword is (Strike). The parry first, from whichever end of it they face (it lies
        /// through them: it is the moment that has to be right, not the way they look);
        /// then the hit, through armour. The Everclear Mirror's copy the same, as a second
        /// player (MimicSystem.AlsoOnCopy).
        /// </summary>
        private void LateCut()
        {
            Vector2 from = LineFrom;
            Vector2 to = LineTo;
            Vector2 along = to - from;
            float length = along.Length();

            if (length < 1f)
                return;

            along /= length;

            Vector2 middle = (from + to) * 0.5f;
            int knots = Math.Max(2, (int)(length / 56f));

            if (Burning)
                BloodSlash.Pair(middle, along, length * 0.5f + 10f, 16f, 30);
            else
                PADParticleHandler.SpawnParticle(new SlashStreak(middle, along, length * 0.5f + 10f, 22f, 30));

            for (int i = 0; i <= knots; i++)
                SparkShard.Spray(Vector2.Lerp(from, to, i / (float)knots), Steel, 3, 4.5f, 20f, 14, falls: true, palette: Sparks);

            Wind(from, to, Math.Min(20, 6 + knots));

            Sound(SoundID.Item71, middle, -0.45f, 1f);
            Sound(SoundID.DD2_GhastlyGlaivePierce, middle, -0.3f, 0.9f);

            if (Near())
                Cinematic.Flash(new Color(236, 244, 255), 0.1f);

            Shake(6f, 12);

            LateCutOn(from, to, along);
            MimicSystem.AlsoOnCopy(() => LateCutOn(from, to, along));
        }

        /// <summary>The late cut's line against whoever is this machine's player for the moment (the copy, under MimicSystem.AlsoOnCopy).</summary>
        private void LateCutOn(Vector2 from, Vector2 to, Vector2 along)
        {
            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            ParryPlayer parry = me.GetModPlayer<ParryPlayer>();

            if (parry.Spares(NPC))
                return;

            float point = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, to, LateWidth, ref point))
                return;

            if (parry.MeetsLine(NPC, from, to, LateWidth) || parry.MeetsLine(NPC, to, from, LateWidth))
                return;

            if (me.immune)
                return;

            int damage = Main.DamageVar(SwordDamage(), -me.luck);

            // The way it went: the way he faces still.
            if (!Pierce(me, "FoighLate", damage, facing, KnockCut))
                return;

            Wound(me);

            // The mark of it on them lies along the line, not across it.
            Vector2 across = new Vector2(-along.Y, along.X);
            FoighRig.Frame mark = frame;

            mark.Guard = me.Center - across;
            mark.Tip = me.Center + across;

            HitLooks(me, 1f, mark);
        }

        // ============================================================
        // DRAWING
        // ============================================================

        private static readonly List<FoighRig.Part> parts = new List<FoighRig.Part>(FoighRig.Pieces);

        /// <summary>The view the open sprite batch draws through, as the game opened it for NPCs (Main.DoDraw_DrawNPCsOverTiles and BehindTiles, 1.4.4).</summary>
        private static void Open(SpriteBatch spriteBatch, Matrix view)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, view);
        }

        /// <summary>
        /// The view for pictures drawn narrower across about an upright line (the whole of
        /// him, turning) and, for one of them, shorter along a line of its own through a
        /// point (his sword, or his head): on top of the game's own view.
        /// </summary>
        private static Matrix Through(float wide, float aboutX, float stretch, float axis, Vector2 at)
        {
            Matrix view = Matrix.Identity;

            if (Math.Abs(stretch - 1f) > 0.001f)
            {
                view = Matrix.CreateTranslation(-at.X, -at.Y, 0f)
                    * Matrix.CreateRotationZ(-axis)
                    * Matrix.CreateScale(stretch, 1f, 1f)
                    * Matrix.CreateRotationZ(axis)
                    * Matrix.CreateTranslation(at.X, at.Y, 0f);
            }

            if (Math.Abs(wide - 1f) > 0.001f)
            {
                view *= Matrix.CreateTranslation(-aboutX, 0f, 0f)
                    * Matrix.CreateScale(wide, 1f, 1f)
                    * Matrix.CreateTranslation(aboutX, 0f, 0f);
            }

            return view * Main.Transform;
        }

        /// <summary>
        /// One of him: these pictures, back to front, in this colour. <paramref name="outline"/>
        /// draws the crowd-control tell's ring of each in place of the picture;
        /// <paramref name="sharpness"/> is how much of the sword's sharp side is shown over
        /// its blunt one, and <paramref name="blood"/> how much of its bloodied side over
        /// that.
        /// </summary>
        private static void DrawParts(SpriteBatch spriteBatch, List<FoighRig.Part> list, FoighRig.Frame of, Vector2 screenPos, Color colour, bool outline = false, float sharpness = 0f, float blood = 0f)
        {
            float aboutX = of.TurnAbout - screenPos.X;
            bool turned = Math.Abs(of.Turn - 1f) > 0.001f;

            // The view the batch is open with: the game's own, or one of ours and which.
            bool own = false;
            float ownStretch = 1f;

            foreach (FoighRig.Part part in list)
            {
                Asset<Texture2D> asset = pieces[part.Piece];

                if (asset == null || !asset.IsLoaded)
                    continue;

                Vector2 at = part.At - screenPos;
                bool stretched = Math.Abs(part.Stretch - 1f) > 0.001f;
                bool special = turned || stretched;

                // A picture that is narrowed or shortened goes through a view of its own;
                // those that are only narrowed with the whole of him share one.
                if (special != own || (special && (stretched || ownStretch != 1f)))
                {
                    spriteBatch.End();
                    Open(spriteBatch, special ? Through(of.Turn, aboutX, part.Stretch, part.Axis, at) : Main.Transform);

                    own = special;
                    ownStretch = stretched ? part.Stretch : 1f;
                }

                SpriteEffects flip = part.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                if (outline)
                {
                    CCBypass.Outline(spriteBatch, asset.Value, null, at, part.Rotation, part.Origin, 1f, flip);
                    continue;
                }

                // The sword's sharp side over its blunt one, by as much of it as is shown,
                // and its bloodied side over that: the same shape and the same place on the
                // picture, so one covers the other.
                bool edged = part.Piece == FoighRig.Sword && sharpness > 0f && sharp != null && sharp.IsLoaded;
                bool red = part.Piece == FoighRig.Sword && blood > 0f && bloody != null && bloody.IsLoaded;

                if (red && blood >= 1f)
                {
                    spriteBatch.Draw(bloody.Value, at, null, colour, part.Rotation, part.Origin, 1f, flip, 0f);
                    continue;
                }

                if (!edged || sharpness < 1f)
                    spriteBatch.Draw(asset.Value, at, null, colour, part.Rotation, part.Origin, 1f, flip, 0f);

                if (edged)
                    spriteBatch.Draw(sharp.Value, at, null, colour * Math.Min(1f, sharpness), part.Rotation, part.Origin, 1f, flip, 0f);

                if (red)
                    spriteBatch.Draw(bloody.Value, at, null, colour * Math.Min(1f, blood), part.Rotation, part.Origin, 1f, flip, 0f);
            }

            if (own)
            {
                spriteBatch.End();
                Open(spriteBatch, Main.Transform);
            }
        }

        /// <summary>The sword standing in the ground on the road, where he will take it from: on its own, not turned with him.</summary>
        private void DrawPlanted(SpriteBatch spriteBatch, Vector2 screenPos, Color colour)
        {
            if (hasSword || State > Move.Draw)
                return;

            FoighRig.Part sword = FoighRig.LooseSword(PlantedGrip, PlantedBlade, false);
            Asset<Texture2D> asset = pieces[FoighRig.Sword];

            if (asset == null || !asset.IsLoaded)
                return;

            spriteBatch.Draw(asset.Value, sword.At - screenPos, null, colour, sword.Rotation, sword.Origin, 1f,
                sword.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsABestiaryIconDummy)
                return false;

            Color light = NPC.GetAlpha(drawColor);

            // The late cut's line, under everything of him.
            if (lineShown > 0.01f)
            {
                spriteBatch.End();
                DrawLine();
                Open(spriteBatch, Main.Transform);
            }

            // What he left behind him, palest first.
            foreach (Ghost ghost in ghosts)
            {
                FoighRig.Pose left = ghost.Pose;

                parts.Clear();
                FoighRig.Frame where = FoighRig.Build(left, ghost.Feet, ghost.Facing, parts);

                // Steel; and, burning, what the sword burns with.
                Color tint = Color.Lerp(Steel, BurnRed, flame);
                Color pale = new Color(tint.R, tint.G, tint.B, 0) * (0.34f * ghost.Left);

                DrawParts(spriteBatch, parts, where, screenPos, pale);
            }

            DrawPlanted(spriteBatch, screenPos, light);
            DrawLeft(spriteBatch, screenPos, light);

            // His end: the sword out of his hand, behind all of him (or, through him, under
            // his body: below); and nothing more of him once he is ash, or gone from the
            // picture under the black (Foigh.End).
            bool loose = EndSwordShown(out FoighRig.Part looseSword, out bool through);

            if (loose && (!through || EndHidden))
                DrawLoose(spriteBatch, looseSword, screenPos);

            if (EndHidden)
                return false;

            parts.Clear();
            FoighRig.Frame shown = FoighRig.Build(pose, Feet, facing, parts);

            if (loose && through)
                UnderBody(parts, looseSword);

            // His hook, under the rest of him as his bottom arm is (Foigh.Reach).
            DrawHook(spriteBatch, screenPos, shown, light);

            DrawParts(spriteBatch, parts, shown, screenPos, light, false, sharpShown, bloodShown);

            // Their wings, in his bottom hand since the cut.
            DrawWings(spriteBatch, screenPos, shown);

            // The stone in his bottom hand, lying along the blade.
            if (State == Move.Whet && M.StoneOut(now) && stone != null && stone.IsLoaded)
            {
                spriteBatch.Draw(stone.Value, shown.FarFist - screenPos, null, light, shown.Blade, stone.Size() * 0.5f, 1f,
                    SpriteEffects.None, 0f);
            }

            bool trailed = trail > 0.01f && pose.Hand != 0 && NPC.alpha < 200;
            bool gleams = gleam != NoGleam && pose.Hand != 0;
            bool burns = flame > 0.01f && pose.Hand != 0 && NPC.alpha < 200;

            if (trailed || gleams || burns)
            {
                spriteBatch.End();

                if (burns)
                    DrawBurning(shown);

                if (trailed)
                    DrawTrail();

                if (gleams)
                    DrawGleam(shown);

                Open(spriteBatch, Main.Transform);
            }

            if (PostureBarShown)
                DrawPosture(spriteBatch, screenPos);

            return false;
        }

        /// <summary>
        /// By request ("keep posture ui hidden"): his posture has no bar. It is seen when
        /// it is worn through and he goes down, and not before. What would draw a bar is
        /// kept below, and is asked for only if this is turned on.
        /// </summary>
        private static readonly bool PostureBarShown = false;

        /// <summary>The posture bar: how far over his head, half its length when full, and how thick.</summary>
        private const float PostureOver = 30f;
        private const int PostureHalf = 30;
        private const int PostureTall = 3;

        private static readonly Color PosturePale = new Color(255, 232, 170);
        private static readonly Color PostureWarm = new Color(255, 160, 48);
        private static readonly Color PostureHot = new Color(255, 64, 40);

        /// <summary>
        /// His posture (Foigh.Hold), over his head, while any of it is worn: a bar that
        /// grows from its middle both ways, pale, then orange, then red; and all of it,
        /// beating, while he is broken. In its own light: it is a mark, not a thing in
        /// the hall. (Not drawn: PostureBarShown.)
        /// </summary>
        private void DrawPosture(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float share = MathHelper.Clamp(postureShown, 0f, 1f);

            if (share <= 0.01f || NPC.alpha > 200)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);

            int x = (int)Math.Round(NPC.Center.X - screenPos.X);
            int y = (int)Math.Round(NPC.position.Y - screenPos.Y - PostureOver);

            float fade = MathHelper.Clamp(share / 0.08f, 0f, 1f) * (1f - NPC.alpha / 255f);

            Color fill = share < 0.5f
                ? Color.Lerp(PosturePale, PostureWarm, share * 2f)
                : Color.Lerp(PostureWarm, PostureHot, share * 2f - 1f);

            if (State == Move.Broken)
                fill = Color.Lerp(PostureHot, Color.White, 0.5f + 0.5f * (float)Math.Sin(now * 0.45f));

            int half = Math.Max(1, (int)Math.Round(PostureHalf * share));

            spriteBatch.Draw(pixel, new Rectangle(x - PostureHalf - 1, y - 1, PostureHalf * 2 + 2, PostureTall + 2), one, new Color(8, 8, 10) * (0.6f * fade));
            spriteBatch.Draw(pixel, new Rectangle(x - half, y, half * 2, PostureTall), one, fill * fade);

            // The mark at its middle, where it grows from.
            spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 2, 2, PostureTall + 4), one, Color.Lerp(fill, Color.White, 0.6f) * fade);
        }

        /// <summary>The crowd-control tell's outline: of every picture of him, under them (PADGlobalNPC draws this before him).</summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            parts.Clear();
            FoighRig.Frame shown = FoighRig.Build(pose, Feet, facing, parts);

            DrawParts(spriteBatch, parts, shown, screenPos, Color.White, true);
        }

        /// <summary>
        /// The late cut's line: a hair of dark, so that it shows against a bright wall; a
        /// hair of light along that, and a little light about it; all of it thinning to
        /// nothing at both ends. While he stands past them a brighter place runs along it,
        /// from where he was to where he is, and is at its end as it opens.
        /// </summary>
        private void DrawLine()
        {
            if (softLine == null || !softLine.IsLoaded)
                return;

            Vector2 along = lineTo - lineFrom;
            float length = along.Length();

            if (length < 8f)
                return;

            along /= length;

            Vector2 across = new Vector2(-along.Y, along.X);
            float runs = State == Move.Wake ? MathHelper.Clamp(clock / M.WakeCuts, 0f, 1f) : -1f;

            LineBand(along, across, length, 3.5f, new Color(Dusk.R, Dusk.G, Dusk.B, 255) * 0.55f, -1f);
            LineBand(along, across, length, 9f, new Color(Steel.R, Steel.G, Steel.B, 0) * 0.3f, runs);
            LineBand(along, across, length, 2.5f, new Color(255, 255, 255, 0), runs);
        }

        /// <summary>One band of the line: this wide to either side of it, this colour at its strongest.</summary>
        private void LineBand(Vector2 along, Vector2 across, float length, float half, Color colour, float runs)
        {
            const int steps = 28;

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                float lit = lineShown * MathHelper.Clamp(Math.Min(k, 1f - k) / 0.12f, 0f, 1f);

                if (runs >= 0f)
                {
                    float off = (k - runs) / 0.07f;

                    lit *= 1f + 0.9f * (float)Math.Exp(-off * off);
                }

                Color here = colour * Math.Min(1f, lit);
                Vector2 at = lineFrom + along * (length * k);

                Strip.Pair(at - across * half, at + across * half, here, here, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }

        /// <summary>
        /// A light running up the blade from its guard to its point: brightest where it is,
        /// and gone a fifth of the blade either side of that. Light only, added to what is
        /// there.
        /// </summary>
        private void DrawGleam(FoighRig.Frame of)
        {
            if (softLine == null || !softLine.IsLoaded)
                return;

            Vector2 along = of.Tip - of.Guard;
            float length = along.Length();

            if (length < 4f)
                return;

            along /= length;

            Vector2 across = new Vector2(-along.Y, along.X) * 7f;
            const int steps = 16;

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                float off = (k - gleam) / 0.2f;
                Color light = new Color(255, 255, 255, 0) * (0.85f * (float)Math.Exp(-off * off));
                Vector2 at = of.Guard + along * (length * k);

                Strip.Pair(at - across, at + across, light, light, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }

        // Laid out again every frame it is drawn, only ever on the main thread.
        private static readonly Vector2[] TrailIn = new Vector2[TrailSteps + 1];
        private static readonly Vector2[] TrailOut = new Vector2[TrailSteps + 1];
        private static readonly Vector2[] TrailEdge = new Vector2[TrailSteps + 1];
        private static readonly float[] TrailStrength = new float[TrailSteps + 1];

        /// <summary>
        /// What the blade leaves behind it: a band from a little past the guard to the point,
        /// over the blade's way these last few ticks, strongest where it is now. First dark,
        /// so that it shows against a bright wall; then steel over that, as light; then a
        /// paler line along the point's own path. The sword bloodied, the dark is all but
        /// black and thicker, the light over it a little red, and the line along the
        /// point's path red and bright: a black sweep with a red edge.
        /// </summary>
        private void DrawTrail()
        {
            if (smear == null || softLine == null || !smear.IsLoaded || !softLine.IsLoaded)
                return;

            if (!TrailPoints())
                return;

            // Steel; or, the sword bloodied, a sweep all but black with red in it, and a
            // red line along the way its point went.
            float red = Math.Max(flame, bloodShown);
            Color shade = Color.Lerp(Dusk, Soot, red);
            Color metal = Color.Lerp(Steel, BurnRed, red);
            Color edge = Color.Lerp(Color.White, TrailRed, red);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color dark = new Color(shade.R, shade.G, shade.B, 255) * ((0.4f + 0.5f * red) * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)TrailSteps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color bright = new Color(metal.R, metal.G, metal.B, 0) * ((0.42f + 0.02f * red) * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], bright, bright, i / (float)TrailSteps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color pale = new Color(edge.R, edge.G, edge.B, 0) * ((0.8f + 0.2f * red) * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)TrailSteps);
            }
            Strip.Draw(softLine.Value);
        }

        /// <summary>
        /// Where the blade has been over the last ticks, newest first: for each moment,
        /// the two ends of what it swept, the inner end of the line along its point's own
        /// path, and how strongly it still shows. False with nothing of it to draw yet.
        /// </summary>
        private bool TrailPoints()
        {
            // So many ticks of the game back: more of the move's own clock than that, in a
            // move his second phase quickens. Burning, it hangs half as long again. (The
            // ticks the sword lingered are ticks of the game too: ClockAgo, Foigh.Wild.)
            float tempo = Tempo;
            float back = Math.Min(TrailTicks * (1f + 0.5f * flame), clock / tempo + Stood);

            if (back < 0.5f)
                return false;

            for (int i = 0; i <= TrailSteps; i++)
            {
                float k = i / (float)TrailSteps;
                float ago = back * k;

                FoighRig.Pose at = i == 0 ? pose : PoseOf(State, ClockAgo(ago));

                if (i != 0 && LaysBlade(at))
                    FoighRig.Ground(ref at);

                FoighRig.Frame blade = FoighRig.Build(at, FeetAgo(ago), facing);

                TrailIn[i] = Vector2.Lerp(blade.Guard, blade.Tip, 0.18f);
                TrailOut[i] = blade.Tip;
                TrailEdge[i] = Vector2.Lerp(blade.Guard, blade.Tip, 0.9f);
                TrailStrength[i] = (float)Math.Pow(1f - k, 1.3f) * trail;
            }

            return true;
        }
    }
}
