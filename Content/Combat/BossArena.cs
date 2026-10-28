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
    ///
    /// A box asked for SEALED (<see cref="Keep(float, float, bool, float, float)"/>, the Gilded
    /// Knight's field) keeps out as well as in: somebody outside it when it comes up, or who leaves
    /// it, is put back outside at a wall they walk into, as one inside is put back inside, and
    /// one taken into it any other way (a rod, a potion, up out of the ground) is put back where
    /// they were. Only those in it as it stands up are in it. It may have a height as well: who
    /// is over or under it then (in a city under the field) is neither in it nor held out at its
    /// walls, only kept from coming up or down into it.
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
        private static bool sealedBox;
        private static float top = float.MinValue;
        private static float bottom = float.MaxValue;

        /// <summary>The box is standing.</summary>
        public static bool Up => asked > 0;

        /// <summary>The standing box keeps those outside it out (only meaningful while it is <see cref="Up"/>).</summary>
        public static bool Sealed => sealedBox;

        /// <summary>The walls, x in the world, in pixels. Only meaningful while it is <see cref="Up"/>.</summary>
        public static float Left => left;
        public static float Right => right;

        /// <summary>The top of the box's height (a world y, in pixels; the least float for a box asked for without one).</summary>
        public static float Top => top;

        /// <summary>The foot of the box's height (a world y, in pixels; the greatest float for a box asked for without one).</summary>
        public static float Bottom => bottom;

        /// <summary>Whether feet at this height (a world y, in pixels) are at the box's height: always, for a box asked for without one.</summary>
        public static bool LevelWith(float feetY) => feetY >= top && feetY <= bottom;

        /// <summary>
        /// The box, this tick: its two walls, x in the world, in pixels. Asked every tick for as
        /// long as it should stand, by the same asker on every machine.
        /// </summary>
        public static void Keep(float leftWall, float rightWall) => Keep(leftWall, rightWall, false);

        /// <summary>
        /// The box, this tick, as <see cref="Keep(float, float)"/>; <paramref name="closed"/>: nobody
        /// comes into it either, from the tick it stands up.
        /// </summary>
        public static void Keep(float leftWall, float rightWall, bool closed) => Keep(leftWall, rightWall, closed, float.MinValue, float.MaxValue);

        /// <summary>
        /// The box, this tick, as <see cref="Keep(float, float, bool)"/>, with a height: only a
        /// player whose feet are between <paramref name="topY"/> and <paramref name="bottomY"/>
        /// (world pixels) as it stands up is in it, or held out at its walls.
        /// </summary>
        public static void Keep(float leftWall, float rightWall, bool closed, float topY, float bottomY)
        {
            if (!(rightWall > leftWall))
                return;

            left = leftWall;
            right = rightWall;
            sealedBox = closed;
            top = bottomY > topY ? topY : float.MinValue;
            bottom = bottomY > topY ? bottomY : float.MaxValue;
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
            sealedBox = false;
            top = float.MinValue;
            bottom = float.MaxValue;
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

        /// <summary>
        /// Kept out of a sealed box: outside it when it was first seen standing (or since), and on
        /// which side of it (-1 its left, 1 its right, 2 over or under it). 0 while there is no
        /// such box.
        /// </summary>
        public int Out { get; private set; }

        /// <summary>Further than this in a tick is not walking or falling into a sealed box: put back where they last stood outside it.</summary>
        private const float Walked = 48f;

        /// <summary>Kept out of a sealed box: where they last stood outside it (on this machine's reckoning of them).</summary>
        private Vector2 lastOutside;

        /// <summary>Come back from death since their last tick.</summary>
        private bool respawned;

        public override void OnRespawn() => respawned = true;

        public override void PostUpdate()
        {
            if (!BossArena.Up)
            {
                Held = false;
                Out = 0;
                respawned = false;
                return;
            }

            if (BossArena.Sealed)
            {
                Sealed();
                respawned = false;
                return;
            }

            Out = 0;
            float middle = Player.Center.X;

            if (!Held)
                Held = middle >= BossArena.Left && middle <= BossArena.Right;
            else if (middle < BossArena.Left - LetGo || middle > BossArena.Right + LetGo)
                Held = false;

            if (!Held)
                return;

            HoldIn();
        }

        /// <summary>Put back inside at the wall they came to, and under Boss Active.</summary>
        private void HoldIn()
        {
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

        /// <summary>
        /// A sealed box: who is inside it as it stands up is in it, at whatever height they go after;
        /// who is outside it then (beside it at its height, or over or under it), or leaves it after,
        /// stays outside. Done the same on every machine, to every machine's copy of every player, as
        /// the open box is: the server moves its copies of players between their machines' word of
        /// them, so its copies are put back as their own machines put them back.
        /// </summary>
        private void Sealed()
        {
            float middle = Player.Center.X;
            bool between = middle >= BossArena.Left && middle <= BossArena.Right;
            bool inside = between && BossArena.LevelWith(Player.Bottom.Y);

            if (Held)
            {
                if (middle < BossArena.Left - LetGo || middle > BossArena.Right + LetGo)
                {
                    Held = false;
                    Out = Side(middle);
                    lastOutside = Player.position;
                    return;
                }

                HoldIn();
                return;
            }

            // First seen in it, or back from death at a bed in it (their home is there; anything else
            // could only take them back to where they died): in it.
            if ((Out == 0 || respawned) && inside)
            {
                Held = true;
                Out = 0;
                HoldIn();
                return;
            }

            if (!inside)
            {
                Out = Side(middle);
                lastOutside = Player.position;

                if (Out != 2)
                    KeepOut();

                return;
            }

            PutBack();
        }

        /// <summary>Which side of a sealed box a player outside it is on: -1 its left, 1 its right, 2 over or under it.</summary>
        private static int Side(float middle) => middle < BossArena.Left ? -1 : middle > BossArena.Right ? 1 : 2;

        /// <summary>Beside a sealed box at its height: held outside its wall on their side, the way one inside is held inside it.</summary>
        private void KeepOut()
        {
            if (!BossArena.LevelWith(Player.Bottom.Y))
                return;

            if (Out < 0)
            {
                float most = BossArena.Left - Player.width;

                if (Player.position.X > most)
                {
                    Player.position.X = most;

                    if (Player.velocity.X > 0f)
                        Player.velocity.X = 0f;
                }
            }
            else if (Out == 1)
            {
                float least = BossArena.Right;

                if (Player.position.X < least)
                {
                    Player.position.X = least;

                    if (Player.velocity.X < 0f)
                        Player.velocity.X = 0f;
                }
            }
        }

        /// <summary>
        /// Found in a sealed box they are kept out of: one who walked into a wall at its height is
        /// stopped at it; one who came down onto it out of the sky over it is borne up by it, a floor
        /// at its top, until they walk off it past a wall; anyone else (up into it out of the ground
        /// under it; taken into it by a rod, a potion, a pylon) is put back where they last stood
        /// outside it, which was clear of the ground. (The game's oldPosition will not do for that: a
        /// teleport sets it to where they went.)
        /// </summary>
        private void PutBack()
        {
            Vector2 was = lastOutside;
            float wasMiddle = was.X + Player.width * 0.5f;
            float wasFeet = was.Y + Player.height;
            bool walked = Math.Abs(Player.position.X - was.X) <= Walked && Math.Abs(Player.position.Y - was.Y) <= Walked;

            if (walked && BossArena.LevelWith(wasFeet))
            {
                if (wasMiddle < BossArena.Left)
                {
                    Player.position.X = BossArena.Left - Player.width;

                    if (Player.velocity.X > 0f)
                        Player.velocity.X = 0f;

                    Out = -1;
                }
                else
                {
                    Player.position.X = BossArena.Right;

                    if (Player.velocity.X < 0f)
                        Player.velocity.X = 0f;

                    Out = 1;
                }
            }
            else if (walked && wasFeet < BossArena.Top)
            {
                Player.position.Y = BossArena.Top - Player.height - 1f;

                if (Player.velocity.Y > 0f)
                    Player.velocity.Y = 0f;

                Player.fallStart = (int)(Player.position.Y / 16f);
                Out = 2;
            }
            else
            {
                Player.position = was;
                Player.velocity = Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Out = Side(wasMiddle);
            }

            lastOutside = Player.position;
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
