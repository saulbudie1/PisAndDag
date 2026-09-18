using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// The Archmage's second crime, enforced.
    ///
    /// §XI [LOCKED]: he did not hide Shimmer. He could not have — it is a substance and
    /// there is a great deal of it. He locked what it *does*. So every pool and seam in
    /// the world stays exactly where it is and looks exactly as it looks, and nothing that
    /// goes into one comes out as anything else. A man can carry a jug of it across a room.
    /// Before, a man carrying a jug of it across a room would have been taking a risk with
    /// what he was.
    ///
    /// Three things are switched off while <see cref="PADWorld.ArchmageShimmer"/> is false,
    /// and they are the three things Shimmer is for:
    ///
    ///   items    do not transmute and do not decraft
    ///   NPCs     do not turn into their other selves, and do not fade out trying
    ///   players  do not take the bath, and do not fall through the world
    ///
    /// Everything else is left alone on purpose. It still glitters, it still counts as a
    /// biome, the Aether is still down there, and you can still swim in it. What is missing
    /// is not the liquid, it is the question. Nobody in the world can tell the difference,
    /// because nobody alive has seen it work.
    ///
    /// **Where the effect actually lives.** Being in shimmer applies BuffID.Shimmer, and
    /// the buff is what sets <c>shimmering</c> on both players and NPCs — Player.UpdateBuffs
    /// and NPC.UpdateNPC_BuffSetFlags are the only things that write it. So the buff is the
    /// switch, and everything downstream of it (the fade, the fall through the world, the
    /// transformation at the end of the fade) turns off with it, in vanilla's own order.
    ///
    /// Blocking only the transformation is NOT enough, and it is worth writing down why:
    /// the fade runs first and the swap happens at the end of it, so stopping the swap
    /// alone leaves the NPC faded to nothing, standing there, invisible and unable to be
    /// talked to but still alive and still fighting. That was the first attempt at this
    /// file. The buff is the right height to cut at.
    ///
    /// tModLoader 1.4.4 has no CanShimmer hook on ModItem or GlobalItem — those arrived
    /// later — so the item half goes through the generated detours, same as the world gen
    /// brackets.
    /// </summary>
    public class ShimmerLock : ModSystem
    {
        /// <summary>True while the world is under the lock — the usual state.</summary>
        public static bool Locked => !PADWorld.ArchmageShimmer;

        public override void Load()
        {
            On_Item.CanShimmer += NothingGoesIn;
            On_NPC.GetShimmered += NothingComesOut;
        }

        /// <summary>
        /// The one gate every item transmutation and every shimmer decrafting recipe has
        /// to pass. Answering no here is enough for all of them at once.
        /// </summary>
        private static bool NothingGoesIn(On_Item.orig_CanShimmer orig, Item self)
            => !Locked && orig(self);

        /// <summary>
        /// Belt and braces. With the buff gone nothing should ever reach this, but an NPC
        /// swapped for another by some path nobody has thought of yet would be a bug that
        /// looks like lore, and those are the worst kind to find later.
        /// </summary>
        private static void NothingComesOut(On_NPC.orig_GetShimmered orig, NPC self)
        {
            if (Locked)
                return;

            orig(self);
        }
    }

    /// <summary>
    /// The player's half. PreUpdateBuffs runs before the buff array is walked, so the
    /// Shimmer buff is gone before anything reads it — which means <c>shimmering</c> is
    /// never set, the movement code never swaps to ShimmerCollision, and the player walks
    /// out of the pool wet and on solid ground.
    /// </summary>
    public class ShimmerLockPlayer : ModPlayer
    {
        public override void PreUpdateBuffs()
        {
            if (!ShimmerLock.Locked)
                return;

            Player.ClearBuff(BuffID.Shimmer);

            // Cleared as well as un-buffed, so a player who was already half faded when
            // the lock came down comes back rather than staying a ghost.
            Player.shimmering = false;
            Player.timeShimmering = 0;
            Player.shimmerTransparency = 0f;
        }
    }

    /// <summary>
    /// The NPCs' half, and the fix for the thing that went wrong the first time: a
    /// transformable NPC that walked into a pool faded all the way out and then stayed
    /// there, invisible, unable to be talked to, still alive and still swinging at whatever
    /// was hostile. Nothing was transforming it, but nothing was stopping the fade either.
    ///
    /// Running in PreAI clears the buff before the NPC's own update reads it, and zeroing
    /// the transparency brings back anything that is already part way gone — including the
    /// ones stranded by the earlier version of this file.
    /// </summary>
    public class ShimmerLockNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (!ShimmerLock.Locked)
                return true;

            int buff = npc.FindBuffIndex(BuffID.Shimmer);

            if (buff >= 0)
                npc.DelBuff(buff);

            npc.shimmering = false;
            npc.shimmerTransparency = 0f;

            return true;
        }
    }
}
