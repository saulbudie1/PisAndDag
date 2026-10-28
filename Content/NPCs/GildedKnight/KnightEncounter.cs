using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Structures;
using PisAndDag.Content.Structures.Grace;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// Where the Gilded Knight stands, and when he comes down the field.
    ///
    /// HIS FIELD (by request: "spawns before the gate, lore wise"; the field placed, of the ways
    /// offered, "Before the Curtain"). The willow flat on the plateau before the Curtain, from the
    /// top of the way up to the mouth of the goblins' bore through the wall's foot: the bore is
    /// the only way in, and he stands before it. Not saved anywhere; found again in each world
    /// from the tower over the stair (its trapdoor's mark, GraceBuilder.WatchTower, whose place
    /// on the wall is fixed) and the ground itself: the Curtain's face, the bore's mouth twelve
    /// columns out from it, and the field from there out along the flat to where the ground falls
    /// away into the way up (no further than <see cref="FieldMost"/> columns, no nearer than
    /// <see cref="FieldLeast"/>).
    ///
    /// HIS POST is thirty columns out from the wall's face, on the road, facing down the field.
    /// The first time anyone comes up onto the plateau (within <see cref="StandFrom"/>) he is
    /// standing there, swords home, as he has stood; when someone comes on up the field to within
    /// <see cref="MeetFrom"/> of him, his scene. If nobody is anywhere near him (past
    /// <see cref="GoneFrom"/>), he is not there; he will be again. Once he is beaten
    /// (PADWorld.DownedGildedKnight) the post stands empty: he perished into light.
    ///
    /// CALLED BACK with what he leaves (the Gilded Spur, by request: "resummon with an item
    /// dropped from him"), rung on his field: he is at his post again under the black of his
    /// scene, and comes at them as he did. Rung anywhere in a world with no kingdom (a spur
    /// brought from another), he comes from off along the ground, to a field about them.
    ///
    /// Decided on the server (or the one machine there is); a client's spur asks it by packet.
    /// </summary>
    public class KnightEncounter : ModSystem
    {
        /// <summary>Within this of his post, on the plateau, he is standing there (world pixels): off the screen.</summary>
        public const float StandFrom = 112 * 16f;

        /// <summary>Nobody within this of him, and he is not there.</summary>
        public const float GoneFrom = 150 * 16f;

        /// <summary>Within this of him along the field, and his scene: far off, so the scene has him walk at them.</summary>
        public const float MeetFrom = 44 * 16f;

        /// <summary>The field, in columns out from the bore's mouth: at most, and at least.</summary>
        public const int FieldMost = 124;
        public const int FieldLeast = 70;

        /// <summary>From the Curtain's face: the bore's mouth, and his post.</summary>
        private const int MouthOut = 12;
        private const int PostOut = 30;

        /// <summary>Rows of the ground above or below the plain's level that are still the flat.</summary>
        private const int FlatRows = 6;

        /// <summary>After he has gone, this long before he stands at his post again.</summary>
        private const int Rest = 300;

        // ---- the field, found once a world ----

        private static bool surveyed;
        private static bool hasField;
        private static float fieldLeft;
        private static float fieldRight;
        private static Vector2 postAt;
        private static int outward;
        private static int level;

        private static int cooldown;
        private static bool wasThere;

        public override void ClearWorld() => Forget();

        public override void OnWorldUnload() => Forget();

        private static void Forget()
        {
            surveyed = false;
            hasField = false;
            cooldown = 0;
            wasThere = false;
        }

        // ============================================================
        // THE FIELD
        // ============================================================

        /// <summary>His field and post in this world, found the first time it is asked; false where there is no kingdom (or no Curtain in it).</summary>
        public static bool Survey()
        {
            if (surveyed)
                return hasField;

            // A client asks before the kingdom's map has come from the server, as often as not:
            // what it finds then is not kept (Found).
            surveyed = Main.netMode != NetmodeID.MultiplayerClient;
            hasField = false;

            if (!ExtraLargeWorld.Active)
                return false;

            Rectangle? trap = null;

            foreach ((string name, Rectangle area) in GraceWorld.Marks)
            {
                if (name == "Grace.CurtainTrapdoor")
                {
                    trap = area;
                    break;
                }
            }

            level = GraceBiome.PlainLevel;

            if (trap == null || level < 0)
                return false;

            // The trapdoor is 29 columns in from the wall's face, two wide (GraceBuilder.WatchTower):
            // the face is the kingdom's first column, the willows on its other side.
            int dir = WorldMirror.Dir;
            int face = dir > 0 ? trap.Value.X - 29 : trap.Value.X + 30;

            outward = -dir;

            int mouth = face + outward * MouthOut;
            int postCol = face + outward * PostOut;

            // A client reads only the ground the server has sent it: all of what is looked at, or
            // nothing is found (and the server, which has all of it, decides).
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                int far = mouth + outward * (FieldMost + 8);
                int x0 = Math.Max(1, Math.Min(face, far)), x1 = Math.Min(Main.maxTilesX - 2, Math.Max(face, far));
                int y0 = Math.Max(1, level - 12), y1 = Math.Min(Main.maxTilesY - 2, level + 40);

                if (!Main.sectionManager.TilesLoaded(x0, y0, x1, y1))
                    return false;
            }

            // Out along the flat until the ground falls away into the way up (or rises into something).
            int edge = mouth;

            for (int k = 1; k <= FieldMost + 8; k++)
            {
                int col = mouth + outward * k;
                int row = GroundRow(col, level);

                if (row < 0 || Math.Abs(row - level) > FlatRows)
                    break;

                edge = col;
            }

            int span = Math.Clamp(Math.Abs(edge - mouth) - 4, FieldLeast, FieldMost);
            int outer = mouth + outward * span;

            if (outward < 0)
            {
                fieldLeft = outer * 16f;
                fieldRight = mouth * 16f;
            }
            else
            {
                fieldLeft = (mouth + 1) * 16f;
                fieldRight = (outer + 1) * 16f;
            }

            int postRow = GroundRow(postCol, level);

            if (postRow < 0)
                return false;

            postAt = new Vector2(postCol * 16f + 8f, postRow * 16f);
            hasField = true;

            // Kept where the world is decided; a client looks again each time it asks (it asks
            // only when the spur is rung, and what it has of the ground changes as it goes about).
            surveyed = Main.netMode != NetmodeID.MultiplayerClient;
            return true;
        }

        /// <summary>The first row of ground (solid, or a platform) in a column, from a few rows over the plain down; -1 if there is none near.</summary>
        private static int GroundRow(int x, int near)
        {
            for (int y = near - 12; y < near + 40; y++)
            {
                if (!WorldGen.InWorld(x, y, 10))
                    return -1;

                Tile t = Main.tile[x, y];

                if (t.HasTile && !t.IsActuated && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]))
                    return y;
            }

            return -1;
        }

        /// <summary>
        /// Far enough inside his field's walls that, as his box stands up, the server's copy of them and
        /// their own machine both have them in it (a player at a wall then could be seen either side).
        /// </summary>
        private const float Inward = 4 * 16f;

        private static bool WellIn(Player player) => player.Center.X >= fieldLeft + Inward && player.Center.X <= fieldRight - Inward;

        /// <summary>
        /// The first row of ground under the columns his body would cover about a column, from a few
        /// rows over the given one down, with room over it across all of them for him to stand (five
        /// rows clear); -1 if there is none near.
        /// </summary>
        private static int GroundWithRoom(int x, int near)
        {
            int left = x - 1, right = x + 1;

            for (int y = near - 12; y < near + 40; y++)
            {
                if (!WorldGen.InWorld(left, y, 10) || !WorldGen.InWorld(right, y, 10))
                    return -1;

                bool ground = false;

                for (int c = left; c <= right && !ground; c++)
                {
                    Tile t = Main.tile[c, y];
                    ground = t.HasTile && !t.IsActuated && (Main.tileSolid[t.TileType] || Main.tileSolidTop[t.TileType]);
                }

                if (ground && GildedKnight.Clear(left, right, y - 5, y - 1))
                    return y;
            }

            return -1;
        }

        /// <summary>On his field, on (or near) the ground of the flat: not under it in the city, not high over it.</summary>
        public static bool OnField(Player player)
        {
            if (!Survey() || player == null || !player.active || player.dead)
                return false;

            float x = player.Center.X;
            float feetRow = player.Bottom.Y / 16f;

            return x >= fieldLeft && x <= fieldRight && feetRow > level - 18 && feetRow < level + 10;
        }

        // ============================================================
        // EVERY TICK, WHERE THE WORLD IS DECIDED
        // ============================================================

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (cooldown > 0)
                cooldown--;

            if (Main.GameUpdateCount % 15 != 0)
                return;

            NPC knight = Find();

            if (knight == null)
            {
                if (wasThere)
                {
                    wasThere = false;
                    cooldown = Rest;
                }

                if (PADWorld.DownedGildedKnight || cooldown > 0 || !Survey() || AnyBoss())
                    return;

                foreach (Player player in Main.ActivePlayers)
                {
                    if (player.dead || Vector2.Distance(player.Center, postAt) > StandFrom || player.Bottom.Y / 16f > level + 12)
                        continue;

                    Spawn(player, false);
                    return;
                }

                return;
            }

            wasThere = true;

            if (knight.ModNPC is not GildedKnight k || k.State != GildedKnight.Act.Post)
                return;

            foreach (Player player in Main.ActivePlayers)
            {
                if (!OnField(player) || !WellIn(player) || Math.Abs(player.Center.X - k.Post.X) > MeetFrom)
                    continue;

                k.Meet(player.whoAmI);
                return;
            }
        }

        /// <summary>The knight there is, if there is one.</summary>
        private static NPC Find()
        {
            int type = ModContent.NPCType<GildedKnight>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == type)
                    return npc;
            }

            return null;
        }

        private static bool AnyBoss()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.boss)
                    return true;
            }

            return false;
        }

        /// <summary>Him at his post on his field: standing his watch, or in his scene at once (called back).</summary>
        private static bool Spawn(Player player, bool scene)
        {
            IEntitySource source = new EntitySource_WorldEvent();
            int index = NPC.NewNPC(source, (int)postAt.X, (int)postAt.Y, ModContent.NPCType<GildedKnight>(), 0, 0f, 0f, 0f, 0f, player.whoAmI);

            if (index < 0 || index >= Main.maxNPCs)
                return false;

            NPC npc = Main.npc[index];

            if (npc.ModNPC is not GildedKnight knight)
                return false;

            knight.Place(postAt, fieldLeft, fieldRight, outward, player.whoAmI, scene);
            npc.netUpdate = true;
            return true;
        }

        // ============================================================
        // THE SPUR
        // ============================================================

        /// <summary>Whether the spur can be rung here and now by this player: why not, or null.</summary>
        public static string CannotCall(Player player)
        {
            NPC knight = Find();

            if (knight != null && knight.ModNPC is GildedKnight k && k.State != GildedKnight.Act.Post)
                return "Busy";

            if (AnyBoss() && knight == null)
                return "Busy";

            if (Survey() && (!OnField(player) || !WellIn(player)))
                return "Elsewhere";

            return null;
        }

        /// <summary>The spur rung, on this player's machine: straight to the world in single player, by packet from a client.</summary>
        public static void Ring(Player player)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Call(player);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                return;

            ModPacket packet = ModContent.GetInstance<PisAndDag>().GetPacket();
            packet.Write((byte)PADPacket.GildedKnight);
            packet.Write(Rung);
            packet.Send();
        }

        /// <summary>What a knight packet says: the spur rung (to the server), nothing answered it (back to whoever rang).</summary>
        private const byte Rung = 0;
        private const byte Unanswered = 1;

        /// <summary>
        /// On the server: the spur rung by <paramref name="from"/>; called, or, if they are not on his
        /// field, told so. On a client: told nothing answered (its own machine could not tell, not
        /// having the ground about his field).
        /// </summary>
        internal static void HandlePacket(BinaryReader reader, int from)
        {
            byte kind = reader.ReadByte();

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                if (kind == Unanswered)
                    GildedSpur.SayNothing(Main.LocalPlayer);

                return;
            }

            if (Main.netMode != NetmodeID.Server || kind != Rung || from < 0 || from >= Main.maxPlayers)
                return;

            Player player = Main.player[from];

            if (!player.active || player.dead)
                return;

            string why = CannotCall(player);

            if (why == null)
            {
                Call(player);
                return;
            }

            if (why == "Elsewhere")
            {
                ModPacket packet = ModContent.GetInstance<PisAndDag>().GetPacket();
                packet.Write((byte)PADPacket.GildedKnight);
                packet.Write(Unanswered);
                packet.Send(from);
            }
        }

        /// <summary>
        /// Called back for this player, where the world is decided: on his field, from his post
        /// (standing there already, his scene at once); in a world with no kingdom, from off along
        /// the ground on whichever side of them there is ground, to a field about them. Null when
        /// he comes; why not, when he does not.
        /// </summary>
        public static string Call(Player player)
        {
            if (player == null || !player.active || player.dead)
                return "There is nobody to call him for.";

            NPC there = Find();

            if (there != null)
            {
                if (there.ModNPC is GildedKnight standing && standing.State == GildedKnight.Act.Post)
                {
                    standing.Meet(player.whoAmI);
                    return null;
                }

                return "He is already here.";
            }

            if (Survey())
                return Spawn(player, true) ? null : "He could not be put at his post.";

            return Elsewhere(player) ? null : "There is no ground near enough for him to come over.";
        }

        /// <summary>
        /// A world with no kingdom: a field of the most columns about them, and him on the ground off
        /// to one side of them (the way they face first), facing them.
        /// </summary>
        private static bool Elsewhere(Player player)
        {
            foreach (int side in new[] { player.direction >= 0 ? 1 : -1, player.direction >= 0 ? -1 : 1 })
            {
                float x = player.Center.X + side * 34f * 16f;
                int col = (int)(x / 16f);
                int feetRow = (int)(player.Bottom.Y / 16f);
                int row = GroundWithRoom(col, feetRow);

                // Ground about as high as theirs: his field's height is his, and they must be in it.
                if (row < 0 || Math.Abs(row - feetRow) > 12)
                    continue;

                Vector2 feet = new Vector2(col * 16f + 8f, row * 16f);
                float middle = (player.Center.X + feet.X) * 0.5f;
                float half = FieldMost * 16f * 0.5f;

                IEntitySource source = new EntitySource_WorldEvent();
                int index = NPC.NewNPC(source, (int)feet.X, (int)feet.Y, ModContent.NPCType<GildedKnight>(), 0, 0f, 0f, 0f, 0f, player.whoAmI);

                if (index < 0 || index >= Main.maxNPCs || Main.npc[index].ModNPC is not GildedKnight knight)
                    return false;

                knight.Place(feet, middle - half, middle + half, -side, player.whoAmI, true);
                Main.npc[index].netUpdate = true;
                return true;
            }

            return false;
        }

        // ============================================================
        // THE COMMAND'S
        // ============================================================

        /// <summary>Back to never beaten, and whoever of him is about, gone.</summary>
        internal static void Reset()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.ModNPC is not GildedKnight)
                    continue;

                npc.active = false;
                npc.netSkip = -1;
                npc.life = 0;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }

            PADWorld.DownedGildedKnight = false;
            cooldown = 0;
            wasThere = false;
            surveyed = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>Where he stands in this world, in a line.</summary>
        internal static string Describe()
        {
            string beaten = PADWorld.DownedGildedKnight ? "beaten (his post stands empty; the spur calls him)" : "not yet beaten";

            if (!Survey())
                return $"The Gilded Knight: {beaten}. This world has no Curtain: the spur calls him to a field about whoever rings it.";

            return $"The Gilded Knight: {beaten}. His post: tile {(int)(postAt.X / 16f)}, {(int)(postAt.Y / 16f)}; " +
                $"his field: tiles {(int)(fieldLeft / 16f)} to {(int)(fieldRight / 16f)}.";
        }
    }
}
