using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

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

        // ---- Chlorophyte Rifle ------------------------------------------------------
        //
        // Set this and a projectile gains the rifle's on-hit behaviour: a spray of
        // chlorophyte that purifies whatever ground it drifts across, and a tick on the
        // player's hit combo.
        //
        // The point of putting it here rather than on a custom projectile class is that
        // the rifle fires VANILLA projectiles — a high-velocity bullet for the ordinary
        // shot and chlorophyte bullets for the burst. There is no class of ours to hang
        // behaviour on, so the flag is stamped from the spawn source instead. Both round
        // types get it, and nothing else in the game firing those projectiles is touched.

        /// <summary>Marks a shot as coming from the Chlorophyte Rifle.</summary>
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
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_ItemUse use && use.Item?.ModItem is Chlorophyte_Rifle)
                ChloRifle = true;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
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
