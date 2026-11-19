using System;
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
    /// The sandstorm the Sandclad mage raises by standing still (by request: "Standing still creates a sand storrms that
    /// kills and collect mana points for you"): about the player, nine tiles out, for as long as they stand; what is in
    /// it is hurt every half second (magic), and each hit gives its player mana, a kill more. It dies down when they
    /// move or the set comes off. Seen: sand blown round the player, low whirls of the game's own sandnado turning about
    /// them, its dust. Its hits keep their own count, so they take nothing from any other hit.
    /// </summary>
    public class SandcladStorm : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandnadoFriendly;

        private const int Fade = 20;

        private ref float Time => ref Projectile.localAI[0];

        /// <summary>How much of it there is, 0 to 1: up as it rises, down as it dies.</summary>
        private float strength;

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Sandclad.StormEvery;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanCutTiles() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            SandcladPlayer clad = player.GetModPlayer<SandcladPlayer>();
            bool up = player.active && !player.dead && clad.MageSet && clad.Still > 0;

            strength = MathHelper.Clamp(strength + (up ? 1f : -1f) / Fade, 0f, 1f);

            if (!up && strength <= 0f)
            {
                Projectile.Kill();
                return;
            }

            Time++;
            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;

            if (Projectile.owner == Main.myPlayer)
                Projectile.damage = (int)Math.Round(player.GetTotalDamage(DamageClass.Magic).ApplyTo(Sandclad.StormDamage));

            if (Main.dedServ)
                return;

            // Sand blown round them, the way it turns.
            for (int i = 0; i < 3; i++)
            {
                if (Main.rand.NextFloat() > strength)
                    continue;

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float far = Sandclad.StormReach * MathF.Sqrt(Main.rand.NextFloat(0.15f, 1f));
                Vector2 out_ = angle.ToRotationVector2();
                Vector2 at = player.Center + new Vector2(out_.X * far, out_.Y * far * 0.75f);
                Vector2 round = new Vector2(-out_.Y, out_.X) * Main.rand.NextFloat(3f, 7f) - out_ * 0.6f;
                PADParticleHandler.SpawnParticle(new SandGrain(at, round, Harenaeum.AnySand, Main.rand.Next(18, 34), Main.rand.NextFloat(1f, 2f)));
            }

            if (Main.rand.NextFloat() < 0.25f * strength)
            {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(Sandclad.StormReach, Sandclad.StormReach * 0.7f);
                PADParticleHandler.SpawnParticle(new DustFog(at, new Vector2(Main.rand.NextFloat(-1f, 1f), -0.2f), Harenaeum.Sand[2], Main.rand.Next(40, 70), Main.rand.NextFloat(0.6f, 1.1f), 0.22f));
            }
        }

        public override bool? CanDamage() => strength >= 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 near = Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight());
            return Vector2.Distance(near, Projectile.Center) <= Sandclad.StormReach;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.DisableKnockback();
        }

        /// <summary>Mana for the hit, more for the kill (on its owner's machine, whose mana it is).</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[Projectile.owner];
            int mana = target.life <= 0 ? Sandclad.ManaKill : Sandclad.ManaHit;

            if (player.statMana >= player.statManaMax2)
                return;

            player.statMana = Math.Min(player.statManaMax2, player.statMana + mana);
            player.ManaEffect(mana);

            if (Main.dedServ)
                return;

            // The mana, seen coming in to them: motes of its blue off what was hit.
            Vector2 to = player.Center - target.Center;

            for (int i = 0; i < (target.life <= 0 ? 7 : 3); i++)
            {
                Vector2 from = target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f);
                PADParticleHandler.SpawnParticle(new HoneyMote(from, to / 24f + Main.rand.NextVector2Circular(1.5f, 1.5f), new Color(90, 150, 255), Main.rand.NextFloat(0.45f, 0.75f), 24, 0f));
            }

            Harenaeum.Burst(target.Center, to, 5, 6f, 0.4f, 0.4f);
        }

        /// <summary>Low whirls turning about the player: the game's sandnado's swirl, laid wide and short, six of them going round.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D swirl = TextureAssets.Projectile[Type].Value;
            Vector2 origin = swirl.Size() * 0.5f;
            Player player = Main.player[Projectile.owner];
            Color sand = new Color(214, 184, 112);
            const int whirls = 6;

            // The air about them thick with it: a faint warm haze, and ribbons of sand going round at every height.
            Color haze = Harenaeum.Sand[2] * (0.1f * strength);
            haze.A = 0;
            global::PisAndDag.Content.NPCs.Mech.DestroyerArt.DrawGlow(Main.spriteBatch, player.Center, Sandclad.StormReach * 2.2f, haze);

            for (int k = 0; k < 7; k++)
            {
                float far = Sandclad.StormReach * (0.3f + 0.1f * k);
                float high = -46f + 15f * k + 6f * MathF.Sin(Time * 0.03f + k);
                float speed = 0.06f - 0.004f * k;
                float head = Time * speed + k * 2.4f;
                float span = 1.5f - 0.08f * k;
                Vector2 middle = player.Center + new Vector2(0f, high);
                float show = strength * (0.55f + 0.45f * MathF.Sin(Time * 0.021f + k * 1.3f));

                Sandclad.Ribbon(Main.spriteBatch, f =>
                {
                    float a = head - span * (1f - f);
                    return middle + new Vector2(MathF.Cos(a) * far, MathF.Sin(a) * far * 0.3f + MathF.Sin(a * 3f + k) * 3f);
                }, 5f, show);
            }

            for (int k = 0; k < whirls; k++)
            {
                float angle = Time * 0.035f + k * MathHelper.TwoPi / whirls;
                float far = Sandclad.StormReach * (0.45f + 0.4f * (k % 2));
                Vector2 foot = player.Bottom + new Vector2(MathF.Cos(angle) * far, MathF.Sin(angle) * far * 0.28f);
                float behind = 0.75f + 0.25f * MathF.Sin(angle);

                for (int i = 0; i < 9; i++)
                {
                    float upShare = i / 8f;
                    Color c = sand * (0.5f * strength * behind * (1f - 0.7f * upShare));
                    c.A = (byte)(c.A * 0.5f);
                    Vector2 at = foot + new Vector2(MathF.Sin(Time * 0.08f + i * 0.5f + k) * 3f * upShare, -i * 5.1f) - Main.screenPosition;
                    Main.EntitySpriteDraw(swirl, at, null, c, -Time * 0.07f + i * -0.31f, origin, MathHelper.Lerp(0.3f, 0.8f, upShare), SpriteEffects.None, 0);
                }
            }

            return false;
        }
    }
}
