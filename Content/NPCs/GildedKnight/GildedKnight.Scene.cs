using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// His scene, by request: "fades to him from afar zoomed, walks slowly with his 2 swords in
    /// his sheathes. Each walk he takes makes a metal wrangling armor sound. Unsheathes from side
    /// to side in x formation (left to right), points one blade slowly at player, pans and unzoom
    /// to player." And his change at half his life, and his end.
    ///
    ///   0     the picture fades to black for everyone in his field;
    ///   24    under the black the camera is put on him, leaned in close;
    ///   24    it fades back up on him, far off from them, walking at them slowly, his swords
    ///         home at his hips, his armour ringing at every footfall (seven steps);
    ///   Walk  he stops; draws both swords across himself together and crosses them before his
    ///         face, the light gathering where they cross; parts them, the Sun down ahead of him
    ///         and the Grace down behind, the two arcs going all the way round him;
    ///   Draw  he points the Sun at them, slowly, on a straight arm, and a glint runs to its point;
    ///         his name comes up under him and goes to the bar;
    ///   Pan   the camera goes back to them over a second and a half and the lean lets go;
    ///         their feet are theirs; the fight.
    ///
    /// Held E skips it, in a world of one (CinematicSkip): he is in his guard, his name up.
    /// </summary>
    public partial class GildedKnight
    {
        // ============================================================
        // THE SCENE'S TICKS
        // ============================================================

        private const int FadeOut = 24;
        private const int FadeIn = 30;

        /// <summary>His walk: how far a tick, and the most steps (seven, from far off; fewer if they are nearer).</summary>
        private const float WalkSpeed = 0.72f;
        private const int WalkStepsMost = 7;

        /// <summary>Where he stops short of them: never nearer than this to where they stand.</summary>
        private const float WalkShort = 260f;

        /// <summary>The steps he walks this scene (sent: every machine's scene must be the same length).</summary>
        private byte walkSteps = WalkStepsMost;

        /// <summary>The ticks the walk takes, and the stop after it.</summary>
        private int WalkTicks => (int)Math.Ceiling(walkSteps * KnightMoves.Stride / WalkSpeed);
        private const int StopTicks = 16;

        private static int WalkFrom => FadeOut;
        private int DrawFrom => WalkFrom + WalkTicks + (walkSteps > 0 ? StopTicks : 0);
        private int PointFrom => DrawFrom + KnightMoves.DrawTicks;
        private int NameAt => PointFrom + KnightMoves.PointRise + 10;
        private int PanAt => PointFrom + KnightMoves.PointTicks - 20;
        private const int PanTicks = 90;

        /// <summary>The scene ends, and their feet are theirs, with the camera half way home (it goes on home).</summary>
        private int SceneTicks => PanAt + PanTicks / 2;

        /// <summary>The steps there is room for between him and them, as his scene begins (where the world is decided).</summary>
        private void MeasureWalk(Player target)
        {
            if (target == null)
            {
                walkSteps = WalkStepsMost;
                return;
            }

            float room = Math.Abs(target.Center.X - Feet.X) - WalkShort;
            walkSteps = (byte)Math.Clamp((int)(room / KnightMoves.Stride), 0, WalkStepsMost);
        }

        /// <summary>The lean on him while the scene is on him.</summary>
        private const float SceneZoom = 1.5f;

        /// <summary>The way he points: at them, as they stood when the point began (this machine's).</summary>
        private float pointAim;
        private KnightTrack pointTrack;

        /// <summary>This machine's own part of it: named, panned home, skipped.</summary>
        private bool named;
        private bool panned;

        // ============================================================
        // EVERY MACHINE
        // ============================================================

        private void SceneTick(Player target)
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.dontTakeDamage = true;

            if (target == null)
                target = Retarget();

            int t = Timer;

            if (t < WalkFrom)
            {
                Pose = KnightMoves.Rest;
                NPC.velocity.X = 0f;

                if (target != null)
                    Face(WayTo(target));
            }
            else if (t < WalkFrom + WalkTicks)
            {
                // The walk: slowly at them, a step every half a second and more.
                float before = gait;
                gait += WalkSpeed / KnightMoves.Stride;

                NPC.velocity.X = WalkSpeed * facing;
                Pose = KnightMoves.Walk(gait);

                if (KnightMoves.Footfall(before, gait))
                    Step();
            }
            else if (t < DrawFrom)
            {
                NPC.velocity.X = 0f;
                Pose = KnightTrack.Between(KnightMoves.Walk(gait), KnightMoves.Rest, (t - WalkFrom - WalkTicks) / (float)StopTicks);
            }
            else if (t < PointFrom)
            {
                NPC.velocity.X = 0f;
                int d = t - DrawFrom;
                Pose = KnightMoves.Draw.Sample(d);

                if (At(DrawFrom + KnightMoves.DrawFree))
                    KnightFx.Drawn(this);

                if (At(DrawFrom + KnightMoves.DrawCrossed))
                    KnightFx.Crossed(this);

                if (At(DrawFrom + KnightMoves.DrawParting))
                    KnightFx.Parting(this);
            }
            else
            {
                NPC.velocity.X = 0f;
                int d = t - PointFrom;

                if (pointTrack == null || At(PointFrom))
                {
                    pointAim = target != null ? AimAt(target) : 0f;
                    pointTrack = KnightMoves.Point(pointAim);
                }

                KnightRig.Pose p = pointTrack.Sample(d);
                Pose = d < 12 ? KnightTrack.Between(KnightMoves.Draw.Last, p, d / 12f) : p;

                if (At(PointFrom + KnightMoves.PointRise))
                    KnightFx.Pointed(this);
            }

            if (!Main.dedServ)
                SceneHere();

            if (Authority && t >= SceneTicks)
                Begin();
        }

        /// <summary>The scene is over (or skipped): his guard, swords out, his rest before his first move (the camera home by then).</summary>
        private void Begin()
        {
            if (!Authority)
                return;

            rest = 90;
            Go(Act.Stance);
        }

        // ============================================================
        // THE PLAYERS IN HIS FIELD
        // ============================================================

        /// <summary>The scene for this machine's player, if they are in his field.</summary>
        private void SceneHere()
        {
            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || !InField(me))
                return;

            int t = Timer;
            Vector2 him = Feet + new Vector2(0f, -46f);

            CinematicSkip.Offer();

            if (CinematicSkip.Take() && Main.netMode == NetmodeID.SinglePlayer)
            {
                Skip();
                return;
            }

            if (t < PanAt + PanTicks / 2)
            {
                Cinematic.Hold();
                Cinematic.Frame();
            }

            // The fade down, the black, and back up on him.
            if (t < FadeOut)
            {
                Cinematic.Fade(Smooth(t / (float)FadeOut));
            }
            else if (t < FadeOut + FadeIn)
            {
                Cinematic.Fade(1f - Smooth((t - FadeOut) / (float)FadeIn));
            }

            // On him, leaned in, from the black until the pan home.
            if (t >= FadeOut - 2 && t < PanAt)
            {
                PisPlayer.BlendCamera(him, 1f);
                Cinematic.Lean(SceneZoom, t < FadeOut + 2 ? 1f : 0.04f);
            }

            if (!named && t >= NameAt)
            {
                named = true;
                BossIntro.PlayNameOnly(NPC.whoAmI);
            }

            if (!panned && t >= PanAt)
            {
                panned = true;
                PisPlayer.PanHome(him, PanTicks);
            }
        }

        /// <summary>Skipped: his guard, his name, the camera home at once.</summary>
        private void Skip()
        {
            Vector2 him = Feet + new Vector2(0f, -46f);

            if (!named)
            {
                named = true;
                BossIntro.PlayNameOnly(NPC.whoAmI);
            }

            if (!panned)
            {
                panned = true;
                PisPlayer.PanHome(him, 20);
            }

            Pose = KnightMoves.Guard;
            from = Pose;
            Begin();
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ============================================================
        // HIS CHANGE
        // ============================================================

        /// <summary>
        /// His change at half his life: rocked back; the blades lowered and a breath; crossed before
        /// his face; a roar of gold; the blades take fire and light; round once with the fire and
        /// light wheeling after them; and fire bursts up out of the ground about him. Nothing lands
        /// on him through it; the camera leans in, and nobody is held.
        /// </summary>
        private void ChangeTick(Player target)
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;

            KnightTrack track = KnightMoves.Change;
            float clock = Timer;
            KnightRig.Pose p = track.Sample(clock);

            Pose = clock < 6f ? KnightTrack.Between(from, p, clock / 6f) : p;

            Vector2 go = track.Travel(clock);
            float dx = (go.X - travelled.X) * facing;
            travelled = go;
            NPC.velocity.X = dx;

            foreach ((int Tick, Cue What) cue in KnightMoves.ChangeCues)
            {
                if (At(cue.Tick))
                    Cued(cue.What, target);
            }

            if (!Main.dedServ && InField(Main.LocalPlayer))
            {
                // The camera leans in on the two of them while the light gathers; nobody held.
                if (Timer > KnightMoves.ChangeCross - 20 && Timer < KnightMoves.ChangePyres + 20)
                    Cinematic.Lean(1.14f, 0.05f);
            }

            if (!Authority)
                return;

            if (At(KnightMoves.ChangeRoar + 4))
            {
                second = true;
                NPC.netUpdate = true;
            }

            if (Timer >= KnightMoves.ChangeTicks)
            {
                second = true;
                rest = 60;
                Go(Act.Stance);
            }
        }

        // ============================================================
        // HIS END
        // ============================================================

        /// <summary>The Sun, let fall: where its grip is, the way it points, how fast it falls and turns, and whether it is down.</summary>
        internal Vector2 droppedGrip;
        internal float droppedAngle;
        private Vector2 droppedVelocity;
        private float droppedSpin;
        internal bool dropped;
        private bool droppedDown;

        /// <summary>How far the gold has smouldered off him, 0 to 1, from the fade cue to his coming apart (drawing and its effects).</summary>
        public float Smoulder => State == Act.End && Timer > KnightMoves.EndFade
            ? MathHelper.Clamp((Timer - KnightMoves.EndFade) / (float)(KnightMoves.EndApart - KnightMoves.EndFade), 0f, 1f) : 0f;

        /// <summary>Come apart: nothing of him is drawn now but the golden grains it made of him.</summary>
        public bool Apart => State == Act.End && Timer >= KnightMoves.EndApart;

        /// <summary>The Sun where it fell, gone after him into grains.</summary>
        public bool SunApart => State == Act.End && Timer >= KnightMoves.EndSunApart;

        /// <summary>This machine has made the grains of him, and of the Sun.</summary>
        private bool cameApart;
        private bool sunCameApart;

        /// <summary>How far into his end's pose he starts: its first knee if a stun had him kneeling already.</summary>
        private int endFrom;

        /// <summary>
        /// His end, by request: falls to his knees first and perishes, slowly (three times as long
        /// as it first was) and without a sound. Rocked by the last blow; the Sun falls from his
        /// hand; he stumbles and drives the Grace into the ground before him; one knee, then the
        /// other, both hands on its hilt; his head bows; then, as the Guide perishes but in gold
        /// (by request), the gold smoulders off him, more and more, and he comes apart into golden
        /// grains that hold his shape a moment and drift away; the Sun in the grass after him.
        /// </summary>
        private void EndTick()
        {
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.dontTakeDamage = true;
            OutOfTheGround();

            // Killed kneeling in a stun, he does not get up to fall again: his end goes on from its
            // first knee (endFrom ticks into it); its moments after that keep their times.
            KnightTrack track = KnightMoves.End;
            float clock = Timer + endFrom;
            KnightRig.Pose p = track.Sample(clock);

            Pose = Timer < 12 ? KnightTrack.Between(from, p, Timer / 12f) : p;

            Vector2 go = track.Travel(clock) - track.Travel(endFrom);
            NPC.velocity.X = (go.X - travelled.X) * facing;
            travelled = go;

            // The Sun leaves his hand as the pose lets it go: it falls, turning, from where it was in
            // his hand a tick ago, and lies where it lands.
            if (!dropped && Pose.SunIn == KnightRig.Dropped)
            {
                KnightRig.Frame frame = KnightRig.Build(before.SunIn == KnightRig.Dropped ? from : before, Feet, facing, null, SunSwing, GraceSwing);
                dropped = true;
                droppedGrip = frame.SunGrip;
                droppedAngle = frame.SunBlade;

                // As slowly as the rest of his end goes.
                droppedVelocity = new Vector2(facing * 0.5f, -0.6f);
                droppedSpin = facing * 0.045f;
            }

            if (dropped && !droppedDown)
            {
                droppedVelocity.Y += 0.1f;
                droppedGrip += droppedVelocity;
                droppedAngle += droppedSpin;

                // Down when its grip is near the ground: laid flat, ringing once.
                if (droppedGrip.Y >= Feet.Y - 4f)
                {
                    droppedGrip.Y = Feet.Y - 4f;
                    droppedAngle = facing > 0 ? 0.04f : MathHelper.Pi - 0.04f;
                    droppedDown = true;

                    if (!Main.dedServ)
                        KnightFx.SwordDown(droppedGrip, droppedAngle);
                }
            }

            foreach ((int Tick, Cue What) cue in KnightMoves.EndCues)
            {
                // The pose's moments by the pose's clock (from where a stun left him); the gold's by his own.
                int at = cue.Tick < KnightMoves.EndFade ? cue.Tick - endFrom : cue.Tick;

                if (at >= 0 && At(at))
                    Cued(cue.What, null);
            }

            if (!Main.dedServ)
            {
                float smoulder = Smoulder;

                if (smoulder > 0f && !Apart)
                    KnightFx.Smouldering(this, smoulder);

                if (InField(Main.LocalPlayer) || Vector2.Distance(Main.LocalPlayer.Center, NPC.Center) < 1400f)
                {
                    // The camera goes to him and leans in as he kneels and comes apart.
                    if (Timer > KnightMoves.EndPlant && Timer < KnightMoves.EndTicks - 30)
                    {
                        float w = Smooth((Timer - KnightMoves.EndPlant) / 180f) * 0.7f;
                        PisPlayer.BlendCamera(Feet + new Vector2(0f, -30f), w);
                        Cinematic.Lean(1.2f, 0.012f);
                        Cinematic.Frame();
                    }

                    if (At(KnightMoves.EndTicks - 30))
                        PisPlayer.PanHome(Feet + new Vector2(0f, -30f), 120);
                }

                // Once each on this machine, whenever it gets there (a client put a long way forward
                // by the server still sees him come apart).
                if (Timer >= KnightMoves.EndApart && !cameApart)
                {
                    cameApart = true;
                    CameApart();
                }

                if (Timer >= KnightMoves.EndSunApart && !sunCameApart)
                {
                    sunCameApart = true;
                    SunComesApart();
                }
            }

            if (Authority && Timer >= KnightMoves.EndTicks)
                Perish();
        }

        /// <summary>Gone, where the world is decided: the kill counted, his loot dropped, the world told.</summary>
        private void Perish()
        {
            // A boss as he dies (the bar went when he came apart): the game's potions and its word that he is beaten.
            NPC.boss = true;
            gone = true;
            NPC.dontTakeDamage = false;
            NPC.life = 0;
            NPC.checkDead();

            // checkDead sends nothing itself; this is how vanilla's own despawn tells the clients.
            if (Main.netMode == NetmodeID.Server && !NPC.active)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }
    }
}
