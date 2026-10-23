using System.Text;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Defaced: what the Defacer leaves on what it hits lifted all the way, for <see cref="Ticks"/>.
    /// Every hit it takes ignores <see cref="DefenseLost"/> of its defense, grit falls off it now and
    /// then, and its name under the cursor is scratched out (DefacedNPC). Lore: §XVII, "chipped faces
    /// on angel statues, scratched-out names on shrines".
    /// </summary>
    public class Defaced : ModBuff
    {
        public const int Ticks = 8 * 60;
        public const int DefenseLost = 10;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            if (Main.dedServ || !Main.rand.NextBool(18))
                return;

            Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height / 2, DustID.Stone, 0f, 0.5f, 0, default, 0.7f);
            dust.velocity.X *= 0.3f;
        }
    }

    /// <summary>
    /// What being Defaced does: the defense ignored, on every hit from anything, and the name
    /// scratched. The scratching is the same every time it is looked at (it is rolled from where the
    /// enemy sits in the world's list), so it does not flicker; about two letters in three go.
    /// </summary>
    public class DefacedNPC : GlobalNPC
    {
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.HasBuff(ModContent.BuffType<Defaced>()))
                modifiers.ArmorPenetration += Defaced.DefenseLost;
        }

        public override void ModifyTypeName(NPC npc, ref string typeName)
        {
            if (npc.active && npc.HasBuff(ModContent.BuffType<Defaced>()))
                typeName = Scratched(typeName, npc.whoAmI);
        }

        /// <summary>A name with most of its letters scratched through.</summary>
        public static string Scratched(string name, int seed)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            StringBuilder scratched = new StringBuilder(name.Length);

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];

                if (char.IsWhiteSpace(c))
                {
                    scratched.Append(c);
                    continue;
                }

                int roll = ((seed + 7) * 73856093 ^ (i + 3) * 19349663) & 0x7fffffff;
                scratched.Append(roll % 100 < 66 ? (roll % 3 == 0 ? '-' : '/') : c);
            }

            return scratched.ToString();
        }
    }
}
