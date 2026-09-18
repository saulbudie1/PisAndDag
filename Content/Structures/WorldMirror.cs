using System;
using System.IO;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Which way round the world is.
    ///
    /// Everything this mod places has a side: the ocean is at one end and the Kingdom of
    /// Grace is at the other, with the snow, the jungle and the siege works in a fixed
    /// order between them. A layout that good is also a layout you have seen once.
    ///
    /// So the whole plan is written in one direction and then a coin is tossed. In half
    /// of all worlds the ocean is on the left and Grace is on the right; in the other
    /// half it is the other way round, and every position the mod chooses runs through
    /// <see cref="X"/> on the way out.
    ///
    /// The flip is rolled once, before the first generation pass, and then saved with
    /// the world — a great deal of code asks which side things are on long after
    /// generation is over.
    /// </summary>
    public class WorldMirror : ModSystem
    {
        /// <summary>True when the ocean is on the right and Grace is on the left.</summary>
        public static bool Flipped;

        /// <summary>+1 when the world reads left to right, -1 when it reads the other way.</summary>
        public static int Dir => Flipped ? -1 : 1;

        /// <summary>
        /// A position in the plan, in the world. Logical 0 is the ocean end and logical
        /// maxTilesX is the Grace end, whichever way round the world came out.
        /// </summary>
        public static int X(int logical)
        {
            int x = Flipped ? Main.maxTilesX - logical : logical;
            return Math.Clamp(x, 0, Main.maxTilesX - 1);
        }

        /// <summary>A span in the plan, in the world, with the ends put back in order.</summary>
        public static (int left, int right) Span(int from, int to)
        {
            int a = X(from), b = X(to);
            return a <= b ? (a, b) : (b, a);
        }

        /// <summary>The ocean end of the world, as a world coordinate.</summary>
        public static int OceanSide => Flipped ? Main.maxTilesX : 0;

        /// <summary>The Grace end of the world, as a world coordinate.</summary>
        public static int GraceSide => Flipped ? 0 : Main.maxTilesX;

        /// <summary>Is this world position on the half that has no ocean.</summary>
        public static bool OnGraceSide(int x) => Flipped ? x < Main.maxTilesX / 2 : x >= Main.maxTilesX / 2;

        public override void ClearWorld() => Flipped = false;

        /// <summary>
        /// Rolled here because PreWorldGen runs after the beach constants are set and
        /// before the first pass reads them, which is the same seam the layout uses.
        /// </summary>
        public override void PreWorldGen()
        {
            if (!ExtraLargeWorld.Active)
            {
                Flipped = false;
                return;
            }

            Flipped = WorldGen.genRand.NextBool();
            Mod.Logger.Info($"Extra Large world: ocean on the {(Flipped ? "right" : "left")}, Grace on the {(Flipped ? "left" : "right")}.");
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (Flipped)
                tag["PADMirrored"] = true;
        }

        public override void LoadWorldData(TagCompound tag) => Flipped = tag.ContainsKey("PADMirrored");

        public override void NetSend(BinaryWriter writer) => writer.Write(Flipped);

        public override void NetReceive(BinaryReader reader) => Flipped = reader.ReadBoolean();
    }
}
