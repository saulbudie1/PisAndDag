using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What Chlorophyte, Arm Me puts on: while it lasts, every bullet the player fires that
    /// is of lower rarity than a Chlorophyte Bullet comes out a Chlorophyte Bullet.
    ///
    /// The turning is <see cref="ArmedRounds"/>'s, at the moment a bullet is made, so it
    /// takes every gun alike: ammo from the inventory, guns that fire their own rounds by
    /// hand, the guns of Aquatic Firearms, turrets. Only the kind of bullet changes. Its
    /// damage, speed and knockback stay what the gun and the ammo gave it, so the round
    /// homes and never hits softer for it (it does lose whatever its own kind did, a
    /// Cursed Bullet's fire and the like).
    ///
    /// What is seen is <see cref="ArmedPlayer"/>'s.
    /// </summary>
    public class Armed : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // The jungle's arming does not outlast the world it was asked in.
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetModPlayer<ArmedPlayer>().Active = true;
        }
    }

    /// <summary>
    /// Armed, seen: green motes coming off the hands and the odd jungle leaf. Particles and
    /// dust, so each client makes its own for every armed player it sees; buffs are sent to
    /// other players, dust is not.
    /// </summary>
    public class ArmedPlayer : ModPlayer
    {
        /// <summary>Set by the buff every tick it is on; cleared every tick.</summary>
        public bool Active;

        public override void ResetEffects() => Active = false;

        public override void PostUpdate()
        {
            if (!Active || Main.dedServ || Player.dead)
                return;

            Vector2 hands = Player.MountedCenter + new Vector2(Player.direction * 8f, 2f * Player.gravDir);

            if (Main.rand.NextBool(5))
            {
                Vector2 at = hands + Main.rand.NextVector2Circular(8f, 7f);
                Dust dust = Dust.NewDustPerfect(at, DustID.ChlorophyteWeapon,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.3f, 0.8f) * Player.gravDir),
                    0, default, Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
            }

            if (Main.rand.NextBool(45))
            {
                Vector2 at = Player.MountedCenter + new Vector2(Main.rand.NextFloat(-10f, 10f), -12f * Player.gravDir);
                Gore.NewGore(Player.GetSource_FromThis(), at - new Vector2(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.4f, 0.2f)), GoreID.TreeLeaf_Jungle, Main.rand.NextFloat(0.7f, 0.9f));
            }

            Lighting.AddLight(hands, 0.1f, 0.26f, 0.08f);
        }
    }

    /// <summary>
    /// The turning, done where every projectile in the game is made
    /// (Projectile.NewProjectile; the other overloads all come through this one). If the
    /// bullet about to be made is one of the lesser kinds and its owner is Armed, it is
    /// made a Chlorophyte Bullet instead, with everything else it was given unchanged.
    ///
    /// Lesser kinds (<see cref="lesser"/>) are worked out from the ammo itself once
    /// everything is loaded. A kind of bullet takes the rarity of the ammo that defines
    /// it, and is lesser below the Chlorophyte Bullet's (Lime): a vanilla bullet by
    /// vanilla's own ammo (the table: Musket Ball, Silver, Tungsten, Meteor, the Endless
    /// Musket Pouch, Party, Ichor, Cursed, Crystal, Exploding, Nano, High Velocity, Venom,
    /// Golden), a modded bullet by its own mod's ammo. Ammo from anywhere else is not
    /// asked: LuiAFK's Unlimited bullets are vanilla bullets at Red rarity, and letting
    /// them vote made every kind "rarer than chlorophyte" and nothing turned at all. The
    /// Luminite Bullet and the Chlorophyte Bullet itself are left as they are.
    ///
    /// Only the player's own shots: fired from something they hold, or by a projectile of
    /// theirs that is not hostile. A bullet an NPC fires is never turned, whoever the game
    /// lists as its owner (in single player that is the player).
    /// </summary>
    public class ArmedRounds : ModSystem
    {
        private static bool[] lesser;
        private static int armedType = -1;

        public override void Load()
        {
            On_Projectile.NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float += Arm;
        }

        public override void PostSetupContent()
        {
            armedType = ModContent.BuffType<Armed>();

            // Each kind of bullet's rarity: the lowest among the ammo that defines it.
            int[] rarity = new int[ProjectileLoader.ProjectileCount];
            Array.Fill(rarity, int.MaxValue);

            for (int type = 1; type < ItemLoader.ItemCount; type++)
            {
                if (!ContentSamples.ItemsByType.TryGetValue(type, out Item ammo))
                    continue;

                int shot = ammo.shoot;

                if (ammo.ammo != AmmoID.Bullet || shot <= 0 || shot >= rarity.Length)
                    continue;

                // A vanilla bullet answers to vanilla ammo; a modded one to its own mod's.
                bool defines = shot < ProjectileID.Count
                    ? type < ItemID.Count
                    : ammo.ModItem != null && ProjectileLoader.GetProjectile(shot)?.Mod == ammo.ModItem.Mod;

                if (defines)
                    rarity[shot] = Math.Min(rarity[shot], ammo.rare);
            }

            bool[] low = new bool[rarity.Length];
            List<int> turned = new();

            for (int i = 0; i < low.Length; i++)
            {
                low[i] = rarity[i] != int.MaxValue && rarity[i] < ItemRarityID.Lime && i != ProjectileID.ChlorophyteBullet;

                if (low[i])
                    turned.Add(i);
            }

            lesser = low;

            // Said once at load, so client.log shows which kinds it will turn.
            Mod.Logger.Info("Chlorophyte, Arm Me turns bullet projectiles: " + string.Join(", ", turned));
        }

        public override void Unload()
        {
            lesser = null;
            armedType = -1;
        }

        private static int Arm(On_Projectile.orig_NewProjectile_IEntitySource_float_float_float_float_int_int_float_int_float_float_float orig,
            IEntitySource source, float x, float y, float speedX, float speedY, int type, int damage, float knockback, int owner,
            float ai0, float ai1, float ai2)
        {
            // Owner -1 means the local player; NewProjectile makes the same substitution.
            if (Turns(source, type, owner == -1 ? Main.myPlayer : owner))
                type = ProjectileID.ChlorophyteBullet;

            return orig(source, x, y, speedX, speedY, type, damage, knockback, owner, ai0, ai1, ai2);
        }

        /// <summary>Whether a projectile of this kind, made from this source for this owner, comes out chlorophyte.</summary>
        public static bool Turns(IEntitySource source, int type, int owner)
        {
            if (lesser == null || type <= 0 || type >= lesser.Length || !lesser[type])
                return false;

            if (owner < 0 || owner >= Main.maxPlayers)
                return false;

            Player player = Main.player[owner];

            if (!player.active || !player.HasBuff(armedType))
                return false;

            if (source is not EntitySource_Parent parent)
                return false;

            if (parent.Entity is Player shooter)
                return shooter.whoAmI == owner;

            if (parent.Entity is Projectile from)
                return from.owner == owner && !from.hostile;

            return false;
        }
    }
}
