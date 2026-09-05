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
        /// How long the world reacts to the orb breaking: 3 seconds. The shake and the
        /// screen filter share this so they cannot drift apart.
        /// </summary>
        private const int StirDurationTicks = 180;

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

        /// <summary>
        /// Ambient glow. Runs each tick for every copy of this tile near the player.
        /// </summary>
        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (Main.dedServ)
                return;

            // Tile coordinates to the sprite's world centre. The +2 matches DrawYOffset,
            // so the motes leave from where the orb actually appears.
            Vector2 anchor = new Vector2(i * 16 + 8, j * 16 + 8 + 2);

            Lighting.AddLight(anchor, new Vector3(0.55f, 0.18f, 0.52f));

            // This hook fires every tick, so without a gate one orb would emit sixty
            // motes a second. Roughly three a second instead.
            if (Main.rand.NextBool(18))
                OrbLight.Spawn(anchor);
        }

        /// <summary>
        /// Breaking the orb is what switches the world into Legends mode, permanently.
        /// </summary>
        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            // Belt and braces alongside GetItemDrops.
            noItem = true;

            // KillTile also fires for hits that don't break the tile and for effect-only
            // calls, so only the one that actually removes it should do anything.
            if (fail || effectOnly)
                return;

            // In a world that is already Legends, the tile still breaks and still drops
            // nothing — it simply does nothing else. No second announcement, no second
            // tremor, and the flag is already set so there is nothing to change.
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
                // PisPlayer holds the shake at full strength for the whole duration and
                // only then decays it, so this reads as a sustained tremor, not a jolt.
                Main.LocalPlayer.PIS().AddScreenShake(12f, StirDurationTicks);

                // Same duration, so the screen finishes bleeding red exactly as the
                // ground stops moving.
                LegendsScreenSystem.Trigger(StirDurationTicks);
            }
        }
    }
}
