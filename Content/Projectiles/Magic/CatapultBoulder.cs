using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Boulder Catapult's boulder: the game's boulder (its picture and, once it is down, its ways), thrown.
    ///
    /// ai[0] is what it is doing: 0 thrown, 1 rolling. Thrown, it falls under <see cref="Gravity"/> on the arc
    /// <see cref="Launch"/> gave it, turning over as it goes; off a wall in the air it is knocked back a little and
    /// keeps falling, off a ceiling it stops rising. The first time it comes down on the ground it lands: a thud,
    /// the ground's dust, a small hop, and from then on it is the game's own boulder (Projectile aiStyle 25 and
    /// Projectile.HandleMovement for type 99, 1.4.4), copied here so it can be the caster's and hurt only what they
    /// may hurt: it gathers speed toward 7 pixels a tick while it is not falling hard, falls at 0.3 to at most 16,
    /// turns by its speed, bounces at a fifth of the speed off any drop of more than 5, is steered off a wall it has
    /// stopped against (a step one way or the other, to whichever side is open), sets off pressure plates, and breaks
    /// the moment it runs into a wall, in stone dust with the dig sound, as theirs do. It also breaks after
    /// <see cref="Life"/> ticks, so that it does not roll across the world.
    ///
    /// It hurts everything it meets once every <see cref="Cooldown"/> ticks, its own count for each, rolling or in
    /// the air; never a player.
    /// </summary>
    public class CatapultBoulder : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Boulder;

        /// <summary>The game's boulder's size (31) and fall (0.3 a tick).</summary>
        public const int Size = 31;
        public const float Gravity = 0.3f;
        public const float MaxFall = 16f;

        /// <summary>Its top speed rolling, and what it gathers a tick toward it (the game's).</summary>
        public const float RollSpeed = 7f;
        public const float RollGather = 0.05f;

        public const int Life = 600;
        public const int Cooldown = 20;

        /// <summary>Its flight, in ticks: this many at the least and the most, and one more for each this many pixels it is thrown.</summary>
        private const float FlightMin = 22f, FlightMax = 70f, FlightBase = 18f, PixelsPerTick = 14f;

        private bool Rolling => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.CultistIsResistantTo[Type] = false;
        }

        public override void SetDefaults()
        {
            Projectile.width = Size;
            Projectile.height = Size;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Cooldown;
            Projectile.timeLeft = Life;
        }

        /// <summary>
        /// The throw that comes down on <paramref name="target"/> from <paramref name="from"/>: it is in the air for
        /// more ticks the further it goes, and with the fall it has (a tick: the velocity takes the fall, then the
        /// boulder takes the velocity) it is at the target on its last.
        /// </summary>
        public static Vector2 Launch(Vector2 from, Vector2 target)
        {
            Vector2 way = target - from;
            float ticks = MathHelper.Clamp(FlightBase + way.Length() / PixelsPerTick, FlightMin, FlightMax);
            float t = (float)Math.Round(ticks);

            // After t ticks: x = t vx, y = t vy + g t (t + 1) / 2.
            return new Vector2(way.X / t, (way.Y - Gravity * t * (t + 1f) * 0.5f) / t);
        }

        public override void AI()
        {
            if (!Rolling)
            {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
                Projectile.rotation += Projectile.velocity.X * 0.04f + Math.Sign(Projectile.velocity.X) * 0.05f;
                return;
            }

            Roll();
        }

        /// <summary>The game's boulder rolling (Projectile.AI, aiStyle 25), as it is for type 99.</summary>
        private void Roll()
        {
            Vector2 v = Projectile.velocity;

            // Stopped against something and not falling: a nudge away from whichever side is shut, looking one, two,
            // then three tiles out.
            if (v.Y <= 0f && v.X == 0f)
            {
                const float nudge = 0.5f;
                int y = (int)(Projectile.position.Y / 16f);

                for (int reach = 0; reach <= 32; reach += 16)
                {
                    bool left = Shut((int)((Projectile.position.X - 8f - reach) / 16f), y);
                    bool right = Shut((int)((Projectile.position.X + Projectile.width + 8f + reach) / 16f), y);

                    if (reach == 32 && !left && !right)
                    {
                        if ((int)(Projectile.Center.X / 16f) % 2 == 0)
                            left = true;
                        else
                            right = true;
                    }

                    if (left)
                    {
                        v.X = nudge;
                        break;
                    }

                    if (right)
                    {
                        v.X = -nudge;
                        break;
                    }
                }
            }

            Projectile.rotation += v.X * 0.06f;

            if (v.Y > MaxFall)
                v.Y = MaxFall;

            if (v.Y <= 6f)
            {
                if (v.X > 0f && v.X < RollSpeed)
                    v.X += RollGather;

                if (v.X < 0f && v.X > -RollSpeed)
                    v.X -= RollGather;
            }

            v.Y += Gravity;
            Projectile.velocity = v;
        }

        private static bool Shut(int x, int y) => WorldGen.SolidTile(x, y) || WorldGen.SolidTile(x, y + 1);

        /// <summary>Pressure plates, as the game's boulders press them (Projectile.Update, type 99).</summary>
        public override void PostAI()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                Collision.SwitchTiles(Projectile, Projectile.position, Projectile.width, Projectile.height, Projectile.oldPosition, 3);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            bool stoppedX = Projectile.velocity.X != oldVelocity.X;
            bool stoppedY = Projectile.velocity.Y != oldVelocity.Y;

            if (!Rolling)
            {
                if (stoppedY && oldVelocity.Y > 0f)
                {
                    Land(oldVelocity);
                    return false;
                }

                if (stoppedX)
                    Projectile.velocity.X = -oldVelocity.X * 0.3f;

                if (stoppedY)
                    Projectile.velocity.Y = 0f;

                return false;
            }

            // The game's boulder (Projectile.HandleMovement, type 99): a hard drop bounces it, a wall breaks it.
            if (stoppedY && oldVelocity.Y > 5f)
            {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
                Projectile.velocity.Y = -oldVelocity.Y * 0.2f;
            }

            if (stoppedX)
                Projectile.Kill();

            return false;
        }

        /// <summary>
        /// Down: the thud and the ground thrown up, a hop of a fifth of the fall, and rolling on the way it was going
        /// (at least a step of it; straight down, the way it was thrown from).
        /// </summary>
        private void Land(Vector2 oldVelocity)
        {
            Projectile.ai[0] = 1f;
            Projectile.netUpdate = true;

            Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);
            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.7f, Pitch = -0.2f }, Projectile.Bottom);

            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.BottomLeft - new Vector2(4f, 6f), Projectile.width + 8, 8, DustID.Stone, 0f, -1.5f, 0, default, 1.1f);
                dust.velocity.X *= 1.6f;
            }

            int way = Math.Sign(oldVelocity.X);

            if (way == 0)
                way = Projectile.Center.X >= Main.player[Projectile.owner].Center.X ? 1 : -1;

            float across = Math.Abs(oldVelocity.X);
            Projectile.velocity = new Vector2(way * Math.Max(across, 1.5f), -oldVelocity.Y * 0.2f);
        }

        /// <summary>Broken: the game's boulder's end (Projectile.Kill, type 99), stone dust and the dig sound.</summary>
        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);

            for (int i = 0; i < 30; i++)
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Stone);

                if (Main.rand.NextBool(2))
                    dust.scale *= 1.4f;

                dust.velocity *= 1.4f;
            }
        }
    }
}
