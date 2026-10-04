using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Stagger. Terraria has no such thing, so this is it: while the timer runs the NPC's
    /// own AI does not, and it slides backwards a fixed distance whatever it is.
    ///
    /// Written for the Greatbow of a Pilgrim (PAD_Item_Plan.md) and deliberately kept
    /// general — anything in the mod that wants to stop something for a moment calls
    /// Stagger.
    ///
    /// The slide is a slide, not knockback. Every boss in the game resists knockback, and
    /// "even the Wall of Flesh goes backwards" is the whole point of the weapon, so the
    /// position is written directly and spread evenly over the stagger so it reads as
    /// being pushed rather than punted.
    ///
    /// THE PARRY'S (<see cref="Reel"/>, <see cref="Push"/>). A second stagger, the one a
    /// parry gives, apart from the Greatbow's and not the same thing:
    ///
    ///   Its AI is stopped, not replaced. For as long as it reels its own AI is not run
    ///   (PreAI says no), and in its place it slumps: it slows to a stop, falls if it was in
    ///   the air and has ground to land on, hangs if it has none. Everything its AI keeps
    ///   (ai, localAI, its own fields, its timers) is left exactly as it was, so when the
    ///   time is up it goes on from where it stopped. Nothing of it is changed that would
    ///   have to be put back.
    ///
    ///   It touches nobody while it reels (CanHitPlayer, CanHitNPC).
    ///
    ///   The first hit on it is a riposte: three times the damage it would have done, four
    ///   times the knockback, never a critical one (ModifyIncomingHit), and it wakes at once
    ///   (HitEffect: its AI runs again the next tick, with the knockback on it). A burn or a
    ///   poison is not a hit. An enemy with a state of its own for being staggered (a flying
    ///   weapon's spin, 'Dame' Quilt's reel) is not stopped, only open to the riposte.
    ///
    ///   Every parry pushes back (<see cref="Push"/>): written into its position like the
    ///   slide, so nothing resists it, but as a shove that dies away, and stopped by walls.
    ///
    /// The server's word, sent with the NPC; each machine counts the ticks down for itself.
    /// </summary>
    public class PADStaggerNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>
        /// Anything whose AI cannot survive being skipped. A worm segment that misses a
        /// tick loses its chain; a teleporter frozen mid-teleport never arrives.
        ///
        /// This is a starting list and it is expected to grow. Nothing tests itself.
        /// </summary>
        private static readonly HashSet<int> NeverStagger = new()
        {
            NPCID.EaterofWorldsHead, NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.TheDestroyer, NPCID.TheDestroyerBody, NPCID.TheDestroyerTail,
            NPCID.DiggerHead, NPCID.DiggerBody, NPCID.DiggerTail,
            NPCID.WyvernHead, NPCID.WyvernBody, NPCID.WyvernBody2,
            NPCID.WyvernBody3, NPCID.WyvernLegs, NPCID.WyvernTail,
            NPCID.DukeFishron,          // phase transitions are timer driven
            NPCID.MoonLordCore, NPCID.MoonLordHand, NPCID.MoonLordHead,
        };

        /// <summary>The push: over this many ticks, each tick's step this much of the last.</summary>
        public const int PushTicks = 24;
        public const float PushDecay = 0.9f;

        /// <summary>What a reeling thing loses of its speed each tick, and how it falls: the game's own gravity for an NPC, and its fastest fall.</summary>
        private const float SlumpDrag = 0.86f;
        private const float SlumpGravity = 0.3f;
        private const float SlumpFall = 10f;

        /// <summary>
        /// Ticks a client that has just seen a riposte land takes no word that the thing is
        /// still reeling: the server's word that it woke is on its way, and anything that left
        /// before it is old.
        /// </summary>
        private const int DeafTicks = 20;

        // ---- the Greatbow's ---------------------------------------------------------------------

        private int staggerTicks;
        private int slideTicks;
        private Vector2 slidePerTick;

        // ---- the parry's --------------------------------------------------------------------------

        /// <summary>Ticks left of a parry's stagger. While there are any, the next hit is a riposte.</summary>
        private int reelTicks;

        /// <summary>Its own AI is stopped for it. Not so for one that has a state of its own for this.</summary>
        private bool held;

        /// <summary>Parries it has taken toward its next stagger (Parry.StaggersEvery). Where the world is decided only.</summary>
        private byte parries;

        /// <summary>The push: this tick's step, and the ticks of it left.</summary>
        private Vector2 push;
        private int pushTicks;

        // ---- each machine's own ---------------------------------------------------------------------

        /// <summary>Ticks it has reeled here (the glints' clock), and whether it was reeling last tick.</summary>
        private int clock;
        private bool wasReeling;

        private int deaf;

        /// <summary>The update a riposte landed on it, for the one who landed it (<see cref="Punch"/>).</summary>
        private uint ripostedAt = uint.MaxValue;

        public bool Staggered => staggerTicks > 0;

        /// <summary>Reeling from a parry: the next hit on it is a riposte.</summary>
        public bool Reeling => reelTicks > 0;

        /// <summary>Stop it. Refreshes rather than stacks.</summary>
        public void Stagger(NPC npc, int ticks)
        {
            if (ticks <= 0 || NeverStagger.Contains(npc.type))
                return;

            staggerTicks = Math.Max(staggerTicks, ticks);
            npc.netUpdate = true;
        }

        /// <summary>Push it, evenly, over <paramref name="ticks"/>.</summary>
        public void Slide(Vector2 direction, float pixels, int ticks)
        {
            if (pixels <= 0f || ticks <= 0)
                return;

            slideTicks = ticks;
            slidePerTick = direction.SafeNormalize(Vector2.UnitX) * (pixels / ticks);
        }

        // ---- the parry's: asked where the world is decided ------------------------------------------

        /// <summary>
        /// Whether a parry may stop this one's AI: nothing on the list above, none of the many
        /// parts of something, nothing that moves as a worm does, and nothing the config or
        /// another mod has said is never to be.
        /// </summary>
        public static bool CanReel(NPC npc)
        {
            return !NeverStagger.Contains(npc.type) && ParryRules.CanBeStopped(npc) && !Parry.NeverStaggers(npc.type);
        }

        /// <summary>
        /// Staggered by a parry, for this long. <paramref name="hold"/>: its own AI is stopped
        /// for it; false for one that has a state of its own for this and only needs to be
        /// open to the riposte. The count toward it starts over.
        /// </summary>
        public void Reel(NPC npc, int ticks, bool hold)
        {
            if (ticks <= 0)
                return;

            reelTicks = Math.Max(reelTicks, ticks);
            held = hold;
            parries = 0;
            npc.netUpdate = true;
        }

        /// <summary>
        /// One more parry taken toward a stagger, by one that takes <paramref name="needed"/>
        /// of them. True, and the count starts over, when that makes enough. The count does
        /// not run down between them.
        /// </summary>
        public bool TakeParry(int needed)
        {
            parries++;

            if (parries < needed)
                return false;

            parries = 0;
            return true;
        }

        /// <summary>
        /// Pushed back this far, this way, by a parry: hard at first and dying away over
        /// <see cref="PushTicks"/>. Something that stands on the ground goes along it. What
        /// speed it had the other way is gone: it was stopped, and the push is all that moves
        /// it (left, a charge would carry it most of the way back through the push).
        /// </summary>
        public void Push(NPC npc, Vector2 direction, float pixels)
        {
            if (pixels <= 0f)
                return;

            Vector2 way = direction.SafeNormalize(Vector2.UnitX);

            if (!npc.noGravity)
                way = new Vector2(way.X < 0f ? -1f : 1f, 0f);

            float against = Vector2.Dot(npc.velocity, way);

            if (against < 0f)
                npc.velocity -= way * against;

            // Each step is PushDecay of the one before, and all of them add up to the pixels asked.
            float first = pixels * (1f - PushDecay) / (1f - (float)Math.Pow(PushDecay, PushTicks));

            push = way * first;
            pushTicks = PushTicks;
            npc.netUpdate = true;
        }

        // ---- every tick -------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (staggerTicks > 0)
            {
                npc.velocity = Vector2.Zero;
                return false;   // its AI does not run at all this tick
            }

            if (reelTicks > 0 && held)
            {
                Slump(npc);
                return false;   // nor here: stopped where it is, to go on from there
            }

            return true;
        }

        /// <summary>
        /// What it does in place of its own AI while it reels: slows to a stop. The game drops
        /// anything that has weight after this (NPC.UpdateNPC adds the fall once the AI has
        /// run); what flies has none, so it is dropped here. What goes through tiles has
        /// nothing to land on, and hangs where it is: a flyer slows, and one with weight is
        /// held up against the fall the game is about to add (left to it, it would drop out
        /// of the world for as long as it reeled).
        /// </summary>
        private static void Slump(NPC npc)
        {
            npc.velocity.X *= SlumpDrag;

            if (Math.Abs(npc.velocity.X) < 0.05f)
                npc.velocity.X = 0f;

            if (!npc.noGravity)
            {
                if (npc.noTileCollide)
                    npc.velocity.Y = -npc.gravity;

                return;
            }

            if (npc.noTileCollide)
                npc.velocity.Y *= SlumpDrag;
            else
                npc.velocity.Y = Math.Min(npc.velocity.Y + SlumpGravity, SlumpFall);
        }

        public override void PostAI(NPC npc)
        {
            if (staggerTicks > 0)
                staggerTicks--;

            if (slideTicks > 0)
            {
                slideTicks--;

                // Written straight into position. The Wall of Flesh re-derives its X from
                // its own AI every tick, which is exactly why this has to be applied while
                // that AI is skipped — it resumes from wherever it has been put.
                npc.position += slidePerTick;
            }

            if (pushTicks > 0)
                Shove(npc);

            if (deaf > 0)
                deaf--;

            bool reeling = reelTicks > 0;

            if (reeling)
            {
                // The first tick this machine sees of it.
                if (!wasReeling)
                {
                    clock = 0;
                    ParryEffects.Break(npc);
                }

                ParryEffects.Tell(npc, clock);

                clock++;
                reelTicks--;
            }

            wasReeling = reeling;
        }

        /// <summary>This tick's step of the push: as far as the tiles let it go, unless it goes through them anyway.</summary>
        private void Shove(NPC npc)
        {
            pushTicks--;

            Vector2 step = push;
            push *= PushDecay;

            if (!npc.noTileCollide)
                step = Collision.TileCollision(npc.position, step, npc.width, npc.height, true, true);

            npc.position += step;

            ParryEffects.Skid(npc, step);
        }

        // ---- while it reels ---------------------------------------------------------------------------

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !(reelTicks > 0 && held);

        public override bool CanHitNPC(NPC npc, NPC target) => !(reelTicks > 0 && held);

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (reelTicks > 0)
                drawColor = ParryEffects.Tint(drawColor, clock);
        }

        /// <summary>The one whose life this one's is: itself, or the part of a many-part thing that holds the life of all of them (NPC.realLife).</summary>
        private static NPC Whole(NPC npc)
        {
            int whole = npc.realLife;

            if (whole < 0 || whole >= Main.maxNPCs || whole == npc.whoAmI)
                return npc;

            NPC other = Main.npc[whole];
            return other != null && other.active ? other : npc;
        }

        /// <summary>
        /// The riposte. Asked wherever a hit is worked out (the machine of whoever lands it),
        /// before it lands: three times what it would have done, four times the knockback, no
        /// critical hit. A hit on one part of a many-part thing is a hit on the whole.
        /// </summary>
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            NPC whole = Whole(npc);

            if (!whole.GetGlobalNPC<PADStaggerNPC>().Reeling)
                return;

            modifiers.FinalDamage *= Parry.RiposteDamage;
            modifiers.Knockback *= Parry.RiposteKnockback;
            modifiers.DisableCrit();
        }

        /// <summary>
        /// A hit has landed (NPC.StrikeNPC calls this on every machine the hit is done on,
        /// after the life and the knockback). If it was reeling, that was the riposte: it
        /// wakes, here and now.
        /// </summary>
        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            NPC whole = Whole(npc);
            PADStaggerNPC state = whole.GetGlobalNPC<PADStaggerNPC>();

            if (state.reelTicks <= 0)
                return;

            state.reelTicks = 0;
            state.ripostedAt = Main.GameUpdateCount;
            ripostedAt = Main.GameUpdateCount;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                state.deaf = DeafTicks;
            else
                whole.netUpdate = true;

            ParryEffects.Riposte(npc, hit.HitDirection);
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            Punch(player);
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.npcProj || projectile.trap || projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
                return;

            Punch(Main.player[projectile.owner]);
        }

        /// <summary>The hit that has just landed was this machine's player's, and it was a riposte: theirs to feel.</summary>
        private void Punch(Player player)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer || ripostedAt != Main.GameUpdateCount)
                return;

            ripostedAt = uint.MaxValue;
            player.GetModPlayer<ParryPlayer>().Punch();
        }

        // ---- sent with it -------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(staggerTicks > 0);
            bitWriter.WriteBit(reelTicks > 0);
            bitWriter.WriteBit(held);
            bitWriter.WriteBit(pushTicks > 0);

            if (staggerTicks > 0)
                binaryWriter.Write((short)staggerTicks);

            if (reelTicks > 0)
                binaryWriter.Write((short)reelTicks);

            if (pushTicks > 0)
            {
                binaryWriter.Write((byte)pushTicks);
                binaryWriter.Write(push.X);
                binaryWriter.Write(push.Y);
            }
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            bool staggered = bitReader.ReadBit();
            bool reeling = bitReader.ReadBit();
            held = bitReader.ReadBit();
            bool pushed = bitReader.ReadBit();

            staggerTicks = staggered ? binaryReader.ReadInt16() : 0;

            int reel = reeling ? binaryReader.ReadInt16() : 0;

            // The riposte this machine has just seen land is newer than this word.
            reelTicks = deaf > 0 ? 0 : reel;

            if (pushed)
            {
                pushTicks = binaryReader.ReadByte();
                push = new Vector2(binaryReader.ReadSingle(), binaryReader.ReadSingle());
            }
            else
            {
                pushTicks = 0;
            }
        }
    }
}
