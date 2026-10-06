using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.Combat
{
    /// <summary>What a parry does to an enemy.</summary>
    public enum ParryClass : byte
    {
        /// <summary>Nothing: it cannot be parried.</summary>
        Never,

        /// <summary>Too big to turn aside. Its hit lands; the parry's own blow still lands on it.</summary>
        Giant,

        /// <summary>Turned aside, struck and pushed back, and staggered every so many parries; the game does not let it be knocked back.</summary>
        Heavy,

        /// <summary>The same, and the game lets it be knocked back.</summary>
        Light,
    }

    /// <summary>Why an enemy is in the class it is in.</summary>
    public enum ParryWhy : byte
    {
        /// <summary>It is nobody's enemy, or does nothing by touch.</summary>
        NoThreat,

        /// <summary>It is a shot in an enemy's place: a ball of fire, a spit, a light.</summary>
        Shot,

        /// <summary>It is a boss.</summary>
        Boss,

        /// <summary>It is a part of a boss.</summary>
        Limb,

        /// <summary>Nothing can hurt it as it is.</summary>
        Unhurt,

        /// <summary>It blows up.</summary>
        Bomb,

        /// <summary>It goes through walls: there is nothing of it to turn aside.</summary>
        Ghost,

        /// <summary>It wears the yellow star: no shield turns this off.</summary>
        Star,

        /// <summary>It is on a list of things never parried (the config, or another mod's word).</summary>
        Listed,

        /// <summary>Something in the mod speaks for it, and says not now.</summary>
        NotNow,

        /// <summary>It is staggered already: it is turning nothing on anyone, and a parry does nothing more to it.</summary>
        Reeling,

        /// <summary>It is too big.</summary>
        Big,

        /// <summary>The game does not let it be knocked back.</summary>
        Unmoved,

        /// <summary>The game lets it be knocked back.</summary>
        Pushed,
    }

    /// <summary>
    /// What can be parried, read off the thing itself: nothing here is a list of what can be,
    /// only short lists of what cannot be told from its numbers.
    ///
    /// AN ENEMY is classed at the moment of the parry, from what it is then
    /// (<see cref="Of(NPC, out ParryWhy)"/>). Never: a boss or a part of one, a shot in an
    /// enemy's place, anything nothing can hurt, a bomb, a ghost. A giant (a hitbox wider than
    /// <see cref="GiantWidth"/> or taller than <see cref="GiantHeight"/>: three players wide,
    /// two tall) is not turned aside. Of the rest, one the game lets be knocked back
    /// (NPC.knockBackResist above 0) is light, and one it does not is heavy: the game's own
    /// line, where it turns knockback off for an enemy (NPC.GetIncomingStrikeModifiers), and
    /// one an enemy's own AI moves as it fights (nine of vanilla's do). A parry does the same
    /// to both; what differs is the riposte's knockback, which is the game's.
    ///
    /// Its life is not looked at: it does not sort them (a Giant Worm's segment has 30 and
    /// cannot be pushed, a Nailhead has 4000 and can), and it is doubled and tripled by the
    /// world's difficulty, where 0 knockback stays 0.
    ///
    /// A SHOT is solid or it is not (<see cref="Solid"/>): arrows, javelins, bones, knives,
    /// stingers, spikes, seeds, darts, and bullets. Vanilla's are named here. Another mod's is
    /// solid if it carries vanilla's arrow flag, if some arrow or dart in the game shoots it,
    /// or if it says it flies as one of these (ModProjectile.AIType); its bullets only when
    /// they stop at the first thing they hit (penetrate 1). The numbers cannot tell a bolt of
    /// wood from a bolt of light, so nothing else is guessed: the rest is for the lists
    /// (Parry.AddProjectile, the config, Mod.Call).
    ///
    /// Nothing in this file touches the mod's own state, so it runs outside the game.
    /// </summary>
    public static class ParryRules
    {
        /// <summary>A hitbox wider than this, or taller than the next, is a giant's. A player is 20 by 42.</summary>
        public const int GiantWidth = 60;
        public const int GiantHeight = 84;

        // ---- enemies: what their numbers do not say ----------------------------------------------

        /// <summary>
        /// Parts of vanilla's bosses that are not bosses by their own flags, are not giants by
        /// their size and can be hurt: their hands, their arms, their segments, what is
        /// mounted on them or grows out of them, what they call up that is no small thing.
        /// The Golem's fists and Skeletron's hand are here by the rules; in the game the mod
        /// speaks for those itself (GolemFistParry, SkeletronHandParry), and it is asked
        /// before the rules are.
        /// </summary>
        private static readonly HashSet<int> BossLimbs = new()
        {
            NPCID.EaterofWorldsBody, NPCID.EaterofWorldsTail,
            NPCID.SkeletronHand,
            NPCID.PrimeCannon, NPCID.PrimeSaw, NPCID.PrimeVice, NPCID.PrimeLaser,
            NPCID.TheDestroyerBody, NPCID.TheDestroyerTail,
            NPCID.GolemFistLeft, NPCID.GolemFistRight,
            NPCID.TheHungry,
            NPCID.PlanterasHook, NPCID.PlanterasTentacle,
            NPCID.MartianSaucerTurret, NPCID.MartianSaucerCannon,
            NPCID.MoonLordFreeEye,
            NPCID.PirateShipCannon,
            NPCID.CultistDragonHead, NPCID.CultistDragonBody1, NPCID.CultistDragonBody2,
            NPCID.CultistDragonBody3, NPCID.CultistDragonBody4, NPCID.CultistDragonTail,
            NPCID.AncientCultistSquidhead,
        };

        /// <summary>Lights and spells that are enemies to the game but do not have its flag for a shot (NPCID.Sets.ProjectileNPC).</summary>
        private static readonly HashSet<int> Lights = new()
        {
            NPCID.AncientLight, NPCID.AncientDoom,
        };

        /// <summary>What blows up when it gets there.</summary>
        private static readonly HashSet<int> Bombs = new()
        {
            NPCID.ChatteringTeethBomb,
            NPCID.DD2KoboldWalkerT2, NPCID.DD2KoboldWalkerT3,
            NPCID.DD2KoboldFlyerT2, NPCID.DD2KoboldFlyerT3,
            NPCID.DD2GoblinBomberT1, NPCID.DD2GoblinBomberT2, NPCID.DD2GoblinBomberT3,
        };

        /// <summary>The class of this enemy as it is now, by its own numbers alone.</summary>
        public static ParryClass Of(NPC npc) => Of(npc, out _);

        /// <summary>The class of this enemy as it is now, by its own numbers alone, and why.</summary>
        public static ParryClass Of(NPC npc, out ParryWhy why)
        {
            if (!npc.active || npc.life <= 0)
            {
                why = ParryWhy.NoThreat;
                return ParryClass.Never;
            }

            return Kind(npc, out why);
        }

        /// <summary>
        /// The same, of anything with an enemy's numbers on it, in the world or not: the
        /// game's own sample of a type (ContentSamples) is classed by this.
        /// </summary>
        public static ParryClass Kind(NPC npc, out ParryWhy why)
        {
            if (npc.friendly || npc.townNPC || npc.damage <= 0)
            {
                why = ParryWhy.NoThreat;
                return ParryClass.Never;
            }

            int type = npc.type;

            if (NPCID.Sets.ProjectileNPC[type] || Lights.Contains(type))
            {
                why = ParryWhy.Shot;
                return ParryClass.Never;
            }

            if (Bossy(npc))
            {
                why = ParryWhy.Boss;
                return ParryClass.Never;
            }

            if (BossLimbs.Contains(type) || PartOfBoss(npc))
            {
                why = ParryWhy.Limb;
                return ParryClass.Never;
            }

            if (npc.dontTakeDamage || npc.immortal)
            {
                why = ParryWhy.Unhurt;
                return ParryClass.Never;
            }

            if (Bombs.Contains(type))
            {
                why = ParryWhy.Bomb;
                return ParryClass.Never;
            }

            if (npc.aiStyle == NPCAIStyleID.HoveringFighter && npc.noTileCollide)
            {
                why = ParryWhy.Ghost;
                return ParryClass.Never;
            }

            return Sized(npc, out why);
        }

        /// <summary>A giant by its size, or light or heavy by its weight.</summary>
        public static ParryClass Sized(NPC npc, out ParryWhy why)
        {
            if (npc.width > GiantWidth || npc.height > GiantHeight)
            {
                why = ParryWhy.Big;
                return ParryClass.Giant;
            }

            return Weighed(npc, out why);
        }

        /// <summary>
        /// Light or heavy: whether the game lets it be knocked back, as it is now. All that is
        /// asked of one the mod or a list says is to be parried whatever it is.
        /// </summary>
        public static ParryClass Weighed(NPC npc, out ParryWhy why)
        {
            if (npc.knockBackResist > 0f)
            {
                why = ParryWhy.Pushed;
                return ParryClass.Light;
            }

            why = ParryWhy.Unmoved;
            return ParryClass.Heavy;
        }

        private static bool Bossy(NPC npc) => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];

        /// <summary>
        /// A boss or a part of one, whatever else is true of it: by its own flags, by the list
        /// above, or by being one of the many parts of something that is (NPC.realLife). For
        /// what asks only that (the Meat Hook, which drags its wielder to these and never
        /// these to its wielder).
        /// </summary>
        public static bool BossOrPart(NPC npc) => Bossy(npc) || BossLimbs.Contains(npc.type) || PartOfBoss(npc);

        /// <summary>One of the many parts of something that is a boss (NPC.realLife: the part whose life is the life of all of them).</summary>
        private static bool PartOfBoss(NPC npc)
        {
            int whole = npc.realLife;

            if (whole < 0 || whole >= Main.maxNPCs || whole == npc.whoAmI)
                return false;

            NPC other = Main.npc[whole];
            return other != null && other.active && Bossy(other);
        }

        /// <summary>
        /// Whether its own AI can be stopped for a while and picked up again. Not one of the
        /// many parts of something, and nothing that moves as a worm does: a segment that
        /// misses its turn loses its place in the line.
        /// </summary>
        public static bool CanBeStopped(NPC npc)
        {
            if (npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                return false;

            return npc.aiStyle != NPCAIStyleID.Worm;
        }

        // ---- shots ---------------------------------------------------------------------------------

        /// <summary>
        /// Vanilla's solid things shot or thrown that the game does not flag as arrows. The
        /// first lot are its enemies' own; the last line is the player's, for another mod's
        /// enemy that throws them.
        /// </summary>
        private static readonly HashSet<int> SolidShots = new()
        {
            ProjectileID.WoodenArrowHostile, ProjectileID.FlamingArrow,
            ProjectileID.JavelinHostile, ProjectileID.DD2JavelinHostile, ProjectileID.DD2JavelinHostileT3,
            ProjectileID.SkeletonBone, ProjectileID.RockGolemRock, ProjectileID.SnowBallHostile,
            ProjectileID.HarpyFeather, ProjectileID.Stinger, ProjectileID.QueenBeeStinger,
            ProjectileID.Nail, ProjectileID.PineNeedleHostile, ProjectileID.OrnamentHostileShrapnel, ProjectileID.FrostShard,
            ProjectileID.SpikedSlimeSpike, ProjectileID.QueenSlimeMinionBlueSpike, ProjectileID.IceSpike, ProjectileID.JungleSpike,
            ProjectileID.RollingCactusSpike,
            ProjectileID.SeedPlantera, ProjectileID.PoisonSeedPlantera, ProjectileID.DandelionSeed,
            ProjectileID.PoisonDart, ProjectileID.PoisonDartTrap, ProjectileID.VenomDartTrap,

            ProjectileID.Shuriken, ProjectileID.ThrowingKnife, ProjectileID.PoisonedKnife, ProjectileID.BoneDagger,
            ProjectileID.Bone, ProjectileID.BoneJavelin, ProjectileID.JavelinFriendly, ProjectileID.FrostDaggerfish,
        };

        /// <summary>
        /// Vanilla's enemies' bullets: the pirate's, the snowman's, the sniper's. All three, by
        /// request. (Their penetrate is -1: the game never looks at it when a hostile shot
        /// hits a player, so it says nothing about them.)
        /// </summary>
        private static readonly HashSet<int> EnemyBullets = new()
        {
            ProjectileID.BulletDeadeye, ProjectileID.BulletSnowman, ProjectileID.SniperBullet,
        };

        /// <summary>What an arrow, a dart, a bullet in the game shoots: vanilla's and every mod's, found at load (<see cref="Learn"/>).</summary>
        private static readonly HashSet<int> ArrowKinds = new();
        private static readonly HashSet<int> DartKinds = new();
        private static readonly HashSet<int> BulletKinds = new();

        /// <summary>Once everything is loaded: what every item that is ammunition shoots.</summary>
        internal static void Learn()
        {
            Forget();

            foreach (Item item in ContentSamples.ItemsByType.Values)
            {
                if (item == null || item.shoot <= ProjectileID.None || item.ammo == AmmoID.None)
                    continue;

                if (item.ammo == AmmoID.Arrow)
                    ArrowKinds.Add(item.shoot);
                else if (item.ammo == AmmoID.Dart)
                    DartKinds.Add(item.shoot);
                else if (item.ammo == AmmoID.Bullet)
                    BulletKinds.Add(item.shoot);
            }
        }

        internal static void Forget()
        {
            ArrowKinds.Clear();
            DartKinds.Clear();
            BulletKinds.Clear();
        }

        /// <summary>
        /// Whether this shot is a solid thing, by what it is alone. Whether it is anybody's
        /// enemy is for whoever asks.
        /// </summary>
        public static bool Solid(Projectile projectile)
        {
            if (projectile.arrow)
                return true;

            int type = projectile.type;

            if (SolidKind(type) || EnemyBullets.Contains(type))
                return true;

            // A mod's shot that flies as one of vanilla's.
            int like = projectile.ModProjectile?.AIType ?? 0;

            if (like > 0 && like != type && SolidKind(like))
                return true;

            // Any other bullet: only the kind that stops at the first thing it hits.
            bool bullet = BulletKinds.Contains(type) || (like > 0 && (BulletKinds.Contains(like) || EnemyBullets.Contains(like)));

            return bullet && projectile.penetrate == 1;
        }

        private static bool SolidKind(int type) => SolidShots.Contains(type) || ArrowKinds.Contains(type) || DartKinds.Contains(type);
    }
}
