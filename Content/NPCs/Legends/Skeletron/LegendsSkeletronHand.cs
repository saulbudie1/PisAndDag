using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// Skeletron's hands. Vanilla runs them, except while their head asks something of them (LegendsSkeletron.Aim: his
    /// coming, and his moves in Legends), when they go where they are asked, as fast as they are asked, and hurt as they
    /// are asked. Given back to vanilla after, idle, their swing counted from nothing.
    ///
    /// They are drawn by their head (LegendsSkeletron.DrawBody), not here: what this keeps is how each looks on this
    /// machine (its angle and pose, eased toward what is asked, so a hand turns and closes rather than jumping), and for a
    /// hand vanilla runs, the look of what vanilla has it doing (hanging, winding up, swung).
    ///
    /// A hand parried in a blow of a move (SkeletronHandParry, which asks <see cref="InMove"/> and <see cref="Striking"/>)
    /// is out of the move: it is thrown back, and is not moved here until the move is over. A blow of both hands
    /// (<see cref="Joint"/>: the clap, the fists together, the grab from both sides) is broken for both; and what it would
    /// have shown as it landed is left out (<see cref="Broken"/>).
    /// </summary>
    public class LegendsSkeletronHand : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.SkeletronHand;

        // ---- how it looks on this machine ----------------------------------------------------------------------------

        internal float angle;
        internal SkeletronRig.Pose pose = SkeletronRig.Relaxed;
        internal float scale = 1f;
        internal bool shown = true;
        internal Color rim;
        internal float rimReach;
        internal bool bypass;
        internal bool hasRoot;
        internal Vector2 root;
        internal LegendsSkeletron.ArmStyle arm;
        internal Vector2 bend;
        private bool looked;

        /// <summary>
        /// Where its arm's top is: not pinned to his shoulder but loose, drifting after it on a spring, never further off
        /// than <see cref="ArmSlack"/>, and pulled along by the hand when the hand goes further than the arm reaches (by
        /// request: "it should fly freely but have a constraint to it... arm stays in place while head bounce").
        /// </summary>
        internal Vector2 armTop;
        internal bool armKnown;
        private Vector2 armDrift;

        /// <summary>How far an arm's top may drift from his shoulder, at scale 1.</summary>
        private const float ArmSlack = 110f;

        /// <summary>Its head had it last tick; it made it untouchable.</summary>
        private bool held;
        private bool madeUntouchable;

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LegendsSkeletron.LifeScale * HandLifeScale);
            entity.damage = (int)(entity.damage * LegendsSkeletron.DamageScale * HandDamageScale);
        }

        /// <summary>A hand's life, a fifth again over his own Legends half again (by request: "for legends buff hands health by 20%").</summary>
        public const float HandLifeScale = 1.2f;

        /// <summary>A hand's blows, at three fifths of what they were (by request: "hands only deal 3/5 damage it do now").</summary>
        public const float HandDamageScale = 0.6f;

        // ---- asked of, by its head ------------------------------------------------------------------------------------

        /// <summary>The head a hand belongs to (vanilla keeps its slot in ai[1]), or null.</summary>
        internal static NPC HeadOf(NPC hand)
        {
            int index = (int)hand.ai[1];

            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC head = Main.npc[index];

            return head.active && head.type == NPCID.SkeletronHead ? head : null;
        }

        /// <summary>Its head's state and what it asks of this hand this tick, if it has a head.</summary>
        private static bool Asked(NPC hand, out LegendsSkeletron state, out LegendsSkeletron.Aim aim)
        {
            state = null;
            aim = default;

            NPC head = HeadOf(hand);

            if (head == null || !head.TryGetGlobalNPC(out state))
                return false;

            aim = state.aims[LegendsSkeletron.IndexOf(hand)];
            return true;
        }

        /// <summary>
        /// Whether a move of Legends has this hand, and which (the head's count of its moves). Read off the head, as every
        /// machine has it.
        /// </summary>
        internal static bool InMove(NPC hand, out int serial)
        {
            serial = -1;

            if (!PADWorld.Legends || !Asked(hand, out LegendsSkeletron state, out LegendsSkeletron.Aim aim))
                return false;

            if (state.scene != LegendsSkeletron.Scene.Fight || state.move == LegendsSkeletron.Move.None || !aim.Held)
                return false;

            serial = state.serial;
            return true;
        }

        /// <summary>Whether that move has this hand in its blow (a parry can meet it): not on its way, not after, not through crowd control.</summary>
        internal static bool Striking(NPC hand) =>
            PADWorld.Legends && Asked(hand, out _, out LegendsSkeletron.Aim aim) && aim.Held && aim.Blow && !aim.Bypass && aim.Hurt > 0f;

        /// <summary>Whether that blow is both hands' (a parry of one breaks it for both).</summary>
        internal static bool Joint(NPC hand) => Asked(hand, out _, out LegendsSkeletron.Aim aim) && aim.Held && aim.Joint;

        /// <summary>A hand of his has been parried in a joint blow, where the world is decided: its head keeps that too (the hand may be broken by it).</summary>
        internal static void Break(NPC hand)
        {
            NPC head = HeadOf(hand);

            if (head != null && head.TryGetGlobalNPC(out LegendsSkeletron state))
                state.brokenIn = state.serial;
        }

        /// <summary>
        /// Whether a parry has broken the joint blow of the move that has this head's hands, as this machine knows it: its
        /// head was told so (<see cref="Break"/>), a hand of his is out of the move for it, or one was turned aside by this
        /// machine's own player a moment ago. What the blow would have shown and sounded is left out. Nothing on a server.
        /// </summary>
        internal static bool Broken(NPC head)
        {
            if (Main.dedServ || head == null)
                return false;

            if (head.TryGetGlobalNPC(out LegendsSkeletron state) && state.brokenIn == state.serial)
                return true;

            ParryPlayer parry = Main.LocalPlayer.GetModPlayer<ParryPlayer>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC hand = Main.npc[i];

                if (!hand.active || hand.type != NPCID.SkeletronHand || (int)hand.ai[1] != head.whoAmI)
                    continue;

                if (SkeletronHandParry.Spent(hand) || parry.Spares(hand))
                    return true;
            }

            return false;
        }

        /// <summary>Whether this one hand's own blow was turned aside, as this machine knows it.</summary>
        internal static bool Turned(NPC hand)
        {
            if (hand == null || !hand.active)
                return true;

            if (SkeletronHandParry.Spent(hand))
                return true;

            return !Main.dedServ && Main.LocalPlayer.GetModPlayer<ParryPlayer>().Spares(hand);
        }

        /// <summary>The other hand on the same head, or null.</summary>
        internal static NPC Sibling(NPC hand)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (i != hand.whoAmI && other.active && other.type == NPCID.SkeletronHand && (int)other.ai[1] == (int)hand.ai[1])
                    return other;
            }

            return null;
        }

        // ---- moved ---------------------------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (!Asked(npc, out LegendsSkeletron state, out LegendsSkeletron.Aim aim))
                return true;

            if (!aim.Held)
            {
                if (held)
                {
                    // Idle, the swing counted from nothing (vanilla's stagger kept: his left hand half a swing on), straight back
                    // to hanging off him.
                    held = false;
                    npc.ai[2] = 0f;
                    npc.ai[3] = npc.ai[0] > 0f ? 150f : 0f;
                    npc.damage = npc.defDamage;
                    npc.reflectsProjectiles = false;
                    npc.netUpdate = true;
                }

                if (madeUntouchable)
                {
                    madeUntouchable = false;
                    BossArrivalWait.SetUntouchable(npc, false);
                }

                return true;
            }

            held = true;

            if (aim.Untouchable != madeUntouchable)
            {
                madeUntouchable = aim.Untouchable;
                BossArrivalWait.SetUntouchable(npc, aim.Untouchable);
            }

            npc.reflectsProjectiles = aim.Reflect;
            npc.damage = aim.Hurt > 0f ? Math.Max(1, (int)(npc.defDamage * aim.Hurt)) : 0;
            npc.spriteDirection = -(int)npc.ai[0];

            // Parried out of this move: thrown back, and no further part in it (SkeletronHandParry moves it).
            if (SkeletronHandParry.Spent(npc))
                return false;

            Vector2 goal = aim.Goal;

            if (aim.Shake > 0f)
            {
                // The same tremble on every machine: from the clock, not the dice.
                float t = Main.GameUpdateCount * 0.9f + npc.whoAmI * 1.7f;
                goal += new Vector2((float)Math.Sin(t * 1.9f), (float)Math.Cos(t * 2.3f)) * aim.Shake;
            }

            if (aim.Gain >= 1f)
            {
                LegendsSkeletron.Place(npc, goal);
            }
            else
            {
                Vector2 want = (goal - npc.Center) * aim.Gain;

                if (want.Length() > aim.Most)
                    want = want.SafeNormalize(Vector2.Zero) * aim.Most;

                npc.velocity = want;
            }

            return false;
        }

        public override void PostAI(NPC npc)
        {
            if (!Asked(npc, out LegendsSkeletron state, out LegendsSkeletron.Aim aim))
                return;

            // Through crowd control: the flag the parry and the tell read, set after every hook that clears it.
            if (aim.Held && aim.Bypass)
                npc.PAD().CCBypass = true;

            if (Main.dedServ)
                return;

            NPC head = Main.npc[(int)npc.ai[1]];

            if (aim.Held)
            {
                if (!looked)
                    scale = aim.Scale;

                Look(aim.Angle, aim.Pose, aim.Turn, aim.Shape);
                scale = MathHelper.Lerp(scale, aim.Scale, 0.35f);
                shown = !aim.Hidden;
                rim = aim.Glow;
                rimReach = aim.GlowReach;
                bypass = aim.Bypass;
                hasRoot = aim.HasRoot;
                root = aim.Root;
                arm = aim.Arm;
                bend = aim.Bend;
                FloatArm(npc, head);
                return;
            }

            // Vanilla has it: it hangs on along its forearm (and when swung, looks the way it flies), open as it winds up,
            // a fist as it is swung, loose and slowly flexing as it hangs.
            float side = LegendsSkeletron.SideOf(npc);
            Vector2 shoulder = SkeletronRig.Shoulder(head.Center, side, head.scale);
            float want = LegendsSkeletron.HangAngle(shoulder, npc.Center, side, head.scale);
            float speed = npc.velocity.Length();

            if (speed > 8f)
                want = want.AngleLerp(SkeletronRig.AngleFor(npc.velocity), MathHelper.Clamp((speed - 8f) / 10f, 0f, 0.8f));

            SkeletronRig.Pose wantPose = npc.ai[2] switch
            {
                1f or 4f => SkeletronRig.Spread,
                2f or 5f => SkeletronRig.Fist,
                _ => LegendsSkeletron.Hanging(side),
            };

            Look(want, wantPose, 0.18f, 0.2f);
            scale = MathHelper.Lerp(scale, 1f, 0.35f);
            shown = true;
            rim = default;
            rimReach = 0f;
            bypass = false;
            hasRoot = false;
            arm = LegendsSkeletron.ArmStyle.Bent;
            FloatArm(npc, head);
        }

        /// <summary>Its arm's loose top, a tick on: after his shoulder on a spring, held to the slack and to the arm's reach.</summary>
        private void FloatArm(NPC npc, NPC head)
        {
            float side = LegendsSkeletron.SideOf(npc);
            Vector2 shoulder = hasRoot ? root : SkeletronRig.Shoulder(head.Center, side, head.scale);

            if (!armKnown || hasRoot)
            {
                // Held by something (a move's root: the tear it comes out of, the hand holding it as a whip): there.
                armTop = shoulder;
                armDrift = Vector2.Zero;
                armKnown = true;
                return;
            }

            armDrift = armDrift * 0.82f + (shoulder - armTop) * 0.08f;
            armTop += armDrift;

            Vector2 off = armTop - shoulder;
            float slack = ArmSlack * head.scale;

            if (off.Length() > slack)
                armTop = shoulder + off * (slack / off.Length());

            Vector2 wrist = SkeletronRig.WristFromMiddle(npc.Center, angle, side > 0f, npc.scale * scale);
            Vector2 toTop = armTop - wrist;
            float reach = SkeletronRig.Reach * head.scale;

            if (toTop.Length() > reach)
                armTop = wrist + toTop * (reach / toTop.Length());
        }

        /// <summary>Turned and posed toward what is wanted, this much of the way a tick (straight there the first time).</summary>
        private void Look(float wantAngle, SkeletronRig.Pose wantPose, float turn, float shape)
        {
            if (!looked)
            {
                looked = true;
                angle = wantAngle;
                pose = wantPose;
                return;
            }

            angle = angle.AngleLerp(wantAngle, MathHelper.Clamp(turn, 0f, 1f));
            pose = SkeletronRig.Pose.Lerp(pose, wantPose, shape);
        }

        // ---- hurting -------------------------------------------------------------------------------------------------

        /// <summary>
        /// As its head asks: not at all, or as hard as asked. A blow of both hands strikes as one: with one of them turned
        /// aside by this machine's player's parry, the other cannot touch that player either for as long as the first
        /// cannot (and on the tick of the parry itself, the one the parry does not face cannot, if the other is about to
        /// be turned aside).
        /// </summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (!Asked(npc, out LegendsSkeletron state, out LegendsSkeletron.Aim aim))
                return true;

            if (state.scene != LegendsSkeletron.Scene.Fight)
                return false;

            if (!aim.Held)
                return true;

            if (aim.Hurt <= 0f)
                return false;

            if (aim.Joint)
            {
                NPC other = Sibling(npc);

                if (other != null)
                {
                    ParryPlayer parry = target.GetModPlayer<ParryPlayer>();

                    if (parry.Spares(other) || (parry.AboutToTurn(other) && !parry.AboutToTurn(npc)))
                        return false;
                }
            }

            return true;
        }

        public override bool CanHitNPC(NPC npc, NPC target) =>
            !Asked(npc, out LegendsSkeletron state, out LegendsSkeletron.Aim aim) || state.scene == LegendsSkeletron.Scene.Fight && (!aim.Held || aim.Hurt > 0f);

        /// <summary>A blow that throws whoever it hits (asked: Aim.Push), on their own machine; and what the move makes of the hit.</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (!Asked(npc, out LegendsSkeletron state, out LegendsSkeletron.Aim aim) || !aim.Held)
                return;

            if (aim.Push != Vector2.Zero && target.whoAmI == Main.myPlayer)
            {
                target.velocity = aim.Push;
                target.fallStart = (int)(target.position.Y / 16f);
            }

            state.HandHit(Main.npc[(int)npc.ai[1]], npc, target);
        }
    }
}
