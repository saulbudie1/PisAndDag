using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// The Gills Potion in the Vast Ocean, by request ("Change gills potion: potion time is
    /// shortened with respect to depth so its full effect only lasts 30 sec in section 6(math
    /// lerp)"). The game's Gills buff (the potion's: 14400 ticks, four minutes) is counted down
    /// faster the deeper the player is, so that a whole potion lasts as long as this depth lets
    /// it: four minutes at the surface, then less, evenly with depth (its length lerped by how
    /// far it is from the surface to the top of the 6th section), to thirty seconds at the top
    /// of the 6th and under it. Eight times as fast there. By the player's middle, as the
    /// depths go (VastOcean.RowOf); over the water, as the game has it.
    ///
    /// Only the time is changed, on the player's own machine (the game counts a buff's time down
    /// only there, Player.UpdateBuffs), just after the game's own count for the tick: a buff run
    /// out is taken off by the game straight after (Player.Update). Water! Resuscitate Me!'s
    /// breathing is a buff of its own (Resuscitated) and lasts its five minutes anywhere.
    ///
    /// The potion's tooltip says so; the buff, while it is being worn down faster, how much.
    /// </summary>
    public static class VastOceanGills
    {
        /// <summary>A whole potion's length, at the surface and at the top of the 6th section, in seconds.</summary>
        public const float SurfaceSeconds = 240f;
        public const float SixthSeconds = 30f;

        /// <summary>How many times as fast the Gills buff is worn down at this row: 1 over the water, 8 at the top of the 6th section and under.</summary>
        public static float Faster(float row)
        {
            if (row < VastOcean.Surface)
                return 1f;

            float t = MathHelper.Clamp((row - VastOcean.Surface) / (VastOcean.SectionTop(6) - VastOcean.Surface), 0f, 1f);

            return SurfaceSeconds / MathHelper.Lerp(SurfaceSeconds, SixthSeconds, t);
        }
    }

    /// <summary>The Gills buff worn down faster with depth, on the player's own machine.</summary>
    public class VastOceanGillsPlayer : ModPlayer
    {
        /// <summary>Ticks owed beyond the game's own one a tick, carried over so that a fraction is not lost.</summary>
        private float owed;

        public override void PostUpdateBuffs()
        {
            if (Player.whoAmI != Main.myPlayer || SubworldKeeper.Current is not VastOcean)
            {
                owed = 0f;
                return;
            }

            int index = Player.FindBuffIndex(BuffID.Gills);

            if (index < 0)
            {
                owed = 0f;
                return;
            }

            owed += VastOceanGills.Faster(VastOcean.RowOf(Player)) - 1f;

            int more = (int)owed;

            if (more <= 0)
                return;

            owed -= more;
            Player.buffTime[index] = Math.Max(0, Player.buffTime[index] - more);
        }
    }

    /// <summary>The Gills Potion's tooltip: a line on what the depth does to it.</summary>
    public class VastOceanGillsPotion : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.GillsPotion;

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            string line = Language.GetTextValue("Mods.PisAndDag.Subworlds.Weight.GillsPotion", VastOceanWeight.FeetAt(6), (int)Math.Round(VastOceanGills.SurfaceSeconds / VastOceanGills.SixthSeconds));
            int at = tooltips.FindLastIndex(t => t.Mod == "Terraria" && t.Name.StartsWith("Tooltip"));

            tooltips.Insert(at < 0 ? tooltips.Count : at + 1, new TooltipLine(Mod, "PADDeepGills", line));
        }
    }

    /// <summary>The Gills buff's own text, while the depth wears it down faster: how many times as fast.</summary>
    public class VastOceanGillsBuff : GlobalBuff
    {
        public override void ModifyBuffText(int type, ref string buffName, ref string tip, ref int rare)
        {
            if (type != BuffID.Gills || SubworldKeeper.Current is not VastOcean)
                return;

            float faster = VastOceanGills.Faster(VastOcean.RowOf(Main.LocalPlayer));

            if (faster >= 1.05f)
                tip += "\n" + Language.GetTextValue("Mods.PisAndDag.Subworlds.Weight.GillsFaster", faster.ToString("0.0"));
        }
    }
}
