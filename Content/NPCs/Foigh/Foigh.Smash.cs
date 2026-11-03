using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// Foigh in the Super Smash mod's battle (BattleRoyaleSupport), by request ("Can foigh also parry other mods attacks?",
    /// "yeah sure"): what the creatures of the other side send at him, from any mod or none, is answered as a player's is
    /// (Foigh.React), at the odds his phase gives, while that battle is on and he is marked onto a side:
    ///
    ///   a body that runs into him      met, as a swing: it does not land, and the creature is thrown back as far as it
    ///                                  can be; that side's touch waits half a second.
    ///   a solid shot                   struck out of the air, his sword up as it comes; half go back at whoever sent it.
    ///   anything else sent at him      stepped out of the way of: to in front of whoever sent it, or up over it.
    ///   one of our creatures' blows    met, if swung; struck aside, if thrown (BattleRoyaleSupport.IGuard).
    ///
    /// Each wears at his posture as a player's would. Only while he is free to answer (CanReact), never broken or down
    /// (Unguarded). Single player only, as the battle is.
    /// </summary>
    public partial class Foigh : BattleRoyaleSupport.IGuard
    {
        /// <summary>A side's body met: how long before it can touch him again (the battle's own wait after a touch).</summary>
        private const int TouchMet = 30;

        /// <summary>Shots struck out of the air to be sent back: made in his own turn, so that they are his side's.</summary>
        private List<(Vector2 at, Vector2 going, int type, Vector2 toward)> smashBacks;

        /// <summary>His side in the battle; -1 with no battle, or on none.</summary>
        private int SmashSide => Main.netMode == NetmodeID.SinglePlayer && BattleRoyaleSupport.Battle ? BattleRoyaleSupport.Side(NPC) : -1;

        /// <summary>Whether what the side <paramref name="theirs"/> sends can be answered now.</summary>
        private bool SmashAnswers(int theirs)
        {
            int mine = SmashSide;
            return mine > 0 && theirs > 0 && theirs != mine && Fighting && !Unguarded && CanReact;
        }

        /// <summary>
        /// Every tick, his: shots struck last tick sent back; then each shot of the other side's about to reach him is
        /// rolled for, once, so that he is doing it when it gets there.
        /// </summary>
        private void SmashReact()
        {
            int mine = SmashSide;

            if (mine <= 0)
            {
                smashBacks?.Clear();
                return;
            }

            if (smashBacks != null && smashBacks.Count > 0)
            {
                foreach (var back in smashBacks)
                    SendBackToward(back.toward, back.at, back.going, back.type);

                smashBacks.Clear();
            }

            if (!CanReact || Unguarded)
                return;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                int theirs = BattleRoyaleSupport.Side(p);

                if (theirs <= 0 || theirs == mine || p.damage <= 0)
                    continue;

                FoighMark mark = p.GetGlobalProjectile<FoighMark>();

                if (mark.Dead || mark.Rolled(NPC))
                    continue;

                bool solid = ParryRules.Solid(p);

                if (!Coming(p, solid ? GuardLead : DodgeLead))
                    continue;

                SmashRoll(p, solid, mark);

                // Gone, or busy: nothing more is asked of him this tick.
                if (!CanReact)
                    return;
            }
        }

        /// <summary>The roll for a creature's shot, once: struck (the sword up now) or stepped out of the way of.</summary>
        private void SmashRoll(Projectile p, bool solid, FoighMark mark)
        {
            NPC maker = BattleRoyaleSupport.Maker(p);
            Vector2 from = maker?.Center ?? p.Center - p.velocity * p.MaxUpdates * DodgeLead;

            if (solid)
            {
                bool struck = Answers(ShotsStruck);

                mark.Set(NPC, struck ? FoighMark.Struck : FoighMark.Lands, struck && Main.rand.NextBool());

                if (struck)
                    GuardedToward(from);

                return;
            }

            // (Out of the way of what has nobody behind it to step toward: it lands.)
            bool dodged = maker != null && Answers(ShotsDodged);

            mark.Set(NPC, dodged ? FoighMark.Dodged : FoighMark.Lands, false);

            if (dodged)
                DodgedFrom(maker, null);
        }

        /// <summary>
        /// A creature's shot he is to strike out of the air, on him (FoighMark.Colliding): it does not land, it is gone at
        /// the end of its tick, and it goes back if it was to. True if struck.
        /// </summary>
        internal bool SmashStruck(Projectile projectile, FoighMark mark)
        {
            if (!SmashAnswers(BattleRoyaleSupport.Side(projectile)))
                return false;

            Vector2 at = Vector2.Clamp(projectile.Center, NPC.TopLeft, NPC.BottomRight);
            NPC maker = BattleRoyaleSupport.Maker(projectile);
            Vector2 from = maker?.Center ?? at - projectile.velocity * 8f;

            mark.Dead = true;
            projectile.alpha = 255;

            ParryEffects.Spark(at);
            Wear(PostureStruck, true);
            GuardedToward(from);

            if (mark.Returns)
                (smashBacks ??= new()).Add((at, projectile.velocity * projectile.MaxUpdates, projectile.type, from));

            return true;
        }

        /// <summary>
        /// A body of the other side's running into him: met, three times in five (as a swing is, by his phase). The battle
        /// asks this of every one of them every tick, before it looks at whether they touch: so that is looked at here.
        /// </summary>
        public override bool CanBeHitByNPC(NPC attacker)
        {
            int theirs = BattleRoyaleSupport.Side(attacker);

            if (!SmashAnswers(theirs) || !attacker.Hitbox.Intersects(NPC.Hitbox) || !Answers(SwingsMet))
                return true;

            Rectangle met = Rectangle.Intersect(attacker.Hitbox, NPC.Hitbox);
            int away = attacker.Center.X >= NPC.Center.X ? 1 : -1;

            ParryEffects.Clash(met.Center.ToVector2(), new Vector2(away, -0.3f));
            CCBypass.Warn(NPC);

            // Thrown back, as far as it can be.
            if (attacker.knockBackResist > 0f)
            {
                attacker.velocity.X = away * ThrownBack.X * attacker.knockBackResist;

                if (!attacker.noGravity)
                    attacker.velocity.Y = ThrownBack.Y * attacker.knockBackResist;

                attacker.netUpdate = true;
            }

            Wear(PostureMet, false);
            ClashedToward(attacker.Center);
            BattleRoyaleSupport.Shield(NPC, theirs, TouchMet);
            return false;
        }

        /// <summary>
        /// A cut of his that the battle turned onto a creature (BattleRoyaleSupport.Struck) leaves the wound his sword leaves,
        /// as on a player (by request: "and he also draw ultrableeding on NPCs too"): a creature's Ultra Bleeding
        /// (UltraBleedingGeneric). Asked by whatever of his cuts would have wounded a player there.
        /// </summary>
        internal static void WoundStruck()
        {
            NPC struck = BattleRoyaleSupport.Struck;

            if (struck != null && struck.active)
                UltraBleedingGeneric.Cut(struck);
        }

        /// <summary>One of our creatures' blows on him (BattleRoyaleSupport.Hurt): met if swung, struck aside if thrown.</summary>
        public bool Turns(int side, bool thrown, Vector2 from)
        {
            if (!SmashAnswers(side) || !Answers(thrown ? ShotsStruck : SwingsMet))
                return false;

            Vector2 at = Vector2.Clamp(from, NPC.TopLeft, NPC.BottomRight);

            if (thrown)
            {
                ParryEffects.Spark(at);
                Wear(PostureStruck, true);
                GuardedToward(from);
                return true;
            }

            int away = from.X >= NPC.Center.X ? 1 : -1;

            ParryEffects.Clash(at, new Vector2(away, -0.3f));
            CCBypass.Warn(NPC);
            Wear(PostureMet, false);
            ClashedToward(from);
            return true;
        }
    }
}
