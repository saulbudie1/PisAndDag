using Microsoft.Xna.Framework;
using PisAndDag.Content.Structures;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// How the Abomanation is called: let small things go, on the Wasted Mile, until
    /// it has had enough of them.
    ///
    /// Bunnies, squirrels, red squirrels, released from the hand, on the ground the
    /// siege was fought from. Nothing is crafted, nothing is thrown, nothing explodes.
    /// Each one is taken the moment it touches the ground; the tenth
    /// (<see cref="Abomanation.Offerings"/>) brings it. The count is world state
    /// (PADWorld.MileOfferings), so it survives leaving and coming back, and it is
    /// shared, so two players can feed it between them.
    ///
    /// **Caught by the spawn, not by the item use.** A released critter is an NPC whose
    /// source names the player who let it go, which is the only signal that separates one
    /// you put down from one that was already hopping about out there. Hooking the item
    /// instead would mean either blocking the use and consuming the stack by hand, or
    /// letting the critter out and killing it a tick later; this way the animal never
    /// finishes arriving, and a wild rabbit that wanders onto the Mile is left alone.
    ///
    /// That source is an <see cref="EntitySource_Parent"/> holding the Player, NOT an
    /// <see cref="EntitySource_ItemUse"/>: vanilla releases through NPC.ReleaseNPC, which
    /// asks the player for GetSource_ReleaseEntity, and that hands back a Parent. Testing
    /// for ItemUse matched nothing at all, so bunnies sat on the ash and were ignored.
    /// </summary>
    public class AbomanationSummon : GlobalNPC
    {
        private static bool IsRodent(int type) =>
            type == NPCID.Bunny || type == NPCID.Squirrel || type == NPCID.SquirrelRed;

        /// <summary>The items that make those three, for the Boss Checklist entry.</summary>
        public static readonly int[] Offerings =
        {
            ItemID.Bunny,
            ItemID.Squirrel,
            ItemID.SquirrelRed,
        };

        /// <summary>
        /// Whether this world position is on the Mile.
        ///
        /// Guarded on the Grand world, because the Mile is a band of THIS mod's layout and
        /// on any other world size those tile numbers are somebody else's ground — usually
        /// off the edge of the map entirely.
        /// </summary>
        public static bool OnTheMile(Vector2 where)
        {
            if (!ExtraLargeWorld.Active)
                return false;

            int x = (int)(where.X / 16f);
            (int left, int right) = Layout.Mile;

            return x >= left && x <= right;
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (!IsRodent(npc.type))
                return;

            // Let go by hand, not born out there.
            if (!LetGo(source, out Player player))
                return;

            if (!OnTheMile(npc.Center))
                return;

            if (NPC.AnyNPCs(ModContent.NPCType<AbomanationHead>()))
                return;

            // The critter never lands.
            npc.active = false;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            PADWorld.MileOfferings++;

            if (PADWorld.MileOfferings < Abomanation.Offerings)
            {
                // Taken. The ground says so, and nothing else does.
                Taken(npc.Center);
                Sync();
                return;
            }

            PADWorld.MileOfferings = 0;
            Sync();
            Call(player);
        }

        /// <summary>
        /// The player who released this animal, if a player released it.
        ///
        /// One test covers every shape it arrives in. Vanilla's own release hands back an
        /// EntitySource_Parent holding the player, and EntitySource_ItemUse — which a mod
        /// handing out critters might use instead — DERIVES from it: its Player property
        /// is that same Entity cast. So matching the base catches both, and matching them
        /// separately does not compile, because the first arm already swallowed the
        /// second.
        /// </summary>
        private static bool LetGo(IEntitySource source, out Player player)
        {
            player = source is EntitySource_Parent parent ? parent.Entity as Player : null;

            return player != null;
        }

        /// <summary>
        /// What one taken animal looks like: the sand going down where it stood. Enough
        /// that a player who let a bunny go on the wrong ground can tell the difference.
        /// </summary>
        private static void Taken(Vector2 where)
        {
            SoundEngine.PlaySound(SoundID.WormDig, where);

            if (Main.dedServ)
                return;

            for (int i = 0; i < 14; i++)
            {
                Dust dust = Dust.NewDustDirect(where - new Vector2(12f, 8f), 24, 16, DustID.Ash,
                    Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-2.6f, -0.6f), 90, default, 1.3f);

                dust.noGravity = false;
            }
        }

        /// <summary>The count is world data; joining clients get it with the rest, and everyone else needs telling now.</summary>
        private static void Sync()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>
        /// Calls it. Not up under the offering: the offering is what it heard, and it
        /// comes from a long way off along the Mile, the way AbomanationHead.Summon does
        /// it, camera and all. The critter's own position only matters for the check
        /// above.
        /// </summary>
        private static void Call(Player player)
        {
            AbomanationHead.Summon(player, new EntitySource_BossSpawn(player, "Abomanation"));
        }
    }
}
