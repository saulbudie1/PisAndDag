using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.LoreItem;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The Guide's main quest: the little hut seawards. He asks it in his first words and in his hints
    /// (Hints.GuideHut): "Do me a favour and tell me what is inside." Inside, in the Blood Shrine's chest,
    /// is his family's Weathered Ledger (put there with the chest's other things, PADStructureSystem;
    /// in a world made before it, put into the shrine's chest when the world loads). Brought back to him
    /// ("The little hut"), he reads it, and the quest is done:
    ///
    ///   - the off hand opens, for every player in the world (OffHandPlayer, OffHandSlot), and
    ///   - the Voodoo Demons come: until then the game's demons below are only demons
    ///     (<see cref="GuideQuestVoodoo"/>). He has read his face in it; whoever else has seen that
    ///     face starts carrying it.
    ///
    /// By request: "Create an OffHand Slot ... unlocked during game Guide's Main Quest (think about one
    /// and until that quest is done you voodoo demons does not spawn)", with the quest the hut and his
    /// book, the whole world's.
    ///
    /// World state, saved and sent: <see cref="Done"/>; <see cref="LedgerPlaced"/>, that the ledger was
    /// put in the shrine's chest; <see cref="LedgerMissing"/>, that a world made before it had no chest
    /// to put it in, where telling him is enough.
    /// </summary>
    public class GuideQuest : ModSystem
    {
        /// <summary>Saved, sent. The ledger has been brought to him: the off hand is open and the demons carry their dolls.</summary>
        public static bool Done;

        /// <summary>Saved. The ledger is in the shrine's chest (set where the chest is filled).</summary>
        public static bool LedgerPlaced;

        /// <summary>Saved, sent. A world made before the ledger, with no shrine chest found to put it in.</summary>
        public static bool LedgerMissing;

        private static readonly Color Opened = new Color(196, 180, 152);

        // ---- what he says (Lore Bible §XXVII: the line, the key, the likeness; none of it named) --------------

        private const string Label = "The little hut";

        private const string Asked =
            "On the trail seawards, where the trees give out. I did not go in. If there is anything in there " +
            "with writing on it, bring it to me.";

        private const string Missing =
            "You went in, then. No, do not tell me. I think I already know. Keep your other hand free from now " +
            "on. Something has been looking for me a long while.";

        private const string Read =
            "That is my grandmother's hand, and her mother's before it. They kept count of the same thing, " +
            "page after page, and at the foot of each page they drew a face. It is mine. Keep your other hand " +
            "free from now on. Whoever else has seen this face will come looking for it.";

        private const string Opening = "Your other hand is free.";

        public override void ClearWorld()
        {
            Done = false;
            LedgerPlaced = false;
            LedgerMissing = false;
        }

        public override void PostSetupContent()
        {
            TalkOptions.Register(NPCID.Guide, new TalkOption(
                TalkOptions.Icon(ModContent.ItemType<WeatheredLedger>()) + Label,
                npc => Talked(Main.LocalPlayer),
                (player, npc) => !Done));
        }

        /// <summary>The player asked him about the hut: with the ledger (or in a world with none to find), it is done.</summary>
        private static void Talked(Player player)
        {
            bool has = player.HasItem(ModContent.ItemType<WeatheredLedger>());

            if (!has && !LedgerMissing)
            {
                TalkOptions.Say(Asked);
                return;
            }

            TalkOptions.Say(has ? Read : Missing);
            Finish();
        }

        /// <summary>Done: here, and on the server, which tells everyone.</summary>
        internal static void Finish()
        {
            if (Done)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<PisAndDag>().GetPacket();
                packet.Write((byte)PADPacket.GuideQuest);
                packet.Write(true);
                packet.Send();
                Done = true;
                return;
            }

            Done = true;
            Main.NewText(Opening, Opened);
        }

        /// <summary>The quest owed again (the command), here and on the server.</summary>
        internal static void Reset()
        {
            Done = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>On the server: a player has finished it (or a command reset it). Everyone hears.</summary>
        internal static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            bool done = reader.ReadBoolean();

            if (Main.netMode != NetmodeID.Server)
                return;

            if (done && !Done)
            {
                Done = true;
                NetMessage.SendData(MessageID.WorldData);
                Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral(Opening), Opened);
            }
        }

        public override void OnWorldLoad()
        {
            // A world made before the ledger: put it in the shrine's chest, once.
            if (Main.netMode == NetmodeID.MultiplayerClient || Done || LedgerPlaced)
                return;

            int chest = FindShrineChest();

            if (chest >= 0 && PutLedger(Main.chest[chest]))
            {
                LedgerPlaced = true;
                LedgerMissing = false;
                Mod.Logger.Info($"Weathered Ledger put in the Blood Shrine's chest at {Main.chest[chest].x}, {Main.chest[chest].y} (a world made before it).");
            }
            else
            {
                LedgerMissing = true;
                Mod.Logger.Info("No Blood Shrine chest found for the Weathered Ledger: the Guide takes the player's word for the hut in this world.");
            }
        }

        /// <summary>
        /// The Blood Shrine's chest in a world that does not say where it is: the Living Wood Chest on the
        /// surface nearest the spawn (the game's own Living Wood Chests are in its living trees, under the
        /// ground); one with the Ominous Orb still in it first.
        /// </summary>
        private static int FindShrineChest()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            int orb = ModContent.ItemType<Items.GameChangingOrb>();

            for (int i = 0; i < Main.maxChests; i++)
            {
                Chest chest = Main.chest[i];

                if (chest == null || !WorldGen.InWorld(chest.x, chest.y, 2))
                    continue;

                Tile tile = Main.tile[chest.x, chest.y];

                if (!tile.HasTile || tile.TileType != TileID.Containers || tile.TileFrameX / 36 != 12 || chest.y > Main.worldSurface)
                    continue;

                foreach (Item item in chest.item)
                {
                    if (item != null && item.type == orb)
                        return i;
                }

                float distance = Math.Abs(chest.x - Main.spawnTileX) + Math.Abs(chest.y - Main.spawnTileY);

                if (distance < bestDistance && distance < 2500f)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The ledger into the first empty slot of a chest; false with none (or one in it already).</summary>
        internal static bool PutLedger(Chest chest)
        {
            int ledger = ModContent.ItemType<WeatheredLedger>();

            foreach (Item item in chest.item)
            {
                if (item != null && item.type == ledger)
                    return true;
            }

            for (int i = 0; i < chest.item.Length; i++)
            {
                if (chest.item[i] == null || chest.item[i].IsAir)
                {
                    chest.item[i] = new Item();
                    chest.item[i].SetDefaults(ledger);
                    return true;
                }
            }

            return false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Done)
                tag["GuideQuestDone"] = true;

            if (LedgerPlaced)
                tag["GuideLedgerPlaced"] = true;

            if (LedgerMissing)
                tag["GuideLedgerMissing"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Done = tag.ContainsKey("GuideQuestDone");
            LedgerPlaced = tag.ContainsKey("GuideLedgerPlaced");
            LedgerMissing = tag.ContainsKey("GuideLedgerMissing");
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(Done);
            writer.Write(LedgerMissing);
        }

        public override void NetReceive(BinaryReader reader)
        {
            Done = reader.ReadBoolean();
            LedgerMissing = reader.ReadBoolean();
        }

        /// <summary>Where it stands, for the command.</summary>
        internal static string Describe() =>
            Done ? "The Guide's quest is done: the off hand is open and the Voodoo Demons come."
            : LedgerMissing ? "The Guide's quest is owed. This world has no ledger to find: asking him about the hut finishes it."
            : "The Guide's quest is owed: the Weathered Ledger is in the Blood Shrine's chest, seawards.";
    }

    /// <summary>
    /// Until the Guide has read his ledger (GuideQuest), the game's Voodoo Demons do not come: one that
    /// would is a plain Demon instead. Summoned ones (a command, a statue, another mod) are left alone.
    /// </summary>
    public class GuideQuestVoodoo : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.VoodooDemon;

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (GuideQuest.Done || Main.netMode == NetmodeID.MultiplayerClient || source is not EntitySource_SpawnNPC)
                return;

            npc.Transform(NPCID.Demon);
        }
    }
}
