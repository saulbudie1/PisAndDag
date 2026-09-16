using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Whether this player is standing in the graveyard. The world's, not a
    /// graveyard: seven tombstones anywhere make vanilla's biome, and that is
    /// nothing to anyone. This is the plot the pass dug and the world buried
    /// you in (GraveyardZone), and things that happen there happen only there.
    ///
    /// Read <c>player.GetModPlayer&lt;GraveyardPlayer&gt;().Inside</c>. Entered is
    /// true for the one tick the player crosses in, for anything that wants a
    /// first-arrival moment later.
    /// </summary>
    public class GraveyardPlayer : ModPlayer
    {
        public bool Inside;

        public bool Entered;

        public override void PostUpdate()
        {
            bool now = GraveyardZone.Contains(Player);

            Entered = now && !Inside;
            Inside = now;
        }
    }
}
