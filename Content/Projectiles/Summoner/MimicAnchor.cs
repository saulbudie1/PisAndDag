using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Summoner;
using PisAndDag.Content.Mimic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// What the Everclear Mirror's copy (MimicSystem) is to its owner's minions: a minion of 15 slots
    /// (EverclearMirror.Slots), unseen and harmless, kept on the copy. It holds the copy's place among them: calling more
    /// minions than there is room for sends it away as it would any minion, and the copy with it; the copy gone, it
    /// goes; the buff ended (EverclearMirrorBuff), it goes and sends the copy away.
    /// </summary>
    public class MimicAnchor : ModProjectile
    {
        /// <summary>Never drawn; the mirror's look stands in for it where the game wants a picture.</summary>
        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicMirror;

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = EverclearMirror.Slots;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override bool? CanCutTiles() => false;

        public override bool? CanDamage() => false;

        public override bool MinionContactDamage() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<EverclearMirrorBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            bool mine = MimicSystem.Exists && MimicSystem.Owner == Projectile.owner;

            if (owner.HasBuff(buff) && mine)
                Projectile.timeLeft = 2;

            // Every slot its owner has, twelve at the least: a minion called after it sends it away.
            Projectile.minionSlots = Math.Max(EverclearMirror.Slots, owner.maxMinions);

            if (mine)
                Projectile.Center = MimicSystem.Copy.Center;
        }

        /// <summary>Sent away, or let go: the copy goes with it.</summary>
        public override void OnKill(int timeLeft)
        {
            if (MimicSystem.Exists && MimicSystem.Owner == Projectile.owner)
                MimicSystem.Dismiss();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
