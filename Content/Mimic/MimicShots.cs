using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The copy's own shots (round 81). Each is marked as it is made with the way of using its weapon and the distance band
    /// the copy was using it at (Practice.Tag), or with its maker's mark when another of its shots made it (a hammer's flares,
    /// an explosion), so the damage it lands, now or a second later, is put down to the way that threw it. And the shots its
    /// weapon itself makes are watched out to their end: how far each flies from it, whether it stays in its hands (its held
    /// projectile), how fast it falls; what they were seen to do is how the copy uses the weapon from then on
    /// (MimicWeapons.Watched, MimicSense).
    /// </summary>
    public class MimicShots : GlobalProjectile
    {
        /// <summary>Each projectile slot's mark (Practice.Tag), with the identity of the shot it was given to.</summary>
        private static readonly int[] tag = new int[Main.maxProjectiles];
        private static readonly int[] tagged = new int[Main.maxProjectiles];

        // The shots watched: the item they came from, the copy's place when each was first seen, the furthest from it each has
        // been, its last two places, its fall so far, whether it was ever the copy's held projectile.
        private static readonly int[] watchItem = new int[Main.maxProjectiles];
        private static readonly int[] watchId = new int[Main.maxProjectiles];
        private static readonly float[] far = new float[Main.maxProjectiles];
        private static readonly Vector2[] last = new Vector2[Main.maxProjectiles];
        private static readonly Vector2[] step = new Vector2[Main.maxProjectiles];
        private static readonly int[] ticks = new int[Main.maxProjectiles];
        private static readonly float[] fall = new float[Main.maxProjectiles];
        private static readonly int[] falls = new int[Main.maxProjectiles];
        private static readonly int[] fallTicks = new int[Main.maxProjectiles];
        private static readonly bool[] wasHeld = new bool[Main.maxProjectiles];
        private static readonly bool[] hitSomething = new bool[Main.maxProjectiles];
        private static readonly int[] fallFrom = new int[Main.maxProjectiles];

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!MimicSystem.Exists || projectile.owner != MimicSystem.Slot)
                return;

            int i = projectile.whoAmI;

            if (i < 0 || i >= Main.maxProjectiles)
                return;

            tagged[i] = projectile.identity;
            watchItem[i] = 0;

            // A minion or a sentry fights on its own for as long as it is out, whatever the copy does meanwhile: what it lands
            // is put down to the way in use when it lands.
            if (projectile.minion || projectile.sentry)
            {
                tag[i] = -1;
                return;
            }

            // Made by another of its shots: that one's mark.
            if (source is EntitySource_Parent parent && parent.Entity is Projectile maker && maker.owner == MimicSystem.Slot)
            {
                tag[i] = TagOf(maker);
                return;
            }

            tag[i] = MimicSystem.TagNow;

            // Made by its weapon as it is used: watched to its end. (Not minions or sentries: they stay out.)
            if (source is EntitySource_ItemUse use && use.Item != null && !use.Item.IsAir && !projectile.minion && !projectile.sentry)
            {
                watchItem[i] = use.Item.type;
                watchId[i] = projectile.identity;
                far[i] = 0f;
                last[i] = projectile.Center;
                step[i] = Vector2.Zero;
                ticks[i] = 0;
                fall[i] = 0f;
                falls[i] = 0;
                fallTicks[i] = 0;
                fallFrom[i] = -1;
                wasHeld[i] = false;
                hitSomething[i] = false;
            }
        }

        /// <summary>A watched shot that hit something: how far it got says where that was, not how far it flies.</summary>
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            int i = projectile.whoAmI;

            if (i >= 0 && i < Main.maxProjectiles && watchItem[i] != 0 && projectile.owner == MimicSystem.Slot)
                hitSomething[i] = true;
        }

        /// <summary>The mark a shot of the copy's was given (-1: none, put down to the way in use when it lands).</summary>
        public static int TagOf(Projectile p)
        {
            int i = p?.whoAmI ?? -1;
            return i >= 0 && i < Main.maxProjectiles && tagged[i] == p.identity && p.owner == MimicSystem.Slot ? tag[i] : -1;
        }

        /// <summary>
        /// Once a tick, after the projectiles have moved (MimicSystem.PostUpdateProjectiles: a held projectile makes itself its
        /// holder's in its own update, and the game clears it again as the player's next update begins): each shot watched,
        /// how far it is from the copy, whether it is the copy's held projectile, how it is falling; a shot gone is told to
        /// MimicWeapons.Watched.
        /// </summary>
        public static void Watch(Player copy)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (watchItem[i] == 0)
                    continue;

                Projectile p = Main.projectile[i];

                if (!p.active || p.identity != watchId[i] || p.owner != MimicSystem.Slot)
                {
                    End(i);
                    continue;
                }

                if (copy.heldProj == i)
                    wasHeld[i] = true;

                far[i] = Math.Max(far[i], Vector2.Distance(p.Center, copy.Center));

                // What it falls by: the second difference of where it is, a tick at a time (pixels a tick each tick, its
                // extra updates and all); while it is going one way across, not turned or stopped.
                Vector2 now = p.Center;
                Vector2 d = now - last[i];

                // From the tick it begins to fall (an arrow flies straight a while first), for 40 ticks at most.
                if (ticks[i] >= 2 && (fallFrom[i] < 0 ? ticks[i] <= 60 : ticks[i] <= fallFrom[i] + 40))
                {
                    Vector2 a = d - step[i];

                    if (Math.Abs(a.X) < 1.5f + 0.05f * Math.Abs(d.X) && Math.Abs(a.Y) < 6f && d.LengthSquared() > 1f)
                    {
                        if (fallFrom[i] < 0 && a.Y > 0.02f)
                            fallFrom[i] = ticks[i];

                        if (fallFrom[i] >= 0)
                        {
                            fall[i] += a.Y;
                            fallTicks[i]++;

                            if (a.Y > 0.02f)
                                falls[i]++;
                        }
                    }
                }

                step[i] = d;
                last[i] = now;
                ticks[i]++;
            }
        }

        private static void End(int i)
        {
            int item = watchItem[i];
            watchItem[i] = 0;

            // A shot gone at once tells nothing of how far it flies (one that hit at point blank, or was killed by its maker).
            if (item == 0 || ticks[i] < 3)
                return;

            bool fell = fallTicks[i] >= 4 && falls[i] * 10 >= fallTicks[i] * 7;
            float g = fallTicks[i] > 0 ? Math.Max(0f, fall[i] / fallTicks[i]) : 0f;
            MimicWeapons.Watched(item, far[i], !hitSomething[i], wasHeld[i], g, fell, Math.Max(0, fallFrom[i] - 1));
        }

        /// <summary>Everything forgotten (the copy gone).</summary>
        public static void Clear()
        {
            Array.Clear(watchItem);
            Array.Clear(tagged);
        }
    }
}
