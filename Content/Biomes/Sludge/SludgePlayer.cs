using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes.Sludge
{
    /// <summary>
    /// A player and toxic sludge (ToxicSludge), by request ("standing in there could give player the debuff
    /// Overwhelming Toxicity where it -3HP a sec; if you dipped in the toxic sludge, you -20 HP a sec. and has a
    /// permanent debuff Stench, making every thing from the shop 10x time more expensive. debuff is cleared only
    /// when you submerged fully in water").
    ///
    ///   in it         any part of them under its surface: Overwhelming Toxicity, kept three seconds after they come
    ///                 out (3 life a second).
    ///   under it      their head under its surface ("dipped"): Overwhelming Toxicity takes 20 a second instead,
    ///                 and they have Stench, for good: through death, and saved with them.
    ///   washed        their head under clean water (water that is not sludge): Stench is gone.
    ///   stinking      a stench comes off them, and every shop asks ten times its price (StenchShops).
    ///
    /// Worked out on the player's own machine, where their life and their buffs are decided.
    /// </summary>
    public class SludgePlayer : ModPlayer
    {
        /// <summary>Their head was under sludge (last tick); they stink (Stench sets it each tick).</summary>
        public bool Dipped, Stinks;

        /// <summary>Life lost a second in it, and under it (a player's life regen is in half points a second).</summary>
        public const int StandingLoss = 3, DippedLoss = 20;

        /// <summary>How long Overwhelming Toxicity stays after they come out.</summary>
        public const int Lingers = 180;

        public override void ResetEffects() => Stinks = false;

        public override void OnEnterWorld() => ToxicSludge.Ask(Mod);

        /// <summary>Where their head is, as the game takes it for drowning: near the top of them, the way they stand.</summary>
        private Vector2 Head => Player.gravDir == 1f
            ? new Vector2(Player.Center.X, Player.position.Y + 8f)
            : new Vector2(Player.Center.X, Player.position.Y + Player.height - 8f);

        /// <summary>Any part of them under the sludge's surface.</summary>
        private bool InSludge()
        {
            if (ToxicSludge.Count == 0)
                return false;

            int left = (int)(Player.position.X / 16f), right = (int)((Player.position.X + Player.width - 1) / 16f);
            int top = (int)(Player.position.Y / 16f), bottom = (int)((Player.position.Y + Player.height - 1) / 16f);

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (ToxicSludge.At(x, y) && ToxicSludge.Top(x, y) < Player.position.Y + Player.height)
                        return true;
                }
            }

            return false;
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            Vector2 head = Head;
            Dipped = ToxicSludge.Count > 0 && ToxicSludge.In(head);

            if (Dipped || InSludge())
                Player.AddBuff(ModContent.BuffType<OverwhelmingToxicity>(), Lingers);

            int stench = ModContent.BuffType<Stench>();

            if (Dipped)
            {
                if (!Player.HasBuff(stench) && !Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.5f, Pitch = -0.4f }, head);
                    CombatText.NewText(Player.Hitbox, new Color(150, 128, 62), Language.GetTextValue("Mods.PisAndDag.Buffs.Stench.Taken"));
                }

                Player.AddBuff(stench, 60);
                return;
            }

            // Washed: their head under clean water.
            if (!Player.HasBuff(stench))
                return;

            int hx = (int)(head.X / 16f), hy = (int)(head.Y / 16f);

            if (!WorldGen.InWorld(hx, hy) || ToxicSludge.Marked(hx, hy))
                return;

            Tile tile = Main.tile[hx, hy];

            if (tile.LiquidAmount == 0 || tile.LiquidType != LiquidID.Water || !Collision.DrownCollision(Player.position, Player.width, Player.height, Player.gravDir))
                return;

            Player.ClearBuff(stench);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.8f }, head);
            CombatText.NewText(Player.Hitbox, new Color(150, 200, 230), Language.GetTextValue("Mods.PisAndDag.Buffs.Stench.Washed"));

            for (int i = 0; i < 16; i++)
                Dust.NewDust(Player.position, Player.width, Player.height, DustID.BreatheBubble, 0f, -1.5f);
        }

        public override void UpdateBadLifeRegen()
        {
            if (!Player.HasBuff(ModContent.BuffType<OverwhelmingToxicity>()))
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= (Dipped ? DippedLoss : StandingLoss) * 2;
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (!Stinks || drawInfo.shadow != 0f)
                return;

            // A little browner, and the stench coming off them.
            r *= 0.92f;
            g *= 0.86f;
            b *= 0.66f;

            if (Main.gamePaused || !Main.rand.NextBool(9))
                return;

            Vector2 at = Player.Center + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-14f, 6f));
            PADParticleHandler.SpawnParticle(new DustFog(at, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-0.9f, -0.4f)),
                new Color(116, 104, 54), Main.rand.Next(40, 70), Main.rand.NextFloat(0.25f, 0.45f), 0.3f));
        }
    }

    /// <summary>Stench: every shop asks ten times its price of whoever stinks (the one opening it, on their own machine).</summary>
    public class StenchShops : GlobalNPC
    {
        /// <summary>How many times its price.</summary>
        public const int Times = 10;

        public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
        {
            if (Main.dedServ || Main.LocalPlayer == null || !Main.LocalPlayer.HasBuff(ModContent.BuffType<Stench>()))
                return;

            foreach (Item item in items)
            {
                if (item == null || item.IsAir)
                    continue;

                long price = (long)(item.shopCustomPrice ?? item.value) * Times;
                item.shopCustomPrice = (int)Math.Min(int.MaxValue, price);
            }
        }
    }
}
