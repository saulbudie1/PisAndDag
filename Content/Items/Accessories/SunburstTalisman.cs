using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// The Sunburst Talisman: a gilt sun on a cord, from the Gilded Knight of Grace, every kill
    /// (GildedKnight.ModifyNPCLoot). By request: "An accessory that makes the increase generation
    /// by 10HP/s in the sun (above ground + daytime) and increase all damage by 7.3%. during the
    /// day and 4.5% at night."
    ///
    /// In the sun (<see cref="InSun"/>: day, and above the ground's surface, the sky included) it
    /// adds 10 life a second to the wearer's regeneration (player.lifeRegen is counted in halves of
    /// a life a second: Player.UpdateLifeRegen adds it to lifeRegenCount every tick and gives a
    /// life for every 120). It is regeneration like any other: whatever stops regeneration (a
    /// burning debuff) stops it too. All damage 7.3% higher by day, 4.5% by night.
    ///
    /// Shown, now and then a mote of gold light rises off the wearer while the sun is on them.
    /// </summary>
    public class SunburstTalisman : ModItem
    {
        /// <summary>Ten life a second, in player.lifeRegen's halves.</summary>
        public const int SunRegen = 20;

        public const float DayDamage = 0.073f;
        public const float NightDamage = 0.045f;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 3);
        }

        /// <summary>The sun on them: day, and above the surface of the world.</summary>
        public static bool InSun(Player player) => Main.dayTime && player.Center.Y < Main.worldSurface * 16.0;

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetDamage(DamageClass.Generic) += Main.dayTime ? DayDamage : NightDamage;

            if (!InSun(player))
                return;

            player.lifeRegen += SunRegen;

            if (!hideVisual && !Main.dedServ && Main.rand.NextBool(24))
            {
                Vector2 at = player.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-16f, 14f));
                PADParticleHandler.SpawnParticle(new GraceMote(at, new Color(255, 206, 110), Main.rand.Next(40, 70)));
            }
        }
    }
}
