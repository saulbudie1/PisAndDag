using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The laser Nightcall's laser fist punches out (by request: "punch out laser pierce 1"): a bolt of Skeletron Prime's
    /// green down the blow's line, three moves a tick, through one enemy into a second, out at a wall.
    /// </summary>
    public class NightcallLaser : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.extraUpdates = 2;
            Projectile.timeLeft = Nightcall.LaserLasts;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.35f, 0.5f, 0.02f);

            if (!Main.dedServ && Main.rand.NextBool(9) && Prime.Seen(Projectile.Center, 40f))
                Prime.Add(new HoneyMote(Projectile.Center, Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.4f, 0.4f), PrimeArt.Nuclear, 0.35f, Main.rand.Next(10, 18), 0.01f));
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ || !Prime.Seen(Projectile.Center, 60f))
                return;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Prime.Add(new BloomGeneric(Projectile.Center, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 0.3f, 8));

            for (int i = 0; i < 4; i++)
                PADParticleHandler.SpawnParticle(new SparkShard(Projectile.Center, (-way).RotatedByRandom(1.2f) * Main.rand.NextFloat(2f, 6f), PrimeArt.NuclearHot, 8f, 2f, 22f,
                    Main.rand.Next(6, 11)));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // A bolt of the green, as the Legends Prime's own lasers are drawn.
            Vector2 at = Projectile.Center;
            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            SpriteBatch batch = Main.spriteBatch;
            DestroyerArt.DrawLine(batch, at - way * 48f, at + way * 8f, 12f, PrimeArt.Lit(PrimeArt.Nuclear * 0.5f));
            DestroyerArt.DrawLine(batch, at - way * 34f, at + way * 6f, 4f, PrimeArt.Lit(PrimeArt.NuclearHot));
            DestroyerArt.DrawGlow(batch, at + way * 3f, 18f, PrimeArt.Lit(Color.White * 0.55f), hotCore: true);
            return false;
        }
    }
}
