using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Melee.Fists
{
    /// <summary>
    /// A player's fists (FistWeapon): where they are in a combo, the guard a blow gives against being touched by what it
    /// is thrown at, the arms' poses while a blow is out (FistArmLayers), and the right-click. The combo, the guard and the
    /// right-click are kept on the player's own machine, where blows are chosen and touches are decided; the poses on
    /// every machine, from the blow (FistPunch runs on all of them).
    /// </summary>
    public class FistPlayer : ModPlayer
    {
        /// <summary>The weapon the combo is of, and the step its next blow is.</summary>
        private int comboOf;
        private int step;

        /// <summary>Ticks since the last blow was thrown, and how long that one was.</summary>
        private int since = 100000;
        private int last;

        /// <summary>While a blow is out: which way, and how far. Set by the blow each tick.</summary>
        private int guardTicks;
        private int guardFacing;
        private float guardReach;

        /// <summary>For each NPC a fist has hit, the tick until which it cannot touch this player.</summary>
        private readonly uint[] struck = new uint[Main.maxNPCs];

        /// <summary>How long after a blow lands its target is held off, in ticks. Mine.</summary>
        public const int HeldOff = 30;

        /// <summary>What a heavy thing's touch does through the guard instead of nothing. Mine.</summary>
        public const float HeavyThrough = 0.5f;

        // ---- the arms, posed (FistArmLayers) --------------------------------------------------------------------

        /// <summary>
        /// The arms' poses this tick and last tick, and whether each smears from the one to the other (FistArmLayers lays
        /// faint copies of it part way between, in <see cref="Smear"/>'s colour). An arm a blow leaves alone (the back arm,
        /// but for the blow it throws) is the game's own.
        /// </summary>
        public ArmPose Front, Back, FrontLast, BackLast;
        public bool FrontSmear, BackSmear;
        public Color Smear = Color.White;

        /// <summary>Ticks left of each arm's pose: 2 when the blow set it this tick, gone the tick after the blow stops.</summary>
        private int frontTicks, backTicks;

        /// <summary>Where each arm's pieces start in the draw list, while it is being drawn.</summary>
        internal int FrontFrom = -1, BackFrom = -1;

        public bool FrontPosed => frontTicks > 1;
        public bool BackPosed => backTicks > 1;

        /// <summary>From the blow, each tick: the front arm's pose, and whether it smears from last tick's.</summary>
        public void PoseFront(ArmPose pose, bool smear)
        {
            bool had = frontTicks > 0;
            FrontLast = Front;
            Front = pose;
            FrontSmear = smear && had;
            frontTicks = 2;
        }

        /// <summary>From the blow, each tick: the back arm's pose, and whether it smears from last tick's.</summary>
        public void PoseBack(ArmPose pose, bool smear)
        {
            bool had = backTicks > 0;
            BackLast = Back;
            Back = pose;
            BackSmear = smear && had;
            backTicks = 2;
        }

        // ---- the combo ------------------------------------------------------------------------------------------

        /// <summary>The step the next blow of <paramref name="weapon"/> will be: the next in the combo, or the first if the last was too long ago or of another weapon.</summary>
        public int NextStep(FistWeapon weapon)
        {
            if (comboOf != weapon.Type || since > last + weapon.ComboWindow)
                return 0;

            return step;
        }

        /// <summary>A blow is thrown: the combo moves on.</summary>
        public void Threw(FistWeapon weapon, int thrown, int ticks)
        {
            comboOf = weapon.Type;
            step = (thrown + 1) % Math.Max(1, weapon.Combo.Length);
            since = 0;
            last = ticks;
        }

        // ---- the guard ------------------------------------------------------------------------------------------

        /// <summary>From the blow, each tick it is out: hold off what is ahead, to <paramref name="reach"/>.</summary>
        public void Guard(int facing, float reach)
        {
            guardTicks = 2;
            guardFacing = facing;
            guardReach = reach;
        }

        /// <summary>From the blow, when it lands on <paramref name="npc"/>.</summary>
        public void Struck(NPC npc) => struck[npc.whoAmI] = Main.GameUpdateCount + HeldOff;

        public override void PostUpdate()
        {
            if (since < 100000)
                since++;

            if (guardTicks > 0)
                guardTicks--;

            if (frontTicks > 0)
                frontTicks--;

            if (backTicks > 0)
                backTicks--;

            if (Player.whoAmI == Main.myPlayer)
                RightClick();
        }

        /// <summary>
        /// The weapon's right-click, taken straight from the mouse so that it cuts into a blow (the item is busy while one
        /// is out): pressed this tick, not on the inventory or anything else the mouse is over, not opening a door or a
        /// chest, the weapon in hand.
        /// </summary>
        private void RightClick()
        {
            if (!PlayerInput.Triggers.JustPressed.MouseRight || Player.mouseInterface || Player.tileInteractionHappened || Player.dead
                || Player.CCed || Main.gameMenu || Main.mapFullscreen || Main.drawingPlayerChat || Main.editSign || Main.editChest)
                return;

            if (Player.HeldItem?.ModItem is not FistWeapon weapon || !weapon.HasAltUse || !weapon.CanAltUse(Player))
                return;

            weapon.AltUse(Player, Player.GetSource_ItemUse(Player.HeldItem));
        }

        /// <summary>
        /// Whether a touch from <paramref name="npc"/> goes through the guard (halved) rather than being held off: a boss, a
        /// part of one (the game counts the Eater's segments and Skeletron's hands apart from their bosses), or anything the
        /// game will not knock back, as such things push through a guard. Mine.
        /// </summary>
        public static bool Heavy(NPC npc) =>
            npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.knockBackResist <= 0f
            || (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active && Main.npc[npc.realLife].boss);

        /// <summary>Whether <paramref name="npc"/> is one the fists are holding off this tick.</summary>
        public bool HoldsOff(NPC npc)
        {
            if (struck[npc.whoAmI] > Main.GameUpdateCount)
                return true;

            if (guardTicks <= 0)
                return false;

            // Ahead, within the reach: the player's own box, stretched forward by it.
            Rectangle zone = Player.Hitbox;
            zone.Inflate(6, 10);

            if (guardFacing > 0)
                zone.Width += (int)guardReach;
            else
            {
                zone.X -= (int)guardReach;
                zone.Width += (int)guardReach;
            }

            return zone.Intersects(npc.Hitbox);
        }

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
        {
            if (Player.whoAmI != Main.myPlayer || Heavy(npc))
                return true;

            return !HoldsOff(npc);
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers)
        {
            if (Heavy(npc) && HoldsOff(npc))
                modifiers.FinalDamage *= HeavyThrough;
        }
    }
}
