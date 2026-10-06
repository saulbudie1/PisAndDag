using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Legends;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Skeletron's hands, parried. Parts of a boss, and so never parried by the rules
    /// (ParryRules.BossLimbs): the mod speaks for them itself (Parry.AddNPC), which is asked
    /// before the rules are, as for the Golem's fists.
    ///
    /// WHEN. Only a hand that is being swung at somebody.
    ///
    ///   Vanilla's two swings, in flight (NPC.AI, its style 12, read from the 1.4.4 assembly;
    ///   the state is NPC.ai[2]): brought down at a player from over his head (2), and swept
    ///   in at one from out to his side (5). And after its AI calls a swing over, for as
    ///   long as the hand is still flying: vanilla ends the first the tick the hand's top is
    ///   under its target's, and the second the tick its middle passes theirs, with the hand
    ///   still going through them at the swing's speed. Hanging by him after (0 or 3) it is
    ///   reined in from the next tick if that speed takes it away from its place by him, and
    ///   only once it is past that place if it takes it there. So a hand hanging by him
    ///   that is faster than that state lets one leave its place (<see cref="SwingSpeed"/>)
    ///   is taken as swung, whatever sent it flying. Not one hanging by him otherwise (or
    ///   lying on a player it has just struck: walked into, not swung), drawn back for a
    ///   swing (1 and 4), or held up while he spins.
    ///
    ///   In Legends, his two moves that are theirs, in their blow: the clap as the hands
    ///   shut, the smash as they come down (LegendsSkeletronHand.Striking). Not on their way
    ///   to their places, waiting there, or after.
    ///
    /// WHAT. The hand takes the parry's blow as anything does, and then a fifth of its own
    /// full life, whatever its armour (<see cref="LifeShare"/>: five parries break a hand).
    /// It is thrown back the way it came, and its swing is over: vanilla's own state for
    /// after it, or, in a move of Legends, no further part in that move. It touches nobody
    /// for a while after (as long as the one who parried it is spared it), or until that
    /// move is over.
    ///
    /// The clap and the smash are both hands' as one, and a parry faces one way: with one of
    /// the two turned aside the blow is broken, and the other is thrown back with it (it
    /// loses nothing). On the machine of whoever parried, the other cannot touch them from
    /// that tick, whatever the server has yet to say (LegendsSkeletronHand.CanHitPlayer).
    ///
    /// Never staggered, and so never open to the riposte; not pushed: it is thrown back
    /// instead.
    ///
    /// Decided where the world is (the server, or the one machine there is) and sent with
    /// the hand; what is seen and heard is each machine's own, the first tick it sees a hand
    /// turned. A server is told of a parry by the machine that made it, of a swing as that
    /// machine saw it, which is the server's hand of a little while before; and a swing is
    /// met at its end. So the server takes that word for a swing that is over where it has
    /// it, if it was only just over (<see cref="Late"/>).
    /// </summary>
    public class SkeletronHandParry : GlobalNPC
    {
        /// <summary>What a parry costs the hand: its full life over this.</summary>
        public const int LifeShare = 5;

        /// <summary>The hand's AI states (NPC.ai[2]): the swing down and what follows it, the sweep and what follows it.</summary>
        private const float Slam = 2f;
        private const float AfterSlam = 3f;
        private const float Sweep = 5f;
        private const float AfterSweep = 0f;

        /// <summary>
        /// Faster than this, in pixels a tick, a hand hanging by him is taken as swung: still
        /// in the swing its AI has just called over, which leaves it at 17 or more, or flying
        /// back to its place by him as fast as that. That state holds what takes a hand away
        /// from its place to 8 across and 3 up or down (6 while he spins), 10 together at the
        /// most, from its first tick; what takes it toward its place it only adds to.
        /// </summary>
        private const float SwingSpeed = 11f;

        /// <summary>Thrown back: pixels a tick at first, what is kept of that each tick, and for how many ticks. 140 pixels in all.</summary>
        private const float ThrowSpeed = 15f;
        private const float ThrowKeep = 0.9f;
        private const int ThrowTicks = 26;

        /// <summary>Ticks a parried hand touches nobody, when no move of Legends had it: as long as the one who parried it is spared it.</summary>
        private const int Harmless = ParryPlayer.Spare;

        /// <summary>
        /// Ticks after a swing is over on a server that it still takes a player's word for a
        /// parry of it: half a second, for the word's way there and the hand's way to that
        /// player's machine before it. Less than <see cref="Harmless"/>, so no swing is ever
        /// paid for twice.
        /// </summary>
        private const int Late = 30;

        private const string Told = "a parry strikes it, takes a fifth of its full life from it and throws it back. Only while it is swung at you. In the clap and the smash the other hand is thrown back with it. Never staggered.";

        // ---- decided where the world is, and sent ------------------------------------------------

        /// <summary>This tick's step of being thrown back, and the ticks of it left.</summary>
        private Vector2 thrown;
        private int throwTicks;

        /// <summary>Ticks left of touching nobody.</summary>
        private int harmless;

        /// <summary>A move of Legends had it when it was turned: it has no further part in that move. Which move (the head's count of them).</summary>
        private bool spent;
        private int spentIn = -1;

        // ---- each machine's own --------------------------------------------------------------------

        /// <summary>It was turned last tick, as this machine saw it.</summary>
        private bool wasTurned;

        /// <summary>A server's own: ticks since it last had this hand in a swing.</summary>
        private int sinceSwung = Late + 1;

        /// <summary>Turned aside by a parry, and not yet over it: it touches nobody, and cannot be parried again.</summary>
        public bool Turned => harmless > 0 || spent;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronHand;

        public override void SetStaticDefaults()
        {
            Parry.AddNPC(NPCID.SkeletronHand, CanBeTurned, Thrown, reels: false, told: Told);
        }

        /// <summary>Whether this hand was parried out of the move of Legends that has it: that move does not move it (LegendsSkeletronHand).</summary>
        internal static bool Spent(NPC npc) => npc.TryGetGlobalNPC(out SkeletronHandParry hand) && hand.spent;

        /// <summary>
        /// Only a hand that is being swung at somebody, and not one a parry has just turned.
        /// On a server, which is only ever asked by a player's word of a parry already made:
        /// one that was being swung no longer ago than that word may be late.
        /// </summary>
        private static bool CanBeTurned(NPC npc)
        {
            if (!npc.TryGetGlobalNPC(out SkeletronHandParry hand) || hand.Turned)
                return false;

            // Its head gone, it is going too.
            if (LegendsSkeletronHand.HeadOf(npc) == null)
                return false;

            if (Main.netMode == NetmodeID.Server)
                return hand.sinceSwung <= Late;

            return Swung(npc);
        }

        /// <summary>Being swung at somebody, as this machine has it now.</summary>
        private static bool Swung(NPC npc)
        {
            // A move of Legends has it: its own AI is not run, and what it left in ai[2] says nothing.
            if (LegendsSkeletronHand.InMove(npc, out _))
                return LegendsSkeletronHand.Striking(npc);

            float state = npc.ai[2];

            if (state == Slam || state == Sweep)
                return true;

            return (state == AfterSlam || state == AfterSweep) && npc.velocity.LengthSquared() > SwingSpeed * SwingSpeed;
        }

        /// <summary>
        /// Parried, where it is simulated: its swing over, thrown back, a fifth of its life
        /// gone. In a move that is both hands', the other goes back with it.
        /// </summary>
        private static void Thrown(NPC npc, Player by, Vector2 away, bool staggers)
        {
            if (!npc.TryGetGlobalNPC(out SkeletronHandParry hand))
                return;

            bool move = LegendsSkeletronHand.InMove(npc, out int serial);

            if (move)
            {
                LegendsSkeletronHand.Break(npc);

                // The blow is both hands': broken for one, it is broken for the other. Told to
                // everyone before this one's loss is, in case that loss is its last.
                NPC other = LegendsSkeletronHand.Sibling(npc);

                if (other != null && other.TryGetGlobalNPC(out SkeletronHandParry second) && !second.spent)
                {
                    second.Throw(other, (other.Center - by.Center).SafeNormalize(away), true, serial);

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, other.whoAmI);
                }
            }
            else if (npc.ai[2] == Slam)
            {
                // Vanilla's own state for after each swing: hanging by him, his count to the next left as it was.
                npc.ai[2] = AfterSlam;
            }
            else if (npc.ai[2] == Sweep)
            {
                npc.ai[2] = AfterSweep;
            }

            hand.Throw(npc, away, move, serial);

            // Whoever parried it has had a hand in him.
            npc.PlayerInteraction(by.whoAmI);

            Parry.Take(npc, Math.Max(1, npc.lifeMax / LifeShare), away.X < 0f ? -1 : 1);
        }

        /// <summary>Thrown back this way from this tick, and harmless: for a while, or for the rest of the move that has it.</summary>
        private void Throw(NPC npc, Vector2 away, bool move, int serial)
        {
            thrown = away.SafeNormalize(-Vector2.UnitY) * ThrowSpeed;
            throwTicks = ThrowTicks;
            harmless = Harmless;
            spent = move;
            spentIn = move ? serial : -1;
            sinceSwung = Late + 1;

            npc.velocity = thrown;
            npc.netUpdate = true;
        }

        public override void PostAI(NPC npc)
        {
            // The move it was parried out of is over (or its head is gone): vanilla has it again.
            if (spent && !(LegendsSkeletronHand.InMove(npc, out int serial) && serial == spentIn))
            {
                spent = false;
                spentIn = -1;
            }

            if (harmless > 0)
                harmless--;

            if (throwTicks > 0)
            {
                // Over whatever its own AI gave it this tick: thrown, and slowing.
                throwTicks--;
                npc.velocity = thrown;
                thrown *= ThrowKeep;
            }
            else if (spent)
            {
                // Out of the move, it hangs where it was thrown to until the move is over.
                npc.velocity = Vector2.Zero;
            }

            bool turned = Turned;

            if (turned && !wasTurned)
                Chipped(npc);

            wasTurned = turned;

            // A server's count of how long ago it had this hand in a swing (CanBeTurned). Thrown
            // back it is fast in a state that hangs it by him, and that is no swing.
            if (Main.netMode == NetmodeID.Server)
            {
                if (!turned && Swung(npc))
                    sinceSwung = 0;
                else if (sinceSwung <= Late)
                    sinceSwung++;
            }
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !Turned;

        public override bool CanHitNPC(NPC npc, NPC target) => !Turned;

        /// <summary>The first tick this machine sees it turned: bone knocked off it, the way it is thrown.</summary>
        private void Chipped(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = npc.Center;
            Vector2 way = thrown.SafeNormalize(-Vector2.UnitY);

            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = 0.3f, PitchVariance = 0.1f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.DD2_SkeletonHurt with { Pitch = -0.2f, Volume = 0.7f }, at);

            PADParticleHandler.SpawnParticle(new ParryRing(at, 8f, 84f, 16, ParryEffects.Steel));

            for (int i = 0; i < 18; i++)
            {
                Vector2 speed = way.RotatedByRandom(1.1f) * Main.rand.NextFloat(2f, 8f);

                Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(npc.width * 0.35f, npc.height * 0.35f), DustID.Bone, speed, 0, default,
                    Main.rand.NextFloat(1f, 1.6f));
            }
        }

        // ---- sent with it --------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(harmless > 0);
            bitWriter.WriteBit(spent);
            bitWriter.WriteBit(throwTicks > 0);

            if (harmless > 0)
                binaryWriter.Write((byte)Math.Min(harmless, byte.MaxValue));

            if (spent)
                binaryWriter.Write(spentIn);

            if (throwTicks > 0)
            {
                binaryWriter.Write((byte)Math.Min(throwTicks, byte.MaxValue));
                binaryWriter.Write(thrown.X);
                binaryWriter.Write(thrown.Y);
            }
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool quiet = bitReader.ReadBit();
            spent = bitReader.ReadBit();
            bool flung = bitReader.ReadBit();

            harmless = quiet ? binaryReader.ReadByte() : 0;
            spentIn = spent ? binaryReader.ReadInt32() : -1;

            if (flung)
            {
                throwTicks = binaryReader.ReadByte();
                thrown = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            }
            else
            {
                throwTicks = 0;
            }
        }
    }
}
