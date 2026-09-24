#if !PREVIEW
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.LoreItem;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures.Grace
{
    /// <summary>
    /// The locks on the deep of the kingdom: the tall gate of the lodge at the head of the
    /// stair under the lowest street (Grace.ReliquarySeal), and the door of the chamber at the
    /// foot of the crack (Grace.ArenaDoor). Between them they are the only ways into
    /// everything under the Undercity; the brick round the deep (GraceBuilder.DeepShell) sees
    /// to the rest.
    ///
    /// While locked, neither opens, whether by hand, by walking into it, by a town NPC or by
    /// a signal down a wire, and neither can be broken or blown up. Each draws as a black
    /// shadow of itself that no light reaches. The first time somebody carrying the
    /// Reliquary Key tries either one, both open for good (PADWorld.GraceUnsealed), and they
    /// go back to being a gate and a door.
    ///
    /// Nothing is said when a lock refuses: the gate is the message.
    /// </summary>
    public class GraceSeal : ModSystem
    {
        /// <summary>The marks of the things that lock.</summary>
        private static readonly string[] Locks = { "Grace.ReliquarySeal", "Grace.ArenaDoor" };

        /// <summary>How near a lock, in tiles, the key has to be for it to turn.</summary>
        private const int KeyReach = 8;

        public override void Load()
        {
            On_WorldGen.OpenDoor += KeepDoorShut;
            On_WorldGen.ShiftTallGate += KeepGateShut;
        }

        /// <summary>This world cell is part of a lock that is still locked.</summary>
        public static bool Locked(int i, int j)
        {
            if (PADWorld.GraceUnsealed)
                return false;

            foreach ((string name, Rectangle area) in GraceWorld.Marks)
            {
                if (!area.Contains(i, j))
                    continue;

                foreach (string lockName in Locks)
                    if (name == lockName)
                        return true;
            }

            return false;
        }

        /// <summary>How far, in tiles, this world position is from the nearest lock. int.MaxValue if there is none.</summary>
        private static int Distance(Vector2 at)
        {
            int best = int.MaxValue;
            foreach ((string name, Rectangle area) in GraceWorld.Marks)
            {
                foreach (string lockName in Locks)
                {
                    if (name != lockName)
                        continue;

                    Vector2 centre = new Vector2(area.X + area.Width * 0.5f, area.Y + area.Height * 0.5f) * 16f;
                    best = System.Math.Min(best, (int)(Vector2.Distance(at, centre) / 16f));
                }
            }

            return best;
        }

        private static bool KeepDoorShut(On_WorldGen.orig_OpenDoor orig, int i, int j, int direction)
        {
            if (Locked(i, j) && !TurnKey(i, j))
                return false;

            return orig(i, j, direction);
        }

        private static bool KeepGateShut(On_WorldGen.orig_ShiftTallGate orig, int x, int y, bool closing, bool forced)
        {
            if (!closing && Locked(x, y) && !TurnKey(x, y))
                return false;

            return orig(x, y, closing, forced);
        }

        /// <summary>Whether this player carries the key, in the pack or on the codex shelf.</summary>
        public static bool Carries(Player player)
        {
            int key = ModContent.ItemType<ReliquaryKey>();
            if (player.HasItem(key))
                return true;

            foreach (Item item in player.GetModPlayer<LoreLock>().Codex)
                if (item != null && item.type == key)
                    return true;

            return false;
        }

        /// <summary>
        /// Somebody is trying a lock. If it is this machine's player, near enough and carrying the
        /// key, everything opens: here at once, and on the server by the packet that goes ahead of
        /// the door's own. A server never turns the key itself; it only hears that it was turned.
        /// </summary>
        private static bool TurnKey(int i, int j)
        {
            if (Main.netMode == NetmodeID.Server)
                return false;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead)
                return false;

            Vector2 lockAt = new Vector2(i * 16 + 8, j * 16 + 8);
            if (Vector2.Distance(player.Center, lockAt) > KeyReach * 16 || !Carries(player))
                return false;

            PADWorld.GraceUnsealed = true;
            SoundEngine.PlaySound(SoundID.Unlock, lockAt);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                GraceWorld.SendUnseal(ModContent.GetInstance<GraceSeal>().Mod);

            return true;
        }

        /// <summary>
        /// On the server: a player says they turned the key. Taken on their word if they are
        /// standing at a lock; then every machine hears the ways are open.
        /// </summary>
        internal static void Heard(int from)
        {
            if (Main.netMode != NetmodeID.Server || PADWorld.GraceUnsealed || from < 0 || from >= Main.maxPlayers)
                return;

            Player player = Main.player[from];
            if (player == null || !player.active || Distance(player.Center) > KeyReach + 6)
                return;

            PADWorld.GraceUnsealed = true;
            NetMessage.SendData(MessageID.WorldData);
        }
    }

    /// <summary>A lock while it is locked: unbreakable, and drawn as a black shadow light does not touch.</summary>
    public class GraceSealTile : GlobalTile
    {
        public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged) => !GraceSeal.Locked(i, j);

        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (GraceSeal.Locked(i, j))
                fail = true;
        }

        public override bool CanExplode(int i, int j, int type) => !GraceSeal.Locked(i, j);

        public override bool PreDraw(int i, int j, int type, SpriteBatch spriteBatch)
        {
            if (type != TileID.ClosedDoor && type != TileID.TallGateClosed || !GraceSeal.Locked(i, j))
                return true;

            // The frame, size and offset the tile renderer itself would use for this cell.
            Tile t = Main.tile[i, j];
            short frameX = t.TileFrameX, frameY = t.TileFrameY;
            Main.instance.TilesRenderer.GetTileDrawData(i, j, t, (ushort)type, ref frameX, ref frameY,
                out int width, out int height, out int top, out int halfBrick, out int addX, out int addY,
                out SpriteEffects effects, out _, out _, out _);

            Texture2D texture = TextureAssets.Tile[type].Value;
            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
            Vector2 at = new Vector2(i * 16 - (int)Main.screenPosition.X - (width - 16f) / 2f,
                j * 16 - (int)Main.screenPosition.Y + top + halfBrick) + zero;
            Rectangle frame = new Rectangle(frameX + addX, frameY + addY, width, height - halfBrick);

            // The shape of the thing in black, whatever the light: the colour takes everything
            // but the outline.
            spriteBatch.Draw(texture, at, frame, Color.Black, 0f, Vector2.Zero, 1f, effects, 0f);
            return false;
        }
    }
}
#endif
