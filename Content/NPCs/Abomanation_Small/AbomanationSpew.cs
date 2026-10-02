using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// What Amalgaermis throws: the bones, the skulls and the hands it brings up
    /// (AbomanationHead.HeaveSpit), and the ash its arms fling (AbomanationHead.ThrowAsh).
    ///
    /// They are all the same thing in the air: thrown on an arc, tumbling, falling at
    /// Abomanation.SpitGravity. They break on ground and on platforms alike. None of them
    /// has art of its own: the bones, the skull and the ash ball are vanilla's, and the
    /// hand is one of the animal's own arms.
    ///
    /// Made by the server (or the one player); every machine flies its own copy, and each
    /// player's own machine decides whether its player was hit, as with any hostile
    /// projectile. The numbers they carry are explained at Abomanation.BoneHit.
    /// </summary>
    public abstract class AbomanationThrown : ModProjectile
    {
        /// <summary>How fast it tumbles, in radians a tick.</summary>
        protected virtual float Spin => 0.25f;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = -1;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 420;
        }

        public override void AI()
        {
            Fall();
        }

        /// <summary>Gravity, a ceiling on the fall, and the tumble: the way it is going.</summary>
        protected void Fall()
        {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Abomanation.SpitGravity, Abomanation.SpitFall);
            Projectile.rotation += Spin * (Projectile.velocity.X >= 0f ? 1f : -1f);
        }

        /// <summary>
        /// Platforms stop it as ground does. Left alone, a projectile falls through them.
        /// The box it is stopped by is a little smaller than the one it hits with, so it
        /// does not catch on a corner it only brushed.
        /// </summary>
        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
        {
            width = 10;
            height = 10;
            fallThrough = false;

            return true;
        }

        /// <summary>Drawn turning about its own middle, in the light it is in.</summary>
        protected void DrawTumbling(Texture2D texture, Color lightColor, Rectangle? frame = null)
        {
            Vector2 origin = (frame?.Size() ?? texture.Size()) * 0.5f;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, frame, Projectile.GetAlpha(lightColor),
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// A bone. Out of its mouth, or out of a skull that has just broken (ai[0] 1).
    /// ai[1] is which bone it looks like: there are four in vanilla that will do.
    /// </summary>
    public class AbomanationBone : AbomanationThrown
    {
        /// <summary>How many looks there are.</summary>
        public const int Looks = 4;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        public override void AI()
        {
            Fall();

            // It came out of a mouth full of blood.
            if (!Main.dedServ && Main.rand.NextBool(9))
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(Projectile.Center, Projectile.velocity * 0.2f,
                    Main.rand.Next(20, 34), Main.rand.NextFloat(0.4f, 0.7f), Abomanation.Blood));
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.4f, Pitch = 0.25f, PitchVariance = 0.3f, MaxInstances = 5 },
                Projectile.Center);

            for (int i = 0; i < 7; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Bone,
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), -Main.rand.NextFloat(0.5f, 3.2f)), 0, default,
                    Main.rand.NextFloat(0.8f, 1.2f));

                dust.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawTumbling(Look((int)Projectile.ai[1]), lightColor);
            return false;
        }

        /// <summary>The four bones: the thrown bone, the skeleton's, the crossed pair, and the one that is picked up.</summary>
        private Texture2D Look(int which)
        {
            switch (((which % Looks) + Looks) % Looks)
            {
                case 1:
                    Main.instance.LoadProjectile(ProjectileID.SkeletonBone);
                    return TextureAssets.Projectile[ProjectileID.SkeletonBone].Value;

                case 2:
                    Main.instance.LoadProjectile(ProjectileID.BoneGloveProj);
                    return TextureAssets.Projectile[ProjectileID.BoneGloveProj].Value;

                case 3:
                    Main.instance.LoadItem(ItemID.Bone);
                    return TextureAssets.Item[ItemID.Bone].Value;

                default:
                    return TextureAssets.Projectile[Type].Value;
            }
        }
    }

    /// <summary>
    /// A skull. Where it lands it breaks, and Abomanation.SkullShards bones come up out of
    /// it and over, each as much a bone as any other.
    /// </summary>
    public class AbomanationSkull : AbomanationThrown
    {
        // The skull vanilla leaves when a skeleton comes apart.
        public override string Texture => "Terraria/Images/Gore_42";

        protected override float Spin => 0.14f;

        public override void SetDefaults()
        {
            base.SetDefaults();

            Projectile.width = 18;
            Projectile.height = 18;
        }

        public override void OnKill(int timeLeft)
        {
            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.7f, Pitch = -0.35f, PitchVariance = 0.2f, MaxInstances = 3 },
                    Projectile.Center);

                for (int i = 0; i < 12; i++)
                {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Bone,
                        new Vector2(Main.rand.NextFloat(-3.2f, 3.2f), -Main.rand.NextFloat(0.5f, 4f)), 0, default,
                        Main.rand.NextFloat(0.9f, 1.4f));

                    dust.noGravity = false;
                }
            }

            // The bones are made where the skull was made.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int bone = ModContent.ProjectileType<AbomanationBone>();
            // A bone out of a skull is to the skull what the numbers in Abomanation say,
            // in the mode this is.
            float share = Main.expertMode
                ? Abomanation.ShardHitExpert / Abomanation.SkullHitExpert
                : Abomanation.ShardHit / Abomanation.SkullHit;

            int damage = Math.Max(1, (int)Math.Round(Projectile.damage * share));

            for (int i = 0; i < Abomanation.SkullShards; i++)
            {
                // Up, fanned, so they come down either side of where it broke: a few
                // tiles off at most, most of a second later.
                float angle = -MathHelper.PiOver2 + (i - (Abomanation.SkullShards - 1) * 0.5f) * 0.35f
                    + Main.rand.NextFloat(-0.12f, 0.12f);

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - new Vector2(0f, 8f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(5f, 6.5f), bone, damage, 0f, Main.myPlayer,
                    1f, Main.rand.Next(AbomanationBone.Looks));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawTumbling(TextureAssets.Projectile[Type].Value, lightColor);
            return false;
        }
    }

    /// <summary>
    /// A hand, with the arm it was on. Where it lands it bursts, and for a few ticks
    /// everything within Abomanation.HandSplash of the place is hit by it.
    ///
    /// ai[0] is 1 once it has burst; that is sent, and every machine makes its own
    /// splash from it. ai[1] is which arm it is.
    ///
    /// Burst, it is kept for half a second with nothing to see and, after the first few
    /// ticks, nothing to be hit by. That is so the machine that made it is still holding
    /// it when the others hear it has burst: gone at once, a late word would make it
    /// again somewhere to burst twice, and an early one would end it before it had.
    /// </summary>
    public class AbomanationHand : AbomanationThrown
    {
        /// <summary>How many looks there are.</summary>
        public const int Looks = 2;

        public override string Texture => Abomanation.Folder + "Arm_On_Bottom_1";

        protected override float Spin => 0.19f;

        private static Asset<Texture2D> longArm;

        private bool Burst => Projectile.ai[0] == 1f;

        /// <summary>Ticks it is kept after it bursts.</summary>
        private const int Linger = 30;

        /// <summary>In the air it hits as anything does. Burst, only for the splash's few ticks.</summary>
        public override bool CanHitPlayer(Player target)
        {
            return !Burst || Projectile.localAI[1] <= Abomanation.HandSplashTicks;
        }

        public override void Unload()
        {
            longArm = null;
        }

        public override void AI()
        {
            if (Burst)
            {
                // Whether this machine saw it land or was told: the splash, once.
                if (Projectile.localAI[0] == 0f)
                {
                    Projectile.localAI[0] = 1f;
                    Projectile.tileCollide = false;
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, Linger);
                    Projectile.Resize((int)(Abomanation.HandSplash * 2f), (int)(Abomanation.HandSplash * 2f));

                    Splatter();
                }

                // Ticks since it burst here: what the splash is measured in.
                Projectile.localAI[1]++;
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Fall();

            if (!Main.dedServ && Main.rand.NextBool(4))
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    Projectile.velocity * 0.25f, Main.rand.Next(22, 38), Main.rand.NextFloat(0.45f, 0.8f), Abomanation.Blood));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!Burst)
            {
                Projectile.ai[0] = 1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
            }

            return false;
        }

        /// <summary>What the splash looks and sounds like: up and out in a low fan, and a mist of it.</summary>
        private void Splatter()
        {
            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center;

            SoundEngine.PlaySound(Abomanation.SplatSound, at);

            for (int i = 0; i < 30; i++)
            {
                float angle = -MathHelper.PiOver2 + Main.rand.NextFloat(-1.35f, 1.35f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(at + Main.rand.NextVector2Circular(8f, 5f),
                    angle.ToRotationVector2() * Main.rand.NextFloat(2f, 8.5f),
                    Main.rand.Next(28, 50), Main.rand.NextFloat(0.6f, 1.2f), Abomanation.Blood));
            }

            for (int i = 0; i < 4; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodFume(at + Main.rand.NextVector2Circular(14f, 6f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.4f, 1.6f)),
                    Main.rand.NextFloat(0.18f, 0.3f), Main.rand.Next(24, 40)));
            }

            for (int i = 0; i < 10; i++)
                Dust.NewDust(at - new Vector2(12f), 24, 24, DustID.Blood, Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Burst, there is no hand left to draw.
            if (Burst)
                return false;

            Texture2D texture = TextureAssets.Projectile[Type].Value;

            if ((int)Projectile.ai[1] % Looks == 1)
            {
                longArm ??= ModContent.Request<Texture2D>(Abomanation.Folder + "Arm_On_Bottom_3", AssetRequestMode.ImmediateLoad);
                texture = longArm.Value;
            }

            DrawTumbling(texture, lightColor);
            return false;
        }
    }

    /// <summary>
    /// A ball of ash, off one of its hands. It goes up a long way and comes down. Where it
    /// comes down, on ground or on a platform, it is dust: it leaves no block, as the sand
    /// and ash that fall of themselves do.
    /// </summary>
    public class AbomanationAsh : AbomanationThrown
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.AshBallFalling;

        protected override float Spin => 0.2f;

        private static readonly Color Dull = new Color(92, 84, 96);

        public override void AI()
        {
            Fall();

            if (!Main.dedServ && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f), DustID.Ash,
                    Projectile.velocity * 0.15f, 120, default, Main.rand.NextFloat(0.7f, 1.1f));

                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = -0.4f, PitchVariance = 0.3f, MaxInstances = 6 },
                Projectile.Center);

            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Ash,
                    new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(0.4f, 3.4f)), 60, default,
                    Main.rand.NextFloat(0.9f, 1.5f));

                dust.noGravity = false;
            }

            PADParticleHandler.SpawnParticle(new DustFog(Projectile.Center - new Vector2(0f, 4f),
                new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.3f, 1f)), Dull,
                Main.rand.Next(50, 80), Main.rand.NextFloat(0.4f, 0.6f), 0.3f));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawTumbling(TextureAssets.Projectile[Type].Value, lightColor);
            return false;
        }
    }
}
