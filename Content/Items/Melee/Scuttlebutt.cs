using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Melee.Fists;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Scuttlebutt, a fist found in the Vast Ocean's lighthouse (the watch room's west sea chest, LighthouseTower), by
    /// request ("Add a boxing weapon same as corosion: Scuttlebutt; 1.5x base damage of corosion; Found in vast ocean's
    /// light house; Start with 2 jabs, a cross and a hook. Right click stand still and perform a 3 second barrage of
    /// water fist ... during 3 second, each second punch 3 flying fist at cursor, alternating hands, 8 second cool
    /// down"; "also move the player during left click like corosion"; "nah make every second is 7 punches").
    ///
    /// A fist weapon (FistWeapon), as Corosion is: a jab, a jab, a cross with the other hand, and a hook (FistArc.Hook,
    /// round from high and wide down onto the line), the player stepping in with each and back out. Right-click: the
    /// barrage (ScuttlebuttBarrage), the player planted where they stand for three seconds and a fist of water thrown
    /// at the cursor seven times a second, one hand then the other (ScuttlebuttWaterFist), each for the weapon's
    /// damage; when it ends, eight seconds before the next (Winded).
    ///
    /// Mine: its numbers past the damage. 27 damage (Corosion's 18, half again), the jabs 0.8 of it, the cross 1.1, the
    /// hook 2.2; the same reach as Corosion's; a step of 8, 10, 16 and 14 pixels; the combo goes on within 36 ticks.
    ///
    /// Placeholder pictures (PlaceholderTextures.md): the item, the fist (ScuttlebuttFist), the forearm (ScuttlebuttArm),
    /// all drawn in code: a cask's oak staves bound in iron hoops.
    /// </summary>
    public class Scuttlebutt : FistWeapon
    {
        public const int Damage = 27;

        /// <summary>Its colour as it strikes: the water's middle blue (ScuttlebuttWaterFist's ramp).</summary>
        public static readonly Color Water = new(131, 173, 212);

        /// <summary>The jab, the jab, the cross, the hook. The first three barely push; the hook throws.</summary>
        private static readonly FistMove[] Moves =
        {
            new FistMove(FistArc.Straight, 11, 0.42f, 0.8f, 0.15f, reach: 0.85f, step: 8f),
            new FistMove(FistArc.Straight, 11, 0.42f, 0.8f, 0.15f, reach: 0.9f, step: 10f),
            new FistMove(FistArc.Straight, 14, 0.45f, 1.1f, 0.3f, reach: 1f, step: 16f, backArm: true),
            new FistMove(FistArc.Hook, 22, 0.5f, 2.2f, 1.4f, reach: 0.95f, step: 14f, throwing: new Vector2(7f, -2.5f)),
        };

        public override FistMove[] Combo => Moves;

        public override float Reach => 44f;

        public override int ComboWindow => 36;

        public override string FistTexture => "PisAndDag/Content/Items/Melee/ScuttlebuttFist";

        public override string ArmTexture => "PisAndDag/Content/Items/Melee/ScuttlebuttArm";

        public override Color Reaching => Water;

        public override bool HasAltUse => true;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetFistDefaults()
        {
            Item.width = 26;
            Item.height = 28;
            Item.damage = Damage;
            Item.knockBack = 5.5f;
            Item.crit = 6;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 3);
        }

        public override SoundStyle Swing(FistMove move, int step) => step switch
        {
            0 or 1 => SoundID.DD2_MonkStaffSwing with { Volume = 0.5f, Pitch = 0.55f, PitchVariance = 0.1f },
            2 => SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.25f, PitchVariance = 0.1f },
            _ => SoundID.DD2_MonkStaffSwing with { Volume = 0.8f, Pitch = -0.3f, PitchVariance = 0.1f },
        };

        public override bool CanAltUse(Player player) =>
            !player.HasBuff(ModContent.BuffType<Winded>()) && player.ownedProjectileCounts[ModContent.ProjectileType<ScuttlebuttBarrage>()] == 0;

        /// <summary>The barrage: the blow in hand dropped, the player planted, the fists thrown.</summary>
        public override bool AltUse(Player player, IEntitySource source)
        {
            int punch = ModContent.ProjectileType<FistPunch>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == punch)
                    p.Kill();
            }

            Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(new Vector2(player.direction, 0f));
            int damage = player.GetWeaponDamage(Item);
            float knockback = player.GetWeaponKnockback(Item, Item.knockBack);
            Projectile.NewProjectile(source, player.MountedCenter, aim, ModContent.ProjectileType<ScuttlebuttBarrage>(), damage, knockback, player.whoAmI);

            return true;
        }

        /// <summary>Drops off the fist as it goes, more as it strikes.</summary>
        public override void Shed(Player player, FistMove move, int step, Vector2 hand, Vector2 reach, float extension, bool striking)
        {
            if (Main.dedServ || extension <= 0.1f)
                return;

            int drops = striking ? (move.Arc == FistArc.Hook ? 3 : 1) : Main.rand.NextBool(4) ? 1 : 0;

            for (int i = 0; i < drops; i++)
            {
                Vector2 at = Vector2.Lerp(hand, reach, Main.rand.NextFloat(0.5f, 1f)) + Main.rand.NextVector2Circular(4f, 4f);
                Dust drop = Dust.NewDustPerfect(at, DustID.Water, player.velocity * 0.4f + Main.rand.NextVector2Circular(1f, 1f), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                drop.noGravity = false;
            }
        }

        public override void OnFistHit(Player player, NPC target, FistMove move, int step, NPC.HitInfo hit, Vector2 at)
        {
            if (Main.dedServ)
                return;

            int splash = move.Arc == FistArc.Hook ? 22 : 8;

            for (int i = 0; i < splash; i++)
            {
                Vector2 velocity = new Vector2(player.direction * Main.rand.NextFloat(0.5f, 4f), Main.rand.NextFloat(-4f, 0.5f));
                Dust.NewDustPerfect(at, i % 3 == 0 ? DustID.DungeonWater : DustID.Water, velocity, 0, default, Main.rand.NextFloat(1f, 1.5f));
            }

            if (move.Arc == FistArc.Hook)
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, Pitch = 0.1f }, at);
        }
    }

    /// <summary>The barrage's hold on its player, on their own machine: while it lasts they cannot walk or jump.</summary>
    public class ScuttlebuttPlayer : ModPlayer
    {
        /// <summary>Ticks left of being planted; the barrage sets it every tick it lasts.</summary>
        public int Planted;

        public override void SetControls()
        {
            if (Planted <= 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
        }

        public override void PostUpdate()
        {
            if (Planted > 0)
                Planted--;
        }
    }
}
