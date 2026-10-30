using System;
using Microsoft.Xna.Framework;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// How the Everclear Mirror's copy fights: what it sees, what it wants, what it does. Nothing here touches the game:
    /// it is handed a picture of the moment (<see cref="Situation"/>) and gives back what to press
    /// (<see cref="Intent"/>), so the same code runs in the game (MimicSense builds the picture from the world) and in
    /// the trainer that tuned its numbers (Claude outputs\MimicTrainer_r79, outside the mod: a small world with the game's movement
    /// in it and bosses that shoot, dash and slam; <see cref="MimicGenes"/> holds what it learned).
    ///
    /// Each tick it:
    ///   1. chooses what to fight: the enemy its owner's minions are told to (a right click with a summon weapon),
    ///      else the one nearest the owner, a boss before others, kept until something is much better;
    ///   2. chooses where to stand: the weapon's distance from it (up close for a sword, far for a gun), on the side
    ///      with fewer shots coming through it, above it if it can fly; and never further from its owner than its
    ///      leash;
    ///   3. plans how to move: every few ticks it tries each way it could hold the keys (left, right or neither; jump,
    ///      drop or neither; a dash either way when it has one) for a short while ahead, moving itself as the game
    ///      would (run, jump, wings, fall, the blocks in the way) and every shot and enemy on as they are going, and
    ///      takes the one that comes nearest where it wants to be without being hit, weighting what is soon over what
    ///      is late, and keeping its plan unless another is clearly better;
    ///   4. attacks whenever its target is in reach and in sight, aimed where the target will be when the shot
    ///      arrives, and whatever else it can hit while its target cannot be hit (a boss's part first); now and then
    ///      with the weapon's right click;
    ///   5. parries something that can be parried and is about to reach it, now and then (less often than it does
    ///      anything else: by request, "parry (this train less)");
    ///   6. drinks a potion when none of it would be wasted, or when it is low.
    /// </summary>
    public sealed class MimicBrain
    {
        public MimicGenes Genes;

        private readonly Random rng;

        /// <summary>The plan being held: which keys, for how many ticks more.</summary>
        private int planMove, planVert, planDash, planLeft;

        private int side;
        private int wayLeft, wayTick, way, wayBand;
        private int parryCooldown;

        /// <summary>How much of a body's usual weight it carries while it does not hurt to touch (MimicSense in the game, the trainer's Arena: the same).</summary>
        public const float DormantShare = 0.5f;

        /// <summary>Room added around such a body, in pixels each way.</summary>
        public const float DormantRoom = 24f;

        /// <summary>Whether it learns where its weapon lands best as it fights (off only while the trainer tries a weapon at set distances).</summary>
        public bool Learn = true;

        public MimicBrain(MimicGenes genes, int seed, Practice practice = null)
        {
            Genes = genes;
            rng = new Random(seed);
            Practice = practice ?? new Practice();
        }

        /// <summary>The line from a body after it, through it and on past it, as a danger (<see cref="MimicGenes.Lane"/>).</summary>
        private void AddLane(ref Situation s, int i)
        {
            if (i < 0 || s.ThreatCount >= s.Threats.Length)
                return;

            Threat th = s.Threats[i];
            Vector2 to = s.Me.Pos - th.Pos;
            float d = to.Length();

            if (d < 1f)
                return;

            s.Threats[s.ThreatCount++] = new Threat
            {
                Line = true,
                Pos = th.Pos,
                Angle = (float)Math.Atan2(to.Y, to.X),
                Length = d + 480f,
                HalfW = (th.HalfW + th.HalfH) * 0.5f,
                Weight = th.Weight * Genes.Lane,
                Life = 40,
            };
        }

        /// <summary>Forgets its plan: after a teleport, or a new weapon.</summary>
        public void Reset()
        {
            planLeft = 0;
            wayLeft = 0;
            side = 0;
        }

        // ---- what it decides ------------------------------------------------------------------------------------

        public Intent Think(ref Situation s)
        {
            Intent intent = default;
            intent.Target = -1;
            intent.AttackTarget = -1;
            intent.Aim = s.Me.Pos + new Vector2(s.Me.Facing * 64f, 0f);

            // Its weapon changed: what it learned of the last one is put by, and this one's taken up.
            Practice.Hold(s.Weapon.Id);

            // How much it reckons with things coming round after it: a shot by as much of its seen turning as it
            // trusts, a body after it by the turn it reckons such bodies make.
            for (int i = 0; i < s.ThreatCount; i++)
            {
                ref Threat th = ref s.Threats[i];
                th.HomeTurn = th.Chaser ? Genes.ChaseTurn : th.HomeTurn * Genes.HomeTrust;
            }

            // A body after it, still or not, can lunge along the line between them (Yharon, the Plaguebringer, the Exo
            // Twins charge so; a worm turns into it): that line is told of as a danger of its own, so it keeps moving
            // across it rather than waiting on it, as a player does against a charger.
            // Only the two nearest such bodies: a worm's every segment is after it too, and its body follows its head.
            if (Genes.Lane > 0.01f)
            {
                int first = -1, second = -1;
                float d1 = 1600f * 1600f, d2 = d1;

                for (int i = 0; i < s.ThreatCount; i++)
                {
                    ref Threat th = ref s.Threats[i];

                    if (!th.AfterMe || th.Line)
                        continue;

                    float d = Vector2.DistanceSquared(s.Me.Pos, th.Pos);

                    if (d < d1)
                    {
                        second = first;
                        d2 = d1;
                        first = i;
                        d1 = d;
                    }
                    else if (d < d2)
                    {
                        second = i;
                        d2 = d;
                    }
                }

                AddLane(ref s, first);
                AddLane(ref s, second);
            }

            // Out of mana for its weapon: it stops using it and rests (standing still, the game gives mana back faster)
            // until it has the share of it back that it waits for.
            if (s.Weapon.ManaCost <= 0f)
                resting = false;
            else if (s.Mana < s.Weapon.ManaCost)
                resting = true;
            else if (resting && s.Mana >= Math.Max(s.Weapon.ManaCost, s.ManaMax * Genes.ManaResume))
                resting = false;

            int t = ChooseTarget(ref s);
            intent.Target = t;
            idle = t < 0;

            // A sword or a spear only lands from within its reach of what it hits, and what it hits hurts to touch: it
            // takes on that much of its own target's touch (Commit) to get there, as a melee player does. The rest of
            // the danger (shots, other bodies) counts in full.
            if (t >= 0 && (s.Weapon.Kind == WeaponKind.Melee || s.Weapon.Kind == WeaponKind.Spear) && Genes.Commit > 0.01f)
            {
                ref Foe target = ref s.Foes[t];

                for (int i = 0; i < s.ThreatCount; i++)
                {
                    ref Threat th = ref s.Threats[i];

                    if (!th.Line && Vector2.DistanceSquared(th.Pos, target.Pos) < 1f)
                        th.Weight *= 1f - Genes.Commit;
                }
            }

            Vector2 want = Where(ref s, t);

            // A weapon that must touch, with something to touch: how much the danger on the way in and the distance from
            // where it wants to be count (CloseRisk, CloseGoal), as a melee player goes in for the hit.
            closeNow = t >= 0 && Close(s.Weapon.Kind) && !resting;
            Plan(ref s, want);

            intent.Move = planMove;
            intent.Jump = planVert == 1 || planVert == 3;
            intent.Down = planVert == 2;
            intent.Dash = planDash;

            // A jump in the air is a new press: let go and press again, once; then held like any jump.
            if (planVert == 3)
            {
                intent.JumpTap = true;
                planVert = 1;
            }

            if (t >= 0 && !resting)
                Attack(ref s, t, ref intent);

            // What it learns from: each tick it is hitting at, how far it is from what it is hitting at, whichever button
            // its way of using the weapon has down (before round 81 only the left click was counted, so a way that uses
            // the right click alone never had a tick put to it and stayed "untried": chosen again and again).
            if ((intent.Use || intent.Alt) && intent.AttackTarget >= 0)
                Practice.Attacking(Vector2.Distance(s.Me.Pos, s.Foes[intent.AttackTarget].Pos));

            Parry(ref s, ref intent);

            // A potion as soon as none of it would be wasted (it can drink one a minute at most, and has many: the longer
            // the fight, the more this is worth), and at once when it is low.
            if (s.Potions > 0 && s.PotionReady && (s.LifeMax - s.Life >= s.PotionHeal || s.Life < s.LifeMax * Genes.HealAt))
                intent.Heal = true;

            if (parryCooldown > 0)
                parryCooldown--;

            return intent;
        }

        /// <summary>
        /// The enemy to fight: the owner's minion target if it is one, else the nearest to the owner within the leash,
        /// a boss counted as nearer by <see cref="MimicGenes.BossBias"/>; the last one kept unless another is nearer by
        /// more than four tiles.
        /// </summary>
        private int ChooseTarget(ref Situation s)
        {
            if (s.OwnerTarget >= 0)
            {
                for (int i = 0; i < s.FoeCount; i++)
                {
                    if (s.Foes[i].Index == s.OwnerTarget)
                        return i;
                }
            }

            float leash = Genes.Leash * 16f;
            int best = -1;
            float bestScore = float.MaxValue;
            float keptScore = float.MaxValue;
            int kept = -1;


            for (int i = 0; i < s.FoeCount; i++)
            {
                ref Foe foe = ref s.Foes[i];
                float d = Vector2.Distance(foe.Pos, s.Owner);

                // Only what it can reach from its leash: with a blade, a spear, a yoyo or a whip, no further out than the
                // weapon reaches (before, a sea creature just past the leash was chosen, and the copy waited at the end
                // of its leash out of reach, "not attacking").
                float outward = Close(s.Weapon.Kind) ? s.Weapon.Reach + Math.Max(foe.HalfW, foe.HalfH) : 160f;

                if (d > leash + outward)
                    continue;

                float score = d - (foe.Boss ? Genes.BossBias * 16f : 0f);

                // Something small on it or its owner (a boss's minion come after them): seen to first, by ChaserBias.
                if (!foe.Boss && Math.Min(Vector2.Distance(foe.Pos, s.Me.Pos), d) < ChaserNear)
                    score -= Genes.ChaserBias * 16f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }

                if (foe.Index == s.LastTargetIndex)
                {
                    keptScore = score;
                    kept = i;
                }
            }

            if (kept >= 0 && keptScore <= bestScore + 64f)
                return kept;

            return best;
        }

        /// <summary>How near it or its owner a small foe has to be to count as one come after them.</summary>
        private const float ChaserNear = 320f;

        /// <summary>
        /// Where it wants to be: at its weapon's distance from the target (further by <see cref="MimicGenes.Targeted"/>
        /// when the target is after the copy rather than its owner), on the safer side; or by its owner.
        /// </summary>
        private Vector2 Where(ref Situation s, int t)
        {
            float leash = Genes.Leash * 16f;

            if (t < 0)
            {
                // Nothing to fight: beside the owner, behind them, at their height (standing where they stand, not
                // hovering over them: by request, it "flopped jiggly" idling in the air).
                float spread = Genes.IdleSpread * 16f;
                return s.Owner + new Vector2(-s.OwnerFacing * spread, 0f);
            }

            ref Foe foe = ref s.Foes[t];

            // The weapon's distance: what it was told to begin with (the weapon's reach and the numbers it was trained
            // with), moved toward where it has found it lands the most (Practice), as it learns this weapon.
            float told = s.Weapon.Best > 0f && s.Weapon.Kind != WeaponKind.Summon
                ? foe.HalfW + s.Weapon.Best
                : Engage(ref s.Weapon, foe.HalfW);
            float away = s.Weapon.Kind == WeaponKind.Summon || !Learn
                ? told
                : Practice.Prefer(told, s.Weapon.Reach + Math.Max(foe.HalfW, foe.HalfH), Genes.LearnPrior, Genes.Explore);
            away += foe.TargetsMe ? Genes.Targeted * 16f : 0f;

            // A weapon that has to reach is used from within its reach: whatever it was trained or has learned, it never
            // stands further off than its blade, spear, yoyo or whip reaches past the target's edge (in the game, a
            // held blade read at 64 px stood 263 px off and never swung).
            if (Close(s.Weapon.Kind))
                away = Math.Min(away, Math.Max(foe.HalfW, foe.HalfH) + Math.Max(24f, s.Weapon.Reach * 0.85f));

            // Resting for mana: further off, out of the way, where it can stand still.
            if (resting)
                away = Math.Max(away, told) * 1.5f + 64f;

            // Drawing a boss off its owner: a boss chooses the nearer player (the distance across and the distance up,
            // added: NPC.TargetClosest), so to take it the copy stands nearer it than the owner by Tank's share. It has
            // three times the life and thirty potions; whether that is worth the danger is the trainer's to say.
            if (foe.Boss && !foe.TargetsMe && Genes.Tank > 0.02f)
            {
                float ownerApart = Math.Abs(foe.Pos.X - s.Owner.X) + Math.Abs(foe.Pos.Y - s.Owner.Y);
                float tank = Math.Max(foe.HalfW + 24f, ownerApart * (1f - Genes.Tank));
                away = Math.Min(away, tank);
            }

            // Which side of it: the one it is on, unless the other is quieter by enough.
            int here = s.Me.Pos.X >= foe.Pos.X ? 1 : -1;

            if (side == 0)
                side = here;

            float costHere = SideCost(ref s, foe.Pos + new Vector2(side * away, 0f), side, foe.Pos.X);
            float costThere = SideCost(ref s, foe.Pos + new Vector2(-side * away, 0f), -side, foe.Pos.X);

            if (costThere + 1.5f < costHere)
                side = -side;

            Vector2 want = foe.Pos + new Vector2(side * away, 0f);

            if (s.Me.CanFly)
            {
                // Above it by HoverAbove; with a weapon that must touch, never so far above that it is out of reach (with
                // wings it hovered over the target, up and across both, past a held blade's reach: the across is capped
                // above, and the up kept to two fifths of it so the two together stay within the reach).
                float above = Genes.HoverAbove * 16f;

                if (Close(s.Weapon.Kind))
                    above = MathHelper.Clamp(above, -away * 0.4f, away * 0.4f);

                want.Y = foe.Pos.Y - above;
            }
            else
            {
                want.Y = s.Me.Pos.Y;

                // A weapon that must touch, and what it fights is above it (a boss in the air, the copy without wings): it
                // jumps for it, as a melee player jumps to swing, as high as its jumps take it; and stands under it, near
                // enough across that a jump brings it within reach (round 81: with a sword it stood under Plantera 130 px
                // off her edge, in reach a sixth of the time).
                if (Close(s.Weapon.Kind) && foe.Pos.Y + foe.HalfH < s.Me.Pos.Y - s.Me.HalfH)
                {
                    want.Y = Math.Max(foe.Pos.Y, s.Me.Pos.Y - JumpHeight(ref s.Me));
                    float across = Math.Max(foe.HalfW, s.Weapon.Reach * 0.5f);

                    if (Math.Abs(want.X - foe.Pos.X) > across)
                        want.X = foe.Pos.X + side * across;
                }
            }

            // Not where its owner is: what is aimed at one of them would hit both. Up close it takes the target's other
            // side from its owner; further off it stands out past them, away from the target.
            float apart = Genes.Apart * 16f;

            if (apart > 8f && Math.Abs(want.X - s.Owner.X) < apart && Math.Abs(want.Y - s.Owner.Y) < apart)
            {
                int ownerSide = s.Owner.X >= foe.Pos.X ? 1 : -1;

                if (Close(s.Weapon.Kind))
                {
                    side = -ownerSide;
                    want.X = foe.Pos.X + side * away;
                }
                else
                {
                    want.X = s.Owner.X + side * apart;
                }
            }

            // A boss that is after it: led away from its owner, Decoy tiles at the least, so what it throws about its
            // target falls far from them (on foot, along the ground).
            if (foe.Boss && foe.TargetsMe && Genes.Decoy > 1f)
            {
                float decoy = Genes.Decoy * 16f;
                Vector2 off = want - s.Owner;

                if (!s.Me.CanFly)
                    off.Y = 0f;

                float len = off.Length();

                if (len < decoy)
                {
                    Vector2 dir = len > 1f ? off / len : new Vector2(side, 0f);
                    want.X = s.Owner.X + dir.X * decoy;

                    if (s.Me.CanFly)
                        want.Y = s.Owner.Y + dir.Y * decoy;
                }
            }

            // Never past the leash from the owner.
            Vector2 fromOwner = want - s.Owner;

            if (fromOwner.Length() > leash)
                want = s.Owner + Norm(fromOwner) * leash;

            return want;
        }

        /// <summary>
        /// How high it can get from where it stands: its jump held (its speed for its ticks, then the rise left at that speed),
        /// and each jump it can make in the air on top.
        /// </summary>
        private static float JumpHeight(ref Body b)
        {
            float g = Math.Max(0.1f, b.Gravity);
            float rise = b.JumpSpeed * b.JumpSpeed / (2f * g);
            float one = b.JumpSpeed * b.JumpTicks + rise;
            float air = b.AirJumps * (b.JumpSpeed * b.AirJumpTicks + rise);
            return Math.Min(one + air, 640f);
        }

        /// <summary>A weapon that has to reach its target: a blade, a spear, a yoyo, a whip.</summary>
        private static bool Close(WeaponKind k) => k == WeaponKind.Melee || k == WeaponKind.Spear || k == WeaponKind.Yoyo || k == WeaponKind.Whip;

        /// <summary>The distance to stand from a target, by the weapon.</summary>
        private float Engage(ref Weapon w, float targetHalfWidth)
        {
            switch (w.Kind)
            {
                case WeaponKind.Melee:
                case WeaponKind.Spear:
                    return targetHalfWidth + Math.Max(16f, w.Reach * Genes.MeleeClose);

                case WeaponKind.Yoyo:
                case WeaponKind.Whip:
                    return targetHalfWidth + Math.Max(24f, w.Reach * Genes.MeleeClose);

                case WeaponKind.Summon:
                    return Genes.RangedDist * 16f * 0.75f;

                default:
                    // A spray or a gust (nothing that flies on: Dancing Sand, a flamethrower) hits harder the nearer, and
                    // only near (as asked: "closer is better"): a third of its reach off the target's edge.
                    if (w.Speed <= 0f && w.Reach < 900f)
                        return targetHalfWidth + Math.Max(48f, w.Reach * 0.3f);

                    return Math.Min(Genes.RangedDist * 16f, Math.Max(64f, w.Reach * 0.85f));
            }
        }

        /// <summary>
        /// How busy a spot is: the shots and bodies that will pass near it soon; and, by <see cref="MimicGenes.FlankBias"/>,
        /// whether it is on the same side of the target as the owner (whom the enemies are after).
        /// </summary>
        private float SideCost(ref Situation s, Vector2 spot, int sideOf, float targetX)
        {
            float cost = 0f;

            for (int i = 0; i < s.ThreatCount; i++)
            {
                ref Threat th = ref s.Threats[i];

                for (int k = 0; k <= 30; k += 10)
                {
                    if (k < th.Delay - 10 || th.Life > 0 && k >= th.Life)
                        continue;

                    Vector2 p = At(ref th, k);

                    if (th.Line)
                    {
                        float a = th.Angle + th.Turn * k;
                        Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                        p += dir * MathHelper.Clamp(Vector2.Dot(spot - p, dir), 0f, th.Length);
                    }

                    float d = Vector2.Distance(p, spot);

                    if (d < 160f)
                        cost += (1f - d / 160f) * th.Weight;
                }
            }

            cost *= Genes.SideSafety;

            if ((s.Owner.X >= targetX ? 1 : -1) == sideOf)
                cost += Genes.FlankBias * 3f;

            return cost;
        }

        // ---- moving ------------------------------------------------------------------------------------------

        private static readonly int[] Moves = { -1, 0, 1 };

        /// <summary>Danger below which a plan is kept to its end without looking again.</summary>
        private const float KeepRisk = 0.15f;

        /// <summary>
        /// Holds its plan, or makes another: every way of holding the keys tried ahead for <see cref="MimicGenes.Horizon"/>
        /// ticks, each scored by the danger met on the way and how far it ends from where it wants to be.
        /// </summary>
        private void Plan(ref Situation s, Vector2 want)
        {
            int horizon = (int)Genes.Horizon;

            if (planLeft > 0)
            {
                planLeft--;

                // Kept unless something is coming that it would meet.
                if (Risk(ref s, planMove, planVert, 0, horizon, want, out _) < KeepRisk)
                {
                    planDash = 0;
                    return;
                }
            }

            float keepCost = Cost(ref s, planMove, planVert, 0, horizon, want);
            float bestCost = float.MaxValue;
            int bestMove = 0, bestVert = 0, bestDash = 0;

            // A jump in the air (3) is worth trying only with one left to make.
            int verts = s.Me.AirJumpsLeft > 0 && !s.Me.Grounded ? 4 : 3;

            for (int m = 0; m < Moves.Length; m++)
            {
                for (int v = 0; v < verts; v++)
                {
                    float c = Cost(ref s, Moves[m], v, 0, horizon, want);

                    if (c < bestCost)
                    {
                        bestCost = c;
                        bestMove = Moves[m];
                        bestVert = v;
                        bestDash = 0;
                    }
                }
            }

            if (s.Me.CanDash)
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        float c = Cost(ref s, d, v, d, horizon, want) + Genes.DashCost;

                        if (c < bestCost)
                        {
                            bestCost = c;
                            bestMove = d;
                            bestVert = v;
                            bestDash = d;
                        }
                    }
                }
            }

            // Its plan is kept unless the best is better by the share it asks.
            if (bestCost < keepCost * (1f - Genes.Hysteresis))
            {
                planMove = bestMove;
                planVert = bestVert;
                planDash = bestDash;
            }
            else
            {
                planDash = 0;
            }

            planLeft = Math.Max(1, (int)Genes.Replan);
        }

        private float Cost(ref Situation s, int move, int vert, int dash, int horizon, Vector2 want)
        {
            float risk = Risk(ref s, move, vert, dash, horizon, want, out Vector2 end);
            float goal = (Math.Abs(end.X - want.X) + Genes.VertWeight * Math.Abs(end.Y - want.Y)) / 16f;

            // With a weapon that must touch, a way of going that passes where it wants to be is as good as one that ends
            // there: the blow lands on the way (a jump up at something in the air and down again, a run through and out).
            if (closeNow)
                goal = Math.Min(goal, nearest) + 0.2f * goal;
            float jump = (vert == 1 || vert == 3) && !s.Me.CanFly ? Genes.JumpCost : 0f;

            // With nothing to fight, wings are for keeping up, not for hanging in the air: going up costs it, unless
            // its owner is well above it.
            if (idle && s.Me.CanFly && (vert == 1 || vert == 3) && s.Owner.Y > s.Me.Pos.Y - 48f)
                jump += IdleFlyCost;

            // Resting for mana: standing still on the ground gives it back fastest (Player.UpdateManaRegen), so moving
            // costs it, by RestStill.
            if (resting && (move != 0 || vert != 0 || dash != 0))
                jump += Genes.RestStill;

            return closeNow
                ? risk * Genes.RiskWeight * Genes.CloseRisk + goal * Genes.GoalWeight * Genes.CloseGoal + jump
                : risk * Genes.RiskWeight + goal * Genes.GoalWeight + jump;
        }

        /// <summary>Fighting this tick with a weapon that must touch (a blade, a spear, a yoyo, a whip).</summary>
        private bool closeNow;

        /// <summary>The nearest the way of going last tried came to where it wants to be, as the goal counts it (Risk).</summary>
        private float nearest;

        /// <summary>What going up on wings costs it when there is nothing to fight and its owner is not above it.</summary>
        private const float IdleFlyCost = 1.5f;

        /// <summary>Nothing to fight this tick.</summary>
        private bool idle;

        /// <summary>Out of mana for its weapon, waiting for it to come back.</summary>
        private bool resting;

        /// <summary>Resting for mana now (for the game's picture of it, and the trainer).</summary>
        public bool Resting => resting;

        /// <summary>What it has learned of the weapons it has fought with: from how far each lands the most.</summary>
        public readonly Practice Practice;

        /// <summary>
        /// Moves itself ahead as the game would with these keys held, and adds up the danger met: each shot and body on
        /// as it is going, its box and its own grown by a margin that widens with time (the further ahead, the less
        /// sure), what is soon counting for more than what is late; nothing while it is still immune.
        /// </summary>
        private float Risk(ref Situation s, int move, int vert, int dash, int horizon, Vector2 want, out Vector2 end)
        {
            Body b = s.Me;
            float risk = 0f;
            float tau = Genes.RiskTau;

            if (dash != 0 && b.CanDash)
                b.Vel.X = dash * b.DashSpeed;

            // What bends toward it (a homing shot, a beam that follows, a body after it) is followed along this way of
            // going as it would follow it: turned toward where it will be, each tick, as fast as it was seen to turn.
            int chasers = 0;

            for (int i = 0; i < s.ThreatCount && i < chaseOf.Length; i++)
            {
                ref Threat th = ref s.Threats[i];
                chaseOf[i] = -1;

                if (th.HomeTurn <= 0f || chasers >= chasePos.Length)
                    continue;

                chaseOf[i] = chasers;
                chasePos[chasers] = th.Pos;
                chaseVel[chasers] = th.Vel;
                chaseAngle[chasers] = th.Angle;
                chasers++;
            }

            nearest = float.MaxValue;

            for (int tick = 1; tick <= horizon; tick++)
            {
                // A tap is a new press on its first tick only; held after it.
                Step(ref b, move, tick == 1 ? vert : vert == 3 ? 1 : vert, s.Ground);

                if (closeNow)
                    nearest = Math.Min(nearest, (Math.Abs(b.Pos.X - want.X) + Genes.VertWeight * Math.Abs(b.Pos.Y - want.Y)) / 16f);

                if (chasers > 0)
                    Chase(ref s, b.Pos, tick);

                if ((tick & 1) != 0 || tick <= s.Me.Immune)
                    continue;

                float margin = Genes.Margin + Genes.MarginGrow * tick;
                float weight = (float)Math.Exp(-tick / tau);

                for (int i = 0; i < s.ThreatCount; i++)
                {
                    ref Threat th = ref s.Threats[i];

                    if (tick < th.Delay || th.Life > 0 && tick >= th.Life)
                        continue;

                    float room = margin + th.Homing * tick;
                    float depth;

                    int c = i < chaseOf.Length ? chaseOf[i] : -1;

                    if (th.Line)
                    {
                        // A beam: how far its middle line passes from the copy's middle, against its half width, the
                        // copy's own half width across it, and the room it keeps from beams.
                        Vector2 o = At(ref th, tick);
                        float a = c >= 0 ? chaseAngle[c] : th.Angle + th.Turn * tick;
                        Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                        float along = MathHelper.Clamp(Vector2.Dot(b.Pos - o, dir), 0f, th.Length);
                        float off = Vector2.Distance(b.Pos, o + dir * along);
                        float across = Math.Abs(dir.Y) * b.HalfW + Math.Abs(dir.X) * b.HalfH;
                        float reach = th.HalfW + across + room + Genes.LineMargin;

                        if (off >= reach)
                            continue;

                        depth = (reach - off) / (room + Genes.LineMargin + 4f);
                    }
                    else
                    {
                        Vector2 p = c >= 0 ? chasePos[c] : At(ref th, tick);
                        float dx = Math.Abs(b.Pos.X - p.X) - (b.HalfW + th.HalfW + room);

                        if (dx >= 0f)
                            continue;

                        float dy = Math.Abs(b.Pos.Y - p.Y) - (b.HalfH + th.HalfH + room);

                        if (dy >= 0f)
                            continue;

                        depth = Math.Min(-dx, -dy) / (room + 4f);
                    }

                    risk += th.Weight * weight * (0.5f + Math.Min(depth, 1.5f));
                }
            }

            end = b.Pos;
            return risk;
        }

        // The ones that follow it, as followed along a way of going (Risk): which threat is which, where each is, how fast,
        // and a beam's angle.
        private readonly int[] chaseOf = new int[320];
        private readonly Vector2[] chasePos = new Vector2[64];
        private readonly Vector2[] chaseVel = new Vector2[64];
        private readonly float[] chaseAngle = new float[64];

        /// <summary>One tick of those that follow it: each turned toward where it is now by as much as it turns a tick.</summary>
        private void Chase(ref Situation s, Vector2 me, int tick)
        {
            for (int i = 0; i < s.ThreatCount && i < chaseOf.Length; i++)
            {
                int c = chaseOf[i];

                if (c < 0)
                    continue;

                ref Threat th = ref s.Threats[i];

                if (tick < th.Delay)
                {
                    chasePos[c] = At(ref th, tick);
                    continue;
                }

                if (th.Line)
                {
                    // A beam that follows: its angle toward it, and its foot carried on.
                    float wantA = (float)Math.Atan2(me.Y - chasePos[c].Y, me.X - chasePos[c].X);
                    chaseAngle[c] += MathHelper.Clamp(MathHelper.WrapAngle(wantA - chaseAngle[c]), -th.HomeTurn, th.HomeTurn);
                    chasePos[c] += chaseVel[c];
                    continue;
                }

                Vector2 v = chaseVel[c];
                float speed = v.Length();

                if (speed > 0.01f)
                {
                    float have = (float)Math.Atan2(v.Y, v.X);
                    float wantB = (float)Math.Atan2(me.Y - chasePos[c].Y, me.X - chasePos[c].X);
                    have += MathHelper.Clamp(MathHelper.WrapAngle(wantB - have), -th.HomeTurn, th.HomeTurn);
                    v = new Vector2((float)Math.Cos(have), (float)Math.Sin(have)) * speed;
                }

                v += th.Acc;
                chaseVel[c] = v;
                chasePos[c] += v;
            }
        }

        /// <summary>Where a threat (or a beam's foot) will be after <paramref name="tick"/> ticks: its velocity, and what it gains a tick.</summary>
        public static Vector2 At(ref Threat th, int tick) => th.Pos + th.Vel * tick + th.Acc * (tick * (tick + 1) * 0.5f);

        /// <summary>
        /// One tick of the game's movement, simplified (Player.HorizontalMovement, JumpMovement, WingMovement, the
        /// fall, the blocks): run toward the top speed, a jump held for its ticks, the wings after it while they last,
        /// the fall to its cap; stopped by solid blocks, landed on them.
        /// </summary>
        public static void Step(ref Body b, int move, int vert, IGround ground)
        {
            // Across.
            float top = b.Grounded ? b.RunTop : Math.Max(b.RunTop, b.CanFly ? b.FlySpeed : 0f);

            if (move != 0)
            {
                if (b.Vel.X * move < 0f)
                    b.Vel.X += move * b.RunSlow;

                if (b.Vel.X * move < b.RunBase)
                    b.Vel.X += move * b.RunAccel;
                else if (b.Vel.X * move < top)
                    b.Vel.X += move * b.RunAccel * (b.CanFly && !b.Grounded ? 0.4f : 0.2f);
            }
            else if (b.Vel.X != 0f)
            {
                float slow = b.Grounded ? b.RunSlow : b.RunSlow * 0.5f;
                b.Vel.X = Math.Abs(b.Vel.X) <= slow ? 0f : b.Vel.X - Math.Sign(b.Vel.X) * slow;
            }

            if (Math.Abs(b.Vel.X) > Math.Max(top, b.DashSpeed))
                b.Vel.X = Math.Sign(b.Vel.X) * Math.Max(top, Math.Abs(b.Vel.X) * 0.92f);

            // Up and down. A tap (3) in the air starts a jump of its own if one is left (a bottle's, a jar's); on the ground,
            // or with none left, it is a jump held.
            if (vert == 3)
            {
                if (!b.Grounded && b.AirJumpsLeft > 0)
                {
                    b.AirJumpsLeft--;
                    b.Vel.Y = -b.JumpSpeed;
                    b.JumpLeft = b.AirJumpTicks;
                }

                vert = 1;
            }

            if (vert == 1)
            {
                if (b.Grounded)
                {
                    b.Vel.Y = -b.JumpSpeed;
                    b.JumpLeft = b.JumpTicks;
                    b.Grounded = false;
                }
                else if (b.JumpLeft > 0)
                {
                    b.Vel.Y = -b.JumpSpeed;
                    b.JumpLeft--;
                }
                else if (b.FlyLeft > 0)
                {
                    b.Vel.Y -= b.ConstantAscend;

                    if (b.Vel.Y > 0f)
                        b.Vel.Y -= b.AscentFalling;
                    else if (b.Vel.Y > -b.JumpSpeed * b.MaxCanAscend)
                        b.Vel.Y -= b.AscentRising;

                    if (b.Vel.Y < -b.JumpSpeed * b.MaxAscent)
                        b.Vel.Y = -b.JumpSpeed * b.MaxAscent;

                    b.FlyLeft--;
                }
            }
            else
            {
                b.JumpLeft = 0;
            }

            b.Vel.Y += b.Gravity;
            float fall = vert == 1 && b.FlyLeft <= 0 && b.CanFly ? b.MaxFall * 0.35f : b.MaxFall;

            if (b.Vel.Y > fall)
                b.Vel.Y = fall;

            // The blocks: across, then up and down, each undone where it would be inside one.
            if (ground != null)
            {
                Vector2 was = b.Pos;
                b.Pos.X += b.Vel.X;

                if (Blocked(ref b, ground))
                {
                    b.Pos.X = was.X;
                    b.Vel.X = 0f;
                }

                float vy = b.Vel.Y;
                b.Pos.Y += vy;
                bool down = vert == 2;

                if (Blocked(ref b, ground) || vy > 0f && !down && OnPlatform(ref b, ground))
                {
                    b.Pos.Y = was.Y;

                    if (vy > 0f)
                    {
                        b.Grounded = true;
                        b.FlyLeft = b.FlyMax;
                        b.AirJumpsLeft = b.AirJumps;
                    }

                    b.Vel.Y = 0f;
                }
                else if (vy != 0f)
                {
                    b.Grounded = false;
                }
            }
            else
            {
                b.Pos += b.Vel;
            }
        }

        private static bool Blocked(ref Body b, IGround ground)
        {
            int x0 = (int)Math.Floor((b.Pos.X - b.HalfW) / 16f);
            int x1 = (int)Math.Floor((b.Pos.X + b.HalfW - 0.01f) / 16f);
            int y0 = (int)Math.Floor((b.Pos.Y - b.HalfH) / 16f);
            int y1 = (int)Math.Floor((b.Pos.Y + b.HalfH - 0.01f) / 16f);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    if (ground.Solid(x, y))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Feet on a platform's top this tick (it stops a fall, not a rise, and not a drop through it).</summary>
        private static bool OnPlatform(ref Body b, IGround ground)
        {
            float feet = b.Pos.Y + b.HalfH;
            int y = (int)Math.Floor(feet / 16f);

            if (feet - y * 16f > 8f)
                return false;

            int x0 = (int)Math.Floor((b.Pos.X - b.HalfW) / 16f);
            int x1 = (int)Math.Floor((b.Pos.X + b.HalfW - 0.01f) / 16f);

            for (int x = x0; x <= x1; x++)
            {
                if (ground.Platform(x, y))
                    return true;
            }

            return false;
        }

        /// <summary>A direction of length one, or none (the game's SafeNormalize, without the game).</summary>
        private static Vector2 Norm(Vector2 v)
        {
            float length = v.Length();
            return length > 1e-4f ? v / length : Vector2.Zero;
        }

        // ---- fighting ----------------------------------------------------------------------------------------

        /// <summary>
        /// At the target, when it is in reach and in sight (a summon weapon does not need either): aimed where it will be
        /// when the shot gets there (its speed and how far, times <see cref="MimicGenes.Lead"/>), straight at it up
        /// close. Its right click now and then, held for a moment, when the weapon has one.
        /// </summary>
        private void Attack(ref Situation s, int t, ref Intent intent)
        {
            // The target when it can be hit now; else whatever else can be, a boss's first (a worm's segments out of
            // the ground while the one it follows is under it, a boss's other parts): a player keeps hitting what is in
            // front of them while they go where they mean to.
            if (!CanHit(ref s, t))
            {
                int other = -1;
                float best = float.MaxValue;

                for (int i = 0; i < s.FoeCount; i++)
                {
                    if (i == t || !CanHit(ref s, i))
                        continue;

                    ref Foe f = ref s.Foes[i];
                    float d = Vector2.Distance(f.Pos, s.Me.Pos) - (f.Boss ? Genes.BossBias * 16f : 0f);

                    if (d < best)
                    {
                        best = d;
                        other = i;
                    }
                }

                if (other >= 0)
                    t = other;
            }

            intent.AttackTarget = t;
            ref Foe foe = ref s.Foes[t];
            ref Weapon w = ref s.Weapon;

            Vector2 from = s.Me.Pos;
            float dist = Vector2.Distance(from, foe.Pos);
            float reach = w.Reach + Math.Max(foe.HalfW, foe.HalfH);

            Vector2 aim = foe.Pos;

            if (w.Speed > 0.5f && w.Kind != WeaponKind.Melee)
            {
                float ticks = Math.Min(40f, dist / w.Speed) * Genes.Lead;
                aim = foe.Pos + foe.Vel * ticks;

                // A shot that falls (a thrown hammer, a javelin, a grenade: its fall read off its own shots as they fly,
                // MimicSystem): thrown up by what it drops over its flight, as a player lobs it.
                if (w.Gravity > 0.005f)
                {
                    float flight = Math.Max(0f, Vector2.Distance(aim, from) / w.Speed - w.GravityAfter);
                    aim.Y -= Math.Min(0.5f * w.Gravity * flight * flight, 640f);
                }
            }

            intent.Aim = aim;

            bool inReach = w.Kind == WeaponKind.Summon || dist <= reach;
            bool seen = foe.Seen || w.Kind == WeaponKind.Summon || w.ThroughWalls;

            if (!inReach || !seen)
                return;

            // How far it is fighting from, by the distance from the target's edge (Practice.Bands): what a way of using the
            // weapon lands is kept for each, so a right click that lands up close (a dash, a parry, a slam) and a left that
            // lands far are each used where they land ("when" as well as "how").
            Practice.Band = Practice.BandOf(dist - Math.Max(foe.HalfW, foe.HalfH), Practice.Band);

            // How it uses the weapon: one of the ways every weapon is tried (Practice.Ways), a bout at a time, the way
            // that lands the most chosen most (Practice.PickWay), the others still tried now and then, and more freely
            // against a target dummy. Nothing about any weapon is told: a charge to loose, a right click to alternate with,
            // a coin to shoot are found by what lands. Before anything is learned, holding the left click is reckoned at
            // HoldPrior of the rate it lands overall, the right click at AltRate's share, the rest at a little less; what
            // it learns outweighs that after about WayPrior ticks of a way. A bout is ended early when the distance band
            // changes, so a way that lands only up close is not kept on far off.
            if (wayLeft <= 0 || wayBand != Practice.Band && wayTick >= 12)
            {
                way = Practice.PickWay(w.HasAlt, Genes.AltRate / 0.5f, Genes.Explore * (s.Practising ? 4f : 1f), Genes.HoldPrior, Genes.WayPrior);
                wayTick = 0;
                wayBand = Practice.Band;
                wayLeft = way == Practice.WayRight ? 40 : way == Practice.WayCharge ? 108 : way == Practice.WayLongCharge ? 228 : way == Practice.WaySetUp ? 90 : 80;
            }

            Practice.Way = way;
            int k = wayTick++;
            wayLeft--;

            switch (way)
            {
                case Practice.WayRight:
                    intent.Alt = true;
                    break;

                case Practice.WayCharge:
                    intent.Use = k % 54 < 50;      // held, then let go for a moment
                    break;

                case Practice.WayLongCharge:
                    intent.Use = k % 114 < 110;
                    break;

                case Practice.WayAlternate:
                    if (k / 20 % 2 == 0)
                        intent.Use = true;
                    else
                        intent.Alt = true;
                    break;

                case Practice.WaySetUp:
                    // a right click, a wait while what it put out settles, then the left click at it if it is still out
                    // (else at the foe)
                    if (k < 3)
                    {
                        intent.Alt = true;
                    }
                    else if (k >= 25)
                    {
                        if (s.OwnShotValid)
                        {
                            float fly = Vector2.Distance(from, s.OwnShot) / Math.Max(8f, w.Speed);
                            intent.Aim = s.OwnShot + s.OwnShotVel * Math.Min(fly, 30f);
                        }

                        intent.Use = true;
                    }
                    break;

                default:
                    intent.Use = true;
                    break;
            }
        }

        /// <summary>A foe its weapon reaches and sees from where it is (a summon weapon reaches anything).</summary>
        private static bool CanHit(ref Situation s, int i)
        {
            ref Foe foe = ref s.Foes[i];
            ref Weapon w = ref s.Weapon;

            if (w.Kind == WeaponKind.Summon)
                return true;

            float reach = w.Reach + Math.Max(foe.HalfW, foe.HalfH);
            return Vector2.Distance(s.Me.Pos, foe.Pos) <= reach && (foe.Seen || w.ThroughWalls);
        }

        /// <summary>
        /// A parry, now and then, when something that can be parried will reach its arc in about the parry's wind-up
        /// (<see cref="Situation.ParryStartup"/> ticks) and it is facing or can face it. <see cref="MimicGenes.ParryRate"/>
        /// of the chances it is given.
        /// </summary>
        private void Parry(ref Situation s, ref Intent intent)
        {
            if (!s.ParryReady || parryCooldown > 0)
                return;

            int look = (int)Genes.ParryLook;
            float reach = s.ParryReach;

            for (int i = 0; i < s.ThreatCount; i++)
            {
                ref Threat th = ref s.Threats[i];

                if (!th.Parryable || th.Line)
                    continue;

                for (int k = s.ParryStartup; k <= s.ParryStartup + look; k += 2)
                {
                    if (k < th.Delay || th.Life > 0 && k >= th.Life)
                        continue;

                    Vector2 p = At(ref th, k);
                    Vector2 me = s.Me.Pos + s.Me.Vel * k;

                    if (Vector2.Distance(p, me) > reach + Math.Max(th.HalfW, th.HalfH))
                        continue;

                    parryCooldown = 30;

                    if (rng.NextDouble() >= Genes.ParryRate)
                        return;

                    intent.Parry = true;
                    intent.ParryFacing = th.Pos.X >= s.Me.Pos.X ? 1 : -1;
                    return;
                }
            }
        }
    }

    // ---- the picture it is given -------------------------------------------------------------------------------

    /// <summary>Its own body and how it can move, as the game has it this tick.</summary>
    public struct Body
    {
        /// <summary>Its middle, and its velocity, in pixels and pixels a tick.</summary>
        public Vector2 Pos, Vel;
        public float HalfW, HalfH;
        public int Facing;

        /// <summary>Top speed on foot (maxRunSpeed), with boots (accRunSpeed, if higher), acceleration and slowing.</summary>
        public float RunBase, RunTop, RunAccel, RunSlow;
        public float JumpSpeed;
        public int JumpTicks, JumpLeft;
        public float Gravity, MaxFall;

        public bool Grounded;

        /// <summary>Wings (or rockets): whether it has any, how many ticks are left and full, how fast across, and how they lift.</summary>
        public bool CanFly;
        public int FlyLeft, FlyMax;
        public float FlySpeed, ConstantAscend, AscentFalling, AscentRising, MaxCanAscend, MaxAscent;

        public bool CanDash;
        public float DashSpeed;

        /// <summary>Jumps it can make in the air (bottles, a jar), how many are left, and how long each is held, in ticks.</summary>
        public int AirJumps, AirJumpsLeft, AirJumpTicks;

        /// <summary>Ticks of immunity left after a hit.</summary>
        public int Immune;
    }

    /// <summary>Something that can hurt it: a shot, or an enemy's body.</summary>
    public struct Threat
    {
        public Vector2 Pos, Vel;
        public float HalfW, HalfH;

        /// <summary>How much it matters: its damage against the copy's life, at least a little.</summary>
        public float Weight;

        public bool Parryable;

        /// <summary>What its velocity gains each tick (a fall, a shot speeding up).</summary>
        public Vector2 Acc;

        /// <summary>Ticks before it can hurt (a warning, not yet a hit), and ticks it has left (0: as long as it is seen).</summary>
        public int Delay, Life;

        /// <summary>
        /// A beam or a line (a deathray, a lance's path): from <see cref="Pos"/> along <see cref="Angle"/> for
        /// <see cref="Length"/> pixels, <see cref="HalfW"/> either side of it, turning <see cref="Turn"/> radians a tick.
        /// </summary>
        public bool Line;
        public float Angle, Length, Turn;

        /// <summary>It bends toward whoever it is after: pixels a tick its path may come off a straight line.</summary>
        public float Homing;

        /// <summary>
        /// It turns toward the copy (a homing shot, a beam that follows, a body after it): the most radians a tick its
        /// heading (a beam's angle) turns. Followed as it would follow the copy along each way of going it tries.
        /// </summary>
        public float HomeTurn;

        /// <summary>A body that is after the copy (it turns toward it as it comes, by <see cref="MimicGenes.ChaseTurn"/>).</summary>
        public bool Chaser;

        /// <summary>
        /// A body whose target is the copy, moving or not: what may lunge at it, along the line between them (a charge
        /// comes along that line; <see cref="MimicGenes.Lane"/>).
        /// </summary>
        public bool AfterMe;
    }

    /// <summary>An enemy it could fight.</summary>
    public struct Foe
    {
        public Vector2 Pos, Vel;
        public float HalfW, HalfH;
        public bool Boss;

        /// <summary>A clear line from the copy to it.</summary>
        public bool Seen;

        /// <summary>The game's index for it (NPC.whoAmI), to keep the same one from tick to tick.</summary>
        public int Index;

        /// <summary>It is after the copy (its target is the copy, not the owner).</summary>
        public bool TargetsMe;
    }

    public enum WeaponKind
    {
        Melee,
        Spear,
        Yoyo,
        Whip,
        Ranged,
        Magic,
        Summon,
    }

    /// <summary>What it is holding, as far as fighting with it goes.</summary>
    public struct Weapon
    {
        public WeaponKind Kind;

        /// <summary>How far it hits from the copy's middle, in pixels.</summary>
        public float Reach;

        /// <summary>How fast its shot goes, in pixels a tick (0 for none).</summary>
        public float Speed;

        public bool HasAlt;

        /// <summary>Its shots go through blocks, so it does not need a clear line.</summary>
        public bool ThroughWalls;

        /// <summary>Mana one use of it costs (0 for none).</summary>
        public float ManaCost;

        /// <summary>
        /// Which weapon it is (an item's type in the game, a weapon's number in the trainer): what it learns is kept for
        /// each weapon apart.
        /// </summary>
        public int Id;

        /// <summary>Its kind among those the trainer sorted every weapon into (MimicWeapons): which numbers it fights by.</summary>
        public int Cluster;

        /// <summary>
        /// How far from the target's edge the trainer found this very weapon does best (tried at several distances in
        /// fights against every boss it knows; MimicWeapons.BestOf), in pixels; 0 when it was never tried.
        /// </summary>
        public float Best;

        /// <summary>A blade held out and swung by a shot of its own (Ark of the Elements, Murasama): MimicWeapons.HeldOut.</summary>
        public bool HeldOut;

        /// <summary>Its class among the four (MimicWeapons.ClassOf): 0 melee, 1 ranged (with rogue and thrown), 2 magic, 3 summon (with whips).</summary>
        public int Class;

        /// <summary>How it is used (MimicWeapons.Styles: a swung blade, a held-out blade, a spear, a thrown weapon, a shooter...).</summary>
        public int Style;

        /// <summary>
        /// What its shot falls by, in pixels a tick gained each tick (0: it flies straight): read off its own shots as they
        /// fly in the game (MimicSystem's watch on them), the weapon's own in the trainer. It lobs by it (Attack).
        /// </summary>
        public float Gravity;

        /// <summary>Ticks its shot flies straight before it begins to fall (an arrow's), as seen.</summary>
        public int GravityAfter;
    }

    /// <summary>The blocks, as far as moving goes.</summary>
    public interface IGround
    {
        bool Solid(int x, int y);

        bool Platform(int x, int y);
    }

    /// <summary>Everything it is told about the moment.</summary>
    public struct Situation
    {
        public Body Me;
        public Vector2 Owner;
        public int OwnerFacing;

        /// <summary>The owner's minion target, as an NPC index, or -1.</summary>
        public int OwnerTarget;

        /// <summary>The NPC index it was fighting last tick, or -1.</summary>
        public int LastTargetIndex;

        public Threat[] Threats;
        public int ThreatCount;
        public Foe[] Foes;
        public int FoeCount;

        public Weapon Weapon;
        public float Life, LifeMax;

        /// <summary>Its mana and the most it can have.</summary>
        public float Mana, ManaMax;
        public int Potions;
        public bool PotionReady;

        /// <summary>What one of its potions heals.</summary>
        public float PotionHeal;

        public bool ParryReady;
        public int ParryStartup;
        public float ParryReach;

        public IGround Ground;

        /// <summary>
        /// Its own shot made by its weapon's right click (the newest still out, MimicSense in the game), for a weapon whose
        /// left click is aimed at what its right click put out (a coin to shoot, a mine to set off): where it is, how fast.
        /// </summary>
        public bool OwnShotValid;
        public Vector2 OwnShot, OwnShotVel;

        /// <summary>What it fights cannot be harmed by it or hurt it (a target dummy): it tries its ways of using a weapon more freely.</summary>
        public bool Practising;
    }

    /// <summary>What it presses this tick.</summary>
    public struct Intent
    {
        public int Move;
        public bool Jump, Down;

        /// <summary>The jump is a new press this tick (let go, then pressed): a jump in the air.</summary>
        public bool JumpTap;
        public int Dash;
        public bool Use, Alt;
        public Vector2 Aim;
        public bool Parry;
        public int ParryFacing;
        public bool Heal;

        /// <summary>Which of the foes it gave (an index into <see cref="Situation.Foes"/>), or -1.</summary>
        public int Target;

        /// <summary>
        /// Which foe it is hitting at (the target, or something else it can hit while the target cannot be: a worm's
        /// segments out of the ground while the one it follows is under it), or -1.
        /// </summary>
        public int AttackTarget;
    }

    /// <summary>
    /// What it learns of a weapon by fighting with it: for each distance from what it hits at (in steps of
    /// <see cref="Step"/> pixels), the ticks it spent hitting from there and the damage that landed from there, the old
    /// counting for less as time goes (half in <see cref="HalfLife"/> ticks). From these, the distance it lands the most
    /// from: what it was told (the weapon's reach and its numbers) to begin with, its own findings more and more as they
    /// grow, and a little curiosity for distances it has not tried. So a weapon that is better up close (a spray whose
    /// hits come quicker the nearer it is) draws it in, and one that misses from afar keeps it nearer, whatever mod the
    /// weapon is from. Kept for every weapon it has held, apart.
    /// </summary>
    public sealed class Practice
    {
        public const float Step = 32f;
        public const int Steps = 40;
        public const float HalfLife = 10800f;

        /// <summary>
        /// The distances it keeps apart for its ways of using a weapon, from the target's edge: near (to 160 px), middling
        /// (to 480), far.
        /// </summary>
        public const int Bands = 3;
        public static readonly string[] BandNames = { "near", "mid", "far" };

        public static int BandOf(float fromEdge) => fromEdge <= 160f ? 0 : fromEdge <= 480f ? 1 : 2;

        /// <summary>The band, kept as it was within 24 px of the line it would cross (a spear stands just about 160 px off).</summary>
        public static int BandOf(float fromEdge, int was)
        {
            int now = BandOf(fromEdge);

            if (now == was || was < 0 || was >= Bands)
                return now;

            float line = Math.Max(now, was) == 1 ? 160f : 480f;
            return Math.Abs(now - was) == 1 && Math.Abs(fromEdge - line) < 24f ? was : now;
        }

        private sealed class Record
        {
            public readonly float[] Ticks = new float[Steps];
            public readonly float[] Dealt = new float[Steps];
            public float Smooth = -1f;

            // ticks spent using the weapon each way (Ways) at each distance band (Bands), and what each landed:
            // [way * Bands + band]
            public readonly float[] WayTicks = new float[Ways * Bands];
            public readonly float[] WayDealt = new float[Ways * Bands];
        }

        /// <summary>
        /// The ways it can use a weapon, the same for any weapon: what each is good for is not told, only learned from what
        /// lands. Hold the left click; hold the right; hold the left a while and let go (a charge loosed); the same, longer;
        /// left and right in turn; a right click and then the left aimed at what the right put out (a coin shot, a mine set
        /// off). A weapon from any mod, however it works, is tried each of these ways and used the way that lands the most,
        /// at each distance apart.
        /// </summary>
        public const int Ways = 6;
        public const int WayHold = 0, WayRight = 1, WayCharge = 2, WayAlternate = 3, WaySetUp = 4, WayLongCharge = 5;
        public static readonly string[] WayNames = { "hold", "right click", "charge", "alternate", "set up and shoot", "long charge" };

        /// <summary>The way it is using the weapon now, and the distance band it is using it at: what it lands is put down to them.</summary>
        public int Way, Band;

        /// <summary>
        /// The way and band now as one number (<see cref="Landed(float, float, int)"/>): a shot is marked with the one it was
        /// made under, so what it lands later (a thrown hammer's smash a second on, a mine set off) is put down to the way
        /// that threw it, not to whatever is being done when it lands.
        /// </summary>
        public int Tag => Way * Bands + Band;

        private readonly System.Collections.Generic.Dictionary<int, Record> records = new System.Collections.Generic.Dictionary<int, Record>();
        private Record now;
        private int nowId = int.MinValue;
        private static readonly float Keep = (float)Math.Pow(0.5, 1.0 / HalfLife);

        /// <summary>The weapon in its hands.</summary>
        public void Hold(int id)
        {
            if (id == nowId && now != null)
                return;

            nowId = id;

            if (!records.TryGetValue(id, out now))
            {
                now = new Record();
                records[id] = now;
            }
        }

        private static int StepOf(float distance) => Math.Clamp((int)(distance / Step), 0, Steps - 1);

        /// <summary>A tick spent hitting at something this far off (either button down).</summary>
        public void Attacking(float distance)
        {
            if (now == null)
                return;

            for (int i = 0; i < Steps; i++)
            {
                now.Ticks[i] *= Keep;
                now.Dealt[i] *= Keep;
            }

            now.Ticks[StepOf(distance)] += 1f;

            for (int i = 0; i < Ways * Bands; i++)
            {
                now.WayTicks[i] *= Keep;
                now.WayDealt[i] *= Keep;
            }

            now.WayTicks[Tag] += 1f;
        }

        /// <summary>
        /// The way to use the weapon for the next bout, at the band it is fighting from: each way's damage a tick there,
        /// pulled toward that way's rate at every distance by half <paramref name="prior"/> ticks' worth, which is itself
        /// pulled toward a guess by <paramref name="prior"/> ticks' worth (holding the left click at <paramref name="holdShare"/>
        /// of the rate it lands overall; the right click at <paramref name="altShare"/> of it; the rest at a little less);
        /// and more for the ways it knows little of (<paramref name="explore"/>, more again while practising). Ways that
        /// need a right click are not tried with a weapon that has none.
        /// </summary>
        public int PickWay(bool hasAlt, float altShare, float explore, float holdShare = 1f, float prior = 240f)
        {
            if (now == null)
                return WayHold;

            float ticks = 0f, dealt = 0f;

            for (int i = 0; i < Ways * Bands; i++)
            {
                ticks += now.WayTicks[i];
                dealt += now.WayDealt[i];
            }

            float rate = ticks > 1f && dealt > 0f ? dealt / ticks : 1f;
            prior = Math.Max(10f, prior);
            float best = float.MinValue;
            int pick = WayHold;
            int b = Math.Clamp(Band, 0, Bands - 1);

            for (int i = 0; i < Ways; i++)
            {
                if (!hasAlt && (i == WayRight || i == WayAlternate || i == WaySetUp))
                    continue;

                float share = i == WayHold ? holdShare : i == WayRight ? MathHelper.Clamp(altShare, 0.05f, 1f) : 0.6f;
                float wt = 0f, wd = 0f;

                for (int k = 0; k < Bands; k++)
                {
                    wt += now.WayTicks[i * Bands + k];
                    wd += now.WayDealt[i * Bands + k];
                }

                float overall = (wd + rate * share * prior) / (wt + prior);
                float here = now.WayTicks[i * Bands + b];
                float score = (now.WayDealt[i * Bands + b] + overall * prior * 0.5f) / (here + prior * 0.5f)
                    + (0.25f + explore) * rate / (float)Math.Sqrt(here + 0.5f * (wt - here) + 1f) * 4f;

                if (score > best)
                {
                    best = score;
                    pick = i;
                }
            }

            return pick;
        }

        /// <summary>What it has learned of each way with the weapon it holds (for the /mirror report): its damage a second, and where it lands best.</summary>
        public string WaysReport()
        {
            if (now == null)
                return "";

            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < Ways; i++)
            {
                float wt = 0f, wd = 0f, bestRate = -1f;
                int bestBand = -1;

                for (int k = 0; k < Bands; k++)
                {
                    float t = now.WayTicks[i * Bands + k], d = now.WayDealt[i * Bands + k];
                    wt += t;
                    wd += d;

                    if (t >= 30f && d / t > bestRate)
                    {
                        bestRate = d / t;
                        bestBand = k;
                    }
                }

                if (wt < 1f)
                    continue;

                sb.Append(sb.Length > 0 ? ", " : "").Append(WayNames[i]).Append(' ').Append((wd / wt * 60f).ToString("0")).Append("/s");

                if (bestBand >= 0)
                    sb.Append(" (best ").Append(BandNames[bestBand]).Append(')');
            }

            return sb.ToString();
        }

        /// <summary>The way it lands the most with at each band, as far as it knows (for the /mirror report).</summary>
        public string BestWays()
        {
            if (now == null)
                return "";

            var sb = new System.Text.StringBuilder();

            for (int k = 0; k < Bands; k++)
            {
                int best = -1;
                float bestRate = 0f;

                for (int i = 0; i < Ways; i++)
                {
                    float t = now.WayTicks[i * Bands + k];

                    if (t >= 30f && now.WayDealt[i * Bands + k] / t > bestRate)
                    {
                        bestRate = now.WayDealt[i * Bands + k] / t;
                        best = i;
                    }
                }

                if (best >= 0)
                    sb.Append(sb.Length > 0 ? "; " : "").Append(BandNames[k]).Append(": ").Append(WayNames[best]);
            }

            return sb.ToString();
        }

        // ---- kept between sessions (MimicPlayer saves them with its owner) ----

        /// <summary>The length of a row as round 81 keeps it; the rows before it (<see cref="OldRow"/>) are read too.</summary>
        public const int Row = Steps * 2 + Ways * Bands * 2;

        /// <summary>A row as rounds 80 kept it: one count for each way, at any distance.</summary>
        private const int OldRow = Steps * 2 + Ways * 2;

        /// <summary>Everything learned, a row of numbers for each weapon (by the trainer's or the game's weapon number).</summary>
        public System.Collections.Generic.IEnumerable<(int id, float[] row)> Export()
        {
            foreach (var kv in records)
            {
                Record r = kv.Value;
                var row = new float[Row];
                Array.Copy(r.Ticks, 0, row, 0, Steps);
                Array.Copy(r.Dealt, 0, row, Steps, Steps);
                Array.Copy(r.WayTicks, 0, row, Steps * 2, Ways * Bands);
                Array.Copy(r.WayDealt, 0, row, Steps * 2 + Ways * Bands, Ways * Bands);
                yield return (kv.Key, row);
            }
        }

        /// <summary>Forgets everything (before what was kept for another player is taken up).</summary>
        public void Clear()
        {
            records.Clear();
            now = null;
            nowId = int.MinValue;
        }

        /// <summary>
        /// Takes up a weapon's row. A row kept before round 81 gives its distances only: its ways were counted wrongly then (a
        /// right click alone was never counted), so they are learned again.
        /// </summary>
        public void Import(int id, float[] row)
        {
            if (row == null || row.Length != Row && row.Length != OldRow)
                return;

            var r = new Record();
            Array.Copy(row, 0, r.Ticks, 0, Steps);
            Array.Copy(row, Steps, r.Dealt, 0, Steps);

            if (row.Length == Row)
            {
                Array.Copy(row, Steps * 2, r.WayTicks, 0, Ways * Bands);
                Array.Copy(row, Steps * 2 + Ways * Bands, r.WayDealt, 0, Ways * Bands);
            }

            records[id] = r;

            if (id == nowId)
                now = r;
        }

        /// <summary>Damage it landed on something this far off, put down to the way and band it is using now.</summary>
        public void Landed(float damage, float distance) => Landed(damage, distance, -1);

        /// <summary>
        /// Damage it landed on something this far off, put down to the way and band <paramref name="tag"/> (<see cref="Tag"/>
        /// when the shot that landed it was made), or to the way it is using now for -1.
        /// </summary>
        public void Landed(float damage, float distance, int tag)
        {
            if (now == null || damage <= 0f)
                return;

            now.Dealt[StepOf(distance)] += damage;
            now.WayDealt[tag >= 0 && tag < Ways * Bands ? tag : Tag] += damage;
        }

        /// <summary>
        /// The distance to fight from: <paramref name="told"/> at first, the best it has found as it learns (each step's
        /// damage a tick, pulled toward what being told would give by <paramref name="prior"/> ticks' worth of it, and
        /// a little more for steps it knows little of, by <paramref name="explore"/>); within <paramref name="reach"/>.
        /// </summary>
        public float Prefer(float told, float reach, float prior, float explore)
        {
            if (now == null)
                return told;

            float ticks = 0f, dealt = 0f;

            for (int i = 0; i < Steps; i++)
            {
                ticks += now.Ticks[i];
                dealt += now.Dealt[i];
            }

            if (ticks < 1f || dealt <= 0f)
                return now.Smooth = told;

            // The rate it has been landing at, overall: the yardstick for what being told is worth and for curiosity.
            float rate = dealt / ticks;
            float spread = Math.Max(96f, told * 0.5f);
            int last = Math.Min(Steps - 1, (int)(Math.Max(reach, Step) / Step));
            float best = float.MinValue;
            float at = told;

            for (int i = 0; i <= last; i++)
            {
                float middle = (i + 0.5f) * Step;
                float off = (middle - told) / spread;
                float guess = rate * (float)Math.Exp(-off * off);
                float score = (now.Dealt[i] + guess * prior) / (now.Ticks[i] + prior)
                    + explore * rate / (float)Math.Sqrt(now.Ticks[i] + 1f);

                if (score > best)
                {
                    best = score;
                    at = middle;
                }
            }

            // Eased, so it does not jump from one distance to another between two ticks.
            now.Smooth = now.Smooth < 0f ? at : now.Smooth + (at - now.Smooth) * 0.02f;
            return now.Smooth;
        }
    }

    /// <summary>
    /// The numbers it fights by, each kept between two bounds (<see cref="Ranges"/>) as a share from 0 to 1 so the
    /// trainer can change them all alike. <see cref="Trained"/> is what the trainer settled on.
    /// </summary>
    public sealed class MimicGenes
    {
        public static readonly (string Name, float Low, float High)[] Ranges =
        {
            ("Horizon", 14f, 40f),        // ticks it looks ahead when it plans
            ("RiskTau", 6f, 40f),         // how fast what is further ahead counts for less
            ("Margin", 0f, 24f),          // pixels of room it keeps from a threat
            ("MarginGrow", 0f, 1.2f),     // and more for each tick ahead
            ("RiskWeight", 0.5f, 8f),     // danger against distance
            ("GoalWeight", 0.05f, 1.5f),  // distance from where it wants to be, a tile at a time
            ("VertWeight", 0f, 1.5f),     // height off it, against distance across
            ("Hysteresis", 0f, 0.6f),     // how much better another plan must be
            ("Replan", 1f, 8f),           // ticks a plan is held at the least
            ("RangedDist", 6f, 28f),      // tiles from a target with a gun or a spell
            ("MeleeClose", 0.3f, 1f),     // share of a blade's reach it stands at
            ("HoverAbove", -4f, 14f),     // tiles above a target, when it can fly
            ("SideSafety", 0f, 3f),       // how much a busy side counts against it
            ("FlankBias", -1f, 1f),       // against standing on its owner's side
            ("Lead", 0f, 1.5f),           // how far ahead of a moving target it aims
            ("HealAt", 0.15f, 0.7f),      // share of its life it drinks at, whatever is wasted
            ("ParryRate", 0f, 0.6f),      // share of its chances to parry it takes
            ("ParryLook", 2f, 14f),       // ticks past the wind-up it looks for one
            ("AltRate", 0f, 0.5f),        // right clicks a second, at most
            ("DashCost", 0f, 20f),        // what a dash costs it, so it dashes only when it must
            ("Leash", 15f, 60f),          // tiles from its owner, at the most
            ("IdleSpread", 2f, 10f),      // tiles behind its owner when there is nothing to fight
            ("JumpCost", 0f, 3f),         // against hopping for nothing
            ("BossBias", 0f, 40f),        // tiles nearer a boss counts as
            ("Targeted", 0f, 8f),         // tiles further off a target that is after it
            ("Tank", 0f, 0.9f),           // share nearer a boss than its owner it stands, to draw the boss off them
            ("LineMargin", 0f, 24f),      // pixels more of room it keeps from a beam
            ("Apart", 0f, 12f),           // tiles it keeps from its owner while fighting, so what is aimed at one misses the other
            ("Decoy", 0f, 30f),           // tiles from its owner it leads a boss that is after it
            ("ManaResume", 0.1f, 1f),     // share of its mana back before it uses a mana weapon again, once it ran dry
            ("RestStill", 0f, 4f),        // what moving costs it while it rests for mana
            ("LearnPrior", 30f, 1500f),   // ticks of hitting it takes before what it learns of a weapon outweighs what it was told
            ("Explore", 0f, 1.5f),        // how much it tries distances it knows little of
            ("ChaserBias", 0f, 60f),      // tiles nearer a small foe come after it or its owner counts as
            ("ChaseTurn", 0f, 0.15f),     // radians a tick it reckons a body that is after it turns toward it
            ("HomeTrust", 0f, 2f),        // how much of a shot's seen turning it reckons with as homing on it
            ("Lane", 0f, 1.5f),           // how much it keeps out of the line from a body after it (where a charge comes)
            ("Commit", 0f, 1f),           // with a sword or spear, how much of the danger of touching its own target it accepts
            ("CloseRisk", 0.15f, 1.5f),   // with a weapon that must touch and something to touch: its danger counts so many times over (round 81)
            ("CloseGoal", 0.5f, 5f),      // and its distance from where it wants to be so many times over
            ("HoldPrior", 0.6f, 2.5f),    // before it has learned a weapon, holding the left click reckoned at so much of what it lands overall
            ("WayPrior", 60f, 900f),      // ticks of a way of using a weapon before what it learns of it outweighs that reckoning
        };

        public const int Count = 42;

        /// <summary>Each number as a share of its range.</summary>
        public readonly float[] Share = new float[Count];

        public MimicGenes(float[] shares)
        {
            // Any not given (numbers trained before a gene was added) start at the first guess's.
            for (int i = 0; i < Count; i++)
                Share[i] = Math.Clamp(i < shares.Length ? shares[i] : Untrained[i], 0f, 1f);
        }

        private float Get(int i) => Ranges[i].Low + (Ranges[i].High - Ranges[i].Low) * Share[i];

        public float Horizon => Get(0);
        public float RiskTau => Get(1);
        public float Margin => Get(2);
        public float MarginGrow => Get(3);
        public float RiskWeight => Get(4);
        public float GoalWeight => Get(5);
        public float VertWeight => Get(6);
        public float Hysteresis => Get(7);
        public float Replan => Get(8);
        public float RangedDist => Get(9);
        public float MeleeClose => Get(10);
        public float HoverAbove => Get(11);
        public float SideSafety => Get(12);
        public float FlankBias => Get(13);
        public float Lead => Get(14);
        public float HealAt => Get(15);
        public float ParryRate => Get(16);
        public float ParryLook => Get(17);
        public float AltRate => Get(18);
        public float DashCost => Get(19);
        public float Leash => Get(20);
        public float IdleSpread => Get(21);
        public float JumpCost => Get(22);
        public float BossBias => Get(23);
        public float Targeted => Get(24);
        public float Tank => Get(25);
        public float LineMargin => Get(26);
        public float Apart => Get(27);
        public float Decoy => Get(28);
        public float ManaResume => Get(29);
        public float RestStill => Get(30);
        public float LearnPrior => Get(31);
        public float Explore => Get(32);
        public float ChaserBias => Get(33);
        public float ChaseTurn => Get(34);
        public float HomeTrust => Get(35);
        public float Lane => Get(36);
        public float Commit => Get(37);
        public float CloseRisk => Get(38);
        public float CloseGoal => Get(39);
        public float HoldPrior => Get(40);
        public float WayPrior => Get(41);

        /// <summary>A guess at good numbers, before any training: where the trainer started.</summary>
        public static readonly float[] Untrained =
        {
            0.54f, 0.35f, 0.33f, 0.29f, 0.33f, 0.21f, 0.27f, 0.33f, 0.29f, 0.45f, 0.5f, 0.5f,
            0.33f, 0.65f, 0.6f, 0.45f, 0.42f, 0.5f, 0.3f, 0.2f, 0.44f, 0.25f, 0.1f, 0.5f,
            0.25f, 0.3f, 0.4f, 0.25f, 0f, 0.6f, 0.3f, 0.2f, 0.2f, 0.2f, 0.3f, 0.5f,
            0.3f, 0.7f,
            // round 81's: each as it was before it was a number (danger and distance once over, holding the left click at the
            // rate it lands, 240 ticks)
            0.63f, 0.111f, 0.2105f, 0.2143f,
        };

        /// <summary>
        /// What the trainer settled on for any weapon (round 80 A: an evolution strategy against the mod's Legends Empress,
        /// Queen Slime and Amalgaermis and the game's Moon Lord and Plantera, every kind of weapon at once): for an item of
        /// no kind (none held) and anything sorted before the kinds' own numbers (<see cref="TrainedByKind"/>) are read.
        /// </summary>
        public static readonly float[] TrainedShares =
        {
            0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
            0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
            0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
        };

        public static MimicGenes Trained => new MimicGenes(TrainedShares);

        /// <summary>
        /// What the trainer settled on for each kind of weapon (MimicWeapons.ClusterOf: round 81's kinds, in the four classes),
        /// in kind order (Claude outputs\MimicTrainer_r81: each kind trained by CMA-ES against all sixteen bosses, from the numbers
        /// of the round-80 kind most of its weapons were in):
        ///   0 Melee / Swing: reach ~56, use 19t
        ///   1 Melee / HeldBlade: reach ~64, use 20t
        ///   2 Melee / HeldBlade (+Thrown, Swing): reach ~85, use 22t, 26 with right 
        ///   3 Melee / Spear: reach ~188, use 24t
        ///   4 Melee / Yoyo: reach ~296, use 25t
        ///   5 Melee / Thrown: reach ~960, use 20t, shot 16px/t, 1 with right click
        ///   6 Melee / SwingShot: reach ~640, use 24t, shot 17px/t, 1 with right clic
        ///   7 Melee / SwingShot: reach ~640, use 24t, shot 12px/t, 3 with right clic
        ///   8 Ranged / Thrown: reach ~960, use 20t, shot 16px/t
        ///   9 Ranged / Thrown (+Shot, HeldShot): reach ~960, use 25t, shot 28px/t
        ///   10 Ranged / Thrown: reach ~960, use 20t, shot 8px/t
        ///   11 Ranged / Shot: reach ~960, use 27t, shot 8px/t
        ///   12 Ranged / Shot (+Thrown, HeldShot): reach ~960, use 19t, shot 25px/t, 2
        ///   13 Ranged / Shot: reach ~960, use 18t, shot 16px/t
        ///   14 Ranged / HeldShot (+Shot, Thrown): reach ~360, use 22t, shot 3px/t
        ///   15 Ranged / HeldShot: reach ~960, use 30t, shot 15px/t, 9 with right clic
        ///   16 Magic / Shot (+Thrown, Spray): reach ~960, use 22t, shot 18px/t, 10 wi
        ///   17 Magic / HeldShot (+Shot, Spray): reach ~960, use 20t, shot 12px/t, 7 w
        ///   18 Summon / Whip (+HeldShot): reach ~160, use 30t
        ///   19 Summon / Minion: reach ~800, use 36t, 2 with right click
        ///   20 Summon / Sentry (+Shot, Minion): reach ~800, use 30t
        /// </summary>
        public static readonly float[][] TrainedByKind =
        {
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.970f, 0.674f, 0.593f, 0.310f, 0.550f, 0.094f, 0.646f, 0.219f, 0.465f, 0.675f, 0.798f, 0.832f,
                0.663f, 0.897f, 0.690f, 0.745f, 0.426f, 0.189f, 0.721f, 0.871f, 0.933f, 0.268f, 0.174f, 0.985f,
                0.950f, 0.206f, 0.361f, 0.289f, 0.321f, 0.590f, 0.245f, 0.321f, 0.360f, 0.758f, 0.542f, 0.354f,
                0.205f, 0.626f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.940f, 0.681f, 0.380f, 0.418f, 0.577f, 0.095f, 0.597f, 0.346f, 0.401f, 0.754f, 0.900f, 0.901f,
                0.790f, 0.912f, 0.696f, 0.766f, 0.488f, 0.259f, 0.748f, 0.852f, 0.817f, 0.197f, 0.082f, 0.819f,
                0.918f, 0.295f, 0.413f, 0.162f, 0.359f, 0.527f, 0.294f, 0.246f, 0.183f, 0.737f, 0.573f, 0.322f,
                0.259f, 0.696f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.967f, 0.593f, 0.271f, 0.637f, 0.331f, 0.084f, 0.676f, 0.098f, 0.423f, 0.814f, 0.858f, 0.979f,
                0.510f, 0.811f, 0.727f, 0.767f, 0.444f, 0.104f, 0.767f, 0.942f, 0.837f, 0.254f, 0.170f, 0.929f,
                0.879f, 0.135f, 0.203f, 0.365f, 0.028f, 0.652f, 0.430f, 0.173f, 0.513f, 0.597f, 0.265f, 0.258f,
                0.200f, 0.442f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.953f, 0.592f, 0.273f, 0.419f, 0.536f, 0.133f, 0.648f, 0.264f, 0.065f, 0.468f, 0.829f, 0.902f,
                0.752f, 0.893f, 0.594f, 0.850f, 0.325f, 0.351f, 0.821f, 0.834f, 0.721f, 0.378f, 0.195f, 0.683f,
                0.900f, 0.215f, 0.267f, 0.299f, 0.364f, 0.709f, 0.166f, 0.130f, 0.300f, 0.676f, 0.352f, 0.070f,
                0.339f, 0.654f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.887f, 0.625f, 0.314f, 0.369f, 0.529f, 0.087f, 0.590f, 0.249f, 0.241f, 0.595f, 0.859f, 0.866f,
                0.611f, 0.926f, 0.664f, 0.839f, 0.414f, 0.085f, 0.911f, 0.959f, 0.772f, 0.302f, 0.097f, 0.836f,
                0.856f, 0.266f, 0.291f, 0.238f, 0.253f, 0.580f, 0.239f, 0.199f, 0.319f, 0.735f, 0.478f, 0.206f,
                0.300f, 0.700f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.926f, 0.619f, 0.353f, 0.454f, 0.303f, 0.038f, 0.644f, 0.245f, 0.122f, 0.582f, 0.718f, 0.950f,
                0.568f, 0.874f, 0.772f, 0.932f, 0.240f, 0.041f, 0.935f, 0.794f, 0.675f, 0.211f, 0.047f, 0.698f,
                0.747f, 0.258f, 0.229f, 0.092f, 0.462f, 0.532f, 0.102f, 0.108f, 0.277f, 0.833f, 0.456f, 0.237f,
                0.340f, 0.577f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
            new[]
            {
                0.926f, 0.619f, 0.353f, 0.454f, 0.303f, 0.038f, 0.644f, 0.245f, 0.122f, 0.582f, 0.718f, 0.950f,
                0.568f, 0.874f, 0.772f, 0.932f, 0.240f, 0.041f, 0.935f, 0.794f, 0.675f, 0.211f, 0.047f, 0.698f,
                0.747f, 0.258f, 0.229f, 0.092f, 0.462f, 0.532f, 0.102f, 0.108f, 0.277f, 0.833f, 0.456f, 0.237f,
                0.340f, 0.577f, 0.630f, 0.111f, 0.210f, 0.214f,
            },
        };

        private static MimicGenes[] byKind;
        private static MimicGenes trained;

        /// <summary>The numbers for a kind of weapon (made once and kept).</summary>
        public static MimicGenes For(int kind)
        {
            if (byKind == null)
            {
                trained = Trained;
                byKind = new MimicGenes[TrainedByKind.Length];

                for (int i = 0; i < byKind.Length; i++)
                    byKind[i] = new MimicGenes(TrainedByKind[i]);
            }

            return kind >= 0 && kind < byKind.Length ? byKind[kind] : trained;
        }
    }
}
