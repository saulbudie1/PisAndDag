using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Mini Sandstorm's tornado (Items.Mage.Incantation.MiniSandstorm). Drawn as the game draws its own sandnado
    /// (projectile 656, whose picture it borrows): its swirl drawn again and again up the column, five pixels apart,
    /// each turned on from the last and wider than the one under it; but smaller, by request (half as wide at the
    /// foot, two thirds at the head), and stood on the ground as a dust devil is, not hung in the air: its foot thick,
    /// its head thinning out. Twelve tiles high at most, less under a roof.
    ///
    /// It goes after the nearest enemy within 40 tiles along the ground, never faster than 2.2 pixels a tick, over
    /// steps but not up walls; with nothing to go after it slows and stands. It grows up out of the ground in a third
    /// of a second and dies down over the last two thirds of one (and at once, when it is cast again).
    ///
    /// What is in it is hurt every 24 ticks; and, where the world is run, pulled to its middle, carried up, the
    /// lighter the faster, and thrown out sideways from its top, to fall and be hurt by the fall (MiniSandstormFall).
    /// How light: how far the game would knock it back (knockBackResist), less for the bigger; a boss, a thing the
    /// game will not knock back, a thing that flies or goes through walls, is not carried at all, only hurt.
    /// </summary>
    public class MiniSandstormTornado : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandnadoFriendly;

        /// <summary>Six seconds; a third of a second to grow, two thirds to die down. Mine.</summary>
        public const int Life = 360, Grow = 20, Die = 40;

        /// <summary>Twelve tiles at most; its middle, where it hurts and carries, 3 tiles across. Mine.</summary>
        public const float Tallest = 12f * 16f, Width = 48f;

        /// <summary>What it goes after, and how fast. Mine.</summary>
        public const float Seek = 40f * 16f, Fastest = 2.2f, Accel = 0.08f;

        /// <summary>The swirl's spacing up the column (the game's own), and its scale at the foot and at the head.</summary>
        private const float Step = 5.1f, FootScale = 0.45f, HeadScale = 1.25f;

        private ref float Time => ref Projectile.ai[0];

        /// <summary>Ticks left of dying down early, when cast again; 0 when not.</summary>
        private ref float Dying => ref Projectile.ai[1];

        /// <summary>Where its foot is, on the ground: the bottom of what it hurts, kept by the owner's machine and told to the rest now and then.</summary>
        private Vector2 Foot
        {
            get => Projectile.Bottom;
            set => Projectile.Bottom = value;
        }

        private float height = 16f;
        private float drift;
        private bool begun;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults()
        {
            Projectile.width = (int)Width;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life + 2;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>Every tornado of a player's dies down: one at a time.</summary>
        public static void DieDownAll(int owner)
        {
            int type = ModContent.ProjectileType<MiniSandstormTornado>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == owner && p.type == type && p.ai[1] == 0f)
                {
                    p.ai[1] = Die;
                    p.netUpdate = true;
                }
            }
        }

        /// <summary>How strong it is now, 0 to 1: growing, whole, dying down.</summary>
        private float Strength
        {
            get
            {
                float grow = MathHelper.Clamp(Time / Grow, 0f, 1f);
                float left = Dying > 0f ? Dying / Die : MathHelper.Clamp((Life - Time) / Die, 0f, 1f);
                return Math.Min(grow, left);
            }
        }

        public override void AI()
        {
            if (!begun)
            {
                // Cast with its middle on the ground: stood on it.
                begun = true;
                Vector2 ground = Projectile.Center;
                Projectile.height = 16;
                Foot = ground;
                SoundEngine.PlaySound(SoundID.Item82 with { Volume = 0.6f }, ground);
            }

            Time++;

            if (Dying > 0f && --Dying <= 0f || Time >= Life)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;

            Go();

            // How high: up to its tallest, or to the roof over it.
            Point foot = Foot.ToTileCoordinates();
            float roof = Tallest;

            for (int i = 2; i <= (int)(Tallest / 16f); i++)
            {
                if (WorldGen.SolidTile(foot.X, foot.Y - i))
                {
                    roof = (i - 1) * 16f;
                    break;
                }
            }

            float strength = Strength;
            height = Math.Max(16f, roof * MathHelper.Clamp(Time / Grow, 0f, 1f));

            // What it hurts: its middle, all the way up.
            Vector2 standing = Foot;
            Projectile.width = (int)Width;
            Projectile.height = (int)height;
            Foot = standing;

            if (Projectile.owner == Main.myPlayer && (int)Time % 20 == 0)
                Projectile.netUpdate = true;

            if (strength > 0.4f)
                Carry(strength);

            if (!Main.dedServ)
                Effects(strength);

            Lighting.AddLight(Foot - new Vector2(0f, height * 0.4f), Harenaeum.Sand[2].ToVector3() * 0.25f * strength);
        }

        /// <summary>After the nearest enemy along the ground: toward it, easing off as it gets there, over steps, never up a wall; slowing to a stand when there is none.</summary>
        private void Go()
        {
            NPC nearest = null;
            float best = Seek;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, Foot);

                if (distance < best)
                {
                    best = distance;
                    nearest = npc;
                }
            }

            // Toward it, slowing as it comes up to it, so that it stops over it rather than passing it by.
            float want = nearest != null ? MathHelper.Clamp((nearest.Center.X - Foot.X) * 0.05f, -Fastest, Fastest) : 0f;
            drift += MathHelper.Clamp(want - drift, -Accel, Accel);

            if (Math.Abs(drift) < 0.01f)
                return;

            // The ground a little ahead: down onto it, up a step of up to three tiles; a wall stops it.
            Vector2 ahead = Foot + new Vector2(drift, -3f * 16f);

            if (CourtOfHandsRite.FindGround(ahead, out Vector2 ground) && ground.Y - Foot.Y > -3f * 16f - 1f && ground.Y - Foot.Y < 20f * 16f)
                Foot = new Vector2(ahead.X, Math.Min(ground.Y, Foot.Y + 8f));
            else
                drift = 0f;
        }

        /// <summary>
        /// What is in it, marked as carried on every machine (for the fall: MiniSandstormFall); and where the world is
        /// run, pulled to its middle and carried up, the lighter the faster, and thrown out from its top, told to the
        /// others now and then, not every tick.
        /// </summary>
        private void Carry(float strength)
        {
            Rectangle column = new Rectangle((int)(Foot.X - Width * 0.5f), (int)(Foot.Y - height), (int)Width, (int)height);
            bool run = Main.netMode != NetmodeID.MultiplayerClient;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile) || !column.Intersects(npc.Hitbox))
                    continue;

                float weight = MiniSandstormFall.Weight(npc);

                if (weight <= 0f)
                    continue;

                npc.GetGlobalNPC<MiniSandstormFall>().Lifted(Projectile.owner, Projectile.damage);

                if (!run)
                    continue;

                // To the middle, going round a little; up, faster the lighter, against the game's own pull down.
                float side = Foot.X - npc.Center.X;
                float round = (float)Math.Sin(Time * 0.15f + npc.whoAmI) * 2.5f;
                npc.velocity.X = MathHelper.Lerp(npc.velocity.X, side * 0.12f + round, 0.2f * strength);

                float rise = -(3f + 5f * weight) * strength;
                npc.velocity.Y = Math.Max(npc.velocity.Y - (0.7f + 0.8f * weight) * strength, rise);

                // Out of its top, sideways, to fall.
                if (npc.Bottom.Y < Foot.Y - height * 0.8f)
                {
                    int away = npc.Center.X >= Foot.X ? 1 : -1;
                    npc.velocity = new Vector2(away * (3.5f + 3f * weight), -2f * weight);
                    npc.netUpdate = true;
                }
                else if ((int)Time % 8 == npc.whoAmI % 8)
                    npc.netUpdate = true;
            }
        }

        private void Effects(float strength)
        {
            // Sand thrown round it and up it, dust kicked up at its foot.
            for (int i = 0; i < 2; i++)
            {
                float up = Main.rand.NextFloat();
                float h = up * height;
                float radius = Width * 0.5f * MathHelper.Lerp(FootScale, HeadScale, up) * 1.1f;
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 at = Foot + new Vector2(side * radius * Main.rand.NextFloat(0.6f, 1f), -h);
                Vector2 velocity = new Vector2(-side * Main.rand.NextFloat(1.5f, 3.5f), -Main.rand.NextFloat(1.5f, 3.5f));
                PADParticleHandler.SpawnParticle(new SandGrain(at, velocity * strength, Harenaeum.AnySand, Main.rand.Next(14, 26), Main.rand.NextFloat(1f, 1.8f)));
            }

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Foot - new Vector2(Width * 0.6f, 6f), (int)(Width * 1.2f), 6, DustID.Sand, Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(1f, 3f), 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = Main.rand.NextBool();
            }

            if ((int)Time % 6 == 0)
            {
                PADParticleHandler.SpawnParticle(new DustFog(Foot + new Vector2(Main.rand.NextFloat(-20f, 20f), -Main.rand.NextFloat(4f, 14f)),
                    new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -0.3f), Harenaeum.Sand[2], Main.rand.Next(30, 50), Main.rand.NextFloat(0.5f, 0.85f) * strength, 0.3f));
            }

            if ((int)Time % 90 == 45)
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 0.25f * strength, Pitch = -0.2f, MaxInstances = 2 }, Foot);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Strength > 0.3f && projHitbox.Intersects(targetHitbox);

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            // Carried, not knocked: the carrying is its own (Carry).
            modifiers.Knockback *= 0f;
        }

        /// <summary>The game's sandnado, smaller and stood on the ground: the swirl again and again up it, turning.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D swirl = TextureAssets.Projectile[Type].Value;
            Rectangle frame = swirl.Frame();
            Vector2 origin = frame.Size() * 0.5f;
            float strength = Strength;
            float spin = -MathHelper.Pi / 50f * Time;
            Color sand = new Color(214, 184, 112);
            float climbed = 0f;

            for (float y = Foot.Y; y > Foot.Y - height; y -= Step)
            {
                climbed += Step;
                float up = climbed / Math.Max(1f, height);
                float turn = climbed * MathHelper.TwoPi / -20f;

                // Thick at the foot, thinning out at the head.
                float alpha = up < 0.75f ? 1f : MathHelper.Clamp((1f - up) / 0.25f, 0f, 1f);
                alpha *= MathHelper.Clamp(up * 8f + 0.35f, 0f, 1f);

                Color c = sand * alpha * strength;
                c.A = (byte)(c.A * 0.5f);

                // The column leans and wavers, more the higher.
                float lean = (float)Math.Sin(Time * 0.07f + up * 3.5f) * 7f * up;
                Vector2 at = new Vector2(Foot.X + lean, y) - Main.screenPosition;

                Main.EntitySpriteDraw(swirl, at, frame, c, spin + turn, origin, MathHelper.Lerp(FootScale, HeadScale, up), SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
