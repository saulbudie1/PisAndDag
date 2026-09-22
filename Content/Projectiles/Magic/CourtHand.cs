using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// One hand of the Court. Comes up out of the ground, stands a moment, goes back
    /// down. Anything over it while it is up is run through, again every few ticks for
    /// as long as it stays there. Blood runs off the wrist onto the ground the whole
    /// time it is out; blood comes off whatever it goes into.
    ///
    /// ai[0] is the lean, in radians, so three side by side do not stand parallel.
    /// ai[1] is which of the four sprites, plus four if it is mirrored, which makes
    /// eight hands out of four drawings. The ground line is the position it was
    /// spawned at, kept in localAI[1] on every client from the first tick, since the
    /// hitbox moves up from it as the hand rises.
    ///
    /// The glow is the outline method from CCBypass, in this cast's red rather than the
    /// warning yellow: eight copies with no alpha, three pixels out, under the sprite.
    /// </summary>
    public class CourtHand : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Projectiles/Magic/CourtofHands_1";

        public const int Variants = 4;

        /// <summary>Sprites times two, for the mirrored ones.</summary>
        public const int Looks = Variants * 2;

        public const int Rise = 8;
        public const int Stand = 12;
        public const int Sink = 8;
        public const int Life = Rise + Stand + Sink;

        /// <summary>Drawn size, on the 40 pixel sprite.</summary>
        public const float Scale = 1.25f;

        /// <summary>Hitbox width. Wider than the sprite to cover the lean.</summary>
        private const int HitWidth = 18;
        private const int SpriteHeight = 40;

        public static readonly Color Glow = new Color(0xff, 0x29, 0x29);
        /// <summary>The blood itself: the mod's blood red and the cast's brighter one.</summary>
        public static readonly Color BloodDark = new Color(150, 8, 12);
        public static readonly Color BloodBright = new Color(0xb1, 0x25, 0x16);

        private static Asset<Texture2D>[] sprites;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            sprites = new Asset<Texture2D>[Variants];

            for (int i = 0; i < Variants; i++)
                sprites[i] = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Magic/CourtofHands_" + (i + 1), AssetRequestMode.ImmediateLoad);
        }

        public override void Unload() => sprites = null;

        public override void SetDefaults()
        {
            Projectile.width = HitWidth;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;

            // Runs the same creature through again every few ticks while it stands there.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        private float Lean => Projectile.ai[0];
        private int Look => (int)MathHelper.Clamp(Projectile.ai[1], 0, Looks - 1);
        private int Variant => Look % Variants;
        private bool Mirrored => Look >= Variants;

        private float Ground => Projectile.localAI[1];

        /// <summary>How far out of the ground it is, 0 to 1. Fast out, slow back in.</summary>
        private float Emerged
        {
            get
            {
                float t = Projectile.localAI[0];

                if (t < Rise)
                {
                    float x = t / Rise;
                    return 1f - (1f - x) * (1f - x);
                }

                if (t < Rise + Stand)
                    return 1f;

                float back = (t - Rise - Stand) / Sink;
                return 1f - back * back;
            }
        }

        private float VisibleHeight => SpriteHeight * Scale * Emerged;

        public override void AI()
        {
            if (Projectile.localAI[1] == 0f)
            {
                // The point it was spawned on is the ground. Kept before the hitbox is
                // moved off it.
                Projectile.localAI[1] = Projectile.position.Y;
            }

            Projectile.localAI[0]++;

            int height = System.Math.Max(2, (int)VisibleHeight);
            Projectile.height = height;
            Projectile.position.Y = Ground - height;
            Projectile.velocity = Vector2.Zero;

            float glow = 0.5f * Emerged;
            Lighting.AddLight(Projectile.Center, glow, glow * 0.1f, glow * 0.1f);

            if (Main.dedServ)
                return;

            Bleed();
        }

        /// <summary>Blood off the wrist, onto the ground, while it stands.</summary>
        private void Bleed()
        {
            if (Emerged < 0.35f || !Main.rand.NextBool(2))
                return;

            Vector2 wrist = new Vector2(Projectile.Center.X, Ground) + new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(2f, 10f) * Emerged);
            Vector2 vel = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-2.5f, -0.5f));

            PADParticleHandler.SpawnParticle(new BloodDroplet(wrist, vel, Main.rand.Next(30, 46),
                Main.rand.NextFloat(0.6f, 0.9f), Main.rand.NextBool() ? BloodDark : BloodBright));
        }

        public override bool? CanHitNPC(NPC target) => Emerged > 0.25f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            // Run through from below: the blood comes off upward and out.
            Vector2 at = new Vector2(Projectile.Center.X, target.Center.Y);

            for (int i = 0; i < 7; i++)
            {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-6f, -2f));

                PADParticleHandler.SpawnParticle(new BloodDroplet(at + Main.rand.NextVector2Circular(6f, 6f), vel,
                    Main.rand.Next(36, 56), Main.rand.NextFloat(0.7f, 1.1f), Main.rand.NextBool() ? BloodDark : BloodBright));
            }

            for (int i = 0; i < 4; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(6f, 6f), DustID.Blood,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f)), 0, default, Main.rand.NextFloat(1f, 1.4f));
                dust.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (sprites == null || Emerged <= 0.02f)
                return false;

            Texture2D texture = sprites[Variant].Value;
            SpriteEffects flip = Mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Only the part that is out of the ground: the bottom of the sprite is cut
            // off, and what is left is pinned to the ground line by its base.
            int visible = System.Math.Max(1, (int)(SpriteHeight * Emerged));
            Rectangle frame = new Rectangle(0, texture.Height - visible, texture.Width, visible);
            Vector2 origin = new Vector2(texture.Width * 0.5f, visible);
            Vector2 at = new Vector2(Projectile.Center.X, Ground) - Main.screenPosition;

            // The lean comes on with the rise, so it stands up straight and settles over.
            float rotation = Lean * Emerged;

            Color glow = Glow * 0.5f;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 3f;
                Main.EntitySpriteDraw(texture, at + offset, frame, glow, rotation, origin, Scale, flip, 0);
            }

            Main.EntitySpriteDraw(texture, at, frame, lightColor, rotation, origin, Scale, flip, 0);

            return false;
        }
    }
}
