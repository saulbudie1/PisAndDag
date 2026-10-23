using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Favor of an Empress: what the Empress gives whoever is in her arena, by request, "allows endless
    /// flight and override custom flight speed of player now to flight speed of solar wings. lasts until
    /// either player die or player beats boss."
    ///
    /// Flight before it is earned, and without end: whatever is worn, the player flies as the solar
    /// wings fly, the best wings there are (their stats read at run time, so a mod that rewrites them is
    /// followed), and never runs out. Not trimmed as worn wings are (PisPlayer), and nothing a worn pair
    /// says about its own speed counts while it lasts. One with no wings on is lent hers to be seen with.
    ///
    /// Given and kept by her arena (EmpressArenaPlayer) for as long as she is there; gone with a death,
    /// as anything not kept is, and the moment she is beaten (her border gone). One it ends on in the
    /// air is let down gently, slowly and unharmed (EmpressArenaPlayer: landing), not left the flight.
    /// </summary>
    public class FavorOfAnEmpress : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        /// <summary>
        /// The flight, for a player with the favour: called after everything worn has had its say
        /// (PisPlayer.PostUpdateMiscEffects), and before anybody is moved by it.
        /// </summary>
        public static void Fly(Player player)
        {
            // The solar wings, as far as moving goes: their speeds along and up, their own way of rising.
            player.wingsLogic = ArmorIDs.Wing.SolarWings;

            // Nothing a worn pair says about itself: a pair of the mods' own would set its own speeds over these.
            player.equippedWings = null;

            WingStats stats = player.GetWingStats(ArmorIDs.Wing.SolarWings);
            player.wingTimeMax = Math.Max(1, stats.FlyTime);

            // Never running out.
            player.wingTime = player.wingTimeMax;
            player.noFallDmg = true;

            // Hers, to be seen with, for one with none of their own on.
            if (player.wings <= 0)
                player.wings = ArmorIDs.Wing.RainbowWings;
        }

        /// <summary>Her colours, a little, off whoever is flying in her favour.</summary>
        public override void Update(Player player, ref int buffIndex)
        {
            if (Main.dedServ || player.velocity.Y == 0f || !Main.rand.NextBool(4))
                return;

            Dust dust = Dust.NewDustPerfect(player.Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-6f, 14f)), DustID.RainbowMk2,
                -player.velocity * 0.15f + new Vector2(0f, 0.5f), 0, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f), 0.8f);
            dust.noGravity = true;
            dust.fadeIn = 0.9f;
        }
    }
}
