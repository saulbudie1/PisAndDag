using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Items.Melee.Fists;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// Nightcall's chainsaw, stuck in what it hit (Nightcall; by request: "Punch out and stuck hand inside (leaving the other
    /// hand punching) while inside the NPC, if its small then its stuck at contact point so no hitbox collide, chipp damage
    /// exponential to a ceiling"; asked after: out when the enemy dies or gets out of reach, or left click is let go).
    /// ai[0] what it is in; ai[1] which hand (0 the left, the front arm; 1 the right, the back arm); its velocity where in
    /// it it is stuck, from its middle (a little way in on a big one, at the spot on a small one).
    ///
    /// The arm is held out to it (FistPlayer.Hold, so a blow of the other hand leaves it be), the forearm drawn on from
    /// the hand to the fist, shaking. It touches only what it is in, whatever is around it: a cut every
    /// Nightcall.ChipEvery ticks, each bigger than the last (Nightcall.ChipShare), no knockback. Sparks off it, its
    /// whine, its light.
    /// </summary>
    public class NightcallSaw : ModProjectile
    {
        public override string Texture => Nightcall.FistPath(NightcallFist.Chainsaw);

        private int TargetIndex => (int)Projectile.ai[0];

        private bool Back => Projectile.ai[1] > 0.5f;

        private Vector2 Stuck => Projectile.velocity;

        private ref float Age => ref Projectile.localAI[0];

        /// <summary>Cuts made (its owner's count, told to the others with its sync).</summary>
        private ref float Cuts => ref Projectile.localAI[1];

        /// <summary>Where the hand is, the way to the fist, as last worked out (for its drawing).</summary>
        private Vector2 hand;
        private float angle;
        private bool posed;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Nightcall.ChipEvery;
            Projectile.timeLeft = 2;
            Projectile.hide = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanCutTiles() => false;

        /// <summary>Over the player, as the blows are.</summary>
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            overPlayers.Add(index);
        }

        private NPC Target
        {
            get
            {
                int i = TargetIndex;
                return i >= 0 && i < Main.maxNPCs ? Main.npc[i] : null;
            }
        }

        private static float Upright(Player player, float world)
        {
            float upright = player.direction == 1 ? world : MathHelper.Pi - world;
            return player.gravDir == -1f ? -upright : upright;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            NPC npc = Target;

            if (!player.active || player.dead || player.CCed || player.HeldItem?.ModItem is not Nightcall
                || npc == null || !npc.active || npc.life <= 0 || npc.friendly)
            {
                Projectile.Kill();
                return;
            }

            Age++;
            Projectile.timeLeft = 2;

            Vector2 at = npc.Center + Stuck;
            Projectile.Center = at;

            // Facing it, unless a blow of the other hand is turning the player its own way.
            if (player.ownedProjectileCounts[ModContent.ProjectileType<FistPunch>()] == 0 && Math.Abs(at.X - player.Center.X) > 4f)
                player.ChangeDir(at.X > player.Center.X ? 1 : -1);

            Vector2 shoulder = FistArms.ShoulderAt(player, Back);
            Vector2 to = at - shoulder;

            // Out: let go of (the owner's say), or too far for the arm.
            if (Projectile.owner == Main.myPlayer && (!player.controlUseItem || to.Length() > Nightcall.SawLetGo))
            {
                Projectile.Kill();
                return;
            }

            angle = to.ToRotation();
            float upright = Upright(player, angle);
            float shake = 0.05f * MathF.Sin(Age * 1.9f);
            ArmPose pose = ArmPose.Aimed(FistArms.StraightOut, upright + shake, new Vector2(1.25f, 0.88f));

            FistPlayer fists = player.GetModPlayer<FistPlayer>();
            fists.Hold(Back);
            fists.Smear = Nightcall.Colour(NightcallFist.Chainsaw);

            if (Back)
                fists.PoseBack(pose, false);
            else
                fists.PoseFront(pose, false);

            float armRotation = VisusGrip.ArmRotation(player, upright);

            if (Back)
                player.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, armRotation);
            else
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armRotation);

            hand = FistArms.HandAt(player, Back, pose);
            posed = true;

            Saw(player, at, to.SafeNormalize(Vector2.UnitX));
        }

        /// <summary>What it throws off as it cuts: sparks back toward the player, its light, its whine; a little shake for its owner.</summary>
        private void Saw(Player player, Vector2 at, Vector2 way)
        {
            if (Main.dedServ)
                return;

            float bite = Nightcall.ChipShare(Age / Nightcall.ChipEvery) / Nightcall.ChipCeiling;
            Lighting.AddLight(at, 0.6f + 0.4f * bite, 0.45f + 0.3f * bite, 0.15f);

            if (Prime.Seen(at, 60f))
            {
                for (int i = 0; i < 1 + (int)(2 * bite); i++)
                {
                    Vector2 velocity = (-way).RotatedByRandom(1.1f) * Main.rand.NextFloat(3f, 8f + 5f * bite) + new Vector2(0f, -1f);
                    Color colour = Main.rand.NextBool(4) ? PrimeArt.NuclearHot : new Color(255, Main.rand.Next(170, 225), 100);
                    PADParticleHandler.SpawnParticle(new SparkShard(at, velocity, colour, 10f, 2.5f, 34f, Main.rand.Next(8, 16), falls: true));
                }

                if (Main.rand.NextBool(5))
                    Prime.Add(new MuzzleSmoke(at, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.4f, 1.2f)), Main.rand.Next(20, 32), 0.22f));
            }

            if ((int)Age % 10 == 1)
                SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.45f + 0.2f * bite, Pitch = 0.1f + 0.3f * bite, MaxInstances = 2 }, at);

            if (Projectile.owner == Main.myPlayer && (int)Age % Nightcall.ChipEvery == 0)
                player.PIS().BumpScreenShake(1f + 1.5f * bite, 3);
        }

        // ---- cutting --------------------------------------------------------------------------------------------------------

        public override bool? CanHitNPC(NPC target) => target.whoAmI == TargetIndex ? null : false;

        /// <summary>Only what it is in, wherever its box is: it is inside it.</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => true;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage *= Nightcall.ChipShare(Cuts);
            modifiers.DisableKnockback();
            modifiers.HitDirectionOverride = target.Center.X >= Main.player[Projectile.owner].Center.X ? 1 : -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Cuts++;
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write((ushort)Math.Min(Cuts, ushort.MaxValue));

        public override void ReceiveExtraAI(BinaryReader reader) => Cuts = reader.ReadUInt16();

        // ---- drawing -----------------------------------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            if (!posed)
                return false;

            Player player = Main.player[Projectile.owner];
            Texture2D fist = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center;
            Vector2 dir = angle.ToRotationVector2();
            Vector2 across = new(-dir.Y, dir.X);
            SpriteEffects flip = dir.X < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Vector2 origin = new(fist.Width * 0.75f, fist.Height * 0.5f);
            float jitter = 1.2f * MathF.Sin(Age * 2.7f);

            // The forearm, from the hand to the back of the fist, laid end to end.
            const string armPath = "PisAndDag/Content/Items/Melee/NightcallArm";
            Vector2 wrist = at - dir * (fist.Width * 0.75f - 2f);
            float length = Vector2.Dot(wrist - hand, dir);

            if (length > 1f && ModContent.HasAsset(armPath))
            {
                Texture2D arm = ModContent.Request<Texture2D>(armPath).Value;

                for (float done = 0f; done < length; done += arm.Width)
                {
                    int width = (int)Math.Min(arm.Width, Math.Ceiling(length - done));
                    Vector2 from = hand + dir * done + across * jitter * 0.3f;
                    Color lit = Lighting.GetColor((from + dir * width * 0.5f).ToTileCoordinates());
                    Main.EntitySpriteDraw(arm, from - Main.screenPosition, new Rectangle(0, 0, width, arm.Height), lit, angle,
                        new Vector2(0f, arm.Height * 0.5f), 1f, flip, 0);
                }
            }

            // The fist, shaking as it cuts; the blur of its blade either side of it, adding light.
            Color fistLit = Lighting.GetColor(at.ToTileCoordinates());
            Vector2 shown = at + across * jitter;

            for (int k = -1; k <= 1; k += 2)
            {
                Color blur = Nightcall.Colour(NightcallFist.Chainsaw) * 0.22f;
                blur.A = 0;
                Main.EntitySpriteDraw(fist, shown + across * k * 2f - Main.screenPosition, null, blur, angle + k * 0.06f, origin, 1f, flip, 0);
            }

            Main.EntitySpriteDraw(fist, shown - Main.screenPosition, null, fistLit, angle, origin, 1f, flip, 0);

            float beat = 0.8f + 0.2f * MathF.Sin(Age * 1.3f);
            DestroyerArt.DrawGlow(Main.spriteBatch, at, 26f * beat, PrimeArt.Lit(new Color(255, 190, 90) * 0.35f));
            return false;
        }
    }
}
