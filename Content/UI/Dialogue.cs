using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// Talking to something that is not a town NPC.
    ///
    /// Vanilla's conversation — right-click, the parchment box at the top, two buttons —
    /// belongs to town NPCs: Player.talkNPC only takes them, and the box is theirs. This
    /// is the other kind. A figure that is not a townsperson gets a small bubble over
    /// its head while the player is near, opens on a right-click at close range, and
    /// speaks from a dark panel low on the screen — where the boss bar lives, in its
    /// palette, because it is the same world speaking. The line arrives a letter at a
    /// time; the answers come up when it has finished, and a click on the panel skips
    /// the wait. Walking away ends it, as does the inventory, as does "Leave".
    ///
    /// The answers are TalkOptions' own — same class, same gold, same heard-grey, same
    /// world memory (TalkMemory) — so a line under Mors and a line under the Guide are
    /// one thing to the player. Only the box is different, because the speaker is.
    ///
    /// Register(type, opener, above, options…) is all a speaker needs:
    ///
    ///     Dialogue.Register(ModContent.NPCType&lt;MorsIdle&gt;(), (player, npc) =&gt; "I know you.", 84,
    ///         new TalkOption("Who you are", _ =&gt; Dialogue.Say(WhoYouAre)),
    ///         new TalkOption("The knife", _ =&gt; Dialogue.Say(TheKnife), (p, _) =&gt; p.HasItem(blade)));
    ///
    /// Say(text, followUps) inside a click replaces the answers with the follow-ups;
    /// Say(text) alone ends the branch, leaving only "Leave". Nothing here is sent over
    /// the network: a conversation is one player looking at one thing.
    ///
    /// Scene(text, answers) is the same thing with the doors shut: no "Leave", no
    /// walking off, no inventory, and the player's feet do not work until an answer is
    /// picked. Use it where the exchange is the content and wandering out halfway
    /// would leave the world in a half-state. Any Say with no follow-ups lets go
    /// again, so a locked branch cannot dead-end.
    ///
    /// A speaker can also start the talking (Converse): the panel comes up on them
    /// without a click, shut like a scene, for someone who stops the player in the
    /// road. Line(text) then has them say the next thing with nothing to answer (their
    /// own code moves them on, and it must); Offer(answers) puts answers under the line
    /// already up, and OfferCorners(left, right) puts two in the panel's two bottom
    /// corners, for a fork rather than a list.
    ///
    /// And a speaker can say something that stops nothing (Aside): the panel comes up
    /// with the line and no answers, holds nobody, takes no click, and goes down by itself
    /// a little after the line has all arrived. For something said in the middle of a fight.
    ///
    /// Whoever speaks is named on a tab over the panel's top edge: "???" until they
    /// have said their name, then their name and title (DialoguePortrait.Name).
    /// </summary>
    public static class Dialogue
    {
        // ============================================================
        // THE PANEL
        // ============================================================

        private const int PanelWidth = 600;

        /// <summary>Text wraps inside this; the panel is PanelWidth and the text is inset.</summary>
        private const int PadX = 24;
        private const int PadTop = 18;
        private const int PadBottom = 16;

        /// <summary>From the bottom of the screen up to the panel's bottom edge.</summary>
        private const int BottomMargin = 118;

        /// <summary>Between the last line of speech and the first answer.</summary>
        private const int AnswersGap = 12;

        /// <summary>TalkPanel.png is 36×36 with 12px ends, so the middle 12px stretch.</summary>
        private const int SliceEnd = 12;

        private const int FadeTicks = 12;

        // ============================================================
        // THE CAMERA
        // ============================================================
        //
        // A scene pulls the camera in a little. Deliberately under BossIntro's 1.22:
        // a boss arriving is an event and this is two people talking, so it should read
        // as leaning in rather than as a cutscene.
        //
        // GameZoomTarget is a SAVED SETTING — Main.SaveSettings writes it to the config
        // file — so the player's own zoom is taken down before it is touched and put
        // back afterwards, on unload, and around every save. Same care BossIntro takes,
        // and for the same reason.

        internal const float SceneZoom = 1.12f;
        private const int ZoomTicks = 24;

        /// <summary>Letters per tick, when a speaker does not say. Under one, so a line takes a breath to arrive.</summary>
        public const float DefaultLettersPerTick = 0.8f;

        /// <summary>PADGlobalItem's LoreColour, and the boss plate's title. It is lore.</summary>
        private static readonly Color TextColour = new Color(196, 180, 152);

        // ============================================================
        // THE NAME TAB
        // ============================================================
        //
        // A small piece of the panel standing up off its top edge, just right of the
        // face: who is speaking. Drawn before the panel, so the panel's own top edge
        // runs across the tab's foot and the two read as one piece.

        /// <summary>Gap between the face's frame and the tab.</summary>
        private const int TabGap = 6;

        /// <summary>The tab's height, and how much of it the panel's edge covers.</summary>
        private const int TabHeight = 30;
        private const int TabTuck = 4;

        /// <summary>Text inset either side of the tab.</summary>
        private const int TabPadX = 12;

        private const float TabScale = 0.85f;

        /// <summary>The title, after the name: the same lore colour, quieter.</summary>
        private static readonly Color TitleColour = new Color(150, 138, 118);

        // ============================================================
        // THE APPROACH
        // ============================================================

        /// <summary>Within this the bubble shows, fading in over the outer part of it.</summary>
        private const float BubbleRange = 220f;
        private const float BubbleFadeFrom = 0.65f;

        /// <summary>Within this a right-click on the figure opens the conversation.</summary>
        public const float TalkRange = 150f;

        /// <summary>The click target is the whole figure, this much wider than its hitbox.</summary>
        private const int TargetSlack = 12;

        /// <summary>Past this the conversation ends on its own.</summary>
        private const float LeaveRange = 240f;

        /// <summary>The bubble bobs this far, this fast.</summary>
        private const float BobHeight = 2f;
        private const float BobRate = 2.4f;

        /// <summary>The bubble's tail tip, in bubble pixels — where it points from.</summary>
        private static readonly Vector2 BubbleTail = new Vector2(5f, 16f);

        /// <summary>The tail sits this far right of the figure's centre line, and this far above its head.</summary>
        private const float BubbleRight = 8f;
        private const float BubbleAbove = 6f;

        // ============================================================
        // REGISTRY
        // ============================================================

        private sealed class Speaker
        {
            public Func<Player, NPC, string> Opener;
            public int Above;
            public float LettersPerTick;
            public List<TalkOption> Options;
        }

        private static readonly Dictionary<int, Speaker> registry = new();

        /// <summary>
        /// Makes this NPC type talkable. <paramref name="opener"/> gives the first line
        /// each time the conversation opens; <paramref name="above"/> is how far above
        /// the NPC's feet its head is, in pixels, so the bubble clears the sprite and
        /// not just the hitbox. <paramref name="lettersPerTick"/> is how fast this one
        /// speaks. The options come up under the opener.
        /// </summary>
        public static void Register(int npcType, Func<Player, NPC, string> opener, int above, float lettersPerTick,
            params TalkOption[] options)
        {
            registry[npcType] = new Speaker
            {
                Opener = opener,
                Above = above,
                LettersPerTick = lettersPerTick,
                Options = new List<TalkOption>(options),
            };
        }

        public static void Register(int npcType, Func<Player, NPC, string> opener, int above, params TalkOption[] options)
            => Register(npcType, opener, above, DefaultLettersPerTick, options);

        public static bool IsSpeaker(int npcType) => registry.ContainsKey(npcType);

        /// <summary>Mod unload. Statics do not clear themselves.</summary>
        public static void Clear()
        {
            registry.Clear();
            Close(silent: true);
            faceIndex = -1;
            faceType = -1;
            ReleaseZoom();
            fade = 0f;
            panel = null;
            bubble = null;
        }

        // ============================================================
        // STATE
        // ============================================================

        private static Asset<Texture2D> panel;
        private static Asset<Texture2D> bubble;

        /// <summary>Who is speaking. -1 when nobody.</summary>
        private static int npcIndex = -1;
        private static int npcType = -1;

        /// <summary>Who spoke last, for the face on the panel while it fades out after them.</summary>
        private static int faceIndex = -1;
        private static int faceType = -1;

        /// <summary>The line, and the line broken to the panel's width.</summary>
        private static string wrapped = "";

        /// <summary>When the current line began arriving, and how fast it does.</summary>
        private static uint lineStart;
        private static float letters = DefaultLettersPerTick;

        /// <summary>The answers up right now. Null means the registered ones.</summary>
        private static List<TalkOption> replaced;

        private static readonly List<TalkOption> offered = new();

        /// <summary>The way out, offered under everything. Never remembered, never grey.</summary>
        private static readonly TalkOption Leave = new TalkOption("Leave", _ => Close()) { Remembered = false };

        private static float fade;
        private static int hovered = -1;
        private static bool rightWasDown;

        /// <summary>A scene is running: one way through it, and the player stands still.</summary>
        private static bool locked;

        /// <summary>The two answers up sit in the panel's two bottom corners, on one row.</summary>
        private static bool corners;

        /// <summary>Answers an opener asked to be shown instead of the registered ones.</summary>
        private static List<TalkOption> pendingScene;

        /// <summary>
        /// The line up is an aside: nothing to answer, nobody held, no way out because none is
        /// needed. It goes by itself this many ticks after it has all arrived.
        /// </summary>
        private static bool aside;
        private static int asideLinger;

        /// <summary>The update the aside's line had all arrived on; none yet at 0.</summary>
        private static uint asideDoneAt;

        public static bool Open => npcIndex >= 0;

        /// <summary>Read by DialogueLock, which is what actually holds the player.</summary>
        public static bool Locked => Open && locked;

        /// <summary>How far the camera has leaned in, 0 to 1.</summary>
        private static float leanIn;
        private static bool holdingZoom;
        private static float savedZoom = 1f;

        internal static bool HoldingZoom => holdingZoom;
        internal static float SavedZoom => savedZoom;

        /// <summary>Smoothstep, so the camera starts and stops softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Leans the camera in for as long as the company lasts and lets it back out
        /// after. Not taken at all while BossIntro has it: two things fighting over one
        /// saved setting would put the wrong number back.
        /// </summary>
        private static void Camera()
        {
            // Stillness, not the conversation.
            //
            // The lean-in belongs to standing near one of them, not to having clicked on
            // one. Hung off the scene it only existed inside the panel, so the player met
            // the figure, saw nothing, clicked, and the world jumped at them. Hung off the
            // buff it is already in when they arrive, it stays while they walk around the
            // figure, and it lets go when they leave — and the same change is what makes
            // it a warning at all, since Stillness is on whenever the world has stopped
            // putting things down around the player.
            //
            // StillnessPlayer re-applies the buff every tick for as long as a keeper is
            // within range, so HasBuff is exactly "one of them is standing there". Mors is
            // the only speaker registered, and he is a keeper, so no conversation loses
            // its camera by this; a speaker who is not a keeper would, and the answer is
            // a Stillness.Register call rather than a second trigger here.
            bool want = Main.LocalPlayer.HasBuff<Stillness>();

            if (want && !holdingZoom && !BossIntro.HoldingZoom && !NPCs.Pugream.EyeFinaleView.HoldingZoom)
            {
                savedZoom = Main.GameZoomTarget;
                holdingZoom = true;
            }

            if (!holdingZoom)
                return;

            leanIn = MathHelper.Clamp(leanIn + (want ? 1f : -1f) / ZoomTicks, 0f, 1f);
            Main.GameZoomTarget = MathHelper.Lerp(savedZoom, SceneZoom, Ease(leanIn));

            if (!want && leanIn <= 0f)
                ReleaseZoom();
        }

        internal static void ReleaseZoom()
        {
            if (!holdingZoom)
                return;

            Main.GameZoomTarget = savedZoom;
            holdingZoom = false;
            leanIn = 0f;
        }

        private static void LoadTextures()
        {
            panel ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/TalkPanel");
            bubble ??= ModContent.Request<Texture2D>("PisAndDag/Content/UI/TalkBubble");
        }

        // ============================================================
        // THE CONVERSATION
        // ============================================================

        private static void Begin(NPC npc, Speaker speaker)
        {
            npcIndex = npc.whoAmI;
            npcType = npc.type;
            faceIndex = npc.whoAmI;
            faceType = npc.type;
            replaced = null;
            locked = false;
            corners = false;
            aside = false;
            pendingScene = null;
            hovered = -1;
            letters = speaker.LettersPerTick;

            // The opener runs before the answers are chosen, so it is the opener's one
            // chance to say that this line is a scene rather than a greeting.
            string line = speaker.Opener?.Invoke(Main.LocalPlayer, npc) ?? "";

            if (pendingScene != null)
            {
                replaced = pendingScene;
                pendingScene = null;
                locked = true;
            }

            SetLine(line);

            // The panel is still fading up; the first words would arrive half-seen and
            // read as a stumble. Let it be there before he speaks.
            lineStart += FadeTicks;

            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        /// <summary>
        /// The speaker says this; these become the only answers, plus Leave. With no
        /// follow-ups the branch is over, and any scene running lets go of the player:
        /// a dead end with no way out would be a soft-lock, so there is not one.
        /// </summary>
        public static void Say(string text, params TalkOption[] followUps)
        {
            if (!Open)
                return;

            if (followUps.Length == 0)
                locked = false;

            corners = false;
            aside = false;
            replaced = new List<TalkOption>(followUps);
            SetLine(text);
        }

        /// <summary>
        /// Says this and shuts the doors: these answers and nothing else, no way out of
        /// the panel, and the player held where they stand until one is picked. At
        /// least one answer, or there would be no way through.
        /// </summary>
        public static void Scene(string text, params TalkOption[] answers)
        {
            if (!Open || answers.Length == 0)
            {
                Say(text, answers);
                return;
            }

            locked = true;
            corners = false;
            aside = false;
            replaced = new List<TalkOption>(answers);
            SetLine(text);
        }

        /// <summary>
        /// This speaker starts talking, with no click from the player: the panel comes up on
        /// them, shut like a scene, with these answers (none is allowed: the speaker's own code
        /// then moves the talk on with Line, Offer or OfferCorners, and must, or the player
        /// stays held).
        /// The speaker need not be registered. Talks at <paramref name="lettersPerTick"/>.
        /// </summary>
        public static void Converse(NPC npc, string text, float lettersPerTick, params TalkOption[] answers)
        {
            if (npc == null || !npc.active)
                return;

            bool already = Open;

            npcIndex = npc.whoAmI;
            npcType = npc.type;
            faceIndex = npc.whoAmI;
            faceType = npc.type;
            pendingScene = null;
            hovered = -1;
            letters = lettersPerTick;
            locked = true;
            corners = false;
            aside = false;
            replaced = new List<TalkOption>(answers);

            SetLine(text);

            // As Begin: a panel still fading up would swallow the first words.
            if (!already)
            {
                lineStart += FadeTicks;
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
        }

        /// <summary>How long an aside stays up once all of it has arrived, when whoever says it does not say: two seconds.</summary>
        public const int AsideLinger = 120;

        /// <summary>
        /// This speaker says this, and nothing stops for it: the panel comes up with the line
        /// and no answers, the player is not held and can do anything, a click on it does
        /// nothing, and it goes down by itself <paramref name="linger"/> ticks after the line
        /// has all arrived. Not over a scene: a line that holds the player is not replaced by
        /// one that does not. The speaker need not be registered.
        /// </summary>
        public static void Aside(NPC npc, string text, float lettersPerTick, int linger = AsideLinger)
        {
            if (npc == null || !npc.active || string.IsNullOrEmpty(text) || Locked)
                return;

            bool already = Open;

            npcIndex = npc.whoAmI;
            npcType = npc.type;
            faceIndex = npc.whoAmI;
            faceType = npc.type;
            pendingScene = null;
            hovered = -1;
            letters = lettersPerTick;
            locked = false;
            corners = false;
            aside = true;
            asideLinger = Math.Max(1, linger);
            asideDoneAt = 0;
            replaced = new List<TalkOption>();

            SetLine(text);

            if (!already)
                lineStart += FadeTicks;
        }

        /// <summary>The speaker says this and there is nothing to answer. The player stays held.</summary>
        public static void Line(string text)
        {
            if (!Open)
                return;

            locked = true;
            corners = false;
            aside = false;
            replaced = new List<TalkOption>();
            SetLine(text);
        }

        /// <summary>
        /// Answers for the line already up, without saying it again: they come up under it
        /// once it has all arrived. Held until one is picked.
        /// </summary>
        public static void Offer(params TalkOption[] answers)
        {
            if (!Open)
                return;

            locked = true;
            corners = false;
            aside = false;
            replaced = new List<TalkOption>(answers);
        }

        /// <summary>
        /// Two answers for the line already up, in the panel's two bottom corners rather
        /// than in a list: a fork. Held until one is picked.
        /// </summary>
        public static void OfferCorners(TalkOption left, TalkOption right)
        {
            if (!Open)
                return;

            locked = true;
            corners = true;
            aside = false;
            replaced = new List<TalkOption> { left, right };
        }

        /// <summary>Whether all of the current line has arrived on the panel.</summary>
        public static bool LineDone => Open && Arrived() >= wrapped.Length;

        /// <summary>Whether this NPC is the one speaking right now.</summary>
        public static bool Speaking(NPC npc) => Open && npc != null && npcIndex == npc.whoAmI && npcType == npc.type;

        /// <summary>
        /// Called from inside an opener: the line it is about to return is a scene, and
        /// these are its answers. The registered options do not appear.
        /// </summary>
        public static void OpenAsScene(params TalkOption[] answers)
        {
            if (answers.Length > 0)
                pendingScene = new List<TalkOption>(answers);
        }

        /// <summary>
        /// Back up to the speaker's own answers, with Leave under them again.
        ///
        /// Say with no follow-ups leaves the player holding one button that ends the
        /// conversation, which makes every question the last question. An answer that
        /// offers this instead is a branch that returns rather than a branch that stops,
        /// and the way out is still there — it is just one step further off, under the
        /// list, where the player chose to go rather than where they were left.
        ///
        /// The line stays up. Making him repeat a greeting to hand back a menu would be
        /// a worse conversation than leaving his last words on the panel. Anything
        /// already heard comes back grey, because Current rebuilds from the registry and
        /// the registry remembers.
        /// </summary>
        public static void Return()
        {
            if (!Open)
                return;

            replaced = null;
            locked = false;
            corners = false;
            aside = false;
        }

        /// <summary>Lets the player go without ending the conversation.</summary>
        public static void Unlock() => locked = false;

        public static void Close(bool silent = false)
        {
            if (Open && !silent)
                SoundEngine.PlaySound(SoundID.MenuClose);

            npcIndex = -1;
            npcType = -1;
            replaced = null;
            pendingScene = null;
            locked = false;
            corners = false;
            aside = false;
            offered.Clear();
            hovered = -1;
        }

        /// <summary>
        /// A beat in a line: the speaker stops here for <see cref="PauseTicks"/> before
        /// going on. Never shown. Two in a row is a longer beat.
        /// </summary>
        public const char Pause = '|';

        private const int PauseTicks = 30;

        /// <summary>Where the beats fall, counted in the line's non-space characters, which wrapping does not move.</summary>
        private static readonly List<int> pauses = new();

        private static void SetLine(string text)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            pauses.Clear();

            var plain = new System.Text.StringBuilder();
            int ordinal = 0;

            foreach (char c in text ?? "")
            {
                if (c == Pause)
                {
                    pauses.Add(ordinal);
                    continue;
                }

                plain.Append(c);

                if (!char.IsWhiteSpace(c))
                    ordinal++;
            }

            wrapped = font.CreateWrappedText(plain.ToString(), PanelWidth - PadX * 2, Language.ActiveCulture.CultureInfo);
            lineStart = Main.GameUpdateCount;
        }

        /// <summary>How much of the line has arrived, in characters, beats included.</summary>
        private static int Arrived()
        {
            // Unsigned: a start still in the future must read as nothing yet, not everything.
            if (Main.GameUpdateCount < lineStart)
                return 0;

            float budget = (Main.GameUpdateCount - lineStart) * letters;

            if (pauses.Count == 0)
                return (int)Math.Min(budget, wrapped.Length);

            float beat = PauseTicks * letters;
            int ordinal = 0;
            int next = 0;

            for (int i = 0; i < wrapped.Length; i++)
            {
                if (!char.IsWhiteSpace(wrapped[i]))
                {
                    while (next < pauses.Count && pauses[next] == ordinal)
                    {
                        budget -= beat;
                        next++;
                    }

                    ordinal++;
                }

                if (budget < 1f)
                    return i;

                budget -= 1f;
            }

            return wrapped.Length;
        }

        /// <summary>The answers to show: registered and allowed, or the replaced set, then Leave.</summary>
        private static List<TalkOption> Current(Player player, NPC npc)
        {
            offered.Clear();

            if (replaced != null)
            {
                offered.AddRange(replaced);
            }
            else if (registry.TryGetValue(npc.type, out Speaker speaker))
            {
                foreach (TalkOption option in speaker.Options)
                {
                    if (option.When == null || option.When(player, npc))
                        offered.Add(option);
                }
            }

            // A scene has no way out but through; an aside needs none.
            if (!locked && !aside)
                offered.Add(Leave);

            return offered;
        }

        // ============================================================
        // INPUT — from the interface layer, where the mouse is honest
        // ============================================================

        /// <summary>
        /// A right-click edge of our own. Main.mouseRightRelease is a shared latch like
        /// the left one (see PADMouse), so the edge is taken from the button itself.
        /// </summary>
        private static bool RightPressed()
        {
            bool down = Main.mouseRight;
            bool pressed = down && !rightWasDown;
            rightWasDown = down;
            return pressed;
        }

        /// <summary>
        /// Where the cursor is in the world. This runs from a UI layer, where
        /// PlayerInput.SetZoom_UI has already divided the mouse by the UI scale and
        /// nothing has applied the game zoom, so Main.MouseWorld read here is wrong by
        /// both. Vanilla's own answer: back to the raw mouse, apply the world zoom,
        /// read, and put the UI scaling back for whatever draws next.
        /// </summary>
        private static Vector2 MouseInWorld()
        {
            PlayerInput.SetZoom_Unscaled();
            PlayerInput.SetZoom_MouseInWorld();
            Vector2 world = Main.MouseWorld;
            PlayerInput.SetZoom_UI();
            return world;
        }

        /// <summary>The whole figure, head included — the hitbox stops at the shoulders.</summary>
        private static Rectangle Target(NPC npc, Speaker speaker)
        {
            int width = npc.width + TargetSlack * 2;
            int height = Math.Max(npc.height, speaker.Above);

            return new Rectangle((int)(npc.Center.X - width * 0.5f), (int)(npc.Bottom.Y - height), width, height);
        }

        /// <summary>The speaker under the cursor and within reach, or null.</summary>
        private static NPC UnderCursor(Player player)
        {
            Point mouse = MouseInWorld().ToPoint();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!registry.TryGetValue(npc.type, out Speaker speaker))
                    continue;

                if (!Target(npc, speaker).Contains(mouse))
                    continue;

                if (Vector2.Distance(player.Center, npc.Center) > TalkRange)
                    continue;

                return npc;
            }

            return null;
        }

        // ============================================================
        // DRAW
        // ============================================================

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            LoadTextures();

            Player player = Main.LocalPlayer;
            bool rightPressed = RightPressed();

            // Someone talkable under the cursor: the cursor is ours, so a right-click
            // opens rather than using whatever is held. Right-clicking the speaker again
            // while open closes.
            NPC under = UnderCursor(player);

            if (under != null)
            {
                player.mouseInterface = true;

                if (rightPressed)
                {
                    Main.mouseRightRelease = false;

                    if (Open && under.whoAmI == npcIndex)
                    {
                        if (!locked)
                            Close();
                    }
                    else if (!Locked && registry.TryGetValue(under.type, out Speaker speaker))
                    {
                        // Not over a scene: one way through it, and it is not someone else.
                        Begin(under, speaker);
                    }
                }
            }

            // Keep the conversation honest: the speaker is gone, or the player is.
            if (Open)
            {
                NPC npc = Main.npc[npcIndex];

                bool gone = !npc.active || npc.type != npcType || player.dead;

                // Walking away and the inventory end an ordinary conversation. They do
                // not end a scene: the player cannot walk, and the scene is the content.
                // Nor an aside, which is said to someone in the middle of something else.
                if (!locked && !aside)
                    gone |= Main.playerInventory || Vector2.Distance(player.Center, npc.Center) > LeaveRange;

                if (gone)
                    Close();
                else if (aside)
                    TickAside();
            }

            fade = MathHelper.Clamp(fade + (Open ? 1f : -1f) / FadeTicks, 0f, 1f);
            Camera();

            if (fade <= 0f)
            {
                hovered = -1;
                return;
            }

            if (!Open)
            {
                DrawPanel(spriteBatch, player, null);
                return;
            }

            DrawPanel(spriteBatch, player, Main.npc[npcIndex]);
        }

        /// <summary>An aside goes down by itself, silently, a little after all of its line has arrived.</summary>
        private static void TickAside()
        {
            if (Arrived() < wrapped.Length)
                return;

            if (asideDoneAt == 0)
                asideDoneAt = Math.Max(1u, Main.GameUpdateCount);

            if (Main.GameUpdateCount - asideDoneAt >= (uint)asideLinger)
                Close(silent: true);
        }

        private static void DrawPanel(SpriteBatch spriteBatch, Player player, NPC npc)
        {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            int arrived = Open ? Arrived() : wrapped.Length;
            bool complete = arrived >= wrapped.Length;

            Vector2 textSize = font.MeasureString(wrapped.Length > 0 ? wrapped : " ");

            List<TalkOption> answers = Open && complete ? Current(player, npc) : null;

            // A line with nothing to answer (Line) leaves no room for answers at all.
            if (answers != null && answers.Count == 0)
                answers = null;

            bool atCorners = corners && answers != null && answers.Count == 2;

            float rowHeight = font.MeasureString("M").Y * TalkOptions.BaseScale + TalkOptions.RowGap;
            float answersHeight = answers == null ? 0f
                : atCorners ? rowHeight - TalkOptions.RowGap
                : answers.Count * rowHeight - TalkOptions.RowGap;

            int height = PadTop + (int)textSize.Y + (answers != null ? AnswersGap + (int)answersHeight : 0) + PadBottom;
            int x = (Main.screenWidth - PanelWidth) / 2;
            int y = Main.screenHeight - BottomMargin - height;

            Rectangle box = new Rectangle(x, y, PanelWidth, height);

            // Over the panel the cursor is ours, and a click while the line is still
            // arriving brings the rest of it at once. Not over an aside: the player is in
            // the middle of something, and the panel takes nothing from them.
            if (!aside && box.Contains(Main.mouseX, Main.mouseY) && PADMouse.Steering)
            {
                player.mouseInterface = true;

                if (Open && !complete && PADMouse.Pressed)
                {
                    PADMouse.Consume();
                    lineStart = 0;
                }
            }

            string shown = arrived >= wrapped.Length ? wrapped : wrapped.Substring(0, arrived);

            // The speaker's face on the corner: black until they have said who they are,
            // which may be in this very line.
            NPC face = npc;

            if (face == null && faceIndex >= 0 && Main.npc[faceIndex].active && Main.npc[faceIndex].type == faceType)
                face = Main.npc[faceIndex];

            if (npc != null)
                DialoguePortrait.Hear(npc, shown);

            // Under the panel, so the panel's edge crosses its foot.
            DrawNameTab(spriteBatch, face, box);

            Utils.DrawSplicedPanel(spriteBatch, panel.Value, box.X, box.Y, box.Width, box.Height,
                SliceEnd, SliceEnd, SliceEnd, SliceEnd, Color.White * fade);

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, shown,
                new Vector2(box.X + PadX, box.Y + PadTop), TextColour * fade, 0f, Vector2.Zero, Vector2.One);

            DialoguePortrait.DrawOnPanel(spriteBatch, face, box, panel.Value, fade);

            if (answers == null)
            {
                hovered = -1;
                return;
            }

            float answersTop = box.Y + PadTop + textSize.Y + AnswersGap;

            hovered = atCorners
                ? TalkOptions.DrawOptionCorners(spriteBatch, answers, npc, box.X + PadX, box.Right - PadX,
                    answersTop, hovered, TalkMemory.Heard)
                : TalkOptions.DrawOptionLines(spriteBatch, answers, npc, box.Right - PadX,
                    answersTop, hovered, TalkMemory.Heard);
        }

        /// <summary>
        /// Who is speaking, on a tab standing up off the panel's top edge beside the face:
        /// "???" until they have said their name, then their name, and their title after it
        /// in a quieter colour (DialoguePortrait.LabelName, LabelTitle). Nobody to name, no tab.
        /// </summary>
        private static void DrawNameTab(SpriteBatch spriteBatch, NPC speaker, Rectangle box)
        {
            if (speaker == null)
                return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            string name = DialoguePortrait.LabelName(speaker);
            string title = DialoguePortrait.LabelTitle(speaker);
            string joint = string.IsNullOrEmpty(title) ? null : ", " + title;

            float nameWidth = font.MeasureString(name).X * TabScale;
            float titleWidth = joint == null ? 0f : font.MeasureString(joint).X * TabScale;

            int x = DialoguePortrait.PanelFrameRight(box) + TabGap;
            int width = (int)Math.Ceiling(nameWidth + titleWidth) + TabPadX * 2;
            Rectangle tab = new Rectangle(x, box.Y - TabHeight + TabTuck, width, TabHeight);

            Utils.DrawSplicedPanel(spriteBatch, panel.Value, tab.X, tab.Y, tab.Width, tab.Height,
                SliceEnd, SliceEnd, SliceEnd, SliceEnd, Color.White * fade);

            // Centred in the part of the tab the panel does not cover.
            float lineHeight = font.MeasureString("M").Y * TabScale;
            Vector2 at = new Vector2(tab.X + TabPadX, tab.Y + (TabHeight - TabTuck - lineHeight) * 0.5f + 2f);
            at = new Vector2((float)Math.Floor(at.X), (float)Math.Floor(at.Y));

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, name, at, TextColour * fade, 0f,
                Vector2.Zero, new Vector2(TabScale));

            if (joint != null)
            {
                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, font, joint, at + new Vector2(nameWidth, 0f),
                    TitleColour * fade, 0f, Vector2.Zero, new Vector2(TabScale));
            }
        }

        /// <summary>
        /// The bubble over a speaker's head while the player is near and not already
        /// talking to it. World space, from PADNPCs.PostDraw. Unlit — it is a mark on
        /// the world, not a thing in it.
        /// </summary>
        internal static void DrawBubble(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (Main.dedServ || !registry.TryGetValue(npc.type, out Speaker speaker))
                return;

            if (Open && npc.whoAmI == npcIndex)
                return;

            LoadTextures();

            float dist = Vector2.Distance(Main.LocalPlayer.Center, npc.Center);

            if (dist > BubbleRange)
                return;

            float alpha = 1f - MathHelper.SmoothStep(0f, 1f,
                MathHelper.Clamp((dist - BubbleRange * BubbleFadeFrom) / (BubbleRange * (1f - BubbleFadeFrom)), 0f, 1f));

            if (alpha <= 0f)
                return;

            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * BobRate + npc.whoAmI) * BobHeight;

            Vector2 tail = new Vector2(
                (int)(npc.Center.X + BubbleRight - screenPos.X),
                (int)(npc.Bottom.Y - speaker.Above - BubbleAbove + npc.gfxOffY + bob - screenPos.Y));

            spriteBatch.Draw(bubble.Value, tail, null, Color.White * alpha, 0f, BubbleTail, 1f, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// Holds the player still for the length of a scene.
    ///
    /// The controls are blanked rather than the position frozen, so everything else the
    /// game does to a standing player still happens: gravity, tiles, damage, the world
    /// going on around them. The inventory key is blanked with the rest, which is the
    /// other door out of a conversation, and noItems shuts the held weapon whether or
    /// not the button got through.
    /// </summary>
    /// <summary>
    /// Keeps the scene's borrowed zoom out of the player's config file, and stops a
    /// scene's camera surviving a world change. Same shape as BossIntroSystem's guard,
    /// which is over the same saved setting; two detours on SaveSettings chain, and
    /// only one of the two is ever holding.
    /// </summary>
    public class DialogueCamera : ModSystem
    {
        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.SaveSettings += RestoreAroundSave;
        }

        private static bool RestoreAroundSave(On_Main.orig_SaveSettings orig)
        {
            bool holding = Dialogue.HoldingZoom;
            float borrowed = Main.GameZoomTarget;

            if (holding)
                Main.GameZoomTarget = Dialogue.SavedZoom;

            bool result = orig();

            if (holding)
                Main.GameZoomTarget = borrowed;

            return result;
        }

        public override void OnWorldUnload()
        {
            Dialogue.Close(silent: true);
            Dialogue.ReleaseZoom();
        }

        public override void PreSaveAndQuit()
        {
            Dialogue.Close(silent: true);
            Dialogue.ReleaseZoom();
        }
    }

    public class DialogueLock : ModPlayer
    {
        public override void SetControls()
        {
            if (Player.whoAmI != Main.myPlayer || !Dialogue.Locked)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlInv = false;

            // Vanilla's own "your hands are full" flag, reset every update and set
            // again here: no swing, no shot, no placing, whatever is in hand.
            Player.noItems = true;

            Player.velocity.X = 0f;
        }
    }
}
