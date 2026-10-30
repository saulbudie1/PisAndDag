using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Accessories.Tentochi;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Tentochi
{
    /// <summary>
    /// The Back Rocket's rocket (by request: "a swaying rocket (based on vanilla textures make one of your own) that deals 80
    /// damage ... when it detects an NPC it homes onto it"). Out of the tube on the back, up and a little back; a breath
    /// later its motor lights and it turns for the enemy the launcher held (ai[0]), gathering speed, swaying either side of
    /// its line as it goes, less as it nears. Its enemy gone, the wearer's machine gives it the nearest in sight (and tells
    /// the others); none, and it flies on and bursts at the end of its fuel. Bursts on what it hits: 80, to that one.
    ///
    /// Drawn: its picture (the mod's own, after the game's rockets: Tentochi's red band), a hard red line of light where it
    /// has been, the motor's flame behind it, smoke and sparks shed (TentochiFX).
    /// </summary>
    public class TentochiRocket : ModProjectile
    {
        /// <summary>Ticks out of the tube before the motor lights.</summary>
        public const int Coast = 9;

        public const float StartSpeed = 4f;
        public const float MaxSpeed = 15f;
        public const float Thrust = 0.55f;

        /// <summary>How far it may turn a tick, in radians: little at first, more as it closes.</summary>
        public const float MinTurn = 0.07f;
        public const float MaxTurn = 0.16f;

        /// <summary>The sway: how far either side of its line it swings, in radians, at its widest.</summary>
        public const float Sway = 0.32f;

        private int Target
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private float Age
        {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 210;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            Age++;

            if (Age == 1f)
            {
                if (!Main.dedServ)
                    TentochiFX.Launch(Projectile.Center, Projectile.velocity);

                // The launcher it left seen kicking, and holding its mark, on the machines that did not fire it.
                if (Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers)
                    Main.player[Projectile.owner].GetModPlayer<TentochiPlayer>().Fired(Target);
            }

            if (Age <= Coast)
            {
                // Out of the tube: slowing, nose turning as it falls back.
                Projectile.velocity *= 0.9f;
                Projectile.velocity.Y += 0.12f;
            }
            else
            {
                NPC target = Chosen();
                float speed = Math.Min(MaxSpeed, Math.Max(StartSpeed, Projectile.velocity.Length()) + Thrust);
                float heading = Projectile.velocity.ToRotation();
                float wanted = heading;
                float distance = 600f;

                if (target != null)
                {
                    wanted = (target.Center - Projectile.Center).ToRotation();
                    distance = Vector2.Distance(target.Center, Projectile.Center);
                }

                // The sway, about the line it wants: wide while far, narrow close in.
                float swing = Sway * MathHelper.Clamp(distance / 320f, 0.15f, 1f);
                wanted += swing * (float)Math.Sin(Age * 0.32f + Projectile.identity * 1.7f);

                float turn = MathHelper.Lerp(MinTurn, MaxTurn, MathHelper.Clamp((Age - Coast) / 40f, 0f, 1f));
                float difference = MathHelper.WrapAngle(wanted - heading);
                heading += MathHelper.Clamp(difference, -turn, turn);

                Projectile.velocity = heading.ToRotationVector2() * speed;

                if (!Main.dedServ)
                    Burn();
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        /// <summary>The enemy it goes for: the one it was given while that can be hit; else, on the wearer's machine, the nearest in sight.</summary>
        private NPC Chosen()
        {
            if (Target >= 0 && Target < Main.maxNPCs && Main.npc[Target].CanBeChasedBy(Projectile))
                return Main.npc[Target];

            if (Projectile.owner != Main.myPlayer)
                return null;

            int best = -1;
            float nearest = TentochiPlayer.RocketRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float distance = Vector2.Distance(npc.Center, Projectile.Center);

                if (distance < nearest && Collision.CanHitLine(Projectile.Center, 1, 1, npc.position, npc.width, npc.height))
                {
                    best = i;
                    nearest = distance;
                }
            }

            if (best != Target)
            {
                Target = best;
                Projectile.netUpdate = true;
            }

            return best >= 0 ? Main.npc[best] : null;
        }

        /// <summary>The motor: sparks and a little smoke out of the tail, red light about it.</summary>
        private void Burn()
        {
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tail = Projectile.Center + back * 10f;

            if (PADParticleHandler.FreeSlots > 6)
            {
                PADParticleHandler.SpawnParticle(new SparkShard(tail, back.RotatedByRandom(0.25f) * Main.rand.NextFloat(2f, 4f),
                    Main.rand.NextBool() ? TentochiFX.Hot : TentochiFX.Amber, 4f, 2f, 9f, 7));

                if (Age % 2 == 0)
                    PADParticleHandler.SpawnParticle(new FlameSmoke(tail, back * 0.6f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                        new Color(110, 106, 116), Main.rand.NextBool() ? 22 : 30, 6f, 20f, 0.35f));

                if (Age % 5 == 0)
                    PADParticleHandler.SpawnParticle(new PixelChip(tail, back * 1.2f + Main.rand.NextVector2Circular(0.6f, 0.6f), TentochiFX.Red, 2f, 14));
            }

            Lighting.AddLight(tail, 0.9f, 0.3f, 0.2f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft)
        {
            if (!Main.dedServ)
                TentochiFX.Blast(Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 back = -Projectile.velocity.SafeNormalize(Vector2.UnitX);

            // Where it has been: a hard line of their red, thinning and fading back along it.
            int length = Projectile.oldPos.Length;

            for (int i = 1; i < length; i++)
            {
                Vector2 a = Projectile.oldPos[i - 1];
                Vector2 b = Projectile.oldPos[i];

                if (a == Vector2.Zero || b == Vector2.Zero)
                    break;

                Vector2 half = Projectile.Size * 0.5f;
                float share = 1f - i / (float)length;
                CyberRing.Line(Main.spriteBatch, a + half, b + half, Math.Max(1f, 2.5f * share), TentochiFX.Glow(TentochiFX.Red, 0.85f * share));
            }

            if (Age > Coast)
            {
                // The motor's flame: a hot point and a soft red behind it, flickering.
                float flicker = 0.85f + 0.15f * (float)Math.Sin(Age * 1.7f);
                Vector2 tail = Projectile.Center + back * 9f;
                EmpressArt.DrawBloom(Main.spriteBatch, tail + back * 4f, 26f * flicker, TentochiFX.Glow(TentochiFX.Red, 0.7f));
                EmpressArt.DrawBloom(Main.spriteBatch, tail, 10f * flicker, TentochiFX.Glow(TentochiFX.Hot, 1f));
                EmpressArt.DrawRay(Main.spriteBatch, tail, back.ToRotation(), 22f * flicker, 7f, TentochiFX.Glow(TentochiFX.Amber, 0.8f));
            }

            Main.EntitySpriteDraw(texture, at, null, Projectile.GetAlpha(lightColor), Projectile.rotation, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0);

            // Its light, on its band: a blink of their red.
            if ((int)(Age / 6f) % 2 == 0)
                EmpressArt.DrawBloom(Main.spriteBatch, Projectile.Center + Projectile.rotation.ToRotationVector2() * 2f, 6f, TentochiFX.Glow(TentochiFX.Red, 1f));

            return false;
        }
    }
}
