using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PisAndDag.Content.Items.Mage
{
    /// <summary>
    /// The thing in the hand. A staff casts spells, a seal casts incantations, and
    /// neither has any numbers of its own: every tick it is held or sits in the bag it
    /// copies its damage, mana, use time, the wait after it, channel and shot from
    /// whichever memorised cast of its kind is selected, so the vanilla item machinery —
    /// mana cost, use timing, the damage tooltip, prefixes — all just work on the cast
    /// that is up.
    ///
    /// Nothing selected, nothing happens: CanUseItem says no.
    ///
    /// Selecting is two keybinds, up and down, which move through the memorised casts
    /// OF THIS KIND ONLY. Holding a seal, the spells are still in memory but not in the
    /// cycle. See MagicArsenal.
    /// </summary>
    public abstract class Catalyst : ModItem
    {
        public abstract CastKind Kind { get; }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.DamageType = DamageClass.Magic;
            Item.damage = 0;
            Item.knockBack = 2f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ProjectileID.None;
            Item.shootSpeed = 10f;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1);
            Item.UseSound = SoundID.Item8;
        }

        /// <summary>The cast that is up for this catalyst, or null.</summary>
        public Castable Selected(Player player) => player.GetModPlayer<MagicArsenal>().Selected(Kind);

        /// <summary>Takes the selected cast's numbers, or none. Run every tick the item exists for the player.</summary>
        private void Apply(Player player)
        {
            Castable cast = Selected(player);

            if (cast == null)
            {
                Item.damage = 0;
                Item.mana = 0;
                Item.channel = false;
                Item.shoot = ProjectileID.None;
                Item.reuseDelay = 0;
                return;
            }

            Item.damage = cast.CastDamage;
            Item.mana = cast.Mana;
            Item.useTime = cast.UseTime;
            Item.useAnimation = cast.UseTime;
            Item.reuseDelay = cast.ReuseDelay;
            Item.channel = cast.Channel;
            Item.shoot = cast.Shot;
            Item.shootSpeed = cast.Speed;
            Item.knockBack = cast.Knockback;
        }

        public override void HoldItem(Player player) => Apply(player);

        public override void UpdateInventory(Player player) => Apply(player);

        public override bool CanUseItem(Player player)
        {
            Castable cast = Selected(player);
            return cast != null && cast.CanCast(player);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Selected(player)?.Cast(player, source, position, velocity, damage, knockback);
            return false;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Castable cast = Selected(Main.LocalPlayer);

            string kind = Language.GetTextValue("Mods.PisAndDag.Magic.Casts" + Kind);
            tooltips.Add(new TooltipLine(Mod, "PADCasts", kind) { OverrideColor = new Color(200, 170, 255) });

            string up = cast == null
                ? Language.GetTextValue("Mods.PisAndDag.Magic.NothingMemorised")
                : Language.GetTextValue("Mods.PisAndDag.Magic.Up", cast.DisplayName.Value);

            tooltips.Add(new TooltipLine(Mod, "PADUp", up));
        }

        /// <summary>
        /// The name of the cast that is up is printed in its element's colours, so the
        /// line says at a glance what is loaded as well as which one it is. "Up: " keeps
        /// the plain colour; only the name is coloured.
        /// </summary>
        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != Mod.Name || line.Name != "PADUp")
                return true;

            Castable cast = Selected(Main.LocalPlayer);

            if (cast == null || MagicColours.Of(cast.Element) is not { } colours)
                return true;

            string label = Language.GetTextValue("Mods.PisAndDag.Magic.Up", "");
            Vector2 at = new Vector2(line.X, line.Y);
            Color plain = line.OverrideColor ?? line.Color;

            ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, label, at, plain,
                line.Rotation, line.Origin, line.BaseScale, -1f, 2f);

            at.X += MagicColours.Width(line.Font, label, line.BaseScale);
            MagicColours.Stroked(Main.spriteBatch, line.Font, cast.DisplayName.Value, at,
                colours.fill, colours.stroke, line.BaseScale, line.Rotation, line.Origin, MagicColours.RampOf(cast.Element));

            return false;
        }
    }
}
