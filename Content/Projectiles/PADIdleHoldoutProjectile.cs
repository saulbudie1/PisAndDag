using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles
{
    /// <summary>
    /// A weapon that is held rather than swung.
    ///
    /// **This is Calamity's BaseIdleHoldoutProjectile, ported.** Same contract, same
    /// registry, same sealed AI that checks the holdout still belongs there and then hands
    /// off to SafeAI. Two things are left out: their profaned crystal check, which is a
    /// Calamity buff this mod has never heard of, and their Unload nulling of the
    /// dictionary, which tModLoader does not need here.
    ///
    /// The shape of it is worth understanding before subclassing. The projectile is not
    /// spawned by using the item. It is spawned by <see cref="CheckForEveryHoldout"/>,
    /// which runs every tick for every player and makes one exist whenever the matching
    /// item is held — so the weapon is simply *there* in the player's hands, all the time,
    /// and clicking is something the projectile notices rather than something that creates
    /// it. PADHoldoutPlayer is what calls it.
    ///
    /// That is why it is called idle: the holdout's resting state is existing.
    /// </summary>
    public abstract class PADIdleHoldoutProjectile : ModProjectile
    {
        public Player Owner => Main.player[Projectile.owner];

        /// <summary>The item this holdout belongs to.</summary>
        public abstract int AssociatedItemID { get; }

        /// <summary>
        /// This projectile's own type.
        ///
        /// Theirs, and their comment explains why it has to be written out by hand: the
        /// type cannot be discovered at load time, because the instance tModLoader builds
        /// to read these properties has an ID of 0. Not ideal. It works.
        /// </summary>
        public abstract int IntendedProjectileType { get; }

        public static Dictionary<int, int> ItemProjectileRelationship = new();

        public override void SetStaticDefaults()
        {
            ItemProjectileRelationship[AssociatedItemID] = IntendedProjectileType;
        }

        public override void Unload()
        {
            ItemProjectileRelationship?.Clear();
        }

        /// <summary>
        /// Makes sure that whatever the player is holding, its holdout exists. Called once
        /// per player per tick from PADHoldoutPlayer.
        /// </summary>
        public static void CheckForEveryHoldout(Player player)
        {
            if (ItemProjectileRelationship is null)
                return;

            foreach (int itemID in ItemProjectileRelationship.Keys)
            {
                Item heldItem = player.HeldItem;

                if (heldItem.type != itemID)
                    continue;

                bool alreadyThere = false;
                int holdoutType = ItemProjectileRelationship[itemID];

                foreach (Projectile p in Main.ActiveProjectiles)
                {
                    if (p.type != holdoutType || p.owner != player.whoAmI)
                        continue;

                    alreadyThere = true;
                    break;
                }

                if (Main.myPlayer == player.whoAmI && !alreadyThere)
                {
                    int damage = player.GetWeaponDamage(heldItem);
                    float kb = player.GetWeaponKnockback(heldItem, heldItem.knockBack);

                    Projectile.NewProjectile(player.GetSource_ItemUse(heldItem), player.Center,
                        Vector2.Zero, holdoutType, damage, kb, player.whoAmI);
                }
            }
        }

        public sealed override void AI()
        {
            CheckForEveryHoldout(Owner);

            if (Owner.HeldItem.type != AssociatedItemID || Owner.CCed || !Owner.active || Owner.dead)
            {
                Projectile.Kill();
                return;
            }

            SafeAI();
        }

        public virtual void SafeAI() { }
    }
}
