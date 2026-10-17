using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// A player's top speed and how much of it they keep, by request, for the HEATR: "slowed down
    /// by 15% (even jump speed). your top speed. Create a void for top speed calculator. to do
    /// this".
    ///
    /// Declared, as the mod's effects are: whatever slows its wearer calls <see cref="Slow"/> every
    /// tick it should, from its UpdateAccessory, UpdateEquip or a buff's Update, with the share of
    /// the speed it takes (0.15 is 15%). Two of them multiply: 15% and 10% leave 76.5%. Nothing is
    /// kept from one tick to the next, so taking it off is what ends it. What is kept is put on the
    /// speeds where the game has settled each one and before it uses it (Player.Update, 1.4.4):
    ///   - running: the top speed on foot and on boots (Player.maxRunSpeed, accRunSpeed), the
    ///     mount's when riding, as the game puts that in the same two just before
    ///     (<see cref="TopSpeedPlayer.PostUpdateRunSpeeds"/>). How fast it is reached and how
    ///     fast it is lost are left alone: it is the top speed that is lower;
    ///   - jumping: the speed a jump leaves at (Player.jumpSpeed, the game's one number for every
    ///     player, set afresh for each as they are updated, by Player.UpdateJumpHeight, and read
    ///     by every jump after it: off the ground, off a rope, off a hook, a double jump). Cut as
    ///     it is set (<see cref="TopSpeedPlayer"/>), so a jump rises slower and not as high; how
    ///     long one can be held is left alone.
    /// Every machine, every player: each machine runs every player's tick.
    ///
    /// And the calculator: the top speed now (<see cref="Of"/>), in miles an hour as the
    /// Stopwatch counts them (<see cref="Mph"/>), and what it was before the cut and after it, as
    /// of the last tick (<see cref="TopSpeedPlayer.Before"/>, <see cref="TopSpeedPlayer.After"/>).
    /// </summary>
    public static class TopSpeed
    {
        /// <summary>Miles an hour in one pixel a tick, as the Stopwatch counts (Main.DrawInfoAccs: 216000 over 42240).</summary>
        public const float MphPerPixelTick = 216000f / 42240f;

        /// <summary>Slower by this share (0 to 1) of the top speed and the jump speed, this tick.</summary>
        public static void Slow(Player player, float share)
        {
            TopSpeedPlayer speed = player.GetModPlayer<TopSpeedPlayer>();
            speed.Kept *= 1f - MathHelper.Clamp(share, 0f, 1f);
        }

        /// <summary>The top speed now, in pixels a tick: on boots, when they are faster than on foot.</summary>
        public static float Of(Player player) => Math.Max(player.maxRunSpeed, player.accRunSpeed);

        /// <summary>A speed in pixels a tick, in miles an hour, rounded as the Stopwatch rounds it.</summary>
        public static int Mph(float pixelsPerTick) => (int)Math.Round(pixelsPerTick * MphPerPixelTick);
    }

    /// <summary>What <see cref="TopSpeed"/> keeps of one player's speed, and where it is put on.</summary>
    public class TopSpeedPlayer : ModPlayer
    {
        /// <summary>What is kept of the top speed and the jump speed this tick: 1, less what <see cref="TopSpeed.Slow"/> has taken.</summary>
        public float Kept = 1f;

        /// <summary>The top speed before the cut and after it, as of the last tick it was settled in (pixels a tick).</summary>
        public float Before { get; private set; }
        public float After { get; private set; }

        public override void Load()
        {
            On_Player.UpdateJumpHeight += CutJump;
        }

        /// <summary>The jump speed, cut for this player as the game sets it for them.</summary>
        private static void CutJump(On_Player.orig_UpdateJumpHeight orig, Player self)
        {
            orig(self);

            float kept = self.GetModPlayer<TopSpeedPlayer>().Kept;

            if (kept < 1f)
                Terraria.Player.jumpSpeed *= kept;
        }

        public override void ResetEffects()
        {
            Kept = 1f;
        }

        /// <summary>The top speeds, cut once the game has settled them (a mount's in them too) and before it moves the player by them.</summary>
        public override void PostUpdateRunSpeeds()
        {
            Before = TopSpeed.Of(Player);

            if (Kept < 1f)
            {
                Player.maxRunSpeed *= Kept;
                Player.accRunSpeed *= Kept;
            }

            After = TopSpeed.Of(Player);
        }
    }
}
