using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Items.Melee.Fists;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Nightcall's right click (by request: "standstill Charge for 2 sec and explode radius 7 tiles and leaving radiation
    /// trail for 5 sec, throwing player to the direction with least hostile npc and projectile"). ai[0], ai[1]: the way
    /// they are thrown, once it is known (the owner's, told to the others).
    ///
    ///   CHARGE 120   the player planted where they stand (in the air, held there), both fists up before the chest, each
    ///                as its hand is set, lit more and more in Skeletron Prime's green; its green drawn in to them from
    ///                all round, arcs off them, the screen shaking more and more.
    ///   BLAST        seven tiles about them (NightcallBlast, six times the weapon's damage).
    ///   THROW 24     thrown the way with least about it (Clearest): every hostile thing within seventy-five tiles counts
    ///                against the way it lies in, the nearer and more squarely in it the more (a boss twice, a shot a little
    ///                under one), and a way walled in close counts against itself; 22 tiles in 24 ticks, quick off the
    ///                mark, no fall counted. The way behind them radioactive for five seconds (NightcallTrail).
    ///   then the cooldown (NightcallSpent).
    /// </summary>
    public class NightcallCharge : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        private ref float Age => ref Projectile.localAI[0];

        private Vector2 Way => new(Projectile.ai[0], Projectile.ai[1]);

        /// <summary>Where the fists are this tick, which way they point (for the drawing).</summary>
        private Vector2 frontHand, backHand, lastCentre;
        private bool posed;

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            overPlayers.Add(index);
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            int t = (int)++Age;
            bool charging = t <= Nightcall.ChargeTicks;

            if (!player.active || player.dead || (charging && player.HeldItem?.ModItem is not Nightcall))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = player.Center;
            NightcallPlayer hands = player.GetModPlayer<NightcallPlayer>();

            if (charging)
            {
                float u = t / (float)Nightcall.ChargeTicks;
                hands.Planted = 2;
                player.itemTime = Math.Max(player.itemTime, 2);
                player.itemAnimation = Math.Max(player.itemAnimation, 2);
                Pose(player, u, t);
                Charging(player, u, t);

                if (t == Nightcall.ChargeTicks)
                    Go(player);

                return;
            }

            posed = false;
            int since = t - Nightcall.ChargeTicks - 1;

            if (since >= Nightcall.ThrowTicks)
            {
                Projectile.Kill();
                return;
            }

            if (Projectile.owner == Main.myPlayer && Way != Vector2.Zero)
                hands.Thrust(Way * Nightcall.ThrowSpeed(since), since == 0);

            // A wake of its green fire behind them as they go.
            if (!Main.dedServ && lastCentre != Vector2.Zero && Prime.Seen(player.Center, 100f))
                PADFire.Trail(lastCentre, player.Center, 24f, FirePalette.Nuclear, 0.45f, smokes: since % 3 == 0);

            lastCentre = player.Center;
        }

        // ---- the charge -------------------------------------------------------------------------------------------------------

        /// <summary>Both fists up before the chest, raised more as it charges, trembling.</summary>
        private void Pose(Player player, float u, int t)
        {
            float shake = 0.08f * u * MathF.Sin(t * 1.7f);
            ArmPose front = new(FistArms.Guard, -0.55f - 0.35f * u + shake, Vector2.One);
            ArmPose back = new(FistArms.Guard, -0.75f - 0.35f * u - shake, Vector2.One);

            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            fists.Hold(false);
            fists.Hold(true);
            fists.Smear = PrimeArt.Nuclear;
            fists.PoseFront(front, false);
            fists.PoseBack(back, false);

            float up = -1f - 0.3f * u;
            float armRotation = VisusGrip.ArmRotation(player, up);
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRotation);
            player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.ThreeQuarters, armRotation);

            frontHand = FistArms.HandAt(player, false, front);
            backHand = FistArms.HandAt(player, true, back);
            posed = true;
        }

        /// <summary>What the charge looks and sounds like, on every machine.</summary>
        private void Charging(Player player, float u, int t)
        {
            if (Main.dedServ)
                return;

            Vector2 heart = Vector2.Lerp(frontHand, backHand, 0.5f) + new Vector2(0f, -4f);

            if (t == 1)
                SoundEngine.PlaySound(Prime.ChargeSound with { Volume = 0.6f, Pitch = 0.2f }, player.Center);

            Lighting.AddLight(heart, 0.3f + 1.1f * u, 0.45f + 1.4f * u, 0.05f);

            // Its green drawn in to the fists from all round.
            if (Main.rand.NextFloat() < 0.4f + 0.6f * u)
            {
                Vector2 from = heart + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(80f, 140f + 60f * u);
                Prime.Add(new HoneyMote(from, (heart - from) * (0.045f + 0.03f * u), Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    Main.rand.NextFloat(0.35f, 0.65f), Main.rand.Next(18, 26), 0f));
            }

            if (t % (int)MathHelper.Lerp(16f, 6f, u) == 0)
            {
                Vector2 fist = Main.rand.NextBool() ? frontHand : backHand;
                Prime.Arc(fist, fist + Main.rand.NextVector2Circular(26f, 22f) * (0.6f + u), PrimeArt.Nuclear, 7f + 5f * u, Main.rand.Next(10, 16), 1);
            }

            if (Projectile.owner == Main.myPlayer && t % 6 == 0)
                player.PIS().BumpScreenShake(1f + 5f * u * u, 8);
        }

        /// <summary>
        /// At its end, on the owner's machine: the blast, the way they are thrown (told to the others), the radiation left
        /// along it, the cooldown.
        /// </summary>
        private void Go(Player player)
        {
            if (Projectile.owner != Main.myPlayer)
                return;

            var source = Projectile.GetSource_FromThis();
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<NightcallBlast>(), (int)Math.Round(Projectile.damage * Nightcall.BlastDamage),
                Projectile.knockBack, player.whoAmI, Nightcall.BlastReach, NightcallBlast.Core);

            Vector2 way = Clearest(player);
            Projectile.ai[0] = way.X;
            Projectile.ai[1] = way.Y;
            Projectile.netUpdate = true;

            Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<NightcallTrail>(), (int)Math.Round(Projectile.damage * Nightcall.TrailDamage),
                0f, player.whoAmI);

            player.AddBuff(ModContent.BuffType<NightcallSpent>(), Nightcall.Cooldown);
        }

        /// <summary>
        /// The way with least about it (by request: "the direction with least hostile npc and projectile"), out of sixteen:
        /// every hostile NPC and shot within seventy-five tiles counts against the ways it lies toward, the nearer and the more
        /// squarely the more; a boss twice, a shot six tenths. A way walled in before the throw's end counts against itself
        /// as much as it is shut. Even, a little rather up than down, and away from where the player faces.
        /// </summary>
        public static Vector2 Clearest(Player player)
        {
            const int ways = 16;
            const float far = 75f * 16f;
            Vector2 from = player.Center;
            Vector2 best = new(-player.direction, -0.5f);
            float least = float.MaxValue;

            for (int i = 0; i < ways; i++)
            {
                Vector2 dir = (i * MathHelper.TwoPi / ways).ToRotationVector2();
                float against = 0f;

                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (npc.friendly || npc.townNPC || npc.lifeMax <= 5 || npc.damage <= 0)
                        continue;

                    against += Weigh(from, dir, npc.Center, far) * (npc.boss ? 2f : 1f);
                }

                foreach (Projectile shot in Main.ActiveProjectiles)
                {
                    if (!shot.hostile || shot.damage <= 0)
                        continue;

                    against += Weigh(from, dir, shot.Center, far) * 0.6f;
                }

                // Walled in: how much of the throw's way is open.
                float open = Open(player, dir);
                against += (1f - open) * 3f;
                against -= 0.05f * -dir.Y;
                against -= 0.03f * -dir.X * player.direction;

                if (against < least)
                {
                    least = against;
                    best = dir;
                }
            }

            return best.SafeNormalize(-Vector2.UnitY);
        }

        /// <summary>How much something at <paramref name="at"/> counts against <paramref name="dir"/>: more the nearer, more the more squarely in it, nothing behind it.</summary>
        private static float Weigh(Vector2 from, Vector2 dir, Vector2 at, float far)
        {
            Vector2 to = at - from;
            float d = to.Length();

            if (d > far || d < 1f)
                return d < 1f ? 1f : 0f;

            float square = Vector2.Dot(dir, to / d);

            if (square <= 0f)
                return 0f;

            return square * square / (1f + d / 200f);
        }

        /// <summary>How far along the throw's way the player's box could go before something solid, as a share of it.</summary>
        private static float Open(Player player, Vector2 dir)
        {
            const float step = 16f;

            for (float d = step; d <= Nightcall.ThrowFar; d += step)
            {
                Vector2 at = player.position + dir * d;

                if (Collision.SolidCollision(at, player.width, player.height))
                    return (d - step) / Nightcall.ThrowFar;
            }

            return 1f;
        }

        // ---- drawing ------------------------------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            if (!posed)
                return false;

            Player player = Main.player[Projectile.owner];
            float u = Smooth(Age / Nightcall.ChargeTicks);
            NightcallPlayer hands = player.GetModPlayer<NightcallPlayer>();
            SpriteBatch batch = Main.spriteBatch;

            // The fists, pointing up and forward, as each hand is set.
            Vector2 dir = new(player.direction * MathF.Cos(1.1f), -MathF.Sin(1.1f) * player.gravDir);
            float angle = dir.ToRotation();
            SpriteEffects flip = dir.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Fist(batch, Nightcall.FistPath(hands.Right), backHand + dir * 6f, angle, flip, u);
            Fist(batch, Nightcall.FistPath(hands.Left), frontHand + dir * 6f, angle, flip, u);

            // The light between them, growing, quicker as it comes.
            Vector2 heart = Vector2.Lerp(frontHand, backHand, 0.5f) + dir * 8f;
            float beat = 0.88f + 0.12f * MathF.Sin(Age * (0.2f + 0.6f * u));
            DestroyerArt.DrawGlow(batch, heart, (30f + 120f * u) * beat, PrimeArt.Lit(PrimeArt.Nuclear * (0.18f + 0.45f * u)));
            DestroyerArt.DrawGlow(batch, heart, (12f + 40f * u) * beat, PrimeArt.Lit(PrimeArt.NuclearHot * (0.4f + 0.5f * u)), hotCore: true);

            // Streaks of its green drawn in to it from all round, longer and quicker as they near.
            for (int k = 0; k < 14; k++)
            {
                int tick = (int)Age + k * 3;
                int round = tick / 22;
                float phase = tick % 22 / 22f;
                float a = Hash(round * 131 + k * 17 + Projectile.identity) * MathHelper.TwoPi;
                float reach = 70f + 110f * u + 60f * Hash(round * 7 + k * 53);
                float near = reach * (1f - phase * phase);
                float length = 10f + 40f * phase;
                Vector2 way = a.ToRotationVector2();
                float alpha = MathF.Sin(phase * MathHelper.Pi) * (0.12f + 0.55f * u);
                DestroyerArt.DrawLine(batch, heart + way * (near + length), heart + way * near, 2.5f, PrimeArt.Lit(PrimeArt.Nuclear * alpha));
            }

            return false;
        }

        private static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        private static void Fist(SpriteBatch batch, string path, Vector2 at, float angle, SpriteEffects flip, float u)
        {
            if (!ModContent.HasAsset(path))
                return;

            Texture2D fist = ModContent.Request<Texture2D>(path).Value;
            Vector2 origin = new(fist.Width * 0.5f, fist.Height * 0.5f);
            Color lit = Color.Lerp(Lighting.GetColor(at.ToTileCoordinates()), Color.White, 0.3f * u);
            Main.EntitySpriteDraw(fist, at - Main.screenPosition, null, lit, angle, origin, 0.8f, flip, 0);

            // Lit through with the green as it charges.
            Color glow = PrimeArt.Lit(PrimeArt.Nuclear) * (0.45f * u);
            Main.EntitySpriteDraw(fist, at - Main.screenPosition, null, glow, angle, origin, 0.8f + 0.08f * u, flip, 0);
        }
    }
}
