using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// Aquatic Firearms. Held, it hangs a Minishark, a Megashark and an S.D.M.G. over the
    /// caster's head, the strongest on top, and they fire where the cursor is for as long
    /// as the staff is held and the mana lasts. All of that is the battery's
    /// (<see cref="AquaticFirearmsBattery"/>); this is the page.
    ///
    /// Conjuration on the chart of schools: it calls things up. Its element is Conquest,
    /// whose stroke is a ramp of three golds (MagicColours.StrokeRamps).
    ///
    /// The cast's own mana, <see cref="Mana"/>, pays for the first
    /// <see cref="AquaticFirearmsBattery.FreeTicks"/>; after that holding it costs
    /// <see cref="AquaticFirearmsBattery.DrainPerSecond"/> a second.
    ///
    /// No source yet: it has to be cheated in. Placeholder art: see PlaceholderTextures.md.
    /// </summary>
    public class AquaticFirearms : Castable
    {
        public override CastKind Kind => CastKind.Spell;

        /// <summary>The top gun's shot, the S.D.M.G.'s: what the tooltip's first number and the staff's damage are.</summary>
        public override int CastDamage => AquaticFirearmsBattery.Stats[AquaticFirearmsBattery.Guns - 1].ShotBase;

        public override int Mana => 50;
        public override int UseTime => 20;
        public override bool Channel => true;
        public override int Shot => ModContent.ProjectileType<AquaticFirearmsBattery>();
        public override string CastType => "Conjuration";
        public override string Element => "Conquest";

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Red;
        }

        /// <summary>One set of guns at a time, and not while the last set is still going.</summary>
        public override bool CanCast(Player player) => player.ownedProjectileCounts[Shot] == 0;

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            // Its velocity is the aim, cursor from the caster's centre.
            Projectile.NewProjectile(source, player.MountedCenter, Main.MouseWorld - player.MountedCenter, Shot, damage, knockback, player.whoAmI);
        }

        /// <summary>
        /// The damage line gives all three shots, top gun first, the way the battery works
        /// them out: through the caster's magic damage and cut down to whole points.
        /// </summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            base.ModifyTooltips(tooltips);

            TooltipLine line = tooltips.Find(t => t.Mod == Mod.Name && t.Name == "PADDamage");

            if (line == null)
                return;

            StatModifier magic = Main.LocalPlayer.GetTotalDamage(DamageClass.Magic);
            AquaticFirearmsBattery.GunStats[] guns = AquaticFirearmsBattery.Stats;
            string[] each = new string[guns.Length];

            for (int i = 0; i < guns.Length; i++)
                each[i] = ((int)(magic.ApplyTo(guns[guns.Length - 1 - i].ShotBase) + 5E-06f)).ToString();

            line.Text = Language.GetTextValue("Mods.PisAndDag.Magic.Damage", string.Join(" / ", each));
        }
    }
}
