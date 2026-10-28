using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Pocket Sandstorm's yoyo (Items.Melee.PocketSandstorm): the game's yoyo (its AI style 99), out to 15 tiles, for
    /// 20 seconds, by request; and round it, a whirl of sand three tiles across each way.
    ///
    /// The whirl, drawn: grains coming in from its edge to its middle along a spiral, faster the further in, as
    /// streaks along the way they go, dim at the edge where they come in and gone at the middle where they are
    /// taken; a dust hanging round its edge, a pale eye in the middle, a glint of the metal now and then. It opens
    /// out over a third of a second when the yoyo is thrown and closes as it comes back.
    ///
    /// The whirl, hitting: every ten ticks, if anything that can be hurt is in it, a needle of sand is flung
    /// from its edge through that thing and on (PocketSandstormNeedle), the next needle at the next thing, round
    /// and round them. From the owner's machine, as the game's own yoyos make what they throw.
    /// </summary>
    public class PocketSandstormYoyo : ModProjectile
    {
        /// <summary>The whirl's reach from the yoyo, by request: three tiles.</summary>
        public const float Radius = 48f;

        /// <summary>Ticks between needles. Mine.</summary>
        public const int NeedleEvery = 10;

        /// <summary>A needle's share of the yoyo's damage. Mine.</summary>
        public const float NeedleShare = 0.34f;

        private const string GlowSoft = "PisAndDag/Content/Graphics/GlowSoft";

        /// <summary>Grains drawn in the whirl.</summary>
        private const int Grains = 90;

        private int time;
        private float open;
        private int nextTarget;

        public override void SetStaticDefaults()
        {
            // By request: twenty seconds, fifteen tiles. The top speed is the Cascade's (mine).
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Type] = 20f;
            ProjectileID.Sets.YoyosMaximumRange[Type] = 15f * 16f;
            ProjectileID.Sets.YoyosTopSpeed[Type] = 14f;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = ProjAIStyleID.Yoyo;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.scale = 1f;
        }

        /// <summary>Whether the yoyo is on its way back (the game's yoyo sets ai[0] to -1 for it).</summary>
        private bool Returning => Projectile.ai[0] < 0f;

        public override void PostAI()
        {
            time++;
            open = MathHelper.Clamp(open + (Returning ? -0.08f : 0.05f), 0f, 1f);

            if (Projectile.owner == Main.myPlayer && !Returning && open > 0.6f && time % NeedleEvery == 0)
                Fling();

            if (!Main.dedServ)
                Effects();

            Lighting.AddLight(Projectile.Center, Harenaeum.PaleGold.ToVector3() * 0.25f * open);
        }

        /// <summary>A needle through the next thing in the whirl, from its edge on the far side.</summary>
        private void Fling()
        {
            Vector2 centre = Projectile.Center;
            NPC chosen = null;
            int count = 0;

            // Round the things in it: the first after the last one hit, by slot, or the first of all.
            NPC first = null;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile) || !Inside(npc, centre))
                    continue;

                count++;
                first ??= npc;

                if (chosen == null && npc.whoAmI > nextTarget)
                    chosen = npc;
            }

            chosen ??= first;

            if (chosen == null)
                return;

            nextTarget = count > 1 ? chosen.whoAmI : -1;

            Vector2 to = chosen.Center;
            Vector2 way = (to - centre).SafeNormalize(Main.rand.NextVector2Unit());
            Vector2 side = new Vector2(-way.Y, way.X);
            Vector2 from = centre - way * Radius * 0.9f + side * Main.rand.NextFloat(-14f, 14f);
            Vector2 velocity = (to - from).SafeNormalize(way) * 12f;

            Projectile.NewProjectile(Projectile.GetSource_FromThis(), from, velocity, ModContent.ProjectileType<PocketSandstormNeedle>(),
                Math.Max(1, (int)(Projectile.damage * NeedleShare)), 0.5f, Projectile.owner);
        }

        /// <summary>Whether any of <paramref name="npc"/> is within the whirl.</summary>
        public static bool Inside(NPC npc, Vector2 centre)
        {
            Vector2 nearest = Vector2.Clamp(centre, npc.Hitbox.TopLeft(), npc.Hitbox.BottomRight());
            return Vector2.DistanceSquared(nearest, centre) <= Radius * Radius;
        }

        private void Effects()
        {
            if (open <= 0.05f)
                return;

            Vector2 centre = Projectile.Center;

            // Dust hanging round the edge, going round with it.
            if (time % 4 == 0)
            {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 at = centre + a.ToRotationVector2() * Radius * open * Main.rand.NextFloat(0.75f, 1f);
                Vector2 round = (a + MathHelper.PiOver2).ToRotationVector2() * 1.6f;
                PADParticleHandler.SpawnParticle(new DustFog(at, round, Harenaeum.Sand[2], Main.rand.Next(26, 40), Main.rand.NextFloat(0.35f, 0.55f) * open, 0.22f));
            }

            if (time % 14 == 0)
                Harenaeum.Glint(centre + Main.rand.NextVector2Circular(Radius, Radius) * 0.7f * open, 0.55f);

            // A grain thrown off now and then, out of the whirl, as a real one throws them.
            if (Main.rand.NextBool(3))
            {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 at = centre + a.ToRotationVector2() * Radius * open;
                Vector2 velocity = (a + MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(2f, 4f) + a.ToRotationVector2() * 1.5f;
                PADParticleHandler.SpawnParticle(new SandGrain(at, velocity, Harenaeum.AnySand, Main.rand.Next(14, 24), Main.rand.NextFloat(1f, 1.6f)));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (open > 0.02f)
                DrawWhirl();

            return true;
        }

        /// <summary>The grains of the whirl, each on its spiral, worked out from the time: nothing kept, nothing spawned.</summary>
        private void DrawWhirl()
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 centre = Projectile.Center - Main.screenPosition;
            float t = time + Projectile.whoAmI * 37;
            float radius = Radius * open;
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            float lit = Math.Max(0.55f, Math.Max(light.R, Math.Max(light.G, light.B)) / 255f);

            for (int i = 0; i < Grains; i++)
            {
                // Each grain its own start round the circle, its own pace in, its own place in that pace.
                float seed = i * 2.39996f;
                float pace = 0.9f + 0.25f * (float)Math.Sin(i * 12.9898f);
                float p = (float)((i * 0.618034f + t * 0.011f * pace) % 1.0);
                float r = radius * (float)Math.Pow(1f - p, 0.75f);

                // Round faster the further in, a turn and a half from the edge to the middle.
                float angle = seed + t * 0.075f + p * 9.5f;
                Vector2 at = angle.ToRotationVector2() * r;
                at.Y *= 0.92f;

                Vector2 along = (angle + MathHelper.PiOver2 + 0.35f).ToRotationVector2();
                float length = 2f + 7f * p;
                float alpha = (float)Math.Sin(Math.PI * Math.Min(1f, p * 1.15f)) * 0.95f;
                Color c = Harenaeum.Sand[i % Harenaeum.Sand.Length];
                Color drawn = new Color((int)(c.R * lit), (int)(c.G * lit), (int)(c.B * lit)) * alpha;
                float thickness = i % 5 == 0 ? 2f : 1.2f;

                Main.EntitySpriteDraw(pixel, centre + at, new Rectangle(0, 0, 1, 1), drawn, along.ToRotation(), new Vector2(0.5f, 0.5f),
                    new Vector2(length, thickness), SpriteEffects.None, 0);
            }

            // The eye: soft and pale, a little brighter than what is round it.
            Texture2D glow = ModContent.Request<Texture2D>(GlowSoft).Value;
            Color eye = Harenaeum.PaleGold * 0.22f * open;
            eye.A = 0;
            Main.EntitySpriteDraw(glow, centre, null, eye, 0f, glow.Size() * 0.5f, 0.28f * open, SpriteEffects.None, 0);
        }
    }
}
