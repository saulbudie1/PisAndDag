using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.NPCs.Abomanation_Small;
using PisAndDag.Content.NPCs.MegaShark;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Boss Checklist. If it is loaded, this mod's bosses go in its log. Nothing here runs
    /// without it, and the mod neither requires nor references it: Mod.Call through
    /// TryGetMod is the whole surface, which is what Boss Checklist asks for.
    ///
    /// An entry costs a call. NPC.boss on its own gets a boss nothing — there is no
    /// detection, and a boss missing from this file is simply missing from the log.
    ///
    /// Progression is Boss Checklist's own scale (1.4.4 branch, BossTracker): Queen Bee 4,
    /// Skeletron 5, Deerclops 6, Wall of Flesh 7, The Twins 9, The Destroyer 10, Skeletron
    /// Prime 11, Plantera 12. Half past a number puts an entry after it.
    ///
    /// Must be sent before AddRecipes; PostSetupContent is where Boss Checklist expects
    /// it. The downed flag is PADWorld.DownedMegashark, set by PADNPCs.OnKill.
    /// </summary>
    public class BossLogSupport : ModSystem
    {
        private const float AfterSkeletron = 5.5f;
        private const float AfterTheMechs = 11.5f;

        public override void PostSetupContent()
        {
            if (!ModLoader.TryGetMod("BossChecklist", out Mod log))
                return;

            log.Call(
                "LogBoss",
                Mod,
                "Megashark",
                AfterTheMechs,
                (Func<bool>)(() => PADWorld.DownedMegashark),
                ModContent.NPCType<Megashark>(),
                new Dictionary<string, object>
                {
                    ["spawnInfo"] = Language.GetOrRegister("Mods.PisAndDag.BossLog.Megashark.SpawnInfo"),
                    ["collectibles"] = new List<int> { ModContent.ItemType<DentedMegashark>() },
                });

            // Met before hardmode, on the Wasted Mile, after Skeletron and before the Wall.
            // No collectibles yet. The "spawn items" are the animals it is called with:
            // let go on that ground, which the spawn line says and AbomanationSummon does.
            log.Call(
                "LogBoss",
                Mod,
                "Abomanation",
                AfterSkeletron,
                (Func<bool>)(() => PADWorld.DownedAbomanation),
                ModContent.NPCType<AbomanationHead>(),
                new Dictionary<string, object>
                {
                    ["spawnInfo"] = Language.GetOrRegister("Mods.PisAndDag.BossLog.Abomanation.SpawnInfo"),
                    ["spawnItems"] = new List<int>(AbomanationSummon.Offerings),
                    ["customPortrait"] = (Action<SpriteBatch, Rectangle, Color>)DrawAbomanation,
                });
        }

        /// <summary>
        /// The picture on the Abomanation's page: the whole animal coiled, rather than
        /// the head Boss Checklist would draw on its own. The same "customPortrait"
        /// hand-off Calamity uses; the texture is drawn once, centred in the space the
        /// log gives it, in the colour it asks for.
        /// </summary>
        private static void DrawAbomanation(SpriteBatch spriteBatch, Rectangle area, Color colour)
        {
            Texture2D texture = ModContent.Request<Texture2D>(
                "PisAndDag/Content/NPCs/Abomanation_Small/Abomanation_BossChecklist", AssetRequestMode.ImmediateLoad).Value;

            Vector2 at = new Vector2(area.Center.X - texture.Width / 2, area.Center.Y - texture.Height / 2);

            spriteBatch.Draw(texture, at, colour);
        }
    }
}
