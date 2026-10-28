using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// What the player she stopped is shown and asked, on their own machine only: the
    /// camera, the bars, being held still or walked, and the box.
    ///
    /// The camera is PisPlayer's (BlendCamera), not Cinematic.Look: Look holds the player
    /// for as long as the camera is away, and the walk up to her needs the player's feet.
    /// The bars, the lean and the fade are Cinematic's; the hold, where there is one, is
    /// too. From the road to the end, while a scene is on, the player faces her
    /// (DonaScenePlayer.FaceToward); the walk up puts them on the side her head is, so the
    /// two face each other.
    ///
    /// The lines are hers, the user's own words, kept as written (the bars in them are
    /// Dialogue.Pause: she trails off): Q01 to Q10 of the script Amy Lam records from
    /// (Claude input/AmyVoiceLines.docx).
    /// </summary>
    public partial class DonaQuinn
    {
        // ============================================================
        // HER LINES
        // ============================================================

        private const string FearMe = "Fear me, you simpleton!";
        private const string ItIsI = "For it is I, Dame Quilt! Challenge me if you dare!";
        private const string Coward = "Coward!| Here comes my lance!";
        private const string Mercy = "Please! Wait! Wait! I surrender!";

        /// <summary>
        /// Finished: her last words, as she lies cut (Q10), slow, a breath at each stop, the
        /// panel going down by itself. Said <see cref="LastWordsAt"/> ticks into it; she dies
        /// (DonaQuinn.Fall, FadeAt) once they are said.
        /// </summary>
        private const string LastWords = "I failed you.|.|.| dear.|.|.| father...";
        private const float LastWordsPace = 0.6f;
        private const int LastWordsAt = 40;

        /// <summary>From her last words begun to all of them up: the panel's fade, 29 letters at LastWordsPace, and six stops.</summary>
        private const int LastWordsTicks = Dialogue.FadeTicks + 49 + 6 * Dialogue.PauseTicks;

        private bool saidLastWords;

        private const string Decline = "Decline";
        private const string Accept = "Accept";
        private const string SpareLabel = "Spare";
        private const string FinishLabel = "Finish";

        /// <summary>Spared, she talks: each line, then the answer that moves her on to the next. The last has none.</summary>
        private static string TalkLine(int i, bool male) => i switch
        {
            0 => "Thank you, kind stranger!",
            1 => "I am but a lonesome knight.|.|.| that I strive to be.",
            2 => "I'm so sorry.|.|.| for the startle. I sure have caused you some damage.",
            3 => "O, I adore knighthood.|.|.| but I can never join the kingdom's army|.|.|.",
            _ => "Huh! Sorry again for the trouble. I hope to see you soon, stranger!",
        };

        private static readonly string[] TalkAnswers = { "...", "...", "Why the knight stuff?", "I am in a hurry" };

        private const int TalkLines = 5;

        // ============================================================
        // THE CAMERA AND THE BARS
        // ============================================================

        /// <summary>How far toward the shot the camera is, 0 to 1, eased by the scene.</summary>
        private float shot;

        /// <summary>Ticks the camera takes to go to the shot and back.</summary>
        private const int ShotTicks = 60;

        /// <summary>The throw's lean, and the close shot's, from the walk up to the end of the talk.</summary>
        private const float ThrowZoom = 1.2f;
        private const float CloseZoom = 1.4f;

        /// <summary>A player further off her than this is too far to share the shot with her: it goes over to her, over this much more.</summary>
        private const float ShareFrom = 500f;
        private const float ShareOver = 300f;

        /// <summary>Her head comes up this long before the box does.</summary>
        private const int HeadUpTicks = 24;

        // ============================================================
        // THE BOX: where it has got to on this machine
        // ============================================================

        private int roadLine;
        private bool saidCoward;
        private bool named;
        private bool pleaded;
        private bool chosen;
        private int talkLine;
        private bool talkDone;

        /// <summary>The scene for the player she stopped, every tick, on their machine.</summary>
        private void Scene(TriunfoTheDonkey donkey)
        {
            Player me = Main.LocalPlayer;

            // Nobody to show it to: everything asked for lets go by itself.
            if (me.dead || !me.active)
                return;

            Vector2 focus = NPC.Center;
            bool wantShot = false;

            switch (State)
            {
                case Phase.Intro:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(1.12f);
                    DonaScenePlayer.FaceToward(NPC);

                    if (Timer >= SecondLineAt)
                        wantShot = true;

                    Road();
                    break;

                case Phase.Challenge:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    DonaScenePlayer.FaceToward(NPC);
                    wantShot = Timer < ChallengeTicks - 6;

                    // "Coward!": said already if the answer was clicked here; said now if
                    // nobody answered on the road in time (the server's timeout).
                    if (!saidCoward)
                    {
                        if (Dialogue.Speaking(NPC))
                            Dialogue.Line(Coward);
                        else
                            Dialogue.Converse(NPC, Coward, Pace);

                        saidCoward = true;
                        roadLine = 3;
                    }

                    // Her name goes up, under her, and the bar comes up with it.
                    if (!named)
                    {
                        named = true;
                        BossIntro.PlayNameOnly(NPC.whoAmI);
                    }

                    if (Timer >= ChallengeTicks - 6 && Dialogue.Speaking(NPC))
                        Dialogue.Close();
                    break;

                case Phase.Buck:
                case Phase.Thrown:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(ThrowZoom);
                    DonaScenePlayer.FaceToward(NPC);
                    focus = Between(me);
                    wantShot = true;
                    break;

                case Phase.Lying:
                    // The walk up (DonaScenePlayer.Approach): a beat on her where she fell,
                    // the fade, and under the black the player put down by her with the shot
                    // gone in close; the picture comes back up on them walking to her.
                    DonaScenePlayer.Approach(NPC, facing);
                    Cinematic.Frame();
                    focus = Between(me);
                    wantShot = true;
                    CloseShot();
                    break;

                case Phase.Plead:
                    // The walk up finishes if it has not, and the box waits for it.
                    DonaScenePlayer.Approach(NPC, facing);
                    DonaScenePlayer.FaceToward(NPC);
                    Cinematic.Frame();
                    focus = Between(me);
                    wantShot = true;
                    CloseShot();

                    if (DonaScenePlayer.There)
                        Cinematic.Hold();

                    if (!DonaScenePlayer.Busy && Timer >= HeadUpTicks)
                        Plead();
                    break;

                case Phase.GetUp:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(CloseZoom);
                    DonaScenePlayer.FaceToward(NPC);
                    focus = Between(me);
                    wantShot = true;
                    break;

                case Phase.Talk:
                    Cinematic.Frame();
                    Cinematic.Lean(CloseZoom - 0.1f);

                    // Facing her while she talks; the last line lets them go, and their feet.
                    if (!talkDone && talkLine < TalkLines)
                        DonaScenePlayer.FaceToward(NPC);

                    focus = Between(me);
                    wantShot = !talkDone;
                    Talk(me);
                    break;

                case Phase.Finish:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(Timer < 20 ? 1.5f : CloseZoom, Timer < 20 ? 0.3f : Cinematic.LeanRate);
                    DonaScenePlayer.FaceToward(NPC);
                    focus = NPC.Center;
                    wantShot = true;

                    if (!saidLastWords && Timer >= LastWordsAt)
                    {
                        saidLastWords = true;
                        Dialogue.Aside(NPC, LastWords, LastWordsPace, 50);
                    }
                    break;

                default:
                    // The fight, and after: the box, if it is somehow still up, goes.
                    if (Dialogue.Speaking(NPC) && State != Phase.Free)
                        Dialogue.Close();
                    break;
            }

            shot = MathHelper.Clamp(shot + (wantShot ? 1f : -1f) / ShotTicks, 0f, 1f);

            if (shot > 0f)
                PisPlayer.BlendCamera(focus, shot * shot * (3f - 2f * shot));
        }

        /// <summary>
        /// Halfway between the player and her, and a little up, so both are in the shot. With
        /// the player too far off to share it, the shot goes over to her, by degrees, so it
        /// never jumps.
        /// </summary>
        private Vector2 Between(Player me)
        {
            Vector2 middle = (me.Center + NPC.Center) * 0.5f + new Vector2(0f, -24f);
            Vector2 her = NPC.Center + new Vector2(0f, -24f);
            float far = MathHelper.Clamp((Vector2.Distance(me.Center, NPC.Center) - ShareFrom) / ShareOver, 0f, 1f);

            return Vector2.Lerp(middle, her, far);
        }

        /// <summary>
        /// The lean from the throw to the walk up: the throw's until the cut, then the close
        /// one, put in place under the black in one go, and the camera with it, so that the
        /// picture comes back up already on the two of them.
        /// </summary>
        private void CloseShot()
        {
            if (!DonaScenePlayer.PastCut)
            {
                Cinematic.Lean(ThrowZoom);
                return;
            }

            bool dark = DonaScenePlayer.AllBlack;

            Cinematic.Lean(CloseZoom, dark ? 1f : Cinematic.LeanRate);

            if (dark)
                shot = 1f;
        }

        /// <summary>
        /// The road: the first line with her face black and "???" on the tab, the second when
        /// it has had a moment (her name in it brings her face up out of the black), then the
        /// two answers. Both lead to the same place.
        /// </summary>
        private void Road()
        {
            if (saidCoward)
                return;

            if (roadLine == 0 && Timer >= FirstLineAt)
            {
                Dialogue.Converse(NPC, FearMe, Pace);
                roadLine = 1;
                return;
            }

            if (roadLine == 1 && Timer >= SecondLineAt && Dialogue.LineDone)
            {
                Dialogue.Line(ItIsI);
                roadLine = 2;
                return;
            }

            if (roadLine == 2 && Dialogue.LineDone)
            {
                Dialogue.Offer(
                    new TalkOption(Decline, _ => Answer()) { Remembered = false },
                    new TalkOption(Accept, _ => Answer()) { Remembered = false });

                roadLine = 20;
            }

            // The box went down under her (something else took it): put it back.
            if (roadLine > 0 && !Dialogue.Speaking(NPC) && Timer > FirstLineAt + 2)
            {
                Dialogue.Converse(NPC, roadLine >= 2 ? ItIsI : FearMe, Pace);

                if (roadLine >= 2)
                    roadLine = 2;
            }
        }

        /// <summary>Either answer on the road: "Coward!", here and now, and the server told.</summary>
        private void Answer()
        {
            Dialogue.Line(Coward);
            saidCoward = true;
            roadLine = 3;
            Choose(DonaQuinnEncounter.Choice.Fight);
        }

        /// <summary>
        /// Her head up, and the box: please. Spare and Finish, one in each bottom corner. Put
        /// back if the box goes down before an answer is picked.
        /// </summary>
        private void Plead()
        {
            if (chosen || (pleaded && Dialogue.Speaking(NPC)))
                return;

            pleaded = true;

            Dialogue.Converse(NPC, Mercy, Pace);
            Dialogue.OfferCorners(
                new TalkOption(SpareLabel, _ => Choose(DonaQuinnEncounter.Choice.Spare)) { Remembered = false },
                new TalkOption(FinishLabel, _ => Choose(DonaQuinnEncounter.Choice.Finish)) { Remembered = false });
        }

        /// <summary>The player's choice, to wherever the world is decided. Spare and Finish close the box.</summary>
        private void Choose(DonaQuinnEncounter.Choice choice)
        {
            if (choice == DonaQuinnEncounter.Choice.Spare || choice == DonaQuinnEncounter.Choice.Finish)
            {
                chosen = true;
                Dialogue.Close();
            }

            DonaQuinnEncounter.Answer(NPC, choice);
        }

        /// <summary>
        /// Spared, she talks: a line, the answer that moves her on, the next line. The last
        /// line lets the player go (Leave), and when the box is down she is told it is over.
        /// If the box goes down before that (something else took it), the line it was on
        /// comes back.
        /// </summary>
        private void Talk(Player me)
        {
            if (talkDone)
                return;

            if (talkLine == 0)
            {
                if (Timer >= 12)
                {
                    Dialogue.Converse(NPC, TalkLine(0, me.Male), Pace, Next(0));
                    talkLine = 1;
                }

                return;
            }

            if (!Dialogue.Speaking(NPC))
            {
                // The last line is up and the box has gone down: done.
                if (talkLine >= TalkLines)
                {
                    talkDone = true;
                    DonaQuinnEncounter.Answer(NPC, DonaQuinnEncounter.Choice.Done);
                    return;
                }

                int line = talkLine - 1;
                Dialogue.Converse(NPC, TalkLine(line, me.Male), Pace, Next(line));
            }

            // Held through the talk, until the last line lets them go.
            if (talkLine < TalkLines)
                Cinematic.Hold();
        }

        /// <summary>The answer under line <paramref name="i"/>: says the next line, or the last one, which lets the player go.</summary>
        private TalkOption Next(int i)
        {
            return new TalkOption(TalkAnswers[i], _ =>
            {
                bool male = Main.LocalPlayer.Male;
                talkLine = i + 2;

                if (i + 1 < TalkLines - 1)
                    Dialogue.Scene(TalkLine(i + 1, male), Next(i + 1));
                else
                    Dialogue.Say(TalkLine(i + 1, male));
            })
            { Remembered = false };
        }
    }
}
