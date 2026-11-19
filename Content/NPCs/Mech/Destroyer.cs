using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// The Destroyer, reworked (by request: "Destroyer rework", "Make it beautiful"). Its element is lightning and energy
    /// (by the user's lore: each mechanical boss an element; its colour #7fd4d1).
    ///
    /// IN EVERY WORLD
    ///   ITS RISING (Rise)      it is put deep under the ground ninety-five tiles off to one side of the player, its body
    ///                          laid out behind it. The camera goes there, the bars come in, the player is held; the
    ///                          ground shakes, louder and louder; it bursts up out of it and slows; turns its head on the
    ///                          player; roars (MechRoar; its name comes up); dives back into the ground, and the camera
    ///                          walks home.
    ///   ITS LAST STAND (Stand) the blow that would kill it refuses: one life, untouchable. For ten seconds it dives under
    ///                          the player and leaps over them, side to side, its whole length deadly to touch.
    ///   ITS END (Finale)       the camera goes to it racing round under the ground; it bursts up at the player and stops
    ///                          short; explosions along it, each sooner after the last, black smoke out of its creases, the
    ///                          Megashark's sparks; a buzzer; and it blows up a segment at a time from its tail to its head.
    ///
    /// IN LEGENDS (Destroyer.Legends, Destroyer.Moves)
    ///   a third again the life, a seventh again the damage; vanilla's chase between moves of its own, its own warned
    ///   bolts in place of vanilla's lasers, its Probes reworked (LegendsProbe); its second wind at 35% (Wake): its eyes
    ///   turn to its colour, from its tail to its head, and steam comes out of every crease.
    ///
    /// While the head is driven by any of this (a scene, a move), its body is laid along the very way its head has come
    /// (Destroyer.Path), all of it, rather than dragged by vanilla's pull. Both cinematics can be skipped (E held in the
    /// corner, a world of one). Not where it is the Mechdusa's body (NPC.IsMechQueenUp): vanilla's, all of it.
    ///
    /// The server (or a single player) decides every step and where; every machine plays it from that (SendExtraAI) and
    /// counts its clock on by itself between packets. Everything seen or heard is played on every machine.
    /// </summary>
    public partial class Destroyer : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.TheDestroyer;

        private enum Scene : byte
        {
            None,
            Rise,
            Stand,
            Finale,

            /// <summary>Its second wind (Legends): untouchable, harmless.</summary>
            Wake,

            /// <summary>One of its own moves (Legends): it can be struck, and strikes.</summary>
            Move,
        }

        private Scene scene;
        private int step;
        private int timer;

        /// <summary>The scene and step this machine played last tick: a new one is entered when they change.</summary>
        private Scene playedScene;
        private int playedStep = -1;

        /// <summary>Its rising has been decided (or there was none to have).</summary>
        private bool started;

        /// <summary>Its name has come up.</summary>
        private bool named;

        /// <summary>Its death is being let through: the loot.</summary>
        private bool ending;

        /// <summary>Its head has blown up; it is not drawn.</summary>
        private bool headGone;

        /// <summary>A blow that would kill it, dealt on this machine (a client), and the server not yet answered: held for this long.</summary>
        private int heldFor;
        private const int HeldTicks = 120;

        /// <summary>Made untouchable by this machine (a scene, a held death, its first ticks on a client), and so to be given back.</summary>
        private bool shielded;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>How far from a scene a player is still in it (its camera, its holding).</summary>
        private const float SceneRange = 5200f;

        /// <summary>In a cinematic: untouchable.</summary>
        internal bool InScene => scene == Scene.Rise || scene == Scene.Stand || scene == Scene.Finale || scene == Scene.Wake || heldFor > 0;

        /// <summary>Its head is driven by a scene or a move, not by vanilla: its body is laid along its way (Destroyer.Path).</summary>
        internal bool Drives => scene != Scene.None;

        /// <summary>Touches nobody: its rising, its end, its second wind.</summary>
        internal bool Harmless => scene == Scene.Rise || scene == Scene.Finale || scene == Scene.Wake;

        /// <summary>Its body in order, from the segment after its head to its tail (refreshed every tick it is reworked).</summary>
        private readonly List<int> chain = new();

        // ---- Legends ------------------------------------------------------------------------------------------------------

        public const float LifeScale = 1.3f;
        public const float DamageScale = 1.15f;

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LifeScale);
            entity.damage = (int)(entity.damage * DamageScale);
        }

        // ---- the clock -----------------------------------------------------------------------------------------------

        private void Go(NPC npc, Scene to, int at)
        {
            scene = to;
            step = at;
            timer = 0;

            if (Authority)
                npc.netUpdate = true;
        }

        private void Next(NPC npc, int at) => Go(npc, scene, at);

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ---- the AI ----------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
                return true;

            Chain(npc);
            Listen(npc);
            soundsThisTick = 0;
            Fade();

            // A death this machine dealt and the server has not answered: lapses if it never does.
            if (heldFor > 0)
            {
                if (scene != Scene.None)
                    heldFor = 0;
                else
                    heldFor--;
            }

            if (scene == Scene.None)
            {
                playedScene = Scene.None;
                playedStep = -1;
                trailLive = false;

                // Untouchable while held, and on a client not yet told of its rising (its first ticks there: not to be struck
                // or named before it). What this machine made untouchable, this machine gives back.
                if (heldFor > 0 || !Authority && !started)
                {
                    npc.dontTakeDamage = true;
                    shielded = true;
                }
                else if (shielded)
                {
                    npc.dontTakeDamage = false;
                    npc.chaseable = true;
                    shielded = false;
                }

                // Legends: its second wind, its moves, its bolts between them (a move may begin here, this tick).
                if (PADWorld.Legends && started && heldFor <= 0)
                    LegendsBetween(npc);

                if (scene == Scene.None)
                {
                    LegendsLook(npc);
                    return true;
                }
            }

            bool entered = scene != playedScene || step != playedStep;
            playedScene = scene;
            playedStep = step;

            if (InScene)
            {
                npc.dontTakeDamage = true;
                shielded = true;
                npc.chaseable = scene == Scene.Stand;
            }
            else
            {
                if (shielded)
                {
                    npc.dontTakeDamage = false;
                    shielded = false;
                }

                npc.chaseable = true;
            }

            npc.alpha = 0;
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);

            if (timer % 30 == 0)
                npc.TargetClosest(false);

            switch (scene)
            {
                case Scene.Rise:
                    RunRise(npc, entered);
                    break;

                case Scene.Stand:
                    RunStand(npc, entered);
                    break;

                case Scene.Finale:
                    RunFinale(npc, entered);
                    break;

                case Scene.Wake:
                    RunWake(npc, entered);
                    break;

                case Scene.Move:
                    RunMove(npc, entered);
                    break;
            }

            LegendsLook(npc);

            if (scene != Scene.None)
                LayBody(npc);

            timer++;
            return false;
        }

        public override void PostAI(NPC npc)
        {
            if (NPC.IsMechQueenUp)
                return;

            // Its first tick was vanilla's, which builds its body: its rising begins on the next.
            if (!started && Authority && npc.ai[0] != 0f)
            {
                started = true;
                BeginRise(npc);
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !Harmless && !headGone;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (headGone)
                return false;

            // Its shake, seen only: drawn off where it is, put back in PostDraw (which always follows).
            if (shake > 0.05f)
            {
                drawShift = Main.rand.NextVector2Circular(shake, shake);
                npc.position += drawShift;
            }

            // Its eye in its colour (its second wind on), or its body lit (a storm's glow): drawn here.
            return !DrawHead(npc, spriteBatch, screenPos, drawColor);
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            npc.position -= drawShift;
            drawShift = Vector2.Zero;
        }

        /// <summary>
        /// Refuses its first death and hands it to its last stand: life pinned at one, untouchable. Its end zeroes its life
        /// itself and calls checkDead again, and that one is let through: the tick its loot drops. A blow anywhere on its
        /// body comes here (its body's life is its head's: NPC.StrikeNPC, realLife).
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (NPC.IsMechQueenUp || ending)
                return true;

            Restore(npc);
            StopBeam();

            // A client's own blow: its death is the server's to begin. Held at one life here until the server says.
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

            if (scene != Scene.Stand && scene != Scene.Finale)
                BeginStand(npc);

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;
            return false;
        }

        /// <summary>
        /// The segment the blow landed on took its head's life (NPC.StrikeNPC, realLife), and a segment at no life is gone the
        /// next tick (NPC.UpdateNPC), every segment behind it with it (their AI: the one ahead at no life). Given back the one.
        /// </summary>
        private static void Restore(NPC npc)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC part = Main.npc[i];

                if (part.active && (part.type == NPCID.TheDestroyerBody || part.type == NPCID.TheDestroyerTail) && (int)part.ai[3] == npc.whoAmI && part.life <= 0)
                    part.life = 1;
            }
        }

        /// <summary>Handed back to vanilla to die: its loot. Its sound was the scene's; every client is told it is gone.</summary>
        private void End(NPC npc)
        {
            ending = true;
            npc.DeathSound = null;
            npc.dontTakeDamage = false;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        // ---- its body ---------------------------------------------------------------------------------------------------

        /// <summary>Its body in order (DestroyerPart is told where in it each segment is, and how long it is).</summary>
        private void Chain(NPC npc)
        {
            chain.Clear();
            int at = (int)npc.ai[0];

            for (int guard = 0; guard < 300 && at > 0 && at < Main.maxNPCs; guard++)
            {
                NPC part = Main.npc[at];

                if (!part.active || part.type != NPCID.TheDestroyerBody && part.type != NPCID.TheDestroyerTail || (int)part.ai[3] != npc.whoAmI)
                    break;

                chain.Add(at);

                if (part.type == NPCID.TheDestroyerTail)
                    break;

                at = (int)part.ai[0];
            }

            for (int i = 0; i < chain.Count; i++)
            {
                if (Main.npc[chain[i]].TryGetGlobalNPC(out DestroyerPart part))
                {
                    part.Index = i;
                    part.Count = chain.Count;
                }
            }
        }

        /// <summary>Its body is laid out deep under the ground, behind where it will rise (its rising's first step).</summary>
        internal bool Laying => scene == Scene.Rise && step == RiseGather;

        /// <summary>Where a segment lies while it is: in a line from its head, away from the player and a little down.</summary>
        internal Vector2 LayAt(int index, float scale)
        {
            float away = look.X >= emerge.X ? -1f : 1f;
            return start + new Vector2(away, 0.45f).SafeNormalize(Vector2.UnitY) * (index + 1) * (int)(44f * scale);
        }

        /// <summary>The head of a segment's Destroyer, and its rework, if it has one.</summary>
        internal static bool HeadOf(NPC part, out NPC head, out Destroyer destroyer)
        {
            head = null;
            destroyer = null;
            int at = (int)part.ai[3];

            if (at < 0 || at >= Main.maxNPCs)
                return false;

            head = Main.npc[at];

            if (head.active && head.type == NPCID.TheDestroyer && head.TryGetGlobalNPC(out destroyer))
                return true;

            head = null;
            return false;
        }

        /// <summary>Where a crease is: between segment <paramref name="i"/> of its chain (or its head, at -1) and the one after it.</summary>
        private Vector2 Crease(NPC npc, int i, out Vector2 along)
        {
            Vector2 a = i < 0 ? npc.Center : Main.npc[chain[i]].Center;
            Vector2 b = Main.npc[chain[i + 1]].Center;
            along = (a - b).SafeNormalize(Vector2.UnitY);
            return (a + b) * 0.5f;
        }

        // ---- where it is, and where it faces --------------------------------------------------------------------------

        /// <summary>
        /// Its head to this place this tick, by its velocity (the game moves it after its AI), facing where it goes, or
        /// <paramref name="face"/> if given (a direction).
        /// </summary>
        private static void Move(NPC npc, Vector2 to, Vector2? face = null)
        {
            npc.velocity = to - npc.Center;
            Vector2 way = face ?? npc.velocity;

            if (way.LengthSquared() > 0.25f)
                npc.rotation = way.ToRotation() + MathHelper.PiOver2;
        }

        /// <summary>Its head turned toward a direction by this share of the way.</summary>
        private static void Turn(NPC npc, Vector2 toward, float share)
        {
            if (toward.LengthSquared() > 0.01f)
                npc.rotation = npc.rotation.AngleLerp(toward.ToRotation() + MathHelper.PiOver2, share);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float u)
        {
            float v = 1f - u;
            return a * v * v + b * 2f * u * v + c * u * u;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
        {
            float v = 1f - u;
            return a * v * v * v + b * 3f * u * v * v + c * 3f * u * u * v + d * u * u * u;
        }

        private Player Target(NPC npc)
        {
            Player p = Main.player[npc.target];
            return p.active && !p.dead ? p : null;
        }

        // ---- the camera -------------------------------------------------------------------------------------------------

        /// <summary>This machine's player is near enough to <paramref name="at"/> to be in the scene.</summary>
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

            // Held through its end, nothing it does touches them.
            if (spare)
            {
                Player me = Main.LocalPlayer;
                me.immune = true;
                me.immuneNoBlink = true;
                me.immuneTime = Math.Max(me.immuneTime, 2);
            }
        }

        // ---- told across the network --------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)scene);
            binaryWriter.Write((byte)step);
            binaryWriter.Write((short)timer);
            binaryWriter.Write(started);
            binaryWriter.Write(seed);
            binaryWriter.Write(fast);
            binaryWriter.Write((short)standClock);
            binaryWriter.Write((byte)leap);
            binaryWriter.Write((sbyte)leapSide);
            binaryWriter.Write((short)curveTicks);

            foreach (Vector2 v in new[] { emerge, start, apex, look, dive, curveA, curveB, curveC, curveD })
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
            seed = binaryReader.ReadInt32();
            fast = binaryReader.ReadBoolean();
            standClock = binaryReader.ReadInt16();
            leap = binaryReader.ReadByte();
            leapSide = binaryReader.ReadSByte();
            curveTicks = binaryReader.ReadInt16();

            Vector2 Read() => new(binaryReader.ReadSingle(), binaryReader.ReadSingle());

            emerge = Read();
            start = Read();
            apex = Read();
            look = Read();
            dive = Read();
            curveA = Read();
            curveB = Read();
            curveC = Read();
            curveD = Read();

            ReceiveLegends(binaryReader);
        }
    }

    /// <summary>
    /// Its coming is its own (BossIntro's camera stays out, and its name comes up as it roars: Destroyer.Rise); its body's
    /// pieces are not shown when the blow that would kill it is refused, nor when its end has already blown them up.
    /// </summary>
    public class DestroyerSystem : ModSystem
    {
        /// <summary>
        /// Its name on its plate and its bar, and the line under it as the name comes up (by request: "rework name plate
        /// of each one: L.O.K.I, Destroyer").
        /// </summary>
        public const string PlateName = "L.O.K.I";
        public const string PlateTitle = "Destroyer";

        /// <summary>
        /// Its name's plate, with its line: brisk (up as it roars, read for a breath, down to the bar while it still roars,
        /// the plate's lean let go over the flight), so the scene is no longer for having a line to read.
        /// </summary>
        internal static readonly BossIntroProfile Plate = new()
        {
            Brisk = true,
        };

        public override void Load()
        {
            On_NPC.VanillaHitEffect += NoPiecesYet;
        }

        public override void Unload()
        {
            On_NPC.VanillaHitEffect -= NoPiecesYet;
            DestroyerArt.Unload();
        }

        public override void PostSetupContent()
        {
            BossPlate.Register(NPCID.TheDestroyer, PlateName, PlateTitle);
            BossPlate.HasOwnIntro(NPCID.TheDestroyer, npc => !NPC.IsMechQueenUp);
            BossPlate.NamesItself(NPCID.TheDestroyer);
        }

        /// <summary>
        /// The blow that would kill it is shown by vanilla as its death (NPC.StrikeNPC runs HitEffect before checkDead): its
        /// segment thrown into pieces. Its death is refused (Destroyer.CheckDead), so while it is, the blow is shown as one it
        /// lives through. At its end, what has blown up already (DestroyerPart.Exploded) and its head show nothing more.
        /// </summary>
        private static void NoPiecesYet(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            bool destroyer = self.type == NPCID.TheDestroyer || self.type == NPCID.TheDestroyerBody || self.type == NPCID.TheDestroyerTail;

            if (!destroyer || self.life > 0 || NPC.IsMechQueenUp)
            {
                orig(self, hitDirection, dmg, instantKill);
                return;
            }

            NPC head = self.type == NPCID.TheDestroyer ? self : null;
            Destroyer rework = null;

            if (head == null)
                Destroyer.HeadOf(self, out head, out rework);
            else
                head.TryGetGlobalNPC(out rework);

            bool gone = self.type == NPCID.TheDestroyer
                ? rework != null && rework.Ended
                : self.TryGetGlobalNPC(out DestroyerPart part) && part.Exploded || head == null || rework != null && rework.Ended;

            if (gone)
                return;

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
