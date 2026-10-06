using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The road: what he does before the fight, on every machine, and what the player it is
    /// for is shown and asked, on their own machine only.
    ///
    ///   AWAIT   his back to them. The picture fades, they are put down twenty-eight tiles
    ///           off and walk to twenty (FoighScenePlayer.Approach), and the server is told.
    ///   TURN    the camera goes over to him; he looks back over his shoulder, and turns.
    ///   TALK    three lines, each with one answer.
    ///   DRAW    the sword out of the ground, up over his head, and down behind him.
    ///   VOW     the fourth line, and Ready.
    ///   BEGIN   his name comes up under him and goes to the bar; the picture fades to
    ///           black, comes back on the player, and he starts to walk.
    ///
    /// The server waits at Await, Talk and Vow for the player's word, and not for ever: a
    /// player who never answers is not a world stopped.
    ///
    /// The camera is PisPlayer's (BlendCamera); the bars, the lean, the fade and the hold are
    /// Cinematic's. The lines are the user's own words, kept as written (the bars in them are
    /// Dialogue.Pause: he stops there).
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // HIS LINES
        // ============================================================

        private static readonly string[] Lines =
        {
            "Thank you, stranger. My gratitude upon you for aiding this.|.|.|. completion. My vengeance is served.",
            "Your dedication for me showed much strength|,| dexterity|,|.|.|. You truly are a warrior|.|.|.",
            "Such spirit in you|.|.|. Would you hold your weapon tight for one last task?",
            "I desire|.|.|. A warrior's death.",
        };

        private const string Silence = "...";
        private const string ReadyLabel = "Ready";

        /// <summary>
        /// What he says as the fight begins, in a box that goes by itself and holds nobody
        /// (Dialogue.Aside). The request names such a box for the first phase and gives no
        /// words for it: with none here, none comes up.
        /// </summary>
        private const string FirstPhaseLine = "";

        // ============================================================
        // THE ROAD'S TICKS
        // ============================================================

        /// <summary>They never walked up: he turns round anyway.</summary>
        private const int AwaitLongest = 1500;

        /// <summary>The camera's way over to him, before he moves.</summary>
        private const int TurnWait = 54;

        /// <summary>Lines never answered: he goes on.</summary>
        private const int TalkLongest = 5400;
        private const int VowLongest = 5400;

        /// <summary>Begin: his name is up and read; the picture goes to black, stays, and comes back on the player; and he walks.</summary>
        private const int FadeOutAt = 150;
        private const int FadeOutTicks = 20;
        private const int BlackTicks = 14;
        private const int FadeInTicks = 26;
        private const int BeginTicks = FadeOutAt + FadeOutTicks + BlackTicks + FadeInTicks;

        /// <summary>How far off the player is put down, and where the walk ends: twenty-eight tiles, and twenty.</summary>
        public const float PutDownAt = 28f * 16f;
        public const float StopAt = 20f * 16f;

        /// <summary>Where the sword stands in the ground before he takes it: its grip, and the way its blade points, in the world.</summary>
        private Vector2 PlantedGrip
        {
            get
            {
                // Ahead of him while his back is to them; behind him once he has turned.
                int ahead = State <= Move.Turn ? facing : -facing;
                return Feet + new Vector2(-M.PlantedGrip.X * ahead, M.PlantedGrip.Y);
            }
        }

        private float PlantedBlade
        {
            get
            {
                int ahead = State <= Move.Turn ? facing : -facing;
                float down = MathHelper.ToRadians(M.PlantedBlade);

                return ahead > 0 ? MathHelper.Pi - down : down;
            }
        }

        // ============================================================
        // EVERY MACHINE
        // ============================================================

        private void Road(Player target)
        {
            NPC.noTileCollide = false;

            Fall();
            NPC.velocity.X = 0f;

            if (!Authority)
                return;

            switch (State)
            {
                case Move.Await:
                    if (now >= AwaitLongest)
                        Go(Move.Turn);
                    break;

                case Move.Turn:
                    if (now >= TurnWait + M.TurnTicks)
                    {
                        // Seen from his other side by the end of it: he faces the other way, standing.
                        facing = -facing;
                        Go(Move.Talk);
                    }
                    break;

                case Move.Talk:
                    if (now >= TalkLongest)
                        Go(Move.Draw);
                    break;

                case Move.Draw:
                    if (At(M.DrawTakes + 1))
                    {
                        hasSword = true;
                        NPC.netUpdate = true;
                    }

                    if (now >= M.DrawTicks)
                        Go(Move.Vow);
                    break;

                case Move.Vow:
                    if (now >= VowLongest)
                        Go(Move.Begin);
                    break;

                case Move.Begin:
                    if (now >= BeginTicks)
                    {
                        hasSword = true;
                        rest = FirstRest;
                        Go(Move.Walk);
                    }
                    break;
            }
        }

        // ============================================================
        // THE PLAYER IT IS FOR
        // ============================================================

        /// <summary>How far toward him the camera is, 0 to 1.</summary>
        private float shot;

        /// <summary>Which of his lines is up, from 1; none yet at 0.</summary>
        private int line;

        private bool talked;
        private bool vowed;
        private bool named;
        private bool cut;

        /// <summary>The lean while he talks, and for the vow.</summary>
        private const float TalkZoom = 1.16f;
        private const float VowZoom = 1.22f;

        /// <summary>The scene for the player it is for, every tick, on their machine.</summary>
        private void Scene()
        {
            Player me = Main.LocalPlayer;

            // Nobody to show it to: everything asked for lets go by itself.
            if (me.dead || !me.active)
                return;

            Vector2 him = NPC.Center + new Vector2(0f, -30f);
            bool wantShot = false;

            switch (State)
            {
                case Move.Await:
                    // The walk up: the fade, put down, the walk (FoighScenePlayer), and the
                    // server told when they are there.
                    FoighScenePlayer.Approach(this);
                    Cinematic.Frame();

                    if (FoighScenePlayer.There)
                        Cinematic.Hold();
                    break;

                case Move.Turn:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(TalkZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;
                    break;

                case Move.Talk:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(TalkZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;
                    Talk();
                    break;

                case Move.Draw:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(TalkZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;

                    if (Dialogue.Speaking(NPC))
                        Dialogue.Close();
                    break;

                case Move.Vow:
                    Cinematic.Hold();
                    Cinematic.Frame();
                    Cinematic.Lean(VowZoom, 0.03f);
                    FoighScenePlayer.FaceToward(NPC);
                    wantShot = true;
                    Vow();
                    break;

                case Move.Begin:
                    Begin(him, out wantShot);
                    break;
            }

            shot = MathHelper.Clamp(shot + (wantShot ? 1f : -1f) / TurnWait, 0f, 1f);

            if (shot > 0f)
                PisPlayer.BlendCamera(him, shot * shot * (3f - 2f * shot));
        }

        /// <summary>
        /// His three lines, one after another, each with its one answer. The third answer tells
        /// the server. A line whose box has gone down under him (something else took it) comes
        /// back.
        /// </summary>
        private void Talk()
        {
            if (talked)
                return;

            if (line == 0)
            {
                if (now >= 14)
                    Say(0);

                return;
            }

            if (!Dialogue.Speaking(NPC))
                Say(line - 1);
        }

        private void Say(int i)
        {
            // He is somebody they have been helping: his face and his name are theirs already.
            DialogueIntroductions.Known.Add(DialogueIntroductions.Key(Type));

            line = i + 1;
            Dialogue.Converse(NPC, Lines[i], Pace, Next(i));
        }

        /// <summary>The answer under line <paramref name="i"/>: his next line, or, under the third, the end of the talk.</summary>
        private TalkOption Next(int i)
        {
            return new TalkOption(Silence, _ =>
            {
                if (i < 2)
                {
                    line = i + 2;
                    Dialogue.Scene(Lines[i + 1], Next(i + 1));
                    return;
                }

                talked = true;
                Dialogue.Close();
                Tell(Word.Talked);
            })
            { Remembered = false };
        }

        /// <summary>The fourth line, and Ready, which tells the server.</summary>
        private void Vow()
        {
            if (vowed || Dialogue.Speaking(NPC) || now < 10)
                return;

            Dialogue.Converse(NPC, Lines[3], Pace, new TalkOption(ReadyLabel, _ =>
            {
                vowed = true;
                Dialogue.Close();
                Tell(Word.Ready);
            })
            { Remembered = false });
        }

        /// <summary>
        /// Ready: his name up under him and to the bar, with the camera still on him; then
        /// the picture to black, the camera put back on the player under it, and back up on
        /// them, their feet theirs again as it comes.
        /// </summary>
        private void Begin(Vector2 him, out bool wantShot)
        {
            if (Dialogue.Speaking(NPC))
                Dialogue.Close();

            if (!named)
            {
                named = true;
                BossIntro.PlayNameOnly(NPC.whoAmI);
            }

            int dark = FadeOutAt + FadeOutTicks;
            int back = dark + BlackTicks;

            wantShot = now < dark;

            if (now < back + FadeInTicks / 2)
                Cinematic.Hold();

            if (now < back + FadeInTicks)
                Cinematic.Frame();

            if (now < dark)
            {
                Cinematic.Lean(VowZoom, 0.03f);
                FoighScenePlayer.FaceToward(NPC);
            }

            if (now >= FadeOutAt && now < dark)
            {
                Cinematic.Fade(Smooth((now - FadeOutAt) / (float)FadeOutTicks));
            }
            else if (now >= dark && now < back)
            {
                Cinematic.Fade(1f);

                // Under the black: the camera home, at once.
                if (!cut)
                {
                    cut = true;
                    shot = 0f;
                    PisPlayer.PanHome(him, 8);
                }
            }
            else if (now >= back && now < back + FadeInTicks)
            {
                Cinematic.Fade(1f - Smooth((now - back) / (float)FadeInTicks));
            }

            // The fight's first words, if there are any: in a box that goes by itself.
            if (now == back + FadeInTicks - 1 && FirstPhaseLine.Length > 0)
                Dialogue.Aside(NPC, FirstPhaseLine, Pace);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }
    }
}
