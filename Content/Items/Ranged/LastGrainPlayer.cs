using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Ranged
{
    /// <summary>
    /// The Last Grain's hourglass, the player's own (Items.Ranged.TheLastGrain): ten seconds of sand, by request,
    /// falling whether the gun is in hand or not. Turned over, the sand that had fallen is what falls next, as a real
    /// one's is, asked: turned at four seconds gone, it has four to give. Empty, and the gun in hand, its holder is
    /// slowed: seven tenths of their speed walking and running (here) and flying (LastGrainWings); jumping and
    /// falling as ever, by request.
    ///
    /// Kept on the holder's own machine, where they move and shoot, and slowing them only there (their movement is theirs
    /// to tell the others; another machine's copy of the glass never turns); nothing of it needs telling.
    /// </summary>
    public class LastGrainPlayer : ModPlayer
    {
        /// <summary>By request: ten seconds.</summary>
        public const int Full = 10 * 60;

        /// <summary>By request: slowed by 30%.</summary>
        public const float Slow = 0.7f;

        /// <summary>Ticks of sand still above the neck.</summary>
        public int Top;

        /// <summary>For the glass on the screen (LastGrainUI): ticks left of a turn, and what was above when it began.</summary>
        public int Turning;
        public int TurnedFrom;

        public const int TurnTicks = 14;

        public bool Empty => Top <= 0;

        /// <summary>Whether the empty glass is slowing them: empty, the gun in hand, on their own machine.</summary>
        public bool Slowed => Player.whoAmI == Main.myPlayer && Empty && Player.HeldItem?.ModItem is TheLastGrain;

        /// <summary>Turned over: what had fallen is above now.</summary>
        public void Flip()
        {
            TurnedFrom = Top;
            Top = Full - Top;
            Turning = TurnTicks;

            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.6f, Pitch = 0.5f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.2f, Pitch = 0.7f }, Player.Center);

            if (!Main.dedServ)
            {
                Vector2 at = Player.MountedCenter + new Vector2(Player.direction * 10f, -6f);
                Harenaeum.Glint(at, 0.8f);

                for (int i = 0; i < 6; i++)
                    PADParticleHandler.SpawnParticle(new SandGrain(at, Main.rand.NextVector2Circular(1.5f, 1.5f), Harenaeum.AnySand, Main.rand.Next(12, 20), Main.rand.NextFloat(0.9f, 1.4f)));
            }
        }

        public override void PostUpdate()
        {
            if (Top > 0)
            {
                Top--;

                // The last grain: a tick of the glass as it goes.
                if (Top == 0 && Player.whoAmI == Main.myPlayer && Player.HeldItem?.ModItem is TheLastGrain)
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.8f, Pitch = -0.4f }, Player.Center);
            }

            if (Turning > 0)
                Turning--;
        }

        public override void PostUpdateRunSpeeds()
        {
            if (!Slowed)
                return;

            Player.maxRunSpeed *= Slow;
            Player.accRunSpeed *= Slow;
            Player.runAcceleration *= Slow;
        }
    }

    /// <summary>
    /// The empty glass's slow, on wings, going up (by request, falling stays as it is). Across, flying is slowed with
    /// running (LastGrainPlayer.PostUpdateRunSpeeds): the game takes a flyer's speed across from the same numbers.
    /// </summary>
    public class LastGrainWings : GlobalItem
    {
        public override void VerticalWingSpeeds(Item item, Player player, ref float ascentWhenFalling, ref float ascentWhenRising,
            ref float maxCanAscendMultiplier, ref float maxAscentMultiplier, ref float constantAscend)
        {
            if (!player.GetModPlayer<LastGrainPlayer>().Slowed)
                return;

            ascentWhenRising *= LastGrainPlayer.Slow;
            maxAscentMultiplier *= LastGrainPlayer.Slow;
            constantAscend *= LastGrainPlayer.Slow;
        }
    }
}
