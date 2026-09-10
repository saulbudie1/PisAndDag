using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// Carries an arc's look and its rules from the thing that fired it into the
    /// projectile itself.
    ///
    /// Exists because the alternative does not work. A projectile's ai[] slots are floats,
    /// and a Color is 32 bits of packed bytes — pushing one through a float either loses
    /// the low channel to rounding or lands on a NaN bit pattern, and both are worse than
    /// they sound. So the values ride in on the spawn source instead, get read in OnSpawn
    /// (which runs BEFORE the sync packet leaves), and go out over SendExtraAI.
    /// </summary>
    public class SurgeSource : EntitySource_Parent
    {
        public Color Colour = SurgeArc.DefaultColour;
        public float Width = SurgeArc.DefaultWidth;
        public bool ThroughWalls;

        public SurgeSource(Entity parent, string context = null) : base(parent, context)
        {
        }
    }

    /// <summary>
    /// One instant bolt, hand to victim.
    ///
    /// Built the way vanilla's Arc Surge is: the projectile does not travel. It appears
    /// AT the strike point already connected to the hand that threw it, hurts everything
    /// standing on the line between the two, and is gone in a fifth of a second. Nothing
    /// about it is a moving object, which is what makes lightning read as lightning
    /// rather than as a fast bullet — a bullet can be dodged after it is fired.
    ///
    /// Vanilla damages along the drawn crackle itself, through a hitbox type that takes a
    /// list of points. There is no such hitbox here, and there could not usefully be one:
    /// the crackle is random per client, so a hit test run against it would disagree
    /// between machines. The straight run is used instead. It differs from the drawn path
    /// by a few pixels and every machine computes the same one.
    /// </summary>
    public class SurgeArc : ModProjectile
    {
        // Nothing is ever drawn from this, but a ModProjectile must name a texture that
        // resolves. Borrowing a vanilla one costs no file.
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        /// <summary>Vanilla's Arc Surge is red, and red reads hotter than blue at this size.</summary>
        public static readonly Color DefaultColour = new Color(255, 92, 92);

        /// <summary>Half-width at the hand. The bolt tapers toward the target on its own.</summary>
        public const float DefaultWidth = 7f;

        /// <summary>Ticks the arc exists — its lifetime and the particle's are the same.</summary>
        private const int Life = 14;

        private const int Forks = 2;

        /// <summary>Electrified, for vanilla's own 4 to 7 seconds.</summary>
        private const int ZapMin = 4 * 60;
        private const int ZapMax = 7 * 60;

        private Color colour = DefaultColour;
        private float width = DefaultWidth;

        /// <summary>
        /// Whether the bolt hurts what it cannot see. Synced because it decides who gets
        /// hit, not just how it looks.
        /// </summary>
        private bool throughWalls;

        /// <summary>
        /// Whether the visual has been thrown yet. Not synced — every machine sets its own,
        /// because every machine draws its own crackle.
        /// </summary>
        private bool started;

        /// <summary>
        /// Where the bolt left from, packed into ai[] at spawn.
        ///
        /// Not read off the player each frame on purpose: ai[] rides along in the spawn
        /// packet, so a client that never saw the shot rebuilds the same geometry, and the
        /// arc stays welded to the spot it left even as the player keeps moving.
        /// </summary>
        private Vector2 Origin => new Vector2(Projectile.ai[0], Projectile.ai[1]);

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;

            // Pierces without limit, but each enemy only once for the arc's whole life:
            // the line is tested every tick, so without the local cooldown a body standing
            // on it would take fourteen hits instead of one.
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source)
        {
            if (source is not SurgeSource surge)
                return;

            colour = surge.Colour;
            width = surge.Width;
            throughWalls = surge.ThroughWalls;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(colour.PackedValue);
            writer.Write(width);
            writer.Write(throughWalls);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            colour = new Color { PackedValue = reader.ReadUInt32() };
            width = reader.ReadSingle();
            throughWalls = reader.ReadBoolean();
        }

        public override void AI()
        {
            // The visual goes off on the first tick rather than in OnSpawn, and that is
            // the whole reason it works in multiplayer. A projectile arriving from the
            // network is built field by field from the packet — extra AI included — and
            // only then does it start updating. Drawing in OnSpawn would mean everyone but
            // the shooter got the default red at the default thickness.
            if (!started)
            {
                started = true;

                if (!Main.dedServ)
                {
                    Lightning.Spawn(Origin, Projectile.Center, colour, width, Life, Forks);

                    SoundEngine.PlaySound(
                        SoundID.Item93 with { Volume = 0.45f, Pitch = 0.5f, PitchVariance = 0.3f },
                        Projectile.Center);
                }
            }

            // Belt and braces: the projectile is spawned with no velocity, and this keeps
            // anything that nudges it — knockback resolution, a stray hook — from dragging
            // the strike point away from the enemy it was aimed at mid-life.
            Projectile.velocity = Vector2.Zero;

            Lighting.AddLight(Projectile.Center, colour.ToVector3() * 0.55f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!SegmentHitsBox(Origin, Projectile.Center, targetHitbox))
                return false;

            // The bolt's own target was already checked for line of sight before it was
            // fired. This second test is for everything ELSE the line happens to cross —
            // without it, an arc thrown past a wall corner quietly hurts whatever is
            // sheltering behind it, which reads as a bug rather than as a feature.
            if (!throughWalls)
            {
                Vector2 middle = new Vector2(targetHitbox.Center.X, targetHitbox.Center.Y);

                if (!Collision.CanHitLine(Origin, 1, 1, middle, 1, 1))
                    return false;
            }

            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Electrified, Main.rand.Next(ZapMin, ZapMax + 1));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // The particle handler draws the bolt. This is only the hitbox and the damage.
            return false;
        }

        /// <summary>
        /// Segment against an axis-aligned box, by the slab method.
        ///
        /// Written out rather than called from Collision so there is nothing in the hot
        /// path that a version bump can rename or reorder. Walks each axis, narrows the
        /// stretch of the segment that could still be inside the box, and gives up the
        /// moment that stretch is empty.
        /// </summary>
        private static bool SegmentHitsBox(Vector2 from, Vector2 to, Rectangle box)
        {
            Vector2 run = to - from;

            float enter = 0f;
            float exit = 1f;

            for (int axis = 0; axis < 2; axis++)
            {
                float start = axis == 0 ? from.X : from.Y;
                float delta = axis == 0 ? run.X : run.Y;
                float low = axis == 0 ? box.Left : box.Top;
                float high = axis == 0 ? box.Right : box.Bottom;

                // Parallel to this pair of edges: either it is between them for the whole
                // segment or it never touches the box at all.
                if (Math.Abs(delta) < 0.0001f)
                {
                    if (start < low || start > high)
                        return false;

                    continue;
                }

                float near = (low - start) / delta;
                float far = (high - start) / delta;

                if (near > far)
                    (near, far) = (far, near);

                enter = Math.Max(enter, near);
                exit = Math.Min(exit, far);

                if (enter > exit)
                    return false;
            }

            return true;
        }
    }
}
