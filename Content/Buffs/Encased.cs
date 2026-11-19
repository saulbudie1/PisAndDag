using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Encased in slime, by Slimy Fortification (SlimeCasing has what it does): five seconds. An NPC's; given by the
    /// spell's caster when enough slime is on it, and told to everyone as any buff on an NPC is. Placeholder picture
    /// (PlaceholderTextures.md), drawn in code.
    /// </summary>
    public class Encased : ModBuff
    {
        /// <summary>Five seconds.</summary>
        public const int Ticks = 5 * 60;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
    }
}
