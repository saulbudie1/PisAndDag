using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Melee.Fists;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Melee;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// Corosion, a fist of Harenaeum, by request ("A fist type weapon ... with a combo: punch, punch, and punch up
    /// dealing good damage, alt: sandy flash step back that basically gives them invincibility with a cool down of 15
    /// secs"). The first fist weapon (FistWeapon): a jab, a cross with the other hand, and an uppercut that throws
    /// what it hits into the air, the whole arm thrown with each, grown out past the hand into a forearm of packed
    /// sand bound in gold as far as the blow reaches. Right-click scatters the player into
    /// sand and puts them back down ten tiles behind, untouchable while they are sand and for half a second after
    /// (Sandstep); then the sand has to settle, ten seconds (SandSettling).
    ///
    /// Mine: its numbers. 18 damage, the jab 0.8 of it, the cross all of it, the uppercut 2.4; a blow reaches 2.75
    /// tiles from the shoulder (asked: a smaller reach), the player stepping 10, 16 and 12 pixels in with them and back
    /// out (asked; from standing only, running, jumping and flying left alone); the combo goes on if the next comes
    /// within 36 ticks of the last ending.
    /// Hellstone's rank, as the ore is.
    ///
    /// Placeholder pictures (PlaceholderTextures.md): the item, the fist (CorosionFist) and the forearm (CorosionArm).
    /// </summary>
    public class Corosion : FistWeapon
    {
        public const int Damage = 18;

        /// <summary>
        /// The jab, the cross, the uppercut. Mine. The first two barely push, so that what they hit is still there for
        /// the uppercut (pushed out of reach by the cross, the uppercut swung at air); the uppercut throws it.
        /// </summary>
        private static readonly FistMove[] Moves =
        {
            new FistMove(FistArc.Straight, 12, 0.42f, 0.8f, 0.15f, reach: 0.9f, step: 10f),
            new FistMove(FistArc.Straight, 14, 0.45f, 1f, 0.3f, reach: 1f, step: 16f, backArm: true),
            new FistMove(FistArc.Rising, 24, 0.5f, 2.4f, 1.3f, reach: 0.9f, step: 12f, throwing: new Vector2(2.5f, -8.5f)),
        };

        public override FistMove[] Combo => Moves;

        public override float Reach => 44f;

        public override int ComboWindow => 36;

        public override string FistTexture => "PisAndDag/Content/Items/Melee/CorosionFist";

        /// <summary>Sand packed into a forearm, bound in gold: the arm grows as far as the blow reaches.</summary>
        public override string ArmTexture => "PisAndDag/Content/Items/Melee/CorosionArm";

        public override Color Reaching => Harenaeum.PaleGold;

        public override bool HasAltUse => true;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetFistDefaults()
        {
            Item.width = 26;
            Item.height = 26;
            Item.damage = Damage;
            Item.knockBack = 5f;
            Item.crit = 6;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(gold: 2, silver: 70);
        }

        public override SoundStyle Swing(FistMove move, int step) => step switch
        {
            0 => SoundID.DD2_MonkStaffSwing with { Volume = 0.5f, Pitch = 0.5f, PitchVariance = 0.1f },
            1 => SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = 0.25f, PitchVariance = 0.1f },
            _ => SoundID.DD2_MonkStaffSwing with { Volume = 0.75f, Pitch = -0.2f, PitchVariance = 0.1f },
        };

        public override bool CanAltUse(Player player) => !player.HasBuff(ModContent.BuffType<SandSettling>());

        /// <summary>The sandstep: away from the cursor, the blow in hand dropped.</summary>
        public override bool AltUse(Player player, IEntitySource source)
        {
            int punch = ModContent.ProjectileType<FistPunch>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == punch)
                    p.Kill();
            }

            float away = Main.MouseWorld.X < player.Center.X ? 1f : -1f;
            Projectile.NewProjectile(source, player.Center, new Vector2(away, 0f), ModContent.ProjectileType<Sandstep>(), 0, 0f, player.whoAmI);
            player.AddBuff(ModContent.BuffType<SandSettling>(), SandSettling.Ticks);

            return true;
        }

        /// <summary>Sand off the fist as it goes, more as it strikes; the uppercut throws a column of it.</summary>
        public override void Shed(Player player, FistMove move, int step, Vector2 hand, Vector2 reach, float extension, bool striking)
        {
            if (Main.dedServ || extension <= 0.1f)
                return;

            Vector2 way = (reach - hand).SafeNormalize(Vector2.UnitX);
            int grains = striking ? (move.Arc == FistArc.Rising ? 4 : 2) : Main.rand.NextBool(3) ? 1 : 0;

            for (int i = 0; i < grains; i++)
            {
                Vector2 at = Vector2.Lerp(hand, reach, Main.rand.NextFloat(0.4f, 1f)) + Main.rand.NextVector2Circular(5f, 5f);
                Vector2 velocity = -way.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3.5f) + player.velocity * 0.5f;
                PADParticleHandler.SpawnParticle(new SandGrain(at, velocity, Harenaeum.AnySand, Main.rand.Next(14, 26), Main.rand.NextFloat(1f, 1.8f)));
            }

            if (striking && Main.rand.NextBool(move.Arc == FistArc.Rising ? 2 : 5))
                PADParticleHandler.SpawnParticle(new DustFog(reach, -way * 0.8f, Harenaeum.Sand[2], Main.rand.Next(20, 34), Main.rand.NextFloat(0.35f, 0.6f), 0.3f));
        }

        public override void OnFistHit(Player player, NPC target, FistMove move, int step, NPC.HitInfo hit, Vector2 at)
        {
            if (Main.dedServ)
                return;

            Vector2 way = move.Arc == FistArc.Rising ? new Vector2(player.direction * 0.3f, -1f * player.gravDir) : new Vector2(player.direction, 0f);

            if (move.Arc == FistArc.Rising)
            {
                Harenaeum.Burst(at, way, 26, 7f, 0.7f, 2f);
                Harenaeum.Glint(at, 1.3f);
                PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Harenaeum.PaleGold * 0.8f, 0.9f, 14));
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.7f, Pitch = 0.1f }, at);
            }
            else
            {
                Harenaeum.Burst(at, way, step == 1 ? 12 : 8, 4.5f, 0.9f, 0.7f);

                if (Main.rand.NextBool(3))
                    Harenaeum.Glint(at, 0.7f);
            }
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<HarenaeumBar>(14)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}
