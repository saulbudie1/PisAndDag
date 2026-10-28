using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- in Legends, her first half ------------------------------------------------------------------------
    //
    // By request, in a Legends world, through her first half (until vanilla's own change at half her life):
    //
    //   HER DASH       becomes a grab (EmpressOfLight.Grab): a dash that meets a player stops on them; she
    //                  holds them out in one hand, the camera leans in, her other hand fills with a ball of
    //                  light, and she pushes it into them: a blast of about 240 (EmpressBlast). One that
    //                  misses goes by as vanilla's does. (Her second half's dash is vanilla's, by request.)
    //   SEVEN ACTS     of her own, every other move, the other being vanilla's own next move as it would
    //                  have come: chosen at random, never one of the last three she played ("no repetition
    //                  also dont repeat attack for more than 3 moves"):
    //                    the mirror crack (Crack), the laser pillars (Pillars), the prism (Prism), the light
    //                    chainsaw (Saw, run with her vanilla moves going on, but no dash and no darting),
    //                    and, by choice from those offered, the mirage (Mirage), the eclipse (Eclipse) and the
    //                    aurora curtains (Aurora).
    //   BY DAY         every one of them kills, as all of hers do by day.
    //   HER WORDS      now and then a line at the foot of the screen, underlined, gone by itself (Talk, Taunt).
    //
    // And all through, both halves, by request: her life x1.65, her defense x1.15 on her own (which is a quarter
    // over vanilla's in every world), and the blows of her body and of all she throws x1.2 (SetDefaults,
    // EmpressShots).
    //
    // Each act puts everything back as it was when it is done ("things have to return to normal after she
    // done with her act"): the light, the world, her copies, the curtains, the mirror.
    //
    // The server (or a single player) decides which act, where, and each step of it (the scene machinery:
    // Scene.Act, its beat and its clock, and the act's own few numbers, SendExtraAI); every machine plays it.
    // Whatever hurts is a hostile projectile the server makes (EmpressHitbox), so every player is hurt by
    // their own machine as vanilla's lances hurt them, with the game's own immunity, dodges and death lines.

    public partial class EmpressOfLight
    {
        /// <summary>
        /// Her acts, in Legends. Her first half's: the first four by request, the next three chosen from those offered. Her
        /// second half's (EmpressOfLight.Second), from the moored volley on: the first eight by request, the last five
        /// chosen from those offered.
        /// </summary>
        internal enum Act : byte
        {
            None,
            Crack,
            Pillars,
            Prism,
            Saw,
            Mirage,
            Eclipse,
            Aurora,

            Moor,
            Negative,
            Cage,
            Constellation,
            Blink,
            Shield,
            Ricochet,
            Heart,
            Lightspeed,
            Dawn,
            Refraction,
            Rain,
            Blade,
        }

        private static readonly Act[] Acts = { Act.Crack, Act.Pillars, Act.Prism, Act.Saw, Act.Mirage, Act.Eclipse, Act.Aurora };

        /// <summary>The act she is in (with Scene.Act), and the three before it, newest first.</summary>
        private Act act;
        private readonly Act[] recent = new Act[3];

        /// <summary>Her next move is one of her acts (the server's: every other move).</summary>
        private bool newNext;

        /// <summary>Vanilla's state as this tick began: how a choice of vanilla's own is seen.</summary>
        private int stateBefore = -1;

        // ---- an act's own numbers, the server's choice, sent ---------------------------------------------------

        /// <summary>A point the act is about (where it is centred), a turn (an angle or a direction), a side (one way or the other).</summary>
        private Vector2 actAt;
        private float actTurn;
        private int actSide = 1;

        /// <summary>For what is only seen: the same on every machine.</summary>
        private int actSeed;

        /// <summary>Up to three points (one about each player it is for, or her copies' places), and how many.</summary>
        private readonly Vector2[] actPoints = new Vector2[3];
        private int actCount;

        /// <summary>A player it is about (her target as it began), or -1.</summary>
        private int actWho = -1;

        /// <summary>A second number of the act's own (how many pillars; which of her is real).</summary>
        private int actN;

        // ---- when ----------------------------------------------------------------------------------------------

        /// <summary>Legends, her first half, her arena up, and not dying: when all of this is hers.</summary>
        private bool LegendsHalf(NPC npc) => PADWorld.Legends && arenaSet && !dying && !npc.AI_120_HallowBoss_IsInPhase2();

        /// <summary>Vanilla's pause before choosing its next move (AI_120, its state 1), in its first half.</summary>
        private static float ChoiceTicks => Main.getGoodWorld ? 22.5f : 45f;

        /// <summary>Vanilla's first half, move by move (AI_120, its state 1): what it does at each count of its cycle.</summary>
        private static readonly int[] Cycle = { 2, 8, 6, 8, 5, 2, 8, 4, 8, 5 };

        /// <summary>
        /// How much an act's blow takes, as vanilla reckons hers (NPC.GetAttackDamage_ForProjectiles: a number
        /// for a normal world and one for an expert one, doubled by the game as any hostile shot's is). By day,
        /// as everything of hers, it kills.
        /// </summary>
        private static int Hurt(NPC npc, int normal, int expert) => NPC.ShouldEmpressBeEnraged() ? 9999 : npc.GetAttackDamage_ForProjectiles(normal, expert);

        /// <summary>Her target, or null if she has none worth the name.</summary>
        private static Player TargetOf(NPC npc)
        {
            if (npc.target < 0 || npc.target >= Main.maxPlayers)
                return null;

            Player p = Main.player[npc.target];

            return p.active && !p.dead && Vector2.Distance(p.Center, npc.Center) < 6400f ? p : null;
        }

        // ---- the choice ----------------------------------------------------------------------------------------

        /// <summary>
        /// Before vanilla's AI, while no scene has her (PreAI): in her Legends half, on the tick vanilla would
        /// choose its next move, an act instead when it is an act's turn. True if an act took the tick.
        /// </summary>
        private bool LegendsChoose(NPC npc)
        {
            stateBefore = (int)npc.ai[0];

            if (!LegendsHalf(npc))
            {
                sawing = false;
                return false;
            }

            if (sawing)
            {
                SawBeforeVanilla(npc);
                return false;
            }

            if (!Authority || !newNext || npc.ai[0] != 1f || npc.ai[1] + 1f < ChoiceTicks)
                return false;

            // At half her life her change is vanilla's to make, and it makes it on this choice.
            if (npc.life <= npc.lifeMax / 2)
                return false;

            // Nor when vanilla would leave on this choice: called by day and the day over (its state 13).
            if (npc.AI_120_HallowBoss_IsGenuinelyEnraged() && (!Main.dayTime || Main.time >= 53400.0))
                return false;

            Player target = TargetOf(npc);

            if (target == null)
                return false;

            StartAct(npc, target);
            return true;
        }

        /// <summary>After vanilla's AI (PostAI): a choice of vanilla's seen, the saw's running, the dash's grab.</summary>
        private void LegendsAfter(NPC npc)
        {
            // The saw going out, a tick at a time, on every machine; heard as it starts to.
            if (sawFade > 0 && !sawing)
            {
                if (sawFade == SawFadeTicks)
                    SawGoesOut(npc);

                sawFade--;
            }

            if (scene != Scene.None || !LegendsHalf(npc))
                return;

            bool chose = stateBefore == 1 && npc.ai[0] != 1f;
            bool done = stateBefore > 1 && stateBefore != 13 && npc.ai[0] == 1f;

            if (chose && Authority)
                newNext = true;

            if (sawing)
                SawAfterVanilla(npc, chose, done);

            // At half her life, vanilla chooses its change: hers instead, a scene of its own (EmpressOfLight.Change).
            if (chose && npc.ai[0] == 10f)
            {
                if (Authority)
                    BeginChange(npc);

                return;
            }

            if (Authority && (npc.ai[0] == 8f || npc.ai[0] == 9f))
                DashMeets(npc);
        }

        /// <summary>One of her acts, chosen: never one of the last three. The server's.</summary>
        private void StartAct(NPC npc, Player target)
        {
            Span<Act> open = stackalloc Act[Acts.Length];
            int count = 0;

            foreach (Act a in Acts)
            {
                if (a != recent[0] && a != recent[1] && a != recent[2])
                    open[count++] = a;
            }

            Act chosen = open[Main.rand.Next(count)];

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

            switch (chosen)
            {
                case Act.Crack: PlanCrack(npc, target); break;
                case Act.Pillars: PlanPillars(npc, target); break;
                case Act.Prism: PlanPrism(npc, target); break;
                case Act.Saw: PlanSaw(npc, target); break;
                case Act.Mirage: PlanMirage(npc, target); break;
                case Act.Eclipse: PlanEclipse(npc, target); break;
                case Act.Aurora: PlanAurora(npc, target); break;
            }

            Go(npc, Scene.Act, 0);
        }

        /// <summary>
        /// Her act is over: back to vanilla, at its pause before choosing (its state 1), its cycle where it was,
        /// and the next move vanilla's. The server's call, and sent.
        /// </summary>
        private void EndAct(NPC npc, bool keepState = false)
        {
            Scene ended = scene;

            // Whatever of it still hurts goes with it.
            KillHitboxes(npc);

            scene = Scene.None;
            beat = 0;
            timer = 0;

            Restore(npc, ended);

            if (Authority && !keepState)
            {
                npc.ai[0] = 1f;
                npc.ai[1] = 0f;
                npc.TargetClosest(false);
            }

            npc.netUpdate = true;
        }

        /// <summary>Is this act of hers going on (for her hitboxes: one whose act is over goes).</summary>
        internal bool ActRunning(Act which) => which == Act.Saw ? sawing || scene == Scene.Act && act == Act.Saw : scene == Scene.Act && act == which;

        // ---- an act, every tick, every machine ----------------------------------------------------------------

        private void RunAct(NPC npc, bool entered)
        {
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.damage = ContactDamage(npc);
            npc.Opacity = 1f;

            Player who = actWho >= 0 && actWho < Main.maxPlayers && Main.player[actWho].active && !Main.player[actWho].dead
                ? Main.player[actWho]
                : TargetOf(npc);

            // Everybody gone: the act is let go of, and vanilla takes her away.
            if (who == null)
            {
                if (Authority)
                    EndAct(npc);

                return;
            }

            if (npc.target != who.whoAmI && Authority)
                npc.target = who.whoAmI;

            // She screams as each act begins, each act at its own pitch; and most times says something of it (Talk).
            if (entered && beat == 0)
            {
                Scream(npc, ActPitch(act), 1f, rings: true);
                ActWords(npc);
            }

            switch (act)
            {
                case Act.Crack: RunCrack(npc, who, entered); break;
                case Act.Pillars: RunPillars(npc, who, entered); break;
                case Act.Prism: RunPrism(npc, who, entered); break;
                case Act.Saw: RunSawConjure(npc, who, entered); break;
                case Act.Mirage: RunMirage(npc, who, entered); break;
                case Act.Eclipse: RunEclipse(npc, who, entered); break;
                case Act.Aurora: RunAurora(npc, who, entered); break;
                default:
                    RunSecondAct(npc, who, entered);
                    break;
            }

            // Facing whoever it is about, as vanilla has her face (her second half's acts may face her otherwise).
            if (!IsSecondAct(act) || !facingHeld)
                npc.spriteDirection = npc.direction = who.Center.X >= npc.Center.X ? 1 : -1;

            facingHeld = false;
        }

        // ---- her voice, and the sound of her acts ------------------------------------------------------------
        //
        // By request ("she needs more sounds in her attacks, pitch it high and lows"; "she needs more of her
        // screams during the gameplay"): the game's own sounds, layered high and low; her scream (her dash's
        // shriek, her cry under it, the game's) at the start of every act with a ring of her pink, and at the
        // height of each.

        /// <summary>A sound at a pitch and loudness, heard where it is; a few may ring at once, so one can be laid over another an octave off. None on a server.</summary>
        private static void Sound(SoundStyle style, Vector2 at, float pitch, float volume = 1f, int instances = 4)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(style with { Pitch = MathHelper.Clamp(pitch, -1f, 1f), Volume = volume, MaxInstances = instances }, at);
        }

        /// <summary>Her scream, at a pitch: her dash's shriek with her cry under it; a ring of her pink off her for a big one.</summary>
        private static void Scream(NPC npc, float pitch, float volume = 1f, bool rings = false)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Item160, npc.Center, pitch, volume);
            Sound(SoundID.Item161, npc.Center, pitch - 0.35f, volume * 0.75f);

            if (rings)
                Cinematic.Roar(npc, 40, 0.55f, quiet: true, rim: EmpressArt.Pink);
        }

        /// <summary>The pitch of the scream she starts each act with: high for the bright ones, low for the dark.</summary>
        private static float ActPitch(Act which) => which switch
        {
            Act.Crack => 0.35f,
            Act.Pillars => -0.25f,
            Act.Prism => 0.15f,
            Act.Saw => -0.4f,
            Act.Mirage => 0.55f,
            Act.Eclipse => -0.6f,
            Act.Aurora => 0.25f,
            Act.Moor => 0.1f,
            Act.Negative => -0.7f,
            Act.Cage => 0.3f,
            Act.Constellation => 0.45f,
            Act.Blink => 0.65f,
            Act.Shield => -0.35f,
            Act.Ricochet => 0.5f,
            Act.Heart => 0.2f,
            Act.Lightspeed => 0.6f,
            Act.Dawn => -0.5f,
            Act.Refraction => 0.35f,
            Act.Rain => -0.1f,
            Act.Blade => -0.25f,
            _ => 0f,
        };

        /// <summary>An act of her second half has her face its own way this tick (not her target's): set as it runs.</summary>
        private bool facingHeld;

        // ---- moving her ----------------------------------------------------------------------------------------

        /// <summary>Flies her toward a place as vanilla flies her between moves: this fast at the most, turning this readily.</summary>
        private static void FlyTo(NPC npc, Vector2 place, float speed, float ease)
        {
            Vector2 way = place - npc.Center;
            float distance = way.Length();

            if (distance < 8f)
            {
                npc.velocity *= 0.8f;
                return;
            }

            Vector2 want = way / distance * Math.Min(speed, distance / 8f);
            npc.velocity = Vector2.Lerp(npc.velocity, want, ease);
        }

        /// <summary>A slow bob in place, for her hanging in the air through an act.</summary>
        private static Vector2 Sway(float t, float size = 10f) => new Vector2((float)Math.Sin(t * 0.031f) * size * 0.6f, (float)Math.Sin(t * 0.047f) * size);

        // ---- what hurts ---------------------------------------------------------------------------------------

        /// <summary>One of her hitboxes, made by the server: unseen, hostile, the act draws what it is; hers (ai[2]), living this long at most.</summary>
        private static void Hitbox(NPC npc, EmpressHitbox.Shape shape, Vector2 at, float a, int damage, int ticks)
        {
            if (!Authority)
                return;

            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<EmpressHitbox>(), damage, 0f, Main.myPlayer, (float)shape, a, npc.whoAmI);

            if (index >= 0 && index < Main.maxProjectiles)
            {
                Main.projectile[index].timeLeft = ticks;
                Main.projectile[index].netUpdate = true;
            }
        }

        /// <summary>Her hitboxes, all of them, gone (the server's: its word goes to every machine).</summary>
        private static void KillHitboxes(NPC npc)
        {
            if (!Authority)
                return;

            int type = ModContent.ProjectileType<EmpressHitbox>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];

                if (p.active && p.type == type && (int)p.ai[2] == npc.whoAmI)
                    p.Kill();
            }
        }

        /// <summary>Is this body in her arena (or near enough its edge)? Her blows reach nobody outside it.</summary>
        internal bool InArena(Rectangle box) => !arenaSet || Decagon.Inside(box.Center.ToVector2(), arenaCentre, EmpressArena.Radius, -24f);

        /// <summary>Her body's blow, as vanilla reckons it each tick (AI_120): her damage scaled for the world, less in an expert one; by day, death.</summary>
        private static int ContactDamage(NPC npc) => NPC.ShouldEmpressBeEnraged() ? 9999 : npc.GetAttackDamage_ScaledByStrength(npc.defDamage * (Main.expertMode ? 0.3f : 1f));

        // ---- sync ---------------------------------------------------------------------------------------------

        private void WriteLegends(BinaryWriter writer)
        {
            writer.Write((byte)act);
            writer.WriteVector2(actAt);
            writer.Write(actTurn);
            writer.Write((sbyte)actSide);
            writer.Write(actSeed);
            writer.Write((byte)actCount);

            for (int i = 0; i < actPoints.Length; i++)
                writer.WriteVector2(actPoints[i]);

            writer.Write((byte)(actWho < 0 ? byte.MaxValue : actWho));
            writer.Write((short)actN);

            writer.Write(sawing);
            writer.Write((short)Math.Clamp(sawTimer, 0, short.MaxValue));
            writer.Write(sawAngle);

            writer.Write((byte)(grabbed < 0 ? byte.MaxValue : grabbed));
        }

        private void ReadLegends(BinaryReader reader)
        {
            act = (Act)reader.ReadByte();
            actAt = reader.ReadVector2();
            actTurn = reader.ReadSingle();
            actSide = reader.ReadSByte();
            actSeed = reader.ReadInt32();
            actCount = reader.ReadByte();

            for (int i = 0; i < actPoints.Length; i++)
                actPoints[i] = reader.ReadVector2();

            int who = reader.ReadByte();
            actWho = who == byte.MaxValue ? -1 : who;
            actN = reader.ReadInt16();

            bool wasSawing = sawing;
            sawing = reader.ReadBoolean();
            int clock = reader.ReadInt16();
            float angle = reader.ReadSingle();

            if (sawing && (!wasSawing || Math.Abs(clock - sawTimer) > 6))
            {
                sawTimer = clock;
                sawAngle = angle;
            }

            if (wasSawing && !sawing)
                sawFade = SawFadeTicks;

            int held = reader.ReadByte();
            grabbed = held == byte.MaxValue ? -1 : held;
        }

        // ---- her arms, held where an act wants them ---------------------------------------------------------

        /// <summary>
        /// How her arms are held for this frame of an act: by angle (each arm turned about its shoulder, the
        /// game's picture of it held straight out, cut from its sheet: EmpressArt.DrawArmAt), or by the game's
        /// own frames when it is not posed. Left and right are the screen's.
        /// </summary>
        internal struct Arms
        {
            public bool Posed;
            public float Left;
            public float Right;
            public int LeftFrame;
            public int RightFrame;

            public static Arms Frames(int left, int right) => new Arms { LeftFrame = left, RightFrame = right };

            public static Arms At(float left, float right) => new Arms { Posed = true, Left = left, Right = right };
        }

        /// <summary>Straight up; level out to each side; and down beside her.</summary>
        internal const float Up = -MathHelper.PiOver2;
        internal const float Down = MathHelper.PiOver2;
        internal static float OutLeft => MathHelper.Pi;
        internal const float OutRight = 0f;

        /// <summary>Her palm, at the end of an arm held at this angle (the screen's left or right arm).</summary>
        internal static Vector2 Palm(NPC npc, bool leftArm, float angle) => EmpressArt.PalmAt(npc.Center, leftArm, angle, npc.scale);

        /// <summary>
        /// Draws her for an act: as the game draws her, with her arms as the act holds them, and a light behind
        /// her if it asks. Expects the NPCs' batch open, and leaves it so.
        /// </summary>
        private static void DrawHerActing(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Arms arms, float bright = 0f, float glow = 0f, float opacity = 1f, Vector2? at = null)
        {
            EmpressArt.Look look = EmpressArt.Look.Of(npc);
            look.Opacity = opacity;
            look.Bright = bright;

            if (at.HasValue)
                look.At = at.Value;

            if (glow > 0.003f)
            {
                Restart(spriteBatch, true);
                DrawBackGlow(spriteBatch, look.At, glow * opacity, 1f);
                Restart(spriteBatch, false);
            }

            if (!arms.Posed)
            {
                look.LeftArm = arms.LeftFrame;
                look.RightArm = arms.RightFrame;
                EmpressArt.DrawHer(spriteBatch, npc, screenPos, look);
                return;
            }

            look.NoArms = true;
            EmpressArt.DrawHer(spriteBatch, npc, screenPos, look);

            Color colour = Color.White * opacity;

            EmpressArt.DrawArmAt(spriteBatch, look.At - screenPos, true, arms.Left, colour, npc.scale);
            EmpressArt.DrawArmAt(spriteBatch, look.At - screenPos, false, arms.Right, colour, npc.scale);

            if (bright > 0.003f)
            {
                Color light = EmpressArt.Additive(Color.White, bright * opacity);
                EmpressArt.DrawArmAt(spriteBatch, look.At - screenPos, true, arms.Left, light, npc.scale, white: true);
                EmpressArt.DrawArmAt(spriteBatch, look.At - screenPos, false, arms.Right, light, npc.scale, white: true);
            }
        }

        /// <summary>Her and what an act shows of her (PreDraw, for Scene.Act and Scene.Grab, and the saw; and her second half).</summary>
        private void DrawLegends(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (SecondLook(npc))
            {
                DrawSecond(npc, spriteBatch, screenPos);
                return;
            }

            if (scene == Scene.Grab)
            {
                DrawGrab(npc, spriteBatch, screenPos);
                return;
            }

            if (scene == Scene.None)
            {
                DrawSawing(npc, spriteBatch, screenPos);
                return;
            }

            switch (act)
            {
                case Act.Crack: DrawCrackHer(npc, spriteBatch, screenPos); break;
                case Act.Pillars: DrawPillarsHer(npc, spriteBatch, screenPos); break;
                case Act.Prism: DrawPrismHer(npc, spriteBatch, screenPos); break;
                case Act.Saw: DrawSawing(npc, spriteBatch, screenPos); break;
                case Act.Mirage: DrawMirageHer(npc, spriteBatch, screenPos); break;
                case Act.Eclipse: DrawEclipseHer(npc, spriteBatch, screenPos); break;
                case Act.Aurora: DrawAuroraHer(npc, spriteBatch, screenPos); break;
                default: EmpressArt.DrawHer(spriteBatch, npc, screenPos, EmpressArt.Look.Of(npc)); break;
            }
        }

        /// <summary>Does she draw her act herself this frame (PreDraw false)? (As the saw goes out, vanilla draws her again.) Her second half in Legends is always hers.</summary>
        private bool DrawsLegends => scene == Scene.Act || scene == Scene.Grab || scene == Scene.Change || sawing || secondDrawn;

        /// <summary>Her second half in Legends, as of her last tick (DrawsLegends, which has no NPC to ask).</summary>
        private bool secondDrawn;

        /// <summary>What goes over the players for an act (EmpressOfLight.DrawOverPlayers): its light, its beams, its walls.</summary>
        private void DrawLegendsOver(NPC npc)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;

            // Bands first (Strip draws with no batch open), then everything the batch draws.
            DrawLegendsBands(npc);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            try
            {
                if (scene == Scene.Grab)
                    DrawGrabOver(npc, spriteBatch);
                else if (scene == Scene.Change)
                    DrawChangeOver(npc, spriteBatch);
                else if (scene == Scene.Act)
                {
                    switch (act)
                    {
                        case Act.Crack: DrawCrackOver(npc, spriteBatch); break;
                        case Act.Pillars: DrawPillarsOver(npc, spriteBatch); break;
                        case Act.Prism: DrawPrismOver(npc, spriteBatch); break;
                        case Act.Mirage: DrawMirageOver(npc, spriteBatch); break;
                        case Act.Eclipse: DrawEclipseOver(npc, spriteBatch); break;
                        case Act.Aurora: DrawAuroraOver(npc, spriteBatch); break;
                        default: DrawSecondOver(npc, spriteBatch); break;
                    }
                }

                // The negative's edge, as it comes back into her.
                if (negativeFade > 0 && !(scene == Scene.Act && act == Act.Negative))
                    DrawNegativeEdge(npc, spriteBatch);


                if (sawing || sawFade > 0 || scene == Scene.Act && act == Act.Saw)
                    DrawSawOver(npc, spriteBatch);
            }
            finally
            {
                spriteBatch.End();
            }
        }

        /// <summary>The bands of an act (no batch open): the aurora's curtains, the eclipse's ring.</summary>
        private void DrawLegendsBands(NPC npc)
        {
            if (sawing || sawFade > 0 || scene == Scene.Act && act == Act.Saw)
                DrawSawBands(npc);

            if (scene != Scene.Act)
                return;

            if (act == Act.Aurora)
                DrawAuroraBands(npc);
            else if (act == Act.Eclipse)
                DrawEclipseBands(npc);
            else if (IsSecondAct(act))
                DrawSecondBands(npc);
        }

        /// <summary>Is there anything of an act to draw over the players this frame?</summary>
        private bool LegendsOver => scene == Scene.Grab || scene == Scene.Act || scene == Scene.Change || sawing || sawFade > 0 || negativeFade > 0;

        /// <summary>Might anything of hers bend the world this tick (EmpressWarp listens while so)?</summary>
        internal bool Bending => !dying && (scene == Scene.Act || scene == Scene.Grab || scene == Scene.Change || sawing || sawFade > 0);

        /// <summary>The part of the world on this machine's screen, world pixels (no margin).</summary>
        private static void View(out float left, out float right, out float top, out float bottom)
        {
            float zoom = Math.Max(Main.GameViewMatrix.Zoom.X, 0.25f);
            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f / zoom;

            left = middle.X - half.X;
            right = middle.X + half.X;
            top = middle.Y - half.Y;
            bottom = middle.Y + half.Y;
        }
    }
}
