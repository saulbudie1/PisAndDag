using System;
using System.Collections.Generic;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.NPCs.MegaShark;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag
{
    /// <summary>
    /// Boss Checklist. If it is loaded, the Megashark goes in its log, placed after the
    /// three mechanical bosses and before Plantera. Nothing here runs without it, and
    /// the mod neither requires nor references it: Mod.Call through TryGetMod is the
    /// whole surface, which is what Boss Checklist asks for.
    ///
    /// Progression is Boss Checklist's own scale (1.4.4 branch, BossTracker): The Twins
    /// 9, The Destroyer 10, Skeletron Prime 11, Plantera 12. Half past Prime puts it
    /// after whichever of the three was last.
    ///
    /// Must be sent before AddRecipes; PostSetupContent is where Boss Checklist expects
    /// it. The downed flag is PADWorld.DownedMegashark, set by PADNPCs.OnKill.
    /// </summary>
    public class BossLogSupport : ModSystem
    {
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
        }
    }
}
