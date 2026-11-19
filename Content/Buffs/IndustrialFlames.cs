using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Industrial Flames, off the Legends Twins' fire (by request: "flaming/fire attacks does Industrial Flames dealing 30
    /// HP/s for 3 secs"): thirty life a second while it lasts. Its picture drawn in code (scratchpad twins/art/icons.py).
    /// On enemies too, off Violent Crimes' fire (the Twins' own weapon): the same thirty a second (TwinsBurnNPC).
    /// </summary>
    public class IndustrialFlames : ModBuff
    {
        /// <summary>How long a hit gives it (by request: three seconds).</summary>
        public const int Ticks = 180;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) => player.GetModPlayer<TwinsBurnPlayer>().Flames = true;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<TwinsBurnNPC>().Flames = true;
    }

    /// <summary>
    /// Chlorine Poisoning, off the Legends Spazmatism's gas and spears (by request: "Chlorine Poisoning for 3 secs -30
    /// hp/s"): thirty life a second while it lasts. Its picture drawn in code. On enemies too, off the chlorine Violent
    /// Crimes leaves behind it (TwinsBurnNPC).
    /// </summary>
    public class ChlorinePoisoning : ModBuff
    {
        public const int Ticks = 180;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) => player.GetModPlayer<TwinsBurnPlayer>().Chlorine = true;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<TwinsBurnNPC>().Chlorine = true;
    }

    /// <summary>What the two do: thirty life a second each, no healing meanwhile; fire on the one, green gas off the other.</summary>
    public class TwinsBurnPlayer : ModPlayer
    {
        /// <summary>Life regeneration is counted in halves of a life a second.</summary>
        private const int ThirtyASecond = 60;

        public bool Flames, Chlorine;

        public override void ResetEffects() => Flames = Chlorine = false;

        public override void UpdateBadLifeRegen()
        {
            if (!Flames && !Chlorine)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;

            if (Flames)
                Player.lifeRegen -= ThirtyASecond;

            if (Chlorine)
                Player.lifeRegen -= ThirtyASecond;
        }

        public override void PostUpdateMiscEffects()
        {
            if (Main.dedServ || Player.dead)
                return;

            if (Flames)
            {
                PADFire.Burn(Player.Bottom + new Vector2(0f, -10f), 9f, 0.9f, FirePalette.Fire, smokes: Main.rand.NextBool(3));
                Lighting.AddLight(Player.Center, 0.9f, 0.45f, 0.12f);
            }

            if (Chlorine && Main.rand.NextBool(3))
                PADFire.Gas(Player.Center + Main.rand.NextVector2Circular(12f, 18f), new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 1f)),
                    Main.rand.NextFloat(22f, 36f), Main.rand.Next(30, 50), 0.8f);
        }
    }

    /// <summary>
    /// The two on an enemy (Violent Crimes gives them): thirty life a second each, no healing meanwhile; fire on the one,
    /// green gas off the other, by how big it is.
    /// </summary>
    public class TwinsBurnNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Life regeneration is counted in halves of a life a second.</summary>
        private const int ThirtyASecond = 60;

        public bool Flames, Chlorine;

        public override void ResetEffects(NPC npc) => Flames = Chlorine = false;

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!Flames && !Chlorine)
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            if (Flames)
                npc.lifeRegen -= ThirtyASecond;

            if (Chlorine)
                npc.lifeRegen -= ThirtyASecond;

            // What it is shown losing each time: six for one of them (five times a second), ten for both.
            damage = Math.Max(damage, Flames && Chlorine ? 10 : 6);
        }

        public override void PostAI(NPC npc)
        {
            if (Main.dedServ || (!Flames && !Chlorine) || !PADFire.Near(npc.Center, 200f))
                return;

            float big = MathHelper.Clamp((npc.width + npc.height) * 0.25f, 8f, 60f);

            if (Flames)
            {
                PADFire.Burn(npc.Center + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f) * npc.width, npc.height * 0.2f), big * 0.5f, 0.5f + big * 0.012f, FirePalette.Fire, smokes: Main.rand.NextBool(4));
                Lighting.AddLight(npc.Center, 0.8f, 0.4f, 0.1f);
            }

            if (Chlorine && Main.rand.NextBool(3))
                PADFire.Gas(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f), new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 1f)),
                    Main.rand.NextFloat(0.9f, 1.4f) * (14f + big), Main.rand.Next(30, 50), 0.8f);
        }
    }
}
