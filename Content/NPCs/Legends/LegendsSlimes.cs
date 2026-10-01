using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// One world scan a tick, shared by every slime alive.
    ///
    /// The King's presence provokes the whole species at once, and every passive slime
    /// needs to know. Asking NPC.AnyNPCs per slime per tick would be two hundred array
    /// walks a frame for one boolean that is the same for all of them.
    /// </summary>
    public class SlimeCourt : ModSystem
    {
        public static bool KingIsHere;

        public override void PostUpdateNPCs()
        {
            KingIsHere = NPC.AnyNPCs(NPCID.KingSlime);
        }

        public override void ClearWorld()
        {
            KingIsHere = false;
        }
    }

    /// <summary>
    /// Slimes stop being enemies once the orb is broken.
    ///
    /// Their AI is left completely alone — they still hop about exactly as they always
    /// did, and vanilla's is better at being a slime than anything written here would be.
    /// The only thing that changes is that an untouched slime is worth no damage, so it
    /// can be walked through, and town NPCs have no reason to swing at it.
    ///
    /// A slime becomes dangerous the moment it has been hurt, which is read straight off
    /// its own health rather than tracked with a flag. There is nothing to sync, nothing
    /// to get out of step, and no way for the state to disagree with what the player can
    /// see on the slime.
    /// </summary>
    public class LegendsSlime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// lateInstantiation is required here: aiStyle is written by the NPC's own
        /// SetDefaults, and the early pass runs before that, where every slime would still
        /// read as aiStyle 0 and be skipped.
        /// </summary>
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return lateInstantiation
                && entity.aiStyle == NPCAIStyleID.Slime
                && entity.type != NPCID.KingSlime;
        }

        // ---- pacifism -------------------------------------------------------

        /// <summary>
        /// Contact damage as the slime was built with.
        ///
        /// Captured on the first update rather than in SetDefaults, because difficulty
        /// scaling runs in between and the pre-scaling number is not the one to restore.
        /// </summary>
        private int baseDamage = -1;

        /// <summary>
        /// Whether this slime is willing to hurt anybody.
        ///
        /// Any wound at all is enough, and the King's arrival wakes the whole species
        /// regardless. Heralds are exempt for the length of the ritual.
        /// </summary>
        private bool Roused(NPC npc)
        {
            return !Herald && (npc.life < npc.lifeMax || SlimeCourt.KingIsHere);
        }

        // ---- herald ---------------------------------------------------------
        //
        // Called up by the King's arrival (LegendsKingSlime, the rite). Harmless and
        // immortal, and going one way: to the altar, one hop at a time over the real
        // ground (SlimeRite), up into its place in the pile there, and, when the pile
        // goes, into him.
        //
        // Every machine works the hops out for itself from the same numbers, so the
        // crowd moves the same everywhere with only the fields below sent. The server
        // decides the two things that need one decider: which place each one gets, and
        // when the rite moves on.

        /// <summary>Called up by the King. Everything below means nothing without it.</summary>
        public bool Herald;

        /// <summary>The King it came for, by index.</summary>
        public short King = -1;

        /// <summary>Its own dice (SlimeRite.Roll): its size, its stride, every hop and every rest.</summary>
        public int Seed;

        /// <summary>Ticks it waits before it sets off.</summary>
        public short Delay;

        /// <summary>Its place in the pile (SlimeRite.Place), or -1 before it has been given one.</summary>
        public sbyte Slot = -1;

        /// <summary>In its place.</summary>
        public bool Seated;

        // Its hopping, worked out on every machine alike.
        private bool grounded;
        private float landY = float.NaN;
        private int hops;
        private int rested;
        private int restFor;
        private int goAt;
        private bool sized;

        // Its shape, for the draw: squash and stretch off how it is moving, and in the
        // Fuse, where it is on its way into the middle.
        private int sinceLanding = 60;
        private float landedHard;
        private Vector2 squash = Vector2.One;
        private float lift;
        private float turn;
        private bool fromMiddle;
        private Vector2 middle;
        private float fade = 1f;

        /// <summary>Near enough the altar to be given its place and jump for it.</summary>
        private const float ClaimRange = 230f;

        /// <summary>
        /// Where the hops aim: a ring round the pile, this far out from its middle, so
        /// each one lands beside the pile and jumps into it rather than landing in it.
        /// </summary>
        private const float WaitNear = 150f;
        private const float WaitFar = 200f;

        /// <summary>Ticks a landing stays in its shape, and ticks of crouch before a jump.</summary>
        private const int LandTicks = 9;
        private const int CrouchTicks = 5;

        /// <summary>The breath it takes on the ground near the altar before the server throws it in.</summary>
        private const int ClaimBreath = 8;

        /// <summary>
        /// The last tick a landing was heard. Twenty slimes each making a sound on every
        /// landing is a hailstorm; one in five ticks is a crowd.
        /// </summary>
        private static uint lastPlop;

        /// <summary>How far its hops reach, and how high they go, against the crowd's.</summary>
        private float Stride => SlimeRite.Roll(Seed, 901, 0.85f, 1.2f);
        private float Spring => SlimeRite.Roll(Seed, 902, 0.85f, 1.2f);

        /// <summary>The server, or a single player: whoever decides.</summary>
        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        // ---- ai -------------------------------------------------------------

        /// <summary>
        /// Vanilla's AI is left running for every slime that is not a herald. The old
        /// version replaced it with a hand-written wander, which is what made passive
        /// slimes feel wrong — they stopped moving like slimes.
        /// </summary>
        public override bool PreAI(NPC npc)
        {
            // Heralds are the King's arrival, and the arrival is not a Legends thing.
            if (Herald)
            {
                HeraldAI(npc);
                return false;
            }

            if (!PADWorld.Legends)
                return true;

            if (baseDamage < 0)
                baseDamage = npc.damage;

            // Zeroing the damage is what takes a passive slime off the town NPCs' target
            // list as well: they pick from things that can actually hurt somebody, so a
            // slime worth no damage is not worth swinging at.
            npc.damage = Roused(npc) ? baseDamage : 0;

            return true;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (Herald)
                return false;

            return !PADWorld.Legends || Roused(npc);
        }

        /// <summary>A herald is not despawned for being far from everyone: it goes when the King lets it (HeraldAI).</summary>
        public override bool CheckActive(NPC npc) => !Herald;

        /// <summary>Made a herald of this King. The server's call, at the spawn.</summary>
        internal void Summon(NPC npc, int king, int seed, int delay)
        {
            Herald = true;
            King = (short)king;
            Seed = seed;
            Delay = (short)Math.Clamp(delay, 0, short.MaxValue);
            Slot = -1;
            Seated = false;

            npc.netUpdate = true;
        }

        private void HeraldAI(NPC npc)
        {
            npc.damage = 0;
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.noTileCollide = true;

            // UpdateNPC adds gravity of its own after the AI has run, and the hops add
            // their own here, the measured amount (SlimeRite.Gravity). Both would land
            // every hop short of where it was aimed.
            npc.noGravity = true;
            npc.aiAction = 0;

            if (!sized)
            {
                // Slimes come in sizes. Off the seed, so every machine agrees on it.
                sized = true;
                npc.scale = SlimeRite.Roll(Seed, 900, 0.8f, 1.25f);
            }

            LegendsKingSlime king = LegendsKingSlime.Of(King);

            if (king == null || king.Phase == KingRite.None || king.Phase == KingRite.Done)
            {
                // No King, or none in the middle of arriving. The server lets it go back
                // to being a slime; a client that has not heard of him yet just waits.
                npc.velocity = Vector2.Zero;

                if (Authority)
                    Dismiss(npc);

                return;
            }

            npc.timeLeft = NPC.activeTime;

            KingRite phase = king.Phase;

            if (phase >= KingRite.Burst)
            {
                // It is in him now. The server took it away as he came out; a client
                // that has not heard yet keeps it out of sight until it does.
                npc.velocity = Vector2.Zero;
                fade = 0f;

                if (Authority)
                    Vanish(npc);

                return;
            }

            fade = 1f;
            lift = 0f;
            turn = 0f;
            fromMiddle = false;

            if (phase == KingRite.Stack || phase == KingRite.Fuse)
            {
                if (Slot < 0)
                {
                    // Nowhere in the pile for it. Should not happen: every one still out
                    // was given a place when the camera came (LegendsKingSlime.CallIn).
                    fade = 0f;

                    if (Authority)
                        Vanish(npc);

                    return;
                }

                // Anyone not down yet is simply there. The pile does not wait.
                if (!Seated)
                    SitDown(npc, king.Altar, false);

                Heap(npc, king);
                return;
            }

            Travel(npc, king);
            Shape(npc);
        }

        /// <summary>The Gather and the Approach: over the land to the altar, then up into its place.</summary>
        private void Travel(NPC npc, LegendsKingSlime king)
        {
            Vector2 altar = king.Altar;

            if (Seated)
            {
                Sit(npc, altar);
                return;
            }

            if (Slot >= 0)
            {
                FlyHome(npc, altar);
                return;
            }

            if (!grounded)
            {
                Fall(npc, altar);
                return;
            }

            npc.velocity = Vector2.Zero;
            sinceLanding++;

            if (Delay > 0)
            {
                Delay--;
                goAt = int.MaxValue;
                return;
            }

            rested++;

            if (Math.Abs(altar.X - npc.Center.X) <= ClaimRange)
            {
                // Close. The server gives it a place after a breath and throws it in; a
                // client crouches and waits to be told where.
                goAt = Math.Min(restFor, ClaimBreath);

                if (Authority && rested >= goAt)
                    Claim(npc, king);

                return;
            }

            goAt = restFor;

            if (rested >= restFor)
                Hop(npc, altar);
        }

        /// <summary>
        /// One hop toward the altar. Aimed at real ground, a stride or what is left of the
        /// way, whichever is less, and high enough to clear everything in between with a
        /// slime's own bounce on top: SlimeRite.Launch gives the throw that lands there.
        /// </summary>
        private void Hop(NPC npc, Vector2 altar)
        {
            int n = hops++;

            float x0 = npc.Center.X;
            float y0 = npc.Bottom.Y;

            // To the ring round the pile on this one's own side of it, a different spot
            // on it every hop, so they arrive as a crowd and not in a queue.
            float from = x0 < altar.X ? -1f : 1f;
            float goal = altar.X + from * SlimeRite.Roll(Seed, n * 5, WaitNear, WaitFar);
            float toGo = goal - x0;

            // The far ones hurry.
            float hurry = MathHelper.Clamp(Math.Abs(toGo) / 700f, 1f, 1.6f);
            float reach = Math.Min(Math.Abs(toGo), SlimeRite.Roll(Seed, n * 5 + 1, 110f, 190f) * Stride * hurry);

            float x1 = x0 + Math.Sign(toGo) * reach;
            float y1 = SlimeRite.Ground(x1, y0, out float ground) ? ground : y0;

            float height = (SlimeRite.Roll(Seed, n * 5 + 3, 30f, 50f) + reach * 0.18f) * Spring;
            float top = Math.Min(SlimeRite.Highest(x0, x1, y0), Math.Min(y0, y1)) - height;

            npc.velocity = SlimeRite.Launch(new Vector2(x0, y0), new Vector2(x1, y1), top, out _);

            grounded = false;
            landY = y1;

            if (Main.netMode == NetmodeID.Server)
                npc.netUpdate = true;
        }

        /// <summary>
        /// Coming down. A hop knows where it lands; one met in the air (out of the sky, or
        /// one this machine has only just heard of) looks straight down, near the altar's
        /// height.
        /// </summary>
        private void Fall(NPC npc, Vector2 altar)
        {
            npc.velocity.Y = Math.Min(npc.velocity.Y + SlimeRite.Gravity, SlimeRite.MaxFall);

            if (float.IsNaN(landY))
                landY = SlimeRite.Ground(npc.Center.X, altar.Y, out float ground) ? ground : altar.Y;

            if (npc.velocity.Y > 0f && npc.Bottom.Y + npc.velocity.Y >= landY)
                Land(npc, landY);
        }

        private void Land(NPC npc, float ground)
        {
            float impact = npc.velocity.Y;

            npc.position.Y = ground - npc.height;
            npc.velocity = Vector2.Zero;

            grounded = true;
            landY = float.NaN;
            rested = 0;
            restFor = (int)SlimeRite.Roll(Seed, hops * 5 + 2, 6f, 16f);
            sinceLanding = 0;
            landedHard = MathHelper.Clamp((impact - 3f) / 9f, 0f, 1f);

            if (impact > 1f)
                Splat(npc, landedHard);
        }

        /// <summary>The server gives it the next place in the pile, and throws it there.</summary>
        private void Claim(NPC npc, LegendsKingSlime king)
        {
            int slot = king.TakeSlot();

            if (slot < 0)
            {
                // More of them than places. This one goes home.
                Dismiss(npc);
                return;
            }

            Slot = (sbyte)slot;
            Throw(npc, king.Altar, 26f + 0.08f * Math.Abs(king.Altar.X - npc.Center.X));
        }

        /// <summary>
        /// Up and into its place in the pile, from wherever it is, over the top of the
        /// pile by <paramref name="over"/>. The server's call, and sent; every machine
        /// flies it the same way from there (FlyHome).
        /// </summary>
        internal void Throw(NPC npc, Vector2 altar, float over)
        {
            Vector2 from = npc.Bottom;
            Vector2 to = SlimeRite.Place(altar, Slot);
            float top = Math.Min(Math.Min(from.Y, to.Y), SlimeRite.Top(altar) - 8f) - over;

            npc.velocity = SlimeRite.Launch(from, to, top, out _);

            grounded = false;
            Delay = 0;
            npc.netUpdate = true;
        }

        /// <summary>On its way into its place. Down onto it the moment it gets there.</summary>
        private void FlyHome(NPC npc, Vector2 altar)
        {
            grounded = false;
            npc.velocity.Y = Math.Min(npc.velocity.Y + SlimeRite.Gravity, SlimeRite.MaxFall);

            if (npc.velocity.Y > 0f && npc.Bottom.Y + npc.velocity.Y >= SlimeRite.Place(altar, Slot).Y)
                SitDown(npc, altar, true);
        }

        /// <summary>Into its place, with a plop when it got there by jumping.</summary>
        private void SitDown(NPC npc, Vector2 altar, bool jumped)
        {
            Vector2 place = SlimeRite.Place(altar, Slot);
            float impact = npc.velocity.Y;

            npc.position = new Vector2(place.X - npc.width * 0.5f, place.Y - npc.height);
            npc.velocity = Vector2.Zero;

            Seated = true;
            grounded = true;
            landY = float.NaN;
            sinceLanding = 0;
            landedHard = jumped ? MathHelper.Clamp((impact - 2f) / 8f, 0.35f, 1f) : 0f;

            if (jumped)
                Splat(npc, landedHard);

            if (Main.netMode == NetmodeID.Server)
                npc.netUpdate = true;
        }

        private void Sit(NPC npc, Vector2 altar)
        {
            Vector2 place = SlimeRite.Place(altar, Slot);

            npc.position = new Vector2(place.X - npc.width * 0.5f, place.Y - npc.height);
            npc.velocity = Vector2.Zero;
            sinceLanding++;
        }

        /// <summary>
        /// Squash and stretch, off how it is moving: drawn out in the air, flattened by a
        /// landing and flatter for a hard one, and crouched in the moment before it goes.
        /// Sat in the pile, it breathes.
        /// </summary>
        private void Shape(NPC npc)
        {
            if (!grounded)
            {
                float s = Math.Min(Math.Abs(npc.velocity.Y) / SlimeRite.MaxFall * 0.3f, 0.22f);

                squash = new Vector2(1f - s * 0.6f, 1f + s);
                turn = MathHelper.Clamp(npc.velocity.X * 0.02f, -0.25f, 0.25f);
                return;
            }

            float q = 0f;

            if (sinceLanding < LandTicks)
            {
                float left = 1f - sinceLanding / (float)LandTicks;
                q = (0.15f + 0.25f * landedHard) * left * left;
            }

            // Crouched for the jump, and held there by a client still waiting to be told.
            if (!Seated && Slot < 0 && Delay <= 0)
                q = Math.Max(q, 0.2f * MathHelper.Clamp((rested - (goAt - CrouchTicks)) / (float)CrouchTicks, 0f, 1f));

            if (Seated)
                q += 0.025f * (float)Math.Sin(Main.GameUpdateCount * 0.2f + Slot * 0.9f);

            squash = new Vector2(1f + q, 1f - q);
        }

        /// <summary>
        /// The Stack and the Fuse. In its place, bouncing with the rest (SlimeRite.Bounce),
        /// and then drawn into the middle, stretched toward it and going faint as it
        /// goes, the far ones first so they all arrive at once.
        /// </summary>
        private void Heap(NPC npc, LegendsKingSlime king)
        {
            Vector2 altar = king.Altar;
            Vector2 place = SlimeRite.Place(altar, Slot);
            int t = king.PhaseTimer;

            npc.position = new Vector2(place.X - npc.width * 0.5f, place.Y - npc.height);
            npc.velocity = Vector2.Zero;

            if (king.Phase == KingRite.Stack)
            {
                SlimeRite.Bounce(t, SlimeRite.Row(Slot), out lift, out squash);
                return;
            }

            // Its middle as it sat at the end of the Stack, crouched, and the pile's.
            Vector2 crouched = SlimeRite.Crouched;
            float half = npc.frame.Height * npc.scale * 0.5f;
            Vector2 from = new Vector2(place.X, place.Y + 4f - half * crouched.Y);
            Vector2 core = SlimeRite.Core(altar);

            float u = SlimeRite.Drawn(t, Vector2.Distance(from, core));

            if (u >= 1f)
            {
                fade = 0f;
                return;
            }

            // Pointed at the middle, but never upside down: a slime pulled up is the same
            // shape as one pulled down, and only one of them looks like a slime.
            Vector2 toward = core - from;
            float angle = toward.LengthSquared() > 1f ? MathHelper.WrapAngle(toward.ToRotation() - MathHelper.PiOver2) : 0f;

            if (angle > MathHelper.PiOver2)
                angle -= MathHelper.Pi;
            else if (angle < -MathHelper.PiOver2)
                angle += MathHelper.Pi;

            float k = (float)Math.Pow(u, 1.5);
            float letGo = Smooth(u * 3f);

            fromMiddle = true;
            middle = Vector2.Lerp(from, core, (float)Math.Pow(u, 2.2));
            squash = Vector2.Lerp(crouched, new Vector2(1f - 0.45f * k, 1f + 1.1f * k), letGo);
            turn = angle * letGo;
            fade = 1f - 0.3f * k;
        }

        /// <summary>Not a herald any more: an ordinary slime again, where it stands. The server's call, and sent.</summary>
        internal void Dismiss(NPC npc)
        {
            Release(npc);
            npc.netUpdate = true;
        }

        private void Release(NPC npc)
        {
            Herald = false;
            Seated = false;
            Slot = -1;
            King = -1;

            npc.noTileCollide = false;
            npc.noGravity = false;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.damage = npc.defDamage;

            squash = Vector2.One;
            lift = 0f;
            turn = 0f;
            fromMiddle = false;
            fade = 1f;
        }

        /// <summary>Gone: into the King, or with nowhere to be. The server's call, sent at once.</summary>
        private static void Vanish(NPC npc)
        {
            npc.active = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>A landing: a little slime off the bottom of it, and a plop if it came down hard enough to hear.</summary>
        private static void Splat(NPC npc, float hard)
        {
            if (Main.dedServ)
                return;

            int count = 2 + (int)(hard * 6f);

            for (int i = 0; i < count; i++)
            {
                Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X, npc.position.Y + npc.height - 6f), npc.width, 6,
                    DustID.t_Slime, Main.rand.NextFloat(-2.5f, 2.5f) * (0.5f + hard), Main.rand.NextFloat(-2.5f, -0.5f) * (0.5f + hard),
                    175, npc.color, Main.rand.NextFloat(0.8f, 1.2f));

                dust.velocity *= 0.8f;
            }

            if (hard < 0.3f || Main.GameUpdateCount - lastPlop < 5)
                return;

            lastPlop = Main.GameUpdateCount;

            SoundEngine.PlaySound(SoundID.NPCHit1 with
            {
                Volume = 0.2f + 0.3f * hard,
                Pitch = 0.35f - 0.45f * hard,
                PitchVariance = 0.15f,
                MaxInstances = 4,
            }, npc.Bottom);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ---- sync -----------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(Herald);

            if (!Herald)
                return;

            bitWriter.WriteBit(Seated);
            binaryWriter.Write(King);
            binaryWriter.Write(Seed);
            binaryWriter.Write(Delay);
            binaryWriter.Write(Slot);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool herald = bitReader.ReadBit();

            if (!herald)
            {
                // Let go by the server: a slime again here too.
                if (Herald)
                    Release(npc);

                return;
            }

            bool seated = bitReader.ReadBit();

            Herald = true;
            King = binaryReader.ReadInt16();
            Seed = binaryReader.ReadInt32();
            Delay = binaryReader.ReadInt16();

            sbyte slot = binaryReader.ReadSByte();

            // Sat is sat: a packet sent a moment before this machine saw it land does
            // not stand it back up.
            Seated = seated || (Seated && slot == Slot);
            Slot = slot;
        }

        // ---- draw -----------------------------------------------------------

        /// <summary>
        /// A herald, drawn the way vanilla draws a slime — two passes, the sprite faint
        /// and then in its colour, the frame's bottom four pixels under its feet — with
        /// the squash, the lean and the lift of whatever it is doing on top. In the Fuse
        /// it is drawn about its middle, wherever that has got to.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (!Herald)
                return true;

            if (fade <= 0.01f)
                return false;

            Main.instance.LoadNPC(npc.type);

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Color light = npc.GetNPCColorTintedByBuffs(drawColor);
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Vector2 at;
            Vector2 origin;

            if (fromMiddle)
            {
                at = middle - screenPos;
                origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            }
            else
            {
                at = new Vector2(npc.position.X + npc.width / 2, npc.position.Y + npc.height + 4f + npc.gfxOffY - lift) - screenPos;
                origin = new Vector2(frame.Width * 0.5f, frame.Height);
            }

            Vector2 scale = squash * npc.scale;

            spriteBatch.Draw(texture, at, frame, npc.GetAlpha(light) * fade, turn, origin, scale, flip, 0f);

            if (npc.color != default)
                spriteBatch.Draw(texture, at, frame, npc.GetColor(light) * fade, turn, origin, scale, flip, 0f);

            return false;
        }
    }

    /// <summary>
    /// The King Slime. The arrival (the rite: heralds, altar, the pile he comes out
    /// of) and the death are his in every world; the rest here is Legends only: the
    /// bigger body, the bigger numbers, the rebuilt fight with the slam and the grab.
    /// Outside Legends the rite hands him to vanilla's own AI when it is done.
    ///
    /// This is vanilla's own AI_015 rebuilt rather than replaced with something new: the
    /// same countdown, the same four-jump cycle ending in a long one, the same teleport
    /// pressure, the same slime-shedding as its health drops, the same shrink. Written out
    /// here so one extra move can be added to the cycle as a first-class member of it.
    ///
    /// Two things the first attempt got wrong, both worth stating because they are easy to
    /// repeat. Gravity is applied by NPC.UpdateNPC AFTER the AI runs, so an AI must never
    /// add its own — doing so also means velocity.Y is never zero when the AI looks at it,
    /// and "am I on the ground" is the test the entire jump cycle hangs off. And the frame
    /// the sprite draws comes from aiAction, so an AI that never sets it leaves the King
    /// stuck on one frame no matter how it moves.
    /// </summary>
    public class LegendsKingSlime : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.KingSlime;
        }

        // ---- stats ----------------------------------------------------------

        private const float KingHealth = 1.3f;

        /// <summary>
        /// Contact damage, against vanilla's.
        ///
        /// Back to even, despite the 1.25 this was written with. The number was never the
        /// problem — it lands exactly once, before difficulty scaling, and defDamage is
        /// written after that so nothing compounds it. What changed is the body: at the
        /// sizes below the King is up to twice as wide and twice as tall, so it covers
        /// four times the ground and simply touches the player far more often than
        /// vanilla's ever could. Paying vanilla's rate on that many more contacts is
        /// already a large increase; charging a quarter more on top of it was what made
        /// the fight read as unfair rather than as heavy.
        /// </summary>
        private const float KingDamage = 1f;

        /// <summary>Size multiplier by difficulty, on top of vanilla's own scaling.</summary>
        private const float SizeNormal = 1.25f;
        private const float SizeExpert = 1.6f;
        private const float SizeMaster = 2f;

        private static float SizeForMode()
        {
            // Master reports expert as well, so it has to be tested first or every master
            // world quietly gets the expert number.
            return Main.masterMode ? SizeMaster : Main.expertMode ? SizeExpert : SizeNormal;
        }

        /// <summary>Contact damage as built, captured once the scaling has finished.</summary>
        private int baseDamage = -1;

        /// <summary>
        /// Whether it was off the ground last tick.
        ///
        /// Vanilla has no landing effect at all, so there is nothing to read this off —
        /// but a King Slime this size dropping onto stone should move some of it, so the
        /// one frame it touches down has to be found rather than inferred from a timer.
        /// </summary>
        private bool airborne;

        // ---- vanilla's own numbers -----------------------------------------

        /// <summary>Ticks the countdown gains per grounded frame, before health bonuses.</summary>
        private const float WindupRate = 2f;

        /// <summary>How long before the jump the squash tell starts.</summary>
        private const float SquashLead = -30f;

        /// <summary>Rest after a normal hop and after the long one.</summary>
        private const float RestShort = -120f;
        private const float RestLong = -200f;

        private const float HopUp = -8f;
        private const float HopSide = 4f;
        private const float LeapUp = -6f;
        private const float LeapSide = 4.5f;
        private const float BoundUp = -13f;
        private const float BoundSide = 3.5f;

        /// <summary>Cap on horizontal drift while airborne, and how fast it is reached.</summary>
        private const float AirSpeed = 3f;
        private const float AirAccel = 0.2f;

        /// <summary>Teleport pressure needed, and how far out of reach counts as pressure.</summary>
        private const float TeleportPressure = 300f;
        private const float TeleportHeadroom = 160f;

        private const float TeleportOutTime = 60f;
        private const float TeleportInTime = 30f;

        /// <summary>Beyond this the King gives up and leaves.</summary>
        private const float LeashRange = 3000f;

        /// <summary>Fraction of max health lost between litters of slimes.</summary>
        private const float SheddingStep = 0.05f;

        // ---- the added move -------------------------------------------------

        /// <summary>
        /// Jumps between specials — counted per jump, not per cycle.
        ///
        /// A cycle is four jumps, so triggering once a cycle put a special nine seconds
        /// apart, and the coin flip below then halved the slams again. Counting jumps
        /// instead is what makes the slam a move you see rather than one you hear about,
        /// and it tightens on its own as the King's wind-up shortens with its health.
        /// </summary>
        private const float SpecialEvery = 3f;

        /// <summary>
        /// Ticks planted and squashed before it launches.
        ///
        /// The move had no wind-up at all, so from the player's side the King simply left
        /// the ground at four times its usual speed with no warning. This is the tell.
        /// </summary>
        private const float SlamCrouch = 40f;

        /// <summary>How much of a slam's landing an ordinary hop is worth.</summary>
        private const float HopLandWeight = 0.35f;

        private const float SlamHeight = 500f;
        private const float SlamHeightJitter = 30f;

        /// <summary>
        /// Ticks to get overhead, from wherever it happens to be standing.
        ///
        /// A duration, not a speed. At a fixed speed the climb took as long as the
        /// distance demanded, so a slam from across the arena read completely differently
        /// from one launched at the player's feet — and neither could be learned. Pinning
        /// the time means the move has one rhythm no matter where it starts.
        /// </summary>
        private const float SlamRiseTime = 90f;

        /// <summary>Held overhead before it comes down.</summary>
        private const float SlamHang = 30f;

        /// <summary>Planted after a miss, before it tries again.</summary>
        private const float SlamRecover = 120f;

        /// <summary>The last of the recovery: the squash that says a second one is coming.</summary>
        private const float RecoverTell = 25f;

        /// <summary>Attempts per slam. Two, then it gives up and moves on.</summary>
        private const int SlamTries = 2;

        private const float SlamAccel = 1.6f;
        private const float SlamMaxFall = 34f;

        /// <summary>
        /// Safety net on the drop, not the way it is meant to end.
        ///
        /// The drop ends the moment it touches ground; this only exists so a slam over a
        /// bottomless pit cannot fall forever with the AI waiting on a landing that is
        /// never coming.
        /// </summary>
        private const float SlamDropLimit = 300f;

        /// <summary>How many tiles the ground heaves out each way from a slam's landing.</summary>
        private const int SlamWaveReach = 34;

        /// <summary>How far the ground beside the body comes up when a slam lands, in pixels.</summary>
        private const float SlamWaveLift = 20f;

        /// <summary>Ticks the heave takes to run its whole reach.</summary>
        private const int SlamWaveSpread = 36;

        /// <summary>Where the current climb set off from, so it can be timed exactly.</summary>
        private Vector2 slamStart;

        /// <summary>Attempts spent on this slam.</summary>
        private int slamTries;

        private const int GrabMinTicks = 180;
        private const int GrabMaxTicks = 301;

        // ---- ai[1] states ---------------------------------------------------
        //
        // 0..3 are vanilla's jump cycle, 5 and 6 its teleport. The slam takes 10..12,
        // deliberately clear of both, so the cycle counter can never wander into it and
        // the teleport can never fire on top of it.

        private const float StateTeleportOut = 5f;
        private const float StateTeleportIn = 6f;
        private const float StateSlamCrouch = 10f;
        private const float StateSlamRise = 11f;
        private const float StateSlamHang = 12f;
        private const float StateSlamDrop = 13f;
        private const float StateSlamRecover = 14f;

        // ---- the arrival ----------------------------------------------------
        //
        // The heralds come (SlimeRite has the ground and the pile; LegendsSlime the
        // hopping): over the land from beyond the altar and from behind the player, and
        // out of the sky in between. Each is given a place in a pile on the altar as it
        // gets near, and jumps into it. With most of them in, the camera goes over and
        // any still out are thrown in. The pile settles, bounces as one three times,
        // higher each time, and crouches; is pulled into its own middle; and he comes
        // out of it all at once, wobbling into shape. He roars (Cinematic.Roar), his
        // name comes up, and the fight starts while the camera walks home.
        //
        // The server runs it and sends the phase, the timer and the altar (SendExtraAI);
        // every machine plays it from those, and nothing seen or heard is decided by one
        // machine alone.
        //
        //     GATHER     until 14 are in their places, or ten seconds
        //     APPROACH   90    the camera goes; anyone still out is thrown in
        //     STACK     106    settle, three bounces, crouch (SlimeRite)
        //     FUSE       24    into the middle, and the core alone a moment
        //     BURST      48    him, wobbling; the crown catches the light; a breath in
        //     ROAR       80    the roar, and his name coming up under him with it
        //     HOLD       51    the name flown to the bar and settled
        //     then the fight, the camera walking home over RiteReturn

        private KingRite rite;
        private int riteTimer;

        /// <summary>Where the rite is staged. Bottom centre, on the ground.</summary>
        private Vector2 altar;

        /// <summary>The phase this machine last played the start of, so each start is played once, on every machine.</summary>
        private KingRite seen;

        private bool glinted;
        private bool plated;
        private bool finished;

        /// <summary>This machine's player was in the scene: the camera is walked home for them when it ends.</summary>
        private bool watching;

        /// <summary>The server's count of places given out in the pile, and of heralds sent out of the sky.</summary>
        private int slotsTaken;
        private int skySent;

        /// <summary>Which side of the player the altar went: the far heralds come from beyond it, the rest from behind the player.</summary>
        private int side = 1;

        /// <summary>How many have to be in their places before the camera goes. Not all twenty: one stuck straggler should not hold the whole opening hostage.</summary>
        private const int HeraldsNeeded = 14;

        /// <summary>Ceiling on the gathering, whatever the heralds are doing.</summary>
        private const int GatherLimit = 600;

        private const int RitePan = 90;

        /// <summary>After the pan: at most this long for the ones thrown in to land.</summary>
        private const int ApproachGrace = 90;

        private const int BurstTicks = 48;
        private const int RoarTicks = 80;

        /// <summary>Into the Burst, when the crown catches the light: as the size has settled.</summary>
        private const int GlintAt = 18;

        /// <summary>
        /// Held on him while his plate finishes. His name comes up with the roar, the way
        /// a title card does: started by hand a tick before it (BossIntro.PlayNameOnly,
        /// on this machine's own count, so a late packet cannot hold it back), and the
        /// plate's own LOOK is not waited for; the rite was the look. He has no line, so
        /// the plain plate: rise, fly and settle, less what the roar has already spent,
        /// and a moment more. Follows the plate's constants.
        /// </summary>
        private const int RiteHold = Content.UI.BossIntro.PlainPlateTicks - Content.UI.BossIntro.PlainLook - RoarTicks + 5;

        /// <summary>The camera's walk home once the fight has started, and the zoom let go over the same.</summary>
        private const int RiteReturn = 60;

        /// <summary>
        /// How far under the altar he is kept while the pile stands in for him: out of
        /// the middle of the shot. BossPlate starts an own-arrival boss's name by itself
        /// once the boss has sat in the middle of the screen for a moment, and the shot
        /// is on the pile for longer than that; with him there his name would come up
        /// in the middle of the pile's bounces.
        /// </summary>
        private const float SeedDepth = 160f;

        /// <summary>The push in over the name's rise. Small; the zoom is a lean, not a cut.</summary>
        private const float RiteZoom = 1.15f;

        /// <summary>
        /// How his plate runs: the plain one, a lean in over the rise, and the zoom let
        /// go over the camera's walk home.
        /// </summary>
        private static readonly Content.UI.BossIntroProfile RiteProfile = new()
        {
            ZoomIn = RiteZoom,
            ReturnTicks = RiteReturn,
        };

        /// <summary>His size while the pile stands in for him, unseen. Barely a slime, so nothing touches him.</summary>
        private const float SeedScale = 0.2f;

        /// <summary>How far from the player the altar is looked for, nearest to the ideal first.</summary>
        private static readonly float[] AltarDistances = { 650f, 560f, 740f, 480f, 820f, 900f };

        /// <summary>The altar's ground is no more than this far above or below the player's feet.</summary>
        private const float AltarRise = 320f;

        /// <summary>
        /// The heralds: nine over the land from beyond the altar, three from behind the
        /// player, eight out of the sky, twenty in all, one for every place in the pile.
        /// The far ones start just past the edge of the screen and step outward.
        /// </summary>
        private const int FarHeralds = 9;
        private const float FarNear = 520f;
        private const float FarStep = 60f;
        private const int BehindHeralds = 3;
        private const int SkyHeralds = 8;

        /// <summary>The first slime out of the sky, and the gap to each after.</summary>
        private const int SkyFirst = 20;
        private const int SkyEvery = 26;

        /// <summary>A player this near the altar is in the scene: camera, bars, held still.</summary>
        private const float SceneRange = 3500f;

        /// <summary>How far above the altar the camera looks while it is still a pile.</summary>
        private const float PileFocus = 44f;

        /// <summary>
        /// The camera's lean, as a zoom counting the player's own: in over the pan,
        /// closer through the stack, and closer again while it goes into itself. The
        /// pile is small; this is what makes it the whole shot.
        /// </summary>
        private const float LeanNear = 1.35f;
        private const float LeanClose = 1.5f;
        private const float LeanFuse = 0.06f;

        /// <summary>How big he is on the first tick of the burst, as a share of himself.</summary>
        private const float BurstFrom = 0.35f;

        /// <summary>The blue of his middle, lit. And what flies off him when he comes out.</summary>
        private static readonly Color CoreBlue = new Color(80, 150, 255);
        private static readonly Color[] BurstSparks = { Color.White, new Color(160, 215, 255), new Color(70, 140, 255) };

        /// <summary>
        /// Vanilla's crown (Extra 39) is drawn over him at his middle, raised by 70 less
        /// this for each of his six frames (Main.DrawNPCDirect_Inner), so it rides his
        /// squash as he bounces.
        /// </summary>
        private static readonly float[] CrownRise = { 2f, -6f, 2f, 10f, 2f, 0f };

        /// <summary>The size he fights at: Legends' bigger body, or the 1.25 vanilla gives him at full health.</summary>
        private static float TargetScale => PADWorld.Legends ? 1.25f * SizeForMode() : 1.25f;

        private bool InRite => rite != KingRite.None && rite != KingRite.Done;

        /// <summary>Unseen, while the pile stands in for him.</summary>
        private bool Hidden => rite >= KingRite.Gather && rite <= KingRite.Fuse;

        /// <summary>
        /// Not drawn: hidden, or the Burst has been reached but not yet played here. The
        /// phase changes at the end of one tick and is played at the start of the next,
        /// and without this he would be seen for the frame between, before his flash.
        /// </summary>
        private bool Veiled => Hidden || (rite == KingRite.Burst && seen != KingRite.Burst);

        /// <summary>The server, or a single player: whoever decides.</summary>
        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        /// <summary>Where the arrival is, for his heralds.</summary>
        public KingRite Phase => rite;
        public int PhaseTimer => riteTimer;
        public Vector2 Altar => altar;

        /// <summary>The King at this index, if there is one there.</summary>
        public static LegendsKingSlime Of(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];

            if (!npc.active || npc.type != NPCID.KingSlime)
                return null;

            return npc.TryGetGlobalNPC(out LegendsKingSlime king) ? king : null;
        }

        /// <summary>The next place in the pile, or -1 when they are all given out. The server's.</summary>
        internal int TakeSlot() => slotsTaken < SlimeRite.Slots ? slotsTaken++ : -1;

        // ---- death ----------------------------------------------------------
        //
        // Beats, in ticks, in order. Written as durations and summed into marks below, so
        // stretching one moves everything after it instead of needing eight numbers
        // re-added by hand.

        private const int DeathPan = 90;
        private const int DeathHold = 120;
        private const int DeathSlash = 120;
        private const int DeathShrink = 60;
        private const int DeathBurst = 60;
        private const int DeathNinja = 60;
        private const int DeathWatch = 120;
        private const int DeathBack = 90;

        private const int SlashAt = DeathPan + DeathHold;
        private const int ShrinkAt = SlashAt + DeathSlash;
        private const int BurstAt = ShrinkAt + DeathShrink;
        private const int NinjaAt = BurstAt + DeathBurst;
        private const int WatchAt = NinjaAt + DeathNinja;
        private const int BackAt = WatchAt + DeathWatch;
        private const int DeathEnd = BackAt + DeathBack;

        /// <summary>
        /// How long the cut is actually on screen, inside its beat.
        ///
        /// Half a second of the two. The rest of the beat is deliberately empty — the
        /// slash lands, and then nothing happens for a second and a half while the King
        /// stands there cut. That silence is the point of the beat; stretching the line
        /// itself across all of it just made a fast motion look slow.
        /// </summary>
        private const int SlashShow = 30;

        /// <summary>
        /// Thickness of the cut's middle layer. The particle builds its bloom and its
        /// core off this one number.
        /// </summary>
        private const float SlashWidth = 26f;

        /// <summary>How many slimes are left standing where the King was.</summary>
        private const int DeathSlimes = 16;

        /// <summary>Latched by CheckDead. Everything else in the AI defers to it.</summary>
        private bool dying;

        private int deathTimer;

        /// <summary>Scale at the moment it starts collapsing, so the shrink has a start.</summary>
        private float deathScale = -1f;

        private bool BodyGone => dying && deathTimer >= BurstAt;
        private bool NinjaGone => dying && deathTimer >= NinjaAt;

        // ---- setup ----------------------------------------------------------

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            // Before vanilla's difficulty scaling, which multiplies whatever it finds here
            // — so these read as "on top of the usual", which is what they mean.
            entity.lifeMax = (int)(entity.lifeMax * KingHealth);
            entity.damage = (int)(entity.damage * KingDamage);
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            // The server's: it picks the altar and calls up the heralds, and the clients
            // are told the rest by the sync (SendExtraAI).
            if (!Authority)
                return;

            npc.TargetClosest(false);

            Player player = Main.player[npc.target];

            if (!player.active || player.dead)
                return;

            altar = PickAltar(player);
            side = altar.X >= player.Center.X ? 1 : -1;

            rite = KingRite.Gather;
            riteTimer = 0;
            slotsTaken = 0;
            skySent = 0;

            Resize(npc, SeedScale);
            Anchor(npc, SeedDepth);

            SummonHeralds(npc, player);

            npc.netUpdate = true;
        }

        /// <summary>
        /// Somewhere to stand, a little way out from the player: on real ground near the
        /// height of their feet, not water, and with room above it for all of him when he
        /// comes. Both sides are tried at each distance, the nearest to the ideal first;
        /// if nowhere will do, the old way, a guess on one side.
        /// </summary>
        private static Vector2 PickAltar(Player player)
        {
            int first = Main.rand.NextBool() ? 1 : -1;

            foreach (float distance in AltarDistances)
            {
                for (int k = 0; k < 2; k++)
                {
                    int way = k == 0 ? first : -first;

                    // Kept well inside the world edge, or a fight near the ocean stages
                    // its rite out of bounds.
                    float x = MathHelper.Clamp(player.Center.X + way * distance, 80f * 16f, (Main.maxTilesX - 80) * 16f);

                    if (!SlimeRite.Ground(x, player.Bottom.Y, out float y, water: false))
                        continue;

                    if (Math.Abs(y - player.Bottom.Y) > AltarRise || !Room(x, y))
                        continue;

                    return new Vector2(x, y);
                }
            }

            float guess = MathHelper.Clamp(player.Center.X + first * AltarDistances[0], 80f * 16f, (Main.maxTilesX - 80) * 16f);

            return new Vector2(guess, SlimeRite.Ground(guess, player.Bottom.Y, out float ground) ? ground : GroundAt(guess));
        }

        /// <summary>
        /// Room for him here, grown: nothing solid where his body will be, and the ground
        /// a third of the way out either side not far off the middle's, so he does not
        /// come out of the pile half inside a hill.
        /// </summary>
        private static bool Room(float x, float y)
        {
            float width = BaseWidth * TargetScale;
            float height = BaseHeight * TargetScale + 32f;

            int left = (int)((x - width * 0.5f) / 16f);
            int right = (int)((x + width * 0.5f) / 16f);
            int top = (int)((y - height) / 16f);
            int bottom = (int)(y / 16f) - 1;

            for (int i = left; i <= right; i++)
            {
                for (int j = top; j <= bottom; j++)
                {
                    if (SlimeRite.Blocks(i, j))
                        return false;
                }
            }

            for (int way = -1; way <= 1; way += 2)
            {
                if (!SlimeRite.Ground(x + way * width / 3f, y, out float near, water: false) || Math.Abs(near - y) > 48f)
                    return false;
            }

            return true;
        }

        /// <summary>World Y of the first solid tile under a column.</summary>
        private static float GroundAt(float worldX)
        {
            int tileX = (int)(worldX / 16f);
            int from = (int)(Main.worldSurface * 0.35);

            for (int y = from; y < Main.maxTilesY - 10; y++)
            {
                Tile tile = Framing.GetTileSafely(tileX, y);

                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType])
                    return y * 16f;
            }

            return (float)Main.worldSurface * 16f;
        }

        /// <summary>
        /// Nails the King to the altar, or to a point <paramref name="below"/> it.
        ///
        /// Written every tick of the ritual, and paired with noGravity. UpdateNPC adds
        /// gravity after the AI has run, so a boss that merely zeroes its velocity is
        /// still moved a little further down every single frame.
        /// </summary>
        private void Anchor(NPC npc, float below = 0f)
        {
            npc.velocity = Vector2.Zero;
            npc.noGravity = true;
            npc.position.X = altar.X - npc.width * 0.5f;
            npc.position.Y = altar.Y + below - npc.height;
        }

        /// <summary>
        /// The heralds that come over the land, sent at the spawn: out past the edge of
        /// the screen beyond the altar, stepping outward, and from behind the player, so
        /// they come past. The sky's come later, one at a time (SendSky). Blue slimes
        /// all: a crowd of colours reads as a zoo, and a crowd of one kind as a
        /// congregation. They differ in size.
        /// </summary>
        private void SummonHeralds(NPC king, Player player)
        {
            for (int i = 0; i < FarHeralds; i++)
            {
                float x = altar.X + side * (FarNear + i * FarStep + Main.rand.NextFloat(-30f, 30f));
                SpawnHerald(king, x, GroundUnder(x, altar.Y), Main.rand.Next(0, 40), Vector2.Zero);
            }

            for (int i = 0; i < BehindHeralds; i++)
            {
                float x = player.Center.X - side * (NPC.sWidth * 0.5f + 140f + i * 110f);
                SpawnHerald(king, x, GroundUnder(x, player.Bottom.Y), Main.rand.Next(0, 20), Vector2.Zero);
            }
        }

        /// <summary>
        /// The slimes out of the sky, one every SkyEvery ticks from SkyFirst, or all that
        /// are left at once when the camera comes. They drop from over the top of every
        /// screen near the altar, somewhere either side of it, onto clear ground; where
        /// there is no clear sky to fall out of, one comes over the land instead.
        /// </summary>
        private void SendSky(NPC king, bool all)
        {
            while (skySent < SkyHeralds && (all || riteTimer >= SkyFirst + skySent * SkyEvery))
            {
                int i = skySent++;
                float x = altar.X + (i % 2 == 0 ? -1f : 1f) * Main.rand.NextFloat(140f, 460f);
                float top = altar.Y - 700f;

                foreach (Player player in Main.ActivePlayers)
                {
                    if (!player.dead && Vector2.Distance(player.Center, altar) < SceneRange)
                        top = Math.Min(top, player.Center.Y - NPC.sHeight * 0.5f - 180f);
                }

                // Not off the top of the world.
                top = Math.Max(top, 20f * 16f);

                if (SlimeRite.Ground(x, altar.Y, out float ground) && SlimeRite.Open(x, top, ground - 8f))
                {
                    SpawnHerald(king, x, top, 0, new Vector2(0f, 2f));
                    continue;
                }

                float far = altar.X + side * (FarNear + (FarHeralds + i) * FarStep);
                SpawnHerald(king, far, GroundUnder(far, altar.Y), 0, Vector2.Zero);
            }
        }

        /// <summary>The ground under this column near this height, or the height itself where there is none to find.</summary>
        private static float GroundUnder(float x, float nearY) => SlimeRite.Ground(x, nearY, out float y) ? y : nearY;

        /// <summary>One blue slime, feet here, made a herald of this King. The server's.</summary>
        private static int SpawnHerald(NPC king, float x, float bottom, int delay, Vector2 velocity)
        {
            x = MathHelper.Clamp(x, 50f * 16f, (Main.maxTilesX - 50) * 16f);

            int index = NPC.NewNPC(king.GetSource_FromAI(), (int)x, (int)bottom, NPCID.BlueSlime);

            if (index < 0 || index >= Main.maxNPCs)
                return -1;

            NPC slime = Main.npc[index];

            if (!slime.TryGetGlobalNPC(out LegendsSlime herald))
            {
                slime.active = false;
                return -1;
            }

            slime.velocity = velocity;
            herald.Summon(slime, king.whoAmI, Main.rand.Next(), delay);

            return index;
        }

        /// <summary>This King's heralds still about, and how many of them are in their places.</summary>
        private static int Heralds(NPC king, out int seated)
        {
            int count = 0;
            seated = 0;

            foreach (NPC slime in Main.ActiveNPCs)
            {
                if (!slime.TryGetGlobalNPC(out LegendsSlime herald) || !herald.Herald || herald.King != king.whoAmI)
                    continue;

                count++;

                if (herald.Seated)
                    seated++;
            }

            return count;
        }

        /// <summary>
        /// The camera is coming: everyone not yet given a place is given one and thrown
        /// at it, from wherever they are, high enough to be seen coming.
        /// </summary>
        private void CallIn(NPC king)
        {
            foreach (NPC slime in Main.ActiveNPCs)
            {
                if (!slime.TryGetGlobalNPC(out LegendsSlime herald) || !herald.Herald || herald.King != king.whoAmI)
                    continue;

                if (herald.Seated || herald.Slot >= 0)
                    continue;

                int slot = TakeSlot();

                if (slot < 0)
                {
                    herald.Dismiss(slime);
                    continue;
                }

                herald.Slot = (sbyte)slot;
                herald.Throw(slime, altar, Math.Min(40f + 0.25f * Math.Abs(slime.Center.X - altar.X), 420f));
            }
        }

        /// <summary>The pile is him now: the heralds go, all at once, and everyone is told.</summary>
        private static void TakeHeralds(NPC king)
        {
            foreach (NPC slime in Main.ActiveNPCs)
            {
                if (!slime.TryGetGlobalNPC(out LegendsSlime herald) || !herald.Herald || herald.King != king.whoAmI)
                    continue;

                slime.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, slime.whoAmI);
            }
        }

        // ---- ai -------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            // Captured before the rite zeroes it, in every world: vanilla's AI does not
            // put the damage back, so the rite has to.
            if (baseDamage < 0)
                baseDamage = npc.damage;

            // The death owns the boss outright, ahead of the ritual and the fight both.
            if (dying)
            {
                Content.Combat.Cinematic.Frame();
                RunDeath(npc);
                return false;
            }

            if (InRite)
            {
                RunRite(npc);
                return false;
            }

            // The rite is over: he is put back to himself, once, on every machine.
            if (rite == KingRite.Done && !finished)
                Finish(npc);

            // Outside Legends the arrival was the whole of it: vanilla fights from here.
            if (!PADWorld.Legends)
                return true;

            KingAI(npc);
            return false;
        }

        /// <summary>
        /// The fight proper.
        ///
        /// Laid out in vanilla's order because the order is load-bearing: the busy states
        /// are resolved first and set a flag, and the grounded jump logic only runs when
        /// nothing else has claimed the frame. That single flag is what guarantees a
        /// teleport and a slam can never both be driving the King on the same tick.
        /// </summary>
        private void KingAI(NPC npc)
        {
            // Scale is squeezed during the teleport, so the shrink and the swell are the
            // same value the rest of the AI already computes.
            float squeeze = 1f;
            bool busy = false;
            bool gone = false;

            npc.aiAction = 0;
            npc.noGravity = false;
            npc.damage = baseDamage;

            if (npc.ai[3] == 0f && npc.life > 0)
                npc.ai[3] = npc.lifeMax;

            npc.TargetClosest();

            Player player = Main.player[npc.target];

            // Out of reach entirely: give up rather than hover at the edge of the world.
            if (player.dead || Vector2.Distance(npc.Center, player.Center) > LeashRange)
            {
                npc.EncourageDespawn(10);
                npc.direction = player.Center.X < npc.Center.X ? 1 : -1;
            }

            // Teleport pressure. It builds while the King cannot see the player or the
            // player is perched well above it, which is precisely the situation where a
            // King Slime would otherwise just bounce impotently below a platform.
            bool reachable = Collision.CanHitLine(npc.Center, 0, 0, player.Center, 0, 0)
                && Math.Abs(npc.Top.Y - player.Bottom.Y) <= TeleportHeadroom;

            if (!reachable)
            {
                npc.ai[2]++;
                npc.localAI[0]++;
            }
            else if (npc.localAI[0] > 0f)
            {
                npc.localAI[0]--;
            }

            // Only from a standing start, and never on top of anything else, and not with
            // someone inside him (HoldingAnyone).
            if (!player.dead && npc.ai[2] >= TeleportPressure && npc.ai[1] < StateTeleportOut
                && npc.velocity.Y == 0f && !HoldingAnyone(npc))
            {
                StartTeleport(npc, player);
            }

            if (npc.ai[1] == StateTeleportOut)
            {
                busy = true;
                npc.aiAction = 1;
                npc.ai[0]++;

                // Shrinks away to half over the second it takes to vanish.
                squeeze = 0.5f + MathHelper.Clamp((TeleportOutTime - npc.ai[0]) / TeleportOutTime, 0f, 1f) * 0.5f;

                gone = npc.ai[0] >= TeleportOutTime;

                if (gone)
                {
                    npc.Bottom = new Vector2(npc.localAI[1], npc.localAI[2]);
                    npc.ai[1] = StateTeleportIn;
                    npc.ai[0] = 0f;
                    npc.netUpdate = true;
                }
                else
                {
                    TeleportDust(npc, 0.5f);
                }
            }
            else if (npc.ai[1] == StateTeleportIn)
            {
                busy = true;
                npc.ai[0]++;

                squeeze = 0.5f + MathHelper.Clamp(npc.ai[0] / TeleportInTime, 0f, 1f) * 0.5f;

                TeleportDust(npc, 2f);

                if (npc.ai[0] >= TeleportInTime)
                {
                    npc.ai[1] = 0f;
                    npc.ai[0] = 0f;
                    npc.netUpdate = true;
                    npc.TargetClosest();
                }
            }
            else if (npc.ai[1] >= StateSlamCrouch)
            {
                busy = true;
                RunSlam(npc, player);
            }

            // Vanilla ties both to the same flag: while it is not on screen it cannot be
            // hit, and hide is what takes the crown and the ninja away with the body.
            npc.hide = gone;
            npc.dontTakeDamage = gone;

            if (npc.velocity.Y == 0f && !busy)
            {
                Grounded(npc, player);
            }
            else
            {
                if (!busy && npc.target < 255)
                    Airborne(npc);

                if (npc.velocity.Y != 0f)
                    airborne = true;
            }

            if (!Main.dedServ)
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                    npc.velocity.X, npc.velocity.Y, 255, new Color(0, 80, 255, 80), npc.scale * 1.2f);

                dust.noGravity = true;
                dust.velocity *= 0.5f;
            }

            if (npc.life <= 0)
                return;

            ApplySize(npc, squeeze);
            Shed(npc);
        }

        /// <summary>
        /// On the ground: bleed off speed, wind up, and jump.
        ///
        /// The countdown climbs faster the more hurt the King is, which is the whole
        /// difficulty curve of the vanilla fight — it never gains a new move, it just
        /// stops giving you time between the ones it has.
        /// </summary>
        private void Grounded(NPC npc, Player player)
        {
            // A fraction of the slam's weight. Using the same burst for both made an
            // ordinary hop look like the big one, which cost the big one its impact.
            if (airborne)
            {
                airborne = false;
                Land(npc, HopLandWeight);
            }

            npc.velocity.X *= 0.8f;

            if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f)
                npc.velocity.X = 0f;

            npc.ai[0] += WindupRate;

            float ratio = npc.life / (float)npc.lifeMax;

            if (ratio < 0.8f) npc.ai[0] += 1f;
            if (ratio < 0.6f) npc.ai[0] += 1f;
            if (ratio < 0.4f) npc.ai[0] += 2f;
            if (ratio < 0.2f) npc.ai[0] += 3f;
            if (ratio < 0.1f) npc.ai[0] += 4f;

            // Still winding up. The squash frame comes in for the last half second, which
            // is the only warning a jump gets and the reason the fight is readable at all.
            if (npc.ai[0] < 0f)
            {
                if (npc.ai[0] >= SquashLead)
                    npc.aiAction = 1;

                return;
            }

            npc.netUpdate = true;
            npc.TargetClosest();

            // A slam takes the place of a jump rather than interrupting one, so the two
            // can never be in flight together.
            npc.localAI[3]++;

            // Not while he is holding someone: the special waits for the first jump after he
            // lets go (HoldingAnyone says why).
            if (npc.localAI[3] >= SpecialEvery && !HoldingAnyone(npc))
            {
                npc.localAI[3] = 0f;

                // A coin. Both are the cycle's special, so which one turns up is not
                // something the player can count out — the teleport keeps its own
                // pressure trigger as well, for the case where it genuinely cannot
                // reach them and the fight would otherwise stall.
                if (Main.rand.NextBool())
                    StartSlam(npc, player);
                else
                    StartTeleport(npc, player);

                return;
            }

            if (npc.ai[1] == 3f)
            {
                // The long one, and the end of a cycle.
                npc.velocity.Y = BoundUp;
                npc.velocity.X += BoundSide * npc.direction;
                npc.ai[0] = RestLong;
                npc.ai[1] = 0f;
            }
            else if (npc.ai[1] == 2f)
            {
                npc.velocity.Y = LeapUp;
                npc.velocity.X += LeapSide * npc.direction;
                npc.ai[0] = RestShort;
                npc.ai[1]++;
            }
            else
            {
                npc.velocity.Y = HopUp;
                npc.velocity.X += HopSide * npc.direction;
                npc.ai[0] = RestShort;
                npc.ai[1]++;
            }
        }

        /// <summary>
        /// Mid-air steering. Gentle, and capped — a King Slime commits to its arc, and
        /// being able to walk out from under one is the point.
        /// </summary>
        private static void Airborne(NPC npc)
        {
            bool room = (npc.direction == 1 && npc.velocity.X < AirSpeed)
                || (npc.direction == -1 && npc.velocity.X > -AirSpeed);

            if (!room)
                return;

            bool pushing = (npc.direction == -1 && npc.velocity.X < 0.1f)
                || (npc.direction == 1 && npc.velocity.X > -0.1f);

            if (pushing)
                npc.velocity.X += AirAccel * npc.direction;
            else
                npc.velocity.X *= 0.93f;
        }

        private static void TeleportDust(NPC npc, float speed)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustDirect(npc.position + Vector2.UnitX * -20f,
                    npc.width + 40, npc.height, DustID.t_Slime, npc.velocity.X, npc.velocity.Y,
                    150, new Color(78, 136, 255, 80), 2f);

                dust.noGravity = true;
                dust.velocity *= speed;
            }
        }

        /// <summary>
        /// Sheds a litter of slimes for every slice of health it loses. Driven off ai[3],
        /// which holds the health at the last litter, so damage that arrives in one lump
        /// pays out once rather than for every tick it took to land.
        /// </summary>
        private static void Shed(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int step = (int)(npc.lifeMax * SheddingStep);

            if (npc.life + step >= npc.ai[3])
                return;

            npc.ai[3] = npc.life;

            int litter = Main.rand.Next(1, 4);

            for (int i = 0; i < litter; i++)
            {
                int x = (int)(npc.position.X + Main.rand.Next(Math.Max(1, npc.width - 32)));
                int y = (int)(npc.position.Y + Main.rand.Next(Math.Max(1, npc.height - 32)));

                int kind = Main.expertMode && Main.rand.NextBool(4)
                    ? NPCID.SlimeSpiked
                    : NPCID.BlueSlime;

                int index = NPC.NewNPC(npc.GetSource_FromAI(), x, y, kind);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC child = Main.npc[index];

                child.SetDefaults(kind);
                child.velocity = new Vector2(Main.rand.Next(-15, 16) * 0.1f,
                    Main.rand.Next(-30, 1) * 0.1f);

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: index);
            }
        }

        // ---- teleport spot --------------------------------------------------

        /// <summary>
        /// Somewhere solid near the player with headroom, no lava, and a clear line to
        /// them. Searched as a ring so the King lands at arm's length rather than inside
        /// the player; the tighter ring is the fallback for cramped ground.
        /// </summary>
        private static Vector2 FindTeleportSpot(NPC npc, Player player, bool cornered)
        {
            // Out of patience: it comes straight down on top of them.
            if (cornered)
                return player.Bottom;

            Point centre = player.Center.ToTileCoordinates();

            if (RingSearch(npc, player, centre, 10, 7, out Vector2 wide))
                return wide;

            if (RingSearch(npc, player, centre, 6, 2, out Vector2 close))
                return close;

            return player.Bottom;
        }

        private static bool RingSearch(NPC npc, Player player, Point centre, int outer, int inner,
            out Vector2 spot)
        {
            spot = Vector2.Zero;

            int found = 0;

            for (int x = centre.X - outer; x <= centre.X + outer; x++)
            {
                for (int y = centre.Y - outer; y <= centre.Y + outer; y++)
                {
                    // The hole in the middle is what makes this a ring.
                    if (Math.Abs(x - centre.X) < inner && Math.Abs(y - centre.Y) < inner)
                        continue;

                    if (!Standable(npc, player, x, y))
                        continue;

                    // Reservoir sampling: one pass, no list, and every candidate equally
                    // likely without knowing up front how many there will be.
                    found++;

                    if (Main.rand.Next(found) == 0)
                        spot = new Vector2(x * 16 + 8, y * 16);
                }
            }

            return found > 0;
        }

        private static bool Standable(NPC npc, Player player, int x, int y)
        {
            if (x < 10 || y < 10 || x >= Main.maxTilesX - 10 || y >= Main.maxTilesY - 10)
                return false;

            Tile tile = Framing.GetTileSafely(x, y);

            if (!tile.HasUnactuatedTile)
                return false;

            bool footing = (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                || TileID.Sets.Platforms[tile.TileType];

            if (!footing || tile.LiquidType == LiquidID.Lava)
                return false;

            // Something has to be able to stand here, and it has to be able to see out.
            if (WorldGen.SolidTile(x, y - 1))
                return false;

            Vector2 stand = new Vector2(x * 16 + 8, y * 16 - npc.height / 2);

            return Collision.CanHitLine(stand, 0, 0, player.Center, 0, 0);
        }

        // ---- size -----------------------------------------------------------

        private void ApplySize(NPC npc, float squeeze)
        {
            float ratio = MathHelper.Clamp(npc.life / (float)npc.lifeMax, 0f, 1f);
            float wanted = (ratio * 0.5f + 0.75f) * squeeze * SizeForMode();

            if (Math.Abs(wanted - npc.scale) < 0.0001f)
                return;

            Resize(npc, wanted);
        }

        /// <summary>
        /// His size before scale: vanilla's own numbers, the ones AI_015 resizes him with.
        ///
        /// Not ContentSamples' King. SetDefaults multiplies the width and height by the
        /// scale it sets (1.25 for him), so the sample is already scaled, and a second
        /// scale on top made him a quarter too big for his own sprite: a hitbox reaching
        /// into thin air over his head, the ninja too high inside him, and vanilla's
        /// crown, which is placed off his middle, floating over his head.
        /// </summary>
        private const float BaseWidth = 98f;
        private const float BaseHeight = 92f;

        /// <summary>
        /// Anchored bottom-centre, the way the vanilla King Slime resizes itself. Growing
        /// from the top-left corner instead would drive it through the floor.
        /// </summary>
        private static void Resize(NPC npc, float scale)
        {
            float bottomX = npc.position.X + npc.width * 0.5f;
            float bottomY = npc.position.Y + npc.height;

            npc.scale = scale;
            npc.width = Math.Max(2, (int)(BaseWidth * scale));
            npc.height = Math.Max(2, (int)(BaseHeight * scale));

            npc.position.X = bottomX - npc.width * 0.5f;
            npc.position.Y = bottomY - npc.height;
        }

        // ---- the slam -------------------------------------------------------

        /// <summary>
        /// Whether anyone is inside him now. While someone is, he starts neither special.
        ///
        /// The slam's climb aims over its target's head and follows them (BeginRise). A
        /// target he is holding is pinned to his own middle, so the mark climbed with him:
        /// straight up he rose ten times too high, and sideways it fed on itself, because
        /// the held player hangs behind him by twice his sideways speed. Any stray half
        /// pixel (his size follows his health, the drain heals him every second, and
        /// Entity.Center rounds an odd width down) doubled every tick until he was thrown
        /// off the world with the player in him, where vanilla's own reads of the tile
        /// under the player ran off the end of the tile array. The teleport is held back
        /// with it, so the rule is one rule: he has you, he hops.
        /// </summary>
        private static bool HoldingAnyone(NPC npc)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active)
                    continue;

                SlimeGrabPlayer grab = player.GetModPlayer<SlimeGrabPlayer>();

                if (grab.HeldBy == npc.whoAmI && grab.Held)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Vanishes and picks somewhere to come back.
        ///
        /// Reached two ways: the pressure counter, when the King cannot get at the player
        /// at all, and the coin flip at the end of a jump cycle. Both land here so there
        /// is one place that knows how a teleport starts.
        /// </summary>
        private void StartTeleport(NPC npc, Player player)
        {
            npc.ai[2] = 0f;
            npc.ai[0] = 0f;
            npc.ai[1] = StateTeleportOut;

            // Long enough out of reach and it stops being polite about where it lands.
            bool cornered = npc.localAI[0] >= 360f
                || Vector2.Distance(npc.Center, player.Center) > 2000f;

            Vector2 spot = FindTeleportSpot(npc, player, cornered);

            npc.localAI[1] = spot.X;
            npc.localAI[2] = spot.Y;
            npc.netUpdate = true;
        }

        private void StartSlam(NPC npc, Player player)
        {
            npc.ai[1] = StateSlamCrouch;
            npc.ai[0] = 0f;

            slamTries = 0;
            npc.netUpdate = true;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.6f, Volume = 0.7f }, npc.Center);
        }

        /// <summary>
        /// Opens a climb from wherever the King is standing now.
        ///
        /// Called for the first attempt and again for the retry, so both are timed from
        /// their own starting point and both roll their own height.
        /// </summary>
        private void BeginRise(NPC npc, Player player)
        {
            slamStart = npc.Center;

            // Stored as an OFFSET from the player rather than an absolute height, so the
            // target follows them up a hill or down a hole instead of aiming at wherever
            // they happened to be standing when it launched.
            npc.localAI[1] = -SlamHeight + Main.rand.NextFloat(-SlamHeightJitter, SlamHeightJitter);

            npc.ai[1] = StateSlamRise;
            npc.ai[0] = 0f;
            npc.netUpdate = true;

            if (!Main.dedServ)
                Main.LocalPlayer.PIS().BumpScreenShake(5f, 14);
        }

        /// <summary>
        /// Up over the player's head, hold, and drop.
        ///
        /// The climb and the hold are scripted — gravity off, velocity written by hand —
        /// and only the drop is physics. That split is deliberate: the parts that have to
        /// be readable are on a clock, and the part that has to feel heavy is not.
        ///
        /// A miss buys one more try and no more. Letting it keep dropping until a timer
        /// expired, which is what it used to do, meant a slam could tie up the whole fight
        /// while the player simply walked away from it.
        /// </summary>
        private void RunSlam(NPC npc, Player player)
        {
            npc.ai[0]++;

            if (player.dead || !player.active)
            {
                EndSlam(npc);
                return;
            }

            if (npc.ai[1] == StateSlamCrouch)
            {
                // Planted and squashing. Everything the player needs is here: it has
                // stopped, it is not winding up a hop, and it is about to leave.
                npc.velocity = Vector2.Zero;
                npc.noGravity = true;
                npc.aiAction = 1;

                if (!Main.dedServ && npc.ai[0] % 5f == 0f)
                {
                    Dust dust = Dust.NewDustDirect(
                        new Vector2(npc.position.X, npc.position.Y + npc.height - 10f),
                        npc.width, 14, DustID.t_Slime,
                        Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f),
                        120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1f, 1.6f));

                    dust.noGravity = true;
                }

                if (npc.ai[0] >= SlamCrouch)
                    BeginRise(npc, player);

                return;
            }

            if (npc.ai[1] == StateSlamRise)
            {
                // Terrain is ignored on the way up. A boss that has to find a hole in the
                // ceiling is a boss that never uses this attack indoors.
                npc.noTileCollide = true;
                npc.noGravity = true;
                npc.aiAction = 0;

                float t = Ease(MathHelper.Clamp((npc.ai[0] + 1f) / SlamRiseTime, 0f, 1f));

                Vector2 want = new Vector2(
                    MathHelper.Lerp(slamStart.X, player.Center.X, t),
                    MathHelper.Lerp(slamStart.Y, player.Center.Y + npc.localAI[1], t));

                // Handed to velocity rather than written into position, because UpdateNPC
                // moves by velocity immediately after this. Setting position here would
                // then be moved AGAIN by whatever velocity was left over, and the boss
                // would arrive early and overshoot; this way it lands on the mark on
                // exactly the right tick and still leaves a real velocity behind for the
                // squash animation and the ninja's wobble to read.
                npc.velocity = want - npc.Center;

                if (npc.ai[0] >= SlamRiseTime)
                {
                    npc.ai[1] = StateSlamHang;
                    npc.ai[0] = 0f;
                }

                return;
            }

            if (npc.ai[1] == StateSlamHang)
            {
                npc.noTileCollide = true;
                npc.noGravity = true;
                npc.aiAction = 1;

                // Dead still. The climb already decided where this comes down, and the
                // half second is there to let the player read that decision and leave —
                // a boss that keeps creeping after them during it is answering the
                // question it just asked.
                npc.velocity = Vector2.Zero;

                if (npc.ai[0] >= SlamHang)
                {
                    npc.ai[1] = StateSlamDrop;
                    npc.ai[0] = 0f;
                    npc.velocity = new Vector2(0f, 6f);
                    npc.noTileCollide = false;

                    if (!Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item167 with { Pitch = 0.5f, Volume = 0.6f }, npc.Center);
                }

                return;
            }

            if (npc.ai[1] == StateSlamRecover)
            {
                // Sat on the ground where it missed. Long enough to punish the whiff, and
                // the squash at the end is the tell that a second one is coming.
                npc.noTileCollide = false;
                npc.noGravity = false;
                npc.velocity.X *= 0.8f;
                npc.aiAction = npc.ai[0] >= SlamRecover - RecoverTell ? 1 : 0;

                if (npc.ai[0] >= SlamRecover)
                    BeginRise(npc, player);

                return;
            }

            // ---- the drop ----

            npc.noGravity = true;
            npc.aiAction = 0;

            // Read BEFORE the acceleration below is applied.
            //
            // Collision zeroes velocity.Y on impact, but the very next thing this method
            // used to do was add SlamAccel to it — so by the time the landing test ran
            // there was no zero left to find, and the King sat grinding into the floor
            // until the drop limit timed out. That is the hovering.
            //
            // The position test is the one that actually carries it: oldPosition is
            // written by UpdateCollision, so a body that is still falling has moved and a
            // body that has landed has not, whatever anything has done to its velocity.
            bool landed = npc.ai[0] > 4f
                && (npc.velocity.Y == 0f || npc.position.Y - npc.oldPosition.Y < 0.5f);

            npc.velocity.X *= 0.96f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + SlamAccel, SlamMaxFall);

            // Air being shoved out from under it. Thrown hard sideways off the bottom
            // edge while the body falls, which is what sells the weight — the same trick
            // the Queen's slam uses, and the reason hers reads as heavy rather than fast.
            if (!Main.dedServ)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector2 at = npc.Bottom
                        - new Vector2(Main.rand.NextFloatDirection() * (npc.width * 0.4f),
                            Main.rand.Next(8));

                    Dust dust = Dust.NewDustPerfect(at, DustID.Smoke, Vector2.Zero, 40, default, 1.4f);

                    dust.noGravity = true;
                    dust.velocity.Y = npc.velocity.Y * 0.9f;
                    dust.velocity.X = (Main.rand.NextBool() ? -10f : 10f)
                        + Main.rand.NextFloatDirection() * 3f;
                }
            }

            // Caught. That ends the whole move regardless of which attempt it was.
            if (npc.Hitbox.Intersects(player.Hitbox))
            {
                Seize(npc, player);
                Land(npc, 1f);
                EndSlam(npc);
                return;
            }

            // The landing is the exit, not a timer — the drop limit is only there so a
            // slam over open air cannot wait forever for ground that is not coming.
            if (!landed && npc.ai[0] < SlamDropLimit)
                return;

            Land(npc, 1f);

            // The floor heaves out from under it both ways in a wave that dies as it runs,
            // and a fog of its own dust rolls off it. Only on a real landing: a drop that
            // ran out over a pit has no floor under it to move, and a catch is made on the
            // player, not on the ground.
            if (landed)
                GroundWave.Slam(npc.Bottom, npc.width * 0.5f, SlamWaveReach, SlamWaveLift, SlamWaveSpread);

            slamTries++;

            if (slamTries >= SlamTries)
            {
                EndSlam(npc);
                return;
            }

            npc.ai[1] = StateSlamRecover;
            npc.ai[0] = 0f;
            npc.netUpdate = true;
        }

        private void EndSlam(NPC npc)
        {
            npc.ai[1] = 0f;

            // The slam has already played its own landing; without this the next grounded
            // tick would play a second, smaller one on top of it.
            airborne = false;

            // Straight back into the jump cycle at a normal rest, so the slam costs the
            // same beat a jump would have.
            npc.ai[0] = RestShort;

            npc.noTileCollide = false;
            npc.noGravity = false;
            npc.netUpdate = true;
        }

        /// <summary>
        /// The impact.
        ///
        /// Three layers, because one is never enough to read as an impact: a low sheet of
        /// smoke thrown up and out, slime shaken off the body, and the ground itself
        /// coming up in pieces. The last one is what ties the hit to the floor rather
        /// than leaving it looking like an effect played on top of the scene.
        /// </summary>
        /// <param name="weight">Scales the whole thing. A slam is worth far more than the
        /// end of an ordinary hop, and using the same burst for both made every landing
        /// look like the big one.</param>
        private static void Land(NPC npc, float weight)
        {
            if (Main.dedServ)
                return;

            int smoke = (int)(20 * weight);

            for (int i = 0; i < smoke; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    npc.Bottom - new Vector2(npc.width / 2f, 30f), npc.width, 30,
                    DustID.Smoke, 0f, 0f, 40, default, 1.5f * weight);

                dust.noGravity = true;

                // Straight up and spread wide: a low sheet rather than a ball.
                dust.velocity.Y = -5f + Main.rand.NextFloat() * -3f;
                dust.velocity.X *= 7f;
            }

            int slime = (int)(24 * weight);

            for (int i = 0; i < slime; i++)
            {
                Dust dust = Dust.NewDustDirect(
                    new Vector2(npc.position.X, npc.position.Y + npc.height - 8f),
                    npc.width, 12, DustID.t_Slime,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f),
                    120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1.2f, 2f) * weight);

                dust.noGravity = true;
                dust.velocity *= 1.3f;
            }

            GroundDust(npc, weight);

            SoundEngine.PlaySound(SoundID.Item167 with { Volume = MathHelper.Clamp(weight, 0.3f, 1f) },
                npc.Center);

            Main.LocalPlayer.PIS().BumpScreenShake(10f * weight, (int)(24 * weight));
        }

        /// <summary>
        /// Kicks the floor up.
        ///
        /// The dust type is whatever the tile underneath would throw if it were mined, so
        /// landing on stone throws stone and landing on sand throws sand — asked for by
        /// tile rather than guessed, which also means it is right for modded ground. The
        /// velocity is then overwritten to send it upward, since the mining dust it
        /// returns is built to fall out of a block, not to be knocked out of one.
        /// </summary>
        private static void GroundDust(NPC npc, float weight)
        {
            GroundDust(npc.position.X, npc.position.X + npc.width, npc.position.Y + npc.height, weight);
        }

        /// <summary>The same, under a stretch of ground: the floor at this height, between these two x. Skeletron's blows throw it too.</summary>
        internal static void GroundDust(float left, float right, float floor, float weight)
        {
            if (Main.dedServ)
                return;

            int row = (int)((floor + 4f) / 16f);
            int from = (int)(left / 16f);
            int to = (int)(right / 16f);

            int each = Math.Max(1, (int)(3 * weight));

            for (int x = from; x <= to; x++)
            {
                Tile tile = Framing.GetTileSafely(x, row);

                if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType])
                    continue;

                for (int i = 0; i < each; i++)
                {
                    int index = WorldGen.KillTile_MakeTileDust(x, row, tile);

                    // 6000 is the "no dust for this tile" answer, and it is also one past
                    // the end of the array.
                    if (index < 0 || index >= Main.maxDust)
                        continue;

                    Dust dust = Main.dust[index];

                    // Gravity left on, so it arcs and falls back rather than drifting off.
                    dust.noGravity = false;
                    dust.velocity = new Vector2(
                        Main.rand.NextFloat(-3.5f, 3.5f),
                        Main.rand.NextFloat(-9f, -4f) * weight);

                    dust.scale = Main.rand.NextFloat(0.9f, 1.5f);
                }
            }
        }

        private static void Seize(NPC npc, Player player)
        {
            player.GetModPlayer<SlimeGrabPlayer>()
                .Seize(npc.whoAmI, Main.rand.Next(GrabMinTicks, GrabMaxTicks));

            if (Main.dedServ)
                return;

            Main.LocalPlayer.PIS().BumpScreenShake(12f, 30);
        }

        // ---- the hops, alive ------------------------------------------------
        //
        // His frames are vanilla's: an idle wobble, a quicker one for the tell, and two for
        // the air. On a body this size that is not enough to read as a jump, so every
        // machine that draws him also shapes him over those frames, from nothing but what
        // it can see: on the ground or not, how fast and which way he is going, and how
        // near his tell is to its end (Coil). Nothing here is sent, and nothing here moves
        // him or touches what he can hit.
        //
        //     COIL     lower and wider as the tell runs out, trembling, leant back from
        //              the way he is about to go; deepest before the big bound and the slam
        //     LAUNCH   tall and thin the moment he leaves, slime and dirt thrown off his base
        //     AIR      drawn out by his speed, round at the top of the jump, leaning into
        //              his way; slime dripping off him, a stream when he falls fast
        //     LAND     flattened by how hard he came down, then wobbling back into shape;
        //              the crown hops off his head as he comes back up, lands crooked, rights
        //
        // His shape and his lean are springs, so whatever cuts into whatever, he never
        // snaps from one to the next; the crown has a spring of its own and its own small
        // fall. In every world: the hop is his, Legends or not. The rite has its own shapes
        // (Jelly), and the teleport and his death only let him settle.

        /// <summary>
        /// How he is drawn over his frame: stretched about his feet (x wider, y taller),
        /// then turned by a lean about a point Pivot of his height above them, the crown
        /// tipped and lifted on its own. At rest it is vanilla's draw exactly.
        /// </summary>
        internal readonly struct Pose
        {
            public readonly Vector2 Stretch;
            public readonly float Lean;
            public readonly float Pivot;
            public readonly float CrownTilt;
            public readonly float CrownLift;

            public Pose(Vector2 stretch, float lean, float pivot, float crownTilt, float crownLift)
            {
                Stretch = stretch;
                Lean = lean;
                Pivot = pivot;
                CrownTilt = crownTilt;
                CrownLift = crownLift;
            }

            /// <summary>A stretch and nothing else: the rite's shapes.</summary>
            public static Pose Of(Vector2 stretch) => new Pose(stretch, 0f, 0f, 0f, 0f);

            public static Pose Rest => Of(Vector2.One);

            public bool IsRest => Stretch == Vector2.One && Lean == 0f && CrownTilt == 0f && CrownLift == 0f;
        }

        /// <summary>The shape's spring: its pull toward where it wants to be, and the share of its speed it loses a tick.</summary>
        private const float JiggleStiffness = 0.22f;
        private const float JiggleDamping = 0.16f;

        /// <summary>No flatter or taller than this, whatever throws him.</summary>
        private const float JiggleLimit = 0.4f;

        /// <summary>How much narrower being taller makes him: 1 would keep his area, 0 his width.</summary>
        private const float Thinning = 0.7f;

        /// <summary>How low he is at the very end of a tell: a hop, the big bound, the slam.</summary>
        private const float CoilHop = 0.14f;
        private const float CoilBound = 0.2f;
        private const float CoilSlam = 0.24f;

        /// <summary>How far he leans back from his way as a tell ends, in radians.</summary>
        private const float CoilLean = 0.05f;

        /// <summary>The trembling at the end of a tell and while he hangs over the player: up and down, and side to side.</summary>
        private const float Tremble = 0.02f;
        private const float TrembleRate = 1.9f;
        private const float Shiver = 0.02f;
        private const float ShiverRate = 2.7f;

        /// <summary>Gathered in on himself while he hangs, before the drop.</summary>
        private const float HangGather = 0.07f;

        /// <summary>
        /// How tall he is as he leaves the ground, by the speed he leaves at, and how much
        /// of the way there he goes at once. Squash to stretch in a tick is what makes the
        /// launch read as a push rather than a lift.
        /// </summary>
        private const float LaunchPerSpeed = 0.022f;
        private const float LaunchLeast = 0.1f;
        private const float LaunchMost = 0.28f;
        private const float LaunchSnap = 0.7f;
        private const float LaunchKick = 0.04f;

        /// <summary>The slam leaves from a standstill, so its launch cannot be read off its speed.</summary>
        private const float SlamLaunch = 0.3f;

        /// <summary>How drawn out he is in the air by his speed, and how round near the top of a jump.</summary>
        private const float AirPerSpeed = 0.022f;
        private const float AirMost = 0.26f;
        private const float ApexRound = 0.05f;
        private const float ApexBand = 3f;

        /// <summary>How hard a landing throws him flat, by the speed he came down at.</summary>
        private const float LandPerSpeed = 0.016f;
        private const float LandLeast = 0.06f;
        private const float LandMost = 0.24f;

        /// <summary>The lean's spring, and how far he leans into his way through the air by his speed.</summary>
        private const float LeanStiffness = 0.16f;
        private const float LeanDamping = 0.24f;
        private const float LeanPerSpeed = 0.032f;
        private const float LeanMost = 0.16f;

        /// <summary>A tick's share of the way between standing and flying, for where the lean turns.</summary>
        private const float AloftBlend = 0.3f;

        /// <summary>The crown's tilt: its spring, and the most it will tip.</summary>
        private const float CrownStiffness = 0.1f;
        private const float CrownDamping = 0.09f;
        private const float CrownTiltMost = 0.35f;

        /// <summary>The crown's hop: how fast it falls back, and how much of that it bounces.</summary>
        private const float CrownFall = 0.55f;
        private const float CrownBounce = 0.3f;

        /// <summary>
        /// Coming back up out of a landing throws the crown: harder the flatter he was, no
        /// harder than this, and only in the wobble of a landing, not in a tell.
        /// </summary>
        private const float PopPerDepth = 16f;
        private const float PopMost = 4f;
        private const float PopFrom = 0.08f;
        private const int PopWindow = 24;
        private const float PopTilt = 0.22f;

        /// <summary>The crown tips back as he launches.</summary>
        private const float LaunchTilt = 0.03f;

        /// <summary>A second launch or landing this soon after the last is the net putting him right, not him.</summary>
        private const int EventGap = 8;

        /// <summary>One drip in this many ticks in the air, on average; faster than this, one in two.</summary>
        private const int DripEvery = 6;
        private const float DripFast = 12f;

        /// <summary>Whether he was standing when this machine last looked, and whether it has looked since he was last himself.</summary>
        private bool standing;
        private bool primed;

        /// <summary>The fastest he has fallen since he left the ground: how hard he lands.</summary>
        private float peakFall;

        /// <summary>The floor he last stood on, for the ground his launch kicks up.</summary>
        private float floorY;

        /// <summary>Ticks since he last launched and last landed, as seen here.</summary>
        private int sinceLaunch = EventGap;
        private int sinceLanding = EventGap;

        /// <summary>His drift as he came down: the way the crown tips.</summary>
        private float landingDrift;

        private int shapeClock;

        /// <summary>His shape: 0 is himself, above it taller and thinner, below it lower and wider. With its speed.</summary>
        private float jiggle;
        private float jiggleSpeed;

        /// <summary>His lean, clockwise, in radians. With its speed.</summary>
        private float lean;
        private float leanSpeed;

        /// <summary>0 standing, 1 in the air, eased: the lean turns about his feet on the ground and his middle in the air.</summary>
        private float aloft;

        /// <summary>The crown's own tilt, and how far it has hopped off his head in pixels. Each with its speed.</summary>
        private float crownTilt;
        private float crownTiltSpeed;
        private float crownLift;
        private float crownLiftSpeed;

        /// <summary>How hard he trembles this tick, 0 to 1.</summary>
        private float quiver;

        public override void PostAI(NPC npc)
        {
            if (!Main.dedServ)
                Animate(npc);
        }

        /// <summary>One tick of his shape, after his AI has had its say and before he moves.</summary>
        private void Animate(NPC npc)
        {
            shapeClock++;
            sinceLaunch++;
            sinceLanding++;

            // The rite shapes him itself, and gone is gone; picked up afresh when he is back.
            if (InRite || npc.hide)
            {
                Settle();
                return;
            }

            // Being moved rather than moving: nothing he does in these is a hop, and his
            // shape only goes quietly back to himself.
            bool calm = dying || npc.ai[1] == StateTeleportOut || npc.ai[1] == StateTeleportIn;

            // Standing: on something, not rising, and not passing through the ground on the
            // slam's climb. Not "not moving": he hangs dead still in the air before the drop.
            bool grounded = !npc.noTileCollide && npc.velocity.Y >= 0f && Standing(npc);

            if (calm || !primed)
            {
                primed = !calm;
                standing = grounded;
            }
            else if (standing && !grounded)
            {
                standing = false;
                peakFall = 0f;
                Launched(npc);
            }
            else if (!standing && grounded)
            {
                standing = true;
                Landed(npc);
            }

            if (standing)
                floorY = npc.position.Y + npc.height;
            else
                peakFall = Math.Max(peakFall, npc.velocity.Y);

            float coil = calm ? 0f : Coil(npc, grounded);
            float target = 0f;
            float leanTarget = 0f;

            if (!calm)
            {
                if (grounded)
                {
                    target = -CoilDepth(npc) * coil;
                    leanTarget = -npc.direction * CoilLean * coil;
                }
                else if (npc.ai[1] == StateSlamHang)
                {
                    target = -HangGather;
                }
                else
                {
                    float speed = Math.Abs(npc.velocity.Y);

                    target = Math.Min(AirMost, speed * AirPerSpeed) - ApexRound * Math.Max(0f, 1f - speed / ApexBand);
                    leanTarget = MathHelper.Clamp(npc.velocity.X * LeanPerSpeed, -LeanMost, LeanMost);
                }
            }

            quiver = calm ? 0f : grounded ? coil : npc.ai[1] == StateSlamHang ? 1f : 0f;

            float was = jiggleSpeed;

            jiggleSpeed += (target - jiggle) * JiggleStiffness - jiggleSpeed * JiggleDamping;
            jiggle = MathHelper.Clamp(jiggle + jiggleSpeed, -JiggleLimit, JiggleLimit);

            // The turn from going down to coming back up, low in a landing's wobble.
            if (grounded && !calm && sinceLanding <= PopWindow && was < 0f && jiggleSpeed >= 0f && jiggle < -PopFrom)
                PopCrown(-jiggle);

            leanSpeed += (leanTarget - lean) * LeanStiffness - leanSpeed * LeanDamping;
            lean += leanSpeed;

            aloft += ((grounded ? 0f : 1f) - aloft) * AloftBlend;

            crownTiltSpeed += -crownTilt * CrownStiffness - crownTiltSpeed * CrownDamping;
            crownTilt = MathHelper.Clamp(crownTilt + crownTiltSpeed, -CrownTiltMost, CrownTiltMost);

            if (crownLift > 0f || crownLiftSpeed > 0f)
            {
                crownLiftSpeed -= CrownFall;
                crownLift += crownLiftSpeed;

                if (crownLift <= 0f)
                {
                    crownLift = 0f;
                    crownLiftSpeed = crownLiftSpeed < -1.2f ? -crownLiftSpeed * CrownBounce : 0f;
                }
            }

            Quieten();

            if (!grounded && !calm)
                Drip(npc);
        }

        /// <summary>Himself again at once: the rite, or while he is gone.</summary>
        private void Settle()
        {
            primed = false;
            quiver = 0f;

            jiggle = jiggleSpeed = 0f;
            lean = leanSpeed = 0f;
            aloft = 0f;
            crownTilt = crownTiltSpeed = 0f;
            crownLift = crownLiftSpeed = 0f;
        }

        /// <summary>What is too small to see is put to nothing, so a still King is drawn by vanilla again.</summary>
        private void Quieten()
        {
            if (Math.Abs(jiggle) < 0.001f && Math.Abs(jiggleSpeed) < 0.001f)
                jiggle = jiggleSpeed = 0f;

            if (Math.Abs(lean) < 0.001f && Math.Abs(leanSpeed) < 0.001f)
                lean = leanSpeed = 0f;

            if (Math.Abs(crownTilt) < 0.001f && Math.Abs(crownTiltSpeed) < 0.001f)
                crownTilt = crownTiltSpeed = 0f;

            if (aloft < 0.001f)
                aloft = 0f;
        }

        /// <summary>
        /// Something under his feet: ground or a top he can stand on just below them, or a
        /// slope or half block his feet are sunk into.
        /// </summary>
        private static bool Standing(NPC npc)
        {
            float bottom = npc.position.Y + npc.height;

            int left = (int)(npc.position.X / 16f);
            int right = (int)((npc.position.X + npc.width - 1f) / 16f);
            int below = (int)((bottom + 4f) / 16f);
            int within = (int)((bottom - 2f) / 16f);

            for (int x = left; x <= right; x++)
            {
                Tile under = Framing.GetTileSafely(x, below);

                if (under.HasUnactuatedTile && (Main.tileSolid[under.TileType] || Main.tileSolidTop[under.TileType]))
                    return true;

                Tile sunk = Framing.GetTileSafely(x, within);

                if (sunk.HasUnactuatedTile && Main.tileSolid[sunk.TileType] && !Main.tileSolidTop[sunk.TileType])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// How far through his tell he is, 0 to 1, off his own clock: the hop's countdown,
        /// the slam's crouch, the end of its recovery. Vanilla's jump cycle runs on the
        /// same numbers (its squash is the last 30 of ai[0]), so a vanilla King reads the
        /// same way; its teleport is not a tell.
        /// </summary>
        private static float Coil(NPC npc, bool grounded)
        {
            if (!grounded || npc.aiAction != 1)
                return 0f;

            float state = npc.ai[1];
            float clock = npc.ai[0];

            if (state >= 0f && state <= 3f)
                return MathHelper.Clamp((clock - SquashLead) / -SquashLead, 0f, 1f);

            if (state == StateSlamCrouch)
                return MathHelper.Clamp(clock / SlamCrouch, 0f, 1f);

            if (state == StateSlamRecover)
                return MathHelper.Clamp((clock - (SlamRecover - RecoverTell)) / RecoverTell, 0f, 1f);

            return 0f;
        }

        /// <summary>How low this tell takes him: the slam lowest, then the big bound (the fourth jump of a cycle), then a hop.</summary>
        private static float CoilDepth(NPC npc)
        {
            float state = npc.ai[1];

            if (state == StateSlamCrouch || state == StateSlamRecover)
                return CoilSlam;

            return state == 3f ? CoilBound : CoilHop;
        }

        /// <summary>He has left the ground: up tall at once, the crown tipped back, and slime and dirt thrown off his base.</summary>
        private void Launched(NPC npc)
        {
            if (sinceLaunch < EventGap)
            {
                sinceLaunch = 0;
                return;
            }

            sinceLaunch = 0;

            bool slam = npc.ai[1] == StateSlamRise;

            float launch = slam
                ? SlamLaunch
                : MathHelper.Clamp(-npc.velocity.Y * LaunchPerSpeed, LaunchLeast, LaunchMost);

            jiggle += (launch - jiggle) * LaunchSnap;
            jiggleSpeed = LaunchKick;
            crownTiltSpeed -= npc.direction * LaunchTilt;

            ThrowOff(npc, slam ? 1.2f : MathHelper.Clamp(-npc.velocity.Y / 10f, 0.4f, 1.3f));
        }

        /// <summary>He has come down: thrown flat by how hard, and slime splashed off both sides.</summary>
        private void Landed(NPC npc)
        {
            if (sinceLanding < EventGap)
            {
                sinceLanding = 0;
                return;
            }

            sinceLanding = 0;
            landingDrift = npc.velocity.X;

            jiggleSpeed -= MathHelper.Clamp(peakFall * LandPerSpeed, LandLeast, LandMost);

            Splash(npc, MathHelper.Clamp(peakFall / 12f, 0.4f, 1.5f));
        }

        /// <summary>The crown thrown off his head as he comes back up: up by how flat he was, and tipped the way he was going.</summary>
        private void PopCrown(float depth)
        {
            crownLiftSpeed = Math.Max(crownLiftSpeed, Math.Min(PopMost, depth * PopPerDepth));

            float way = Math.Abs(landingDrift) > 0.5f
                ? Math.Sign(landingDrift)
                : Main.rand.NextBool() ? 1f : -1f;

            crownTiltSpeed += way * depth * PopTilt;
        }

        /// <summary>
        /// His launch: a wet sound, slime thrown out and up off both edges of his base to
        /// fall and splat, and the ground he pushed off kicked up.
        /// </summary>
        private void ThrowOff(NPC npc, float strength)
        {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f - 0.2f * strength, Volume = 0.35f + 0.3f * strength, MaxInstances = 3 },
                npc.Bottom);

            float half = npc.width * 0.5f;
            int globs = 3 + (int)(4f * strength);

            for (int i = 0; i < globs; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;

                Vector2 from = new Vector2(npc.Center.X + side * half * Main.rand.NextFloat(0.5f, 0.95f),
                    floorY - Main.rand.NextFloat(2f, 12f));

                Vector2 velocity = new Vector2(side * Main.rand.NextFloat(1.5f, 4.5f), Main.rand.NextFloat(-4.5f, -1.5f))
                    * (0.6f + 0.4f * strength);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.6f, 1.05f), 80, SlimeGlob.Blue));
            }

            int drops = 8 + (int)(10f * strength);

            for (int i = 0; i < drops; i++)
            {
                Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X, floorY - 10f), npc.width, 10,
                    DustID.t_Slime, 0f, 0f, 120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1.1f, 1.7f));

                dust.velocity = new Vector2(Main.rand.NextFloat(-5f, 5f) * (0.6f + 0.4f * strength), Main.rand.NextFloat(-2.5f, -0.5f));
                dust.noGravity = Main.rand.NextBool(3);
            }

            GroundDust(npc.position.X, npc.position.X + npc.width, floorY, 0.2f * strength);
        }

        /// <summary>
        /// His landing's spray: slime splashed out off both sides of him, to fall and splat.
        /// In Legends that is all, since the fight's own landing (Land) throws the smoke and
        /// the ground and shakes the screen; outside it vanilla has no landing at all, so
        /// here it is the wet half of one: a sound, slime, a little ground, no shake.
        /// </summary>
        private static void Splash(NPC npc, float weight)
        {
            float half = npc.width * 0.5f;
            float floor = npc.position.Y + npc.height;
            int globs = 3 + (int)(5f * weight);

            for (int i = 0; i < globs; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;

                Vector2 from = new Vector2(npc.Center.X + side * half * Main.rand.NextFloat(0.6f, 1f),
                    floor - Main.rand.NextFloat(4f, 14f));

                Vector2 velocity = new Vector2(side * Main.rand.NextFloat(2.5f, 7f), Main.rand.NextFloat(-5.5f, -2f))
                    * (0.7f + 0.3f * weight);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.7f, 1.2f), 90, SlimeGlob.Blue));
            }

            if (PADWorld.Legends)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.75f, Volume = 0.35f + 0.25f * weight, MaxInstances = 3 },
                npc.Bottom);

            int drops = 8 + (int)(8f * weight);

            for (int i = 0; i < drops; i++)
            {
                Dust dust = Dust.NewDustDirect(new Vector2(npc.position.X, floor - 10f), npc.width, 10,
                    DustID.t_Slime, 0f, 0f, 120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1.1f, 1.7f));

                dust.velocity = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-3f, -0.5f)) * (0.6f + 0.4f * weight);
                dust.noGravity = Main.rand.NextBool(3);
            }

            GroundDust(npc, 0.25f * weight);
        }

        /// <summary>Slime dripping off him in the air: now and then, and a stream when he is going fast.</summary>
        private static void Drip(NPC npc)
        {
            bool fast = npc.velocity.Length() > DripFast;

            if (!Main.rand.NextBool(fast ? 2 : DripEvery))
                return;

            Vector2 from = new Vector2(npc.Center.X + Main.rand.NextFloat(-0.35f, 0.35f) * npc.width,
                npc.position.Y + npc.height - Main.rand.NextFloat(4f, 16f));

            // Less than his own speed, so it falls away from him rather than riding along.
            Vector2 velocity = npc.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.5f, 1.5f));

            PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.45f, 0.8f), 90, SlimeGlob.Blue));
        }

        /// <summary>His shape this frame, over his frame. Rest exactly when nothing is moving it.</summary>
        internal Pose Posed()
        {
            float shape = jiggle;
            float wide = 1f;

            if (quiver > 0f)
            {
                shape += Tremble * quiver * (float)Math.Sin(shapeClock * TrembleRate);
                wide += Shiver * quiver * (float)Math.Sin(shapeClock * ShiverRate);
            }

            Vector2 stretch = shape == 0f && wide == 1f
                ? Vector2.One
                : new Vector2((float)Math.Pow(1f + shape, -Thinning) * wide, 1f + shape);

            return new Pose(stretch, lean, aloft * 0.5f, crownTilt, crownLift);
        }

        // ---- the arrival, run ----------------------------------------------

        private void RunRite(NPC npc)
        {
            // Asked afresh every tick (Scene), so it says whether the player was in the
            // shot on the last tick of it.
            watching = false;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.noTileCollide = true;

            // His frames, through vanilla's own frame code: the fast tremble while he
            // forms and roars, his ordinary idle otherwise.
            npc.aiAction = rite == KingRite.Burst || rite == KingRite.Roar ? 1 : 0;

            // Before the size: he is not drawn as himself until this machine has played
            // the start of the Burst (Veiled), and his size is his own from that tick.
            bool entered = seen != rite;
            seen = rite;

            float size = Hidden ? SeedScale : TargetScale;

            if (Math.Abs(npc.scale - size) > 0.0001f)
                Resize(npc, size);

            Anchor(npc, Hidden ? SeedDepth : 0f);

            switch (rite)
            {
                case KingRite.Gather:
                    if (Authority)
                        Gather(npc);

                    break;

                case KingRite.Approach:
                    Scene(npc, Ease(riteTimer / (float)RitePan));

                    if (Authority && riteTimer >= RitePan)
                    {
                        int heralds = Heralds(npc, out int seated);

                        if (seated >= heralds || riteTimer >= RitePan + ApproachGrace)
                            Next(npc, KingRite.Stack);
                    }

                    break;

                case KingRite.Stack:
                    Scene(npc, 1f);
                    StackBeat(npc);

                    if (Authority && riteTimer >= SlimeRite.StackTicks)
                        Next(npc, KingRite.Fuse);

                    break;

                case KingRite.Fuse:
                    Scene(npc, 1f);
                    FuseBeat(npc);

                    if (Authority && riteTimer >= SlimeRite.FuseTicks + SlimeRite.FuseHold)
                        Next(npc, KingRite.Burst);

                    break;

                case KingRite.Burst:
                    Scene(npc, 1f);

                    if (entered)
                    {
                        if (Authority)
                            TakeHeralds(npc);

                        Burst(npc);
                    }

                    if (!glinted && riteTimer >= GlintAt)
                    {
                        glinted = true;
                        Glint(npc);
                    }

                    if (riteTimer >= BurstTicks - 1)
                        Announce(npc);

                    if (Authority && riteTimer >= BurstTicks)
                        Next(npc, KingRite.Roar);

                    break;

                case KingRite.Roar:
                    Scene(npc, 1f);

                    if (entered)
                        Roar(npc);

                    Announce(npc);
                    Spit(npc);

                    if (Authority && riteTimer >= RoarTicks)
                        Next(npc, KingRite.Hold);

                    break;

                case KingRite.Hold:
                    Scene(npc, 1f);

                    if (Authority && riteTimer >= RiteHold)
                        Next(npc, KingRite.Done);

                    break;
            }

            riteTimer++;
        }

        /// <summary>
        /// On to the next phase, from its first tick. The server's call, and sent; the
        /// timer is ticked up to zero at the end of this one.
        /// </summary>
        private void Next(NPC npc, KingRite phase)
        {
            rite = phase;
            riteTimer = -1;
            npc.netUpdate = true;
        }

        /// <summary>
        /// The server, while they come: the sky's sent on time, and when enough are in
        /// their places (or the gathering has gone on long enough) the rest are sent at
        /// once and everyone still out is thrown in as the camera goes.
        /// </summary>
        private void Gather(NPC npc)
        {
            SendSky(npc, false);

            int heralds = Heralds(npc, out int seated);
            int expected = heralds + SkyHeralds - skySent;

            if (seated < Math.Min(HeraldsNeeded, expected) && riteTimer < GatherLimit)
                return;

            SendSky(npc, true);
            CallIn(npc);
            Next(npc, KingRite.Approach);
        }

        /// <summary>
        /// He is put back to himself for the fight, on every machine, and a player who
        /// was watching gets the camera walked home and is let go; the jump cycle is the
        /// server's. His opening wind-up is longer than the walk, so nothing lands on
        /// anyone who cannot yet see him.
        /// </summary>
        private void Finish(NPC npc)
        {
            finished = true;

            if (watching && !Main.dedServ)
                PisPlayer.PanHome(Focus(), RiteReturn);

            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.noTileCollide = false;
            npc.noGravity = false;
            npc.hide = false;
            npc.damage = baseDamage;
            npc.aiAction = 0;

            if (!Authority)
                return;

            // Handed to the jump cycle mid-wind-up, so the fight opens with a beat of
            // squash rather than with an instant jump.
            npc.ai[0] = RestShort;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;

            npc.netUpdate = true;
        }

        /// <summary>
        /// The shot, for a player near enough to be in it: on the pile, then on him, with
        /// the bars, the interface away, the player held, and the camera leaning in
        /// while it is a pile and snapping back out when he comes out of it.
        /// </summary>
        private void Scene(NPC npc, float blend)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || Vector2.Distance(me.Center, altar) > SceneRange)
                return;

            watching = true;

            PisPlayer.BlendCamera(Focus(), blend);
            PisPlayer.HideUI();
            Content.Combat.Cinematic.Hold();
            Content.Combat.Cinematic.Frame();

            switch (rite)
            {
                case KingRite.Approach:
                    LeanTo(MathHelper.Lerp(1f, LeanNear, Ease(riteTimer / (float)RitePan)));
                    break;

                case KingRite.Stack:
                    LeanTo(MathHelper.Lerp(LeanNear, LeanClose, riteTimer / (float)SlimeRite.StackTicks));
                    break;

                case KingRite.Fuse:
                    LeanTo(LeanClose + LeanFuse * Math.Min(1f, riteTimer / (float)SlimeRite.FuseTicks));
                    break;

                case KingRite.Burst:
                    // Out, fast, as he comes out: the size of him is the point.
                    Content.Combat.Cinematic.Lean(1f, 0.25f);
                    break;
            }
        }

        /// <summary>Where the camera looks: the pile's middle while it is a pile, his after.</summary>
        private Vector2 Focus() => altar - new Vector2(0f, Hidden ? PileFocus : BaseHeight * TargetScale * 0.45f);

        /// <summary>
        /// The camera leaned in to this zoom, counting the player's own: a framing, not a
        /// multiple, so a player who already plays zoomed in that far is not pushed
        /// further.
        /// </summary>
        private static void LeanTo(float zoom)
        {
            Content.Combat.Cinematic.Lean(Math.Max(1f, zoom / Math.Max(1f, Main.GameZoomTarget)));
        }

        /// <summary>The pile landing, three times: louder, harder, and the ground kicked up.</summary>
        private void StackBeat(NPC npc)
        {
            if (Main.dedServ)
                return;

            int landed = SlimeRite.Landing(riteTimer);

            if (landed == 0)
                return;

            float weight = landed / (float)SlimeRite.Bounces;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f + 0.35f * weight, Pitch = -0.5f + 0.2f * (landed - 1), MaxInstances = 3 }, altar);
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.25f + 0.35f * weight, Pitch = -0.2f }, altar);

            Main.LocalPlayer.PIS().BumpScreenShake(2f + 3f * landed, 10 + 4 * landed);

            float half = SlimeRite.Width * 0.5f;

            for (int i = 0; i < 6 + landed * 5; i++)
            {
                Dust dust = Dust.NewDustDirect(new Vector2(altar.X - half, altar.Y - 8f), (int)SlimeRite.Width, 8, DustID.t_Slime,
                    Main.rand.NextFloat(-4f, 4f) * (0.5f + weight), Main.rand.NextFloat(-3f, -0.5f) * (0.5f + weight),
                    175, new Color(0, 80, 255, 100), Main.rand.NextFloat(1f, 1.5f));

                dust.noGravity = Main.rand.NextBool();
            }

            GroundDust(altar.X - half, altar.X + half, altar.Y, 0.35f * landed);
        }

        /// <summary>
        /// The pile going in: slime pulled off it into the middle, a gulp as each part
        /// goes, and the middle lighting up, brighter and bigger the more of them are in
        /// it, and shivering once they all are.
        /// </summary>
        private void FuseBeat(NPC npc)
        {
            if (Main.dedServ)
                return;

            int t = riteTimer;
            Vector2 core = SlimeRite.Core(altar);
            float full = Math.Min(1f, t / (float)SlimeRite.FuseTicks);

            if (t < SlimeRite.FuseTicks)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 out_ = Main.rand.NextVector2CircularEdge(1f, 0.7f) * Main.rand.NextFloat(55f, 110f);
                    Vector2 swirl = out_.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(1f, 3f);

                    PADParticleHandler.SpawnParticle(new SlimeGlob(core + out_, swirl, Main.rand.NextFloat(0.6f, 1.1f), 60,
                        SlimeGlob.Blue, falls: false).HomeTo(core, 0));
                }

                if (t % 3 == 0)
                {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.6f + 0.8f * full, MaxInstances = 3 }, core);
                }
            }
            else
            {
                Main.LocalPlayer.PIS().BumpScreenShake(2.5f, 2);
            }

            if (t % 2 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(core, Vector2.Zero, CoreBlue * (0.5f + 0.5f * full),
                    (24f + 80f * full) / 160f, 10));
            }

            Lighting.AddLight(core, 0.15f + 0.35f * full, 0.3f + 0.5f * full, 0.5f + 0.5f * full);
        }

        /// <summary>
        /// He comes out: a flash, the landing of all of him at once, the pile thrown off
        /// him in globs that all come back to him, and a burst of light where the middle
        /// was.
        /// </summary>
        private void Burst(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 core = SlimeRite.Core(altar);

            Content.Combat.Cinematic.Flash(new Color(110, 180, 255), 0.35f);

            Land(npc, 1.25f);

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.7f }, core);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.55f, Volume = 1f }, core);

            for (int i = 0; i < 28; i++)
            {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.35f, 1.35f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 11f);

                PADParticleHandler.SpawnParticle(new SlimeGlob(core, velocity, Main.rand.NextFloat(0.8f, 1.5f), 120,
                    SlimeGlob.Blue).HomeTo(npc, Main.rand.Next(26, 45)));
            }

            SparkShard.Spray(core, Color.White, 14, 7f, npc.height * 0.55f, 20, palette: BurstSparks);

            PADParticleHandler.SpawnParticle(new BloomGeneric(core, Vector2.Zero, CoreBlue, npc.width * 1.8f / 160f, 26));
        }

        /// <summary>The crown catches the light, once, as he settles into his size.</summary>
        private void Glint(NPC npc)
        {
            if (Main.dedServ)
                return;

            Jelly(out float size, out Vector2 stretch);

            // A little over the middle of the crown: on it whichever way he faces.
            Texture2D crown = TextureAssets.Extra[ExtrasID.KingSlimeCrown].Value;
            Vector2 at = CrownAt(npc, size, Pose.Of(stretch)) - new Vector2(0f, crown.Height * 0.15f * size);

            PADParticleHandler.SpawnParticle(new CrownGlint(at, 34f, 30, new Color(255, 244, 200)));

            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.2f, Volume = 0.7f }, at);
        }

        /// <summary>He roars (Cinematic.Roar: the rings, the dim, the shake), and something wet in it.</summary>
        private static void Roar(NPC npc)
        {
            if (Main.dedServ)
                return;

            Content.Combat.Cinematic.Roar(npc, RoarTicks, 1f, SoundID.Roar with { Pitch = -0.4f, Volume = 1f });

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.85f, Volume = 0.9f }, npc.Center);
        }

        /// <summary>His name, once, under him: the plate, the long one (RiteProfile).</summary>
        private void Announce(NPC npc)
        {
            if (plated)
                return;

            plated = true;
            Content.UI.BossIntro.PlayNameOnly(npc.whoAmI, RiteProfile);
        }

        /// <summary>Slime shaken off the top of him while he roars, and left to fall.</summary>
        private void Spit(NPC npc)
        {
            if (Main.dedServ || riteTimer >= 60 || riteTimer % 3 != 0)
                return;

            for (int i = 0; i < 2; i++)
            {
                float across = Main.rand.NextFloat(-0.42f, 0.42f);
                Vector2 from = new Vector2(npc.Center.X + across * npc.width, npc.position.Y + npc.height * 0.3f);
                Vector2 velocity = new Vector2(across * Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(-7f, -3f));

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.6f, 1.1f), 70, SlimeGlob.Blue));
            }
        }

        // ---- the arrival, drawn ---------------------------------------------

        /// <summary>
        /// His shape while the rite has him, on top of his scale: an overall size, and a
        /// stretch (x wider, y taller) about his feet. False when he is drawn exactly as
        /// vanilla draws him.
        ///
        ///   BURST     out of the core at a third of himself, past his size and back,
        ///             wobbling tall and wide as it settles; the last twelve ticks, a
        ///             breath in, low and wide
        ///   ROAR      up tall and narrow, rung like a bell at the start, trembling
        ///   HOLD      breathing, and still by the end, so the hand back to vanilla's
        ///             draw cannot be seen
        /// </summary>
        private bool Jelly(out float size, out Vector2 stretch)
        {
            size = 1f;
            stretch = Vector2.One;

            float t = riteTimer;

            switch (rite)
            {
                case KingRite.Burst:
                {
                    size = 1f + (BurstFrom - 1f) * (float)Math.Exp(-t / 6f) * (float)Math.Cos(0.32f * t);

                    float wobble = 0.25f * (float)Math.Exp(-t / 9f) * (float)Math.Sin(0.5f * t + 0.9f);
                    float breath = 0.1f * Ease((t - (BurstTicks - 12)) / 12f);

                    stretch = new Vector2((1f - wobble) * (1f + 0.8f * breath), (1f + wobble) * (1f - breath));
                    return true;
                }

                case KingRite.Roar:
                {
                    float voice = Ease(t / 6f) * (1f - Ease((t - (RoarTicks - 18)) / 18f));
                    float ring = 0.12f * (float)Math.Exp(-t / 6f) * (float)Math.Cos(0.8f * t);

                    stretch = new Vector2(
                        1f - 0.07f * voice - 0.6f * ring + 0.012f * (float)Math.Sin(3.1f * t) * voice,
                        1f + 0.11f * voice + ring + 0.012f * (float)Math.Sin(2.6f * t) * voice);
                    return true;
                }

                case KingRite.Hold:
                {
                    // Breathing, and still again by the end, so the hand back to
                    // vanilla's draw cannot be seen.
                    float breath = 0.018f * (float)Math.Sin(0.12f * t) * (1f - Ease((t - (RiteHold - 20)) / 20f));
                    stretch = new Vector2(1f - 0.6f * breath, 1f + breath);
                    return true;
                }
            }

            return false;
        }

        /// <summary>The bottom centre of his frame, where vanilla anchors it: four pixels under his feet.</summary>
        private static Vector2 Feet(NPC npc) =>
            new Vector2(npc.position.X + npc.width / 2, npc.position.Y + npc.height + 4f + npc.gfxOffY);

        /// <summary>
        /// Where vanilla draws the crown's middle, moved with his shape: stretched about
        /// his feet, lifted off his head by its own hop, and turned with his lean.
        /// </summary>
        private static Vector2 CrownAt(NPC npc, float size, Pose pose)
        {
            int frame = npc.frame.Height > 0 ? npc.frame.Y / npc.frame.Height : 0;
            float rise = CrownRise[Math.Clamp(frame, 0, CrownRise.Length - 1)];

            Vector2 vanilla = npc.Center + new Vector2(0f, npc.gfxOffY - (70f - rise) * npc.scale);
            Vector2 feet = Feet(npc);

            return Turned(npc, feet + (vanilla - feet) * pose.Stretch * size - new Vector2(0f, pose.CrownLift), pose);
        }

        /// <summary>
        /// Where vanilla seats the ninja: his middle, lagging his motion twice as hard
        /// sideways as vertically, and nudged by whichever squash frame is showing.
        /// </summary>
        private static Vector2 NinjaSeat(NPC npc)
        {
            Vector2 lag = new Vector2(-npc.velocity.X * 2f, -npc.velocity.Y);

            if (npc.frame.Y == 120)
                lag.Y += 2f;
            else if (npc.frame.Y == 360)
                lag.Y -= 2f;
            else if (npc.frame.Y == 480)
                lag.Y -= 6f;

            return new Vector2(npc.position.X + npc.width / 2, npc.position.Y + npc.height / 2) + lag;
        }

        /// <summary>A point of his, moved with his shape: stretched about his feet, then turned with his lean.</summary>
        private static Vector2 Shaped(NPC npc, Vector2 point, float size, Pose pose)
        {
            Vector2 feet = Feet(npc);

            return Turned(npc, feet + (point - feet) * pose.Stretch * size, pose);
        }

        /// <summary>Turned with his lean, about the point Pivot of his height above his feet.</summary>
        private static Vector2 Turned(NPC npc, Vector2 point, Pose pose)
        {
            if (pose.Lean == 0f)
                return point;

            Vector2 pivot = Feet(npc) - new Vector2(0f, npc.height * pose.Pivot);

            return point.RotatedBy(pose.Lean, pivot);
        }

        // ---- holding someone, drawn -------------------------------------------

        /// <summary>
        /// Set by SlimeGrabOverlay on each frame it has drawn his body and crown over a
        /// player he holds; read and cleared by his own draw. He leaves the body to the
        /// overlay only while it is doing it, so a held player who somehow was not drawn
        /// costs at most one frame of him.
        /// </summary>
        internal bool Carried;

        /// <summary>Whether a held player may carry his body: in the fight, not the arrival or the death.</summary>
        internal bool Carriable => rite == KingRite.Done && !dying;

        /// <summary>
        /// Of the players he holds, the one drawn last, or -1: the others draw in order
        /// of index, and the local player after all of them. Only those drawn the
        /// ordinary way, after the NPCs, count.
        /// </summary>
        internal static int Carrier(int king)
        {
            int carrier = -1;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead || player.ghost || player.stoned || player.ShouldNotDraw || player.isLockedToATile)
                    continue;

                if (i != Main.myPlayer && player.outOfRange)
                    continue;

                SlimeGrabPlayer grab = player.GetModPlayer<SlimeGrabPlayer>();

                if (!grab.Held || grab.HeldBy != king)
                    continue;

                if (i == Main.myPlayer)
                    return i;

                carrier = i;
            }

            return carrier;
        }

        /// <summary>
        /// His body and crown, placed and lit as vanilla draws them and in the shape his
        /// hops have him in, as player draw data for the overlay, and kept upright however
        /// the player they cover is turned.
        /// </summary>
        internal static void AddBodyAndCrown(NPC npc, Pose pose, List<DrawData> into)
        {
            Main.instance.LoadNPC(npc.type);

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Texture2D crown = TextureAssets.Extra[ExtrasID.KingSlimeCrown].Value;

            Color light = npc.GetNPCColorTintedByBuffs(Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f)));
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Rectangle frame = npc.frame;

            into.Add(new DrawData(body, Turned(npc, Feet(npc), pose) - Main.screenPosition, frame, npc.GetAlpha(light),
                npc.rotation + pose.Lean, new Vector2(frame.Width * 0.5f, frame.Height), npc.scale * pose.Stretch, flip, 0f)
                { ignorePlayerRotation = true });

            into.Add(new DrawData(crown, CrownAt(npc, 1f, pose) - Main.screenPosition, null, light, pose.Lean + pose.CrownTilt,
                crown.Size() * 0.5f, 1f, flip, 0f) { ignorePlayerRotation = true });
        }

        /// <summary>
        /// Nothing is drawn at all while the pile stands in for him — body, crown and
        /// ninja alike. While the rite has him after that, he is drawn here, in its
        /// shape (DrawKing). While he holds someone, only the ninja is drawn here; the
        /// body and crown go over them (SlimeGrabOverlay). Otherwise he is drawn here
        /// while his hops shape him (Animate), and left to vanilla while nothing does.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            bool carried = Carried;
            Carried = false;

            if (Veiled)
                return false;

            Pose pose = Posed();

            if (!BodyGone)
            {
                if (!dying && Jelly(out float size, out Vector2 stretch))
                {
                    DrawKing(npc, spriteBatch, screenPos, drawColor, size, Pose.Of(stretch));
                    return false;
                }

                if (carried && Carriable && Carrier(npc.whoAmI) >= 0)
                {
                    DrawNinja(npc, spriteBatch, screenPos, drawColor, pose);
                    return false;
                }

                if (!pose.IsRest)
                {
                    DrawKing(npc, spriteBatch, screenPos, drawColor, 1f, pose);
                    return false;
                }

                return true;
            }

            // The body is gone but the ninja is not, and vanilla draws them together: the
            // body, the ninja inside it, and the crown (Extra 39) over it, all in one
            // pass, so there is no alpha that hides one and keeps the others. Refusing
            // the whole draw and putting the ninja back by hand is what lets him outlive
            // the thing he was riding in; the crown went with the body, as its gore.
            if (!NinjaGone)
                DrawNinja(npc, spriteBatch, screenPos, drawColor, pose);

            return false;
        }

        /// <summary>
        /// Him, the way vanilla draws him: the ninja, the body over him, and the crown
        /// over that, placed off his middle and his frame. With a shape on top (Pose),
        /// about his feet: the ninja and the crown are carried by it and keep their own
        /// size. At a size of one and a pose at rest this is vanilla's draw exactly, so
        /// the hand back to vanilla, at the end of the rite and whenever a hop has settled,
        /// does not show.
        /// </summary>
        private static void DrawKing(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, float size, Pose pose)
        {
            Main.instance.LoadNPC(npc.type);

            Texture2D body = TextureAssets.Npc[npc.type].Value;
            Texture2D ninja = TextureAssets.Ninja.Value;
            Texture2D crown = TextureAssets.Extra[ExtrasID.KingSlimeCrown].Value;

            Color light = npc.GetNPCColorTintedByBuffs(drawColor);
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Rectangle frame = npc.frame;

            spriteBatch.Draw(ninja, Shaped(npc, NinjaSeat(npc), size, pose) - screenPos, null, light,
                npc.velocity.X * 0.05f + pose.Lean, new Vector2(ninja.Width / 2, ninja.Height / 2), size, SpriteEffects.None, 0f);

            spriteBatch.Draw(body, Turned(npc, Feet(npc), pose) - screenPos, frame, npc.GetAlpha(light), npc.rotation + pose.Lean,
                new Vector2(frame.Width * 0.5f, frame.Height), npc.scale * pose.Stretch * size, flip, 0f);

            spriteBatch.Draw(crown, CrownAt(npc, size, pose) - screenPos, null, light, pose.Lean + pose.CrownTilt,
                crown.Size() * 0.5f, size, flip, 0f);
        }

        /// <summary>
        /// The ninja alone, placed the way vanilla places him (NinjaSeat), tilted by the
        /// horizontal speed, and carried by the King's shape.
        /// </summary>
        private static void DrawNinja(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, Pose pose)
        {
            Texture2D ninja = TextureAssets.Ninja.Value;

            spriteBatch.Draw(ninja, Shaped(npc, NinjaSeat(npc), 1f, pose) - screenPos, null, npc.GetNPCColorTintedByBuffs(drawColor),
                npc.velocity.X * 0.05f + pose.Lean, new Vector2(ninja.Width / 2, ninja.Height / 2), 1f, SpriteEffects.None, 0f);
        }

        // ---- death ----------------------------------------------------------

        /// <summary>
        /// Refuses the first death and hands the King to the animation instead.
        ///
        /// Life pinned at one, damage off, and false returned so vanilla does not remove
        /// it. The sequence zeroes the life itself at the end and calls checkDead again;
        /// the flag makes that second call the real one, which is the frame the loot
        /// drops on.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (dying)
                return true;

            dying = true;
            deathTimer = 0;

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;

            return false;
        }

        /// <summary>
        /// The end of the King.
        ///
        /// Pan out, a long silent hold, then it comes apart in stages — cut, collapse,
        /// burst, and the ninja last of all. The silence before the cut is doing as much
        /// work as anything after it: with no pause the slash lands while the fight is
        /// still ringing and reads as one more attack rather than as the end.
        /// </summary>
        private void RunDeath(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.noTileCollide = false;
            npc.aiAction = 0;

            // Left to settle rather than frozen mid-air, in case it died during a slam.
            npc.velocity.X *= 0.9f;

            if (npc.velocity.Y == 0f)
                npc.velocity.X *= 0.8f;

            DeathCamera(npc);
            PisPlayer.HideUI();

            if (deathTimer == SlashAt)
                Cut(npc);

            if (deathTimer >= ShrinkAt && deathTimer < BurstAt)
                Collapse(npc);

            if (deathTimer == BurstAt)
                Rupture(npc);

            if (deathTimer == NinjaAt)
                NinjaGoes(npc);

            // Smoke and slime keep coming off it right through the middle beats, so the
            // holds are never actually still.
            if (!Main.dedServ && deathTimer > SlashAt && deathTimer < NinjaAt && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                    Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, 0f),
                    120, new Color(0, 80, 255, 100), Main.rand.NextFloat(1f, 1.8f));

                dust.noGravity = true;
            }

            deathTimer++;

            if (deathTimer < DeathEnd)
                return;

            // Handed back to vanilla. CheckDead now returns true, so this is the frame the
            // loot drops on and the camera is already home.
            //
            // HitEffect is deliberately NOT called here. Vanilla's own runs a King Slime
            // branch when life hits zero that throws two hundred dust AND a second crown
            // gore — which is where the duplicate crown was coming from. Everything that
            // branch would have done has already happened, at the moment the body went,
            // and it looked better there.
            npc.life = 0;
            npc.checkDead();
        }

        private void DeathCamera(NPC npc)
        {
            if (Main.dedServ)
                return;

            float blend =
                deathTimer < DeathPan ? Ease(deathTimer / (float)DeathPan)
                : deathTimer < BackAt ? 1f
                : 1f - Ease((deathTimer - BackAt) / (float)DeathBack);

            PisPlayer.BlendCamera(npc.Center, blend);
        }

        /// <summary>
        /// The cut lands.
        ///
        /// The line itself is a SlashStreak particle rather than something drawn in
        /// PostDraw. That buys two things: the particle handler draws it additively, which
        /// is what makes it read as light rather than a painted bar, and it draws at the
        /// dust layer — above the King — without this class having to close and reopen
        /// the sprite batch mid-frame to change blend state.
        /// </summary>
        private static void Cut(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = -0.6f }, npc.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(8f, 20);

            // Down and to the right, through him, and longer than he is — so it reads as
            // something that passed through rather than something drawn on.
            Vector2 along = new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX);
            float reach = npc.Size.Length() * 1.15f;

            PADParticleHandler.SpawnParticle(
                new SlashStreak(npc.Center, along, reach, SlashWidth, SlashShow, npc));

            // Thrown along the cut in both directions, so the spray follows the line
            // rather than blooming out of the middle of him.

            for (int i = 0; i < 40; i++)
            {
                Vector2 at = npc.Center + along * Main.rand.NextFloat(-npc.width * 0.6f, npc.width * 0.6f);

                Dust dust = Dust.NewDustPerfect(at, DustID.t_Slime,
                    along.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-6f, 6f),
                    80, new Color(120, 200, 255, 80), Main.rand.NextFloat(1.2f, 2f));

                dust.noGravity = true;
            }
        }

        /// <summary>Falls in on itself for a second before it goes.</summary>
        private void Collapse(NPC npc)
        {
            if (deathScale < 0f)
                deathScale = npc.scale;

            float t = (deathTimer - ShrinkAt) / (float)DeathShrink;

            Resize(npc, MathHelper.Lerp(deathScale, deathScale * 0.45f, Ease(t)));

            if (Main.dedServ || !Main.rand.NextBool(2))
                return;

            Dust puff = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.t_Slime,
                Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-6f, -1f),
                80, new Color(0, 80, 255, 80), Main.rand.NextFloat(1.4f, 2.2f));

            puff.noGravity = true;
        }

        /// <summary>
        /// The body lets go: dust, a crowd of slimes, and the crown falls off as a gore.
        ///
        /// The crown is part of the King's sprite rather than a separate draw, so it can
        /// only leave by the body leaving — the gore is what carries it on afterwards,
        /// and it is the same one vanilla drops when the King teleports.
        /// </summary>
        private void Rupture(NPC npc)
        {
            if (!Main.dedServ)
            {
                SlimeDust(npc.position, npc.width, npc.height, 70, new Color(0, 80, 255, 100));

                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.4f, Volume = 1f }, npc.Center);
                Main.LocalPlayer.PIS().BumpScreenShake(14f, 34);

                Gore.NewGore(npc.GetSource_FromAI(), npc.Center + new Vector2(-40f, -npc.height / 2f),
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -4f), GoreID.KingSlimeCrown);
            }

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int i = 0; i < DeathSlimes; i++)
            {
                int index = NPC.NewNPC(npc.GetSource_FromAI(),
                    (int)npc.Center.X, (int)(npc.position.Y + npc.height), NPCID.BlueSlime);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC slime = Main.npc[index];

                slime.velocity = new Vector2(Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-11f, -5f));
                slime.netUpdate = true;
            }
        }

        /// <summary>The last of him. Nothing is left drawing after this.</summary>
        private static void NinjaGoes(NPC npc)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 10; i++)
            {
                Particles.BloodDroplet.SpawnBurst(npc.Center + Main.rand.NextVector2Circular(10f, 10f),
                    (MathHelper.TwoPi * i / 10f).ToRotationVector2(), 9, 18f);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.5f, Volume = 0.8f }, npc.Center);
            Main.LocalPlayer.PIS().BumpScreenShake(7f, 20);
        }

        /// <summary>Smoothstep. Zero slope at both ends, so pans start and stop softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ---- sync -----------------------------------------------------------
        //
        // The rite is the server's; this is how the clients follow it. A new phase is
        // taken with its timer; within a phase each machine keeps its own count, and a
        // resend only moves it if it has drifted, so a late packet cannot put the
        // animation back a few ticks.

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)rite);
            binaryWriter.Write((short)Math.Clamp(riteTimer, short.MinValue, short.MaxValue));
            binaryWriter.WriteVector2(altar);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            KingRite phase = (KingRite)binaryReader.ReadByte();
            int timer = binaryReader.ReadInt16();
            altar = binaryReader.ReadVector2();

            if (phase != rite)
            {
                rite = phase;
                riteTimer = timer;
            }
            else if (Math.Abs(timer - riteTimer) > 10)
            {
                riteTimer = timer;
            }
        }

        /// <summary>
        /// A scatter of slime, thrown up and out of a box.
        ///
        /// The helper the ritual's burst and the King's death both want. It went missing
        /// when the burst was rewritten to inline its own loop, which left the death
        /// calling something that no longer existed.
        /// </summary>
        /// <param name="tint">Pass default for the King's own blue.</param>
        private static void SlimeDust(Vector2 position, int width, int height, int count, Color tint)
        {
            if (tint == default)
                tint = new Color(0, 80, 255, 100);

            for (int i = 0; i < count; i++)
            {
                Dust dust = Dust.NewDustDirect(position, width, height, DustID.t_Slime,
                    Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, 1f),
                    120, tint, Main.rand.NextFloat(1.1f, 1.8f));

                dust.noGravity = true;
                dust.velocity *= 1.2f;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (InRite)
                return false;

            if (!PADWorld.Legends)
                return true;

            // Already being drained. Contact damage on top of the grab would be charging
            // twice for the same hit.
            return !target.GetModPlayer<SlimeGrabPlayer>().Held;
        }
    }

    /// <summary>
    /// Being inside the King Slime.
    ///
    /// Held at its centre, unable to move or use anything, losing health a second at a
    /// time — and what is lost goes into the boss rather than simply going away.
    /// </summary>
    public class SlimeGrabPlayer : ModPlayer
    {
        /// <summary>Index of the NPC holding this player, or -1.</summary>
        public int HeldBy = -1;

        public int HeldTicks;

        private int drain;

        /// <summary>Health taken, and given to the King, once a second.</summary>
        private const int DrainPerSecond = 20;

        public bool Held =>
            HeldBy >= 0
            && HeldBy < Main.maxNPCs
            && Main.npc[HeldBy].active
            && Main.npc[HeldBy].type == NPCID.KingSlime
            && HeldTicks > 0
            && !Player.dead;

        public void Seize(int npcIndex, int ticks)
        {
            HeldBy = npcIndex;
            HeldTicks = ticks;
            drain = 0;
        }

        /// <summary>Grace after being spat out, so the release is not a second attack.</summary>
        private const int ReleaseGrace = 60;

        public override void ResetEffects()
        {
            if (!Held)
            {
                if (HeldBy >= 0)
                    Release();

                HeldBy = -1;
                HeldTicks = 0;
                return;
            }

            HeldTicks--;

            // Blocks every item, including ones that bypass the control flags below.
            Player.noItems = true;
        }

        /// <summary>
        /// Lets go.
        ///
        /// The player is standing in the middle of the King at this moment, so without a
        /// grace window the very next tick is a full contact hit, and walking out of a
        /// body that size is several more. That was the single worst source of damage in
        /// the fight, and none of it was the boss actually landing anything — it was the
        /// player being released inside it and charged for the trip out.
        /// </summary>
        private void Release()
        {
            Player.fullRotation = 0f;

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, ReleaseGrace);

            // Thrown clear as well, so the grace is spent leaving rather than standing in
            // the middle of it waiting for the timer.
            if (HeldBy >= 0 && HeldBy < Main.maxNPCs && Main.npc[HeldBy].active)
            {
                Vector2 out2 = (Player.Center - Main.npc[HeldBy].Center)
                    .SafeNormalize(-Vector2.UnitY);

                Player.velocity = out2 * 6f + new Vector2(0f, -4f);
            }
        }

        public override void SetControls()
        {
            if (!Held)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override void PreUpdateMovement()
        {
            if (!Held)
                return;

            NPC king = Main.npc[HeldBy];

            // The ninja's own wobble, lifted from how vanilla draws him inside the King:
            // the body lags the movement, twice as hard sideways as vertically, and tilts
            // with the horizontal speed. Reusing his numbers rather than inventing some is
            // what makes a caught player look like they belong in there.
            Vector2 lag = new Vector2(-king.velocity.X * 2f, -king.velocity.Y);

            // And the squash frames nudge him, so a landing visibly jolts whoever is
            // inside instead of leaving them serenely centred through it.
            if (king.frame.Y == 120)
                lag.Y += 2f;
            else if (king.frame.Y == 360)
                lag.Y -= 2f;
            else if (king.frame.Y == 480)
                lag.Y -= 6f;

            // Written straight rather than eased. This is a hold — anything that looks
            // like give invites the player to try to fight out of it.
            Player.Center = king.Center + lag;
            Player.velocity = Vector2.Zero;

            Player.fullRotation = king.velocity.X * 0.05f;
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole climb the boss made counts as a fall the moment it lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (++drain < 60)
                return;

            drain = 0;

            // The local player's own hit, the way vanilla deals contact damage; Hurt
            // tells the others. Through armour entirely: this is not a blow to be
            // shrugged off with defence, and at twenty a second a suit of anything
            // turned it into one point, which read as nothing happening at all.
            if (Player.whoAmI == Main.myPlayer)
                Player.Hurt(PlayerDeathReason.ByNPC(HeldBy), DrainPerSecond, 0, dodgeable: false, scalingArmorPenetration: 1f);

            // Stolen, not merely spent.
            king.life = Math.Min(king.lifeMax, king.life + DrainPerSecond);
            king.HealEffect(DrainPerSecond);
        }
    }

    /// <summary>
    /// The King drawn over whoever he is holding.
    ///
    /// Players draw after NPCs, so a held player stood on top of him, which is the
    /// opposite of being inside him. Vanilla's ninja reads as inside because the body
    /// is drawn over the ninja, translucent. This is the same order for the player: the
    /// King's own draw keeps only the ninja (LegendsKingSlime.PreDraw), and this layer,
    /// after the player's, draws his body and crown over them, exactly where and as
    /// vanilla would have. Drawn once, over the player drawn last of those he holds.
    ///
    /// Kept upright: player draw data is turned with the player (the held player tilts
    /// the way the ninja does), and the body must not be.
    /// </summary>
    public class SlimeGrabOverlay : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.BeetleBuff);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f
            && !drawInfo.headOnlyRender
            && drawInfo.drawPlayer.GetModPlayer<SlimeGrabPlayer>().Held;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            int held = player.GetModPlayer<SlimeGrabPlayer>().HeldBy;
            NPC king = Main.npc[held];

            if (!king.TryGetGlobalNPC(out LegendsKingSlime state) || !state.Carriable)
                return;

            if (LegendsKingSlime.Carrier(held) != player.whoAmI)
                return;

            LegendsKingSlime.AddBodyAndCrown(king, state.Posed(), drawInfo.DrawDataCache);
            state.Carried = true;
        }
    }
}
