using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Nothing comes near certain company.
    ///
    /// While the player is stood close to one of the figures registered here, hostile
    /// spawning stops. It is not protection and it does not clear what is already out;
    /// the world simply declines to put anything new down while that thing is standing
    /// there. The player is told by the icon and by nothing else.
    ///
    /// Registered per NPC type, so anyone who ought to have the same weight later is
    /// one Register call away.
    /// </summary>
    public class Stillness : ModBuff
    {
        /// <summary>Twice the distance at which you can speak to one of them.</summary>
        public const float Range = Dialogue.TalkRange * 2f;

        /// <summary>NPC types whose company holds the world off.</summary>
        private static readonly HashSet<int> keepers = new();

        public static void Register(int npcType) => keepers.Add(npcType);

        public static void Clear() => keepers.Clear();

        /// <summary>Is one of them standing within Range of this player.</summary>
        public static bool Near(Player player)
        {
            if (keepers.Count == 0)
                return false;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (keepers.Contains(npc.type) && Vector2.Distance(player.Center, npc.Center) <= Range)
                    return true;
            }

            return false;
        }

        public override void SetStaticDefaults()
        {
            // Not cancellable: it is the world's doing, not the player's.
            Main.debuff[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = false;
        }

        public override void Unload() => Clear();
    }

    /// <summary>Puts the buff on while the company lasts, and takes it straight off after.</summary>
    public class StillnessPlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            if (Stillness.Near(Player))
                Player.AddBuff(ModContent.BuffType<Stillness>(), 2);
        }
    }

    /// <summary>
    /// What the buff does. EditSpawnRate is per player and runs before the spawn roll,
    /// so zeroing the cap here means nothing is placed around that player at all,
    /// while everybody else's world carries on.
    /// </summary>
    public class StillnessSpawns : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!player.HasBuff<Stillness>())
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }
}
