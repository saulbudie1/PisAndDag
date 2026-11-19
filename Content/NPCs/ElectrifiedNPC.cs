using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs
{
    /// <summary>
    /// The game's own Electrified debuff, made to work on enemies (by request, for The Chain: "electrified debuff is in game
    /// make it affect npcs by stun non boss by debuff time and -20HP/s"). On an enemy it takes 20 life a second; anything
    /// but a boss (or a part of one) is stunned for as long as it lasts, by the mod's stagger (PADStaggerNPC.Stagger: its
    /// AI stopped, held where it is; worms and the like it leaves alone). Its lightning crackles over it meanwhile.
    ///
    /// The debuff is the game's, sent by the game; each machine stuns it when it first sees it there, the server's word
    /// sent with the NPC.
    /// </summary>
    public class ElectrifiedNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>It was electrified last tick (this machine's).</summary>
        private bool had;

        private static bool Boss(NPC npc)
        {
            NPC whole = npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;
            return npc.boss || whole.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || NPCID.Sets.ShouldBeCountedAsBoss[whole.type];
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!npc.HasBuff(BuffID.Electrified))
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= 40;

            if (damage < 5)
                damage = 5;
        }

        public override void PostAI(NPC npc)
        {
            int at = npc.FindBuffIndex(BuffID.Electrified);
            bool now = at >= 0;

            if (now && !had && !Boss(npc))
                npc.GetGlobalNPC<PADStaggerNPC>().Stagger(npc, npc.buffTime[at]);

            had = now;

            if (!now || Main.dedServ)
                return;

            if (Main.rand.NextBool(5))
            {
                Vector2 from = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                Lightning.Spawn(from, from + Main.rand.NextVector2Circular(22f, 22f), DestroyerArt.Teal, 3.5f, 8, 0);
            }

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, 0.7f);
                dust.noGravity = true;
                dust.velocity *= 0.6f;
            }

            Lighting.AddLight(npc.Center, 0.2f, 0.4f, 0.4f);
        }
    }
}
