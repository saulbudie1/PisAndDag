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

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// One of the two sandstorms the Sandclad summoner keeps (by request: "Create 2 floating sand storms that circles you
    /// (recreate vanilla sandstorm but make it move on air, idle is just it standing still)"; "Press bind to make
    /// sandstorm concentrate at the finger tip, making it rage harder"). ai[0] which of the two.
    ///
    /// The game's sandnado, drawn as the game draws it, hung in the air: it goes round its player, opposite the other,
    /// upright and turning on the spot. On the key (SandcladPlayer.Rage) both go to the cursor and rage there: bigger,
    /// harder, hurting far more often; then back. Summon damage. Its hits keep their own count for each enemy (by request:
    /// "doesnt interfere with other damage"): it neither waits on another hit nor makes one wait.
    /// </summary>
    public class SandcladDevil : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandnadoFriendly;

        private const float Tall = 84f, Wide = 40f, Step = 5.1f;

        private bool Second => Projectile.ai[0] > 0.5f;

        private ref float Time => ref Projectile.localAI[0];

        /// <summary>How far raged it shows, 0 to 1.</summary>
        private float raged;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = (int)Wide;
            Projectile.height = (int)Tall;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Sandclad.DevilEvery;
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
        }

        public override bool? CanCutTiles() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            SandcladPlayer clad = player.GetModPlayer<SandcladPlayer>();

            if (!player.active || player.dead || !clad.SummonSet)
            {
                Projectile.Kill();
                return;
            }

            Time++;
            Projectile.timeLeft = 2;
            // Whether they rage is the owner's to say (its key); told to the rest with the storm.
            if (Projectile.owner == Main.myPlayer)
            {
                float now = clad.Rage > 0 ? 1f : 0f;

                if (now != Projectile.ai[1])
                    Projectile.netUpdate = true;

                Projectile.ai[1] = now;
            }

            bool raging = Projectile.ai[1] > 0.5f;
            raged = MathHelper.Clamp(raged + (raging ? 0.06f : -0.04f), 0f, 1f);
            Projectile.localNPCHitCooldown = raging ? Sandclad.RageEvery : Sandclad.DevilEvery;

            if (Projectile.owner == Main.myPlayer)
            {
                // Round the player, opposite the other; raging, at the cursor, the two a little apart.
                float angle = Main.GameUpdateCount * 0.03f + (Second ? MathHelper.Pi : 0f);
                Vector2 want = raging
                    ? Main.MouseWorld + new Vector2(Second ? 14f : -14f, 0f)
                    : player.Center + new Vector2(MathF.Cos(angle) * 120f, MathF.Sin(angle) * 60f - 20f);

                Vector2 to = want - Projectile.Center;

                if (to.Length() > 2000f)
                    Projectile.Center = want;
                else
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, to * (raging ? 0.22f : 0.16f), 0.3f);

                float damage = player.GetTotalDamage(DamageClass.Summon).ApplyTo(Sandclad.DevilDamage) * (raging ? Sandclad.RageDamage : 1f);
                Projectile.damage = (int)Math.Round(damage);

                if ((int)Time % 12 == 0)
                    Projectile.netUpdate = true;
            }

            if (Main.dedServ)
                return;

            Lighting.AddLight(Projectile.Center, 0.25f + 0.2f * raged, 0.2f + 0.15f * raged, 0.08f);

            // Dust hanging under it, falling away.
            if (Main.rand.NextFloat() < 0.12f + 0.2f * raged)
                PADParticleHandler.SpawnParticle(new DustFog(Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), Tall * 0.45f * Size), new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), 0.5f),
                    Harenaeum.Sand[2], Main.rand.Next(30, 50), Main.rand.NextFloat(0.4f, 0.7f) * Size, 0.28f));

            if (Main.rand.NextFloat() < 0.35f + 0.65f * raged)
            {
                float size = Size;
                Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-Wide, Wide) * 0.5f * size, Main.rand.NextFloat(-Tall, Tall) * 0.5f * size);
                Vector2 round = new Vector2(Main.rand.NextBool() ? 4f : -4f, -Main.rand.NextFloat(0.5f, 2f)) * (1f + raged);
                PADParticleHandler.SpawnParticle(new SandGrain(at, round, Harenaeum.AnySand, Main.rand.Next(14, 26), Main.rand.NextFloat(1f, 2f)));
            }
        }

        private float Size => MathHelper.Lerp(1f, Sandclad.RageSize, raged);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float size = Size;
            Rectangle box = new((int)(Projectile.Center.X - Wide * 0.5f * size), (int)(Projectile.Center.Y - Tall * 0.5f * size), (int)(Wide * size), (int)(Tall * size));
            return box.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.Knockback *= 0.3f;
        }

        /// <summary>The game's sandnado, as the game draws it: its swirl again and again up the column, each turned on from the last, wider toward the top.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D swirl = TextureAssets.Projectile[Type].Value;
            Vector2 origin = swirl.Size() * 0.5f;
            float size = Size;
            float tall = Tall * size;
            float spin = -MathHelper.Pi / 50f * Time * (1f + 1.5f * raged);
            Color sand = Color.Lerp(new Color(214, 184, 112), new Color(255, 214, 130), raged);
            Vector2 foot = Projectile.Center + new Vector2(0f, tall * 0.5f);
            float climbed = 0f;

            // Its head trails behind its foot as it goes.
            float bend = MathHelper.Clamp(-Projectile.velocity.X * 2.2f, -34f, 34f);

            if (raged > 0.01f)
            {
                Color glow = Harenaeum.Gold * (0.28f * raged);
                glow.A = 0;
                global::PisAndDag.Content.NPCs.Mech.DestroyerArt.DrawGlow(Main.spriteBatch, Projectile.Center, tall * 2.1f, glow);
            }

            for (float y = 0f; y < tall; y += Step)
            {
                climbed += Step;
                float up = climbed / tall;
                float alpha = up < 0.75f ? 1f : MathHelper.Clamp((1f - up) / 0.25f, 0f, 1f);
                alpha *= MathHelper.Clamp(up * 6f + 0.2f, 0f, 1f);

                Color c = sand * alpha;
                c.A = (byte)(c.A * 0.5f);

                float lean = MathF.Sin(Time * (0.07f + 0.1f * raged) + up * 3.5f) * (5f + 6f * raged) * up;
                Vector2 at = foot + new Vector2(lean + bend * up * up, -y) - Main.screenPosition;
                Main.EntitySpriteDraw(swirl, at, null, c, spin + climbed * MathHelper.TwoPi / -20f, origin, MathHelper.Lerp(0.3f, 0.95f, up) * size, SpriteEffects.None, 0);
            }

            // Ribbons of sand wound up it, turning with it: three, faster and more of them seen as it rages.
            for (int k = 0; k < 3; k++)
            {
                float turn = Time * (0.09f + 0.12f * raged) + k * MathHelper.TwoPi / 3f;
                float show = 0.5f + 0.5f * raged;

                Sandclad.Ribbon(Main.spriteBatch, f =>
                {
                    float up = 0.08f + 0.8f * f;
                    float a = turn + f * 4.2f;
                    float out_ = (9f + 22f * up) * size;
                    return foot + new Vector2(MathF.Cos(a) * out_ + bend * up * up, -tall * up + MathF.Sin(a) * out_ * 0.25f);
                }, 3.5f * size, show, 16);
            }

            return false;
        }

        /// <summary>Sand thrown off it where it cuts, and a fleck of the metal's turquoise.</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            Harenaeum.Burst(target.Center, new Vector2(Main.rand.NextBool() ? 1f : -1f, -0.6f), 4 + (int)(4 * raged), 6f, 0.9f, 0.5f);

            if (Main.rand.NextBool(3))
            {
                Dust fleck = Dust.NewDustPerfect(target.Center, DustID.TintableDustLighted, Main.rand.NextVector2Circular(3f, 3f), 120, Harenaeum.Turquoise, 0.9f);
                fleck.noGravity = true;
            }
        }
    }
}
