using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Which vanilla bosses animate their own arrival, so the camera side of BossIntro
    /// stays out of it. The bar and the name are universal and need nothing here.
    ///
    /// Mod bosses do this from their own SetDefaults (NPC.PAD().OwnIntro); this file is
    /// for the ones whose SetDefaults is not ours. The Wall's entry lives with its
    /// profile in WallOfFleshIntro.
    /// </summary>
    public class BossRoster : ModSystem
    {
        public override void PostSetupContent()
        {
            // The Legends King runs a rite — heralds gather, the camera commits, he
            // swells up out of a seed. Outside Legends he is vanilla's King Slime and
            // gets the universal arrival like anything else.
            BossPlate.HasOwnIntro(NPCID.KingSlime, _ => PADWorld.Legends);
        }
    }
}
