using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Base
{
    public abstract class BaseIdleHoldoutProjectile : ModProjectile
    {
        public Player Owner => Main.player[Projectile.owner];

        public abstract int AssociatedItemID { get; }

        // The projectile type cannot be directly discerned at load time (Using Activator.CreateInstance will create an associated projectile with an ID of 0) and as
        // such this property exists as a direct means of retrieving it. Not ideal, but it works.
        public abstract int IntendedProjectileType { get; }
        public static Dictionary<int, int> ItemProjectileRelationship = [];

        public override void SetStaticDefaults()
        {
            ItemProjectileRelationship[AssociatedItemID] = IntendedProjectileType;
        }

        public override void Unload()
        {
            ItemProjectileRelationship?.Clear();
            ItemProjectileRelationship = null;
        }

        public static void CheckForEveryHoldout(Player player)
        {
            foreach (int itemID in ItemProjectileRelationship.Keys)
            {
                Item heldItem = player.HeldItem;
                if (heldItem.type != itemID)
                    continue;

                bool bladeIsPresent = false;
                int holdoutType = ItemProjectileRelationship[itemID];
                foreach (Projectile p in Main.ActiveProjectiles)
                {
                    if (p.type != holdoutType || p.owner != player.whoAmI)
                        continue;

                    bladeIsPresent = true;
                    break;
                }

                if (Main.myPlayer == player.whoAmI && !bladeIsPresent)
                {
                    int damage = player.GetWeaponDamage(heldItem);
                    float kb = player.GetWeaponKnockback(heldItem, heldItem.knockBack);
                    Projectile.NewProjectile(player.GetSource_ItemUse(heldItem), player.Center, Vector2.Zero, holdoutType, damage, kb, player.whoAmI);
                }
            }
        }

        public sealed override void AI()
        {
            CheckForEveryHoldout(Owner);
            if (Owner.HeldItem.type != AssociatedItemID || Owner.CCed || !Owner.active || Owner.dead )
            {
                Projectile.Kill();
                return;
            }
            SafeAI();
        }

        public virtual void SafeAI() { }
    }
}
