using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Ranged;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PisAndDag.Content.Projectiles
{
    /// <summary>
    /// Per-projectile mod data, reached with projectile.PAD().
    ///
    /// Instanced for the same reason as PADGlobalNPC: a field here belongs to one shot.
    /// This is where a projectile carries something its ai[] slots have no room for — a
    /// source item, a flag saying it came from a set bonus, a stack that should not reset
    /// when the projectile is recycled into a different type.
    ///
    /// Anything stored here is LOCAL. ai[] rides along in the spawn packet and this does
    /// not, so a field that other clients must agree on either goes in ai[] or gets
    /// written and read through SendExtraAI / ReceiveExtraAI.
    /// </summary>
    public class PADGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

         
        public bool Chomp;

        /// <summary>How long one bite holds the arrow still. It damages throughout.</summary>
        public int FreezeTicks ;

        /// <summary>How long after letting go before it may bite again. It damages nothing in here.</summary>
        public int ChompCool;

        /// <summary>Ticks LEFT of that cooldown. Zero means ready.</summary>
        private int ChompCoolDown;


        private float FreezeTimer;

        private Vector2 storedVelocity;

        /// <summary>
        /// The heading the arrow had when it bit, held for as long as it is stuck.
        ///
        /// A vanilla arrow is aiStyle 1 and repoints its own sprite from velocity every
        /// tick, and its AI runs before this hook does. On a frozen arrow that velocity is
        /// the single tick of gravity it just gained, so left alone it stands on its nose.
        /// </summary>
        private float storedRotation;
        public bool ChloRifle;

        /// <summary>Set once this shot has connected, so a miss can be told from a hit.</summary>
        private bool chloConnected;

        /// <summary>Spill radius, in blocks. Rolled per hit.</summary>
        private const float MinSpillTiles = 3f;
        private const float MaxSpillTiles = 10f;

        /// <summary>
        /// How much harder the high-velocity round throws its spray than a burst round.
        ///
        /// The ordinary shot arrives carrying far more energy, so its chlorophyte is
        /// thrown much further past the target. Same direction as the shot, just further —
        /// negating this direction was a misreading of "way back" and it looked like the
        /// spray was bouncing off the enemy back at the player.
        /// </summary>
        private const float HighVelocityThrow = 2.2f;

        /// <summary>
        /// Reads the flag off whatever fired this shot.
        ///
        /// OnSpawn rather than SetDefaults, because these are vanilla projectiles with no
        /// SetDefaults of ours to edit. The source is only meaningful on the side that
        /// actually fired — a remote client rebuilding this from a packet gets a sync
        /// source and no flag — but everything the flag drives runs owner-side anyway,
        /// so there is nothing for the other side to miss.
        /// </summary>
        /// 
        public override void AI(Projectile projectile)
        {
            if (Chomp)
            {

                if (FreezeTimer > 0f)
                {
                    FreezeTimer--;
                    projectile.velocity = Vector2.Zero;

                    // Stopped, so hold the heading it arrived on rather than the gravity
                    // vanilla just handed it.
                    projectile.rotation = storedRotation;

                    // Timer just ran out — pick the flight back up exactly where it left
                    // off, and only NOW start the cooldown. Starting it on the bite would
                    // spend the fifteen chewing ticks inside the cooldown, which is the
                    // one stretch that is supposed to hurt.
                    if (FreezeTimer <= 0f)
                    {
                        projectile.velocity = storedVelocity;
                        ChompCoolDown = ChompCool;
                    }
                }
                else if (ChompCoolDown > 0)
                {
                    ChompCoolDown--;
                }
            }
        }
         
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse use && use.Item?.ModItem is Chlorophyte_Rifle)
                ChloRifle = true;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            // Biting is allowed for the whole freeze — that is the chomp. Only the
            // cooldown that follows it stops damage.
            //
            // How MANY times it bites in those fifteen ticks is localNPCHitCooldown's
            // business, set on the arrow at spawn. At 10 it lands twice per lodge; the
            // plan's five hits per lodge is FreezeTicks / 5, so 3.
            if (Chomp && ChompCoolDown > 0)
                return false;

            return base.CanHitNPC(projectile, target);
        }
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Chomp)
            {
                 
                // Only the bite that BEGINS a lodge starts the hold. The bites that land
                // during it must not touch FreezeTimer — pushing it back to full on every
                // hit is what made the arrow lodge forever, and now that it damages while
                // stuck it would happen on every single chew.
                if (FreezeTimer <= 0f)
                {
                    storedVelocity = projectile.velocity;
                    storedRotation = projectile.rotation;
                    FreezeTimer = FreezeTicks;
                }
            }
            if (!ChloRifle)
                return;

            // Captured BEFORE chloConnected is set, because the combo rule below depends
            // on whether this round had already hit something.
            bool firstHit = !chloConnected;
            chloConnected = true;

            float reach = Main.rand.NextFloat(MinSpillTiles, MaxSpillTiles);
            bool owner = projectile.owner == Main.myPlayer;

            // The spray is what purifies — there is no separate patch of converted ground.
            // Every client draws its own leaves so the effect is seen everywhere, but only
            // the firing client's leaves write tiles.
            //
            // The cone always opens ALONG the shot, so the spill carries on through the
            // target. The high-velocity round simply throws its spray much further, since
            // it arrives with much more behind it.
            //
            // Velocity is read before anything else touches the projectile, because one
            // that dies on impact may have had it zeroed by the time this runs.
            bool highVelocity = projectile.type == ProjectileID.BulletHighVelocity;

            float throwSpeed = highVelocity ? HighVelocityThrow : 1f;

            ChlorophyteSpill.SpawnBurst(projectile.Center, projectile.velocity, reach, owner, throwSpeed);

            // Two rules decide whether this hit advances the streak, and both of them are
            // about keeping "consecutive shots" meaning shots rather than hits.
            //
            //   firstHit  — a round that pierces two enemies is still one shot. Without
            //               this a single bullet through a crowd could arm the burst on
            //               its own, which is not a streak, it is a lucky line-up.
            //
            //   the type  — only the ordinary high-velocity round counts. The burst fires
            //               ten chlorophyte bullets at once, so letting those count would
            //               have every burst instantly re-arm the next one and the weapon
            //               would never leave burst mode.
            //
            // They still spill and purify; they just do not feed the counter.
            if (owner && firstHit && highVelocity)
                Main.player[projectile.owner].PIS().ChloRegisterHit();
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (!ChloRifle || chloConnected)
                return;

            // A round that expired without touching anything is the miss that breaks the
            // combo. Checked here rather than on a timer so "consecutive" means what it
            // says: the streak ends the moment a shot fails to land.
            //
            // Restricted to the ordinary round for the same reason it is above, and this
            // half matters more. A burst throws ten chlorophyte bullets that mostly miss
            // and expire a second or two later — by which time the player may already
            // have landed a fresh shot and started a new streak. Letting those expiries
            // break the combo would quietly kill every streak begun in the wake of a
            // burst. Chlorophyte rounds are neutral: they neither build it nor end it.
            if (projectile.owner == Main.myPlayer && projectile.type == ProjectileID.BulletHighVelocity)
                Main.player[projectile.owner].PIS().ChloBreakCombo();
        }
    }
}
