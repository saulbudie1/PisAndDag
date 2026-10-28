using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- in Legends, her second half ------------------------------------------------------------------------------
    //
    // By request, in a Legends world, once vanilla's own change at half her life has come:
    //
    //   HER CHANGE     a scene of its own instead of vanilla's (EmpressOfLight.Change): the camera on her, the world
    //                  bent into her bust and hips and taken out of her there (EmpressVeil), her left hand raised and a
    //                  bow formed in it out of light (EmpressBow: Eventide's shape, bigger), and the day stopped
    //                  (EmpressTime); a line of hers as it begins and another as the day stops.
    //   THIRTEEN ACTS  of her own, every other move, the other vanilla's own next move as it would have come; never one
    //                  of the last three she played: the eight asked for (the moored volley, the negative, the cage of
    //                  curtains, the constellation, the blinks, the mirror barrier, the ricochets, the prism at her heart)
    //                  and the five chosen from those offered (the lightspeed shot, the dawnbreak, the refraction volley,
    //                  the rain of light, the blade of everlasting rainbow). Her first is always the moored volley: the
    //                  bow, at once.
    //   HER DASH       a grab again (EmpressOfLight.Grab), the bow in her other hand: light burns through whoever she
    //                  holds, and she casts them aside.
    //   HER BOW        in her hand all through: lowered at rest, raised and drawn to shoot, her other hand drawing its
    //                  string to her chest (her arm on the game's own frame for it: the hand up there).
    //
    // As her first half's, each act puts back what it changed when it is done. The server (or a single player) decides
    // which, where and each step; every machine plays it.

    public partial class EmpressOfLight
    {
        private static readonly Act[] SecondActs =
        {
            Act.Moor, Act.Negative, Act.Cage, Act.Constellation, Act.Blink, Act.Shield, Act.Ricochet,
            Act.Heart, Act.Lightspeed, Act.Dawn, Act.Refraction, Act.Rain, Act.Blade,
        };

        /// <summary>Is this one of her second half's acts?</summary>
        private static bool IsSecondAct(Act which) => which >= Act.Moor;

        /// <summary>Her change has been made (the veil, the bow, the day held). Sent.</summary>
        private bool unveiled;

        /// <summary>Her first act of her second half is still to come: the moored volley, at once. The server's.</summary>
        private bool firstSecond;

        /// <summary>Legends, her second half, her arena up, and not dying: when all of this is hers.</summary>
        private bool LegendsSecond(NPC npc) => PADWorld.Legends && arenaSet && !dying && npc.AI_120_HallowBoss_IsInPhase2();

        /// <summary>Is the day held by her (EmpressTime): from the moment of her change until she is gone, through her death too.</summary>
        internal bool HoldsTime(NPC npc) => unveiled && PADWorld.Legends && !ending && npc.AI_120_HallowBoss_IsInPhase2();

        /// <summary>Is she drawn as her second half (her veil, her bow) this tick: in it, or changing into it.</summary>
        private bool SecondLook(NPC npc) => PADWorld.Legends && arenaSet && (scene == Scene.Change || npc.AI_120_HallowBoss_IsInPhase2() && scene != Scene.Death);

        /// <summary>How far to one side of her the one she fights must be before she turns to them, between her acts.</summary>
        private const float FaceGive = 90f;

        /// <summary>Vanilla's pause before choosing its next move in its second half (AI_120, its state 1).</summary>
        private static float SecondChoiceTicks => Main.getGoodWorld ? 10f : 20f;

        // ---- the choice -------------------------------------------------------------------------------------------

        /// <summary>
        /// Before vanilla's AI, while no scene has her (PreAI): in her second half, on the tick vanilla would choose its
        /// next move, an act instead when it is an act's turn. True if an act took the tick.
        /// </summary>
        private bool SecondChoose(NPC npc)
        {
            if (!LegendsSecond(npc))
                return false;

            if (!Authority || !newNext || negativeOn || npc.ai[0] != 1f || npc.ai[1] + 1f < SecondChoiceTicks)
                return false;

            // Not when vanilla would leave on this choice (called by day and the day over: its state 13).
            if (npc.AI_120_HallowBoss_IsGenuinelyEnraged() && (!Main.dayTime || Main.time >= 53400.0))
                return false;

            Player target = TargetOf(npc);

            if (target == null)
                return false;

            StartSecond(npc, target);
            return true;
        }

        /// <summary>After vanilla's AI, in her second half (PostAI): a choice of vanilla's seen, the negative's count, the dash's grab.</summary>
        private void SecondAfter(NPC npc)
        {
            if (scene != Scene.None || !LegendsSecond(npc))
                return;

            bool chose = stateBefore == 1 && npc.ai[0] != 1f;
            bool done = stateBefore > 1 && stateBefore != 13 && npc.ai[0] == 1f;

            if (chose && Authority)
                newNext = true;

            // Her bow toward whoever she fights between her acts too (vanilla never turns her): with a little give, so she
            // does not turn about over and over under or over them. Only seen: every machine its own.
            Player target = TargetOf(npc);

            if (target != null)
            {
                float across = target.Center.X - npc.Center.X;

                if (across > FaceGive)
                    npc.spriteDirection = 1;
                else if (across < -FaceGive)
                    npc.spriteDirection = -1;
            }

            if (negativeOn)
                NegativeAfter(npc, done);

            if (Authority && (npc.ai[0] == 8f || npc.ai[0] == 9f))
                DashMeets(npc);
        }

        /// <summary>One of her second half's acts, chosen: never one of the last three (the first, the bow's). The server's.</summary>
        private void StartSecond(NPC npc, Player target)
        {
            Act chosen;

            if (firstSecond)
            {
                chosen = Act.Moor;
                firstSecond = false;
            }
            else
            {
                Span<Act> open = stackalloc Act[SecondActs.Length];
                int count = 0;

                foreach (Act a in SecondActs)
                {
                    if (a != recent[0] && a != recent[1] && a != recent[2])
                        open[count++] = a;
                }

                chosen = open[Main.rand.Next(count)];
            }

            recent[2] = recent[1];
            recent[1] = recent[0];
            recent[0] = chosen;

            newNext = false;
            act = chosen;
            actWho = target.whoAmI;
            actSeed = Main.rand.Next(1 << 20);
            actCount = 0;
            actN = 0;
            actSide = target.Center.X >= npc.Center.X ? 1 : -1;
            actAt = target.Center;
            actTurn = 0f;

            npc.velocity *= 0.3f;

            // Every act wants her inside her arena (her shadow falls across it, her arrows find its walls, her sphere has
            // room to fly): if vanilla left her outside or near its border (it parks her above the player, who may be
            // high), she steps in first.
            StepIn(npc);

            switch (chosen)
            {
                case Act.Moor: PlanMoor(npc, target); break;
                case Act.Negative: PlanNegative(npc, target); break;
                case Act.Cage: PlanCage(npc, target); break;
                case Act.Constellation: PlanConstellation(npc, target); break;
                case Act.Blink: PlanBlink(npc, target); break;
                case Act.Shield: PlanShield(npc, target); break;
                case Act.Ricochet: PlanRicochet(npc, target); break;
                case Act.Heart: PlanHeart(npc, target); break;
                case Act.Lightspeed: PlanLightspeed(npc, target); break;
                case Act.Dawn: PlanDawn(npc, target); break;
                case Act.Refraction: PlanRefraction(npc, target); break;
                case Act.Rain: PlanRain(npc, target); break;
                case Act.Blade: PlanBlade(npc, target); break;
            }

            Go(npc, Scene.Act, 0);
        }

        /// <summary>How far in from her arena's border an act starts her, at least.</summary>
        private const float StepInMargin = 260f;

        /// <summary>How far she must jump in a tick, outside a blink, to be seen to step (StepSeen).</summary>
        private const float StepSeenJump = 160f;

        /// <summary>Each machine's own: where she was last tick, for StepSeen.</summary>
        private Vector2 seenAt;

        /// <summary>Put inside her arena, StepInMargin in from its border, if she is not. The server's; sent with her place.</summary>
        private void StepIn(NPC npc)
        {
            if (!arenaSet || Decagon.Inside(npc.Center, arenaCentre, EmpressArena.Radius, StepInMargin))
                return;

            Vector2 velocity = npc.velocity;
            npc.Center = Decagon.PushInside(npc.Center, arenaCentre, EmpressArena.Radius, StepInMargin, ref velocity);
            npc.velocity = velocity;
            npc.netUpdate = true;
        }

        /// <summary>
        /// Her stepping in, seen (on any machine, as her place reaches it): light where she was and where she is, and the
        /// sound of a blink. Not for her blinks, which have their own.
        /// </summary>
        private void StepSeen(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 was = seenAt;
            seenAt = npc.Center;

            if (was == Vector2.Zero || !unveiled || !LegendsSecond(npc) || (scene == Scene.Act && act == Act.Blink))
                return;

            if (Vector2.DistanceSquared(was, npc.Center) < StepSeenJump * StepSeenJump)
                return;

            Sound(EmpressSounds.Blink, npc.Center, -0.25f, 0.6f, 2);
            PADParticleHandler.SpawnParticle(new BloomGeneric(was, Vector2.Zero, EmpressArt.Pink, 220f / 160f, 14));
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, Color.White, 260f / 160f, 14));
            EmpressWarp.Ripple(npc.Center, 12f, 36f, 8f, 18);
        }

        /// <summary>One of her second half's acts, every tick, every machine (RunAct).</summary>
        private void RunSecondAct(NPC npc, Player who, bool entered)
        {
            switch (act)
            {
                case Act.Moor: RunMoor(npc, who, entered); break;
                case Act.Negative: RunNegative(npc, who, entered); break;
                case Act.Cage: RunCage(npc, who, entered); break;
                case Act.Constellation: RunConstellation(npc, who, entered); break;
                case Act.Blink: RunBlink(npc, who, entered); break;
                case Act.Shield: RunShield(npc, who, entered); break;
                case Act.Ricochet: RunRicochet(npc, who, entered); break;
                case Act.Heart: RunHeart(npc, who, entered); break;
                case Act.Lightspeed: RunLightspeed(npc, who, entered); break;
                case Act.Dawn: RunDawn(npc, who, entered); break;
                case Act.Refraction: RunRefraction(npc, who, entered); break;
                case Act.Rain: RunRain(npc, who, entered); break;
                case Act.Blade: RunBlade(npc, who, entered); break;
                default:
                    if (Authority)
                        EndAct(npc);
                    break;
            }
        }

        // ---- her bow, held ----------------------------------------------------------------------------------------
        //
        // Each machine's own (only seen): asked for every tick by whatever has her, at rest if nothing asks; and eased
        // there, so it is raised, drawn and lowered rather than jumped. The bow is in the hand on the side she faces (the
        // way she aims), her other hand drawing its string back to her chest.

        /// <summary>Asked for this tick: aimed (and where, how far drawn, what is nocked), or held up over her (its arm's angle), or at rest.</summary>
        private bool bowAiming;
        private float bowAimWant;
        private float bowDrawWant;
        private int bowArrows;
        private float bowSpread;
        private float bowLook;
        private float bowRaise = float.NaN;
        private float bowRaiseAim;

        /// <summary>Her other arm, asked for this tick: posed at an angle (NaN: not), or on a frame of the game's (-1: vanilla's own).</summary>
        private float freeAngle = float.NaN;
        private int freeFrame = -1;

        /// <summary>As drawn: the bow arm's angle, the aim, how far drawn, the string still shaking, which side it is held on.</summary>
        private float bowArmNow;
        private float bowAimNow;
        private float bowDrawNow;
        private float bowTwang;
        private int bowSideNow;
        private float freeAngleNow;

        /// <summary>Every tick, before anything asks (PreAI): the bow at rest, her other arm its own.</summary>
        private void BowRest()
        {
            bowAiming = false;
            bowDrawWant = 0f;
            bowArrows = 0;
            bowSpread = 0f;
            bowRaise = float.NaN;
            freeAngle = float.NaN;
            freeFrame = -1;
            freeSnap = false;
        }

        /// <summary>The bow aimed this tick: drawn so far, so many arrows on it, fanned so wide, of this colour (below 0 the mirror's glass).</summary>
        private void Aim(float aim, float drawn = 0f, int arrows = 0, float spread = 0f, float look = 0f)
        {
            bowAiming = true;
            bowAimWant = aim;
            bowDrawWant = MathHelper.Clamp(drawn, 0f, 1f);
            bowArrows = arrows;
            bowSpread = spread;
            bowLook = look;
        }

        /// <summary>The bow held up this tick, its arm at this angle, the bow turned this way (not aimed: shown).</summary>
        private void HoldBowUp(float arm, float aim)
        {
            bowAiming = false;
            bowRaise = arm;
            bowRaiseAim = aim;
        }

        /// <summary>The string let go: it shakes a moment after.</summary>
        private void Loosed()
        {
            bowTwang = 1f;
            bowDrawNow = 0f;
        }

        /// <summary>
        /// Her other hand held at an angle this tick (the screen's way: 0 right, down positive), eased there, or put there at
        /// once (<paramref name="snap"/>: something held in it must be where the act has it); or on one of the game's frames.
        /// </summary>
        private void FreeArmAt(float angle, bool snap = false)
        {
            freeAngle = angle;
            freeSnap = snap;
        }

        private bool freeSnap;

        private void FreeArmFrame(int frame) => freeFrame = frame;

        /// <summary>Is the bow in her left hand (the screen's)? The side she faces.</summary>
        private static bool BowLeft(NPC npc) => npc.spriteDirection != 1;

        /// <summary>Her drawing hand, on the game's first frame of her other arm: up at her chest, the bow's side across from it.</summary>
        private static Vector2 Anchor(NPC npc, bool bowLeft) => npc.Center + new Vector2(bowLeft ? -EmpressArt.ChestHand.X : EmpressArt.ChestHand.X, EmpressArt.ChestHand.Y) * npc.scale;

        /// <summary>
        /// The bow arm's angle for an aim: its palm (the grip) on the line from her drawing hand the way she aims, so the
        /// string comes back to that hand along the arrow's line.
        /// </summary>
        private static float BowArmFor(NPC npc, bool bowLeft, float aim)
        {
            Vector2 shoulder = npc.Center + (bowLeft ? EmpressArt.LeftShoulder : EmpressArt.RightShoulder) * npc.scale;
            Vector2 anchor = Anchor(npc, bowLeft);
            Vector2 u = aim.ToRotationVector2();
            Vector2 d = shoulder - anchor;
            float cross = d.X * u.Y - d.Y * u.X;
            float reach = EmpressArt.ArmReach * npc.scale;

            return aim + (float)Math.Asin(MathHelper.Clamp(cross / reach, -0.95f, 0.95f));
        }

        /// <summary>The bow at rest: its arm's angle below level, out to her side; its aim below level.</summary>
        private const float RestArm = 0.6f;
        private const float RestAim = 0.25f;

        /// <summary>Where the bow arm and the bow are asked to be this tick.</summary>
        private void BowTarget(NPC npc, bool bowLeft, out float arm, out float aim)
        {
            if (bowAiming)
            {
                aim = bowAimWant;
                arm = BowArmFor(npc, bowLeft, aim);
                return;
            }

            if (!float.IsNaN(bowRaise))
            {
                arm = bowRaise;
                aim = bowRaiseAim;
                return;
            }

            // At rest: the arm out and down at her side, the bow held upright beside her, pointing out and a little down.
            arm = bowLeft ? MathHelper.Pi - RestArm : RestArm;
            aim = bowLeft ? MathHelper.Pi - RestAim : RestAim;
        }

        /// <summary>Every tick (PostAI), every machine with a screen: the bow eased toward what was asked.</summary>
        private void BowTick(NPC npc)
        {
            if (Main.dedServ)
                return;

            bool left = BowLeft(npc);
            int side = left ? -1 : 1;
            BowTarget(npc, left, out float arm, out float aim);

            // Turned about, the bow is in her other hand at once.
            if (side != bowSideNow)
            {
                bowSideNow = side;
                bowArmNow = arm;
                bowAimNow = aim;
                freeAngleNow = float.IsNaN(freeAngle) ? 0f : freeAngle;
            }

            float rate = bowAiming ? 0.32f : 0.18f;
            bowArmNow = LerpAngle(bowArmNow, arm, rate);
            bowAimNow = LerpAngle(bowAimNow, aim, rate);

            // Drawn steadily (as asked), let go at once.
            bowDrawNow = bowDrawWant > bowDrawNow ? MathHelper.Lerp(bowDrawNow, bowDrawWant, 0.5f) : bowDrawWant;
            bowTwang *= 0.9f;

            if (!float.IsNaN(freeAngle))
                freeAngleNow = freeSnap ? freeAngle : LerpAngle(freeAngleNow, freeAngle, 0.35f);

            freeSnap = false;
        }

        /// <summary>The bow as drawn this frame: her bow hand (the grip), the aim, where the string is, her drawing hand.</summary>
        private void BowNow(NPC npc, Vector2 middle, out Vector2 grip, out float aim, out Vector2 nock, out Vector2 hold)
        {
            bool left = BowLeft(npc);
            grip = EmpressArt.PalmAt(middle, left, bowArmNow, npc.scale);
            aim = bowAimNow;
            hold = Anchor(npc, left) + (middle - npc.Center);

            Vector2 u = aim.ToRotationVector2();
            float back = Vector2.Dot(grip - hold, u);
            float drawnBack = MathHelper.Lerp(EmpressBow.RestBack, MathHelper.Clamp(back, EmpressBow.RestBack, EmpressBow.MostBack), bowDrawNow);
            nock = EmpressBow.StringAt(grip, aim, drawnBack);
        }

        /// <summary>The nock as it will be at a full draw, aimed this way (for where arrows leave from, on every machine alike).</summary>
        private static Vector2 NockFor(NPC npc, float aim)
        {
            bool left = BowLeft(npc);
            float arm = BowArmFor(npc, left, aim);
            Vector2 grip = EmpressArt.PalmAt(npc.Center, left, arm, npc.scale);
            Vector2 hold = Anchor(npc, left);
            float back = MathHelper.Clamp(Vector2.Dot(grip - hold, aim.ToRotationVector2()), EmpressBow.RestBack, EmpressBow.MostBack);
            return EmpressBow.StringAt(grip, aim, back);
        }

        // ---- how far her change has come -------------------------------------------------------------------------

        /// <summary>How far her veil has come (EmpressVeil), and its edges' light thrown up as it is made.</summary>
        private float VeilNow(NPC npc) => scene == Scene.Change ? ChangeVeil() : npc.AI_120_HallowBoss_IsInPhase2() ? 1f : 0f;

        private float VeilFlareNow => scene == Scene.Change ? ChangeVeilFlare() : 0f;

        /// <summary>How far her bow has formed (EmpressBow).</summary>
        private float BowFormNow(NPC npc) => scene == Scene.Change ? ChangeBowForm() : npc.AI_120_HallowBoss_IsInPhase2() ? 1f : 0f;

        // ---- drawing her ------------------------------------------------------------------------------------------

        /// <summary>
        /// Her second half, as the game draws her (vanilla's images of her as she dashes too), with her veil, her bow in
        /// her hand and her other arm as it is asked to be (or the game's own frame for it); a light behind her if asked.
        /// Expects the NPCs' batch open, and leaves it so.
        /// </summary>
        private void DrawSecondHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, float bright = 0f, float glow = 0f, float opacity = 1f, Vector2? at = null, float white = 0f, float size = 1f)
        {
            EmpressArt.Look look = EmpressArt.Look.Of(npc);
            look.Opacity = npc.Opacity * opacity;
            look.Bright = bright;
            look.White = white;
            look.Size = size;
            look.NoArms = true;
            look.Veil = VeilNow(npc);
            look.VeilFlare = VeilFlareNow;
            look.VeilWhite = scene == Scene.Change ? ChangeVeilWhite() : 0f;

            if (at.HasValue)
                look.At = at.Value;

            // Vanilla's own images of her as she dashes (and her fading through the middle of it).
            if (scene == Scene.None && (npc.ai[0] == 8f || npc.ai[0] == 9f))
                look.Opacity *= DrawDashImages(npc, spriteBatch, screenPos);

            if (glow > 0.003f)
            {
                Restart(spriteBatch, true);
                DrawBackGlow(spriteBatch, look.At, glow * look.Opacity, 1f);
                Restart(spriteBatch, false);
            }

            EmpressArt.DrawHer(spriteBatch, npc, screenPos, look);
            DrawSecondArms(npc, spriteBatch, screenPos, look);
        }

        /// <summary>Her arms and her bow, over her: the bow, then the hand that holds it, then her other arm.</summary>
        private void DrawSecondArms(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, EmpressArt.Look look)
        {
            if (look.Opacity <= 0.003f)
                return;

            bool left = BowLeft(npc);
            Vector2 middle = look.At;
            Vector2 onScreen = middle - screenPos;
            float scale = npc.scale * look.Size;
            Color colour = Color.White * look.Opacity;
            Color light = EmpressArt.Additive(Color.White, look.Bright * look.Opacity);
            float form = BowFormNow(npc);

            BowNow(npc, middle, out Vector2 grip, out float aim, out Vector2 nock, out Vector2 hold);

            // The bow, in the light it is made of (smoothly sampled: it is far larger than its picture).
            if (form > 0.003f)
            {
                Restart(spriteBatch, true);
                EmpressBow.Draw(spriteBatch, grip, aim, form, nock, bowArrows, bowSpread, bowTwang, look.Opacity, bowLook, bowDrawNow, hold);
                Restart(spriteBatch, false);
            }

            // The hand that holds it.
            EmpressArt.DrawArmAt(spriteBatch, onScreen, left, bowArmNow, colour, scale);

            if (look.Bright > 0.003f)
                EmpressArt.DrawArmAt(spriteBatch, onScreen, left, bowArmNow, light, scale, white: true);

            // Her other arm: drawing the string to her chest; or held as asked; or the game's own frame for it.
            bool freeLeft = !left;

            if (bowDrawNow > 0.05f && form > 0.5f)
            {
                DrawFreeFrame(spriteBatch, onScreen, freeLeft, 1, colour, light, scale, look.Bright);
            }
            else if (!float.IsNaN(freeAngle))
            {
                EmpressArt.DrawArmAt(spriteBatch, onScreen, freeLeft, freeAngleNow, colour, scale);

                if (look.Bright > 0.003f)
                    EmpressArt.DrawArmAt(spriteBatch, onScreen, freeLeft, freeAngleNow, light, scale, white: true);
            }
            else
            {
                int frame = freeFrame;

                if (frame < 0)
                {
                    // The game's own for this arm: its frames are the sheets', flipped as she is (EmpressArt.VanillaArms).
                    EmpressArt.VanillaArms(npc, out int sheetLeft, out int sheetRight);
                    bool flipped = npc.spriteDirection == 1;
                    int screenLeft = flipped ? sheetRight : sheetLeft;
                    int screenRight = flipped ? sheetLeft : sheetRight;
                    frame = freeLeft ? screenLeft : screenRight;
                }

                DrawFreeFrame(spriteBatch, onScreen, freeLeft, frame, colour, light, scale, look.Bright);
            }
        }

        private static void DrawFreeFrame(SpriteBatch spriteBatch, Vector2 onScreen, bool leftArm, int frame, Color colour, Color light, float scale, float bright)
        {
            EmpressArt.DrawArmFrame(spriteBatch, onScreen, leftArm, frame, colour, scale);

            if (bright > 0.003f)
                EmpressArt.DrawArmFrame(spriteBatch, onScreen, leftArm, frame, light, scale, white: true);
        }

        /// <summary>
        /// Vanilla's images of her as she dashes (Main.DrawNPCDirect_HallowBoss, 1.4.4): three either side spinning about
        /// her in rainbow colours, and four left behind along her way. How much of her shows through the middle of the
        /// dash is given back (she fades a moment, as vanilla has her).
        /// </summary>
        private float DrawDashImages(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float t = npc.ai[1];
            float spin = Utils.GetLerpValue(0f, 30f, t, true) * Utils.GetLerpValue(90f, 30f, t, true);
            float strength = Utils.GetLerpValue(0f, 30f, t, true) * Utils.GetLerpValue(90f, 70f, t, true);
            float fade = Utils.GetLerpValue(0f, 15f, t, true) * Utils.GetLerpValue(45f, 30f, t, true);

            if (strength <= 0.003f)
                return 1f - fade;

            Main.instance.LoadNPC(npc.type);

            Texture2D wings = EmpressArt.WingsBack;
            Texture2D glow = EmpressArt.WingsGlow;
            Texture2D streamers = EmpressArt.Tentacles;
            Texture2D body = EmpressArt.Body;
            Texture2D leftSheet = EmpressArt.ArmLeft;
            Texture2D rightSheet = EmpressArt.ArmRight;
            SpriteEffects effects = EmpressArt.Effects(npc);
            Vector2 middle = npc.Center - screenPos;
            float time = (float)Main.timeForVisualEffects / 60f;

            Rectangle wingFrame = wings.Frame(1, 11, 0, (int)(npc.localAI[0] / 4f) % 11);
            Rectangle streamerFrame = streamers.Frame(1, 8, 0, (int)(npc.localAI[0] / 4f) % 8);
            Rectangle bodyFrame = body.Frame(1, 2, 0, 1);
            EmpressArt.VanillaArms(npc, out int armLeft, out int armRight);
            Rectangle leftFrame = leftSheet.Frame(1, EmpressArt.ArmFrames, 0, armLeft);
            Rectangle rightFrame = rightSheet.Frame(1, EmpressArt.ArmFrames, 0, armRight);

            for (int i = -3; i <= 7; i++)
            {
                if (i == 0)
                    continue;

                Vector2 at = middle;
                Color colour;

                if (i > 3)
                {
                    float behind = Utils.GetLerpValue(30f, 70f, t, true);

                    if (behind <= 0f)
                        continue;

                    float hue = (i + 5f) / 10f;
                    colour = Main.hslToRgb(hue, 1f, MathHelper.Lerp(0.5f, 1f, Utils.GetLerpValue(90f, 0f, t, true))) * 0.8f * strength * (1f - fade);
                    colour.A /= 3;
                    at = middle + npc.velocity * -3f * (i - 4f) * behind;
                }
                else
                {
                    float hue = (i + 5f) / 10f;
                    float reach = 200f;
                    Matrix turn = Matrix.CreateRotationX((time - 0.3f + i * 0.1f) * 0.7f * MathHelper.TwoPi)
                        * Matrix.CreateRotationY((time - 0.8f + i * 0.3f) * 0.7f * MathHelper.TwoPi)
                        * Matrix.CreateRotationZ((time + i * 0.5f) * 0.1f * MathHelper.TwoPi);
                    Vector3 way = Vector3.Transform(Vector3.Forward, turn);
                    reach += Utils.GetLerpValue(-1f, 1f, way.Z, true) * 150f;

                    colour = Main.hslToRgb(hue, 1f, MathHelper.Lerp(0.5f, 1f, Utils.GetLerpValue(90f, 0f, t, true))) * 0.8f * strength;
                    colour.A /= 3;
                    at += (new Vector2(way.X, way.Y) * reach * spin).RotatedBy(t / 180f * MathHelper.TwoPi);
                }

                spriteBatch.Draw(wings, at, wingFrame, colour, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, effects, 0f);
                spriteBatch.Draw(glow, at, wingFrame, colour, npc.rotation, wingFrame.Size() / 2f, npc.scale * 2f, effects, 0f);
                spriteBatch.Draw(streamers, at, streamerFrame, colour, npc.rotation, EmpressArt.BodyOrigin, npc.scale, effects, 0f);
                // Her images as she is now: her own second body, veiled, if she has it.
                Texture2D now = EmpressVeil.Current;

                if (now != null)
                    spriteBatch.Draw(now, at, null, colour, npc.rotation, EmpressArt.BodyOrigin, npc.scale, effects, 0f);
                else
                    spriteBatch.Draw(body, at, bodyFrame, colour, npc.rotation, EmpressArt.BodyOrigin, npc.scale, effects, 0f);
                spriteBatch.Draw(leftSheet, at, leftFrame, colour, npc.rotation, leftFrame.Size() / 2f, npc.scale, effects, 0f);
                spriteBatch.Draw(rightSheet, at, rightFrame, colour, npc.rotation, rightFrame.Size() / 2f, npc.scale, effects, 0f);
            }

            return 1f - fade;
        }

        /// <summary>Her, and what an act of her second half shows of her (DrawLegends).</summary>
        private void DrawSecond(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (scene == Scene.Change)
            {
                DrawChangeHer(npc, spriteBatch, screenPos);
                return;
            }

            if (scene == Scene.Grab)
            {
                DrawGrab(npc, spriteBatch, screenPos);
                return;
            }

            if (scene != Scene.Act || !IsSecondAct(act))
            {
                DrawSecondHer(npc, spriteBatch, screenPos, 0f, scene == Scene.None ? 0f : 0.3f);
                return;
            }

            switch (act)
            {
                case Act.Dawn:
                    // The false day's light and her shadow behind her and everyone (bands: no batch open).
                    spriteBatch.End();

                    try
                    {
                        DrawDawnBands(npc);
                    }
                    finally
                    {
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                    }

                    DrawSecondHer(npc, spriteBatch, screenPos, 0.25f * DawnLight(), 0.35f);
                    break;

                case Act.Blink: DrawBlinkHer(npc, spriteBatch, screenPos); break;
                case Act.Shield: DrawShieldHer(npc, spriteBatch, screenPos); break;
                case Act.Heart: DrawHeartHer(npc, spriteBatch, screenPos); break;
                case Act.Blade: DrawBladeHer(npc, spriteBatch, screenPos); break;
                default: DrawSecondHer(npc, spriteBatch, screenPos, 0f, 0.35f); break;
            }
        }

        /// <summary>What goes over the players for an act of her second half (DrawLegendsOver).</summary>
        private void DrawSecondOver(NPC npc, SpriteBatch spriteBatch)
        {
            switch (act)
            {
                case Act.Moor: DrawMoorOver(npc, spriteBatch); break;
                case Act.Negative: DrawNegativeOver(npc, spriteBatch); break;
                case Act.Cage: DrawCageOver(npc, spriteBatch); break;
                case Act.Constellation: DrawConstellationOver(npc, spriteBatch); break;
                case Act.Blink: DrawBlinkOver(npc, spriteBatch); break;
                case Act.Shield: DrawShieldOver(npc, spriteBatch); break;
                case Act.Ricochet: DrawRicochetOver(npc, spriteBatch); break;
                case Act.Heart: DrawHeartOver(npc, spriteBatch); break;
                case Act.Lightspeed: DrawLightspeedOver(npc, spriteBatch); break;
                case Act.Dawn: DrawDawnOver(npc, spriteBatch); break;
                case Act.Refraction: DrawRefractionOver(npc, spriteBatch); break;
                case Act.Rain: DrawRainOver(npc, spriteBatch); break;
                case Act.Blade: DrawBladeOver(npc, spriteBatch); break;
            }
        }

        /// <summary>The bands of an act of her second half over the players (no batch open: Strip). (The dawn's are behind: DrawSecond.)</summary>
        private void DrawSecondBands(NPC npc)
        {
            if (act == Act.Cage)
                DrawCageBands(npc);

            if (act == Act.Blade)
                DrawBladeBands(npc);
        }

        // ---- sync ---------------------------------------------------------------------------------------------

        private void WriteSecond(BinaryWriter writer)
        {
            writer.Write(unveiled);
            writer.Write(negativeOn);
            writer.Write((byte)Math.Clamp(negativeDone, 0, 255));
            writer.Write(shieldTaken);
            writer.Write((byte)Math.Clamp(lightHit, 0, 255));

            for (int i = 0; i < blinkPlaces.Length; i++)
                writer.WriteVector2(blinkPlaces[i]);
        }

        private void ReadSecond(BinaryReader reader)
        {
            unveiled = reader.ReadBoolean();

            negativeOn = reader.ReadBoolean();
            negativeDone = reader.ReadByte();

            shieldTaken = reader.ReadInt32();
            lightHit = reader.ReadByte();

            for (int i = 0; i < blinkPlaces.Length; i++)
                blinkPlaces[i] = reader.ReadVector2();
        }
    }
}
