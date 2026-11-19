using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Items.Melee
{
    /// <summary>
    /// A player's Nightcall (Nightcall): what each hand is (by request: "default is vice on both"), kept with the player
    /// and told to every machine (the fists are drawn from it there); the menu's key (the reload key, by request: "the
    /// same button as reload button"); and, for its charge (NightcallCharge), the player planted, and then thrown.
    /// </summary>
    public class NightcallPlayer : ModPlayer
    {
        public NightcallFist Left = NightcallFist.Vice, Right = NightcallFist.Vice;

        /// <summary>The hand a step of its combo is: 0 the left, 1 the right.</summary>
        public NightcallFist Of(int step) => step == 0 ? Left : Right;

        /// <summary>A hand set from the menu (on its player's own machine; the others are told through SendClientChanges).</summary>
        public void Set(int hand, NightcallFist fist)
        {
            if (hand == 0)
                Left = fist;
            else
                Right = fist;
        }

        /// <summary>Ticks left of being planted: the charge sets it every tick it lasts.</summary>
        public int Planted;

        /// <summary>The throw: this fast, this way, for the tick to come (the charge sets it every tick it lasts).</summary>
        private Vector2 thrust;
        private int thrustFor;
        private bool thrustBegins;

        public void Thrust(Vector2 velocity, bool begins)
        {
            thrust = velocity;
            thrustFor = 1;
            thrustBegins |= begins;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (PADKeybinds.Reload != null && PADKeybinds.Reload.JustPressed && Player.HeldItem?.ModItem is Nightcall)
                NightcallMenu.Toggle();
        }

        /// <summary>Planted: it cannot walk or jump.</summary>
        public override void SetControls()
        {
            if (Planted <= 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
            Player.controlDown = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void PreUpdateMovement()
        {
            if (Planted > 0)
            {
                // Held where it stands (in the air too: it hangs there while it charges).
                Player.velocity = Vector2.Zero;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            if (thrustFor <= 0)
                return;

            thrustFor--;

            // As the game does for a grapple, on the machine whose player it is: off what is ridden, and off every hook.
            if (thrustBegins && Player.whoAmI == Main.myPlayer)
            {
                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
            }

            thrustBegins = false;

            // No fall is counted in a throw.
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.velocity = thrust;
            Player.pulley = false;
            Player.armorEffectDrawShadow = true;

            if (Player.velocity.Y > 0f)
                Player.GoingDownWithGrapple = true;
        }

        public override void PostUpdate()
        {
            if (Planted > 0)
                Planted--;
        }

        public override void UpdateDead()
        {
            Planted = 0;
            thrustFor = 0;
            thrustBegins = false;
        }

        // ---- kept, and told -------------------------------------------------------------------------------------------------

        public override void SaveData(TagCompound tag)
        {
            tag["nightcallLeft"] = (byte)Left;
            tag["nightcallRight"] = (byte)Right;
        }

        public override void LoadData(TagCompound tag)
        {
            Left = Read(tag, "nightcallLeft");
            Right = Read(tag, "nightcallRight");
        }

        private static NightcallFist Read(TagCompound tag, string key)
        {
            if (!tag.ContainsKey(key))
                return NightcallFist.Vice;

            int value = tag.GetByte(key);
            return value <= (int)NightcallFist.Laser ? (NightcallFist)value : NightcallFist.Vice;
        }

        public override void CopyClientState(ModPlayer targetCopy)
        {
            NightcallPlayer copy = (NightcallPlayer)targetCopy;
            copy.Left = Left;
            copy.Right = Right;
        }

        public override void SendClientChanges(ModPlayer clientPlayer)
        {
            NightcallPlayer was = (NightcallPlayer)clientPlayer;

            if (was.Left != Left || was.Right != Right)
                Send(-1, Player.whoAmI);
        }

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) => Send(toWho, fromWho);

        private void Send(int toWho, int fromWho)
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)PADPacket.Nightcall);
            packet.Write((byte)Player.whoAmI);
            packet.Write((byte)Left);
            packet.Write((byte)Right);
            packet.Send(toWho, fromWho);
        }

        /// <summary>A player's two hands: to the server from that player's own machine (it speaks for nobody else), and on from there to every other.</summary>
        public static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            int who = reader.ReadByte();
            int left = reader.ReadByte(), right = reader.ReadByte();

            if (Main.netMode == NetmodeID.Server)
                who = whoAmI;

            if (who < 0 || who >= Main.maxPlayers)
                return;

            NightcallPlayer hands = Main.player[who].GetModPlayer<NightcallPlayer>();
            hands.Left = left <= (int)NightcallFist.Laser ? (NightcallFist)left : NightcallFist.Vice;
            hands.Right = right <= (int)NightcallFist.Laser ? (NightcallFist)right : NightcallFist.Vice;

            if (Main.netMode == NetmodeID.Server)
                hands.Send(-1, who);
        }
    }
}
