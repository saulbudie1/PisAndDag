using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Mage
{
    /// <summary>
    /// What sits in the hand while Meagre Flame is held down. Not the fireball; the
    /// gathering of it. Let go and the ball leaves. Hold, and after
    /// <see cref="ChargeTicks"/> it is ready to go out twice the size at three times the
    /// damage, for the rest Of The mana (the cast's own cost was paid on the click; the
    /// charged throw asks for the difference up to forty on release, and if the player
    /// has not got it the small ball goes instead).
    ///
    /// The catalyst's use time is pinned to two ticks while this lives, so the hand stays
    /// up; on release it is set to the cast's real use time, so the next throw waits.
    ///
    /// ai[0] is the charge, in ticks. Owner drives everything and syncs the aim; other
    /// clients only draw.
    /// </summary>
    public class MeagreFlameHold : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        /// <summary>Seven seconds.</summary>
        public const int ChargeTicks = 2*60;

        /// <summary>Total mana Of The charged throw. The cast's Mana is already paid; the rest is taken on release.</summary>
        public const int ChargedMana = 40;

        public const int BigDamageTimes = 3;

        /// <summary>Distance from the shoulder to the hand, in pixels.</summary>
        private const float Reach = 20f;

        private Player Owner => Main.player[Projectile.owner];

        private float Charge => MathHelper.Clamp(Projectile.ai[0] / ChargeTicks, 0f, 1f);
        private bool Charged => Projectile.ai[0] >= ChargeTicks;

        /// <summary>Set the tick the charge completes, so the sting plays once.</summary>
        private bool rang;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.HeldItem?.ModItem is not Catalyst)
            {
                Projectile.Kill();
                return;
            }

            Vector2 shoulder = player.RotatedRelativePoint(player.MountedCenter, true, true);

            if (Projectile.owner == Main.myPlayer)
            {
                Vector2 aim = (Main.MouseWorld - shoulder).SafeNormalize(Vector2.UnitX * player.direction);

                if (Vector2.DistanceSquared(aim, Projectile.velocity) > 0.001f)
                {
                    Projectile.velocity = aim;
                    Projectile.netUpdate = true;
                }
            }

            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Vector2 hand = shoulder + dir * Reach;

            Projectile.Center = hand;
            Projectile.rotation = dir.ToRotation();
            Projectile.timeLeft = 2;

            // The player: faces the aim, arm out along it, hand held up.
            player.ChangeDir(dir.X < 0f ? -1 : 1);
            player.heldProj = Projectile.whoAmI;
            player.itemRotation = (dir * player.direction).ToRotation();
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, dir.ToRotation() - MathHelper.PiOver2);

            if (Projectile.ai[0] < ChargeTicks + 1)
                Projectile.ai[0]++;

            if (Charged && !rang)
            {
                rang = true;

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.4f }, hand);
                    PADParticleHandler.SpawnParticle(new BloomGeneric(hand, Vector2.Zero, MeagreFlameBall.Yellow, 0.3f, 10));
                }
            }

            float glow = 0.3f + 0.7f * Charge;
            Lighting.AddLight(hand, 1f * glow, 0.5f * glow, 0.15f * glow);

            if (!Main.dedServ)
                Gather(hand);

            if (Projectile.owner == Main.myPlayer && !player.channel)
            {
                Release(player, hand, dir);
                Projectile.Kill();
            }
        }

        /// <summary>Embers drawn in from the air around the hand, more Of Them the longer it is held.</summary>
        private void Gather(Vector2 hand)
        {
            int count = Charged ? 3 : (Main.rand.NextFloat() < 0.4f + 0.6f * Charge ? 1 : 0) + (Charge > 0.5f ? 1 : 0);
            float radius = 26f + 14f * Charge;

            for (int i = 0; i < count; i++)
            {
                Vector2 from = hand + Main.rand.NextVector2CircularEdge(radius, radius) * Main.rand.NextFloat(0.8f, 1.1f);
                int life = Main.rand.Next(12, 18);
                Vector2 vel = (hand - from) / (life * 0.8f);
                Color colour = Main.rand.NextBool(3) ? MeagreFlameBall.Yellow : MeagreFlameBall.Orange;

                PADParticleHandler.SpawnParticle(new Ember(from, vel, colour,
                    Main.rand.NextFloat(0.35f, 0.7f) * (1f + Charge * 0.5f), life, lift: 0f));
            }

            if (Charged && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(hand + Main.rand.NextVector2Circular(8f, 8f), DustID.Torch,
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
        }

        /// <summary>Throws the ball. Owner only.</summary>
        private void Release(Player player, Vector2 hand, Vector2 dir)
        {
            Item item = player.HeldItem;
            Castable cast = (item.ModItem as Catalyst)?.Selected(player);

            int extra = ChargedMana - (cast?.Mana ?? 0);
            bool big = Charged && (extra <= 0 || player.CheckMana(extra, true, false));

            int damage = big ? Projectile.damage * BigDamageTimes : Projectile.damage;
            float speed = big ? 9f : 10f;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), hand, dir * speed,
                ModContent.ProjectileType<MeagreFlameBall>(), damage, Projectile.knockBack, player.whoAmI, big ? 1f : 0f);

            // The real use time starts now, not on the click.
            player.itemTime = item.useTime;
            player.itemAnimation = item.useAnimation;

            SoundEngine.PlaySound(SoundID.Item20 with
            {
                Volume = big ? 0.9f : 0.7f,
                Pitch = big ? -0.35f : 0f,
                PitchVariance = 0.1f,
            }, hand);

            if (!Main.dedServ)
            {
                float s = big ? 1.6f : 1f;

                PADParticleHandler.SpawnParticle(new BloomGeneric(hand, dir * 2f, MeagreFlameBall.Orange, 0.35f * s, 10));

                for (int i = 0; i < (big ? 12 : 6); i++)
                {
                    Vector2 vel = dir.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 4f) * s;

                    PADParticleHandler.SpawnParticle(new Ember(hand, vel, MeagreFlameBall.Orange,
                        Main.rand.NextFloat(0.4f, 0.8f) * s, Main.rand.Next(14, 24)));
                }

                MuzzleSmoke.Spawn(hand, dir, big ? 5 : 3);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.ai[0] <= 0f)
                return false;

            Texture2D bloom = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 origin = bloom.Size() * 0.5f;

            // Grows with the charge; once full, a slow breath so it reads as ready.
            float c = Charge;
            float breath = Charged ? 1f + 0.1f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 9f) : 1f;
            float size = (0.06f + 0.2f * c) * breath;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(bloom, at, null, MeagreFlameBall.Orange * (0.5f + 0.4f * c), 0f, origin, size, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, at, null, MeagreFlameBall.Yellow * (0.4f + 0.5f * c), 0f, origin, size * 0.6f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, at, null, MeagreFlameBall.Heart * c, 0f, origin, size * 0.3f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
