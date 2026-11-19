using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// Skeletron Prime, reworked (by request: "Skeletron Prime: The whole idea is the nuclear powered war machine, his
    /// attacks will be nuclear based"). Its element (by the user's lore): nuclear, the green #bbff00. This is its head;
    /// its arms are <see cref="PrimeArm"/>.
    ///
    ///   ITS COMING (Enter)       "like normal but only head comes in on top, robotic screech, then a pair of arms flies
    ///                            out of behind the head, then another pair, they are activated and have a red led on
    ///                            each, the eye also sparks red and have a trail of red in the iris. Every arms points
    ///                            up and shoots at the sky". (Prime.Enter)
    ///   ITS ARMS                 fly free of it with vanilla's reach, each one rigid limb (a hand and two bones) that
    ///                            never changes length (by request: "make it so that the limb length stay the same,
    ///                            because skeletron stretches it"). An arm that would die is held at one life,
    ///                            untouchable, harmless, without its AI: it falls to the ground and lies flat there.
    ///                            (PrimeArm)
    ///   NO ARM LEFT (Change)     "Cinematic, always spikes on his head, things go dark and he shakes with sparks, and
    ///                            his eyes glow the nuclear green and has a long trail of that green as well". Its
    ///                            spikes and green eyes stay; what it does is vanilla's still (asked after: look only).
    ///                            (Prime.Change)
    ///   ITS END (Finale)         "pans over, shakes, opens mouth and theres a expo glowing nuclear green aura from mouth
    ///                            like about to shoot something, Pans to the nearest arm and its moving, flaoting and
    ///                            flies over to the head, pans to head and all four limbs reach the mouth at once and
    ///                            cover its mouth, things go darker temporarily with only the rays of green from mouth
    ///                            that spreads outwards, covered by hands, and create a super cool impact explosion
    ///                            frame, creating a giant explosion and an area that is radio active for 5 mins."
    ///                            (Prime.Finale, PrimeBlast, PrimeFallout)
    ///
    ///   IN LEGENDS               three fifths its head's life, three times its arms'; its head cannot be hurt while an
    ///                            arm is up; its arms' own attacks, their specials and their attacks together; its own
    ///                            four once no arm is left. (Prime.Legends, Prime.Group, Prime.Moves; PrimeArm.Legends,
    ///                            PrimeArm.Group; PrimeShots)
    ///
    /// Out of Legends its attacks are vanilla's. Not where it is the Mechdusa's (NPC.IsMechQueenUp): vanilla's, all of it.
    ///
    /// The server (or a single player) decides every step; every machine plays it from that (SendExtraAI), counting on by
    /// itself between packets. Its arms are moved by its scenes on every machine, from its step and its clock.
    /// </summary>
    public partial class Prime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronPrime;

        internal enum Scene : byte
        {
            None,
            Enter,
            Change,
            Finale,

            /// <summary>One thing all its arms stop for (Legends): its head cannot be hurt.</summary>
            Group,

            /// <summary>One of its own attacks, once no arm is left (Legends): it can be struck, and strikes.</summary>
            Move,
        }

        private Scene scene;
        private int step;
        private int timer;

        private Scene playedScene;
        private int playedStep = -1;

        /// <summary>
        /// The furthest its clock has been seen in this step, on this machine: what happens at a tick happens as its
        /// clock passes that tick (<see cref="At"/>), so a clock set on a little by what the server says skips nothing,
        /// and one set back a little repeats nothing.
        /// </summary>
        private int seenTick = -1;

        /// <summary>Its coming has been decided (or there was none to have).</summary>
        private bool started;

        private bool named;

        /// <summary>Its death is being let through: the loot.</summary>
        private bool ending;

        /// <summary>No arm left, and it has changed: its spikes out for good, its eyes its green.</summary>
        private bool changed;

        /// <summary>Ticks no arm of it has been up (the server's count): its change begins after a few.</summary>
        private int armsGone;
        private const int ChangeAfter = 50;

        /// <summary>A blow that would kill it, dealt on this machine (a client), the server not yet answered.</summary>
        private int heldFor;
        private const int HeldTicks = 120;

        /// <summary>Made untouchable by this machine, and so to be given back.</summary>
        private bool shielded;

        /// <summary>How hard it shakes this tick, in pixels: only seen.</summary>
        internal float shake;
        private Vector2 drawShift;

        /// <summary>What its sparks and flickers are worked out from, the same on every machine.</summary>
        private int seed;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        private const float SceneRange = 5200f;

        // ---- its picture ------------------------------------------------------------------------------------------------
        //
        // Vanilla's head (NPC_127, 140 by 940): two blocks of 470, the first its eyes red, the second its sockets dark;
        // in each, three frames of 156: its mouth shut, its mouth open, its spikes out (its mouth shut). The first two
        // differ only in rows 108 to 143 (its jaw), the first and third only in rows 2 to 83 (its spikes): so any look
        // is the top of one over the bottom of another, cut at row 96, and its spikes can be out with its mouth open.
        // Measured: its eyes' middles 17.7 left and 18.4 right of its picture's middle and 7.4 over it, its mouth 34
        // under it shut and 39 open.

        internal const float FrameHigh = 156f;
        private const int FrameWide = 140;
        private const int Cut = 96;

        /// <summary>Vanilla places it by its picture's height over its frames (940 over 6), not by its frame's.</summary>
        private const float SheetStep = 940f / 6f;

        internal static bool IsArm(int type) => type >= NPCID.PrimeCannon && type <= NPCID.PrimeLaser;

        internal Scene Now => scene;
        internal int Step => step;
        internal int Clock => timer;
        internal int Seed => seed;
        internal bool Ending => ending;
        internal bool Started => started;

        /// <summary>The middle of its picture: what it is drawn about, and turns about.</summary>
        internal static Vector2 Middle(NPC npc) =>
            new(npc.Center.X, npc.position.Y + npc.height + 4f + (FrameHigh * 0.5f - SheetStep) * npc.scale + npc.gfxOffY);

        private static Vector2 OnFace(NPC npc, float x, float y) => Middle(npc) + new Vector2(x, y).RotatedBy(npc.rotation) * npc.scale;

        /// <summary>One of its eyes' middles (-1 its left as it is seen, 1 its right).</summary>
        internal static Vector2 Eye(NPC npc, int side) => OnFace(npc, side < 0 ? -17.7f : 18.4f, -7.4f);

        /// <summary>The middle of its mouth.</summary>
        internal Vector2 Mouth(NPC npc) => OnFace(npc, 0f, MouthOpen(npc) ? 39f : 34f);

        // ---- its arms ---------------------------------------------------------------------------------------------------

        private static readonly int[] armList = new int[8];

        /// <summary>Its arms that are here, into <see cref="armList"/>; how many.</summary>
        private static int FindArms(NPC head)
        {
            int n = 0;

            for (int i = 0; i < Main.maxNPCs && n < armList.Length; i++)
            {
                NPC arm = Main.npc[i];

                if (arm.active && IsArm(arm.type) && (int)arm.ai[1] == head.whoAmI)
                    armList[n++] = i;
            }

            return n;
        }

        // ---- the clock ----------------------------------------------------------------------------------------------------

        private void Go(NPC npc, Scene to, int at)
        {
            scene = to;
            step = at;
            timer = 0;

            if (Authority)
                npc.netUpdate = true;
        }

        private void Next(NPC npc, int at) => Go(npc, scene, at);

        internal static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A number from a number, the same on every machine (0 to 1).</summary>
        internal static float Chance(int from)
        {
            unchecked
            {
                uint h = (uint)from * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFF) / 65535f;
            }
        }

        // ---- the AI -------------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
            {
                Unshield(npc);
                return true;
            }

            if (heldFor > 0)
            {
                if (scene != Scene.None)
                    heldFor = 0;
                else
                    heldFor--;
            }

            shake = 0f;

            if (scene == Scene.None)
            {
                playedScene = Scene.None;
                playedStep = -1;
                StopSounds();

                // In Legends, while an arm of it is up (by request: "Head is locked invincible and only move around
                // untiil all arms are defeated much like skeletron").
                bool legends = PADWorld.Legends && started;
                bool locked = legends && !changed;

                // Untouchable while held, locked, and on a client not yet told of its coming. What this machine made
                // untouchable, this machine gives back.
                if (heldFor > 0 || locked || !Authority && !started)
                {
                    npc.dontTakeDamage = true;
                    shielded = true;

                    // Nothing that seeks wastes itself on it while it cannot be hurt: its arms are the ones to hit.
                    if (locked)
                        npc.chaseable = false;
                }
                else
                {
                    Unshield(npc);
                }

                // No arm of it left up: its change (the server's to begin).
                if (Authority && started && !changed && heldFor <= 0)
                    WatchArms(npc);

                // Legends: vanilla's hover kept, and what comes between stretches of it (one may begin here, this tick).
                if (scene == Scene.None && legends && heldFor <= 0)
                    LegendsBetween(npc);

                if (scene == Scene.None)
                {
                    Look(npc);
                    return true;
                }
            }

            bool entered = scene != playedScene || step != playedStep;
            playedScene = scene;
            playedStep = step;

            if (entered)
                seenTick = -1;

            int ran = timer;

            // In an attack of its own it can be struck; in everything else it cannot.
            if (scene == Scene.Move)
            {
                Unshield(npc);
            }
            else
            {
                npc.dontTakeDamage = true;
                npc.chaseable = false;
                shielded = true;
                StripBuffs(npc);
            }

            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
            hurts = false;
            hurtScale = 1f;

            if (timer % 30 == 0)
                npc.TargetClosest(false);

            switch (scene)
            {
                case Scene.Enter:
                    RunEnter(npc, entered);
                    break;

                case Scene.Change:
                    RunChange(npc, entered);
                    break;

                case Scene.Finale:
                    RunFinale(npc, entered);
                    break;

                case Scene.Group:
                    RunGroup(npc, entered);
                    break;

                case Scene.Move:
                    RunMove(npc, entered);
                    break;
            }

            // The step it ran (if it did not go on to another: that one begins afresh).
            if (scene == playedScene && step == playedStep)
                seenTick = Math.Max(seenTick, ran);

            Look(npc);
            timer++;
            return false;
        }

        /// <summary>Its clock passes this tick of its step now (once, on this machine; not one long past: a machine that comes to a step late does not play all of it at once).</summary>
        private bool At(int tick) => seenTick < tick && timer >= tick && timer - tick <= Late;

        /// <summary>How long past its tick a thing may still happen.</summary>
        internal const int Late = 6;

        private void Unshield(NPC npc)
        {
            if (!shielded)
                return;

            shielded = false;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
        }

        /// <summary>
        /// Nothing eats at it while it cannot be hurt (a scene; an arm struck down): what it had on it is taken off. A
        /// burn left on it would else kill it over and over, each death refused, each shown as a blow.
        /// </summary>
        internal static void StripBuffs(NPC npc)
        {
            for (int i = 0; i < NPC.maxBuffs; i++)
            {
                if (npc.buffType[i] > 0)
                {
                    npc.buffType[i] = 0;
                    npc.buffTime[i] = 0;
                }
            }
        }

        public override void PostAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
                return;

            // Its first tick was vanilla's, which makes its arms: its coming begins on the next.
            if (!started && Authority && npc.ai[0] != 0f)
            {
                started = true;
                BeginEnter(npc);
            }
        }

        /// <summary>No arm of it up for a little while (each fallen, lying, or gone): it changes. The server's.</summary>
        private void WatchArms(NPC npc)
        {
            int n = FindArms(npc);

            for (int k = 0; k < n; k++)
            {
                if (Main.npc[armList[k]].TryGetGlobalNPC(out PrimeArm arm) && arm.Up)
                {
                    armsGone = 0;
                    return;
                }
            }

            if (++armsGone >= ChangeAfter)
                BeginChange(npc);
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) =>
            NPC.IsMechQueenUp || heldFor <= 0 && (scene == Scene.None || hurts);

        // ---- its death ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Its death is refused, and its end begins (Prime.Finale): held at one life, untouchable. Its end zeroes its life
        /// itself and asks again, and that one is let through: the tick its loot drops.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (NPC.IsMechQueenUp || ending)
                return true;

            // A client's own blow: its death is the server's to begin. Held at one life here until the server says.
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (scene == Scene.None)
                {
                    heldFor = HeldTicks;
                    shielded = true;
                }

                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.active = true;
                return false;
            }

            if (scene != Scene.Finale)
                BeginFinale(npc);

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;
            return false;
        }

        /// <summary>Handed back to vanilla to die: the loot.</summary>
        private void End(NPC npc)
        {
            if (ending)
                return;

            ending = true;
            npc.DeathSound = null;
            npc.dontTakeDamage = false;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        // ---- where it is, and who it is on ---------------------------------------------------------------------------------

        /// <summary>To this place this tick, by its velocity (the game moves it after its AI).</summary>
        internal static void Move(NPC npc, Vector2 to) => npc.velocity = to - npc.Center;

        internal static Player Target(NPC npc)
        {
            Player p = Main.player[npc.target];
            return p.active && !p.dead ? p : null;
        }

        // ---- the camera ---------------------------------------------------------------------------------------------------

        internal static bool Watching(Vector2 at)
        {
            if (Main.dedServ)
                return false;

            Player me = Main.LocalPlayer;
            return me.active && !me.dead && Vector2.Distance(me.Center, at) <= SceneRange;
        }

        /// <summary>The shot, for a player in the scene: the camera <paramref name="blend"/> of the way to <paramref name="on"/>, the bars, the lean, the player held and spared.</summary>
        private static void Shot(Vector2 on, float blend, float lean)
        {
            PisPlayer.BlendCamera(on, blend);
            Cinematic.Hold();
            Cinematic.Frame();
            Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)));

            Player me = Main.LocalPlayer;
            me.immune = true;
            me.immuneNoBlink = true;
            me.immuneTime = Math.Max(me.immuneTime, 2);
        }

        // ---- how it looks -------------------------------------------------------------------------------------------------

        /// <summary>Its spikes are out: changed for good, or in vanilla's spin (its third frame), or from the moment its change lights it.</summary>
        private bool Spiked(NPC npc)
        {
            if (scene == Scene.Enter)
                return false;

            if (changed || scene == Scene.Change && step >= ChIgnite)
                return true;

            // Its arms' spin (Legends): out from the moment they click home.
            if (scene == Scene.Group)
                return act == Act.Spin && (step > SpLock || step == SpLock && timer >= SpClickAt);

            return scene == Scene.None && npc.frame.Y >= npc.frame.Height * 2;
        }

        /// <summary>Its mouth is open: vanilla's chewing between scenes; in a scene, when the scene says.</summary>
        private bool MouthOpen(NPC npc)
        {
            switch (scene)
            {
                case Scene.Enter:
                    return step == EnScreech && timer >= ScreechAt && timer < ScreechAt + ScreechLong;

                case Scene.Change:
                    return step == ChIgnite && timer >= 4 && timer < 4 + ScreechLong;

                case Scene.Finale:
                    return step > FinOpen || step == FinOpen && timer >= OpenAt;

                case Scene.Move when act == Act.Detonate:
                    // Open for what it draws in; shut, dazed.
                    return step == DeSuck;

                case Scene.Move when act == Act.Beams && step == BeCharge:
                    return timer < 66;

                case Scene.Move when act == Act.Launch && step == LaRoar:
                    return timer < 52;

                default:
                    return npc.frame.Height > 0 && npc.frame.Y / npc.frame.Height == 1;
            }
        }

        /// <summary>Its eyes: lit or dark, red or its green, and how bright their glow (1 as vanilla's).</summary>
        private void EyeLook(out bool lit, out bool green, out float glow)
        {
            lit = true;
            green = changed;
            glow = 1f;

            switch (scene)
            {
                case Scene.Enter:
                    if (step < EnOn || step == EnOn && timer < EyesAt)
                        lit = false;
                    else if (step == EnOn)
                        glow = 1f + 2f * (1f - Smooth((timer - EyesAt) / 24f));

                    break;

                case Scene.Change:
                    if (step < ChIgnite)
                    {
                        // Red, guttering, and out before its change lights it.
                        green = false;

                        if (step == ChDark)
                        {
                            float u = timer / (float)ChDarkTicks;
                            lit = timer < EyesOutAt && Chance(seed + timer / 3) > u * 0.9f;
                            glow = 0.6f + 0.8f * Chance(seed * 3 + timer / 2);
                        }
                    }
                    else
                    {
                        green = true;

                        if (step == ChIgnite)
                            glow = 1f + 2.5f * (1f - Smooth(timer / 40f));
                    }

                    break;

                case Scene.Finale:
                    if (!changed)
                    {
                        // Not changed in its fight: it goes its green as its mouth opens, guttering red till then.
                        green = step >= FinOpen;

                        if (step == FinShake)
                            lit = Chance(seed + timer / 3) > 0.45f * timer / FinShakeTicks;
                    }

                    if (step >= FinOpen)
                        glow = 1f + 1.5f * Aura;

                    break;

                case Scene.Group:
                    glow = 1f + eyeHeat;
                    break;

                case Scene.Move:
                    glow = 1f + eyeHeat;

                    // Dazed (gone up; stuck in the ground): guttering.
                    if (act == Act.Detonate && step == DeDaze && timer < DeDazeTicks - 30 || act == Act.Launch && step == LaStuck && timer < LaStuckTicks - 24)
                        lit = Chance(seed + timer / 3) > 0.42f;

                    break;
            }
        }

        // ---- drawing ------------------------------------------------------------------------------------------------------

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsMechQueenUp || npc.IsABestiaryIconDummy)
                return true;

            // Its shake, only seen: drawn off where it is, put back in PostDraw (which always follows).
            if (shake > 0.05f)
            {
                drawShift = Main.rand.NextVector2Circular(shake, shake);
                npc.position += drawShift;
            }

            DrawHead(npc, spriteBatch, screenPos, drawColor);
            return false;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!NPC.IsMechQueenUp && !npc.IsABestiaryIconDummy)
                DrawOver(npc, spriteBatch, screenPos);

            npc.position -= drawShift;
            drawShift = Vector2.Zero;
        }

        /// <summary>
        /// One look of its head out of a sheet (vanilla's, or one made from it: PrimeArt): the top of the frame its spikes
        /// ask for over the bottom of the frame its mouth asks for, from the block its eyes ask for.
        /// </summary>
        private static void DrawSheet(SpriteBatch batch, Texture2D sheet, bool dark, bool spiked, bool open, Vector2 at, Color colour, float rotation, float scale)
        {
            int block = dark ? sheet.Height / 2 : 0;
            int high = (int)FrameHigh;
            int top = block + (spiked ? 2 : 0) * high;
            int bottom = block + (open ? 1 : 0) * high;
            Vector2 pivot = new(FrameWide * 0.5f, high * 0.5f);

            batch.Draw(sheet, at, new Rectangle(0, top, FrameWide, Cut), colour, rotation, pivot, scale, SpriteEffects.None, 0f);
            batch.Draw(sheet, at, new Rectangle(0, bottom + Cut, FrameWide, high - Cut), colour, rotation, pivot - new Vector2(0f, Cut), scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Its head, drawn here and not by vanilla (its spikes, its mouth and its eyes are its scenes' to say): the faint
        /// copies of where it was that vanilla draws behind it, itself, its eyes' glow (red, or its green), and their
        /// trails.
        /// </summary>
        private void DrawHead(NPC npc, SpriteBatch batch, Vector2 screenPos, Color drawColor)
        {
            Main.instance.LoadNPC(npc.type);
            Texture2D red = TextureAssets.Npc[npc.type].Value;
            EyeLook(out bool lit, out bool green, out float glow);
            Texture2D body = green ? PrimeArt.GreenOf(red) : red;
            bool spiked = Spiked(npc), open = MouthOpen(npc);
            Vector2 middle = Middle(npc);
            Vector2 at = middle - screenPos;
            Color colour = npc.GetAlpha(npc.GetNPCColorTintedByBuffs(drawColor));

            DrawEyeTrails(npc, batch, lit, green);

            // As vanilla draws the mechanical bosses: every second one of its last ten places, fainter the older.
            for (int k = Math.Min(9, npc.oldPos.Length - 1); k >= 1; k -= 2)
            {
                Vector2 old = npc.oldPos[k] + (middle - npc.position) - screenPos;
                DrawSheet(batch, body, !lit, spiked, open, old, colour * ((10 - k) / 20f), npc.rotation, npc.scale);
            }

            DrawSheet(batch, body, !lit, spiked, open, at, colour, npc.rotation, npc.scale);

            if (lit)
            {
                Texture2D eyes = TextureAssets.BoneEyes.Value;

                if (green)
                    eyes = PrimeArt.GlowGreenOf(eyes);

                int high = (int)FrameHigh;
                Vector2 pivot = new(FrameWide * 0.5f, high * 0.5f);
                Color light = new Color(200, 200, 200, 0) * Math.Min(1f, glow);
                batch.Draw(eyes, at, new Rectangle(0, 0, FrameWide, high), light, npc.rotation, pivot, npc.scale, SpriteEffects.None, 0f);

                // Brighter than vanilla's (a flare, its aura): a soft light over each eye.
                if (glow > 1.05f)
                {
                    Color c = green ? PrimeArt.Nuclear : PrimeArt.Red;

                    for (int side = -1; side <= 1; side += 2)
                        DestroyerArt.DrawGlow(batch, Eye(npc, side), 26f + 22f * (glow - 1f), PrimeArt.Lit(c * Math.Min(0.9f, 0.4f * (glow - 1f))));
                }
            }
        }

        /// <summary>Its own picture again over it, in a colour, added on as light: a flash, a sheen.</summary>
        private void Tint(NPC npc, SpriteBatch batch, Vector2 screenPos, Color colour)
        {
            Texture2D sheet = TextureAssets.Npc[npc.type].Value;
            DrawSheet(batch, sheet, false, Spiked(npc), MouthOpen(npc), Middle(npc) - screenPos, PrimeArt.Lit(colour), npc.rotation, npc.scale);
        }

        // ---- told across the network --------------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)step);
            binaryWriter.Write((short)Math.Min(timer, short.MaxValue));
            binaryWriter.Write(started);
            binaryWriter.Write(changed);
            binaryWriter.Write(seed);
            binaryWriter.Write((short)nearest);
            binaryWriter.Write(anchor.X);
            binaryWriter.Write(anchor.Y);
            binaryWriter.Write(holdAt.X);
            binaryWriter.Write(holdAt.Y);
            SendLegends(binaryWriter);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            scene = (Scene)binaryReader.ReadByte();
            step = binaryReader.ReadByte();
            timer = binaryReader.ReadInt16();
            started = binaryReader.ReadBoolean();
            changed = binaryReader.ReadBoolean();
            seed = binaryReader.ReadInt32();
            nearest = binaryReader.ReadInt16();
            anchor = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            holdAt = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            ReceiveLegends(binaryReader);
        }
    }

    /// <summary>
    /// Skeletron Prime's coming is its own (BossIntro's camera stays out; its name comes up as its arms fire: Prime.Enter);
    /// the bones vanilla draws from each of its arms toward its head are never drawn (each arm draws its own limb:
    /// PrimeArm); and the blow that would kill it, or one of its arms, is not shown as its death while it is refused.
    /// </summary>
    public class PrimeSystem : ModSystem
    {
        /// <summary>
        /// Its name on its plate and its bar, and the line under it as the name comes up (by request: "rework name
        /// plate of each one: Skeletron Prime, Chemical Warfare Machine").
        /// </summary>
        public const string PlateName = "Skeletron Prime";
        public const string PlateTitle = "Chemical Warfare Machine";

        /// <summary>Its name's plate, with its line: brisk (up as its arms fire, read for a breath, down to the bar as the scene lets go).</summary>
        internal static readonly BossIntroProfile Plate = new()
        {
            Brisk = true,
        };

        public override void Load()
        {
            On_Main.DrawNPCDirect += NoBones;
            On_NPC.VanillaHitEffect += NoPiecesYet;
        }

        public override void Unload()
        {
            On_Main.DrawNPCDirect -= NoBones;
            On_NPC.VanillaHitEffect -= NoPiecesYet;
            PrimeArt.Unload();
        }

        public override void PostSetupContent()
        {
            BossPlate.Register(NPCID.SkeletronPrime, PlateName, PlateTitle);
            BossPlate.HasOwnIntro(NPCID.SkeletronPrime, npc => !NPC.IsMechQueenUp);
            BossPlate.NamesItself(NPCID.SkeletronPrime);

            // Its bar comes up with its name, not before: it arrives switched off.
            BossPlate.BarWaitsForName(NPCID.SkeletronPrime);
        }

        /// <summary>
        /// Main.DrawNPCDirect draws two bones from each of its arms toward its head before the arm itself (for any NPC
        /// whose AI is one of its arms': its one read of that there), each a set step along the way, so the arm seems to
        /// stretch as its hand flies off. For the call the arm's AI is said to be none, so they are not drawn; the arm
        /// draws its own limb (PrimeArm). The Mechdusa's are left alone.
        /// </summary>
        private static void NoBones(On_Main.orig_DrawNPCDirect orig, Main self, SpriteBatch mySpriteBatch, NPC rCurrentNPC, bool behindTiles, Vector2 screenPos)
        {
            if (rCurrentNPC == null || !Prime.IsArm(rCurrentNPC.type) || NPC.IsMechQueenUp || rCurrentNPC.IsABestiaryIconDummy
                || rCurrentNPC.aiStyle < 33 || rCurrentNPC.aiStyle > 36)
            {
                orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos);
                return;
            }

            int style = rCurrentNPC.aiStyle;
            rCurrentNPC.aiStyle = -1;

            try
            {
                orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos);
            }
            finally
            {
                rCurrentNPC.aiStyle = style;
            }
        }

        /// <summary>
        /// The blow that would kill it (or one of its arms) is shown by vanilla as its death (NPC.StrikeNPC runs HitEffect
        /// before checkDead): its pieces thrown. Refused (Prime.CheckDead, PrimeArm.CheckDead), it is shown as a blow it
        /// lives through.
        /// </summary>
        private static void NoPiecesYet(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            bool ours = self.type == NPCID.SkeletronPrime || Prime.IsArm(self.type);

            if (!ours || self.life > 0 || NPC.IsMechQueenUp || !Refused(self))
            {
                orig(self, hitDirection, dmg, instantKill);
                return;
            }

            int life = self.life;
            self.life = 1;

            try
            {
                orig(self, hitDirection, dmg, instantKill);
            }
            finally
            {
                self.life = life;
            }
        }

        /// <summary>Its death would be refused now: its head's, always till its end lets it through; an arm's, while its head is here.</summary>
        private static bool Refused(NPC npc)
        {
            if (npc.type == NPCID.SkeletronPrime)
                return npc.TryGetGlobalNPC(out Prime head) && !head.Ending;

            return npc.TryGetGlobalNPC(out PrimeArm arm) && !arm.Ending && PrimeArm.HeadOf(npc, out _, out _);
        }
    }
}
