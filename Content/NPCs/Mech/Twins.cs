using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The Twins, reworked (by request: "rework: the Twins"). Their elements (by the user's lore): the Retinazer fire and
    /// flame (#c2380e), the Spazmatism toxin and gas (#3ea637).
    ///
    ///   THEIR COMING (Enter)     the camera goes a little over the player, who is held. They cross the sky far off, half
    ///                            their size and hazed with it, each from its own side; come back over the screen twice
    ///                            their size, dark and fast; and drop down from above to either side over the player. Their
    ///                            name comes up. (Twins.Enter)
    ///   THEIR CHANGE             each changes as vanilla's does, then is untouchable, its first form's moves still its own,
    ///                            until the other has changed too; then both fight in their second (by request).
    ///   THE FIRST TO FALL (Fall) held at one life, untouchable, harmless, still: sparks, sooner and sooner, for three
    ///                            seconds, black smoke pouring out of it; then it falls, and hits the ground hard, and lies
    ///                            there. No camera. (Twins.Fall)
    ///   THE LAST (Finale)        the camera goes to it. It stops where it is, flashing its colour, a buzzer going, three
    ///                            seconds; turns to its twin; the camera goes there; its twin shakes, stirs, rises, and both
    ///                            turn on the player; the camera comes home. Their eyes mark the player for two seconds (the
    ///                            player free), then lock, and both fly at that spot to meet there in one second: a great
    ///                            fireball of gas and fire. (Twins.Finale, TwinsBlast)
    ///
    /// The cord vanilla draws between them is never drawn (by request: "no string attached"). Not where they are the
    /// Mechdusa's (NPC.IsMechQueenUp): vanilla's, all of it.
    ///
    /// The server (or a single player) decides every step; every machine plays it from that (SendExtraAI), counting on by
    /// itself between packets. The fallen one is moved by the one still up's scene, on every machine.
    /// </summary>
    public partial class Twins : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.Retinazer || entity.type == NPCID.Spazmatism;

        /// <summary>The Retinazer's element: fire and flame.</summary>
        public static readonly Color Fire = new(194, 56, 14);

        /// <summary>The Spazmatism's element: toxin and gas.</summary>
        public static readonly Color Toxic = new(62, 166, 55);

        private enum Scene : byte
        {
            None,
            Enter,
            Fall,
            Down,
            Finale,

            /// <summary>One of its own attacks (Legends): it can be struck, and strikes.</summary>
            Move,

            /// <summary>An attack with its twin (Legends): untouchable while it lasts.</summary>
            Duo,
        }

        private Scene scene;
        private int step;
        private int timer;

        private Scene playedScene;
        private int playedStep = -1;

        /// <summary>Its coming has been decided (or there was none to have).</summary>
        private bool started;

        private bool named;

        /// <summary>Its death is being let through: the loot.</summary>
        private bool ending;

        /// <summary>Changed, and held untouchable in its first form's moves until its twin has changed too.</summary>
        private bool waiting;

        /// <summary>Let into its second form for good.</summary>
        private bool released;

        /// <summary>A blow that would kill it, dealt on this machine (a client), the server not yet answered.</summary>
        private int heldFor;
        private const int HeldTicks = 120;

        /// <summary>Made untouchable by this machine, and so to be given back.</summary>
        private bool shielded;

        /// <summary>Its life while its first form's moves run (raised past the change's mark for vanilla's AI), to be put back.</summary>
        private int liftedLife = -1;

        /// <summary>How hard it shakes this tick, in pixels: only seen.</summary>
        internal float shake;
        private Vector2 drawShift;

        /// <summary>What its sparks and smoke are worked out from, the same on every machine.</summary>
        private int seed;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        private const float SceneRange = 5200f;

        /// <summary>
        /// Where vanilla draws its eye's middle (Main.DrawNPCDirect_Inner): its picture's pivot (55, 107) put at its hitbox's
        /// bottom, plus four, less its frame's height (200), plus the pivot's height and Main.NPCAddHeight's 30 for the Twins,
        /// all at its scale. Four pixels over its centre at its own size.
        /// </summary>
        private const float FrameHigh = 200f;
        private const float AddHigh = 30f;

        /// <summary>The point in its picture it is drawn about: its eye's middle.</summary>
        internal static readonly Vector2 Pivot = new(55f, 107f);

        internal static bool IsRet(NPC npc) => npc.type == NPCID.Retinazer;

        internal static Color ElementOf(NPC npc) => IsRet(npc) ? Fire : Toxic;

        /// <summary>Untouchable: its scenes, a held death, its wait, and its first ticks on a client.</summary>
        internal bool InScene => scene != Scene.None || heldFor > 0;

        /// <summary>Fallen, lying, or in the end: none of vanilla's AI.</summary>
        internal bool Out => scene == Scene.Fall || scene == Scene.Down || scene == Scene.Finale;

        // ---- the pair ---------------------------------------------------------------------------------------------------

        /// <summary>Its twin, if it has one (the nearest of the other kind).</summary>
        internal static bool Partner(NPC npc, out NPC other, out Twins twin)
        {
            other = null;
            twin = null;
            int want = IsRet(npc) ? NPCID.Spazmatism : NPCID.Retinazer;
            float best = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];

                if (!n.active || n.type != want || !n.TryGetGlobalNPC(out Twins t))
                    continue;

                float d = Vector2.DistanceSquared(n.Center, npc.Center);

                if (d < best)
                {
                    best = d;
                    other = n;
                    twin = t;
                }
            }

            return other != null;
        }

        /// <summary>Its twin has changed (or is past changing): in its second form, waiting, fallen, or gone.</summary>
        private static bool Changed(NPC other, Twins twin) =>
            other == null || !other.active || other.ai[0] >= 3f || twin.waiting || twin.Out;

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

        // ---- the AI -------------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
            {
                Unshield(npc);
                npc.hide = false;
                return true;
            }

            Partner(npc, out NPC other, out Twins twin);

            if (heldFor > 0)
            {
                if (scene != Scene.None)
                    heldFor = 0;
                else
                    heldFor--;
            }

            if (!started && Authority)
                BeginEnter(npc, other, twin);

            npc.hide = scene == Scene.Enter && (step == EnBack || step == EnFront);
            shake = 0f;

            // Changed, and waiting on its twin: let into its second form the moment its twin has changed.
            if (waiting)
                WaitOver(npc, other, twin);

            if (PADWorld.Legends && started)
                LegendsDuoClock(npc, other, twin);

            if (scene == Scene.None)
            {
                playedScene = Scene.None;
                playedStep = -1;
                duoSeen = -1;

                // Legends: vanilla's AI kept on its first attack, and its own attacks between (one may begin here).
                if (PADWorld.Legends && started && heldFor <= 0)
                    LegendsBetween(npc);
            }

            if (scene == Scene.None)
            {
                if (waiting)
                    Wait(npc);

                // Untouchable while held, waiting, and on a client not yet told of its coming. What this machine made
                // untouchable, this machine gives back.
                if (heldFor > 0 || waiting || !Authority && !started)
                {
                    npc.dontTakeDamage = true;
                    shielded = true;

                    // Nothing that seeks wastes itself on it while it cannot be hurt: its twin is the one to hit.
                    if (waiting)
                        npc.chaseable = false;
                }
                else
                {
                    Unshield(npc);
                }

                return true;
            }

            bool entered = scene != playedScene || step != playedStep;
            playedScene = scene;
            playedStep = step;

            // In an attack of its own it can be struck (unless it waits on its twin); in everything else it cannot.
            if (scene == Scene.Move && !waiting)
            {
                Unshield(npc);
            }
            else
            {
                npc.dontTakeDamage = true;
                npc.chaseable = false;
                shielded = true;
            }

            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

            if (timer % 30 == 0 && scene != Scene.Down)
                npc.TargetClosest(false);

            switch (scene)
            {
                case Scene.Enter:
                    RunEnter(npc, entered, other, twin);
                    break;

                case Scene.Fall:
                    RunFall(npc, entered);
                    break;

                case Scene.Down:
                    RunDown(npc, entered, other, twin);
                    break;

                case Scene.Finale:
                    RunFinale(npc, entered, other, twin);
                    break;

                case Scene.Move:
                    RunMove(npc, entered, other);
                    break;

                case Scene.Duo:
                    RunDuo(npc, other, twin);
                    break;
            }

            timer++;
            return false;
        }

        private void Unshield(NPC npc)
        {
            if (!shielded)
                return;

            shielded = false;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
        }

        public override void PostAI(NPC npc)
        {
            if (liftedLife >= 0)
            {
                npc.life = Math.Min(npc.life, liftedLife);
                liftedLife = -1;
            }

            if (NPC.IsMechQueenUp || scene != Scene.None || waiting || released || npc.ai[0] < 3f)
                return;

            // Its change is done: into its second form only if its twin has changed too; held in its first's moves if not.
            Partner(npc, out NPC other, out Twins twin);

            if (Changed(other, twin))
            {
                released = true;
                return;
            }

            waiting = true;
            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;

            if (Authority)
                npc.netUpdate = true;
        }

        /// <summary>Waiting on its twin: let go into its second form the moment its twin has changed (or fallen, or gone).</summary>
        private void WaitOver(NPC npc, NPC other, Twins twin)
        {
            if (other == null || !other.active || other.ai[0] >= 3f || twin.Out)
                Release(npc);
        }

        /// <summary>
        /// Changed, waiting on its twin: its first form's moves (vanilla's, ai[0] at 0), untouchable. Vanilla's first form
        /// changes once its life is under four tenths, so for its AI its life is read as just over that, and put back after.
        /// </summary>
        private void Wait(NPC npc)
        {
            int mark = (int)(npc.lifeMax * 0.4f) + 1;

            if (npc.life < mark)
            {
                liftedLife = npc.life;
                npc.life = mark;
            }

            WaitGlow(npc);
        }

        private void Release(NPC npc)
        {
            waiting = false;
            released = true;
            npc.ai[0] = 3f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;
            Unshield(npc);
            ReleaseFx(npc);

            if (Authority)
                npc.netUpdate = true;
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            // Waiting, it runs its first form's moves in its second form's shape.
            if (waiting && npc.frame.Y < frameHeight * 3)
                npc.frame.Y += frameHeight * 3;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) =>
            NPC.IsMechQueenUp || (scene == Scene.None || scene == Scene.Move || scene == Scene.Duo) && heldFor <= 0;

        // ---- its death ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// The first of them to die is refused, and falls (Twins.Fall); the second is refused, and ends them both (Twins.Finale).
        /// One alone (its twin never here, or gone) dies as vanilla's does.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (NPC.IsMechQueenUp || ending)
                return true;

            if (!Partner(npc, out NPC other, out Twins twin))
                return true;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (scene == Scene.None || scene == Scene.Move)
                {
                    heldFor = HeldTicks;
                    shielded = true;
                }

                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.active = true;
                return false;
            }

            if (scene == Scene.None || scene == Scene.Move)
            {
                act = Act.None;
                heat = 0f;

                if (twin.scene == Scene.Fall || twin.scene == Scene.Down)
                    BeginFinale(npc);
                else
                    BeginFall(npc);
            }

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;
            return false;
        }

        /// <summary>Handed back to vanilla to die: the loot (its twin's first, so the last of them carries it).</summary>
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

        /// <summary>What they have out, gone: lasers and flames in flight (the server's).</summary>
        private static void ClearShots()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile shot = Main.projectile[i];

                if (shot.active && shot.hostile && (shot.type == ProjectileID.EyeLaser || shot.type == ProjectileID.DeathLaser
                    || shot.type == ProjectileID.CursedFlameHostile || shot.type == ProjectileID.EyeFire || TwinsShots.IsOurs(shot.type)))
                    shot.Kill();
            }
        }

        // ---- where it is, and where it looks ------------------------------------------------------------------------------

        /// <summary>Its eye's middle, where it is drawn about.</summary>
        internal static Vector2 EyeCentre(NPC npc) =>
            new(npc.Center.X, npc.position.Y + npc.height + 4f + (Pivot.Y + AddHigh - FrameHigh) * npc.scale + npc.gfxOffY);

        /// <summary>The way it looks (rotation 0 looks straight down).</summary>
        internal static Vector2 Facing(NPC npc) => (npc.rotation + MathHelper.PiOver2).ToRotationVector2();

        /// <summary>Its pupil, or its mouth: the front of its eye.</summary>
        internal static Vector2 Front(NPC npc, float ahead = 58f) => EyeCentre(npc) + Facing(npc) * ahead * npc.scale;

        /// <summary>The rotation that looks along a direction.</summary>
        private static float LookAlong(Vector2 way) => way.ToRotation() - MathHelper.PiOver2;

        /// <summary>To this place this tick, by its velocity (the game moves it after its AI).</summary>
        private static void Move(NPC npc, Vector2 to) => npc.velocity = to - npc.Center;

        /// <summary>Its look turned toward a direction by this share of the way.</summary>
        private static void Turn(NPC npc, Vector2 toward, float share)
        {
            if (toward.LengthSquared() > 0.01f)
                npc.rotation = npc.rotation.AngleLerp(LookAlong(toward), share);
        }

        private static Player Target(NPC npc)
        {
            Player p = Main.player[npc.target];
            return p.active && !p.dead ? p : null;
        }

        // ---- the camera ---------------------------------------------------------------------------------------------------

        private static bool Watching(Vector2 at)
        {
            if (Main.dedServ)
                return false;

            Player me = Main.LocalPlayer;
            return me.active && !me.dead && Vector2.Distance(me.Center, at) <= SceneRange;
        }

        /// <summary>The shot, for a player in the scene: the camera <paramref name="blend"/> of the way to <paramref name="on"/>, the bars, the lean, the player held.</summary>
        private static void Shot(Vector2 on, float blend, float lean, bool spare = false)
        {
            PisPlayer.BlendCamera(on, blend);
            Cinematic.Hold();
            Cinematic.Frame();
            Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)));

            if (spare)
            {
                Player me = Main.LocalPlayer;
                me.immune = true;
                me.immuneNoBlink = true;
                me.immuneTime = Math.Max(me.immuneTime, 2);
            }
        }

        // ---- drawing ------------------------------------------------------------------------------------------------------

        public override void DrawBehind(NPC npc, int index)
        {
            if (!npc.hide || scene != Scene.Enter)
                return;

            // Far off behind everything, or right over everything (Twins.Enter).
            if (step == EnBack)
                Main.instance.DrawCacheNPCsMoonMoon.Add(index);
            else if (step == EnFront)
                Main.instance.DrawCacheNPCsOverPlayers.Add(index);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsMechQueenUp)
                return true;

            if (scene == Scene.Enter && (step == EnBack || step == EnFront))
            {
                DrawPass(npc, spriteBatch, screenPos);
                return false;
            }

            // Its shake, only seen: drawn off where it is, put back in PostDraw (which always follows).
            if (shake > 0.05f)
            {
                drawShift = Main.rand.NextVector2Circular(shake, shake);
                npc.position += drawShift;
            }

            DrawUnder(npc, spriteBatch, screenPos);
            return true;
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!NPC.IsMechQueenUp)
                DrawOver(npc, spriteBatch, screenPos);

            npc.position -= drawShift;
            drawShift = Vector2.Zero;
        }

        /// <summary>Its own picture again over it, in a colour, added on as light: a flash, a sheen.</summary>
        private static void Tint(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color colour)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(texture, EyeCentre(npc) - screenPos, npc.frame, DestroyerArt.Lit(colour), npc.rotation, Pivot, npc.scale, flip, 0f);
        }

        // ---- told across the network --------------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)step);
            binaryWriter.Write((short)Math.Min(timer, short.MaxValue));
            binaryWriter.Write(started);
            binaryWriter.Write(waiting);
            binaryWriter.Write(released);
            binaryWriter.Write(seed);
            binaryWriter.Write(groundY);
            binaryWriter.Write(restRot);
            binaryWriter.Write((sbyte)spin);

            foreach (Vector2 v in new[] { anchor, restAt, holdAt, lockAt })
            {
                binaryWriter.Write(v.X);
                binaryWriter.Write(v.Y);
            }

            SendLegends(binaryWriter);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            scene = (Scene)binaryReader.ReadByte();
            step = binaryReader.ReadByte();
            timer = binaryReader.ReadInt16();
            started = binaryReader.ReadBoolean();
            waiting = binaryReader.ReadBoolean();
            released = binaryReader.ReadBoolean();
            seed = binaryReader.ReadInt32();
            groundY = binaryReader.ReadSingle();
            restRot = binaryReader.ReadSingle();
            spin = binaryReader.ReadSByte();

            Vector2 Read() => new(binaryReader.ReadSingle(), binaryReader.ReadSingle());

            anchor = Read();
            restAt = Read();
            holdAt = Read();
            lockAt = Read();

            ReceiveLegends(binaryReader);
        }
    }

    /// <summary>
    /// The Twins' coming is their own (BossIntro's camera stays out; their name comes up as they drop in: Twins.Enter); the
    /// cord between them is never drawn; and the blow that would kill one is not shown as its death while it is refused.
    /// </summary>
    public class TwinsSystem : ModSystem
    {
        /// <summary>
        /// Their name on their plate and their bar, and the line under it as the name comes up (by request: "rework name
        /// plate of each one: Spazmatism and Retinazer, the Twins").
        /// </summary>
        public const string PlateName = "Spazmatism and Retinazer";
        public const string PlateTitle = "The Twins";

        /// <summary>
        /// Their name's plate, with its line: brisk (up as they hang there, read for a breath, down to the bar as the
        /// scene lets go, the plate's lean let go over the flight).
        /// </summary>
        internal static readonly BossIntroProfile Plate = new()
        {
            Brisk = true,
        };

        public override void Load()
        {
            IL_Main.DrawNPCs += NoCord;
            On_NPC.VanillaHitEffect += NoPiecesYet;
        }

        public override void Unload()
        {
            IL_Main.DrawNPCs -= NoCord;
            On_NPC.VanillaHitEffect -= NoPiecesYet;
            Twins.GasTrail.Clear();
            TwinsArt.Unload();
        }

        public override void ClearWorld() => Twins.GasTrail.Clear();

        public override void PostSetupContent()
        {
            BossPlate.Register(NPCID.Retinazer, PlateName, PlateTitle);
            BossPlate.Register(NPCID.Spazmatism, PlateName, PlateTitle);
            BossPlate.HasOwnIntro(NPCID.Retinazer, npc => !NPC.IsMechQueenUp);
            BossPlate.HasOwnIntro(NPCID.Spazmatism, npc => !NPC.IsMechQueenUp);
            BossPlate.NamesItself(NPCID.Retinazer);
            BossPlate.NamesItself(NPCID.Spazmatism);
        }

        /// <summary>
        /// Main.DrawNPCs draws the cord between the Twins when the Mechdusa is not up (its one read of NPC.IsMechQueenUp):
        /// that read answers yes, so it never is (by request: "no string attached"). The Mechdusa's own cord is drawn
        /// elsewhere (Main.DrawNPCDirect_Inner) and is left alone.
        /// </summary>
        private void NoCord(ILContext il)
        {
            ILCursor cursor = new(il);

            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall(typeof(NPC), "get_IsMechQueenUp")))
            {
                Mod.Logger.Warn("The Twins: Main.DrawNPCs no longer reads NPC.IsMechQueenUp; the cord between them will be drawn.");
                return;
            }

            cursor.EmitDelegate<Func<bool, bool>>(_ => true);
        }

        /// <summary>
        /// The blow that would kill one of them is shown by vanilla as its death (NPC.StrikeNPC runs HitEffect before
        /// checkDead): its pieces thrown. Refused (Twins.CheckDead), it is shown as a blow it lives through.
        /// </summary>
        private static void NoPiecesYet(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            bool twin = self.type == NPCID.Retinazer || self.type == NPCID.Spazmatism;

            if (!twin || self.life > 0 || NPC.IsMechQueenUp || !self.TryGetGlobalNPC(out Twins rework) || rework.Ending || !Twins.Partner(self, out _, out _))
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
    }
}
