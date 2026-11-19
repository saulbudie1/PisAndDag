using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The radiation Nightcall's charge leaves behind its player as they are thrown (by request: "leaving radiation trail
    /// for 5 sec"): the way they went, kept by every machine as it sees them go, for five seconds after. What stands on
    /// it is hurt every half second (Nightcall.TrailDamage of the weapon's damage) and made Radioactive (its slow, its
    /// drain) for two; never its player, nor anyone else's. Seen: a band of its green along the way, pulsing, motes rising
    /// off it, a thin haze; its light. All of it thinning over its last second.
    /// </summary>
    public class NightcallTrail : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>How far apart the places it keeps are; how thick the way is, to hurt.</summary>
        private const float Every = 18f, Thick = 40f;

        private ref float Age => ref Projectile.localAI[0];

        private readonly List<Vector2> way = new();

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Nightcall.TrailEvery;
            Projectile.timeLeft = Nightcall.ThrowTicks + Nightcall.TrailLasts + 4;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanCutTiles() => false;

        /// <summary>What is left of it: whole until its last second.</summary>
        private float Left => MathHelper.Clamp(Projectile.timeLeft / 60f, 0f, 1f);

        public override void AI()
        {
            Age++;
            Player player = Main.player[Projectile.owner];

            // Kept as its player goes, through the throw.
            if (Age <= Nightcall.ThrowTicks + 3 && player.active && !player.dead)
            {
                Vector2 here = player.Center;

                if (way.Count == 0 || Vector2.Distance(way[^1], here) >= Every)
                    way.Add(here);
            }

            if (way.Count == 0)
                return;

            Projectile.Center = way[^1];

            if (Main.dedServ)
                return;

            float left = Left;
            float pulse = 0.82f + 0.18f * MathF.Sin(Age * 0.11f);

            for (int i = 0; i < way.Count; i += 3)
                Lighting.AddLight(way[i], 0.3f * left * pulse, 0.42f * left * pulse, 0.01f);

            for (int n = 0; n < 2; n++)
            {
                Vector2 at = Along(Main.rand.NextFloat()) + Main.rand.NextVector2Circular(14f, 14f);

                if (Main.rand.NextFloat() < left && Prime.Seen(at, 40f))
                    Prime.Add(new HoneyMote(at, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.2f, 0.9f)),
                        Main.rand.NextBool(4) ? PrimeArt.NuclearHot : PrimeArt.Nuclear, Main.rand.NextFloat(0.3f, 0.55f), Main.rand.Next(40, 80), 0.008f));
            }

            if (Main.rand.NextFloat() < 0.18f * left)
            {
                Vector2 at = Along(Main.rand.NextFloat());

                if (Prime.Seen(at, 80f))
                    Prime.Add(new SteamPuff(at, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.05f, 0.3f)), new Color(150, 204, 40),
                        Main.rand.Next(80, 140), Main.rand.NextFloat(20f, 30f), Main.rand.NextFloat(50f, 80f), Main.rand.NextFloat(0.14f, 0.22f)));
            }
        }

        /// <summary>A place along the way, 0 its start to 1 its end.</summary>
        private Vector2 Along(float f)
        {
            if (way.Count == 1)
                return way[0];

            float at = f * (way.Count - 1);
            int i = Math.Min((int)at, way.Count - 2);
            return Vector2.Lerp(way[i], way[i + 1], at - i);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (way.Count == 0)
                return false;

            float point = 0f;

            if (way.Count == 1)
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), way[0], way[0] + Vector2.UnitY, Thick, ref point);

            for (int i = 0; i < way.Count - 1; i++)
            {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), way[i], way[i + 1], Thick, ref point))
                    return true;
            }

            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => modifiers.DisableKnockback();

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<Radioactive>(), 120);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (way.Count < 2)
                return false;

            float left = Left;
            float pulse = 0.82f + 0.18f * MathF.Sin(Age * 0.11f);

            for (int i = 0; i < way.Count - 1; i++)
            {
                // Fading in from its start a little, so it does not begin as a cut.
                float k = left * pulse * MathHelper.Clamp((i + 1) / 3f, 0f, 1f);
                DestroyerArt.DrawLine(Main.spriteBatch, way[i], way[i + 1], 30f, PrimeArt.Lit(PrimeArt.NuclearDeep * (0.22f * k)));
                DestroyerArt.DrawLine(Main.spriteBatch, way[i], way[i + 1], 12f, PrimeArt.Lit(PrimeArt.Nuclear * (0.16f * k)));
            }

            return false;
        }
    }
}
