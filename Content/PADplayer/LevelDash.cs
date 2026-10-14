using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// A dash goes level. Tabi and the Master Ninja Gear, the Shield of Cthulhu, the
    /// Solar Flare and Crystal Assassin armours: while one is under way the player moves
    /// along the ground's line and nothing else, whatever they were doing before it, and
    /// falls again the moment it is spent. (None is under way on a mount: mounting ends
    /// one, Mount.SetMount, and DashMovement begins none on a mount.)
    ///
    /// Vanilla's "dashing" is Player.dashDelay below zero: DashMovement sets it to -1 on
    /// the tick a dash starts and holds it there until the dash has slowed to a run (or
    /// the Shield of Cthulhu has bounced off something), when it becomes the cooldown.
    /// Gravity has been added to the fall by the time PreUpdateMovement runs and the
    /// player has not been moved yet, so taking the fall away here is the whole of it.
    ///
    /// Within a step of the ground (<see cref="OverGround"/>) all of the fall is taken, as it
    /// always was; further up, all but a hair of it (<see cref="Hair"/>), by request: "Why is
    /// the dash regenerating my farts? is it because it goes vel.Y = 0 so it registers? maybe
    /// make it falls down very slightly". It was: the game takes a player who is not falling
    /// at all for one standing on the ground (Player.Update: velocity.Y == 0f), and in the
    /// air, dashing, gave back what the ground gives back: every jump in the air
    /// (RefreshDoubleJumps: the bottle's farts, a Cloud's), the rocket boots' time and the
    /// wings' flight; let them jump as if off the ground (JumpMovement); and slowed them as it
    /// slows one on the ground (HorizontalMovement). With a hair of a fall left, the way
    /// gravity pulls, it takes them for what they are, in the air, and a dash in the air
    /// carries as far as the game's own does there.
    ///
    /// The step is the game's own (Collision.StepDown): it sets a player down onto ground
    /// more than 7 px and less than 17 px under their feet, under where they are going, if
    /// they were not falling when the tick began (their fall, at that point of it, is the
    /// tick's gravity alone). It is how a run goes down stairs, and how a level dash went down
    /// a hill's slope, the hill falling away under it every tick and the step setting it back
    /// down the tick after. Within its reach the game goes on taking the dasher for one on the
    /// ground, as before: along the ground, up and down steps and slopes, over platforms, a
    /// dash goes as it went. Out of its reach the hair is left; and where the hair alone kept
    /// the game from the step (ground come within reach of a dash in the air), the step is
    /// taken here (<see cref="StepDown"/>), on the tick the game would have taken it.
    ///
    /// A jump begun in the air while a dash is under way (a fart, a Cloud's), or wings or
    /// rocket boots held through one, are spent as the game spends them and lift nothing till
    /// the dash is spent: the dash holds the player level, whatever they do. (Before the hair
    /// the game took a dasher in the air for one on the ground: a jump pressed was one off it,
    /// and all of it was given back.)
    /// </summary>
    public class LevelDash : ModPlayer
    {
        /// <summary>The fall left to a dash in the air, in pixels a tick: a pixel in about eight seconds of dashing (a dash of Tabi's lasts under one).</summary>
        public const float Hair = 0.002f;

        public override void PreUpdateMovement()
        {
            if (Player.dashDelay >= 0)
                return;
            StepDown();
            Player.velocity.Y = OverGround() ? 0f : Hair * Player.gravDir;
        }

        /// <summary>
        /// The game's own step down, as Player.Update takes it just before this hook: not while
        /// shimmering, nor in a minecart or on a mount that keeps off the ground (the UFO, the
        /// Drill, the Cute Fishron, the Pirate Ship, the Dark Mage's book, the Lava Shark), and
        /// only for a player whose fall is the tick's gravity alone (velocity.Y == gravity). Taken
        /// here only where that last was not so for the hair alone: a fall of the tick's gravity
        /// and no more than the hair left at the end of the last tick. Where the game took the
        /// step itself (the tick began on the ground) the fall is the gravity alone and this is
        /// not taken again. With the gravity turned upside down the game's test is not met at the
        /// start of a dash's tick, hair or none (the fall is upward), and neither is this taken.
        /// </summary>
        void StepDown()
        {
            Player p = Player;
            if (p.gravDir != 1f || p.shimmering)
                return;
            if (p.velocity.Y <= p.gravity || p.velocity.Y > p.gravity + Hair)
                return;
            if (p.mount.Active && (p.mount.Cart || p.mount.Type is MountID.UFO or MountID.Drill or MountID.CuteFishron
                or MountID.PirateShip or MountID.DarkMageBook or MountID.LavaShark))
                return;
            Collision.StepDown(ref p.position, ref p.velocity, p.width, p.height, ref p.stepSpeed, ref p.gfxOffY, (int)p.gravDir, p.waterWalk || p.waterWalk2);
        }

        /// <summary>
        /// Whether the game's step down, looking where it looks, finds ground less than 17 px
        /// under the player's feet (or at them, or over them, as on a slope): Collision.StepDown's
        /// own search, read from it, without the step. The columns under where the player will be
        /// after this tick's move; the row their feet are in or stand on, and the one under it; a
        /// tile's top counted only if that tile, lifted 17 px, overlaps them where they are now; a
        /// half block's top 8 px down; the top of a pool counted for one who walks on water; each
        /// of the world's lowest 42 rows counted as ground. Never with the gravity upside down or
        /// while shimmering, when the game takes no step.
        /// </summary>
        bool OverGround()
        {
            Player p = Player;
            if (p.gravDir != 1f || p.shimmering)
                return false;
            Vector2 at = p.position;
            at.X += p.velocity.X;
            at.Y = (float)Math.Floor((at.Y + p.height) / 16f) * 16f - p.height;
            int left = (int)(at.X / 16f), right = (int)((at.X + p.width) / 16f);
            int row = (int)((at.Y + p.height + 4f) / 16f);
            float floor = (row + p.height / 16 + (p.height % 16 > 0 ? 1 : 0)) * 16;
            float lowest = Main.bottomWorld / 16f - 42f;
            bool onWater = p.waterWalk || p.waterWalk2;
            for (int i = left; i <= right; i++)
            {
                for (int j = row; j <= row + 1; j++)
                {
                    if (!WorldGen.InWorld(i, j, 1))
                        return false;
                    Tile tile = Main.tile[i, j];
                    if (onWater && tile.LiquidAmount > 0 && Main.tile[i, j - 1].LiquidAmount == 0)
                    {
                        int top = j * 16 + 16 - (tile.LiquidAmount / 32 * 2 + 2);
                        if (new Rectangle(i * 16, j * 16 - 17, 16, 16).Intersects(new Rectangle((int)p.position.X, (int)p.position.Y, p.width, p.height)) && top < floor)
                            floor = top;
                    }
                    if (j >= lowest || tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                    {
                        int top = j * 16 + (tile.IsHalfBlock ? 8 : 0);
                        if (Utils.FloatIntersect(i * 16, j * 16 - 17, 16f, 16f, p.position.X, p.position.Y, p.width, p.height) && top < floor)
                            floor = top;
                    }
                }
            }
            return floor - (p.position.Y + p.height) < 17f;
        }
    }
}
