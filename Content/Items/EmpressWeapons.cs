using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items
{
    /// <summary>
    /// Her weapons, in a Legends world, 1.15 times as hard (by request: "just for this buff her weapons damage by 1.15x in
    /// legends", her weapons being what the Empress of Light drops): Nightglow, Starlight, Kaleidoscope, Eventide and
    /// Terraprisma. The tooltip says so, and so does every hit of theirs: what each throws or swings takes its damage from
    /// the weapon's (Player.GetWeaponDamage), the whip's lash with the rest. Terraprisma's blades are the exception, and
    /// are made so as they hit (EmpressWeaponBlades). Only the weapon's own share: an arrow loosed from Eventide adds what
    /// an arrow adds, and the whip's mark on an enemy stays what it is.
    ///
    /// Asked of the world as it is, at every swing: a world that turns Legends (its Orb broken) has them so at once, and
    /// the same weapons carried into another world are the game's again.
    /// </summary>
    public class EmpressWeapons : GlobalItem
    {
        /// <summary>How much harder they are in Legends.</summary>
        public const float LegendsDamage = 1.15f;

        /// <summary>The weapons she drops.</summary>
        public static bool IsHers(int type) => type == ItemID.FairyQueenMagicItem || type == ItemID.PiercingStarlight
            || type == ItemID.RainbowWhip || type == ItemID.FairyQueenRangedItem || type == ItemID.EmpressBlade;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => IsHers(entity.type);

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
        {
            if (PADWorld.Legends)
                damage *= LegendsDamage;
        }
    }

    /// <summary>
    /// Terraprisma's blades, in Legends, 1.15 times as hard as they hit (EmpressWeapons). The game gives a summoned blade
    /// its damage again every tick from the weapon's own number and the player's summon damage, not from the weapon as
    /// held (Projectile.Update, originalDamage), so what the weapon is given never reaches them; this does, hit by hit.
    /// </summary>
    public class EmpressWeaponBlades : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.EmpressBlade;

        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (PADWorld.Legends)
                modifiers.SourceDamage *= EmpressWeapons.LegendsDamage;
        }
    }
}
