using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.DonaQuinn
{
    /// <summary>
    /// 'Dame' Quilt's lance, thrown. Heavy: it goes up and comes down on an arc that ends where
    /// the player was going to be, point first, and wherever it comes down it goes into the
    /// ground and stays there, standing at the angle it came in at, until she rides back and
    /// pulls it out (DonaQuinn.Fetch), or there is nobody left to come back for it and it fades.
    ///
    /// In the air its head hurts: 52 through armour, once, against each machine's own player,
    /// checked as anything that hurts by touch is. In the ground it is only a lance in the
    /// ground. Drawn behind the tiles, so the part of it in the ground is in the ground.
    ///
    /// The same picture as the one she holds (Lance_AlsoGore.png, by name), its point at the top
    /// middle (11, 0). The projectile is its point: the hitbox, the arc and the drawing are all
    /// about the point.
    /// </summary>
    public class DonaLanceThrown : ModProjectile
    {
        public override string Texture => DonaRig.Folder + "Lance_AlsoGore";

        /// <summary>Its weight: pixels a tick it gains falling, every tick, and the fastest it falls.</summary>
        public const float Gravity = 0.35f;
        private const float MaxFall = 16f;

        /// <summary>How much of where they are going she allows for: half of it.</summary>
        private const float Lead = 0.5f;

        /// <summary>How far the point goes into the ground.</summary>
        private const float Embed = 12f;

        /// <summary>The part that hurts: from this far back along it to just past the point, this thick; and the knock.</summary>
        private const float Head = 40f;
        private const float HeadWidth = 16f;
        private const float Knockback = 6f;

        /// <summary>Ticks it takes to fade once nobody is coming back for it.</summary>
        private const int FadeTicks = 30;

        /// <summary>The point on the picture.</summary>
        private static readonly Vector2 Point = new Vector2(DonaRig.LanceWidth / 2f, 0f);

        // ai[0] her whoAmI · ai[1] 0 in the air, 1 in the ground · localAI[0] 1 once it has hit this machine's player

        private bool Planted => Projectile.ai[1] == 1f;

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.netImportant = true;

            // Drawn behind the tiles (DrawBehind), so the part of it in the ground is in the ground.
            Projectile.hide = true;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles,
            List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCsAndTiles.Add(index);
        }

        /// <summary>
        /// The throw: from the point in her hand to where the player will be (half of where they
        /// are going), over a time that grows with how far it is, in the order the game moves a
        /// projectile (its AI adds the fall, then it moves).
        /// </summary>
        public static Vector2 Aim(Vector2 from, Player target)
        {
            float dx = target.Center.X - from.X;
            int ticks = (int)MathHelper.Clamp(Math.Abs(dx) / 8f, 24f, 64f);
            Vector2 to = target.Center + new Vector2(target.velocity.X * ticks * Lead, 0f);

            dx = to.X - from.X;
            float dy = to.Y - from.Y;

            float vx = MathHelper.Clamp(dx / ticks, -12f, 12f);
            float vy = MathHelper.Clamp((dy - Gravity * ticks * (ticks + 1) / 2f) / ticks, -15f, 15f);

            return new Vector2(vx, vy);
        }

        private DonaQuinn Owner()
        {
            int i = (int)Projectile.ai[0];

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active && npc.ModNPC is DonaQuinn dona ? dona : null;
        }

        public override void AI()
        {
            // Nobody coming back for it: it goes, on every machine by itself. (A machine that has
            // not yet heard she let go of it, for a tick or two, only starts to fade it.)
            DonaQuinn dona = Owner();

            if (dona == null || !dona.WantsLance)
            {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 255 / FadeTicks + 1);

                if (Projectile.alpha >= 255)
                {
                    Projectile.Kill();
                    return;
                }
            }
            else
            {
                Projectile.alpha = Math.Max(0, Projectile.alpha - 32);
            }

            if (Planted)
            {
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Strike();
        }

        /// <summary>Its head against this machine's own player: 52 through armour, varied as vanilla varies any hit, once.</summary>
        private void Strike()
        {
            if (Main.dedServ || Projectile.localAI[0] != 0f || Projectile.alpha > 0)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.immune)
                return;

            Vector2 along = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 point = Projectile.Center;
            float where = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, point - along * Head, point + along * 4f, HeadWidth, ref where))
                return;

            int damage = Main.DamageVar(DonaQuinn.ThrowDamage, -me.luck);
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText.DonaLanceThrown", me.name));

            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            if (me.Hurt(reason, damage, direction, false, false, ImmunityCooldownID.General, true, 0f, 1f, Knockback) <= 0)
                return;

            Projectile.localAI[0] = 1f;

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f, Volume = 1f }, me.Center);
            me.PIS().BumpScreenShake(5f, 12);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!Planted)
                Plant(oldVelocity);

            return false;
        }

        /// <summary>Into the ground, point first, at the angle it came in at, and there it stays. The server tells her where.</summary>
        private void Plant(Vector2 oldVelocity)
        {
            Vector2 along = oldVelocity.SafeNormalize(Vector2.UnitY);

            Projectile.ai[1] = 1f;
            Projectile.tileCollide = false;
            Projectile.rotation = oldVelocity.ToRotation() + MathHelper.PiOver2;
            Projectile.position += along * Embed;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.3f, Volume = 1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.5f, Volume = 0.7f }, Projectile.Center);

                for (int i = 0; i < 10; i++)
                {
                    Dust dust = Dust.NewDustDirect(Projectile.Center - along * Embed - new Vector2(6f), 12, 12, DustID.Dirt,
                        -along.X * Main.rand.NextFloat(0.5f, 2f) + Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                    dust.noGravity = false;
                }
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                Owner()?.LanceDown(Projectile.Center.X);
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ || Projectile.alpha >= 200)
                return;

            // Out of the ground: a little of it comes with it.
            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(6f), 12, 12, DustID.Dirt,
                    Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 2f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            // Lit where the shaft is, not where the point is (in the ground, in the dark).
            Vector2 along = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
            Color light = Lighting.GetColor((Projectile.Center - along * 30f).ToTileCoordinates());

            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(light),
                Projectile.rotation, Point, 1f, SpriteEffects.None);

            return false;
        }
    }
}
