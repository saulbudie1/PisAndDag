using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
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
    /// He is seven pictures (FoighRig), each turned about its own joint. Seen turning, all
    /// of him is drawn narrower across, about the upright line through his feet, and his
    /// sword shorter along its own length as it points at the eye or away: a sprite batch
    /// cannot do either to one picture, so those are drawn through a view of their own
    /// (<see cref="Through"/>), and only those, only then.
    /// </summary>
    public partial class Foigh : ICCBypassOutline
    {
        private static readonly Asset<Texture2D>[] pieces = new Asset<Texture2D>[FoighRig.Pieces];
        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> softLine;

        /// <summary>Steel, and the pale edge of it; his eye.</summary>
        private static readonly Color Steel = new Color(214, 226, 240);
        private static readonly Color Dusk = new Color(52, 44, 60);
        private static readonly Color Eye = new Color(255, 70, 92);
        private static readonly Color[] Sparks = { new Color(255, 255, 255), new Color(214, 226, 240), new Color(255, 214, 160) };

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

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            for (int i = 0; i < pieces.Length; i++)
                pieces[i] = null;

            smear = null;
            softLine = null;
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

        /// <summary>Where his feet have been, tick by tick, newest first: what the trail is laid along.</summary>
        private readonly Vector2[] feetLog = new Vector2[8];
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
            }

            // Not out of a flash step, which is a cut; not on the road, where every pose is
            // already the last one's end; and never the first he is seen in.
            bool cutTo = (int)from < 0 || from == Move.Flash || State == Move.Flash || State <= Move.Begin || from <= Move.Begin;

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
            else if (State == Move.FourRun)
                gait += speed / M.RunStride;

            stride = MathHelper.Lerp(stride, State == Move.Walk && speed > 0.2f ? 1f : 0f, 0.14f);

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

            FoighRig.Pose shown = PoseOf(State, now);

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

            // The trail: up at once while the blade cuts, gone a few ticks after.
            trail = hasSword && Cuts(now, out _, out _) ? 1f : Math.Max(0f, trail - 0.22f);
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

        /// <summary>Whether a change of the way he faces is seen as him turning about: on his feet, not in the middle of a blow.</summary>
        private bool TurnsAbout => State == Move.Walk || State == Move.Guard || State == Move.Knives || State == Move.Leave;

        /// <summary>
        /// Whether the blade is kept out of the ground in this pose: on his feet on level
        /// ground, in anything but the moves that put it there on purpose.
        /// </summary>
        private bool LaysBlade(in FoighRig.Pose of)
        {
            if (of.Air || of.Hand == 0 || !grounded)
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

                case Move.Reel:
                    Sound(SoundID.NPCHit4, NPC.Center, -0.3f, 0.7f);
                    Puff(feet, 6, 2.4f, 0.8f);
                    break;

                case Move.Stagger:
                    Sound(SoundID.Dig, feet, -0.8f, 0.9f);
                    Puff(feet, 8, 2f);
                    break;

                case Move.Dying:
                    Sound(SoundID.Dig, feet, -0.9f, 1f);
                    Sound(SoundID.NPCDeath1, NPC.Center, -0.7f, 0.6f);
                    Puff(feet, 10, 2.2f);
                    Shake(4f, 14);
                    break;
            }
        }

        /// <summary>Every tick, on a machine with a screen: the sparks, dust and sounds that belong to this tick of the move he is in, and whoever he has hold of put where he has them.</summary>
        private void Looks()
        {
            Vector2 feet = Feet;

            Carry();

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

                case Move.TwoStuck:
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
                    int count = Math.Max(1, (int)Arg);

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
                        BloodDroplet.SpawnBurst(Vector2.Lerp(frame.Guard, frame.Tip, 0.6f), new Vector2(facing, -0.3f), 12, 40f);

                    if (At(M.FlingDown + 1)) Impact(new Vector2(frame.Tip.X, feet.Y), 0.7f);
                    break;

                case Move.Dying:
                    if (now >= DyingTicks - DyingFade && now % 2 == 0)
                    {
                        Vector2 at = NPC.position + new Vector2(Main.rand.NextFloat(NPC.width), Main.rand.NextFloat(NPC.height));

                        PADParticleHandler.SpawnParticle(new DustFog(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 1.2f)),
                            new Color(120, 116, 122), Main.rand.Next(50, 80), Main.rand.NextFloat(0.3f, 0.6f), 0.3f));
                    }
                    break;
            }
        }

        /// <summary>His sword on this machine's player: the cut seen, the shake, the sound.</summary>
        private void HitLooks(Player me, float power, FoighRig.Frame blade)
        {
            Vector2 along = (blade.Tip - blade.Guard).SafeNormalize(Vector2.UnitX);
            Vector2 across = new Vector2(-along.Y, along.X);

            Sound(SoundID.Item1, me.Center, -0.5f - 0.2f * power, 1f);
            Sound(SoundID.NPCHit18, me.Center, -0.3f, 0.6f);

            PADParticleHandler.SpawnParticle(new SlashStreak(me.Center, across, 46f + 14f * power, 8f + 3f * power, 14));
            BloodDroplet.SpawnBurst(me.Center, across * (Main.rand.NextBool() ? 1f : -1f), (int)(6 + 4 * power), 40f);

            me.PIS().BumpScreenShake(5f + 3f * power, 10 + (int)(4 * power));
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
        /// draws the crowd-control tell's ring of each in place of the picture.
        /// </summary>
        private static void DrawParts(SpriteBatch spriteBatch, List<FoighRig.Part> list, FoighRig.Frame of, Vector2 screenPos, Color colour, bool outline = false)
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
                    CCBypass.Outline(spriteBatch, asset.Value, null, at, part.Rotation, part.Origin, 1f, flip);
                else
                    spriteBatch.Draw(asset.Value, at, null, colour, part.Rotation, part.Origin, 1f, flip, 0f);
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

            // What he left behind him, palest first.
            foreach (Ghost ghost in ghosts)
            {
                FoighRig.Pose left = ghost.Pose;

                parts.Clear();
                FoighRig.Frame where = FoighRig.Build(left, ghost.Feet, ghost.Facing, parts);

                Color pale = new Color(Steel.R, Steel.G, Steel.B, 0) * (0.34f * ghost.Left);

                DrawParts(spriteBatch, parts, where, screenPos, pale);
            }

            DrawPlanted(spriteBatch, screenPos, light);

            parts.Clear();
            FoighRig.Frame shown = FoighRig.Build(pose, Feet, facing, parts);

            DrawParts(spriteBatch, parts, shown, screenPos, light);

            if (trail > 0.01f && pose.Hand != 0 && NPC.alpha < 200)
            {
                spriteBatch.End();
                DrawTrail();
                Open(spriteBatch, Main.Transform);
            }

            return false;
        }

        /// <summary>The crowd-control tell's outline: of every picture of him, under them (PADGlobalNPC draws this before him).</summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            parts.Clear();
            FoighRig.Frame shown = FoighRig.Build(pose, Feet, facing, parts);

            DrawParts(spriteBatch, parts, shown, screenPos, Color.White, true);
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
        /// paler line along the point's own path.
        /// </summary>
        private void DrawTrail()
        {
            if (smear == null || softLine == null || !smear.IsLoaded || !softLine.IsLoaded)
                return;

            float back = Math.Min(TrailTicks, now);

            if (back < 0.5f)
                return;

            for (int i = 0; i <= TrailSteps; i++)
            {
                float k = i / (float)TrailSteps;
                float ago = back * k;

                FoighRig.Pose at = i == 0 ? pose : PoseOf(State, now - ago);

                if (i != 0 && LaysBlade(at))
                    FoighRig.Ground(ref at);

                FoighRig.Frame blade = FoighRig.Build(at, FeetAgo(ago), facing);

                TrailIn[i] = Vector2.Lerp(blade.Guard, blade.Tip, 0.18f);
                TrailOut[i] = blade.Tip;
                TrailEdge[i] = Vector2.Lerp(blade.Guard, blade.Tip, 0.9f);
                TrailStrength[i] = (float)Math.Pow(1f - k, 1.3f) * trail;
            }

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color dark = new Color(Dusk.R, Dusk.G, Dusk.B, 255) * (0.4f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)TrailSteps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color bright = new Color(Steel.R, Steel.G, Steel.B, 0) * (0.42f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], bright, bright, i / (float)TrailSteps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color pale = new Color(255, 255, 255, 0) * (0.8f * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)TrailSteps);
            }
            Strip.Draw(softLine.Value);
        }
    }
}
