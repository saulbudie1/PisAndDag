using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Armor;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The gust of sand that throws the Sandclad mage to the cursor (SandcladPlayer), seen: the way they went, kept as they
    /// go, drawn as ribbons of sand streaming along it, weaving over one another, with the game's sandnado's swirl turning
    /// along it, thinning from its far end; grains and dust thrown off behind. It hurts nothing. Gone half a second after.
    /// </summary>
    public class SandcladGust : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandnadoFriendly;

        private const int After = 30;

        private ref float Time => ref Projectile.localAI[0];

        private readonly List<Vector2> way = new();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Sandclad.GustTicks + After;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Time++;
            Player player = Main.player[Projectile.owner];

            if (Time <= Sandclad.GustTicks + 1 && player.active && !player.dead)
            {
                if (way.Count == 0 || Vector2.Distance(way[^1], player.Center) > 6f)
                    way.Add(player.Center);

                Projectile.Center = player.Center;

                if (!Main.dedServ)
                {
                    Vector2 back = -player.velocity.SafeNormalize(Vector2.UnitX);

                    for (int i = 0; i < 4; i++)
                        PADParticleHandler.SpawnParticle(new SandGrain(player.Center + Main.rand.NextVector2Circular(14f, 20f), back.RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 9f),
                            Harenaeum.AnySand, Main.rand.Next(16, 30), Main.rand.NextFloat(1f, 2.2f)));

                    PADParticleHandler.SpawnParticle(new DustFog(player.Center + Main.rand.NextVector2Circular(10f, 16f), back * Main.rand.NextFloat(0.5f, 2f), Harenaeum.Sand[2],
                        Main.rand.Next(30, 50), Main.rand.NextFloat(0.6f, 1f), 0.3f));
                }
            }
        }

        private Vector2 Along(float f)
        {
            float at = MathHelper.Clamp(f, 0f, 1f) * (way.Count - 1);
            int i = Math.Min((int)at, way.Count - 2);
            return Vector2.Lerp(way[i], way[i + 1], at - i);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (way.Count < 2)
                return false;

            float left = MathHelper.Clamp(Projectile.timeLeft / (float)After, 0f, 1f);
            Vector2 across = (way[^1] - way[0]).SafeNormalize(Vector2.UnitX);
            across = new Vector2(-across.Y, across.X);

            // The swirl turning along the way, biggest where they are, thinning behind.
            Texture2D swirl = TextureAssets.Projectile[Type].Value;
            Vector2 origin = swirl.Size() * 0.5f;
            int stamps = Math.Max(2, way.Count);

            for (int i = 0; i < stamps; i++)
            {
                float f = i / (float)(stamps - 1);
                Color c = new Color(214, 184, 112) * (0.4f * left * f);
                c.A = (byte)(c.A * 0.5f);
                Main.EntitySpriteDraw(swirl, Along(f) - Main.screenPosition, null, c, -Time * 0.2f + i * 0.7f, origin, 0.25f + 0.45f * f, SpriteEffects.None, 0);
            }

            // Five ribbons along it, weaving.
            for (int k = 0; k < 5; k++)
            {
                float phase = k * 1.9f + Time * 0.25f;
                float wide = 6f + 5f * k;

                Sandclad.Ribbon(Main.spriteBatch, f => Along(f) + across * MathF.Sin(f * 7f + phase) * wide * (0.3f + 0.7f * f), 4.5f, left * (0.9f - 0.1f * k), 22);
            }

            return false;
        }
    }
}
