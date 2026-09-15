using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;
using Terraria.UI.Gamepad;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The mouse plumbing the mod's late-drawn UI needs, in one place.
    ///
    /// Two things here are not what vanilla does, and both are deliberate.
    /// </summary>
    public static class PADMouse
    {
        private static bool wasDown;

        /// <summary>True on the one frame the left button goes down.</summary>
        public static bool Pressed { get; private set; }

        /// <summary>
        /// Call once at the top of the frame's UI, before anything reads Pressed.
        ///
        /// The edge is taken from Main.mouseLeft rather than Main.mouseLeftRelease.
        /// mouseLeftRelease is a shared latch — every widget that handles a click sets it
        /// false so the click is not taken twice, and it is only restored at the very end
        /// of Main.Draw. Watching the button ourselves gives an edge that nothing upstream
        /// can spend. Main.mouseLeft is written once per update by PlayerInput and is not
        /// touched during the draw, so it is safe to read here.
        /// </summary>
        public static void Tick()
        {
            bool down = Main.mouseLeft;

            Pressed = down && !wasDown;
            wasDown = down;
        }

        /// <summary>Marks the press as handled, for us and for anything downstream.</summary>
        public static void Consume()
        {
            Pressed = false;
            Main.mouseLeftRelease = false;
        }

        /// <summary>
        /// Is the mouse cursor the thing the player is pointing with?
        ///
        /// This is PlayerInput.IgnoreMouseInterface with one clause removed, and removing
        /// it is the whole point. Vanilla's property reads:
        ///
        ///     if (Main.LocalPlayer.UsingOrReusingItem &amp;&amp; !UsingGamepad &amp;&amp; !Main.gamePaused)
        ///         return true;
        ///     return UsingGamepad &amp;&amp; !UILinkPointNavigator.Available;
        ///
        /// So every mouse hit test guarded by it goes dead for as long as the player is
        /// swinging or firing something. Vanilla can afford that: its own widgets are only
        /// reachable with the inventory open, and hovering one sets mouseInterface, which
        /// stops the click from using the item in the first place. This mod's line sits
        /// under the chat with the inventory shut, over a player holding an autoReuse gun
        /// that may still be cycling from the last shot — and then the press lands on a
        /// frame where UsingOrReusingItem is already true, the hit test says the cursor is
        /// nowhere, and the line lights up on hover and does nothing at all when clicked.
        ///
        /// Only the half of that property that is genuinely about the mouse is kept.
        /// </summary>
        public static bool Steering => !(PlayerInput.UsingGamepad && !UILinkPointNavigator.Available);
    }

    /// <summary>
    /// The Mechanic's repair bench: one slot and a hammer, for straightening out a Dented
    /// Megashark.
    ///
    /// Built as the Goblin Tinkerer's reforge menu, because that is what it is, and it
    /// opens the way the tinkerer's does — NPCChatText_TinkererReforge in Main.cs:
    ///
    ///     playerInventory = true;
    ///     npcChatText = "";
    ///     SoundEngine.PlaySound(12);
    ///     InReforgeMenu = true;
    ///
    /// The dialogue goes away, the inventory comes up, and the bench takes the reforge
    /// menu's own spot at (50, 270). Every number here is vanilla's: the label 50 across,
    /// the hammer 70 across and 40 down, TextureAssets.Reforge[0] and [1] for its two
    /// states, the ±15 hit box, the 0.8→1 bloom stepped by 0.02 a frame, ItemSlot context
    /// 5 (PrefixItem), inventoryScale 0.85, and the same conditions that close the menu.
    ///
    /// The one thing vanilla gets for free that a mod has to ask for is the crafting list
    /// staying out of the way: DrawInventory skips it while InReforgeMenu is set. For mods
    /// tModLoader added Main.hidePlayerCraftingMenu, read at the same spot and reset every
    /// frame the inventory draws, so it is re-asserted each frame the bench is open.
    /// ExampleMod's ExamplePersonUI — the tModLoader team's own reforge-style bench — does
    /// exactly this.
    ///
    /// The bench closes itself, too: once a gun has been straightened and the player takes
    /// it, the inventory shuts and the Mechanic gets her line back.
    /// </summary>
    public static class MegasharkRepairUI
    {
        /// <summary>Which NPC opened this. -1 when closed.</summary>
        public static int NPCIndex = -1;

        public static bool Open;

        /// <summary>What is sitting in the slot. Never left holding anything once closed.</summary>
        public static Item Slot = new Item();

        /// <summary>What she says once the fixed gun is back in the player's hands.</summary>
        public const string DoneLine = "There you go, good as new. Try not to bend it again.";

        /// <summary>Top-left of the item slot. Vanilla's reforge slot, exactly.</summary>
        private const int SlotX = 50;
        private const int SlotY = 270;

        /// <summary>Where the label starts, relative to the slot. Vanilla's offset.</summary>
        private const int LabelDX = 50;

        /// <summary>Hammer centre, relative to the slot. Vanilla's offsets.</summary>
        private const int HammerDX = 70;
        private const int HammerDY = 40;

        /// <summary>Half-width of the hammer's hit box. Vanilla uses 15 either way.</summary>
        private const int HammerReach = 15;

        /// <summary>Vanilla's inventory scale for this row of the UI.</summary>
        private const float SlotScale = 0.85f;

        /// <summary>
        /// Blooms 0.8 → 1 while hovered, the way the reforge hammer does. Vanilla steps it
        /// by 0.02 a frame and so does this.
        /// </summary>
        private static float hammerScale = 0.8f;

        private static bool hoveringHammer;

        /// <summary>
        /// True once the slot holds a gun this bench straightened. It is what turns the
        /// next click on the slot from "swap" into "take it and pack up".
        /// </summary>
        private static bool repaired;

        /// <summary>Opens the bench for this NPC. Called from the answer line.</summary>
        public static void Show(int npcIndex)
        {
            NPCIndex = npcIndex;
            Open = true;
            repaired = false;

            // The tinkerer's exact move: dialogue down, inventory up.
            Main.playerInventory = true;
            Main.npcChatText = "";

            // Set here as well as in Draw so the inventory's very first frame is clear.
            Main.hidePlayerCraftingMenu = true;
        }

        /// <summary>
        /// Shuts the bench and gives back whatever was in the slot.
        ///
        /// Every exit runs through here. The one thing this UI must never do is eat a
        /// weapon, and the way that happens is an exit path someone forgot to hang the
        /// give-back on — so there is exactly one path.
        /// </summary>
        public static void Close()
        {
            Open = false;
            NPCIndex = -1;
            repaired = false;
            hoveringHammer = false;
            hammerScale = 0.8f;

            // Not left set: the flag is only reset by the inventory drawing, and the
            // inventory may not draw again for a while.
            Main.hidePlayerCraftingMenu = false;

            if (Main.dedServ)
                return;

            if (!Slot.IsAir)
            {
                // The cloned variant, as ExampleMod's bench does it: QuickSpawnItem spawns
                // a fresh item from type and prefix, which would drop this mod's own
                // per-instance state (the magazine) on the floor with it.
                Player player = Main.LocalPlayer;
                player.QuickSpawnClonedItem(player.GetSource_Misc("PADMegasharkRepair"), Slot, Slot.stack);
                Slot.TurnToAir();
            }

            // Anything on the cursor would otherwise vanish with the inventory.
            Main.LocalPlayer.dropItemCheck();
        }

        /// <summary>
        /// The happy ending: the fixed gun is in the pack, so the inventory shuts and the
        /// Mechanic picks the conversation back up. talkNPC was never cleared, so the
        /// dialogue box simply returns with her new line in it.
        /// </summary>
        private static void Finish()
        {
            Main.playerInventory = false;
            Main.npcChatText = DoneLine;
            SoundEngine.PlaySound(SoundID.MenuClose);

            Close();
        }

        /// <summary>True for the one item this bench accepts.</summary>
        public static bool Accepts(Item item) =>
            item != null && !item.IsAir && item.type == ModContent.ItemType<DentedMegashark>();

        /// <summary>Is the NPC that opened this still nearby and being talked to?</summary>
        private static bool StillTalking()
        {
            Player player = Main.LocalPlayer;

            if (!Main.npc.IndexInRange(NPCIndex) || !Main.npc[NPCIndex].active)
                return false;

            // Vanilla's own reforge guard, plus the NPC check. talkNPC going to -1 covers
            // walking away, the NPC dying, and closing the conversation.
            return player.talkNPC == NPCIndex
                && player.chest == -1
                && player.sign == -1
                && Main.npcShop == 0
                && !Main.InGuideCraftMenu
                && !Main.InReforgeMenu;
        }

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (!Open)
                return;

            // Escape shuts the inventory, and the bench goes with it — checked BEFORE the
            // inventory is forced back open below, or there would be no way out.
            if (!StillTalking() || !Main.playerInventory)
            {
                Close();
                return;
            }

            // Held open the way the tinkerer holds it open: the player needs somewhere to
            // drag the gun from. And the crafting list kept out of the bench's spot — this
            // flag is cleared every frame the inventory draws, so it is set every frame.
            Main.playerInventory = true;
            Main.hidePlayerCraftingMenu = true;

            Player player = Main.LocalPlayer;

            DrawLabel(spriteBatch);
            DrawSlot(spriteBatch, player);
            DrawHammer(spriteBatch, player);
        }

        /// <summary>
        /// The line beside the slot, where the tinkerer puts "Reforge:" or "Place an item
        /// here to reforge it".
        /// </summary>
        private static void DrawLabel(SpriteBatch spriteBatch)
        {
            string label = Slot.IsAir
                ? "Hand it over"
                : repaired ? "Good as new, take it" : "That'll do it";

            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value,
                label, new Vector2(SlotX + LabelDX, SlotY),
                new Color(Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor, Main.mouseTextColor),
                0f, Vector2.Zero, Vector2.One);
        }

        private static void DrawSlot(SpriteBatch spriteBatch, Player player)
        {
            // ItemSlot reads this rather than taking a scale argument, and it decides both
            // the drawn size and the hit box — so it is set here rather than inherited from
            // whatever the last bit of vanilla UI left behind.
            float restore = Main.inventoryScale;
            Main.inventoryScale = SlotScale;

            float width = TextureAssets.InventoryBack.Width() * Main.inventoryScale;
            float height = TextureAssets.InventoryBack.Height() * Main.inventoryScale;

            bool hovering = Main.mouseX >= SlotX && Main.mouseX <= SlotX + width
                         && Main.mouseY >= SlotY && Main.mouseY <= SlotY + height
                         && PADMouse.Steering;

            if (hovering)
            {
                // craftingHide fades the crafting tip out from under the bench, and
                // mouseInterface is what stops a click here also swinging a weapon.
                Main.craftingHide = true;
                player.mouseInterface = true;

                if (!Slot.IsAir)
                {
                    // MouseTextHackZoom, not MouseText — the hack variant is the one that
                    // forces an already-set HoverItem to actually render its tooltip, and
                    // it is what Calamity's enchant slot uses for the same job.
                    Main.HoverItem = Slot.Clone();
                    Main.instance.MouseTextHackZoom(string.Empty);
                }

                if (PADMouse.Pressed)
                    ClickSlot(player);
            }

            // ItemSlot.Draw wants an array and an index. One-element scratch, same as the
            // Lore Codex does it.
            Item[] scratch = { Slot };

            ItemSlot.Draw(spriteBatch, scratch, ItemSlot.Context.PrefixItem, 0, new Vector2(SlotX, SlotY));

            // Draw writes through the array, so copy any change back out.
            Slot = scratch[0];

            Main.inventoryScale = restore;
        }

        /// <summary>
        /// Two different clicks live on this slot.
        ///
        /// Before a repair it is a plain swap, done by hand rather than through
        /// ItemSlot.Handle because the bench takes exactly one kind of item and Handle
        /// would accept anything with a prefix.
        ///
        /// After a repair, clicking the fixed gun sends it straight into the pack and shuts
        /// the bench. Straight into the pack, not onto the cursor: the bench closes the
        /// inventory on the way out, and vanilla drops whatever is on the cursor when the
        /// inventory closes — the player would watch their new Megashark fall on the floor.
        /// </summary>
        private static void ClickSlot(Player player)
        {
            if (repaired && !Slot.IsAir)
            {
                // Only with an empty hand: the fixed gun is not something to swap onto.
                if (!Main.mouseItem.IsAir)
                    return;

                PADMouse.Consume();

                // InventoryUIToInventorySettings: the preset vanilla uses for a shift-click
                // out of a UI slot — pickup sound, no popup text.
                Item leftover = player.GetItem(player.whoAmI, Slot, GetItemSettings.InventoryUIToInventorySettings);

                if (leftover.IsAir)
                {
                    // Rebound, NOT TurnToAir: GetItem stores the very same instance in the
                    // inventory (GetItem_FillEmptyInventorySlot does inventory[i] = item),
                    // so clearing it here would clear the gun the player just received.
                    Slot = new Item();
                    Finish();
                    return;
                }

                // Pack full. Stays on the bench, where the player can see it.
                Slot = leftover;
                return;
            }

            if (Accepts(Main.mouseItem) || Main.mouseItem.IsAir)
            {
                PADMouse.Consume();
                Utils.Swap(ref Main.mouseItem, ref Slot);
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        private static void DrawHammer(SpriteBatch spriteBatch, Player player)
        {
            // Vanilla only draws the hammer once something is in the slot, and so does
            // this — and only for something it can act on. A hammer over a gun that is
            // already fixed invites a click that cannot do anything.
            if (!Accepts(Slot))
            {
                hoveringHammer = false;
                hammerScale = 0.8f;
                return;
            }

            int x = SlotX + HammerDX;
            int y = SlotY + HammerDY;

            bool hovering = Main.mouseX > x - HammerReach && Main.mouseX < x + HammerReach
                         && Main.mouseY > y - HammerReach && Main.mouseY < y + HammerReach
                         && PADMouse.Steering;

            // Bloom in and out at vanilla's rate rather than snapping between two sizes.
            if (hovering && hammerScale < 1f)
                hammerScale += 0.02f;
            else if (!hovering && hammerScale > 0.8f)
                hammerScale -= 0.02f;

            Texture2D hammer = TextureAssets.Reforge[hovering ? 1 : 0].Value;

            spriteBatch.Draw(hammer, new Vector2(x, y), null, Color.White, 0f,
                hammer.Size() / 2f, hammerScale, SpriteEffects.None, 0f);

            if (!hovering)
            {
                hoveringHammer = false;
                return;
            }

            Main.hoverItemName = "Straighten it out";
            player.mouseInterface = true;

            if (!hoveringHammer)
                SoundEngine.PlaySound(SoundID.MenuTick);

            hoveringHammer = true;

            if (PADMouse.Pressed)
            {
                PADMouse.Consume();
                Repair();
            }
        }

        /// <summary>
        /// The dent comes out. The bent gun is replaced in the slot by a working one, so
        /// the result appears where the player is already looking rather than in the pack.
        /// </summary>
        private static void Repair()
        {
            Item fixed_ = new Item();
            fixed_.SetDefaults(ItemID.Megashark);

            // Prefix carries over. The player may have reforged the dented one, and
            // throwing that away for repairing it would be a punishment for using the
            // feature. Stack is always 1 — the gun is not stackable.
            if (Slot.prefix > 0)
                fixed_.Prefix(Slot.prefix);

            Slot = fixed_;
            repaired = true;

            SoundEngine.PlaySound(SoundID.Item37);   // the reforge chime
            Main.LocalPlayer.PIS().AddScreenShake(1.6f, 8);
        }
    }

    /// <summary>
    /// The bench is drawn from TalkOptionSystem's layer, after the answer lines. This only
    /// covers the exit that layer cannot: a world unload with the bench open would strand
    /// the item, since Close only ever runs from Draw and Draw stops being called.
    /// </summary>
    public class MegasharkRepairSystem : ModSystem
    {
        public override void OnWorldUnload()
        {
            if (MegasharkRepairUI.Open)
                MegasharkRepairUI.Close();
        }
    }
}
