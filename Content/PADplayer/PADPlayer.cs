using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// Home for everything equipment switches on.
    ///
    /// Effects are declared, not toggled: something calls the effect's method every tick
    /// it should be active, and stopping the call is what turns it off. Nothing has to
    /// remember to switch anything back, and two sources of the same effect stack by both
    /// asking for it rather than overwriting each other.
    ///
    /// Reached with player.PAD(). Kept apart from PisPlayer on purpose: the state in here
    /// is erased sixty times a second, and the camera state in PisPlayer must not be.
    /// </summary>
    public class PADPlayer : ModPlayer
    {
        // ---- Surging Core ---------------------------------------------------
        //
        // Lightning that finds its own targets. Modelled on vanilla's Arc Surge, which
        // throws an instant bolt from the player's hand to a point and damages everything
        // along the line, but driven by a timer instead of a mouse click.
        //
        //   player.PAD().SurgingCore();                 // defaults
        //   player.PAD().SurgingCore(60, 20, 3);        // stronger, faster, three at once
        //   player.PAD().SurgingCore(range: 600f);      // same core, longer arm
        //   player.PAD().SurgingCore(colour: Color.Violet, thickness: 12f, throughWalls: true);
        //
        // Call it from UpdateAccessory, a set bonus, a buff, a pet's AI — anywhere that
        // runs every tick before PostUpdateEquips, which is where the volley actually
        // goes out. Calling it once does nothing lasting; calling it every tick is the
        // effect.

        /// <summary>
        /// Default reach, in pixels. Measured to the nearest point of an enemy's hitbox
        /// rather than to its centre, so a large body standing at the edge of the ring
        /// still counts — centre to centre would quietly shorten the range against
        /// anything big.
        /// </summary>
        public const float SurgeRange = 340f;

        /// <summary>Ceiling on arcs in one volley, however many sources pile up.</summary>
        private const int MaxArcs = 8;

        private bool surging;
        private int surgeDamage;
        private int surgeRate;
        private int surgeArcs;
        private float surgeRange;
        private float surgeThickness;
        private bool surgeThroughWalls;
        private Color surgeColour;

        /// <summary>Survives ResetEffects — the whole point of it is to span ticks.</summary>
        private int surgeCooldown;

        /// <summary>
        /// Asks for lightning this tick.
        /// </summary>
        /// <param name="damage">Base damage per arc, before the player's own bonuses.</param>
        /// <param name="rate">Ticks between volleys. Lower is faster.</param>
        /// <param name="arcs">Enemies struck per volley, nearest first.</param>
        /// <param name="range">Reach in pixels, to the nearest edge of an enemy's hitbox.</param>
        /// <param name="thickness">Half-width of the bolt at the hand. It tapers on its own.</param>
        /// <param name="throughWalls">Let it target and hurt what it cannot see.</param>
        /// <param name="colour">Tint of the glow. The core burns white on top of it regardless.</param>
        public void SurgingCore(int damage = 40, int rate = 36, int arcs = 1, float range = SurgeRange,
            float thickness = SurgeArc.DefaultWidth, bool throughWalls = false, Color? colour = null)
        {
            // First caller of the tick sets the terms; everyone after it merges in. Taking
            // the best of each rather than letting the last call win means the order the
            // accessories happen to update in cannot change the result — and two sources
            // of the effect read as two sources instead of one quietly cancelling the
            // other. More arcs, harder hits, shorter gap, longer arm, fatter bolt, and a
            // wall-piercing source upgrades the whole volley.
            //
            // Colour is the exception and is first-come. There is no sensible "best" of
            // two colours — averaging them gives a muddy third one nobody chose — so the
            // first caller of the tick owns the look. If two pieces of gear want different
            // lightning, that is a design question, not something to split the difference on.
            if (!surging)
            {
                surging = true;
                surgeDamage = damage;
                surgeRate = rate;
                surgeArcs = arcs;
                surgeRange = range;
                surgeThickness = thickness;
                surgeThroughWalls = throughWalls;
                surgeColour = colour ?? SurgeArc.DefaultColour;
                return;
            }

            surgeDamage = Math.Max(surgeDamage, damage);
            surgeRate = Math.Min(surgeRate, rate);
            surgeArcs += arcs;
            surgeRange = Math.Max(surgeRange, range);
            surgeThickness = Math.Max(surgeThickness, thickness);
            surgeThroughWalls |= throughWalls;
        }

        public override void ResetEffects()
        {
            surging = false;
        }

        /// <summary>
        /// Runs after everything equipped has had its say, so whatever asked for the core
        /// this tick has already asked by now.
        /// </summary>
        public override void PostUpdateEquips()
        {
            if (!surging || Player.dead)
            {
                // Reset rather than freeze, so putting the gear back on does not fire
                // instantly off a timer that was already spent.
                surgeCooldown = 0;
                return;
            }

            // Only the owning client spawns the projectiles; they sync outward from there.
            // Letting every client fire would multiply the volley by the player count.
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (surgeCooldown > 0)
            {
                surgeCooldown--;
                return;
            }

            if (Surge())
                surgeCooldown = Math.Max(1, surgeRate);
        }

        /// <summary>
        /// Throws bolts at the nearest eligible enemies. Returns whether anything was
        /// actually struck — a volley that finds nothing costs no cooldown, so the core is
        /// ready the instant something walks into range.
        /// </summary>
        private bool Surge()
        {
            // The hand, not the body. Vanilla anchors Arc Surge here, and it is what makes
            // the bolt read as leaving the weapon rather than the player's stomach.
            Vector2 origin = Player.RotatedRelativePoint(Player.MountedCenter);

            int wanted = (int)MathHelper.Clamp(surgeArcs, 1, MaxArcs);
            Span<int> struck = stackalloc int[MaxArcs];
            int fired = 0;

            while (fired < wanted)
            {
                int target = Nearest(origin, struck, fired);

                if (target == -1)
                    break;

                struck[fired] = target;
                fired++;

                Throw(origin, Main.npc[target]);
            }

            return fired > 0;
        }

        /// <summary>Closest valid enemy that is not already in <paramref name="taken"/>.</summary>
        private int Nearest(Vector2 origin, Span<int> taken, int takenCount)
        {
            int best = -1;

            // Doubles as the ceiling and as the best-so-far, so an enemy only replaces the
            // current pick if it is BOTH in range and closer than what is already held.
            float bestReach = surgeRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.CanBeChasedBy(Player))
                    continue;

                bool already = false;

                for (int t = 0; t < takenCount; t++)
                {
                    if (taken[t] == i)
                    {
                        already = true;
                        break;
                    }
                }

                if (already)
                    continue;

                float reach = ReachTo(origin, npc);

                if (reach > bestReach)
                    continue;

                // Lightning does not go through walls, unless it was asked to. Vanilla
                // walks the line in steps for its own arcs; CanHitLine is the same test
                // with the stepping already done.
                if (!surgeThroughWalls
                    && !Collision.CanHitLine(Player.position, Player.width, Player.height,
                        npc.position, npc.width, npc.height))
                    continue;

                best = i;
                bestReach = reach;
            }

            return best;
        }

        private void Throw(Vector2 origin, NPC target)
        {
            int damage = (int)Player.GetDamage(DamageClass.Generic).ApplyTo(surgeDamage);

            // The look and the rules ride in on the source rather than being assigned to
            // the projectile afterwards. NewProjectile sends the sync packet before it
            // returns, so anything set on the way out is already in that packet — set it
            // after and every other player watches a default-red arc for a frame.
            SurgeSource source = new SurgeSource(Player, "PADSurgingCore")
            {
                Colour = surgeColour,
                Width = surgeThickness,
                ThroughWalls = surgeThroughWalls
            };

            Projectile.NewProjectile(
                source,
                target.Center,
                Vector2.Zero,
                ModContent.ProjectileType<SurgeArc>(),
                damage,
                0f,
                Player.whoAmI,
                origin.X,
                origin.Y);
        }

        /// <summary>Distance from a point to the nearest point of an NPC's hitbox.</summary>
        private static float ReachTo(Vector2 from, NPC npc)
        {
            float x = MathHelper.Clamp(from.X, npc.position.X, npc.position.X + npc.width);
            float y = MathHelper.Clamp(from.Y, npc.position.Y, npc.position.Y + npc.height);

            return Vector2.Distance(from, new Vector2(x, y));
        }
    }
}
