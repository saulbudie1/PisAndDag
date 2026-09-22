using PisAndDag.Content.Projectiles.Mage;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Incantation
{
    /// <summary>
    /// The first incantation. A ball of fire, thrown, that falls and bursts. Held, it
    /// gathers into something bigger; see <see cref="MeagreFlameHold"/>.
    /// </summary>
    public class MeagreFlame : Castable
    {
        public override CastKind Kind => CastKind.Incantation;
        public override int CastDamage => 40;
        public override int Mana => 18;
        public override int UseTime => 60;
        public override bool Channel => true;
        public override int Shot => ModContent.ProjectileType<MeagreFlameHold>();
        public override string CastType => "Evocation";
        public override string Element => "Flame";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 46;
            Item.height = 53;
            Item.rare = ItemRarityID.Green;
        }
    }
}
