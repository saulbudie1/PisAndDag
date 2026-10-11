using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The passage into his third phase: what he does when three tenths of his life are
    /// left, on every machine, and what the player he fights is shown and asked, on their
    /// own machine only. And what is his third phase's own after it. By request:
    ///
    ///   SPENT     whatever he was doing, he stops, and the picture goes to black (as at
    ///             two thirds: Foigh.Passage).
    ///   SHUDDER   under the black the player is stood sixteen tiles off him, and the
    ///             camera put on him. The picture comes back on him: on his feet, bent
    ///             over his sword, which is in the ground before him, both hands on its
    ///             handle, all of him shaking. He coughs blood twice, raises his head,
    ///             and says a line, which has its one answer.
    ///   GUT       he says nothing. The sword out of the ground, up at the sky in both
    ///             hands, and held; tipped back over his shoulder and let forward through
    ///             his hands until he has it by the blade, its point at himself; and
    ///             through him, to the guard.
    ///   BLED      torn out again, up over him, and flicked down: the blood leaves the
    ///             blade along the way each part of it is going. From here the sword is
    ///             bloodied. He comes up, looks along it, and says a second line; lets it
    ///             down, looks up at them, and says a third. Both go by themselves.
    ///   SEVER     a flash step to behind them. His bottom hand closes on their wings and
    ///             pulls; the sword comes down between his hand and their back, into the
    ///             ground to its guard; the wings are in his hand, and nobody flies from
    ///             that tick on. He leaves the sword standing, comes away from them a
    ///             step, and looks at what he has. A fourth line, by itself.
    ///   RESUME    the picture to black at once (as at two thirds), what he had in his
    ///             hand let fall. Under it he is back where he stood, the sword in his
    ///             hand, and it has taken: black, and red in it. The picture comes back
    ///             on the player, and the fight goes on.
    ///
    /// Nothing lands on him from the first tick of it to the last, and the player is held
    /// for as long. The server waits at Shudder, at Bled and at Sever for the player's
    /// word, and not for ever. Only the player he fights is moved and shown it; to anybody
    /// else he is simply doing it.
    ///
    /// HIS THIRD PHASE, by request: every blow of the sword leaves a wound (Ultra
    /// Bleeding: three at the most, 12 life a second each, and no life coming back by
    /// itself; five seconds from the last blow); nobody flies while he is there (wings, a
    /// booster and flying mounts do nothing, and wings are not drawn); and the sword
    /// smokes black and red, every cut of it drawn in those. What he fights with is his
    /// second phase's, and one more, by request ("he can also throw a flying slash
    /// projectile", as "a move set"):
    ///
    ///   REND      three cuts where he stands, the blade flaring first: down, up, and
    ///             down in both hands. Each is let go of as it is made and goes on
    ///             through the air (FoighSlash), at them, wherever they are ("the slash
    ///             is multi directional so if ur on the ceiling it also hits you"): the
    ///             third the largest. Before each he looks for them again.
    ///
    /// His second line and his fourth are the user's own words, kept as written; the first
    /// and the third were offered and chosen. The bars in them are Dialogue.Pause.
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // HIS LINES
        // ============================================================

        private static readonly string[] ThirdLines =
        {
            "Ahh|.|.|. blood.| Mine.| I had forgotten the taste of it.",
            "The muscle hardens to rust|,| the sword bleeds with the body",
            "It is fairer so.",
            "Futile devices",
        };

        // ============================================================
        // THE PASSAGE'S TICKS
        // ============================================================

        /// <summary>How far off him the player is stood, under the black: sixteen tiles; and, where there is no ground that far off on either side of him, no nearer than this.</summary>
        public const float StandOffAt = 16f * 16f;
        public const float StandOffLeast = 8f * 16f;

        /// <summary>
        /// Shudder: the tick of the black the player is moved on; the tick the picture
        /// begins to come back, and how long it takes; the tick of his first line; and,
        /// that line never answered, when he goes on by himself.
        /// </summary>
        private const int ShudderCut = 3;
        private const int ShudderBlack = 16;
        private const int ShudderFadeIn = 30;
        private const int ShudderSpeaks = M.ShudderLooked + 8;
        private const int ShudderLongest = ShudderSpeaks + 5400;

        /// <summary>
        /// Bled: the tick his second line begins, and how long it stays once all of it is
        /// there; his third no sooner than this (he has looked up at them by then), and
        /// how long that one stays; and, neither ever read, when he goes on by himself.
        /// </summary>
        private const int BledSpeaks = M.BledRises + 10;
        private const int BledLinger = 70;
        private const int BledThird = M.BledLooks + 6;
        private const int WeighedLinger = 60;
        private const int BledLongest = M.BledLooks + 900;

        /// <summary>Sever: the tick his fourth line begins, how long it stays once all of it is there, and when he goes on by himself.</summary>
        private const int SeverSpeaks = M.SeverHolds + 6;
        private const int SeverLinger = 45;
        private const int SeverLongest = SeverSpeaks + 600;

        /// <summary>The lean on him while he shakes and bleeds; closer while the sword is in him; and closest behind them.</summary>
        private const float ThirdZoom = 1.3f;
        private const float GutZoom = 1.42f;
        private const float SeverZoom = 1.5f;

        /// <summary>Behind them: this far from their middle to his (his hand is then on their wings a hand behind their back, and there is room between for the sword to come down). And beside them is no further than this, and about level with them.</summary>
        private const float SeverGap = 42f;
        private const float SeverReach = SeverGap + 18f;
        private const float SeverLevel = 20f;

        /// <summary>How far he pulls them back by their wings, and how far over it tips them.</summary>
        private const float PullBack = 4f;
        private const float PullTilt = 0.2f;

        /// <summary>The light up the blade while he holds it at the sky: when it sets off.</summary>
        private const int GutGleamAt = M.GutHigh + 4;

        // ============================================================
        // WHERE THE WORLD IS DECIDED
        // ============================================================

        /// <summary>
        /// Black by now, the server's: what he had thrown is gone with the picture, the
        /// sword is in his hands, and he is bent over it.
        /// </summary>
        private void Brace()
        {
            Gather();
            FaceThem(Target());
            Go(Move.Shudder);
        }

        /// <summary>What he had thrown is gone, and the sword is in his hands: the server's, under the black of either passage.</summary>
        private void Gather()
        {
            ThrownSword()?.Kill();

            int knife = ModContent.ProjectileType<FoighDagger>();
            int back = ModContent.ProjectileType<FoighReturned>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if ((projectile.type == knife && (int)projectile.ai[0] == NPC.whoAmI) || (projectile.type == back && (int)projectile.ai[1] == NPC.whoAmI))
                    projectile.Kill();
            }

            hasSword = true;
        }

        /// <summary>
        /// To behind them, the server's: past them, where their back is; or, with nowhere
        /// to stand there, beside them on his own side (their own machine turns their back
        /// to him wherever he is: FoighScenePlayer.FaceAway). With nowhere beside them at
        /// all, from where he stands. Where he stood is kept: he is back there when the
        /// picture comes back.
        /// </summary>
        private void StepToSever(Player target)
        {
            anchor = Feet;

            if (target == null || target.dead)
            {
                Go(Move.Sever);
                return;
            }

            int side = target.Center.X >= NPC.Center.X ? 1 : -1;

            for (int turn = 0; turn < 2; turn++)
            {
                if (Stand(target.Center.X + side * SeverGap, target.Bottom.Y, out Vector2 feet) && Beside(feet, target))
                {
                    FlashTo(feet, Move.Sever);
                    return;
                }

                side = -side;
            }

            Go(Move.Sever);
        }

        /// <summary>Whether, standing here, he is beside this player: near enough for his hand to be on their back, not on top of them, and about level with them.</summary>
        private static bool Beside(Vector2 feet, Player player)
        {
            float off = Math.Abs(feet.X - player.Center.X);

            return off >= 24f && off <= SeverReach && Math.Abs(feet.Y - player.Bottom.Y) <= SeverLevel;
        }

        /// <summary>Under the black at the end of it, every machine: he is where he stood before he went behind them.</summary>
        private void Return()
        {
            NPC.position = new Vector2(anchor.X - NPC.width * 0.5f, anchor.Y - NPC.height);
            NPC.velocity = Vector2.Zero;

            if (!Authority)
                return;

            FaceThem(Target());
            NPC.netUpdate = true;
        }

        // ============================================================
        // EVERY MACHINE
        // ============================================================

        private void Shudder(Player target)
        {
            Fall();
            NPC.velocity.X = 0f;

            if (!Authority)
                return;

            // Under the black they are stood somewhere else: he is turned to where they are
            // now, for as long as nobody can see him.
            if (now <= ShudderBlack)
                FaceThem(target);

            if (now >= ShudderLongest)
                Go(Move.Gut);
        }

        private void Gut()
        {
            Fall();
            NPC.velocity.X = 0f;

            if (Authority && now >= M.GutTicks)
                Go(Move.Bled);
        }

        private void Bled(Player target)
        {
            Fall();
            NPC.velocity.X = 0f;

            if (Authority && now >= BledLongest)
                StepToSever(target);
        }

        private void Sever()
        {
            Fall();

            // With what he has cut off them in his hand he comes away from them a step.
            NPC.velocity.X = Stepped(M.SeverSteps);

            // Where he stood for the cut, on this machine (the sword's point stays there).
            if (now <= M.SeverCuts + 2)
            {
                cutFeet = Feet.X;
                cutSeenHere = true;
            }

            if (Authority && now >= SeverLongest)
                Go(Move.Resume);
        }

        /// <summary>Where he stood for the cut, as this machine saw it.</summary>
        private float cutFeet;
        private bool cutSeenHere;

        /// <summary>
        /// The sword where he left it standing, behind them, as it is to be drawn: where
        /// his hand had it the moment before he let go, from where he stood for the cut
        /// (as this machine saw that; or, not seen here, where his step away is written
        /// to have begun). It stands until the picture has gone; under the black it is in
        /// his hand again.
        /// </summary>
        private bool Left(List<FoighRig.Part> scratch, out FoighRig.Part sword)
        {
            sword = default;

            bool stands = (State == Move.Sever && now > M.SeverLeaves)
                || (State == Move.Resume && phase >= 3 && leaving != null && now < FadeOutTicks);

            if (!stands)
                return false;

            float stood = cutSeenHere ? cutFeet : Feet.X - facing * M.Advance(M.SeverSteps, State == Move.Sever ? now : M.SeverHolds);

            scratch.Clear();
            FoighRig.Build(M.SeverLeft, new Vector2(stood, Feet.Y), facing, scratch);

            foreach (FoighRig.Part part in scratch)
            {
                if (part.Piece == FoighRig.Sword)
                {
                    sword = part;
                    return true;
                }
            }

            return false;
        }

        // ============================================================
        // THE PLAYER IT IS FOR
        // ============================================================

        /// <summary>Which of his four lines is up on this machine's panel, from 1; none at 0. And the tick of the move he is in at which all of it was there; less than none before it is.</summary>
        private int thirdUp;
        private int thirdRead = -1;

        private bool stood;
        private bool witnessed;
        private bool secondRead;
        private bool weighed;
        private bool shorn;

        /// <summary>This machine's player is being pulled back by their wings, and where their middle was when the hand closed.</summary>
        private bool pulled;
        private Vector2 pulledFrom;

        /// <summary>The first tick this machine sees of the passage: nothing of it has been shown yet.</summary>
        private void ThirdBegins()
        {
            thirdUp = 0;
            thirdRead = -1;
            stood = false;
            witnessed = false;
            secondRead = false;
            weighed = false;
            shorn = false;
            pulled = false;
            wingsHeld = -1;
            cutSeenHere = false;
        }

        /// <summary>The scene for the player it is for, every tick of the four parts that are this passage's own (and of his step behind them), on their machine.</summary>
        private void ThirdScene(Player me, out bool wantShot)
        {
            wantShot = true;

            Cinematic.Hold();
            Cinematic.Frame();

            switch (State)
            {
                case Move.Shudder:
                    FoighScenePlayer.FaceToward(NPC);
                    Cinematic.Lean(ThirdZoom, 0.03f);

                    if (now < ShudderBlack)
                        Cinematic.Fade(1f);
                    else if (now < ShudderBlack + ShudderFadeIn)
                        Cinematic.Fade(1f - Smooth((now - ShudderBlack) / (float)ShudderFadeIn));

                    // Under the black: the player stood off him, and the camera on him
                    // already, so that it is on him the picture comes back. (One who became
                    // his after the picture was back is left where they stand, and the
                    // camera goes over to him as it does at two thirds.)
                    if (!stood && now >= ShudderCut)
                    {
                        stood = true;

                        if (now < ShudderBlack)
                        {
                            shot = 1f;
                            me.GetModPlayer<FoighScenePlayer>().PutOff(NPC, StandOffAt, StandOffLeast);
                        }
                    }

                    ShudderTalk();
                    break;

                case Move.Gut:
                    FoighScenePlayer.FaceToward(NPC);
                    Cinematic.Lean(now >= M.GutTurned ? GutZoom : ThirdZoom, 0.03f);

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();
                    break;

                case Move.Bled:
                    FoighScenePlayer.FaceToward(NPC);
                    Cinematic.Lean(now < M.BledRises ? GutZoom : ThirdZoom, 0.03f);
                    BledTalk();
                    break;

                // Gone from where he stood to behind them: they have not turned.
                case Move.Flash:
                    Cinematic.Lean(SeverZoom, 0.08f);

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();
                    break;

                case Move.Sever:
                    FoighScenePlayer.FaceAway(NPC);
                    Cinematic.Lean(SeverZoom, 0.08f);
                    Pull(me);
                    SeverTalk();
                    break;
            }
        }

        /// <summary>
        /// His first line, with its one answer, which tells the server. Its box gone down
        /// under him (something else took it), it comes back.
        /// </summary>
        private void ShudderTalk()
        {
            if (witnessed || now < ShudderSpeaks)
                return;

            if (thirdUp == 1 && Dialogue.Speaking(NPC))
                return;

            // They have been fighting him: his face and his name are theirs already.
            DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

            thirdUp = 1;

            Dialogue.Converse(NPC, ThirdLines[0], Pace, new TalkOption(Silence, _ =>
            {
                witnessed = true;
                Dialogue.Close();
                Tell(Word.Witnessed);
            })
            { Remembered = false });
        }

        /// <summary>
        /// One of his lines with nothing under it: put up (and again, its box gone down
        /// under him before it was read), and true once all of it has been there for this
        /// long.
        /// </summary>
        private bool ThirdSaid(int which, int linger)
        {
            if (thirdUp != which || !Dialogue.Speaking(NPC))
            {
                DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

                thirdUp = which;
                thirdRead = -1;

                Dialogue.Converse(NPC, ThirdLines[which - 1], Pace);
                return false;
            }

            if (thirdRead < 0)
            {
                if (Dialogue.LineDone)
                    thirdRead = now;

                return false;
            }

            return now - thirdRead >= linger;
        }

        /// <summary>His second line, and, that one read and his head up, his third: then the server is told.</summary>
        private void BledTalk()
        {
            if (weighed || now < BledSpeaks)
                return;

            if (!secondRead)
            {
                if (!ThirdSaid(2, BledLinger))
                    return;

                secondRead = true;
            }

            if (now < BledThird || !ThirdSaid(3, WeighedLinger))
                return;

            weighed = true;
            Dialogue.Close();
            Tell(Word.Weighed);
        }

        /// <summary>His fourth line; read, the server is told, and the picture goes.</summary>
        private void SeverTalk()
        {
            if (shorn || now < SeverSpeaks || !ThirdSaid(4, SeverLinger))
                return;

            shorn = true;
            Dialogue.Close();
            Tell(Word.Shorn);
        }

        /// <summary>Whether he is beside this player: near enough for his hand to be on their back.</summary>
        private bool Reaches(Player player)
        {
            return Math.Abs(NPC.Center.X - player.Center.X) <= SeverReach && Math.Abs(NPC.Bottom.Y - player.Bottom.Y) <= SeverLevel + 4f;
        }

        /// <summary>
        /// This machine's player, from the tick his hand closes on their wings to the cut:
        /// pulled back toward him a little and tipped over backward (FoighGripPlayer), and
        /// let go of as the sword comes down.
        /// </summary>
        private void Pull(Player me)
        {
            FoighGripPlayer grip = me.GetModPlayer<FoighGripPlayer>();

            if (now >= M.SeverCuts)
            {
                if (pulled)
                {
                    pulled = false;
                    grip.LetGo(NPC.whoAmI, Vector2.Zero);
                }

                return;
            }

            if (now < M.SeverGrabs || !Reaches(me))
                return;

            if (!pulled)
            {
                pulled = true;
                pulledFrom = me.Center;
            }

            float t = Easing.Smoother(MathHelper.Clamp((now - M.SeverGrabs) / (float)(M.SeverPulls - M.SeverGrabs), 0f, 1f));
            int toward = NPC.Center.X >= me.Center.X ? 1 : -1;

            grip.Hold(NPC.whoAmI, pulledFrom + new Vector2(toward * PullBack * t, 0f), toward * PullTilt * t);
        }

        // ============================================================
        // WHAT IT LOOKS AND SOUNDS LIKE
        // ============================================================

        /// <summary>Blood: wet, and where it has lain a while.</summary>
        private static readonly Color Blood = new Color(150, 8, 12);
        private static readonly Color Clot = new Color(112, 6, 10);

        /// <summary>The black off the sword, and the red in it. (His cuts let go of are drawn in the same: FoighSlash.)</summary>
        internal static readonly Color Soot = new Color(12, 6, 8);
        internal static readonly Color Cinder = new Color(255, 96, 60);
        internal static readonly Color BurnRed = new Color(206, 22, 28);
        internal static readonly Color BurnHot = new Color(255, 186, 150);

        /// <summary>The line the bloodied sword's point leaves along its way.</summary>
        internal static readonly Color TrailRed = new Color(255, 72, 58);

        /// <summary>How much of the sword's bloodied side is shown over its sharp one, 0 to 1; and how much of the black and red is on it, 0 to 1.</summary>
        private float bloodShown;
        private float flame;

        /// <summary>The sword is bloodied, as it is drawn on this machine; and it burns.</summary>
        public bool Bloodied => bloodShown >= 1f;
        public bool Burning => flame > 0.5f;

        /// <summary>
        /// Every tick, every machine: the sword is bloodied by going through him (it
        /// comes on over the ticks that takes, and what is out of his back is red by the
        /// time it is in to the guard); and it burns from under the black at the end of
        /// the passage, for as long as he is there.
        /// </summary>
        private void ShowBlood()
        {
            bool bloody = phase >= 3 && State != Move.Spent && State != Move.Shudder;

            bloodShown = !bloody ? 0f
                : State == Move.Gut ? MathHelper.Clamp((now - M.GutPoised) / (float)(M.GutThrough - M.GutPoised), 0f, 1f)
                : 1f;

            bool lit = HasCut && State != Move.Sever && !(State == Move.Resume && now < FadeOutTicks + BlackTicks / 2);

            flame = Toward(flame, lit ? 1f : 0f, 0.1f);
        }

        /// <summary>The blade swung with nothing to cut, in the passage: seen as a cut is (its trail), and no more than that.</summary>
        private bool Sweeps => (State == Move.Bled && clock > M.BledOut && clock <= M.BledFlick + 1f)
            || (State == Move.Sever && clock > M.SeverUp && clock <= M.SeverCuts + 1f);

        /// <summary>
        /// A place on the sword as it is held this tick, this many pixels from its guard
        /// toward its point (the other way is less than nothing). Through him, its guard
        /// is at the middle of him: the front of him is a little short of that, and his
        /// back a little past it.
        /// </summary>
        private Vector2 Along(float pixels)
        {
            Vector2 way = (frame.Tip - frame.Guard).SafeNormalize(new Vector2(-facing, 0f));

            return frame.Guard + way * pixels;
        }

        private Vector2 Belly => Along(-8f);
        private Vector2 Spine => Along(10f);

        /// <summary>The way back from the passage, from the pose the cut left him in: as he stood until the picture has gone, and in his stance under the black. None on a machine that did not see that.</summary>
        private FoighTrack leaving;

        /// <summary>
        /// One drop of blood from here, thrown so: it lives until it is at the ground he
        /// stands on and no longer, so that none falls through it; and, if asked, what is
        /// left of it lies there a while.
        /// </summary>
        private void Drop(Vector2 from, Vector2 velocity, float size, bool stain)
        {
            float ground = Feet.Y;

            // As the droplet will go (BloodDroplet.Update): how many ticks to the ground.
            Vector2 at = from;
            Vector2 going = velocity;
            int life = 0;

            while (at.Y < ground && life < 90)
            {
                at += going;
                going.X *= 0.965f;
                going.Y = MathHelper.Clamp(going.Y + 0.42f, -18f, 18f);
                life++;
            }

            // (One that would be at the ground within the tick is not seen falling at all:
            // a drop is drawn first where its first tick has taken it.)
            if (life >= 2)
                PADParticleHandler.SpawnParticle(new BloodDroplet(from, velocity, life, size, Blood));

            if (stain)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(new Vector2(at.X, ground - 1f), Vector2.Zero,
                    Main.rand.Next(150, 230), Main.rand.NextFloat(0.4f, 0.7f), Clot, false));
            }
        }

        /// <summary>A few drops let fall from about here.</summary>
        private void Drip(Vector2 from, int drops)
        {
            for (int i = 0; i < drops; i++)
            {
                Drop(from + Main.rand.NextVector2Circular(2f, 2f), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(0.2f, 0.9f)),
                    Main.rand.NextFloat(0.45f, 0.8f), true);
            }
        }

        /// <summary>
        /// Blood out of something, about this way: most of it and the fastest down the
        /// middle of the cone, as a jet is. None of it falls through the ground.
        /// </summary>
        private void Burst(Vector2 from, Vector2 toward, int drops, float spreadDegrees)
        {
            Vector2 way = toward.SafeNormalize(Vector2.UnitY);
            float spread = MathHelper.ToRadians(spreadDegrees);

            for (int i = 0; i < drops; i++)
            {
                float off = Main.rand.NextFloat(-spread, spread);
                float speed = MathHelper.Lerp(1.5f, 7f, 1f - Math.Abs(off) / spread) * Main.rand.NextFloat(0.7f, 1.2f);

                Drop(from, way.RotatedBy(off) * speed, Main.rand.NextFloat(0.5f, 0.95f), i % 3 == 0);
            }
        }

        /// <summary>
        /// The blood off the blade as it is swung, this tick: from places along it, each
        /// thrown the way that place of the blade is going (from where it was a tick ago
        /// to where it is), so that it leaves the swing along its tangents.
        /// </summary>
        private void Fling(int drops)
        {
            FoighRig.Frame before = FoighRig.Build(PoseOf(State, clockWas), feetWas, facing);

            for (int i = 0; i < drops; i++)
            {
                float along = Main.rand.NextFloat(0.25f, 1f);

                Vector2 from = Vector2.Lerp(before.Guard, before.Tip, along);
                Vector2 to = Vector2.Lerp(frame.Guard, frame.Tip, along);
                Vector2 going = to - from;

                if (going.LengthSquared() < 4f)
                    continue;

                Drop(Vector2.Lerp(from, to, Main.rand.NextFloat()), going * Main.rand.NextFloat(0.22f, 0.5f) + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Main.rand.NextFloat(0.5f, 0.95f), i % 3 == 0);
            }
        }

        /// <summary>The passage's blood, dust, sparks and sounds, this tick of it (Looks asks, on a machine with a screen).</summary>
        private void ThirdLooks()
        {
            Vector2 feet = Feet;

            switch (State)
            {
                case Move.Shudder:
                    for (int i = 0; i < M.Coughs.Length; i++)
                    {
                        if (At(M.Coughs[i]))
                            SpitBlood(i == M.Coughs.Length - 1 ? 12 : 7);
                    }

                    // Off his chin, between.
                    if (now > ShudderBlack && now % 23 == 5)
                        Drip(frame.MouthAt, 1);
                    break;

                case Move.Gut:
                {
                    Vector2 where = new Vector2(feet.X + facing * M.ShudderEnters, feet.Y);

                    // He sets himself over it and heaves: it grinds in the ground, and comes a little.
                    if (At(M.GutSets)) Sound(SoundID.Tink, frame.Grip, -0.8f, 0.5f);

                    if (now > M.GutSets && now < M.GutLoose && now % 5 == 0)
                        Puff(where, 1, 1.2f, 0.6f);

                    if (now <= M.GutLoose)
                        furrow = -1;

                    // It gives.
                    if (At(M.GutLoose))
                    {
                        Sound(SoundID.Dig, where, -0.6f, 0.9f);
                        Sound(SoundID.Tink, where, -0.7f, 0.6f);
                        Puff(where, 8, 2.4f);
                        Quake(3f, 8);
                    }

                    // Torn out forward through the ground; and where its point comes up out of it.
                    if (now > M.GutLoose && now < M.GutOut && InGround(out Vector2 cut))
                        Plough(cut, false, where.X);

                    if (At(M.GutOut))
                        Plough(new Vector2(feet.X + facing * M.GutBreaks, feet.Y), true, where.X);

                    // At the sky.
                    if (At(M.GutHigh)) Glint(frame.Tip, Steel, 34f, 18);
                    if (At(GutGleamAt + 2)) Sound(SoundID.Item37, frame.Guard, 0.5f, 0.45f);
                    if (At(GutGleamAt + GleamTicks - 2)) Glint(frame.Tip, Color.White, 36f, 16);

                    // Tipped back over his shoulder, and let forward through his hands: the
                    // air it goes through, the steel through his grip, and nothing said.
                    if (At(M.GutHeld + 4)) Sound(SoundID.Item1, NPC.Center, -0.75f, 0.7f);
                    if (At(M.GutTips)) Sound(SoundID.Item71, frame.NearFist, 0.6f, 0.45f);

                    // He has it by the blade from here: it opens his hands.
                    if (now > M.GutTips && now <= M.GutThrough && now % 5 == 0)
                        Drip(frame.NearFist, 1);

                    if (At(M.GutTurned)) Sound(SoundID.Tink, frame.NearFist, -0.4f, 0.35f);
                    if (At(M.GutPoised - 4)) Glint(frame.EyeAt, Eye, 26f, 12);

                    // Through him.
                    if (At(M.GutThrough - 2))
                    {
                        Sound(SoundID.DD2_GhastlyGlaivePierce, NPC.Center, -0.45f, 1f);
                        Sound(SoundID.NPCHit1, NPC.Center, -0.6f, 0.9f);
                    }

                    if (At(M.GutThrough))
                    {
                        Burst(Belly, new Vector2(facing, -0.2f), 8, 34f);
                        Burst(Spine, new Vector2(-facing, -0.25f), 18, 30f);
                        Sound(SoundID.NPCDeath1, NPC.Center, -0.6f, 0.5f);
                        Quake(7f, 16);

                        if (shot > 0f || Near())
                            Cinematic.Flash(new Color(170, 16, 20), 0.14f);
                    }

                    // And held there: it runs down the blade behind him and off it, and out of his mouth.
                    if (now > M.GutThrough && now % 4 == 0)
                        Drip(Along(Main.rand.NextFloat(12f, FoighRig.GripToTip - FoighRig.GripToGuard)), 1);

                    if (now > M.GutThrough && now % 17 == 3)
                        Drip(frame.MouthAt, 1);
                    break;
                }

                case Move.Bled:
                    // He sets himself against it.
                    if (At(M.BledSets)) Sound(SoundID.NPCHit1, NPC.Center, -0.7f, 0.5f);
                    if (At(M.BledOut - 4)) Sound(SoundID.DD2_GhastlyGlaivePierce, NPC.Center, -0.2f, 0.9f);

                    // And it is out of him.
                    if (At(M.BledOut))
                    {
                        Burst(NPC.Center + new Vector2(facing * 8f, -2f), new Vector2(facing, -0.35f), 20, 36f);
                        Burst(NPC.Center + new Vector2(-facing * 8f, -4f), new Vector2(-facing, 0.1f), 8, 40f);
                        Sound(SoundID.NPCHit1, NPC.Center, -0.4f, 1f);
                        Quake(5f, 12);
                    }

                    // Up over him, and flicked down: the blood leaves it the way each part of the blade is going.
                    if (At(M.BledOut + 2)) Sound(SoundID.Item71, NPC.Center, -0.3f, 1f);

                    if (now > M.BledOut && now <= M.BledFlick + 1)
                        Fling(now > M.BledUp ? 9 : 5);

                    // What is left on it leaves it where it stops.
                    if (At(M.BledFlick))
                    {
                        Sound(SoundID.Item1, frame.Tip, 0.3f, 0.9f);

                        for (int i = 0; i < 14; i++)
                        {
                            Drop(Vector2.Lerp(frame.Guard, frame.Tip, Main.rand.NextFloat(0.3f, 1f)),
                                new Vector2(facing * Main.rand.NextFloat(2.5f, 9f), Main.rand.NextFloat(-1.5f, 3.5f)), Main.rand.NextFloat(0.5f, 0.95f), i % 2 == 0);
                        }
                    }

                    // What still runs off the blade, and out of him.
                    if (now > M.BledFlick && now % 9 == 0)
                        Drip(OnBlade(Main.rand.NextFloat(0.2f, 1f)), 1);

                    if (now > M.BledOut && now % 27 == 7)
                        Drip(NPC.Center + new Vector2(facing * 6f, 0f), 1);
                    break;

                case Move.Sever:
                    // The hand closes.
                    if (At(M.SeverGrabs))
                    {
                        Sound(SoundID.Item1, frame.FarFist, -0.5f, 0.9f);
                        PADParticleHandler.SpawnParticle(new ParryRing(frame.FarFist, 6f, 36f, 9, Steel * 0.3f));
                    }

                    if (At(M.SeverUp - 5)) Sound(SoundID.Item71, NPC.Center, -0.5f, 0.8f);

                    if (At(M.SeverCuts))
                        Shear();

                    // What he has in his hand drips.
                    if (now > M.SeverCuts && now % 8 == 0 && WingsHeld() > 0)
                        Drip(frame.FarFist + new Vector2(0f, 8f), 1);
                    break;

                // The way back from it: under the black the sword is in his hand again, and it takes.
                case Move.Resume when phase >= 3:
                    if (leaving != null && At(FadeOutTicks + 2))
                    {
                        Sound(SoundID.Dig, NPC.Center, -0.6f, 0.7f);
                        Sound(SoundID.Item71, NPC.Center, -0.5f, 0.8f);
                    }

                    if (At(FadeOutTicks + BlackTicks / 2))
                        Sound(SoundID.DD2_BetsyFlameBreath, NPC.Center, -0.5f, 0.6f);

                    if (At(FadeOutTicks + BlackTicks + 5))
                        Kindle();
                    break;
            }
        }

        /// <summary>
        /// The cut, on a machine with a screen: steel, the blood off their back, the mark
        /// of it in the air, and where the sword has gone into the ground.
        /// </summary>
        private void Shear()
        {
            Player target = Target();
            bool beside = target != null && Reaches(target);

            // Their back, at about their shoulders, on the side of them he is on.
            Vector2 at = beside
                ? new Vector2(target.Center.X + (NPC.Center.X >= target.Center.X ? 9f : -9f), target.Center.Y - 8f)
                : frame.FarFist;

            Sound(SoundID.Item71, at, -0.2f, 1f);
            Sound(SoundID.DD2_GhastlyGlaivePierce, at, -0.1f, 0.9f);
            Sound(SoundID.NPCHit18, at, -0.3f, 0.7f);

            Burst(at, new Vector2(-facing * 0.3f, -1f), 14, 60f);
            BloodSlash.Pair(at, new Vector2(facing * 0.36f, 1f), 62f, 6f, 20);

            // Into the ground before him: steel on stone, and little of the ground thrown
            // up (what he has in his hand is to be seen).
            Vector2 down = new Vector2(frame.Tip.X, Feet.Y);

            Sound(SoundID.DD2_MonkStaffGroundImpact, down, -0.3f, 0.75f);
            SparkShard.Spray(down + new Vector2(0f, -4f), Steel, 9, 6f, 24f, 16, falls: true, palette: Sparks);
            Puff(down, 4, 1.8f, 0.9f);
            Quake(6f, 12);

            if (shot > 0f || Near())
                Cinematic.Flash(new Color(170, 16, 20), 0.1f);
        }

        /// <summary>
        /// The sword has taken, as the picture comes back: cuts hanging in the air about
        /// him, red and black, crossing; a red haze low down; and what it throws off.
        /// </summary>
        private void Kindle()
        {
            Vector2 middle = NPC.Center + new Vector2(0f, -10f);

            Sound(SoundID.Item74, middle, -0.6f, 0.55f);
            Sound(SoundID.Item71, middle, -0.6f, 0.9f);

            for (int i = 0; i < 7; i++)
            {
                // Each through him or near him, at its own slant; none of them level, none upright.
                float slant = MathHelper.ToRadians(Main.rand.NextFloat(14f, 66f)) * (i % 2 == 0 ? 1f : -1f);
                Vector2 through = middle + new Vector2(Main.rand.NextFloat(-34f, 34f), Main.rand.NextFloat(-30f, 22f));

                BloodSlash.Pair(through, new Vector2((float)Math.Cos(slant), (float)Math.Sin(slant)), Main.rand.NextFloat(86f, 168f), Main.rand.NextFloat(4f, 7f), Main.rand.Next(20, 34));
            }

            Haze(Feet, 12, 3.2f);

            for (int i = 0; i < 16; i++)
            {
                PADParticleHandler.SpawnParticle(new Ember(middle + Main.rand.NextVector2Circular(22f, 26f),
                    new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(0.6f, 3.4f)), Cinder, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(22, 40)));
            }

            if (Near())
                Cinematic.Flash(new Color(150, 12, 16), 0.1f);

            Quake(4f, 10);
        }

        /// <summary>A red haze off the ground about this place, going out along it both ways, and black in it.</summary>
        private static void Haze(Vector2 at, int count, float push)
        {
            for (int i = 0; i < count; i++)
            {
                float side = Main.rand.NextBool() ? -1f : 1f;
                Vector2 from = at + new Vector2(Main.rand.NextFloat(-14f, 14f), -Main.rand.NextFloat(2f, 12f));

                PADParticleHandler.SpawnParticle(new BloodFume(from, new Vector2(side * Main.rand.NextFloat(0.4f, push), -Main.rand.NextFloat(0.05f, 0.5f)),
                    Main.rand.NextFloat(0.14f, 0.26f), Main.rand.Next(28, 48)));

                if (i % 3 == 0)
                {
                    PADParticleHandler.SpawnParticle(new BurnSmoke(from, new Vector2(side * Main.rand.NextFloat(0.3f, push * 0.7f), -Main.rand.NextFloat(0.2f, 0.9f)),
                        Soot, BurnRed, Main.rand.Next(34, 54), Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(34f, 50f), Main.rand.NextFloat(0.75f, 1f)));
                }
            }
        }

        // ============================================================
        // HIS THIRD PHASE'S OWN
        // ============================================================

        /// <summary>A blow of his sword has landed on this machine's player: in his third phase it leaves a wound.</summary>
        private void Wound(Player me)
        {
            if (phase >= 3 && !me.dead)
                UltraBleeding.Wound(me);
        }

        // ---- the three cuts, each let go of ----

        /// <summary>
        /// How likely he is to begin them, beside what else he may pick (Choose): up
        /// close; and from further off, wherever they are, over his head with it (each
        /// is thrown at them). And how far off he will begin them from.
        /// </summary>
        private const float RendNear = 1.5f;
        private const float RendFar = 3.5f;
        private const float RendReach = 800f;

        /// <summary>
        /// Where each leaves him, from the ground between his feet. The first this far
        /// ahead (the middle of the blade as it comes through level) and this high. The
        /// second from the middle of the blade as it is. The third this far ahead, a
        /// little short of where the sword's point lands, its lower tip this far over the
        /// ground.
        /// </summary>
        private const float SlashAhead = 60f;
        private const float SlashHigh = 36f;
        private const float GreatAhead = 84f;
        private const float SlashClear = 2f;

        /// <summary>
        /// Each goes at them, wherever they are, by request ("the slash is multi
        /// directional so if ur on the ceiling it also hits you"): anywhere ahead of him,
        /// straight up and straight down with it, and over his own head, as far round
        /// from the way he faces as this (half of a right angle past straight up). Not
        /// behind him: in a cut he does not turn, and what he lets go of then goes the
        /// way he faces.
        /// </summary>
        private static readonly float SlashBack = MathHelper.ToRadians(135f);

        /// <summary>
        /// The three cuts, a step into each. Before each of them he looks for them again,
        /// and comes round if they have got behind him; in a cut he does not. Where the
        /// world is decided, each is let go of as it is made.
        /// </summary>
        private void Rend(Player target)
        {
            Fall();
            NPC.velocity.X = Stepped(M.RendSteps);

            foreach (int tick in M.RendLooks)
            {
                if (At(tick))
                    FaceThem(target);
            }

            if (!Authority)
                return;

            for (int which = 0; which < M.RendCasts.Length; which++)
            {
                if (At(M.RendCasts[which]))
                    Cast(target, which);
            }

            if (now >= M.RendTicks)
                Rest(40, 62);
        }

        /// <summary>
        /// One of the three, let go of, the server's (FoighSlash): at where they will
        /// be, half of where they are going allowed for (as a knife is), and no lower
        /// than has its lower tip at their feet, so that at one who stands on the ground
        /// he stands on it goes level, clear of that ground.
        /// </summary>
        private void Cast(Player target, int which)
        {
            Cast(target, which, which == M.RendCasts.Length - 1 ? FoighSlash.Great : FoighSlash.Plain);
        }

        /// <summary>The same, of this kind (his second phase's wind is let go as these are: Foigh.Reach).</summary>
        private void Cast(Player target, int which, int kind)
        {
            float tall = FoighSlash.TallOf(kind);
            float speed = FoighSlash.SpeedOf(kind);

            Vector2 feet = Feet;
            Vector2 from;

            if (which == 1)
            {
                FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), feet, facing);

                from = Vector2.Lerp(at.Guard, at.Tip, 0.5f);
            }
            else if (kind == FoighSlash.Great)
            {
                from = new Vector2(feet.X + facing * GreatAhead, feet.Y - SlashClear - tall * 0.5f);
            }
            else
            {
                from = new Vector2(feet.X + facing * SlashAhead, feet.Y - SlashHigh);
            }

            // With a wall where it would begin there is nowhere for it to go.
            if (Collision.SolidCollision(from - new Vector2(5f), 10, 10))
                return;

            Projectile.NewProjectile(NPC.GetSource_FromAI(), from, SlashWay(target, from, tall, speed) * speed,
                ModContent.ProjectileType<FoighSlash>(), 0, 0f, Main.myPlayer, NPC.whoAmI, kind);
        }

        /// <summary>
        /// The way one of them goes that begins here: at them, if they are anywhere it
        /// can be sent; the way he faces, if not. Let go from over them (his third
        /// phase's wheel: Foigh.Wild), one who is under him or at his back is not out of
        /// its way: it goes straight down.
        /// </summary>
        private Vector2 SlashWay(Player target, Vector2 from, float tall, float speed, bool over = false)
        {
            Vector2 ahead = new Vector2(facing, 0f);

            if (target == null || target.dead)
                return ahead;

            Vector2 to = target.Center + target.velocity * (Vector2.Distance(from, target.Center) / speed) * 0.5f;

            to.Y = Math.Min(to.Y, target.Bottom.Y - SlashClear - tall * 0.5f);

            Vector2 off = to - from;

            if (off.LengthSquared() < 1f)
                return ahead;

            // From the way he faces: up is less than nothing, down more.
            float turn = (float)Math.Atan2(off.Y, off.X * facing);

            if (over && turn > MathHelper.PiOver2)
                return Vector2.UnitY;

            if (turn < -SlashBack || turn > MathHelper.PiOver2)
                return ahead;

            return new Vector2(facing * (float)Math.Cos(turn), (float)Math.Sin(turn));
        }

        /// <summary>
        /// The blade before it lets its cuts go, on a machine with a screen: what it
        /// burns with stands off the whole length of it at once, and is heard.
        /// </summary>
        private void Flare()
        {
            Vector2 along = frame.Tip - frame.Guard;
            Vector2 off = new Vector2(along.Y, -along.X).SafeNormalize(-Vector2.UnitY);

            // Up off it, whichever way it lies.
            if (off.Y > 0f)
                off = -off;

            Sound(SoundID.Item74, frame.Guard + along * 0.5f, -0.3f, 0.5f);
            Glint(frame.Tip, Cinder, 34f, 14);

            for (int i = 0; i < 12; i++)
            {
                Vector2 at = frame.Guard + along * Main.rand.NextFloat(0.08f, 1f);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, off * Main.rand.NextFloat(0.6f, 2.4f) + Main.rand.NextVector2Circular(0.5f, 0.5f), Soot, BurnRed,
                    Main.rand.Next(24, 40), Main.rand.NextFloat(9f, 15f), Main.rand.NextFloat(26f, 40f), Main.rand.NextFloat(0.75f, 1f)));

                if (i % 2 == 0)
                {
                    PADParticleHandler.SpawnParticle(new Ember(at, off * Main.rand.NextFloat(0.8f, 3f) + Main.rand.NextVector2Circular(0.8f, 0.8f), Cinder,
                        Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(18, 32)));
                }

                if (i % 3 == 0)
                    PADParticleHandler.SpawnParticle(new BloodFume(at, off * Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(0.1f, 0.16f), Main.rand.Next(16, 28)));
            }
        }

        /// <summary>
        /// Every tick, on a machine with a screen, while the sword burns: black off the
        /// whole length of the blade, in tatters, lifted and left behind as it moves; red
        /// in it; sparks; and its light.
        /// </summary>
        private void Smoulder()
        {
            if (flame <= 0.02f || !frame.HasSword || NPC.alpha >= 200 || !Near(1700f))
                return;

            Vector2 along = frame.Tip - frame.Guard;
            float length = along.Length();

            // Seen end on there is next to nothing of it to burn.
            if (length < 8f)
                return;

            bool cutting = trail > 0.5f;
            Vector2 back = new Vector2(-facing * 0.5f, -1f);

            for (int i = 0; i < (cutting ? 3 : 1); i++)
            {
                if (Main.rand.NextFloat() > flame)
                    continue;

                Vector2 at = frame.Guard + along * Main.rand.NextFloat(0.06f, 1f) + Main.rand.NextVector2Circular(3f, 3f);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, back * Main.rand.NextFloat(0.4f, 1.3f) + NPC.velocity * 0.2f, Soot, BurnRed,
                    Main.rand.Next(cutting ? 34 : 24, cutting ? 56 : 40), Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(22f, 36f), Main.rand.NextFloat(0.75f, 1f)));
            }

            if (Main.GameUpdateCount % 2 == 0 || cutting)
            {
                Vector2 at = frame.Guard + along * Main.rand.NextFloat(0.1f, 1f);

                PADParticleHandler.SpawnParticle(new BloodFume(at, back * Main.rand.NextFloat(0.2f, 0.8f), Main.rand.NextFloat(0.05f, 0.1f) * (cutting ? 1.5f : 1f), Main.rand.Next(14, 26)));
            }

            if (Main.GameUpdateCount % 4 == 0)
            {
                Vector2 at = frame.Guard + along * Main.rand.NextFloat(0.1f, 1f);

                PADParticleHandler.SpawnParticle(new Ember(at, new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.4f, 1.8f)), Cinder,
                    Main.rand.NextFloat(0.4f, 0.75f), Main.rand.Next(18, 32)));
            }

            Lighting.AddLight(frame.Guard + along * 0.5f, 0.55f * flame, 0.07f * flame, 0.06f * flame);
        }

        /// <summary>The burning sword on this machine's player: the cut seen as its cuts are, crossed, and more blood.</summary>
        private void BurnLooks(Player me, float power, Vector2 across)
        {
            BloodSlash.Pair(me.Center, across, 46f + 14f * power, 6f + 2f * power, 18);

            // Two more through them, across the first.
            for (int i = 0; i < 2; i++)
            {
                float turn = MathHelper.ToRadians(Main.rand.NextFloat(38f, 76f)) * (i == 0 ? 1f : -1f);

                BloodSlash.Pair(me.Center + Main.rand.NextVector2Circular(8f, 10f), across.RotatedBy(turn), Main.rand.NextFloat(34f, 58f), 4.5f, Main.rand.Next(14, 22));
            }

            BloodDroplet.SpawnBurst(me.Center, across * (Main.rand.NextBool() ? 1f : -1f), (int)(10 + 6 * power), 50f);

            for (int i = 0; i < 4; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodFume(me.Center + Main.rand.NextVector2Circular(12f, 16f), Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Main.rand.NextFloat(0.1f, 0.18f), Main.rand.Next(18, 30)));
            }
        }

        /// <summary>The burning sword into the ground: what a hard blow of it leaves in the air over the place, and along the ground from it.</summary>
        private void Scorch(Vector2 at, float hard)
        {
            Haze(at, (int)(5 + 5 * hard), 2.2f + hard);

            if (hard < 1.5f)
                return;

            for (int i = 0; i < 2; i++)
            {
                float slant = MathHelper.ToRadians(Main.rand.NextFloat(12f, 40f)) * (i == 0 ? 1f : -1f);

                BloodSlash.Pair(at + new Vector2(Main.rand.NextFloat(-10f, 10f), -Main.rand.NextFloat(10f, 34f)),
                    new Vector2((float)Math.Cos(slant), (float)Math.Sin(slant)), Main.rand.NextFloat(70f, 128f), 5f, Main.rand.Next(18, 28));
            }
        }

        // ---- the sword, left standing ----

        /// <summary>The sword where he left it standing behind them: on its own, not turned with him, bloodied as it is.</summary>
        private void DrawLeft(SpriteBatch spriteBatch, Vector2 screenPos, Color colour)
        {
            if (!Left(parts, out FoighRig.Part sword))
                return;

            Texture2D picture = SwordPicture(true, true);

            if (picture == null)
                return;

            spriteBatch.Draw(picture, sword.At - screenPos, null, colour, sword.Rotation, sword.Origin, 1f,
                sword.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        // ---- their wings, in his hand ----

        /// <summary>What he has in his bottom hand since the cut, as an item of the game's: their wings; nothing at 0; not looked for yet, less than that.</summary>
        private int wingsHeld = -1;

        /// <summary>
        /// The wings a player has on, as an item: the pair that is drawn on them, or the
        /// pair they fly by. 0 with none.
        /// </summary>
        private static int WingsOf(Player player)
        {
            if (player.wings > 0)
            {
                foreach (Item item in player.armor)
                {
                    if (item != null && !item.IsAir && item.wingSlot == player.wings)
                        return item.type;
                }
            }

            Item worn = player.equippedWings;

            return worn != null && !worn.IsAir ? worn.type : 0;
        }

        /// <summary>What he has in his hand since the cut, looked for the first time it is asked.</summary>
        private int WingsHeld()
        {
            if (wingsHeld < 0)
            {
                Player target = Target();

                wingsHeld = target != null ? WingsOf(target) : 0;
            }

            return wingsHeld;
        }

        /// <summary>
        /// Their wings, in his bottom hand from the cut on; and let fall as the picture
        /// goes. The item's own picture, lit as the place is.
        /// </summary>
        private void DrawWings(SpriteBatch spriteBatch, Vector2 screenPos, FoighRig.Frame of)
        {
            bool held = State == Move.Sever && now >= M.SeverCuts;
            bool falling = State == Move.Resume && phase >= 3 && leaving != null && now < FadeOutTicks + 2;

            if ((!held && !falling) || WingsHeld() <= 0)
                return;

            Main.GetItemDrawFrame(wingsHeld, out Texture2D texture, out Rectangle box);

            if (texture == null)
                return;

            Vector2 at = of.FarFist + new Vector2(facing * 2f, 6f);
            float turn = facing * 0.3f;

            if (falling)
            {
                // Out of his hand: down by its own weight, turning over as it goes.
                at += new Vector2(-facing * 0.4f * now, 0.21f * now * now);
                turn += facing * 0.11f * now;
                at.Y = Math.Min(at.Y, Feet.Y - 4f);
            }

            Color light = Lighting.GetColor(at.ToTileCoordinates());

            spriteBatch.Draw(texture, at - screenPos, box, light, turn, box.Size() * 0.5f, 1f,
                facing > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
        }

        // ---- the sword, burning ----

        /// <summary>
        /// The black and the red on the blade itself: a wide faint red about it; soot
        /// standing off its upper side in tongues that never hold still; the blade red
        /// hot under it, and a paler line down its middle. Soot is drawn over what is
        /// behind it; the red is light.
        /// </summary>
        private void DrawBurning(FoighRig.Frame of)
        {
            if (softLine == null || !softLine.IsLoaded)
                return;

            Vector2 along = of.Tip - of.Guard;
            float length = along.Length();

            if (length < 8f)
                return;

            along /= length;

            // The side of the blade the soot stands off: its upper one, or behind him if it is upright.
            Vector2 off = new Vector2(along.Y, -along.X);

            if (off.Y > 0.05f || (Math.Abs(off.Y) <= 0.05f && off.X * facing > 0f))
                off = -off;

            const int steps = 22;
            float time = Main.GlobalTimeWrappedHourly * 9f + NPC.whoAmI;

            // Under the soot, a wide faint red: against the dark, where soot alone is
            // nothing, its tongues are then black shapes cut out of it.
            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = of.Guard + along * (length * k);
                float thin = MathHelper.Clamp(Math.Min(k / 0.08f, (1f - k) / 0.05f), 0f, 1f);
                Color glow = new Color(BurnRed.R, BurnRed.G, BurnRed.B, 0) * (0.28f * flame * (0.5f + 0.5f * thin));

                Strip.Pair(at - off * 13f, at + off * 22f, glow, glow, 0.5f);
            }

            Strip.Draw(softLine.Value);

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = of.Guard + along * (length * k);

                // Tongues: two turns of different lengths against each other, so it never repeats on a beat.
                float tongue = 0.5f + 0.5f * (float)(Math.Sin(i * 1.9f + time) * Math.Sin(i * 0.63f - time * 0.61f));
                float thin = MathHelper.Clamp(Math.Min(k / 0.08f, (1f - k) / 0.05f), 0f, 1f);
                Color soot = new Color(Soot.R, Soot.G, Soot.B, 255) * (0.62f * flame * thin * (0.55f + 0.45f * tongue));

                Strip.Pair(at - off * 3f, at + off * (9f + 9f * tongue), soot, soot, 0.5f);
            }

            Strip.Draw(softLine.Value);

            float flicker = 0.8f + 0.2f * (float)Math.Sin(time * 2.3f);

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = of.Guard + along * (length * k);
                float patch = 0.6f + 0.4f * (float)Math.Sin(i * 1.3f - time * 0.9f);
                Color red = new Color(BurnRed.R, BurnRed.G, BurnRed.B, 0) * (0.55f * flame * flicker * patch);

                Strip.Pair(at - off * 7f, at + off * 7f, red, red, 0.5f);
            }

            Strip.Draw(softLine.Value);

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Vector2 at = of.Guard + along * (length * k);
                Color hot = new Color(BurnHot.R, BurnHot.G, BurnHot.B, 0) * (0.34f * flame * flicker);

                Strip.Pair(at - off * 2.2f, at + off * 2.2f, hot, hot, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }
    }
}
