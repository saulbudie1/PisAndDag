using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// Chilled on creatures. Vanilla puts Chilled only on players and has no slowing debuff for
    /// NPCs at all, so this teaches Chilled to slow them. Cold Call puts it on.
    ///
    /// The slow is Calamity's (CalamityGlobalNPC.PostAI): once the creature has moved itself,
    /// its velocity is divided by one plus <see cref="Slow"/>, every tick. Bosses are left alone,
    /// as Calamity leaves them, and so is anything vanilla counts as part of one.
    /// </summary>
    public class ChilledNPCs : GlobalNPC
    {
        /// <summary>Calamity's figure for its lighter slows (Webbed, Time Distortion).</summary>
        public const float Slow = 0.15f;

        /// <summary>What a chilled creature is drawn through: cooler, never brighter.</summary>
        private static readonly Color Tint = new Color(170, 215, 255);

        private static bool Chilled(NPC npc) =>
            !npc.boss && !NPCID.Sets.ShouldBeCountedAsBoss[npc.type] && npc.HasBuff(BuffID.Chilled);

        public override void PostAI(NPC npc)
        {
            if (!Chilled(npc))
                return;

            npc.velocity *= 1f / (1f + Slow);

            if (!Main.dedServ && Main.rand.NextBool(6))
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Frost, 0f, 0f, 0, default,
                    Main.rand.NextFloat(0.7f, 1f));
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Chilled(npc))
                drawColor = drawColor.MultiplyRGB(Tint);
        }
    }
}
