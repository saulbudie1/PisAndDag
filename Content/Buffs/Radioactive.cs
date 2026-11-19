using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Mech;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Radioactive: what the ground Skeletron Prime went up over gives to whatever stands on it (PrimeFallout), and its
    /// blast to what it hits (by request: "Debuff: Radioactive , slow player down by 50%, defense down by 50%, life
    /// draining 60 HP/s. on npc: slow down non boss by 50%, -60HP/s"). Its picture drawn in code (scratchpad
    /// prime/art.py). It lasts as long as they stand there and a second after.
    /// </summary>
    public class Radioactive : ModBuff
    {
        /// <summary>How long it stays once they are off the ground that gives it.</summary>
        public const int Lingers = 60;

        public const float Slow = 0.5f;
        public const float DefenseLeft = 0.5f;

        /// <summary>Sixty life a second: life regeneration is counted in halves of a life a second.</summary>
        public const int Drain = 120;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) => player.GetModPlayer<RadioactivePlayer>().Radioactive = true;

        public override void Update(NPC npc, ref int buffIndex) => npc.GetGlobalNPC<RadioactiveNPC>().Radioactive = true;
    }

    /// <summary>What it does to a player: half their speed on foot, half their defense, sixty life a second and no healing meanwhile; its green over them.</summary>
    public class RadioactivePlayer : ModPlayer
    {
        public bool Radioactive;

        public override void ResetEffects() => Radioactive = false;

        public override void UpdateBadLifeRegen()
        {
            if (!Radioactive)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= Buffs.Radioactive.Drain;
        }

        public override void PostUpdateMiscEffects()
        {
            if (!Radioactive)
                return;

            Player.statDefense *= Buffs.Radioactive.DefenseLeft;

            if (Main.dedServ || Player.dead)
                return;

            Lighting.AddLight(Player.Center, 0.3f, 0.42f, 0f);

            if (Main.rand.NextBool(4))
            {
                HoneyMote mote = new(Player.Center + Main.rand.NextVector2Circular(12f, 20f), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1.1f)),
                    Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear, Main.rand.NextFloat(0.3f, 0.5f), Main.rand.Next(20, 36), 0.02f)
                {
                    Pixelate = true,
                };

                PADParticleHandler.SpawnParticle(mote);
            }
        }

        public override void PostUpdateRunSpeeds()
        {
            if (!Radioactive)
                return;

            Player.maxRunSpeed *= Buffs.Radioactive.Slow;
            Player.accRunSpeed *= Buffs.Radioactive.Slow;
            Player.runAcceleration *= Buffs.Radioactive.Slow;
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (!Radioactive)
                return;

            r *= 0.8f;
            b *= 0.45f;
        }
    }

    /// <summary>
    /// What it does to an enemy: sixty life a second and no healing meanwhile; and, one that is not a boss, half as far
    /// each tick as it would have gone (what it means to do is left alone: only how far it gets). Its green over it.
    /// </summary>
    public class RadioactiveNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool Radioactive;

        public override void ResetEffects(NPC npc) => Radioactive = false;

        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!Radioactive)
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= Buffs.Radioactive.Drain;

            // What it is shown losing each time: twelve, five times a second.
            damage = Math.Max(damage, 12);
        }

        public override void PostAI(NPC npc)
        {
            if (!Radioactive)
                return;

            // Slowed: put back half of what it is about to move, where that does not put it into the ground. Not a
            // boss, and not a worm's body (which follows its head).
            if (!npc.boss && !NPCID.Sets.ShouldBeCountedAsBoss[npc.type] && (npc.realLife < 0 || npc.realLife == npc.whoAmI))
            {
                Vector2 back = npc.position - npc.velocity * (1f - Buffs.Radioactive.Slow);

                if (npc.noTileCollide || !Collision.SolidCollision(back, npc.width, npc.height))
                    npc.position = back;
            }

            if (Main.dedServ || !Prime.Seen(npc.Center, 100f))
                return;

            Lighting.AddLight(npc.Center, 0.25f, 0.36f, 0f);

            if (Main.rand.NextBool(5))
            {
                HoneyMote mote = new(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1.1f)),
                    Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear, Main.rand.NextFloat(0.3f, 0.5f), Main.rand.Next(20, 36), 0.02f)
                {
                    Pixelate = true,
                };

                PADParticleHandler.SpawnParticle(mote);
            }
        }

        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Radioactive)
                drawColor = drawColor.MultiplyRGB(new Color(204, 255, 116));
        }
    }
}
