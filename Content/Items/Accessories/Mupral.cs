using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace PisAndDag.Content.Items.Accessories
{
    /// <summary>
    /// M.U.P.R.A.L, the Mechanic's Ultra Pressure Resistance Anchored Lantern, by request:
    /// "Add an accessory item: M.U.P.R.A.L (Mechanic's Ultra Pressure Resistence Anchored Lantern)
    /// 16 Use: Anchored on the shoulder mode 1:shines a bit of radial light with radius of 6.1 in
    /// air and 3.7 in water, dim and affected by tiles,( tiles in front will hinder scan rays),
    /// mode2: radial dims to radius of 1.2 and concentrate into a flash light that shines to the
    /// cursor, max distance of 13 tiles in water and 23 tiles in air. the color is a beige. flash
    /// light angle is 20 degrees Use light physics to make it realistic. Whatever shown For now its
    /// the only actual light source in the vast ocean. (remember to dim it the more it goes out)
    /// Make it pretty"; then, asked what the 16 was, "Mechanic sells it for 20 gold", and asked how
    /// its two modes are switched, "Both": a key, and a right click on it; and later, "change the
    /// viewing beam distance to 15 tiles normal and 11 tiles under water, make the rays super fine
    /// and dont light it block by block, any thing passed by the ray is lit up, the solid blocks
    /// will be lit up too but only the part within the path of the ray. Change beam angle to 30
    /// degrees".
    ///
    /// Worn, a small lamp sits on its wearer's front shoulder on a strap (MupralLayer), and lights:
    ///   - GLOW (the first): a dim light all about, to 6.1 tiles through air and 3.7 through water;
    ///   - BEAM (the second): the light about down to 1.2 tiles, and a beam of 30 degrees from the
    ///     lamp toward the cursor, the lamp turning on its strap to point it, 50 tiles through
    ///     water and 68 through air wherever the cursor is (by request: "Remake the light beam
    ///     mupral to be max 50 in water. (it is like that every where the ray wont reduce if i
    ///     bring my mouse in)"; air as much further as it always was, 15 to 11).
    /// The HEATR carries a lamp of the same kind, both its lights at once and wider (MupralKit.Suit,
    /// MupralLight); worn, it is the one that shines, and the key does nothing.
    /// Either way the light goes out from the lamp in fine straight lines, lights whatever they
    /// pass, to the pixel, goes a tile into the first block in its way (the part of the block in its
    /// path lit, dimming as it goes in) and no further, so what is behind a block is in its shadow,
    /// and is the dimmer the further it has gone, to nothing at those distances; how it is worked
    /// out is MupralLight's. It is light as the game has its own (it lights the blocks, walls,
    /// water, creatures and players it falls on, and the map as it shows them), but it is not
    /// spread from tile to tile as the game spreads its lights, so in the Vast Ocean's depths,
    /// where the game's lights light only the tile they are in (VastOceanLight), it is the one
    /// light that lights anything.
    ///
    /// Sold by the Mechanic, 20 gold before her mood moves it as it moves all her prices
    /// (<see cref="MupralShop"/>). The mode is the wearer's and is kept with the player; switched,
    /// while one is worn, by the key (Controls: "Switch the M.U.P.R.A.L's light", L to begin with),
    /// or a right click on the lantern in the accessory slot it is worn in (<see cref="MupralSlot"/>)
    /// or on another in the inventory (one not worn is put on by a right click, as the game does);
    /// a click is heard. Mine: Orange rarity; its pictures are mine (PlaceholderTextures.md), the
    /// item's drawn again in 2x2 pixels by the sprite model (32 by 32).
    /// </summary>
    public class Mupral : ModItem
    {
        /// <summary>Beside the mod's other accessories in the localization file (Items.Accessories).</summary>
        public override string LocalizationCategory => "Items.Accessories";

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.accessory = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.buyPrice(gold: 20);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            MupralPlayer lamp = player.GetModPlayer<MupralPlayer>();
            lamp.Worn = true;
            lamp.WornShown |= !hideVisual;
        }

        /// <summary>
        /// A right click on it in the inventory switches its light while one is worn, and it stays
        /// (ConsumeItem); while none is, the game has the click, and puts it on (ItemSlot.RightClick:
        /// SwapEquip, for an item that does not stack, when this says no).
        /// </summary>
        public override bool CanRightClick() => Main.LocalPlayer.GetModPlayer<MupralPlayer>() is { Lit: true, Suit: false };

        public override bool ConsumeItem(Player player) => false;

        public override void RightClick(Player player) => player.GetModPlayer<MupralPlayer>().Switch();

        /// <summary>Which light it is giving now, and the key that switches it, as bound.</summary>
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            Player player = Main.LocalPlayer;

            if (player == null)
                return;

            bool beam = player.GetModPlayer<MupralPlayer>().Beam;
            List<string> keys = PADKeybinds.Lantern?.GetAssignedKeys(InputMode.Keyboard);
            string key = keys != null && keys.Count > 0 ? keys[0] : Language.GetTextValue("Mods.PisAndDag.Items.Accessories.Mupral.NoKey");
            int at = tooltips.FindLastIndex(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));

            TooltipLine now = new TooltipLine(Mod, "MupralMode", Language.GetTextValue(beam ? "Mods.PisAndDag.Items.Accessories.Mupral.NowBeam" : "Mods.PisAndDag.Items.Accessories.Mupral.NowGlow", key))
            {
                OverrideColor = new Color(232, 214, 176),
            };

            if (at < 0)
                tooltips.Add(now);
            else
                tooltips.Insert(at, now);
        }
    }

    /// <summary>The Mechanic sells it, for what it is worth (Item.value: 20 gold), as a shop prices anything.</summary>
    public class MupralShop : GlobalNPC
    {
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Mechanic)
                shop.Add(ModContent.ItemType<Mupral>());
        }
    }

    /// <summary>
    /// A right click on the lantern in the accessory slot it is worn in switches its light, as in
    /// the inventory (the game would pick it up into the hand instead: ItemSlot.RightClick, for
    /// its accessory slots). Everything else in every slot is left to the game.
    /// </summary>
    public class MupralSlot : ModSystem
    {
        public override void Load()
        {
            On_ItemSlot.RightClick_ItemArray_int_int += RightClicked;
        }

        private static void RightClicked(On_ItemSlot.orig_RightClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
        {
            if (Math.Abs(context) == ItemSlot.Context.EquipAccessory && inv != null && slot >= 0 && slot < inv.Length
                && inv[slot]?.ModItem is Mupral && Main.mouseRight && Main.mouseRightRelease && Main.LocalPlayer.itemAnimation == 0
                && !Main.LocalPlayer.GetModPlayer<MupralPlayer>().Suit)
            {
                Main.mouseRightRelease = false;
                Main.LocalPlayer.GetModPlayer<MupralPlayer>().Switch();
                return;
            }

            orig(inv, context, slot);
        }
    }

    /// <summary>The lights a lamp gives: the glow; the beam with a little glow about; the HEATR's wide glow and wide beam at once.</summary>
    public enum MupralKit : byte
    {
        Glow,
        Beam,
        Suit,
    }

    /// <summary>
    /// The lantern on a player: worn or not this tick, shown or not, which light it gives (kept with
    /// the player), and where its beam points: the cursor, for the player whose machine it is; for
    /// the others, where their machines last said, turned to smoothly (a <see cref="PADPacket.Lantern"/>
    /// packet, from each player's own machine to the server and on to every other: when the light
    /// is switched, and while the beam is on, when it has turned two degrees from what was last
    /// said, at most every third tick). The HEATR's lamp is this one too (<see cref="Suit"/>).
    /// </summary>
    public class MupralPlayer : ModPlayer
    {
        /// <summary>Worn this tick (Mupral.UpdateAccessory), and its picture shown (not hidden in its slot).</summary>
        public bool Worn;
        public bool WornShown;

        /// <summary>The HEATR worn this tick, and its lamp shown (the HEATR's UpdateAccessory).</summary>
        public bool SuitWorn;
        public bool SuitShown;

        /// <summary>Worn as of when everything worn last had its say (PostUpdateEquips): what lights and draws.</summary>
        public bool Lit { get; private set; }
        public bool Shown { get; private set; }

        /// <summary>The HEATR's lamp is the one lit: both its lights at once, and no switching.</summary>
        public bool Suit { get; private set; }

        /// <summary>The second light, the beam; the first, the glow, when not.</summary>
        public bool Beam;

        /// <summary>The lights it gives now.</summary>
        public MupralKit Kit => Suit ? MupralKit.Suit : Beam ? MupralKit.Beam : MupralKit.Glow;

        /// <summary>A beam is on: the lamp points it where the cursor is.</summary>
        public bool Aiming => Suit || Beam;

        /// <summary>Where the beam points, as an angle in the world (0 to the right, down positive), and where the others were last told it pointed.</summary>
        public float Aim;
        private float heardAim;
        private float toldAim;
        private uint toldAt;

        public override void ResetEffects()
        {
            Worn = false;
            WornShown = false;
            SuitWorn = false;
            SuitShown = false;
        }

        public override void PostUpdateEquips()
        {
            Lit = Worn || SuitWorn;
            Shown = WornShown || SuitShown;
            Suit = SuitWorn;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (PADKeybinds.Lantern != null && PADKeybinds.Lantern.JustPressed && Lit && !Suit)
                Switch();
        }

        /// <summary>The other light: on this machine (the key, a right click), heard, and told to the others.</summary>
        public void Switch()
        {
            Beam = !Beam;

            if (!Main.dedServ)
                SoundEngine.PlaySound(SoundID.Mech with { Pitch = Beam ? 0.2f : -0.15f, Volume = 0.8f }, Player.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
                Send(-1, Player.whoAmI);
        }

        public override void PostUpdate()
        {
            if (Main.dedServ)
                return;

            if (Player.whoAmI == Main.myPlayer)
            {
                Aim = (Main.MouseWorld - MupralLight.Pivot(Player)).ToRotation();

                // While a beam is on: the others told where it points as it turns.
                if (Main.netMode == NetmodeID.MultiplayerClient && Lit && Aiming && Math.Abs(MathHelper.WrapAngle(Aim - toldAim)) > 0.035f
                    && Main.GameUpdateCount - toldAt >= 3)
                {
                    Send(-1, Player.whoAmI);
                }
            }
            else
            {
                Aim = Utils.AngleLerp(Aim, heardAim, 0.35f);
            }
        }

        // ---- saving and the network ---------------------------------------------------------

        public override void SaveData(TagCompound tag)
        {
            if (Beam)
                tag["beam"] = true;
        }

        public override void LoadData(TagCompound tag)
        {
            Beam = tag.ContainsKey("beam") && tag.GetBool("beam");
        }

        /// <summary>On joining: told as it is.</summary>
        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer)
        {
            Send(toWho, fromWho);
        }

        private void Send(int toWho, int fromWho)
        {
            toldAim = Aim;
            toldAt = Main.GameUpdateCount;

            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)PADPacket.Lantern);
            packet.Write((byte)Player.whoAmI);
            packet.Write(Beam);
            packet.Write(Aim);
            packet.Send(toWho, fromWho);
        }

        /// <summary>
        /// A player's lantern: which light, and where it points. To the server from that player's
        /// own machine (it speaks for nobody else), and on from there to every other.
        /// </summary>
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            int who = reader.ReadByte();
            bool beam = reader.ReadBoolean();
            float aim = reader.ReadSingle();

            if (Main.netMode == NetmodeID.Server)
                who = whoAmI;

            if (who < 0 || who >= Main.maxPlayers || float.IsNaN(aim))
                return;

            MupralPlayer lamp = Main.player[who].GetModPlayer<MupralPlayer>();
            lamp.Beam = beam;
            lamp.heardAim = aim;

            if (Main.netMode == NetmodeID.Server)
            {
                lamp.Aim = aim;
                lamp.Send(-1, who);
            }
        }
    }
}
