using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Chat;             // ChatHelper
using Terraria.DataStructures;   // AnchorData
using Terraria.Enums;            // AnchorType
using Terraria.ID;
using Terraria.Localization;     // NetworkText
using Terraria.ModLoader;
using Terraria.ObjectData;       // TileObjectData

namespace PisAndDag.Content.Tiles
{
    /// <summary>
    /// A 1x1 decorative orb that may only be set down on top of an anvil.
    /// Walk-through like a vase, hammer-only to remove, and drops nothing when broken.
    /// </summary>
    public class Game_Changing_Orb : ModTile
    {
        /// <summary>
        /// The only tiles the orb will anchor to. Add modded anvil tile types here if you
        /// ever want it to sit on those too.
        /// </summary>
        private static readonly int[] ValidAnvils = { TileID.Anvils, TileID.MythrilAnvil };

        /// <summary>
        /// How long the shake holds at full when the orb breaks, before it dies away: the
        /// impact only. The rest of the stir is LegendsScreenSystem's scene, whose camera
        /// takes over from the shake as it rises.
        /// </summary>
        public const int ImpactShakeTicks = 40;

        public override void SetStaticDefaults()
        {
            // Furniture-style tile: carries its own placement frame, nothing anchors to it,
            // and it is never solid — so you walk straight through it, exactly like a
            // placed bottle or vase. Non-solid is what makes it "background" and stops it
            // interfering with movement; there is no separate flag for that.
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;

            // Hammer instead of pickaxe. Note that tileCut is deliberately left false —
            // that is the flag that lets a sword swing shatter a vase, and you asked for
            // hammer only.
            Main.tileHammer[Type] = true;

            TileID.Sets.DisableSmartCursor[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

            // AnchorType.AlternateTile paired with AnchorAlternateTiles is the mechanism
            // for "may only rest on these specific tile IDs". The usual SolidTile or Table
            // anchors would never work here: an anvil is neither solid nor a table, so the
            // orb could not be placed on one at all.
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.AlternateTile, 1, 0);
            TileObjectData.newTile.AnchorAlternateTiles = ValidAnvils;

            TileObjectData.newTile.LavaDeath = false;

            // Nudges the sprite down so it sits on the anvil's surface rather than
            // floating a pixel above it.
            TileObjectData.newTile.DrawYOffset = 2;

            TileObjectData.addTile(Type);

            DustType = DustID.PurpleCrystalShard;
            HitSound = SoundID.Shatter;

            AddMapEntry(new Color(96, 44, 92), CreateMapEntryName());
        }

        /// <summary>
        /// Drops nothing, on purpose. Placing consumes the orb and breaking destroys it,
        /// so the decision to place one is permanent.
        /// </summary>
        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }
         
        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (Main.dedServ)
                return;
             
            Vector2 anchor = new Vector2(i * 16 + 8, j * 16 + 8 + 2);

            Lighting.AddLight(anchor, new Vector3(0.55f, 0.18f, 0.52f));
             
            if (Main.rand.NextBool(18))
                OrbLight.Spawn(anchor);
        }
         
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        { 
            noItem = true;
             
            if (fail || effectOnly)
                return;
             
            if (PADWorld.Legends)
                return;

            PADWorld.Legends = true;

            // Push the changed world flag to clients.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            AnnounceLegends();
        }

        private static void AnnounceLegends()
        {
            string message = "The essence of " + Main.worldName + " have been stirred";
            Color color = new Color(224, 18, 218);

            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(message, color);
            else if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), color);

            if (Main.netMode != NetmodeID.Server)
            { 
                Main.LocalPlayer.PIS().AddScreenShake(12f, ImpactShakeTicks);
                 
                LegendsScreenSystem.Trigger();
            }
        }
    }
}
