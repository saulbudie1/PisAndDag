using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Signal Pistol's flare. It burns for <see cref="Burn"/> ticks from the shot, sputtering out
    /// over the last <see cref="Sputter"/>.
    ///
    /// Free (ai[0] 0), in the air it arcs and falls, sparks and pink smoke off it; in water it is
    /// slowed hard and sinks slowly, bubbles and embers off it. The first enemy it meets it sticks in
    /// (ai[0] 1, ai[1] the enemy; where on it, sent) and burns: that hit is the whole damage, and
    /// every <see cref="BurnEvery"/> ticks after it <see cref="BurnShare"/> of it, no push. A tile it
    /// meets, it sticks to (ai[0] 2) and burns on, hurting whatever touches it the same way. What it
    /// was stuck in dies or goes, it falls free again.
    ///
    /// It lights a wide circle red (in the Vast Ocean's deep, VastOceanLight keeps every light to its
    /// own cell: there it is a red point with its own glow round it). What swims within
    /// <see cref="LureReach"/> of it turns toward it, pulled a little every few ticks (no boss, no
    /// part of one, no town folk). And while it burns, its owner has the game's Darkness.
    /// </summary>
    public class SignalFlare : ModProjectile
    {
        private const int Free = 0, InEnemy = 1, OnTile = 2;

        public const int Burn = 360;
        public const int Sputter = 50;

        public const int BurnEvery = 30;
        public const float BurnShare = 0.6f;

        /// <summary>How far it draws what swims, and how hard, every <see cref="LureEvery"/> ticks; the fastest it makes anything go.</summary>
        public const float LureReach = 30f * 16f;
        private const float LurePull = 0.35f;
        private const int LureEvery = 4;
        private const float LureMost = 7f;

        private const float Gravity = 0.13f;
        private const float MostFall = 10f;
        private const float WaterDrag = 0.92f;
        private const float SinkSpeed = 0.45f;

        public static readonly Color Red = new Color(255, 52, 46);
        public static readonly Color Hot = new Color(255, 214, 180);
        private static readonly Color[] Sparks = { new Color(255, 236, 200), new Color(255, 140, 90), new Color(240, 60, 50) };

        /// <summary>The glows, made by formula (Content/Graphics): a wide soft halo, a white-hot core, a four point glint.</summary>
        private static Asset<Texture2D> soft, hot, star;

        private int State
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private Vector2 offset;
        private bool wasWet;
        private int lureSends;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            soft = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft");
            hot = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowHot");
            star = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowStar");
        }

        public override void Unload()
        {
            soft = null;
            hot = null;
            star = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = BurnEvery;
            Projectile.timeLeft = Burn;
            Projectile.netImportant = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(offset);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            offset = reader.ReadVector2();
        }

        /// <summary>How brightly it burns this tick, 0 to 1: all of it until it sputters out.</summary>
        private float Strength()
        {
            if (Projectile.timeLeft > Sputter)
                return 0.9f + 0.1f * Main.rand.NextFloat();

            float left = Projectile.timeLeft / (float)Sputter;
            return left * (Main.rand.NextBool(4) ? 0.35f : 1f);
        }

        private bool Wet()
        {
            Point cell = Projectile.Center.ToTileCoordinates();

            if (cell.X < 0 || cell.X >= Main.maxTilesX || cell.Y < 0 || cell.Y >= Main.maxTilesY)
                return false;

            Tile tile = Main.tile[cell.X, cell.Y];
            return tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water;
        }

        public override void AI()
        {
            bool wet = Wet();

            if (wet && !wasWet && Projectile.timeLeft < Burn - 1 && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.25f, Pitch = 0.6f }, Projectile.Center);

                for (int i = 0; i < 6; i++)
                    Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0f, -2f, 0, default, 1.1f);
            }

            wasWet = wet;

            switch (State)
            {
                case InEnemy:
                    Stuck();
                    break;
                case OnTile:
                    Projectile.velocity = Vector2.Zero;

                    // Its tile gone, it falls again.
                    if (!Collision.SolidCollision(Projectile.position - new Vector2(2f), Projectile.width + 4, Projectile.height + 4) && Projectile.owner == Main.myPlayer)
                    {
                        State = Free;
                        Projectile.netUpdate = true;
                    }

                    break;
                default:
                    Fly(wet);
                    break;
            }

            float k = Strength();
            Lighting.AddLight(Projectile.Center, 2.1f * k, 0.42f * k, 0.34f * k);

            if (Main.netMode != NetmodeID.MultiplayerClient && Projectile.timeLeft % LureEvery == 0 && k > 0.3f)
                Lure();

            if (Projectile.owner == Main.myPlayer && Projectile.timeLeft % 20 == 0 && Projectile.timeLeft > Sputter)
                Main.player[Projectile.owner].AddBuff(BuffID.Darkness, 30);

            if (!Main.dedServ)
                Burning(wet, k);
        }

        private void Fly(bool wet)
        {
            Projectile.tileCollide = true;

            if (wet)
            {
                Projectile.velocity *= WaterDrag;
                Projectile.velocity.Y = MathHelper.Lerp(Projectile.velocity.Y, SinkSpeed, 0.08f);
                Projectile.velocity.X += (float)Math.Sin(Projectile.timeLeft * 0.07f) * 0.01f;
            }
            else
            {
                Projectile.velocity.X *= 0.995f;
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MostFall);
            }

            if (Projectile.velocity.LengthSquared() > 0.01f)
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        private void Stuck()
        {
            Projectile.tileCollide = false;
            int index = (int)Projectile.ai[1];
            NPC npc = index >= 0 && index < Main.maxNPCs ? Main.npc[index] : null;

            if (npc == null || !npc.active || npc.life <= 0)
            {
                Projectile.velocity = new Vector2(0f, 1f);

                if (Projectile.owner == Main.myPlayer)
                {
                    State = Free;
                    Projectile.netUpdate = true;
                }

                return;
            }

            Projectile.Center = npc.Center + offset;
            Projectile.velocity = Vector2.Zero;
        }

        /// <summary>What swims near it turns toward the light: a pull every few ticks, never past <see cref="LureMost"/>.</summary>
        private void Lure()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.wet || npc.townNPC || NPCID.Sets.ProjectileNPC[npc.type] || ParryRules.BossOrPart(npc))
                    continue;

                Vector2 way = Projectile.Center - npc.Center;
                float distance = way.Length();

                if (distance > LureReach || distance < 40f)
                    continue;

                npc.velocity += way / distance * LurePull;

                float speed = npc.velocity.Length();

                if (speed > LureMost)
                    npc.velocity *= LureMost / speed;

                if (Main.netMode == NetmodeID.Server && ++lureSends % 6 == 0)
                    npc.netUpdate = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (State == Free && Projectile.owner == Main.myPlayer)
            {
                State = OnTile;
                Projectile.netUpdate = true;
            }

            Projectile.velocity = Vector2.Zero;
            return false;
        }

        public override bool? CanHitNPC(NPC target)
        {
            // Stuck in one enemy, it burns only that one.
            if (State == InEnemy && target.whoAmI != (int)Projectile.ai[1])
                return false;

            return null;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (State == Free)
                return;

            modifiers.SourceDamage *= BurnShare;
            modifiers.Knockback *= 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (State != Free)
                return;

            State = InEnemy;
            Projectile.ai[1] = target.whoAmI;

            float halfW = Math.Max(0f, target.width * 0.5f - 4f);
            float halfH = Math.Max(0f, target.height * 0.5f - 4f);
            offset = new Vector2(MathHelper.Clamp(Projectile.Center.X - target.Center.X, -halfW, halfW),
                MathHelper.Clamp(Projectile.Center.Y - target.Center.Y, -halfH, halfH));

            Projectile.netUpdate = true;

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item20 with { Pitch = 0.3f, Volume = 0.45f }, Projectile.Center);
                SparkShard.Spray(Projectile.Center, Sparks[1], 6, 4f, 16f, 10, falls: true, palette: Sparks);
            }
        }

        /// <summary>What comes off it as it burns: sparks and smoke in the air, bubbles and embers in water.</summary>
        private void Burning(bool wet, float k)
        {
            if (k <= 0.05f)
                return;

            Vector2 at = Projectile.Center;

            if (wet)
            {
                if (Main.rand.NextBool(3))
                {
                    Dust bubble = Dust.NewDustDirect(at - new Vector2(3f), 6, 6, DustID.BreatheBubble, 0f, 0f, 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                    bubble.velocity = new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-2.2f, -1f));
                    bubble.noGravity = true;
                }

                if (Main.rand.NextBool(4))
                {
                    PADParticleHandler.SpawnParticle(new Ember(at + Main.rand.NextVector2Circular(3f, 3f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.6f, -0.1f)), Sparks[1] * k,
                        Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(30, 60), 0.01f));
                }

                return;
            }

            if (Main.rand.NextBool(2))
            {
                Vector2 thrown = new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.4f, 0.4f)) - Projectile.velocity * 0.2f;
                PADParticleHandler.SpawnParticle(new SparkShard(at, thrown, Sparks[Main.rand.Next(Sparks.Length)] * k,
                    5f, 9f, Main.rand.NextFloat(8f, 14f), Main.rand.Next(8, 14), falls: true));
            }

            if (Projectile.timeLeft % 3 == 0)
            {
                PADParticleHandler.SpawnParticle(new FlameSmoke(at, -Projectile.velocity * 0.1f + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -0.4f),
                    new Color(232, 96, 112), Main.rand.Next(50, 80), 10f, 34f, 0.3f * k));
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.2f, Pitch = 0.8f }, Projectile.Center);

            for (int i = 0; i < 3; i++)
            {
                PADParticleHandler.SpawnParticle(new FlameSmoke(Projectile.Center, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -0.5f),
                    new Color(120, 90, 96), Main.rand.Next(60, 90), 12f, 40f, 0.3f));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float k = Strength();
            Vector2 at = Projectile.Center - Main.screenPosition;

            if (soft != null && hot != null && star != null && k > 0f)
            {
                float time = Main.GameUpdateCount;

                // It breathes, as a burning flare does, and wavers on top of that.
                float breath = 0.92f + 0.06f * (float)Math.Sin(time * 0.31f + Projectile.whoAmI) + 0.04f * (float)Math.Sin(time * 0.77f);

                // A wide faint halo: the air lit red about it; in water wider and softer, the light hanging in it.
                Glow(soft.Value, at, Red * ((wasWet ? 0.2f : 0.26f) * k * breath), (wasWet ? 300f : 210f) * breath, 0f);

                // The flame's own light, orange, and the star itself white hot.
                Glow(soft.Value, at, new Color(255, 112, 64) * (0.5f * k), 74f * breath, 0f);
                Glow(hot.Value, at, Hot * (0.95f * k), 30f, 0f);

                // Two glints turning slowly against each other.
                Glow(star.Value, at, new Color(255, 196, 160) * (0.65f * k), 50f * breath, time * 0.015f);
                Glow(star.Value, at, new Color(255, 110, 84) * (0.3f * k), 30f, MathHelper.PiOver4 - time * 0.011f);
            }

            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Color white = Color.White * Math.Max(0.4f, k);
            white.A = (byte)(255 * Math.Max(0.4f, k));

            Main.EntitySpriteDraw(texture, at, null, white, Projectile.rotation, texture.Size() * 0.5f, 1f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>One glow in added light, this many pixels across.</summary>
        private static void Glow(Texture2D texture, Vector2 at, Color colour, float across, float rotation)
        {
            colour.A = 0;
            Main.EntitySpriteDraw(texture, at, null, colour, rotation, texture.Size() * 0.5f, across / texture.Width, SpriteEffects.None, 0);
        }
    }
}
