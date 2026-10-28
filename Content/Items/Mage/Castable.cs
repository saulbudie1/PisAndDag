using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace PisAndDag.Content.Items.Mage
{
    /// <summary>Which catalyst casts it. A staff casts spells; a seal casts incantations.</summary>
    public enum CastKind
    {
        Spell,
        Incantation,
    }

    /// <summary>
    /// A spell or an incantation: a thing you keep, not a thing you swing.
    ///
    /// It is an item so it can be found, dropped, crafted and traded like anything
    /// else, but it does nothing in the hand and cannot be used. It goes in the Magic
    /// Arsenal (MagicArsenal, the window beside the inventory), and while it sits there
    /// it is memorised. Casting is done through a catalyst held in the hand, which reads
    /// its numbers off whichever of these is selected and fires <see cref="Cast"/>.
    ///
    /// The lore for all of this is §XXIV of the Bible: magic is drawn out of the land,
    /// and the element of a thing is where it was drawn from.
    /// </summary>
    public abstract class Castable : ModItem
    {
        public abstract CastKind Kind { get; }

        /// <summary>Base damage. The catalyst's damage becomes this while it is selected.</summary>
        public abstract int CastDamage { get; }

        /// <summary>Mana per cast. Anything extra a cast can cost on top is that cast's own business.</summary>
        public abstract int Mana { get; }

        /// <summary>Ticks between casts.</summary>
        public abstract int UseTime { get; }

        /// <summary>The projectile the catalyst fires. What it does from there is its own.</summary>
        public abstract int Shot { get; }

        /// <summary>What it does: Explosion, Heal, Pierce and so on. A label, shown in the tooltip.</summary>
        public abstract string CastType { get; }

        /// <summary>Where it was drawn from: Flame, Frost, Blood and so on. A label, shown in the tooltip.</summary>
        public abstract string Element { get; }

        /// <summary>Whether the catalyst holds while the button is held. For casts that charge.</summary>
        public virtual bool Channel => false;

        public virtual float Speed => 10f;

        /// <summary>The sound each cast makes, in the place of the catalyst's own (the game's magic chime, SoundID.Item8). For casts that go off so often the chime would be a rattle.</summary>
        public virtual SoundStyle? CastSound => null;
        public virtual float Knockback => 2f;

        /// <summary>
        /// Ticks the catalyst waits after a cast before it will cast again. Vanilla's
        /// reuseDelay: the hand stays up and nothing goes off, and use speed shortens it as it
        /// shortens the cast.
        /// </summary>
        public virtual int ReuseDelay => 0;

        /// <summary>Whether it can go off right now. For casts that should not stack on themselves.</summary>
        public virtual bool CanCast(Player player) => true;

        /// <summary>Fires the cast. The default puts <see cref="Shot"/> in the air with the numbers the catalyst worked out.</summary>
        public virtual void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            Projectile.NewProjectile(source, position, velocity, Shot, damage, knockback, player.whoAmI);
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(silver: 50);
            Item.useStyle = ItemUseStyleID.None;
            Item.consumable = false;
            Item.noMelee = true;

            // Magic, so anything that asks what class a thing is gets the right
            // answer. No damage of its own: the number is CastDamage, and the catalyst
            // carries it.
            Item.DamageType = DamageClass.Magic;
        }

        public override bool CanUseItem(Player player) => false;

        // ---- tooltip -----------------------------------------------------------

        /// <summary>
        /// The damage as this player would deal it: the base through their magic
        /// damage, the way a weapon's tooltip shows it. The base is what the catalyst
        /// takes; the scaling is vanilla's, on the cast when it is fired.
        /// </summary>
        public int ScaledDamage(Player player) =>
            (int)Math.Round(player.GetTotalDamage(DamageClass.Magic).ApplyTo(CastDamage));

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            string kind = Language.GetTextValue("Mods.PisAndDag.Magic." + Kind);
            bool held = Main.LocalPlayer.GetModPlayer<MagicArsenal>().Holds(Type);

            tooltips.Add(new TooltipLine(Mod, "PADKind", kind) { OverrideColor = MagicColours.Kinds[Kind].fill });
            // A cast that hurts nothing (a ward, a blessing) says nothing about damage.
            if (CastDamage > 0)
                tooltips.Add(new TooltipLine(Mod, "PADDamage", Language.GetTextValue("Mods.PisAndDag.Magic.Damage", ScaledDamage(Main.LocalPlayer))));
            tooltips.Add(new TooltipLine(Mod, "PADMana", Language.GetTextValue("Mods.PisAndDag.Magic.Mana", Mana)));
            tooltips.Add(new TooltipLine(Mod, "PADType", Language.GetTextValue("Mods.PisAndDag.Magic.Type", CastType)));
            tooltips.Add(new TooltipLine(Mod, "PADElement", Language.GetTextValue("Mods.PisAndDag.Magic.Element", Element)));
            tooltips.Add(new TooltipLine(Mod, "PADArsenal",
                Language.GetTextValue(held ? "Mods.PisAndDag.Magic.InArsenal" : "Mods.PisAndDag.Magic.PlaceInArsenal"))
            {
                OverrideColor = held ? new Color(120, 220, 120) : new Color(160, 160, 160),
            });
        }

        /// <summary>
        /// The kind line and the element's name are drawn by hand so they can carry a
        /// coloured stroke, which a TooltipLine cannot: vanilla's shadow is black.
        /// </summary>
        public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
        {
            if (line.Mod != Mod.Name)
                return true;

            Vector2 at = new Vector2(line.X, line.Y);

            if (line.Name == "PADKind")
            {
                (Color fill, Color stroke) kind = MagicColours.Kinds[Kind];
                MagicColours.Stroked(Main.spriteBatch, line.Font, line.Text, at, kind.fill, kind.stroke, line.BaseScale, line.Rotation, line.Origin);
                return false;
            }

            if (line.Name == "PADElement" && MagicColours.Of(Element) is { } colours)
            {
                // "Element: " in the line's own colour, then the name in the element's.
                string label = Language.GetTextValue("Mods.PisAndDag.Magic.Element", "");
                Color plain = line.OverrideColor ?? line.Color;

                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, line.Font, label, at, plain,
                    line.Rotation, line.Origin, line.BaseScale, -1f, 2f);

                at.X += MagicColours.Width(line.Font, label, line.BaseScale);
                MagicColours.Stroked(Main.spriteBatch, line.Font, Element, at, colours.fill, colours.stroke, line.BaseScale, line.Rotation, line.Origin,
                    MagicColours.RampOf(Element));
                return false;
            }

            return true;
        }
    }
}
