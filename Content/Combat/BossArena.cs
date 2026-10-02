using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// A boss's box: two walls nobody can see, one either side of a fight, from the sky to the
    /// bottom of the world. Inside it a player cannot walk out past a wall, their camera will
    /// not show anything past one (so a wall, reached, is the edge of their screen), nothing
    /// in it can be blown up, and they are under Boss Active (<see cref="BossActive"/>):
    /// nothing built, nothing broken.
    ///
    /// The shape of it is the one Calamity's Supreme Calamitas fight uses, read for how it
    /// works and written again here: a box the boss keeps up, players put back inside it
    /// every tick. Theirs is drawn and has four sides; this one is not drawn and has two, and
    /// it holds the camera as well, which theirs does not.
    ///
    /// Whoever wants one asks for it every tick with <see cref="Keep"/>, on every machine (a
    /// boss's AI runs on all of them), and it is gone a tick after they stop asking: a boss
    /// that dies, leaves or is unloaded cannot leave anyone walled in. One at a time: the last
    /// to ask has it.
    ///
    /// A player is in it from the tick their middle is between the walls while it stands
    /// (<see cref="BossArenaPlayer.Held"/>): those inside when it comes up, and anyone who
    /// walks in after. Somebody outside it is left alone. The boss itself is not held: where
    /// it goes is its own AI's business.
    /// </summary>
    public static class BossArena
    {
        /// <summary>Ticks the box outlives the last tick it was asked for: the one between the asker's update and the players' next.</summary>
        private const int Slack = 2;

        /// <summary>Ticks the camera takes to settle against the walls, and to come away when they go.</summary>
        private const int EaseTicks = 40;

        private static int asked;
        private static float left;
        private static float right;

        /// <summary>The box is standing.</summary>
        public static bool Up => asked > 0;

        /// <summary>The walls, x in the world, in pixels. Only meaningful while it is <see cref="Up"/>.</summary>
        public static float Left => left;
        public static float Right => right;

        /// <summary>
        /// The box, this tick: its two walls, x in the world, in pixels. Asked every tick for as
        /// long as it should stand, by the same asker on every machine.
        /// </summary>
        public static void Keep(float leftWall, float rightWall)
        {
            if (!(rightWall > leftWall))
                return;

            left = leftWall;
            right = rightWall;
            asked = Slack;
        }

        /// <summary>Whether this column of tiles is in a box that is standing.</summary>
        public static bool Covers(int tileX)
        {
            return Up && (tileX + 1) * 16f > left && tileX * 16f < right;
        }

        /// <summary>First thing in the update, before the players: a box nobody asked for last tick goes.</summary>
        internal static void Tick()
        {
            if (asked > 0)
                asked--;
        }

        internal static void Reset()
        {
            asked = 0;
            hold = 0f;
        }

        // ---- the camera ----------------------------------------------------------------
        //
        // Held last of all, after vanilla has placed the camera, every mod has moved it and
        // vanilla has held it to the world's own edges (Main.ClampScreenPositionToWorld): the
        // same sum as that one, with the box's walls for the world's. Eased in and out, so the
        // picture never jumps when a box comes up round a camera that is already past a wall,
        // or goes from round one that is against it.

        /// <summary>How far the camera is held by the walls, 0 to 1.</summary>
        private static float hold;

        /// <summary>The walls it is held by: kept after the box has gone, for the way back.</summary>
        private static float holdLeft;
        private static float holdRight;

        /// <summary>Once a tick, on a machine with a screen: whether this machine's player is in the box.</summary>
        internal static void TickCamera()
        {
            Player me = Main.LocalPlayer;

            if (Up && me.active && me.GetModPlayer<BossArenaPlayer>().Held && By(me, left, right))
            {
                holdLeft = left;
                holdRight = right;
                hold = Math.Min(1f, hold + 1f / EaseTicks);
                return;
            }

            // The box has gone from round them: the camera walks back to them. They have been
            // taken out of it (a mirror, a bed): it is theirs again at once.
            hold = !Up && me.active && By(me, holdLeft, holdRight) ? Math.Max(0f, hold - 1f / EaseTicks) : 0f;
        }

        /// <summary>Between these walls, or no further past one than a held player is put back from.</summary>
        private static bool By(Player player, float leftWall, float rightWall)
        {
            float middle = player.Center.X;
            return middle >= leftWall - BossArenaPlayer.LetGo && middle <= rightWall + BossArenaPlayer.LetGo;
        }

        /// <summary>
        /// The camera, kept from showing anything past a wall. What is on screen runs from
        /// screenPosition + Translation for screenWidth / Zoom (SpriteViewMatrix), which is how
        /// vanilla does the same sum for the world's edges.
        /// </summary>
        internal static void HoldCamera()
        {
            if (hold <= 0f || Main.gameMenu)
                return;

            float zoom = Main.GameViewMatrix.Zoom.X;

            if (!(zoom > 0f))
                return;

            float shift = Main.GameViewMatrix.Translation.X;
            float wide = Main.screenWidth / zoom;

            float least = holdLeft - shift;
            float most = holdRight - wide - shift;

            float now = Main.screenPosition.X;

            // A screen wider than the box shows all of it, with the box in the middle.
            float held = most >= least ? MathHelper.Clamp(now, least, most) : (least + most) * 0.5f;
            float eased = hold * hold * (3f - 2f * hold);

            Main.screenPosition.X = (float)Math.Round(MathHelper.Lerp(now, held, eased));
        }
    }

    /// <summary>Ticks the box, and holds the camera to it after everything else has had the camera.</summary>
    public class BossArenaSystem : ModSystem
    {
        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.ClampScreenPositionToWorld += HoldCameraLast;
        }

        private static void HoldCameraLast(On_Main.orig_ClampScreenPositionToWorld orig)
        {
            orig();
            BossArena.HoldCamera();
        }

        public override void PreUpdateEntities()
        {
            BossArena.Tick();
        }

        public override void PostUpdateEverything()
        {
            if (!Main.dedServ)
                BossArena.TickCamera();
        }

        public override void OnWorldUnload()
        {
            BossArena.Reset();
        }
    }

    /// <summary>
    /// A player's side of the box: put back inside it at the end of every tick they are in
    /// it, the way vanilla puts a player back inside the world (Player.BordersMovement: where
    /// they are, and the speed that took them there, after the move), and under Boss Active
    /// for as long.
    ///
    /// After the move rather than before it, so that anything that reads what a wall did to
    /// them reads it off this one too: the ragdoll (RagdollPlayer) comes off it as it comes
    /// off a wall of stone, because this runs before that does (the two are in the order of
    /// their names).
    /// </summary>
    public class BossArenaPlayer : ModPlayer
    {
        /// <summary>
        /// Further past a wall than this is not walking, running or being thrown (a mirror, a
        /// bed, a death): they are let go rather than put back. Nothing that moves a player a
        /// tick at a time covers this much ground in one.
        /// </summary>
        public const float LetGo = 320f;

        /// <summary>In the box: between its walls when it came up, or walked in since.</summary>
        public bool Held { get; private set; }

        public override void PostUpdate()
        {
            if (!BossArena.Up)
            {
                Held = false;
                return;
            }

            float middle = Player.Center.X;

            if (!Held)
                Held = middle >= BossArena.Left && middle <= BossArena.Right;
            else if (middle < BossArena.Left - LetGo || middle > BossArena.Right + LetGo)
                Held = false;

            if (!Held)
                return;

            float least = BossArena.Left;
            float most = BossArena.Right - Player.width;

            if (Player.position.X < least)
            {
                Player.position.X = least;

                if (Player.velocity.X < 0f)
                    Player.velocity.X = 0f;
            }
            else if (Player.position.X > most)
            {
                Player.position.X = most;

                if (Player.velocity.X > 0f)
                    Player.velocity.X = 0f;
            }

            Player.AddBuff(ModContent.BuffType<BossActive>(), 2);
        }
    }

    /// <summary>Nothing in a standing box is blown up: Boss Active stops the pick and the hammer, and this the bomb.</summary>
    public class BossArenaTiles : GlobalTile
    {
        public override bool CanExplode(int i, int j, int type) => !BossArena.Covers(i);
    }

    /// <summary>Nor its walls.</summary>
    public class BossArenaWalls : GlobalWall
    {
        public override bool CanExplode(int i, int j, int type) => !BossArena.Covers(i);
    }
}
