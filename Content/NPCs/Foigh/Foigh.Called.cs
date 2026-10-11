using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// What is called up to fight for a player: a minion, a sentry, and what those shoot. By
    /// request on the 8th: "any suggestion for minion damage cuz if i have 20 minions of
    /// either projectile shooting eyes or very long stardust dragon, its shredding him"; and,
    /// of what was offered, the ball (Foigh.Wild), and then "the other applies", "4/9 chance"
    /// for the ball's cuts (FoighLine), and "killing 1 stardust dragon means killing the whole
    /// dragon"; and then, of the ball by phase, the chance "normal" in his first, "1.5x
    /// phase 1" in his second and "3x phase 1" in his third. Before this all of it landed,
    /// for what the cap allowed.
    ///
    ///   TURNED ASIDE AT HIS USUAL RATE. In all three phases each hit of it is answered at
    ///   the rate he answers anything with from his second phase on: two times in three; four
    ///   times in five in his third phase. In whatever move he is in, as what keeps coming is
    ///   (Foigh.Hold), for it does not stop coming; but not when he answers nothing: broken,
    ///   down on a knee, or with his sword thrown aside. Turned aside, it is through with him
    ///   for as long as a hit of it would have been, and a shot of it that would have stopped
    ///   at him is struck out of the air; what was called up is not hurt by it. It wears
    ///   nothing off his posture and does not count as holding him at it (Foigh.Hold): what
    ///   fights for a player does not break him, and does not set him running at them. With
    ///   nothing more to be taken from him (the cap) it is turned aside every time, the same
    ///   way.
    ///
    ///   A CAP OF ITS OWN. Over any sixty ticks, what lands of it can take no more than half of
    ///   what the cap lets him lose in a second (Foigh.Hold), whatever else is hitting him: a
    ///   hit of it is cut down to what is left of that, and with less than a point left it is
    ///   turned aside. (Broken, he turns nothing aside, and the least a hit can take, one
    ///   point, gets through.)
    ///
    ///   THE STARDUST DRAGON. The game has every part of it wait one wait, its head's,
    ///   whichever part hit (Projectile.Damage, 1.4.4): turned aside, the whole of it waits as
    ///   if it had hit. Cut down by the ball, any part of it, it is cut down whole (FoighLine).
    ///
    /// THE BALL (Foigh.Wild) answers it when what lands of it has taken nine tenths of all its
    /// own cap lets it take in three seconds; and each of the ball's cuts cuts down what it
    /// goes through of it four times in nine in his first phase, six in nine in his second,
    /// and every time in his third (FoighLine).
    ///
    /// WHO DECIDES. The machine of the player whose it is, where the game works its hits out
    /// (Foigh.React): each machine counts its own player's, so two players' have a cap each.
    /// </summary>
    public partial class Foigh
    {
        /// <summary>Of what the cap lets him lose in a second, the share what fights for a player can take over any sixty ticks.</summary>
        public const float CalledShare = 0.5f;

        /// <summary>The ticks its cap is counted over: a second.</summary>
        public const int CalledWindow = 60;

        // ---- each machine's own, of its own player's ----

        /// <summary>
        /// What it took from him in each of the ticks before this one of the last
        /// CalledWindow, where in those the next goes, the sum of them, and this tick's so far.
        /// </summary>
        private readonly int[] calledRing = new int[CalledWindow - 1];
        private int calledAt;
        private int calledSum;
        private int calledNow;

        /// <summary>What a hit of what fights for this machine's player can take from him now.</summary>
        public float CalledAllowed => NPC.lifeMax * CapRate * CalledShare - calledSum - calledNow;

        /// <summary>
        /// Whether he turns aside a hit of what fights for them, rolled now: as he answers
        /// anything from his second phase on, two times in three; and in his third, four in
        /// five.
        /// </summary>
        private bool AnswersCalled()
        {
            if (phase >= 3)
                return Main.rand.Next(ThirdOf) < ThirdAnswers;

            return Main.rand.Next(SecondOf) < SecondAnswers;
        }

        /// <summary>
        /// A hit of what fights for this machine's player is about to land on him (asked from
        /// CanBeHitByProjectile, while he answers anything at all): turned aside, and true,
        /// with nothing more to be taken from him or by it, or at his usual rate. Else it
        /// lands, for no more than its own cap leaves (ModifyHitByProjectile).
        /// </summary>
        private bool CalledTurned(Projectile projectile, FoighMark mark)
        {
            if (!Capped && CalledAllowed >= 1f && !AnswersCalled())
                return false;

            // A shot of it that stops at what it hits is struck out of the air, as any is;
            // anything else, and what was called up itself, goes on, through with him for as
            // long as a hit of it would be (ShareShot: the Stardust Dragon, all of it).
            if (projectile.penetrate == 1 && !projectile.minion && !projectile.sentry)
            {
                mark.Dead = true;
                projectile.alpha = 255;
            }
            else
            {
                ShareShot(NPC, projectile, 0);
            }

            // Turned aside is not missed (Foigh.Reach); and it is seen and heard as anything
            // turned aside is, owing nothing to his posture (Foigh.Hold).
            swingLanded = true;

            if (sparkWait <= 0)
            {
                sparkWait = SparkEvery;
                Ward(Vector2.Clamp(projectile.Center, NPC.TopLeft, NPC.BottomRight));
            }

            return true;
        }

        /// <summary>On the machine working the hit out, before it lands: what fights for its player, no more than its own cap leaves.</summary>
        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (Fighting && !Passing && projectile.owner == Main.myPlayer && projectile.IsMinionOrSentryRelated)
                modifiers.SetMaxDamage(Math.Max(1, (int)CalledAllowed));
        }

        /// <summary>Something of this machine's player's has landed on him (OnHitByProjectile): counted against its own cap, if it fights for them.</summary>
        private void CalledLanded(Projectile projectile, int damageDone)
        {
            if (projectile.owner == Main.myPlayer && damageDone > 0 && projectile.IsMinionOrSentryRelated)
                calledNow += damageDone;
        }

        /// <summary>
        /// Every tick, on a machine with a player, before this tick's shots: the last tick's
        /// goes into the count, and the oldest out. It begins again empty after the road and
        /// after a passage.
        /// </summary>
        private void Calls()
        {
            if (!Fighting || Passing)
            {
                Array.Clear(calledRing, 0, calledRing.Length);
                calledAt = 0;
                calledSum = 0;
                calledNow = 0;
                return;
            }

            calledSum += calledNow - calledRing[calledAt];
            calledRing[calledAt] = calledNow;
            calledAt = (calledAt + 1) % calledRing.Length;
            calledNow = 0;
        }

        /// <summary>Whether a projectile is a part of the Stardust Dragon: its head, either kind of its body, its tail.</summary>
        public static bool Dragon(Projectile projectile) => projectile.type >= ProjectileID.StardustDragon1 && projectile.type <= ProjectileID.StardustDragon4;

        /// <summary>
        /// The waits, one for each NPC, that a projectile keeping its own hits hits by: its
        /// own; but the Stardust Dragon's body and tail by its head's, the first of their
        /// owner's there is, as the game has it (Projectile.Damage and
        /// Projectile.FindStardustDragonHeadOfOwner, 1.4.4).
        /// </summary>
        private static int[] HitsBy(Projectile projectile)
        {
            if (Dragon(projectile) && projectile.type != ProjectileID.StardustDragon1)
            {
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile head = Main.projectile[i];

                    if (head.active && head.owner == projectile.owner && head.type == ProjectileID.StardustDragon1)
                        return head.localNPCImmunity;
                }
            }

            return projectile.localNPCImmunity;
        }
    }
}
