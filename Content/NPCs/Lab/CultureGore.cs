using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Lab
{
    /// <summary>
    /// What a Culture comes apart into (by request: "gore that dissolves itself thats it"): a lump of it, a length of
    /// strand, an eye, its teeth. Each lies where it falls a second or so, then goes: fading, shrinking, bleeding out as
    /// it does, until nothing is left of it. Not kept as the game keeps its other gore.
    /// </summary>
    public abstract class CultureGore : ModGore
    {
        /// <summary>Its picture: CultureGore1 to 4, beside the Culture's own.</summary>
        protected abstract int Piece { get; }

        public override string Texture => "PisAndDag/Content/NPCs/Lab/CultureGore" + Piece;

        public override void SetStaticDefaults()
        {
            GoreID.Sets.DisappearSpeed[Type] = 1;
            GoreID.Sets.DisappearSpeedAlpha[Type] = 3;
        }

        /// <summary>Its time before it starts to go, short: a second, or a little more.</summary>
        public override void OnSpawn(Gore gore, IEntitySource source)
        {
            gore.timeLeft = Main.rand.Next(50, 100);
        }

        /// <summary>Going: smaller and smaller, and its blood out of it.</summary>
        public override bool Update(Gore gore)
        {
            if (gore.alpha > 0)
            {
                gore.scale *= 0.988f;

                if (Main.rand.NextBool(6))
                {
                    Dust drop = Dust.NewDustDirect(gore.position, 12, 8, DustID.Blood, 0f, 0.5f);
                    drop.velocity.X *= 0.2f;
                }
            }

            return true;
        }
    }

    public class CultureLump : CultureGore
    {
        protected override int Piece => 1;
    }

    public class CultureStrand : CultureGore
    {
        protected override int Piece => 2;
    }

    public class CultureEyeGore : CultureGore
    {
        protected override int Piece => 3;
    }

    public class CultureTeeth : CultureGore
    {
        protected override int Piece => 4;
    }
}
