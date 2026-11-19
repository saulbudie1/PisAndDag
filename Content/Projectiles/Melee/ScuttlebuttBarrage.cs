using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Items.Melee.Fists;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Scuttlebutt's barrage (by request: "Right click stand still and perform a 3 second barrage of water fist"; "during
    /// 3 second, each second punch 3 flying fist at cursor, alternating hands, 8 second cool down"; then "nah make every
    /// second is 7 punches"). Three seconds: the
    /// player planted where they stand (ScuttlebuttPlayer, and nothing to carry them sideways), facing the cursor; seven
    /// times a second an arm punches out at the cursor, the front then the back in turn, and as it comes out a fist of
    /// water leaves the hand for the cursor (ScuttlebuttWaterFist) for the weapon's damage; the arm comes back to the
    /// guard. Twenty-one in all. When it ends, the player is winded eight seconds (Winded). Hits nothing itself. The arms
    /// are the player's own, posed (FistPlayer); on each hand that shows, Scuttlebutt's glove (by request: "make him
    /// still wear the gloves on right click"), small at the guard and bigger as it punches, drawn over the arms.
    /// </summary>
    public class ScuttlebuttBarrage : ModProjectile
    {
        public override string Texture => "Terraria/Images/MagicPixel";

        /// <summary>How long it lasts; how many fists a second (one every 8 4/7 ticks); ticks from a punch starting to its fist leaving the hand.</summary>
        public const int Lasts = 180;
        public const int PerSecond = 7;
        private const int Leaves = 1;

        /// <summary>The punch's arm, by ticks since it started: out, held, back.</summary>
        private const int Out = 2, Held = 4, Back = 8;

        /// <summary>The tick punch <paramref name="k"/> starts on, counting from 0.</summary>
        private static int StartOf(int k) => (k * 60 + PerSecond - 1) / PerSecond;

        private const float Speed = 14f;

        private const float GuardTurn = -0.55f;

        private float Time
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
            Projectile.timeLeft = Lasts + 2;
            DrawHeldProjInFrontOfHeldItemAndArms = true;
        }

        /// <summary>The gloves this tick, for the drawing: each hand, the way it points, how big; the back one only while it punches.</summary>
        private Vector2 frontHand, backHand;
        private float frontTurn, backTurn, frontSize, backSize;
        private bool backShows;

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A world angle as if the player faced right and stood the right way up.</summary>
        private static float Upright(Player player, float world)
        {
            float upright = player.direction == 1 ? world : MathHelper.Pi - world;
            return player.gravDir == -1f ? -upright : upright;
        }

        /// <summary>How far out the punching arm is, <paramref name="since"/> ticks after it started.</summary>
        private static float Extension(int since)
        {
            if (since < Out)
                return Smooth(since / (float)Out);

            if (since < Held)
                return 1f;

            return 1f - Smooth((since - Held) / (float)(Back - Held));
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead || player.CCed || player.HeldItem?.ModItem is not Scuttlebutt)
            {
                Projectile.Kill();
                return;
            }

            Time++;

            if (Time > Lasts)
            {
                Projectile.Kill();
                return;
            }

            // At the cursor, as it moves.
            if (Projectile.owner == Main.myPlayer)
            {
                Vector2 aim = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Projectile.velocity);

                if (Vector2.Dot(aim, Projectile.velocity) < 0.995f)
                    Projectile.netUpdate = true;

                Projectile.velocity = aim;
            }

            int facing = Projectile.velocity.X >= 0f ? 1 : -1;
            float angle = Projectile.velocity.ToRotation();

            // Planted: nothing carries them sideways; the item busy.
            player.ChangeDir(facing);
            player.velocity.X = 0f;
            player.GetModPlayer<ScuttlebuttPlayer>().Planted = 2;
            player.itemTime = Math.Max(player.itemTime, 2);
            player.itemAnimation = Math.Max(player.itemAnimation, 2);
            Projectile.Center = player.MountedCenter;
            player.heldProj = Projectile.whoAmI;

            int tick = (int)Time - 1;
            int thrown = tick * PerSecond / 60;

            if (StartOf(thrown) > tick)
                thrown--;

            int since = tick - StartOf(thrown);
            bool back = thrown % 2 == 1;
            float e = Extension(since);

            // The punching arm, out at the cursor and back; the front arm keeps the guard while the back one punches.
            float aimUp = Upright(player, angle);
            ArmPose punch = ArmPose.Aimed(FistArms.StraightOut, aimUp, new Vector2(MathHelper.Lerp(0.7f, 1.25f, e), MathHelper.Lerp(1.06f, 0.88f, e)));
            ArmPose guard = new ArmPose(FistArms.Guard, GuardTurn + 0.05f * (float)Math.Sin(Time * 0.3f), Vector2.One);
            bool smear = since > 0 && since < Held;
            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            fists.Smear = Scuttlebutt.Water;

            float armRotation = VisusGrip.ArmRotation(player, aimUp);
            Player.CompositeArmStretchAmount stretch = e > 0.6f ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter;

            ArmPose front = !back && e > 0.05f ? punch : guard;
            backShows = back && e > 0.05f;

            if (backShows)
            {
                fists.PoseBack(punch, smear);
                player.SetCompositeArmBack(true, stretch, armRotation);
            }

            fists.PoseFront(front, !back && smear);

            if (!back)
                player.SetCompositeArmFront(true, stretch, armRotation);

            // The gloves: at each hand, pointing from the shoulder through it; a punching one along the aim.
            frontHand = FistArms.HandAt(player, false, front);
            frontTurn = !back && e > 0.05f ? angle : (frontHand - FistArms.ShoulderAt(player, false)).ToRotation();
            frontSize = !back ? MathHelper.Lerp(Glove, GlovePunching, e) : Glove;
            backHand = FistArms.HandAt(player, true, punch);
            backTurn = angle;
            backSize = MathHelper.Lerp(Glove, GlovePunching, e);

            // The fist of water, leaving the hand as the arm comes out.
            if (since == Leaves)
            {
                Vector2 hand = FistArms.HandAt(player, back, punch);

                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.55f, Pitch = 0.2f + (back ? -0.1f : 0.1f), PitchVariance = 0.1f }, hand);

                if (Projectile.owner == Main.myPlayer)
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), hand, Projectile.velocity * Speed, ModContent.ProjectileType<ScuttlebuttWaterFist>(),
                        Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
        }

        public override void OnKill(int timeLeft)
        {
            Player player = Main.player[Projectile.owner];

            if (Projectile.owner == Main.myPlayer && player.active && !player.dead)
                player.AddBuff(ModContent.BuffType<Winded>(), Winded.Ticks);
        }

        /// <summary>How big the glove is at the guard, and punching (FistPunch's fist is 0.55 at the hand, 1 at full reach).</summary>
        private const float Glove = 0.6f, GlovePunching = 0.85f;

        public override bool PreDraw(ref Color lightColor)
        {
            Scuttlebutt weapon = ModContent.GetInstance<Scuttlebutt>();

            if (Time <= 0f || !ModContent.HasAsset(weapon.FistTexture))
                return false;

            Texture2D glove = ModContent.Request<Texture2D>(weapon.FistTexture).Value;
            Vector2 origin = new Vector2(glove.Width * 0.5f, glove.Height * 0.5f);
            SpriteEffects flip = Projectile.velocity.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;

            if (backShows)
                Main.EntitySpriteDraw(glove, backHand - Main.screenPosition, null, Lighting.GetColor(backHand.ToTileCoordinates()), backTurn, origin, backSize, flip, 0);

            Main.EntitySpriteDraw(glove, frontHand - Main.screenPosition, null, Lighting.GetColor(frontHand.ToTileCoordinates()), frontTurn, origin, frontSize, flip, 0);
            return false;
        }
    }

    /// <summary>
    /// A fist of water from Scuttlebutt's barrage: Scuttlebutt's fist, in water's blues (by request: "same texture as the
    /// fist but threshold it to black and white and map the brightness to a range from #cafafc, #83add4, #4c6fb5"),
    /// thrown straight at the cursor. It strikes one thing and bursts, or bursts on what is solid; water drips off it as
    /// it goes and a faint wake of it trails behind.
    /// </summary>
    public class ScuttlebuttWaterFist : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 50;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.velocity.X >= 0f ? 1 : -1;
            Lighting.AddLight(Projectile.Center, 0.12f, 0.24f, 0.36f);

            if (!Main.dedServ && Main.rand.NextBool(2))
            {
                Dust drop = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Water, -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, 0, default, 1.1f);
                drop.noGravity = Main.rand.NextBool(2);
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.35f, Pitch = 0.3f, PitchVariance = 0.15f }, Projectile.Center);

            for (int i = 0; i < 12; i++)
            {
                Vector2 velocity = -Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedByRandom(1.1f) * Main.rand.NextFloat(1f, 4f);
                Dust.NewDustPerfect(Projectile.Center, i % 3 == 0 ? DustID.DungeonWater : DustID.Water, velocity, 0, default, Main.rand.NextFloat(1f, 1.5f));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D fist = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(fist.Width * 0.75f, fist.Height * 0.5f);
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            // Water carries a little light of its own: never darker than half lit.
            Color lit = Color.Lerp(lightColor, Color.White, 0.5f);

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                Color wake = lit * (0.35f - i * 0.07f);
                wake.A = 0;
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(fist, at, null, wake, Projectile.oldRot[i], origin, Projectile.scale * (1f - i * 0.08f), flip, 0);
            }

            Main.EntitySpriteDraw(fist, Projectile.Center - Main.screenPosition, null, lit, Projectile.rotation, origin, Projectile.scale, flip, 0);
            return false;
        }
    }
}
