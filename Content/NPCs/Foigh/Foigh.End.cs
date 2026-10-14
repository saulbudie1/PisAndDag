using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using PisAndDag.Content.Subworlds;
using PisAndDag.Content.UI;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The end of it: what he does from the blow that would have killed him, on every
    /// machine; and what the player he fights is shown, asked and walked through, on their
    /// own machine. By request (on the 8th):
    ///
    ///   DYING    whatever he was doing, he stops, rocked by it. In the hall (EmptyHall)
    ///            he is gone, and stands at "the 4th columm at the end of the hall": the
    ///            fourth pilaster counted from the end wall (the one against it the first,
    ///            as where the player stood in the picture sent with it), his back to the
    ///            end, facing down the hall. Anywhere else he stays where he is.
    ///   SHOWN    by a later request, "fades and pans to him at the pillar before he do his
    ///            roar": the picture fades; the player is put down twenty tiles in front of
    ///            him; the picture comes back on them, and the camera goes over to him where
    ///            he stands, at the pillar in the hall, getting his breath.
    ///   ROAR     "a big long roar", two and a half seconds of it, red and black
    ///            (Cinematic.Roar), its voice as the camera gets to him; and in the hall it
    ///            "turns off all the torches except the ones in a streak above him": every
    ///            torch and Tiki Torch goes out as the roar reaches it, out along the hall
    ///            from him, all but those up his own pilaster.
    ///   FOLD     "At a slower pace": down onto his knees, his body over and his hands onto
    ///            the ground, and the sword falls in front of him.
    ///   BOWED    his head down, shaking and bleeding: the player walks up to five tiles
    ///            off him.
    ///   LAUGH    he looks up to them: "Ha ha ha ha... That was an interesting fight.",
    ///            and coughs blood, two to four times. (...)
    ///   ASKED    "It is true after all, ... wasn't I?" (Yes you were) (No you weren't)
    ///   KNEW     yes: "I knew it", coughing two to four times, "300 years of training".
    ///   MOUTH    no: "I did get you to bleed a few times despite your big mouth". (...)
    ///   CHOIR    "O, Guah! O, Yotsubba I shall join you... in singing in the choir". (...)
    ///   PLEAD    his back straight, his head up, his hands down, still on his knees:
    ///            "Please... chosen one. I have lived long enough..." (Finish) (Spare)
    ///   FINISH   the player takes the sword up from the ground and walks up to him;
    ///   THRUST   their bottom hand on his shoulder, the other drives the sword through him
    ///            thirty degrees down, his body square to it: rigid, "like in the anime
    ///            with the big ouch"; he looks at them: "Thank you".
    ///   CAST     the blade drawn out, the body cast down; it smoulders, goes to a pile of
    ///            ash, and the ash blows away (as the Guide goes, GuideSacrifice). The
    ///            picture to black, and back with Foigh's Rust Side Sword in the player's
    ///            inventory (a stand-in: FoighsRustSideSword).
    ///   SPARE    the player walks up, takes the sword, and throws it behind them; he looks
    ///            down; they walk out of the picture, and he is there, at a loss, ten
    ///            seconds.
    ///   SCORN    "What insolent moron..."; and
    ///   SLUMP    he is down on his face, and the picture goes to the loading screen as the
    ///            player leaves the place (SubworldPassage). Anywhere but a place, the
    ///            picture to black and back, and he is gone.
    ///
    /// Nothing lands on him from the first tick of it. The player is held for all of it
    /// but where they walk (FoighEndPlayer: their feet are walked for them then, and nothing
    /// touches them either), the bars in. The server waits for the player's word at every
    /// answer and every walk, and not for ever: never answered, he goes on; a question
    /// never answered is "No you weren't", and a choice never made is Spare (not choosing
    /// is the choice: PAD_Lore_Planning 9.2). Only the player he fights is moved and shown
    /// it; to anybody else he is simply doing it.
    ///
    /// The lines are the user's own words, kept as written; the bars in them are
    /// Dialogue.Pause, and "*cough blood range 2-4 times*" is his, not a word of it.
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // HIS LINES
        // ============================================================

        private static readonly string[] EndLines =
        {
            "Ha ha ha ha... That was an interesting fight.",
            "It is true after all, you|.|.|. mortal warrior|.|.|, chosen to disturb the House of Gods|.|.|. Well|,| I was pretty close, wasn't I?",
            "I knew it",
            " 300 years of training",
            "I did get you to bleed a few times despite your big mouth",
            "O|, Guah!| O|, Yotsubba I shall join you|.|.|. in singing in the choir",
            "Please|.|.|. chosen one. I have lived long enough|.|.|.",
            "Thank you",
            "What insolent moron|.|.|.",
        };

        private const int LineLaugh = 0;
        private const int LineAsked = 1;
        private const int LineKnew = 2;
        private const int LineYears = 3;
        private const int LineMouth = 4;
        private const int LineChoir = 5;
        private const int LinePlead = 6;
        private const int LineThanks = 7;
        private const int LineScorn = 8;

        /// <summary>The part of his first line that is laughing.</summary>
        private const string Laughter = "Ha ha ha ha";

        private const string YesLabel = "Yes you were";
        private const string NoLabel = "No you weren't";
        private const string FinishLabel = "Finish";
        private const string SpareLabel = "Spare";

        // ============================================================
        // THE END'S TICKS
        // ============================================================

        /// <summary>Dying: rocked by the blow (as a passage begins: M.Spent); then, on the ground, the step to the end of the hall, or the roar where he is. Still in the air after this many, all the same.</summary>
        private const int DyingRocks = 40;
        private const int DyingLongest = 150;

        /// <summary>The roar's strength (Cinematic.Roar: 1 is a boss's roar), and its voice (the game's roar, lower and so longer); and how fast the dark goes out from him along the hall, in pixels a tick (across the whole of it in a second).</summary>
        private const float RoarStrength = 1.5f;
        private const float RoarPitch = -0.45f;
        private const float SweepSpeed = 40f;

        /// <summary>
        /// Shown: the picture to black; the tick of the black the player is put down on;
        /// black until; back up over this long; the camera sets off for him; and the ticks it
        /// takes (to him, and back, all through his end). His roar begins so that its voice
        /// comes as the camera gets to him.
        /// </summary>
        private const int ShownFade = 24;
        private const int ShownCut = ShownFade + 3;
        private const int ShownBlack = ShownFade + 16;
        private const int ShownFadeIn = 30;
        private const int ShownPanAt = ShownBlack + ShownFadeIn + 12;
        private const int EndPan = 96;
        private const int ShownTicks = ShownPanAt + EndPan - M.RoarDraws;

        /// <summary>How far in front of him the player is put down, and how near they walk up, from his middle to theirs: twenty tiles and five, by request; no nearer than the least where there is no ground that far off. And how near a walk's end they are taken to have got there.</summary>
        public const float EndPutAt = 20f * 16f;
        public const float EndPutLeast = 8f * 16f;
        public const float EndNear = 5f * 16f;
        private const float EndCoast = 6f;

        /// <summary>The lean on him, from when the camera has set off for him to the end; and where it looks, from his feet, ahead of him and up (between him and the player, as he is down).</summary>
        private const float EndZoom = 1.3f;
        private static readonly Vector2 EndAim = new Vector2(20f, 34f);

        /// <summary>Bowed: the walk up never done, he goes on by himself.</summary>
        private const int BowedLongest = 900;

        /// <summary>His first line, once his head is up; any other, a moment into its own part; and a line never answered, he goes on by himself after this.</summary>
        private const int LaughSpeaks = M.LaughLooked + 8;
        private const int LineSpeaks = 10;
        private const int EndTalkLongest = 5400;

        /// <summary>The first cough after the line, or the words, it follows; and the answer, this long after the last.</summary>
        private const int CoughAfter = 10;
        private const int AnsweredAfter = 24;

        /// <summary>Plead: his last line, once he is up.</summary>
        private const int PleadSpeaks = M.UpTicks + 12;

        /// <summary>The sword taken up off the ground: the hand down to it by TakeReaches, it in the hand from TakeHolds, and up by TakeTicks.</summary>
        private const int TakeReaches = 12;
        private const int TakeHolds = 20;
        private const int TakeTicks = 30;

        /// <summary>Finish: the walk up to him never done, he goes on by himself.</summary>
        private const int FinishLongest = TakeTicks + 600;

        /// <summary>Thrust: "Thank you", once he has looked at them; how long it stays once all of it is there; and never read, when he goes on by himself.</summary>
        private const int ThanksSpeaks = M.ThrustLooked + 6;
        private const int ThanksLinger = 60;
        private const int ThrustLongest = ThanksSpeaks + 900;

        /// <summary>
        /// Cast: smoke off him from a little after he is down, more and more of it (as off
        /// the Guide, GuideSacrifice); the ash, which lies and is then blown away (by the end
        /// of CastFades, near enough all of it: AshLies, AshLifts, AshDrift's own life); the
        /// picture to black, black (the sword given under it, the flames lit again, the
        /// camera home), and back; and he is gone.
        /// </summary>
        private const int CastSmoulders = M.CastDown + 8;
        private const int CastAshes = CastSmoulders + 100;
        private const int CastFades = CastAshes + 200;
        private const int CastFadeOut = 24;
        private const int CastBlack = 20;
        private const int CastFadeIn = 30;
        private const int CastGives = CastFades + CastFadeOut + 4;
        private const int CastTicks = CastFades + CastFadeOut + CastBlack + CastFadeIn;

        /// <summary>Spare: the sword taken up (by TakeTicks), held up, and thrown (let go of at SpareThrows); the walk away, from SpareWalks.</summary>
        private const int SpareHolds = TakeTicks + 22;
        private const int SpareThrows = SpareHolds + 10;
        private const int SpareWalks = M.SpareBows + 10;

        /// <summary>Scorn: how long his line stays once all of it is there; and never read, when he goes on by himself.</summary>
        private const int ScornLinger = 50;
        private const int ScornLongest = LineSpeaks + 900;

        /// <summary>Slump: the picture to the loading screen as he is down (SubworldPassage); or, anywhere but a place, to black, black (he is gone from the picture under it), and back; and he is gone.</summary>
        private const int SlumpLeaves = M.SlumpDown;
        private const int SlumpFadeOut = 24;
        private const int SlumpBlack = 20;
        private const int SlumpFadeIn = 30;
        private const int SlumpTicks = SlumpLeaves + SlumpFadeOut + SlumpBlack + SlumpFadeIn;

        // ---- the sword, out of his hand ----

        /// <summary>
        /// Where it lies in front of him: its point this far ahead of his feet and this
        /// high, its grip toward the player, turned toward the eye (seen at this much of its
        /// length). So it lies between the two of them, its grip (82 pixels ahead of his
        /// feet) where the hand of a player standing five tiles off him (EndNear), facing
        /// him, comes down when they reach for it (TakeDown: two pixels behind the middle
        /// of them, the game's own sums, HandFrom).
        /// </summary>
        private const float LieTip = 38f;
        private const float LieUp = 3f;
        private const float LieReach = 0.5f;

        /// <summary>
        /// The thrust (Foigh.End, by request "bottom hand on his shoulder, and the other hand
        /// ... pierce the sword thru"). The sword is longer than the player is tall, so it is
        /// held by the blade: as it is drawn back, the hand this far up it from the grip
        /// (the point a little short of him); driven through, this far (the point well out
        /// of his back). And the arm: up as it is drawn back, forward as it is driven,
        /// measured from straight ahead, down positive.
        /// </summary>
        private const float StabDrawn = 76f;
        private const float StabThrough = 40f;
        private const float StabArmBack = -65f;
        private const float StabArmIn = -28f;

        /// <summary>The thrown sword: how fast it leaves the hand, back and up; its weight; how fast it turns over.</summary>
        private static readonly Vector2 ThrowSpeed = new Vector2(6.2f, -5.2f);
        private const float ThrowWeight = 0.3f;
        private const float ThrowTurn = 0.32f;

        // ============================================================
        // WHO
        // ============================================================

        /// <summary>At nothing: from the blow that would have killed him to the end of it.</summary>
        public bool Ending => OfEnd(State);

        /// <summary>Whether a move is a part of his end.</summary>
        private static bool OfEnd(Move move) => move == Move.Dying || move >= Move.Shown;

        /// <summary>Down on his knees (his legs into the ground: M.KneelRise), on his back or on his face: the tiles are drawn over what is in the ground (NPC.behindTiles).</summary>
        private bool EndLow => (State == Move.Fold && now >= M.FoldFalls) || (State > Move.Fold && State <= Move.Slump);

        /// <summary>The one of him at his end whose end is for this player, or null.</summary>
        internal static Foigh EndFor(int player)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is Foigh foigh && foigh.Ending && foigh.TargetIndex == player)
                    return foigh;
            }

            return null;
        }

        // ============================================================
        // WHERE THE WORLD IS DECIDED
        // ============================================================

        /// <summary>The flames of the hall his roar has still to put out, nearest first, each with how far it is from him: as the voice begins.</summary>
        private List<(Point At, float Far)> flames;

        /// <summary>
        /// The blow that would have killed him, where the world is decided (CheckDead; and
        /// by hand, /foigh end): he is left on one life, nothing lands on him, whoever he has
        /// hold of is let go of, and his end begins.
        /// </summary>
        private void Fell()
        {
            NPC.life = 1;
            NPC.dontTakeDamage = true;

            if (!Authority || Ending)
                return;

            Release();
            Go(Move.Dying);
        }

        /// <summary>
        /// By hand (/foigh end): his end, as the blow that would have killed him begins it.
        /// Null when it is done; why not when it is not.
        /// </summary>
        internal static string ToEnd()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is not Foigh foigh)
                    continue;

                if (foigh.Ending)
                    return "His end has begun already.";

                if (!foigh.Fighting)
                    return foigh.State <= Move.Begin ? "The fight has not begun: he is still on the road." : "It is over.";

                if (foigh.Passing)
                    return "He is between two phases: when the picture is back.";

                foigh.Fell();
                return null;
            }

            return "He is not here.";
        }

        /// <summary>A cough's count, where the world is decided: two to four, by request.</summary>
        private static float CoughCount() => Main.rand.Next(2, 5);

        /// <summary>A part of his end answered, or walked, on the machine of the player it is for (Word.Last): on to the next.</summary>
        private void EndAnswered(bool flag)
        {
            switch (State)
            {
                case Move.Bowed:
                    Go(Move.Laugh, CoughCount());
                    break;

                case Move.Laugh:
                    Go(Move.Asked);
                    break;

                case Move.Asked:
                    Go(flag ? Move.Knew : Move.Mouth, flag ? CoughCount() : 0f);
                    break;

                case Move.Knew:
                case Move.Mouth:
                    Go(Move.Choir);
                    break;

                case Move.Choir:
                    Go(Move.Plead);
                    break;

                case Move.Plead when Timer >= PleadSpeaks:
                    Go(flag ? Move.Finish : Move.Spare);
                    break;

                case Move.Finish when Timer >= TakeTicks:
                    Go(Move.Thrust);
                    break;

                case Move.Thrust when Timer >= ThanksSpeaks:
                    Go(Move.Cast);
                    break;

                case Move.Scorn when Timer >= LineSpeaks:
                    Go(Move.Slump);
                    break;
            }
        }

        /// <summary>Gone, where the world is decided: nothing off him (what he leaves is the end's own), nothing in the chat (NPC.PAD().GivesNothing); and the hall's flames lit again, however it ended.</summary>
        private void Perish()
        {
            EmptyHall.RelightHere();

            gone = true;
            NPC.PAD().GivesNothing = true;
            NPC.life = 0;
            NPC.checkDead();

            // checkDead sends nothing itself; this is how vanilla's own despawn tells the
            // clients (NPC.CheckActive: life 0, then SyncNPC).
            if (Main.netMode == NetmodeID.Server && !NPC.active)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
        }

        // ============================================================
        // EVERY MACHINE
        // ============================================================

        /// <summary>The tick of his end, every machine (the server moves him on).</summary>
        private void Ends()
        {
            Fall();
            NPC.velocity.X = State == Move.Dying ? NPC.velocity.X * 0.8f : 0f;

            // Spared: where the sword left the player's hand, and which way they threw it, as this machine saw it.
            if (State == Move.Spare && At(SpareThrows))
                Threw();

            if (!Authority)
                return;

            switch (State)
            {
                case Move.Dying:
                    if ((now < DyingRocks || !grounded) && now < DyingLongest)
                        break;

                    // His sword back in his hand, whatever he had thrown gone: as under the
                    // black of a passage.
                    Gather();

                    if (EmptyHall.EndColumn(out Vector2 column))
                    {
                        // Gone from where he is, and at the end of the hall, facing down it.
                        stepFrom = NPC.position;
                        stepTo = new Vector2(column.X - NPC.width * 0.5f, column.Y - NPC.height);
                        NPC.position = stepTo;
                        NPC.velocity = Vector2.Zero;

                        Face(EmptyHall.DownTheHall);
                        Go(Move.Shown, 1f);
                        break;
                    }

                    FaceThem(Target());
                    Go(Move.Shown);
                    break;

                case Move.Shown:
                    // Put down in front of him under the black (their own machine): he is
                    // turned to where they are, until the camera sets off for him. (In the
                    // hall they are where he faces: nothing turns.)
                    if (now > ShownCut + 2 && now <= ShownPanAt)
                        FaceThem(Target());

                    if (now >= ShownTicks)
                        Go(Move.Roar);
                    break;

                case Move.Roar:
                    Sweep();

                    if (now >= M.RoarTicks)
                        Go(Move.Fold);
                    break;

                case Move.Fold:
                    if (now >= M.FoldLets && hasSword)
                    {
                        hasSword = false;
                        NPC.netUpdate = true;
                    }

                    if (now >= M.FoldTicks)
                        Go(Move.Bowed);
                    break;

                case Move.Bowed:
                    if (now >= BowedLongest)
                        Go(Move.Laugh, CoughCount());
                    break;

                case Move.Laugh:
                    if (now >= EndTalkLongest)
                        Go(Move.Asked);
                    break;

                case Move.Asked:
                    if (now >= EndTalkLongest)
                        Go(Move.Mouth);
                    break;

                case Move.Knew:
                case Move.Mouth:
                    if (now >= EndTalkLongest)
                        Go(Move.Choir);
                    break;

                case Move.Choir:
                    if (now >= EndTalkLongest)
                        Go(Move.Plead);
                    break;

                case Move.Plead:
                    if (now >= PleadSpeaks + EndTalkLongest)
                        Go(Move.Spare);
                    break;

                case Move.Finish:
                    if (now >= FinishLongest)
                        Go(Move.Thrust);
                    break;

                case Move.Thrust:
                    if (now >= ThrustLongest)
                        Go(Move.Cast);
                    break;

                case Move.Cast:
                    // Under the black: the hall's flames lit again.
                    if (At(CastGives))
                        EmptyHall.RelightHere();

                    if (now >= CastTicks)
                        Perish();
                    break;

                case Move.Spare:
                    if (now >= M.SpareTicks)
                        Go(Move.Scorn);
                    break;

                case Move.Scorn:
                    if (now >= ScornLongest)
                        Go(Move.Slump);
                    break;

                case Move.Slump:
                    if (now >= SlumpTicks)
                        Perish();
                    break;
            }
        }

        /// <summary>
        /// His roar puts out the hall's flames, where the world is decided: from the voice
        /// on, each as the dark going out from him reaches it, but those up his own
        /// pilaster. Not anywhere but the hall.
        /// </summary>
        private void Sweep()
        {
            if (now < M.RoarDraws || !EmptyHall.Here(out EmptyHall hall))
                return;

            if (flames == null)
            {
                var all = new List<Point>();
                hall.Flames(all);

                flames = new List<(Point, float)>();

                Vector2 from = NPC.Center;
                int keep = hall.EndColumnX;

                foreach (Point at in all)
                {
                    if (at.X == keep || !EmptyHall.Lit(at))
                        continue;

                    flames.Add((at, Vector2.Distance(from, at.ToWorldCoordinates(8f, 8f))));
                }

                flames.Sort((a, b) => a.Far.CompareTo(b.Far));
            }

            float reach = (now - M.RoarDraws + 1) * SweepSpeed;
            int put = 0;

            while (put < flames.Count && flames[put].Far <= reach)
            {
                EmptyHall.SetLit(flames[put].At, false);
                Snuffed(flames[put].At);
                put++;
            }

            if (put > 0)
                flames.RemoveRange(0, put);
        }

        /// <summary>A flame put out, on a machine with a screen: a wisp of smoke off it.</summary>
        private static void Snuffed(Point at)
        {
            if (Main.dedServ)
                return;

            Vector2 world = at.ToWorldCoordinates(8f, 6f);

            for (int i = 0; i < 2; i++)
            {
                PADParticleHandler.SpawnParticle(new MuzzleSmoke(world + Main.rand.NextVector2Circular(2f, 2f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1.1f)), Main.rand.Next(30, 50), Main.rand.NextFloat(0.3f, 0.5f)));
            }
        }

        // ============================================================
        // THE PLAYER IT IS FOR
        // ============================================================

        /// <summary>The tick of the part he is in that its line went up on, here, from when its letters come (the panel coming up first, if it was down); less than none until it has.</summary>
        private int endLineAt = -1;

        /// <summary>The tick all of the line was there on, here; less than none before.</summary>
        private int endLineRead = -1;

        /// <summary>This part's answer is up; its word has gone to the server; the player has been put down; given the sword; taken out of the place; the camera put home.</summary>
        private bool endOffered;
        private bool endTold;
        private bool endPut;
        private bool endGiven;
        private bool endLeft;
        private bool endHome;

        /// <summary>The sword as it left the player's hand, spared: where, which way it was pointing, which way it went, the ground it comes down on; none before.</summary>
        private Vector2 thrownFrom;
        private float thrownBlade;
        private int thrownWay;
        private float thrownGround;
        private bool thrown;

        /// <summary>The sword as it left his hand, falling, as this machine saw it: its grip and the way it pointed.</summary>
        private Vector2 letGrip;
        private float letBlade;
        private bool letSeen;

        /// <summary>The first tick this machine sees of his end: nothing of it shown yet, nothing asked. Their own wounds of him stop with it (Ultra Bleeding), on their machine.</summary>
        private void EndBegins()
        {
            endLineAt = -1;
            endLineRead = -1;
            endOffered = false;
            endTold = false;
            endPut = false;
            endGiven = false;
            endLeft = false;
            endHome = false;
            goingOut = false;
            thrown = false;
            letSeen = false;
            flames = null;

            if (TargetIsHere)
            {
                Player me = Main.LocalPlayer;

                for (int wounds = 1; wounds <= UltraBleeding.Most; wounds++)
                    me.ClearBuff(UltraBleeding.TypeFor(wounds));
            }
        }

        /// <summary>A part of his end begun, as this machine sees it: its line not up yet, its word not sent.</summary>
        private void EndPart()
        {
            endLineAt = -1;
            endLineRead = -1;
            endOffered = false;
            endTold = false;
        }

        /// <summary>Where the camera looks while it is on him: between him and the player, as he is down.</summary>
        private Vector2 EndLook => Feet + new Vector2(facing * EndAim.X, -EndAim.Y);

        /// <summary>The scene for the player it is for, every tick of his end, on their machine.</summary>
        private void EndScene()
        {
            Player me = Main.LocalPlayer;

            // Nobody to show it to: everything asked for lets go by itself.
            if (me.dead || !me.active)
                return;

            bool wantShot = false;

            FoighEndPlayer.Ask(NPC);
            FoighEndPlayer.FaceHim(NPC);
            Cinematic.Frame();

            switch (State)
            {
                case Move.Dying:
                    Cinematic.Hold();
                    break;

                case Move.Shown:
                    Cinematic.Hold();

                    if (now < ShownFade)
                        Cinematic.Fade(Smooth(now / (float)ShownFade));
                    else if (now < ShownBlack)
                        Cinematic.Fade(1f);
                    else if (now < ShownBlack + ShownFadeIn)
                        Cinematic.Fade(1f - Smooth((now - ShownBlack) / (float)ShownFadeIn));

                    // Under the black: in front of him, twenty tiles off, on their feet; the
                    // camera on them, at once. (One who became his after the picture was back
                    // is left where they stand.)
                    if (!endPut && now >= ShownCut)
                    {
                        endPut = true;

                        if (now < ShownBlack)
                        {
                            shot = 0f;
                            me.GetModPlayer<FoighEndPlayer>().PutAhead(NPC, facing, EndPutAt, EndPutLeast);
                        }
                    }

                    if (now >= ShownPanAt)
                    {
                        wantShot = true;
                        Cinematic.Lean(EndZoom, 0.03f);
                    }
                    break;

                case Move.Roar:
                case Move.Fold:
                    Cinematic.Hold();
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);
                    break;

                case Move.Bowed:
                {
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);

                    // Up to five tiles off him, on the side of him they are on.
                    int side = me.Center.X >= Feet.X ? 1 : -1;
                    float to = Feet.X + side * EndNear;

                    if (Math.Abs(me.Center.X - to) > EndCoast)
                    {
                        FoighEndPlayer.WalkTo(to);
                    }
                    else if (!endTold)
                    {
                        endTold = true;
                        Tell(Word.Last, n: (int)Move.Bowed);
                    }
                    break;
                }

                case Move.Laugh:
                case Move.Asked:
                case Move.Knew:
                case Move.Mouth:
                case Move.Choir:
                case Move.Plead:
                    Cinematic.Hold();
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);
                    EndTalk();
                    break;

                case Move.Finish:
                {
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();

                    if (now < TakeTicks)
                    {
                        Cinematic.Hold();
                        break;
                    }

                    // Up to him, to where their hand will be on his shoulder.
                    float to = StabStand(me);

                    if (Math.Abs(me.Center.X - to) > EndCoast)
                    {
                        FoighEndPlayer.WalkTo(to);
                    }
                    else if (!endTold)
                    {
                        endTold = true;
                        Tell(Word.Last, n: (int)Move.Finish);
                    }
                    break;
                }

                case Move.Thrust:
                    Cinematic.Hold();
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);

                    if (!endTold && now >= ThanksSpeaks && Said(EndLines[LineThanks], ThanksLinger))
                    {
                        endTold = true;
                        Dialogue.Close();
                        Tell(Word.Last, n: (int)Move.Thrust);
                    }
                    break;

                case Move.Cast:
                {
                    Cinematic.Hold();

                    int dark = CastFades + CastFadeOut;
                    int back = dark + CastBlack;

                    wantShot = now < dark;

                    if (now < dark)
                        Cinematic.Lean(EndZoom, 0.03f);

                    if (now >= CastFades && now < dark)
                    {
                        Cinematic.Fade(Smooth((now - CastFades) / (float)CastFadeOut));
                    }
                    else if (now >= dark && now < back)
                    {
                        Cinematic.Fade(1f);

                        // Under the black: the camera home at once, and the sword theirs.
                        if (!endHome)
                        {
                            endHome = true;
                            shot = 0f;
                            PisPlayer.PanHome(EndLook, 8);
                        }

                        if (!endGiven && now >= CastGives)
                        {
                            endGiven = true;
                            me.QuickSpawnItem(new EntitySource_Gift(NPC), ModContent.ItemType<FoighsRustSideSword>(), 1);
                        }
                    }
                    else if (now >= back)
                    {
                        Cinematic.Fade(1f - Smooth((now - back) / (float)CastFadeIn));
                    }
                    break;
                }

                case Move.Spare:
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();

                    // Away from him, out of the picture, their back to him.
                    if (now >= SpareWalks)
                        FoighEndPlayer.WalkTo(me.Center.X + (me.Center.X >= Feet.X ? 1 : -1) * 4000f);
                    else
                        Cinematic.Hold();
                    break;

                case Move.Scorn:
                    Cinematic.Hold();
                    FoighEndPlayer.FaceHim(null);
                    wantShot = true;
                    Cinematic.Lean(EndZoom, 0.03f);

                    if (!endTold && now >= LineSpeaks && Said(EndLines[LineScorn], ScornLinger))
                    {
                        endTold = true;
                        Dialogue.Close();
                        Tell(Word.Last, n: (int)Move.Scorn);
                    }
                    break;

                case Move.Slump:
                {
                    Cinematic.Hold();
                    FoighEndPlayer.FaceHim(null);

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();

                    // As he is down: out of the place, by its own black and its loading screen.
                    if (!endLeft && now >= SlumpLeaves)
                    {
                        endLeft = true;
                        goingOut = Subworld.Active && SubworldPassage.Leave(Feet);
                    }

                    if (goingOut)
                    {
                        wantShot = true;
                        Cinematic.Lean(EndZoom, 0.03f);
                        break;
                    }

                    // Anywhere else: to black, the camera home, and back.
                    int dark = SlumpLeaves + SlumpFadeOut;
                    int back = dark + SlumpBlack;

                    wantShot = now < dark;

                    if (now < dark)
                        Cinematic.Lean(EndZoom, 0.03f);

                    if (now >= SlumpLeaves && now < dark)
                    {
                        Cinematic.Fade(Smooth((now - SlumpLeaves) / (float)SlumpFadeOut));
                    }
                    else if (now >= dark && now < back)
                    {
                        Cinematic.Fade(1f);

                        if (!endHome)
                        {
                            endHome = true;
                            shot = 0f;
                            PisPlayer.PanHome(EndLook, 8);
                        }
                    }
                    else if (now >= back)
                    {
                        Cinematic.Fade(1f - Smooth((now - back) / (float)SlumpFadeIn));
                    }
                    break;
                }
            }

            shot = MathHelper.Clamp(shot + (wantShot ? 1f : -1f) / EndPan, 0f, 1f);

            if (shot > 0f)
                PisPlayer.BlendCamera(EndLook, shot * shot * (3f - 2f * shot));
        }

        /// <summary>Leaving the place by the passage: this machine has asked it to, and it has begun.</summary>
        private bool goingOut;

        /// <summary>
        /// His lines, with their answers, on the panel, each in the part it is said in: put
        /// up (and again, if the panel has gone down under it before it was answered); with
        /// the coughs that go with the first and with "I knew it" let finish before the
        /// answer comes up; the answer tells the server.
        /// </summary>
        private void EndTalk()
        {
            if (endTold)
                return;

            int at = State == Move.Laugh ? LaughSpeaks : State == Move.Plead ? PleadSpeaks : LineSpeaks;

            if (now < at)
                return;

            // They have been fighting him: his face and his name are theirs already.
            DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

            if (endLineAt >= 0 && !Dialogue.Speaking(NPC))
            {
                // Gone down under him (something else took it): up again.
                endLineAt = -1;
                endOffered = false;
            }

            if (endLineAt < 0)
            {
                bool open = Dialogue.Open;

                switch (State)
                {
                    case Move.Laugh:
                        Dialogue.Converse(NPC, EndLines[LineLaugh], Pace);
                        break;

                    case Move.Asked:
                        Dialogue.Converse(NPC, EndLines[LineAsked], Pace);
                        Dialogue.OfferCorners(Answer(YesLabel, true), Answer(NoLabel, false));
                        endOffered = true;
                        break;

                    case Move.Knew:
                        Dialogue.Converse(NPC, KnewLine((int)Arg), Pace);
                        break;

                    case Move.Mouth:
                        Dialogue.Converse(NPC, EndLines[LineMouth], Pace, Answer(Silence, false));
                        endOffered = true;
                        break;

                    case Move.Choir:
                        Dialogue.Converse(NPC, EndLines[LineChoir], Pace, Answer(Silence, false));
                        endOffered = true;
                        break;

                    case Move.Plead:
                        Dialogue.Converse(NPC, EndLines[LinePlead], Pace);
                        Dialogue.OfferCorners(Answer(FinishLabel, true), Answer(SpareLabel, false));
                        endOffered = true;
                        break;
                }

                endLineAt = now + (open ? 0 : Dialogue.FadeTicks);
                return;
            }

            // The first line and "I knew it": the answer once the coughs are done.
            if (!endOffered && (State == Move.Laugh || State == Move.Knew) && now >= AnswerAt())
            {
                endOffered = true;
                Dialogue.Offer(Answer(Silence, false));
            }
        }

        /// <summary>An answer: the line left up with nothing under it, and the server told which.</summary>
        private TalkOption Answer(string label, bool flag)
        {
            Move part = State;

            return new TalkOption(label, _ =>
            {
                if (endTold || State != part)
                    return;

                endTold = true;
                Dialogue.Offer();
                Tell(Word.Last, n: (int)part, flag: flag);
            })
            { Remembered = false };
        }

        /// <summary>
        /// A line with nothing under it: put up (and again, its panel gone down under it
        /// before it was read), and true once all of it has been there this long.
        /// </summary>
        private bool Said(string text, int linger)
        {
            if (endLineAt < 0 || !Dialogue.Speaking(NPC))
            {
                DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

                bool open = Dialogue.Open;

                Dialogue.Converse(NPC, text, Pace);
                endLineAt = now + (open ? 0 : Dialogue.FadeTicks);
                endLineRead = -1;
                return false;
            }

            if (endLineRead < 0)
            {
                if (Dialogue.LineDone)
                    endLineRead = now;

                return false;
            }

            return now - endLineRead >= linger;
        }

        // ---- the lines' own clocks: his laugh, his coughs, the answer after them ----

        /// <summary>The ticks the panel takes to bring this much of a line up at his pace, its bars and all (as Dialogue brings a line up: a letter's time each character, Dialogue.PauseTicks each bar).</summary>
        private static float Typed(string text)
        {
            float letters = 0f;
            int bars = 0;

            foreach (char c in text)
            {
                if (c == Dialogue.Pause)
                    bars++;
                else
                    letters++;
            }

            return letters / Pace + bars * Dialogue.PauseTicks;
        }

        /// <summary>His "I knew it" line: the words, a pause as long as his coughs take, and the rest.</summary>
        private static string KnewLine(int coughs)
        {
            int bars = (int)Math.Ceiling(((Math.Max(1, coughs) - 1) * M.EndCoughEvery + M.EndCoughTicks + 12) / (float)Dialogue.PauseTicks);

            return EndLines[LineKnew] + new string(Dialogue.Pause, bars) + EndLines[LineYears];
        }

        /// <summary>The tick its line's letters began on, here: as it went up on this machine, or, where it is not shown, when it would have.</summary>
        private float EndLineFrom()
        {
            if (endLineAt >= 0)
                return endLineAt;

            // The first line opens the panel (it comes up first); the rest are put up on it.
            return State == Move.Laugh ? LaughSpeaks + Dialogue.FadeTicks : LineSpeaks;
        }

        /// <summary>His first cough in the part he is in, on this machine's clock of it: after his first line, or after "I knew it". None in any other.</summary>
        private float CoughFrom()
        {
            if (State == Move.Laugh)
                return EndLineFrom() + Typed(EndLines[LineLaugh]) + CoughAfter;

            if (State == Move.Knew)
                return EndLineFrom() + Typed(EndLines[LineKnew]) + 6f;

            return -1f;
        }

        /// <summary>How many coughs there are in the part he is in.</summary>
        private int CoughsHere => State == Move.Laugh || State == Move.Knew ? Math.Clamp((int)Arg, 0, 4) : 0;

        /// <summary>When the answer comes up under the first line, or "I knew it": the coughs done, and the line all there.</summary>
        private float AnswerAt()
        {
            float from = CoughFrom();
            float coughs = from + (Math.Max(1, CoughsHere) - 1) * M.EndCoughEvery + M.EndCoughTicks + AnsweredAfter;
            float line = State == Move.Knew ? EndLineFrom() + Typed(KnewLine(CoughsHere)) + 10f : coughs;

            return Math.Max(coughs, line);
        }

        /// <summary>His laugh at this tick of his first line: from its first letter to the end of the laughing.</summary>
        private float LaughNow(float tick)
        {
            if (State != Move.Laugh)
                return 0f;

            float from = EndLineFrom();

            return M.Laughing(tick, from, from + Typed(Laughter));
        }

        /// <summary>How hard a cough bends him at this tick of the part he is in.</summary>
        private float CoughNow(float tick)
        {
            int count = CoughsHere;

            return count > 0 ? M.Coughing(tick, CoughFrom(), count) : 0f;
        }

        // ============================================================
        // THE PLAYER'S HANDS, AND THE SWORD OUT OF HIS HAND
        // ============================================================

        /// <summary>The player's arms as his end has them: each, whether it is moved, how far it reaches, and its turn (Player.SetCompositeArmFront and Back).</summary>
        internal struct EndHands
        {
            public bool Front;
            public Player.CompositeArmStretchAmount FrontStretch;
            public float FrontRotation;
            public bool Back;
            public Player.CompositeArmStretchAmount BackStretch;
            public float BackRotation;
        }

        /// <summary>The sword out of his hand, as it is drawn: whether it is seen; its grip in the world; the way its blade points (a world angle); how much of its length is seen; whether it is through him (drawn under his body).</summary>
        private struct EndSword
        {
            public bool Shown;
            public Vector2 Grip;
            public float Blade;
            public float Reach;
            public bool Through;
        }

        /// <summary>A world angle (clockwise from +x) for a turn from straight ahead of one facing this way, in degrees, down positive (straight up is -90, straight back 180).</summary>
        private static float AheadOf(int way, float down)
        {
            float a = MathHelper.ToRadians(down);

            return way >= 0 ? a : MathHelper.Pi - a;
        }

        /// <summary>From one world angle to another, this share of the way, the short way round.</summary>
        private static float Between(float from, float to, float t) => from + MathHelper.WrapAngle(to - from) * t;

        /// <summary>A composite arm's own turn, for an arm pointing this world angle (Player.SetCompositeArmFront: nothing turned is straight down).</summary>
        private static float ArmTurn(float world) => world - MathHelper.PiOver2;

        /// <summary>
        /// Where a player's hand is from the middle of them (Player.MountedCenter), its arm
        /// pointing this world angle, for the way they face: the game's own sums
        /// (Player.GetFrontHandPosition and GetBackHandPosition, 1.4.4), here so that where
        /// they will be can be worked out before they are there.
        /// </summary>
        private static Vector2 HandFrom(int way, bool front, Player.CompositeArmStretchAmount stretch, float world)
        {
            Vector2 along = new Vector2((float)Math.Cos(world), (float)Math.Sin(world));

            if (front)
            {
                float length = stretch == Player.CompositeArmStretchAmount.Full ? 10f
                    : stretch == Player.CompositeArmStretchAmount.None ? 4f
                    : stretch == Player.CompositeArmStretchAmount.Quarter ? 6f
                    : 8f;

                Vector2 bend = new Vector2(0f, way == -1 ? -3f : 3f).RotatedBy(world);

                return along * length + (way == -1 ? new Vector2(4f, -2f) : new Vector2(-4f, -2f)) + bend;
            }

            Vector2 reach = stretch == Player.CompositeArmStretchAmount.Full ? new Vector2(10f, 12f)
                : stretch == Player.CompositeArmStretchAmount.None ? new Vector2(4f, 6f)
                : stretch == Player.CompositeArmStretchAmount.Quarter ? new Vector2(6f, 8f)
                : new Vector2(8f, 10f);

            return along * reach + (way == -1 ? new Vector2(-6f, -2f) : new Vector2(6f, -2f));
        }

        /// <summary>The back arm's shoulder, from the middle of a player facing this way (the same sums).</summary>
        private static Vector2 BackShoulder(int way) => way == -1 ? new Vector2(-6f, -2f) : new Vector2(6f, -2f);

        /// <summary>How far an arm reaches for a hand this far from its shoulder: no further than it must, all of it past that.</summary>
        private static Player.CompositeArmStretchAmount Reaching(float far)
        {
            return far >= 11f ? Player.CompositeArmStretchAmount.Full
                : far >= 8.5f ? Player.CompositeArmStretchAmount.ThreeQuarters
                : far >= 6.5f ? Player.CompositeArmStretchAmount.Quarter
                : Player.CompositeArmStretchAmount.None;
        }

        /// <summary>His chest, in the world, as he is shown: a little over halfway from his hips up to his neck.</summary>
        private Vector2 Chest => Vector2.Lerp(frame.Hips, frame.NeckAt, 0.55f);

        /// <summary>His shoulder on the side of him the player is on (the far one, on his front), in the world, as he is shown.</summary>
        private Vector2 FrontShoulder
        {
            get
            {
                Vector2 local = FoighRig.Turned(FoighRig.FarShoulder, pose.Lean);

                return new Vector2(Feet.X + facing * local.X * frame.Turn, frame.Hips.Y + local.Y);
            }
        }

        /// <summary>
        /// Where the player stands for the thrust, the middle of them (x): their hand, as it
        /// drives the sword, a little over his chest and short of it, so that the blade from
        /// it to his chest goes down at the slant asked for (M.ThrustSlant), his body square to
        /// it. From there their bottom hand is on his shoulder.
        /// </summary>
        private float StabStand(Player me)
        {
            int way = me.Center.X >= Feet.X ? -1 : 1;

            FoighRig.Frame through = FoighRig.Build(M.Thrust(M.ThrustThrough), Feet, facing);
            Vector2 chest = Vector2.Lerp(through.Hips, through.NeckAt, 0.55f);
            Vector2 hand = HandFrom(way, true, Player.CompositeArmStretchAmount.Full, AheadOf(way, StabArmIn));

            float handY = Feet.Y - me.height * 0.5f + hand.Y;
            float across = Math.Max(4f, chest.Y - handY) / (float)Math.Tan(MathHelper.ToRadians(M.ThrustSlant));

            return chest.X - way * across - hand.X;
        }

        /// <summary>Where the sword lies in front of him once it has fallen: its point by his hands, its grip toward the player, turned toward the eye.</summary>
        private EndSword Lying()
        {
            Vector2 point = Feet + new Vector2(facing * LieTip, -LieUp);

            return new EndSword
            {
                Shown = true,
                Grip = point + new Vector2(facing * FoighRig.GripToTip * LieReach, 0f),
                Blade = facing > 0 ? MathHelper.Pi : 0f,
                Reach = LieReach,
            };
        }

        /// <summary>The sword falling out of his hand, at this tick of the fold: from where his hand let it go to where it lies.</summary>
        private EndSword Dropping(float tick)
        {
            EndSword lying = Lying();

            if (tick >= M.FoldLands)
                return lying;

            // Not seen let go of here: from where the pose has it as he lets go.
            if (!letSeen)
            {
                FoighRig.Frame at = FoighRig.Build(M.Folding(M.FoldLets - 1), Feet, facing);

                letGrip = at.Grip;
                letBlade = at.Blade;
                letSeen = true;
            }

            float k = Easing.Apply(Ease.PolyIn, MathHelper.Clamp((tick - M.FoldLets) / (M.FoldLands - M.FoldLets), 0f, 1f), 2);

            return new EndSword
            {
                Shown = true,
                Grip = Vector2.Lerp(letGrip, lying.Grip, k),
                Blade = Between(letBlade, lying.Blade, k),
                Reach = MathHelper.Lerp(1f, LieReach, k),
            };
        }

        /// <summary>
        /// Taken up off the ground, and carried: the hand down to the grip before their feet
        /// (by TakeReaches), the sword in it (TakeHolds), and up onto their shoulder, its blade
        /// back over it (by TakeTicks), all of its length seen.
        /// </summary>
        private void Taking(Player p, int way, float tick, ref EndHands hands, ref EndSword sword)
        {
            float hang = MathHelper.PiOver2;
            float down = AheadOf(way, TakeDown);
            float carried = AheadOf(way, CarryArm);

            float arm = tick < TakeReaches ? Between(hang, down, Easing.Smoother(tick / TakeReaches))
                : tick < TakeHolds ? down
                : Between(down, carried, Easing.Smoother(MathHelper.Clamp((tick - TakeHolds) / (TakeTicks - TakeHolds), 0f, 1f)));

            hands.Front = true;
            hands.FrontStretch = Player.CompositeArmStretchAmount.Full;
            hands.FrontRotation = ArmTurn(arm);

            Vector2 hand = p.MountedCenter + HandFrom(way, true, hands.FrontStretch, arm);
            EndSword lying = Lying();

            float k = Easing.Smoother(MathHelper.Clamp((tick - TakeReaches) / (TakeTicks - TakeReaches), 0f, 1f));

            sword.Shown = true;
            sword.Grip = Vector2.Lerp(lying.Grip, hand, k);
            sword.Blade = Between(lying.Blade, AheadOf(way, CarryBlade), k);
            sword.Reach = MathHelper.Lerp(LieReach, 1f, k);
        }

        /// <summary>The arm reaching down for the sword on the ground (from straight ahead, down positive). Carried: the hand at the chest, the blade back over the shoulder.</summary>
        private const float TakeDown = 62f;
        private const float CarryArm = 20f;
        private const float CarryBlade = -120f;

        /// <summary>
        /// The thrust. Their bottom hand onto his shoulder (by M.ThrustHand). The other, the
        /// sword lifted off their shoulder and turned on him, the hand run up the blade toward
        /// the point (StabDrawn), the arm up, the point at his chest; and driven (M.ThrustIn
        /// to M.ThrustThrough), the arm forward, the hand back down the blade to StabThrough,
        /// through him and out of his back; and held there.
        /// </summary>
        private void Stabbing(Player p, int way, float tick, ref EndHands hands, ref EndSword sword)
        {
            Vector2 middle = p.MountedCenter;

            Vector2 shoulder = middle + BackShoulder(way);
            Vector2 his = FrontShoulder;

            hands.Back = true;
            hands.BackStretch = Reaching(Vector2.Distance(his, shoulder));
            hands.BackRotation = ArmTurn(Between(MathHelper.PiOver2, (his - shoulder).ToRotation(), Easing.Smoother(MathHelper.Clamp(tick / M.ThrustHand, 0f, 1f))));

            float armDegrees;
            float slide;

            if (tick < M.ThrustHand)
            {
                float k = Easing.Smoother(tick / M.ThrustHand);

                armDegrees = MathHelper.Lerp(CarryArm, StabArmBack, k);
                slide = MathHelper.Lerp(0f, StabDrawn, k);
            }
            else if (tick < M.ThrustIn)
            {
                armDegrees = StabArmBack;
                slide = StabDrawn;
            }
            else if (tick < M.ThrustThrough)
            {
                float k = Easing.Apply(Ease.PolyOut, (tick - M.ThrustIn) / (M.ThrustThrough - M.ThrustIn), 3);

                armDegrees = MathHelper.Lerp(StabArmBack, StabArmIn, k);
                slide = MathHelper.Lerp(StabDrawn, StabThrough, k);
            }
            else
            {
                armDegrees = StabArmIn;
                slide = StabThrough;
            }

            float arm = AheadOf(way, armDegrees);

            hands.Front = true;
            hands.FrontStretch = Player.CompositeArmStretchAmount.Full;
            hands.FrontRotation = ArmTurn(arm);

            Vector2 hand = middle + HandFrom(way, true, hands.FrontStretch, arm);
            float aim = (Chest - hand).ToRotation();
            float blade = tick < M.ThrustHand ? Between(AheadOf(way, CarryBlade), aim, Easing.Smoother(tick / M.ThrustHand)) : aim;

            sword.Shown = true;
            sword.Blade = blade;
            sword.Grip = hand - blade.ToRotationVector2() * slide;
            sword.Reach = 1f;
            sword.Through = tick >= M.ThrustIn;
        }

        /// <summary>
        /// The cast. The blade drawn out of him (to M.CastOut), the hand back up it and the
        /// arm up; their bottom hand shoving him over (M.CastOut to M.CastShoved), and down;
        /// and the hand back to the grip, the sword back on their shoulder.
        /// </summary>
        private void Withdrawing(Player p, int way, float tick, ref EndHands hands, ref EndSword sword)
        {
            Vector2 middle = p.MountedCenter;
            Vector2 shoulder = middle + BackShoulder(way);

            // The bottom hand on his shoulder; out straight at him as it shoves; down.
            float toHim = (FrontShoulder - shoulder).ToRotation();
            float shove = AheadOf(way, -10f);
            float hang = MathHelper.PiOver2;

            float back = tick < M.CastOut ? toHim
                : tick < M.CastShoved ? Between(toHim, shove, Easing.Smoother((tick - M.CastOut) / (M.CastShoved - M.CastOut)))
                : Between(shove, hang, Easing.Smoother(MathHelper.Clamp((tick - M.CastShoved) / 16f, 0f, 1f)));

            hands.Back = true;
            hands.BackStretch = tick < M.CastOut ? Reaching(Vector2.Distance(FrontShoulder, shoulder)) : Player.CompositeArmStretchAmount.Full;
            hands.BackRotation = ArmTurn(back);

            float armDegrees;
            float slide;
            float blade;

            Vector2 Hand(float degrees) => middle + HandFrom(way, true, Player.CompositeArmStretchAmount.Full, AheadOf(way, degrees));

            if (tick < M.CastOut)
            {
                float k = Easing.Smoother(tick / M.CastOut);

                armDegrees = MathHelper.Lerp(StabArmIn, StabArmBack, k);
                slide = MathHelper.Lerp(StabThrough, StabDrawn, k);
                blade = (Chest - Hand(armDegrees)).ToRotation();
            }
            else
            {
                float k = Easing.Smoother(MathHelper.Clamp((tick - M.CastOut) / 30f, 0f, 1f));

                armDegrees = MathHelper.Lerp(StabArmBack, CarryArm, k);
                slide = MathHelper.Lerp(StabDrawn, 0f, k);
                blade = Between(AheadOf(way, 38f), AheadOf(way, CarryBlade), k);
            }

            float arm = AheadOf(way, armDegrees);

            hands.Front = true;
            hands.FrontStretch = Player.CompositeArmStretchAmount.Full;
            hands.FrontRotation = ArmTurn(arm);

            Vector2 hand = middle + HandFrom(way, true, hands.FrontStretch, arm);

            sword.Shown = true;
            sword.Blade = blade;
            sword.Grip = hand - blade.ToRotationVector2() * slide;
            sword.Reach = 1f;
            sword.Through = tick < M.CastOut * 0.6f;
        }

        /// <summary>
        /// Spared: taken up as for the thrust (by TakeTicks); held up before them, its point
        /// at the sky (SpareHolds); thrown, the arm over their head and back (let go of at
        /// SpareThrows); and the arm on round, behind them and down, and theirs again.
        /// </summary>
        private void Sparing(Player p, int way, float tick, ref EndHands hands, ref EndSword sword)
        {
            if (tick < TakeTicks)
            {
                Taking(p, way, tick, ref hands, ref sword);
                return;
            }

            float armDegrees;
            float bladeDegrees;

            if (tick < SpareHolds)
            {
                float k = Easing.Smoother(MathHelper.Clamp((tick - TakeTicks) / 14f, 0f, 1f));

                armDegrees = MathHelper.Lerp(CarryArm, -60f, k);
                bladeDegrees = MathHelper.Lerp(CarryBlade, -88f, k);
            }
            else if (tick < SpareThrows)
            {
                float k = Easing.Apply(Ease.PolyIn, (tick - SpareHolds) / (SpareThrows - SpareHolds), 2);

                armDegrees = MathHelper.Lerp(-60f, -165f, k);
                bladeDegrees = MathHelper.Lerp(-88f, -200f, k);
            }
            else
            {
                float k = Easing.Smoother(MathHelper.Clamp((tick - SpareThrows) / SpareFollows, 0f, 1f));

                armDegrees = MathHelper.Lerp(-165f, -270f, k);
                bladeDegrees = 0f;
            }

            float arm = AheadOf(way, armDegrees);

            hands.Front = true;
            hands.FrontStretch = Player.CompositeArmStretchAmount.Full;
            hands.FrontRotation = ArmTurn(arm);

            if (tick >= SpareThrows)
            {
                hands.Front = tick < SpareThrows + SpareFollows;
                sword = Flying(tick);
                return;
            }

            float blade = AheadOf(way, bladeDegrees);

            sword.Shown = true;
            sword.Grip = p.MountedCenter + HandFrom(way, true, hands.FrontStretch, arm);
            sword.Blade = blade;
            sword.Reach = 1f;
        }

        /// <summary>The ticks the arm takes on round after the sword has left it.</summary>
        private const float SpareFollows = 14f;

        /// <summary>
        /// Spared, the tick it is let go of, every machine: where it leaves their hand, which
        /// way it points, which way it goes (behind them), and the ground it comes down on
        /// (his, or whatever is under where it comes down, if that is not level with it).
        /// </summary>
        private void Threw()
        {
            Player p = Target();

            if (p == null)
                return;

            int way = p.direction;
            EndHands hands = default;
            EndSword sword = default;

            Sparing(p, way, SpareThrows - 0.001f, ref hands, ref sword);

            thrownFrom = sword.Grip;
            thrownBlade = sword.Blade;
            thrownWay = -way;
            thrownGround = Feet.Y;
            thrown = true;

            float x = thrownFrom.X + thrownWay * ThrowSpeed.X * Lands();

            if (GroundUnder(x, Feet.Y - 48f, out float ground))
                thrownGround = ground;
        }

        /// <summary>The top of the first thing that can be stood on in this column, from this high down, sixteen tiles at most.</summary>
        private static bool GroundUnder(float x, float fromY, out float ground)
        {
            ground = 0f;

            int i = (int)(x / 16f);

            if (i < 1 || i >= Main.maxTilesX - 1)
                return false;

            int first = Math.Max(1, (int)(fromY / 16f));
            int last = Math.Min(Main.maxTilesY - 2, first + 16);

            for (int j = first; j <= last; j++)
            {
                Tile tile = Main.tile[i, j];

                if (tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                {
                    ground = j * 16f;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Thrown, at this tick of the part: through the air, turning over; and from where it comes down, lying there.</summary>
        private EndSword Flying(float tick)
        {
            if (!thrown)
                return default;

            float since = Math.Max(0f, tick - SpareThrows);
            float lands = Lands();

            Vector2 speed = new Vector2(thrownWay * ThrowSpeed.X, ThrowSpeed.Y);
            float s = Math.Min(since, lands);

            Vector2 grip = thrownFrom + speed * s + new Vector2(0f, ThrowWeight * 0.5f * s * s);
            float blade = thrownBlade + thrownWay * ThrowTurn * s;

            // Down: flat on the ground, which way it was more nearly pointing.
            if (since >= lands)
                blade = Math.Cos(blade) >= 0.0 ? 0f : MathHelper.Pi;

            return new EndSword { Shown = true, Grip = grip, Blade = blade, Reach = 1f };
        }

        /// <summary>The ticks after it leaves the hand that the thrown sword is down on its ground.</summary>
        private float Lands()
        {
            float drop = thrownGround - LieUp - thrownFrom.Y;
            float up = -ThrowSpeed.Y;

            return (up + (float)Math.Sqrt(up * up + 2f * ThrowWeight * Math.Max(0f, drop))) / ThrowWeight;
        }

        /// <summary>
        /// The player's hands and the sword out of his hand as his end has them at this tick
        /// of the part he is in, on every machine, from the player as they stand.
        /// </summary>
        private void Choreo(Player p, float tick, out EndHands hands, out EndSword sword)
        {
            hands = default;
            sword = default;

            int way = p != null ? p.direction : -facing;

            switch (State)
            {
                case Move.Fold:
                    if (tick >= M.FoldLets)
                        sword = Dropping(tick);
                    break;

                case Move.Bowed:
                case Move.Laugh:
                case Move.Asked:
                case Move.Knew:
                case Move.Mouth:
                case Move.Choir:
                case Move.Plead:
                    sword = Lying();
                    break;

                case Move.Finish:
                    if (p == null)
                        sword = Lying();
                    else
                        Taking(p, way, Math.Min(tick, TakeTicks), ref hands, ref sword);
                    break;

                case Move.Thrust:
                    if (p != null)
                        Stabbing(p, way, tick, ref hands, ref sword);
                    break;

                case Move.Cast:
                    if (p != null)
                        Withdrawing(p, way, tick, ref hands, ref sword);
                    break;

                case Move.Spare:
                    if (p != null)
                        Sparing(p, way, tick, ref hands, ref sword);
                    else
                        sword = tick < SpareThrows ? Lying() : Flying(tick);
                    break;

                case Move.Scorn:
                case Move.Slump:
                    sword = Flying(float.MaxValue);
                    break;
            }
        }

        /// <summary>
        /// The player's hands, if his end has them this tick: for FoighEndPlayer, on every
        /// machine. A player's tick comes before his: the tick of the part he is in that is
        /// to come (what his clock stands at now), which is the one he is then drawn at.
        /// </summary>
        internal bool EndHandsFor(Player p, out EndHands hands)
        {
            hands = default;

            if (!Ending || p == null || p.whoAmI != TargetIndex)
                return false;

            Choreo(p, Clock, out hands, out _);

            return hands.Front || hands.Back;
        }

        // ============================================================
        // HOW HE HOLDS HIMSELF
        // ============================================================

        /// <summary>
        /// How he holds himself at this tick of a part of his end (Foigh.Fight's PoseOf asks):
        /// rocked by the blow, as a passage begins (from the pose he was in: Entered); getting
        /// his breath while he is shown; his roar; down; bowed; his head up to them, laughing
        /// and coughing; up on his knees; the sword through him; cast down; left there; down
        /// on his face.
        /// </summary>
        private FoighRig.Pose EndPose(Move move, float t)
        {
            switch (move)
            {
                case Move.Dying:
                    return (spent ??= M.Spent(M.Drag)).Sample(t);

                case Move.Shown:
                    return M.Showing(t, ShownTicks);

                case Move.Roar:
                    return M.Roaring(t);

                case Move.Fold:
                    return M.Folding(t);

                case Move.Bowed:
                    return M.Bowing(t);

                case Move.Laugh:
                    return M.Raised(t, t / M.LaughLooked, LaughNow(t), CoughNow(t));

                case Move.Knew:
                    return M.Raised(t, 1f, 0f, CoughNow(t));

                case Move.Asked:
                case Move.Mouth:
                case Move.Choir:
                    return M.Raised(t, 1f, 0f, 0f);

                case Move.Plead:
                    return M.Rising(t);

                // On his knees as he rose, while they take the sword up and come to him.
                case Move.Finish:
                    return M.Rising(M.UpTicks + 20f + t);

                case Move.Thrust:
                    return M.Thrust(t);

                case Move.Cast:
                    return M.Casting(t);

                case Move.Spare:
                    return M.Spared(t);

                // As the ten seconds left him.
                case Move.Scorn:
                    return M.Spared(M.SpareTicks + t);

                case Move.Slump:
                    return M.Slumping(t);

                default:
                    return M.Drag;
            }
        }

        // ============================================================
        // WHAT IT LOOKS AND SOUNDS LIKE
        // ============================================================

        /// <summary>Every tick, every machine, once he has been shown for it: where his hand lets the sword go, as it is drawn here.</summary>
        private void EndShow()
        {
            if (State == Move.Fold && now < M.FoldLets && frame.HasSword)
            {
                letGrip = frame.Grip;
                letBlade = frame.Blade;
                letSeen = true;
            }
        }

        /// <summary>He is not drawn: ash, or gone from the picture under the black at the last.</summary>
        private bool EndHidden => (State == Move.Cast && clock >= CastAshes)
            || (State == Move.Slump && !goingOut && clock >= SlumpLeaves + SlumpFadeOut);

        /// <summary>What a part of his end looks and sounds like as it begins, the tick this machine sees it.</summary>
        private void EndEntered()
        {
            Vector2 feet = Feet;

            switch (State)
            {
                case Move.Dying:
                    // The blow that would have killed him, and him rocked by it.
                    Sound(SoundID.NPCHit1, NPC.Center, -0.6f, 1f);
                    Sound(SoundID.NPCDeath1, NPC.Center, -0.7f, 0.5f);
                    BloodDroplet.SpawnBurst(NPC.Center, new Vector2(-facing, -0.6f), 12, 50f);
                    Shake(6f, 16);
                    break;

                case Move.Shown when Arg >= 1f:
                {
                    // Gone from where he was (left there a moment, the way he faced there), and
                    // at the end of the hall. (He is shown at this tick's end: his eye there is
                    // worked out from how he stood.)
                    Vector2 was = stepFrom + new Vector2(NPC.width * 0.5f, NPC.height);

                    if (ghosts.Count < 12)
                        ghosts.Add(new Ghost { Pose = pose, Feet = was, Facing = facedWas != 0 ? facedWas : facing, Left = 1f });

                    Sound(SoundID.DoubleJump, was, -0.5f, 0.9f);
                    Puff(was, 6, 2.6f, 0.8f);

                    Sound(SoundID.Item1, NPC.Center, -0.6f, 0.9f);
                    Puff(feet, 7, 3.2f, 0.8f);
                    PADParticleHandler.SpawnParticle(new ParryRing(NPC.Center, 14f, 70f, 10, Steel * 0.3f, NPC));
                    Glint(FoighRig.Build(pose, feet, facing).EyeAt, Eye, 22f, 10);
                    break;
                }
            }
        }

        /// <summary>Every tick of his end, on a machine with a screen: the roar, his fall, the sword, his blood, his coughs, the smoke and the ash.</summary>
        private void EndLooks()
        {
            Vector2 feet = Feet;

            switch (State)
            {
                case Move.Roar:
                    if (At(M.RoarDraws))
                    {
                        // By request, red and black, as his burning sword is.
                        Cinematic.Roar(NPC, M.RoarVoice, RoarStrength, SoundID.Roar with { Pitch = RoarPitch, Volume = 1f }, rim: BurnRed);

                        PADParticleHandler.SpawnParticle(new BloomGeneric(NPC.Center, Vector2.Zero, BurnRed * 0.5f, 1.2f, 18));

                        for (int i = 0; i < 12; i++)
                        {
                            Vector2 way = (MathHelper.TwoPi * i / 12f).ToRotationVector2();

                            PADParticleHandler.SpawnParticle(new BurnSmoke(NPC.Center + way * 8f, way * Main.rand.NextFloat(1.5f, 3.5f), Soot, BurnRed,
                                Main.rand.Next(26, 40), Main.rand.NextFloat(10f, 16f), Main.rand.NextFloat(30f, 44f), Main.rand.NextFloat(0.7f, 0.9f)));
                        }
                    }

                    // His breath, black with red in it, while the voice lasts.
                    if (now > M.RoarDraws && now < M.RoarLets && now % 6 == 0)
                    {
                        Vector2 going = new Vector2(facing * 1.2f, -1.6f);

                        PADParticleHandler.SpawnParticle(new BurnSmoke(frame.MouthAt, going * Main.rand.NextFloat(0.8f, 1.4f), Soot, BurnRed,
                            Main.rand.Next(24, 34), Main.rand.NextFloat(6f, 9f), Main.rand.NextFloat(20f, 28f), Main.rand.NextFloat(0.6f, 0.8f)));
                    }
                    break;

                case Move.Fold:
                    if (At(M.FoldKnees))
                    {
                        Sound(SoundID.Dig, feet, -0.8f, 0.9f);
                        Puff(feet, 8, 2.2f);
                        Shake(3f, 8);
                    }

                    if (At(M.FoldHands))
                    {
                        Sound(SoundID.Dig, frame.NearFist, -0.6f, 0.5f);
                        Puff(new Vector2(frame.NearFist.X, feet.Y), 5, 1.6f, 0.8f);
                    }

                    if (At(M.FoldLands))
                    {
                        Vector2 lies = Lying().Grip + new Vector2(-facing * FoighRig.GripToTip * LieReach * 0.5f, 0f);

                        Sound(SoundID.Tink, lies, -0.5f, 0.7f);
                        Sound(SoundID.Dig, lies, -0.6f, 0.5f);
                        SparkShard.Spray(lies, Steel, 5, 4f, 16f, 12, falls: true, palette: Sparks);
                        Puff(new Vector2(lies.X, feet.Y), 6, 2f, 0.6f);
                    }

                    if (now >= M.FoldHands)
                        Bleeds();
                    break;

                case Move.Bowed:
                case Move.Asked:
                case Move.Mouth:
                case Move.Choir:
                case Move.Plead:
                    Bleeds();
                    break;

                case Move.Laugh:
                case Move.Knew:
                    Bleeds();
                    CoughBlood();
                    break;

                case Move.Finish:
                    if (At(TakeHolds))
                        Sound(SoundID.Tink, Lying().Grip, -0.5f, 0.5f);

                    if (At(TakeTicks - 4))
                        Sound(SoundID.Item1, NPC.Center, -0.7f, 0.5f);
                    break;

                case Move.Thrust:
                    if (At(M.ThrustIn + 3))
                        Pierced();

                    // Off the point, out of his back.
                    if (now > M.ThrustThrough && now < M.ThrustLooked + 30 && now % 9 == 0 && Target() is Player p)
                    {
                        Choreo(p, clock, out _, out EndSword sword);
                        Drip(sword.Grip + sword.Blade.ToRotationVector2() * FoighRig.GripToTip, 1);
                    }
                    break;

                case Move.Cast:
                    if (At(8) && Target() is Player q)
                    {
                        // The blade out of him: blood after it.
                        Sound(SoundID.Item1, Chest, -0.5f, 0.8f);
                        Sound(SoundID.NPCHit1, Chest, -0.5f, 0.6f);
                        Burst(Chest, q.Center - Chest, 10, 30f);
                    }

                    if (At(M.CastDown))
                    {
                        Sound(SoundID.Dig, feet, -0.8f, 1f);
                        Puff(feet, 10, 2.4f);
                        Shake(4f, 10);
                    }

                    if (now >= CastSmoulders && now < CastAshes)
                        Smoulders();

                    if (At(CastAshes))
                        Ashes();
                    break;

                case Move.Spare:
                    if (At(TakeHolds))
                        Sound(SoundID.Tink, Lying().Grip, -0.5f, 0.5f);

                    if (At(SpareThrows))
                        Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, -0.2f, 0.9f);

                    if (thrown && At(SpareThrows + (int)Math.Ceiling(Lands())))
                    {
                        Vector2 down = Flying(float.MaxValue).Grip;

                        Sound(SoundID.Tink, down, -0.4f, 0.8f);
                        Sound(SoundID.Dig, down, -0.6f, 0.6f);
                        SparkShard.Spray(down, Steel, 6, 4.5f, 18f, 12, falls: true, palette: Sparks);
                        Puff(new Vector2(down.X, feet.Y), 6, 2.2f, 0.6f);
                    }
                    break;

                case Move.Slump:
                    if (At(M.SlumpDown))
                    {
                        Sound(SoundID.Dig, feet, -0.8f, 1f);
                        Puff(feet, 9, 2.2f);
                        Shake(3f, 8);
                    }
                    break;
            }
        }

        /// <summary>Shaking and bleeding, by request: a drop off his mouth now and then, onto the ground.</summary>
        private void Bleeds()
        {
            if (now % 23 == 0 || now % 37 == 0)
                Drip(frame.MouthAt, 1);
        }

        /// <summary>Each cough, the tick it comes on this machine: blood out of his mouth onto the ground ahead of him.</summary>
        private void CoughBlood()
        {
            int count = CoughsHere;

            if (count <= 0)
                return;

            float from = CoughFrom();

            for (int i = 0; i < count; i++)
            {
                if (At((int)Math.Round(from + i * M.EndCoughEvery)))
                    SpitBlood(i == count - 1 ? 9 : 6);
            }
        }

        /// <summary>The blade into him, on a machine with a screen: heard, felt, and his blood out of his back after the point and out of the wound.</summary>
        private void Pierced()
        {
            Player p = Target();

            if (p == null)
                return;

            Choreo(p, clock, out _, out EndSword sword);

            Vector2 along = sword.Blade.ToRotationVector2();
            Vector2 chest = Chest;

            Sound(SoundID.DD2_GhastlyGlaivePierce, chest, -0.3f, 1f);
            Sound(SoundID.NPCHit1, chest, -0.5f, 0.9f);

            Burst(chest + along * 10f, along, 14, 26f);
            Burst(chest - along * 4f, -along, 6, 40f);

            Shake(6f, 14);

            if (Near())
                Cinematic.Flash(new Color(170, 16, 20), 0.08f);
        }

        /// <summary>Smoke off him, more and more of it, and sparks going up: as off the Guide before he comes apart (GuideSacrifice).</summary>
        private void Smoulders()
        {
            float ramp = (now - CastSmoulders) / (float)(CastAshes - CastSmoulders);
            float rate = MathHelper.Lerp(0.03f, 0.4f, ramp);

            int puffs = (int)rate;

            if (Main.rand.NextFloat() < rate - puffs)
                puffs++;

            for (int i = 0; i < puffs; i++)
            {
                Vector2 at = Vector2.Lerp(frame.Hips, frame.NeckAt, Main.rand.NextFloat()) + Main.rand.NextVector2Circular(8f, 4f);
                Vector2 drift = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1.6f, -0.5f) * (0.6f + ramp));

                PADParticleHandler.SpawnParticle(new MuzzleSmoke(at, drift, Main.rand.Next(40, 70), Main.rand.NextFloat(0.35f, 0.65f) * (0.7f + ramp)));
            }

            if (Main.rand.NextFloat() < ramp * 0.8f)
            {
                Dust ember = Dust.NewDustPerfect(Vector2.Lerp(frame.Hips, frame.NeckAt, Main.rand.NextFloat()) + Main.rand.NextVector2Circular(10f, 5f),
                    DustID.Torch, new Vector2(0f, Main.rand.NextFloat(-2f, -0.5f)), 0, default, 1.1f);

                ember.noGravity = true;
            }
        }

        /// <summary>His pictures' pixels, read once each (reading one back off the card is slow): for the ash.</summary>
        private static readonly Color[][] piecePixels = new Color[FoighRig.Pieces][];

        private static Color[] PixelsOf(int piece)
        {
            if (piecePixels[piece] != null)
                return piecePixels[piece];

            Asset<Texture2D> asset = pieces[piece];

            if (asset == null || !asset.IsLoaded)
                return null;

            Texture2D texture = asset.Value;
            Color[] data = new Color[texture.Width * texture.Height];

            try
            {
                texture.GetData(data);
            }
            catch
            {
                // A picture the card will not give back is not worth the game: no ash.
                return null;
            }

            return piecePixels[piece] = data;
        }

        /// <summary>How long the ash lies before the first of it is lifted, and over how long after that the rest is, from the side the wind comes from; how high the heap is at its middle; and the wind (along, away from the player; and up).</summary>
        private const int AshLies = 40;
        private const int AshLifts = 60;
        private const float AshHeap = 8f;
        private static readonly Vector2 AshWind = new Vector2(2.4f, -1.1f);

        /// <summary>Where each fleck of him was drawn, and how bright: for the heap, worked out before any of it is made.</summary>
        private static readonly List<(Vector2 At, float Bright)> flecks = new List<(Vector2, float)>();

        /// <summary>
        /// His body to ash, on a machine with a screen: every other pixel of him as he lies,
        /// one fleck each (AshDrift), each where it was drawn and as bright. It falls into a
        /// heap where he lay, highest in the middle of him; and the wind (his own: away from
        /// the player, behind him, and up) takes it off, the side it comes from first. Heard,
        /// and felt.
        /// </summary>
        private void Ashes()
        {
            Sound(SoundID.NPCDeath6, NPC.Center, -0.4f, 0.8f);
            Shake(4f, 16);

            var list = new List<FoighRig.Part>();
            FoighRig.Build(pose, Feet, facing, list);

            flecks.Clear();

            float least = float.MaxValue;
            float most = float.MinValue;

            foreach (FoighRig.Part part in list)
            {
                if (part.Piece == FoighRig.Sword)
                    continue;

                Color[] pixels = PixelsOf(part.Piece);

                if (pixels == null)
                    continue;

                Point size = FoighRig.Sizes[part.Piece];

                for (int y = 0; y < size.Y; y += 2)
                {
                    for (int x = 0; x < size.X; x += 2)
                    {
                        Color c = pixels[x + y * size.X];

                        if (c.A < 40)
                            continue;

                        float bright = (c.R + c.G + c.B) / 765f;

                        if (bright <= AshDrift.DarkFloor)
                            continue;

                        // Where the game draws this pixel (SpriteBatch: the picture mirrored
                        // within itself, then turned about the pivot, which is given as on
                        // the mirrored picture).
                        float drawnX = part.FlipX ? size.X - 1 - x : x;
                        Vector2 at = part.At + new Vector2(drawnX + 0.5f - part.Origin.X, y + 0.5f - part.Origin.Y).RotatedBy(part.Rotation);

                        flecks.Add((at, bright));
                        least = Math.Min(least, at.X);
                        most = Math.Max(most, at.X);
                    }
                }
            }

            if (flecks.Count == 0)
                return;

            float middle = (least + most) * 0.5f;
            float half = Math.Max(8f, (most - least) * 0.5f);
            Vector2 wind = new Vector2(-facing * AshWind.X, AshWind.Y);
            int made = 0;

            foreach ((Vector2 at, float bright) in flecks)
            {
                // The heap: highest in the middle of where he lay, nothing at its ends. A fleck
                // already lower than its place in it stays where it is (in the heap); one in
                // the ground is on it.
                float off = (at.X - middle) / half;
                float high = AshHeap * Math.Max(0f, 1f - off * off);
                float ground = Math.Min(Feet.Y - 1f, Math.Max(Feet.Y - 1f - Main.rand.NextFloat(0f, high), at.Y));

                // The side the wind comes from (ahead of him, where the player is) goes first.
                float order = MathHelper.Clamp(0.5f + facing * (Feet.X - at.X) / 60f, 0f, 1f);
                int lifts = AshLies + (int)(order * AshLifts) + Main.rand.Next(0, 8);

                PADParticleHandler.SpawnParticle(new AshDrift(at, bright, ground, lifts, wind, lifts + Main.rand.Next(70, 110), made++));
            }

            flecks.Clear();
        }

        // ============================================================
        // DRAWING
        // ============================================================

        private static readonly List<FoighRig.Part> loose = new List<FoighRig.Part>(1);

        /// <summary>
        /// The sword out of his hand as his end has it, this frame, as a picture: false where
        /// it is not seen. Through him, it is to be drawn under his body (among his pictures,
        /// before the body); anywhere else, behind all of him.
        /// </summary>
        private bool EndSwordShown(out FoighRig.Part part, out bool through)
        {
            part = default;
            through = false;

            if (!Ending)
                return false;

            Choreo(Target(), clock, out _, out EndSword sword);

            if (!sword.Shown)
                return false;

            part = FoighRig.LooseSword(sword.Grip, sword.Blade, Math.Cos(sword.Blade) < 0.0, sword.Reach);
            through = sword.Through;

            return true;
        }

        /// <summary>The sword out of his hand drawn on its own, in the light where it is, its sharp side and its blood as his have been.</summary>
        private void DrawLoose(SpriteBatch spriteBatch, FoighRig.Part part, Vector2 screenPos)
        {
            loose.Clear();
            loose.Add(part);

            Color light = Lighting.GetColor((int)(part.At.X / 16f), (int)(part.At.Y / 16f));
            FoighRig.Frame flat = new FoighRig.Frame { Turn = 1f, TurnAbout = part.At.X };

            DrawParts(spriteBatch, loose, flat, screenPos, light, false, sharpShown, bloodShown);
        }

        /// <summary>The sword out of his hand put among his pictures, under his body: through him.</summary>
        private static void UnderBody(List<FoighRig.Part> list, FoighRig.Part part)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Piece == FoighRig.Torso)
                {
                    list.Insert(i, part);
                    return;
                }
            }

            list.Insert(0, part);
        }
    }
}
