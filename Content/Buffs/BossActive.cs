using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Boss Active: what a player is under for as long as they are inside a boss's box
    /// (BossArena, which puts it on and keeps it on, and takes it off by no longer doing so).
    ///
    /// Under it nothing is built and nothing is broken: vanilla's own flag for that
    /// (Player.noBuilding, the one the Old One's Army's Creative Shock sets), which stops
    /// blocks and walls being placed, the pick, the axe, the hammer, buckets and the drill.
    /// It does not stop explosives, which no player flag does: the box does that itself, for
    /// everything inside it (BossArenaTiles). The walls that keep the player in are the box's
    /// too; the icon is how the player is told that any of it is going on.
    ///
    /// Not the player's to take off, not saved, and no clock on it: it lasts as long as the
    /// fight does.
    /// </summary>
    public class BossActive : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = false;

            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.noBuilding = true;
        }
    }
}
