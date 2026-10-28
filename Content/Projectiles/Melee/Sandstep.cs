using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Corosion's right-click, by request ("sandy flash step back that basically gives them invincibility"): the
    /// player comes apart into sand where they stand, goes back the way they face as a smear of it, ten tiles in nine
    /// ticks, and is put back together at the end. Nothing touches them while they are sand, nor for half a second
    /// after (the game's own invincibility, blinking once they are whole again). Walls stop it; it does not fall.
    ///
    /// Its velocity is only the way it goes. Seen on every machine; moved on the owner's, where the player is.
    /// </summary>
    public class Sandstep : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        /// <summary>Ticks as sand, and how fast it goes. Ten tiles. Mine.</summary>
        public const int Dash = 9;
        public const float Speed = 17.5f;

        /// <summary>Ticks untouchable after it, whole again. Mine.</summary>
        public const int After = 30;

        private int Time
        {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        private int Way => Projectile.velocity.X >= 0f ? 1 : -1;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Dash + 20;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        /// <summary>
        /// Nothing of its own to draw: the sand is particles, the player is hidden (SandstepPlayer). Without this the game
        /// drew its texture, MagicPixel, a strip one pixel wide and a thousand high: a line down the screen.
        /// </summary>
        public override bool PreDraw(ref Color lightColor) => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            int t = Time++;
            bool mine = Projectile.owner == Main.myPlayer;

            if (t == 0)
            {
                if (mine)
                    player.SetImmuneTimeForAllTypes(Math.Max(player.immuneTime, Dash + After));

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.55f, Pitch = 0.6f, PitchVariance = 0.1f }, player.Center);
                    Scatter(player, 30, 1f);
                }
            }

            if (t < Dash)
            {
                player.GetModPlayer<SandstepPlayer>().Sand = 2;

                if (mine)
                {
                    player.immuneNoBlink = true;
                    player.velocity = new Vector2(Way * Speed, 0f);
                    player.fallStart = (int)(player.position.Y / 16f);
                }

                if (!Main.dedServ)
                    Smear(player, t);
            }
            else if (t == Dash)
            {
                if (mine)
                {
                    player.immuneNoBlink = false;
                    player.velocity.X *= 0.2f;
                }

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.5f }, player.Center);
                    Gather(player);
                }
            }

            Projectile.Center = player.Center;
        }

        /// <summary>The player coming apart: grains thrown off their body, and a hanging puff.</summary>
        private static void Scatter(Player player, int grains, float strength)
        {
            for (int i = 0; i < grains; i++)
            {
                Vector2 at = player.position + new Vector2(Main.rand.NextFloat(player.width), Main.rand.NextFloat(player.height));
                Vector2 velocity = (at - player.Center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(1f, 3.5f) * strength
                    + new Vector2(0f, -0.5f);
                PADParticleHandler.SpawnParticle(new SandGrain(at, velocity, Harenaeum.AnySand, Main.rand.Next(20, 40), Main.rand.NextFloat(1f, 2f)));
            }

            for (int i = 0; i < 4; i++)
            {
                PADParticleHandler.SpawnParticle(new DustFog(player.Center + Main.rand.NextVector2Circular(10f, 18f), Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Harenaeum.Sand[2], Main.rand.Next(30, 50), Main.rand.NextFloat(0.6f, 0.9f), 0.35f));
            }
        }

        /// <summary>The player as sand, going: grains where their body is, left behind as it moves on, and a glint now and then.</summary>
        private void Smear(Player player, int t)
        {
            for (int i = 0; i < 14; i++)
            {
                Vector2 at = player.position + new Vector2(Main.rand.NextFloat(player.width), Main.rand.NextFloat(player.height))
                    - player.velocity * Main.rand.NextFloat(0f, 1f);
                Vector2 velocity = new Vector2(Way * Main.rand.NextFloat(0.5f, 3f), Main.rand.NextFloat(-0.8f, 0.5f));
                PADParticleHandler.SpawnParticle(new SandGrain(at, velocity, Harenaeum.AnySand, Main.rand.Next(16, 32), Main.rand.NextFloat(1f, 2.2f)));
            }

            if (t % 3 == 0)
                Harenaeum.Glint(player.Center + Main.rand.NextVector2Circular(10f, 18f), 0.6f);

            PADParticleHandler.SpawnParticle(new DustFog(player.Center + Main.rand.NextVector2Circular(8f, 16f), player.velocity * 0.05f,
                Harenaeum.Sand[2], Main.rand.Next(24, 40), Main.rand.NextFloat(0.45f, 0.75f), 0.3f));

            Lighting.AddLight(player.Center, Harenaeum.PaleGold.ToVector3() * 0.4f);
        }

        /// <summary>Put back together: grains drawn in to where the body will be, and a flash of the metal.</summary>
        private static void Gather(Player player)
        {
            for (int i = 0; i < 24; i++)
            {
                Vector2 from = player.Center + Main.rand.NextVector2CircularEdge(26f, 34f) * Main.rand.NextFloat(0.7f, 1f);
                Vector2 velocity = (player.Center - from) * Main.rand.NextFloat(0.1f, 0.16f);
                PADParticleHandler.SpawnParticle(new SandGrain(from, velocity, Harenaeum.AnySand, Main.rand.Next(10, 18), Main.rand.NextFloat(1f, 1.8f)));
            }

            Harenaeum.Glint(player.Center, 1.1f);
            PADParticleHandler.SpawnParticle(new BloomGeneric(player.Center, Vector2.Zero, Harenaeum.PaleGold * 0.5f, 0.8f, 12));
        }
    }

    /// <summary>A player as sand (Sandstep): not drawn, for as long as they are.</summary>
    public class SandstepPlayer : ModPlayer
    {
        /// <summary>Ticks left as sand. Set by the sandstep each tick it goes, on every machine.</summary>
        public int Sand;

        public override void PostUpdate()
        {
            if (Sand > 0)
                Sand--;
        }

        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (Sand <= 0)
                return;

            foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers)
                layer.Hide();
        }
    }
}
