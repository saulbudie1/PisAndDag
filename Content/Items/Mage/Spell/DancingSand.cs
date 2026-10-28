using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Mage.Spell
{
    /// <summary>
    /// Dancing Sand, by request ("the staff to mouth and blows a spray of high vel sand with an angle of 15
    /// degrees, distance of up to 30 tiles, dealing consecutive damage, localhit ... is 7-40 the closer you are
    /// (1-5 tile is 7 and lerp til 30), but damage is low, supposed to be dropped by Antlion, Antlion charger or
    /// giant antlion charger with a chance of 1/50, roughly 150 dps at 20 tiles is perfect"; its element Earth).
    ///
    /// Held, the staff goes to the mouth (ColdCallHold, as Cold Call's) and a spray of sand is blown off its head
    /// toward the cursor for as long as the button is held, a cast every <see cref="UseTime"/> ticks, each at its
    /// mana: a cone fifteen degrees wide and thirty tiles long, stopped by the ground (DancingSandGust). Whatever is
    /// in it is hurt again and again, the more often the nearer it is: every 7 ticks within 5 tiles, out to every
    /// 40 at 30 (DancingSandGust.Cooldown). At its damage that is about 150 a second at 20 tiles, 100 at 30, and 574
    /// within 5, to no defence.
    ///
    /// Evocation: it destroys. Earth: drawn from the desert. Dropped by the Antlion, the Antlion Charger and the
    /// Giant Antlion Charger, one time in fifty (VanillaLoot). Placeholder art (PlaceholderTextures.md).
    /// </summary>
    public class DancingSand : Castable
    {
        /// <summary>One in this many from each antlion the request names.</summary>
        public const int DropChance = 50;

        public override CastKind Kind => CastKind.Spell;

        /// <summary>A hit's damage: at 20 tiles it lands every 26.8 ticks (DancingSandGust.Cooldown), 2.24 a second, for about 150.</summary>
        public override int CastDamage => 67;

        /// <summary>Six casts a second while it is held: twelve mana a second.</summary>
        public override int Mana => 2;
        public override int UseTime => 10;

        public override int Shot => ModContent.ProjectileType<DancingSandGust>();
        public override string CastType => "Evocation";
        public override string Element => "Earth";

        /// <summary>A shove of sand, away from the caster.</summary>
        public override float Knockback => 1.5f;

        /// <summary>A rush of air, low, in the place of the chime six times a second.</summary>
        public override SoundStyle? CastSound => SoundID.Item34 with { Volume = 0.35f, Pitch = 0.35f, PitchVariance = 0.15f, MaxInstances = 3 };

        public override void SetDefaults()
        {
            base.SetDefaults();
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(gold: 1);
        }

        public override void Cast(Player player, IEntitySource source, Vector2 position, Vector2 velocity, int damage, float knockback)
        {
            Vector2 head = ColdCallHold.HeadOf(player);
            float aim = (Main.MouseWorld - head).SafeNormalize(velocity.SafeNormalize(Vector2.UnitX * player.direction)).ToRotation();

            // Up until the next cast comes (this one's length and two ticks more), so the staff and the spray go on
            // without a gap while the button is held, and stop soon after it is let go or the mana runs out.
            float ticks = player.itemAnimationMax + 2f;

            ColdCallHold.Keep(player, source, ticks, 1f);
            DancingSandGust.Keep(player, source, ticks, aim, damage, knockback);
        }
    }
}
