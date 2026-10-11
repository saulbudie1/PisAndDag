using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The passage into his second phase: what he does when two thirds of his life are left,
    /// on every machine, and what the player he fights is shown and asked, on their own
    /// machine only. By request:
    ///
    ///   SPENT    whatever he was doing, he stops. The picture goes to black.
    ///   KNEE     under the black he is down on a knee before his sword, which stands
    ///            upright in the ground, driven in until its handle is at his chest, both
    ///            his hands on the handle and his head bowed over it; and the player is put
    ///            down seventy tiles off him (FoighScenePlayer.PutOff). The picture comes
    ///            back on the player; the camera goes over to him, and leans in. He looks
    ///            down and spits blood four times, a second, four tenths and three tenths
    ///            of a second apart. He looks up, and says two lines, each with its one
    ///            answer.
    ///   WHET     he heaves at the sword, tears it out forward through the ground, and
    ///            has it up at the sky; then down before him, a stone out of his belt, and
    ///            the sword drawn back along it from guard to point: sparks, and the blade
    ///            is the sharp one from there on.
    ///   OATH     his third line, which has no answer: it goes by itself.
    ///   RESUME   the picture to black, back on the player, and the fight goes on.
    ///
    /// Nothing lands on him from the first tick of it to the last, and the player is held
    /// for as long (Cinematic.Hold: nothing lands on them either). The server waits at Knee
    /// and at Oath for the player's word, and not for ever. Only the player he fights is
    /// moved and shown it; to anybody else he is simply doing it.
    ///
    /// The camera is PisPlayer's (BlendCamera); the bars, the lean, the fade and the hold are
    /// Cinematic's. The lines are the user's own words, kept as written (the bars in them are
    /// Dialogue.Pause: he stops there).
    ///
    /// What he fights with afterwards is in Foigh.Fight (Choose, and the hand's move) and
    /// Foigh.React: the same sword, quicker, and one move more.
    ///
    /// The passage into his third phase (Foigh.Third) begins and ends as this one does:
    /// Spent and Resume are both's, and so is what goes with them here (Yield, the scene's
    /// own tick, the way back).
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // HIS LINES
        // ============================================================

        private static readonly string[] SecondLines =
        {
            "Your skills are|.|.|. exceedingly well honed. I can feel it!",
            "You have earned my sharp side of the blade|.|.|.!",
            "Let this foolish old man's flame rage once more",
        };

        // ============================================================
        // THE PASSAGE'S TICKS
        // ============================================================

        /// <summary>How far off him the player is put down: seventy tiles, by request; and, where there is no ground that far off on either side of him, no nearer than this.</summary>
        public const float PutOffAt = 70f * 16f;
        public const float PutOffLeast = 30f * 16f;

        /// <summary>Spent: ticks the picture takes to go to black; and, still in the air after this many, he kneels where he comes down all the same.</summary>
        private const int SpentFade = 24;
        private const int SpentLongest = 150;

        /// <summary>
        /// Knee: the tick of the black the player is moved on; the tick the picture begins to
        /// come back, and how long it takes; the tick the camera sets off for him, and how
        /// long it takes to get there; the tick of his first line; and, his lines never
        /// answered, when he goes on by himself.
        /// </summary>
        private const int KneeCut = 3;
        private const int KneeBlack = 16;
        private const int KneeFadeIn = 30;
        private const int KneePanAt = KneeBlack + KneeFadeIn + 12;
        private const int KneePan = 96;
        private const int KneeSpeaks = M.KneeLooked + 10;
        private const int KneeLongest = KneeSpeaks + 5400;

        /// <summary>Oath: the tick his third line begins; how long it stays once all of it is there; and, never read, when he goes on by himself.</summary>
        private const int OathSpeaks = 12;
        private const int OathLinger = 80;
        private const int OathLongest = 900;

        /// <summary>Resume: the picture to black, black, and back up on the player (the road's own numbers); then he walks.</summary>
        private const int ResumeTicks = FadeOutTicks + BlackTicks + FadeInTicks;

        /// <summary>The lean on him while he kneels and whets, and for his third line.</summary>
        private const float KneeZoom = 1.3f;
        private const float OathZoom = 1.36f;

        // ============================================================
        // WHERE THE WORLD IS DECIDED
        // ============================================================

        /// <summary>
        /// Whether he can stop for it this tick: in any move of the fight but between two
        /// places (a flash step is over in a few ticks, and he is where he was going). Not
        /// once he is dying or leaving: a blow that takes him from over two thirds of his
        /// life to none ends it there.
        /// </summary>
        private bool CanYield => State >= Move.Walk && State <= Move.Broken && State != Move.Flash;

        /// <summary>
        /// Two thirds of his life left, or three tenths (Foigh.Third), the server's:
        /// whoever he has hold of is let go, he is in his next phase, and he stops. One
        /// phase at a time: taken from his first to under three tenths at a blow, he goes
        /// through the one passage and then, on his first step out of it, the other.
        /// </summary>
        private void Yield()
        {
            Release();
            Settle();

            phase = phase < 2 ? 2 : 3;
            drank = 0;
            drankBy = -1;

            Go(Move.Spent);
        }

        /// <summary>
        /// Black by now, the server's: what he had thrown is gone with the picture, the sword
        /// is in his hands, and he is down on his knee.
        /// </summary>
        private void Kneel()
        {
            Gather();
            FaceThem(Target());
            Go(Move.Knee);
        }

        // ============================================================
        // EVERY MACHINE
        // ============================================================

        private void Spent()
        {
            Fall();
            NPC.velocity.X *= 0.8f;

            if (!Authority)
                return;

            if ((now < SpentFade + 2 || !grounded) && now < SpentLongest)
                return;

            // Into his second phase, onto his knee; into his third, onto his feet over his sword.
            if (phase >= 3)
                Brace();
            else
                Kneel();
        }

        private void Knee(Player target)
        {
            Fall();
            NPC.velocity.X = 0f;

            if (!Authority)
                return;

            // Under the black they are put down somewhere else: he is turned to where they are
            // now, for as long as nobody can see him (until the camera sets off for him).
            if (now <= KneePanAt)
                FaceThem(target);

            if (now >= KneeLongest)
                Go(Move.Whet);
        }

        private void Whet()
        {
            Fall();
            NPC.velocity.X = 0f;

            if (Authority && now >= M.WhetTicks)
                Go(Move.Oath);
        }

        private void Oath()
        {
            Fall();
            NPC.velocity.X = 0f;

            if (Authority && now >= OathLongest)
                Go(Move.Resume);
        }

        private void Resume()
        {
            Fall();
            NPC.velocity.X = 0f;

            // Out of the passage into his third phase: under the black he is back where he
            // stood before he went behind them.
            if (phase >= 3 && At(FadeOutTicks + 4))
                Return();

            if (!Authority || now < ResumeTicks)
                return;

            rest = FirstRest;
            Go(Move.Walk);
        }

        // ============================================================
        // THE PLAYER IT IS FOR
        // ============================================================

        /// <summary>Which of his three lines is up, from 1; none yet at 0.</summary>
        private int said;

        private bool put;
        private bool answered;
        private bool sworn;
        private bool cutBack;

        /// <summary>The tick of Oath at which all of his third line was there; less than none before it is.</summary>
        private int oathRead = -1;

        /// <summary>The first tick this machine sees of a passage, either of them: nothing of it has been shown yet.</summary>
        private void PassageBegins()
        {
            said = 0;
            put = false;
            answered = false;
            sworn = false;
            cutBack = false;
            oathRead = -1;

            ThirdBegins();
        }

        /// <summary>The scene for the player it is for, every tick, on their machine.</summary>
        private void PassageScene()
        {
            Player me = Main.LocalPlayer;

            // Nobody to show it to: everything asked for lets go by itself.
            if (me.dead || !me.active)
                return;

            Vector2 him = NPC.Center + new Vector2(0f, -30f);
            bool wantShot = false;

            switch (State)
            {
                case Move.Spent:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Fade(Smooth(now / (float)SpentFade));
                    break;

                case Move.Knee:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    FoighScenePlayer.FaceToward(NPC);

                    if (now < KneeBlack)
                        Cinematic.Fade(1f);
                    else if (now < KneeBlack + KneeFadeIn)
                        Cinematic.Fade(1f - Smooth((now - KneeBlack) / (float)KneeFadeIn));

                    // Under the black: the player somewhere else, and the camera with them. (One
                    // who became his after the picture was back is left where they stand.)
                    if (!put && now >= KneeCut)
                    {
                        put = true;

                        if (now < KneeBlack)
                        {
                            shot = 0f;
                            me.GetModPlayer<FoighScenePlayer>().PutOff(NPC, PutOffAt, PutOffLeast);
                        }
                    }

                    if (now >= KneePanAt)
                    {
                        wantShot = true;
                        Cinematic.Lean(KneeZoom, 0.03f);
                    }

                    KneeTalk();
                    break;

                case Move.Whet:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(KneeZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();
                    break;

                case Move.Oath:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(OathZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;
                    OathSaid();
                    break;

                case Move.Resume:
                    Back(him, out wantShot);
                    break;

                // The passage into his third phase (Foigh.Third), his step behind them with it.
                case Move.Shudder:
                case Move.Gut:
                case Move.Bled:
                case Move.Sever:
                case Move.Flash:
                    ThirdScene(me, out wantShot);
                    break;
            }

            shot = MathHelper.Clamp(shot + (wantShot ? 1f : -1f) / KneePan, 0f, 1f);

            if (shot > 0f)
                PisPlayer.BlendCamera(him, shot * shot * (3f - 2f * shot));
        }

        /// <summary>
        /// His two lines on his knee, one after the other, each with its one answer. The
        /// second answer tells the server. A line whose box has gone down under him
        /// (something else took it) comes back.
        /// </summary>
        private void KneeTalk()
        {
            if (answered || now < KneeSpeaks)
                return;

            if (said == 0)
            {
                SayOnKnee(0);
                return;
            }

            if (!Dialogue.Speaking(NPC))
                SayOnKnee(said - 1);
        }

        private void SayOnKnee(int i)
        {
            // They have been fighting him: his face and his name are theirs already.
            DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

            said = i + 1;
            Dialogue.Converse(NPC, SecondLines[i], Pace, KneeAnswer(i));
        }

        /// <summary>The answer under line <paramref name="i"/>: his next line, or, under the second, the end of the talk.</summary>
        private TalkOption KneeAnswer(int i)
        {
            return new TalkOption(Silence, _ =>
            {
                if (i < 1)
                {
                    said = 2;
                    Dialogue.Scene(SecondLines[1], KneeAnswer(1));
                    return;
                }

                answered = true;
                Dialogue.Close();
                Tell(Word.Answered);
            })
            { Remembered = false };
        }

        /// <summary>
        /// His third line, with nothing under it: up until all of it has been there a while,
        /// then down by itself, and the server told.
        /// </summary>
        private void OathSaid()
        {
            if (sworn || now < OathSpeaks)
                return;

            if (!Dialogue.Speaking(NPC))
            {
                // Not up yet; or it has gone down under him before it was read: up again.
                said = 3;
                oathRead = -1;
                Dialogue.Converse(NPC, SecondLines[2], Pace);
                return;
            }

            if (oathRead < 0)
            {
                if (Dialogue.LineDone)
                    oathRead = now;

                return;
            }

            if (now - oathRead < OathLinger)
                return;

            sworn = true;
            Dialogue.Close();
            Tell(Word.Sworn);
        }

        /// <summary>
        /// The way back to the fight: the picture to black with the camera still on him,
        /// the camera put back on the player under it, and back up on them, their feet theirs
        /// again as it comes.
        /// </summary>
        private void Back(Vector2 him, out bool wantShot)
        {
            if (Dialogue.Speaking(NPC))
                Dialogue.Close();

            int dark = FadeOutTicks;
            int back = dark + BlackTicks;

            wantShot = now < dark;

            if (now < back + FadeInTicks / 2)
                Cinematic.Hold();

            if (now < back + FadeInTicks)
                Cinematic.Frame();

            if (now < dark)
            {
                Cinematic.Lean(phase >= 3 ? SeverZoom : OathZoom, 0.03f);

                // (Out of his third passage he is behind them, and their back is to him
                // until the picture has gone.)
                if (phase >= 3)
                    FoighScenePlayer.FaceAway(NPC);
                else
                    FoighScenePlayer.FaceToward(NPC);

                Cinematic.Fade(Smooth(now / (float)FadeOutTicks));
            }
            else if (now < back)
            {
                Cinematic.Fade(1f);

                // Under the black he is back where he stood: they are turned to him there.
                if (phase >= 3 && now > FadeOutTicks + 4)
                    FoighScenePlayer.FaceToward(NPC);

                // Under the black: the camera home, at once.
                if (!cutBack)
                {
                    cutBack = true;
                    shot = 0f;
                    PisPlayer.PanHome(him, 8);
                }
            }
            else if (now < back + FadeInTicks)
            {
                Cinematic.Fade(1f - Smooth((now - back) / (float)FadeInTicks));
            }
        }

        // ============================================================
        // WHAT IT LOOKS AND SOUNDS LIKE
        // ============================================================

        /// <summary>How far along the blade the stone has been, guard to point: 0 before it starts, 1 once it is off the point.</summary>
        private float whetted;

        /// <summary>How much of the sword's sharp side is shown over its blunt one, 0 to 1.</summary>
        private float sharpShown;

        /// <summary>A light running up the blade, guard to point: where it is, a little under 0 to a little over 1; none at less than that.</summary>
        private float gleam = NoGleam;

        private const float NoGleam = -1f;

        /// <summary>The light up the blade: when it sets off, how long it takes, and when the second one sets off.</summary>
        private const int GleamAt = M.WhetShown - 4;
        private const int GleamTicks = 16;
        private const int GleamAgain = GleamAt + 24;

        /// <summary>The sword is its sharp side as it is drawn on this machine.</summary>
        public bool Sharp => sharpShown >= 1f;

        /// <summary>
        /// Every tick, every machine, once he has been built for it: how far the stone has
        /// been along the blade (where his bottom hand is on it), how much of the sharp side
        /// that shows, and the light that runs up it after.
        /// </summary>
        private void ShowBlade()
        {
            if (State == Move.Whet)
            {
                if (fresh)
                    whetted = 0f;

                if (now >= M.WhetSet && now <= M.WhetSwipe + 6 && frame.HasSword)
                    whetted = Math.Max(whetted, StoneAlong());

                if (now > M.WhetSwipe + 6)
                    whetted = 1f;

                gleam = Gleaming(now - GleamAt);

                if (gleam == NoGleam)
                    gleam = Gleaming(now - GleamAgain);
            }
            else if (State == Move.Poise)
            {
                // Sunk low for the late cut: he looks along the line, and the light goes up the blade.
                gleam = Gleaming(now - M.PoiseGleams);
            }
            else if (State == Move.Hang)
            {
                // Beside them in the air, as the sword goes up.
                gleam = Gleaming(now - 1);
            }
            else if (State == Move.Gut)
            {
                // Held up at the sky, before he turns it on himself.
                gleam = Gleaming(now - GutGleamAt);
            }
            else
            {
                gleam = NoGleam;
            }

            // (Blunt still while he stops and kneels on the way into his second phase; not
            // on the way into his third, which begins as that does.)
            sharpShown = phase < 2 || (phase == 2 && (State == Move.Spent || State == Move.Knee)) ? 0f
                : State == Move.Whet ? whetted
                : 1f;

            ShowBlood();
        }

        /// <summary>Where the light up the blade is, this many ticks after it set off.</summary>
        private static float Gleaming(int ticks)
        {
            return ticks < 0 || ticks > GleamTicks ? NoGleam : -0.15f + 1.3f * ticks / GleamTicks;
        }

        /// <summary>Where along the blade his bottom hand is, guard to point, 0 to 1.</summary>
        private float StoneAlong()
        {
            Vector2 blade = frame.Tip - frame.Guard;
            float length = blade.LengthSquared();

            if (length < 1f)
                return 0f;

            return MathHelper.Clamp(Vector2.Dot(frame.FarFist - frame.Guard, blade) / length, 0f, 1f);
        }

        /// <summary>The place on the blade's upper edge this far along it, guard to point.</summary>
        private Vector2 OnBlade(float along)
        {
            return Vector2.Lerp(frame.Guard, frame.Tip, MathHelper.Clamp(along, 0f, 1f));
        }

        /// <summary>
        /// Blood out of his mouth, down onto the ground ahead of him: each drop lives until it
        /// is at the ground and no longer, so that none falls through it.
        /// </summary>
        private void SpitBlood(int drops)
        {
            Vector2 mouth = frame.MouthAt;
            float ground = Feet.Y;
            Color blood = new Color(150, 8, 12);

            Sound(SoundID.NPCHit1, mouth, -0.55f, 0.5f);
            Sound(SoundID.NPCDeath1, mouth, 0.45f, 0.25f);

            for (int i = 0; i < drops; i++)
            {
                float turn = MathHelper.ToRadians(Main.rand.NextFloat(-20f, 20f));
                Vector2 velocity = new Vector2(facing * 0.8f, 0.6f).RotatedBy(turn * facing) * Main.rand.NextFloat(1.4f, 4.6f);

                // As the droplet will go (BloodDroplet.Update): how many ticks to the ground.
                Vector2 at = mouth;
                Vector2 going = velocity;
                int life = 0;

                while (at.Y < ground && life < 60)
                {
                    at += going;
                    going.X *= 0.965f;
                    going.Y = MathHelper.Clamp(going.Y + 0.42f, -18f, 18f);
                    life++;
                }

                PADParticleHandler.SpawnParticle(new BloodDroplet(mouth, velocity, Math.Max(2, life), Main.rand.NextFloat(0.5f, 0.9f), blood));

                // And where it came down, what is left of it: lying there, and slowly gone.
                if (i % 2 == 0)
                {
                    PADParticleHandler.SpawnParticle(new BloodDroplet(new Vector2(at.X, ground - 1f), Vector2.Zero,
                        Main.rand.Next(150, 230), Main.rand.NextFloat(0.4f, 0.7f), new Color(112, 6, 10), false));
                }
            }
        }

        /// <summary>The stone on the steel, this tick: sparks off where it is, and the grit it takes off the blade falling.</summary>
        private void Grind()
        {
            Vector2 at = frame.FarFist;
            Vector2 along = (frame.Tip - frame.Guard).SafeNormalize(new Vector2(facing, 0f));
            Vector2 off = new Vector2(along.Y, -along.X);

            // Off the blade's upper side, whichever way it points.
            if (off.Y > 0f)
                off = -off;

            for (int i = 0; i < 3; i++)
            {
                Vector2 way = (off + along * Main.rand.NextFloat(-0.2f, 1.1f)).SafeNormalize(off);

                PADParticleHandler.SpawnParticle(new SparkShard(at, way * Main.rand.NextFloat(2f, 6.5f),
                    Sparks[Main.rand.Next(Sparks.Length)], 5f, 9f, Main.rand.NextFloat(10f, 16f), Main.rand.Next(8, 15), falls: true));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Steel * 0.5f, 0.14f, 5));

            if (Main.rand.NextBool(2))
            {
                Dust grit = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(3f, 3f), DustID.Stone,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 1.4f)), 120, new Color(92, 86, 90), Main.rand.NextFloat(0.5f, 0.8f));

                grit.noLight = true;
            }
        }

        /// <summary>The passage's sparks, dust, blood and sounds, this tick of it (Looks asks, on a machine with a screen).</summary>
        private void PassageLooks()
        {
            Vector2 feet = Feet;

            switch (State)
            {
                case Move.Knee:
                    for (int i = 0; i < M.Spits.Length; i++)
                    {
                        if (At(M.Spits[i]))
                            SpitBlood(i == M.Spits.Length - 1 ? 13 : 8);
                    }
                    break;

                case Move.Whet:
                {
                    Vector2 where = new Vector2(feet.X + facing * M.KneeEnters, feet.Y);

                    // He sets himself over it and heaves: it grinds in the ground, and comes a little.
                    if (At(M.WhetGrips)) Sound(SoundID.Tink, frame.Grip, -0.8f, 0.5f);

                    if (now > M.WhetGrips && now < M.WhetLoose && now % 5 == 0)
                        Puff(where, 1, 1.2f, 0.6f);

                    if (now <= M.WhetLoose)
                        furrow = -1;

                    // It gives.
                    if (At(M.WhetLoose))
                    {
                        Sound(SoundID.Dig, where, -0.6f, 0.9f);
                        Sound(SoundID.Tink, where, -0.7f, 0.6f);
                        Puff(where, 8, 2.4f);
                        Quake(3f, 8);
                    }

                    // Torn out forward through the ground; and where its point comes up out of it.
                    if (now > M.WhetLoose && now < M.WhetOut && InGround(out Vector2 cut))
                        Plough(cut, false);

                    if (At(M.WhetOut))
                        Plough(new Vector2(feet.X + facing * M.WhetBreaks, feet.Y), true);

                    if (At(M.WhetHigh)) Glint(frame.Tip, Steel, 34f, 18);
                    if (At(M.WhetBelt + 2)) Sound(SoundID.Tink, frame.FarFist, 0.5f, 0.35f);

                    if (At(M.WhetSet + 1)) Sound(SoundID.Item71, frame.FarFist, 0.55f, 0.8f);

                    if (now > M.WhetSet && now <= M.WhetSwipe + 2 && whetted < 1f)
                        Grind();

                    if (At(M.WhetSwipe + 3))
                    {
                        Sound(SoundID.Tink, frame.Tip, 0.7f, 0.5f);
                        Glint(frame.Tip, Color.White, 26f, 12);
                    }

                    if (At(GleamAt + 2))
                    {
                        Sound(SoundID.Item37, frame.Guard, 0.5f, 0.45f);
                        PADParticleHandler.SpawnParticle(new ParryRing(OnBlade(0.5f), 10f, 64f, 14, Steel * 0.3f, NPC));
                    }

                    if (At(GleamAt + GleamTicks - 2)) Glint(frame.Tip, Color.White, 40f, 18);

                    // The blade new: lights along it, here and there, while he holds it up.
                    if (now > M.WhetSwipe + 6 && now < M.WhetAway + 8 && now % 4 == 0)
                    {
                        PADParticleHandler.SpawnParticle(new CrownGlint(OnBlade(Main.rand.NextFloat(0.08f, 1f)), Main.rand.NextFloat(8f, 15f), Main.rand.Next(8, 13), Color.White));
                    }

                    if (At(M.WhetAway + 6)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.8f);

                    if (At(M.WhetTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// The tile column the ground has been heaved up as far as, along the sword's way
        /// forward through it; less than none before it has set off.
        /// </summary>
        private int furrow = -1;

        /// <summary>Where the sword goes into the ground he stands on, as it is held this tick. False if it is not in it.</summary>
        private bool InGround(out Vector2 at)
        {
            float ground = Feet.Y;

            at = default;

            if (!frame.HasSword || frame.Tip.Y <= ground || frame.Grip.Y >= ground)
                return false;

            at = new Vector2(MathHelper.Lerp(frame.Grip.X, frame.Tip.X, (ground - frame.Grip.Y) / (frame.Tip.Y - frame.Grip.Y)), ground);
            return true;
        }

        /// <summary>
        /// The sword on its way forward through the ground: the ground over it heaved up,
        /// tile by tile, from where the blade went in as far as here, and steel on stone
        /// here. Where its point comes up out of it (<paramref name="breaks"/>), what that
        /// throws up as well. It went in where it stood before him on his knee; or, in
        /// another move, at <paramref name="from"/> (the cut up: where it is first seen in
        /// the ground).
        /// </summary>
        private void Plough(Vector2 cut, bool breaks, float from = float.NaN)
        {
            int row = (int)((cut.Y + 4f) / 16f);
            int to = (int)(cut.X / 16f);

            if (furrow < 0)
                furrow = (int)((float.IsNaN(from) ? Feet.X + facing * M.KneeEnters : from) / 16f) - facing;

            // A tile at a time, and no further in one tick than a blade of his could go.
            for (int n = 0; n < 8 && (to - furrow) * facing > 0; n++)
            {
                furrow += facing;
                Heave(furrow, row, breaks && furrow == to ? 9f : Main.rand.NextFloat(4f, 6.5f));
            }

            for (int i = 0; i < (breaks ? 9 : 2); i++)
            {
                Vector2 way = new Vector2(facing * Main.rand.NextFloat(0.4f, 3.4f), -Main.rand.NextFloat(1.8f, 6f));

                PADParticleHandler.SpawnParticle(new SparkShard(cut + new Vector2(0f, -3f), way, Sparks[Main.rand.Next(Sparks.Length)], 5f, 9f,
                    Main.rand.NextFloat(10f, 16f), Main.rand.Next(8, 15), falls: true));
            }

            if (!breaks)
                return;

            Sound(SoundID.Dig, cut, -0.4f, 1f);
            Sound(SoundID.Item71, NPC.Center, -0.4f, 0.9f);
            Puff(cut, 12, 3.4f, 2.2f);
            PADParticleHandler.SpawnParticle(new BloomGeneric(cut, Vector2.Zero, Steel * 0.5f, 0.3f, 10));
            Quake(5f, 12);
        }

        /// <summary>
        /// The screen shaken for whoever is watching him: anybody near, as in the fight
        /// (Shake); and the player the passage is for, who has been put down too far off
        /// for that and sees him by the camera.
        /// </summary>
        private void Quake(float power, int ticks)
        {
            if (shot > 0f || Near())
                Main.LocalPlayer.PIS().BumpScreenShake(power, ticks);
        }

        /// <summary>
        /// One tile of the ground heaved up and let down again, if it is the top of the
        /// ground there. Drawn only: nothing of the world moves (LiftedTile).
        /// </summary>
        private void Heave(int column, int row, float height)
        {
            if (!WorldGen.InWorld(column, row, 2))
                return;

            Tile ground = Main.tile[column, row];
            Tile over = Main.tile[column, row - 1];

            bool top = ground.HasUnactuatedTile && Main.tileSolid[ground.TileType]
                && !(over.HasUnactuatedTile && Main.tileSolid[over.TileType] && !Main.tileSolidTop[over.TileType]);

            if (top)
                PADParticleHandler.SpawnParticle(new LiftedTile(new Point(column, row), 0, 16, height, facing, GroundWave.FogColour(column, row)));
        }
    }
}
