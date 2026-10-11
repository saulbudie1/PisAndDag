using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Ultra Bleeding on an enemy: what every blow of Foigh's Rust Side Sword leaves while the
    /// sword is drenched (BloodDrenchedSword; by request, "any attack in blood drenched sword
    /// causes a version of ultra bleeding generic -200 per second"): the sword's cuts, its red
    /// slashes, the straight cuts out of the ground. One strength, not a player's three wounds:
    /// <see cref="LifePerSecond"/> life a second for <see cref="Duration"/> from the last blow
    /// (each blow puts the whole of it back), through any defense, as anything that bleeds or
    /// burns is. Blood spews down out of it as it does out of a player (UltraBleedingPlayer).
    ///
    /// Put on where the blow is worked out, the sword's owner's machine, which tells the world
    /// (NPC.AddBuff); the world tells every machine what an enemy has. Nothing bleeds that
    /// nothing can hurt at the time (NPC.dontTakeDamage: Foigh and 'Dame' Quilt between their
    /// fights, a boss not yet open to harm), and what the game keeps from every buff keeps it
    /// off (NPC.buffImmune). Each piece of a worm bleeds into the one life they share, as
    /// anything that bleeds or burns them does.
    /// </summary>
    public class UltraBleedingGeneric : ModBuff
    {
        /// <summary>The picture of a player's Ultra Bleeding, three wounds: it is their kind. (A buff an enemy has is not shown.)</summary>
        public override string Texture => "PisAndDag/Content/Buffs/UltraBleeding3";

        /// <summary>How long it lasts from the last blow, in ticks: a player's Ultra Bleeding's.</summary>
        public const int Duration = UltraBleeding.Duration;

        /// <summary>Life taken a second.</summary>
        public const int LifePerSecond = 200;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            npc.GetGlobalNPC<UltraBleedingNPC>().Bleeding = true;
        }

        /// <summary>A blow of the drenched sword's that has landed: the enemy bleeding, for the whole of its time from now.</summary>
        public static void Cut(NPC npc)
        {
            if (npc == null || !npc.active || npc.dontTakeDamage)
                return;

            npc.AddBuff(ModContent.BuffType<UltraBleedingGeneric>(), Duration);
        }
    }

    /// <summary>What Ultra Bleeding does to an enemy: the life it takes, and the blood out of it.</summary>
    public class UltraBleedingNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Bleeding this tick: set by the buff, every tick it is on (UltraBleedingGeneric.Update).</summary>
        public bool Bleeding;

        /// <summary>
        /// Where its wounds are on it, as shares of half its width and height from its middle
        /// (toward the way it faces, and down); and how many it has: one, and two and three on
        /// the bigger, by the size of it.
        /// </summary>
        private static readonly Vector2[] WoundAt = { new Vector2(0.35f, -0.3f), new Vector2(-0.4f, 0.05f), new Vector2(0.2f, 0.4f) };
        private const float SecondWoundFrom = 60f, ThirdWoundFrom = 120f;

        /// <summary>A beat of the heart, in ticks; the ticks of each beat a wound spurts for, and the drops it spurts a tick; one tick in how many a drop runs out between; and the ticks between two wounds' spurts (a player's, UltraBleedingPlayer's).</summary>
        private const int Beat = 36;
        private const int SpurtTicks = 5;
        private const int SpurtDrops = 2;
        private const int RunsOneIn = 3;
        private const int WoundsApart = 12;

        /// <summary>A drop lives no longer than this; one drop of a spurt in this many leaves a stain where it comes down; and none is made for an enemy this far from this machine's player.</summary>
        private const int DropLongest = 90;
        private const int StainsOneIn = 3;
        private const float SeenWithin = 2400f;

        private static readonly Color Spurt = new Color(150, 8, 12);
        private static readonly Color Runs = new Color(112, 6, 10);

        public override void ResetEffects(NPC npc)
        {
            Bleeding = false;
        }

        /// <summary>
        /// lifeRegen counts half points of life a second. The game takes it off in pieces of
        /// <paramref name="damage"/>, and shows each: <see cref="UltraBleedingGeneric.LifePerSecond"/>
        /// at a time, once a second.
        /// </summary>
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (!Bleeding || npc.dontTakeDamage)
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= UltraBleedingGeneric.LifePerSecond * 2;

            if (damage < UltraBleedingGeneric.LifePerSecond)
                damage = UltraBleedingGeneric.LifePerSecond;
        }

        /// <summary>Every tick, on a machine with a screen: blood spews down out of every wound it has, a spurt with every beat and a run of it between.</summary>
        public override void PostAI(NPC npc)
        {
            if (Main.dedServ || !Bleeding || Main.LocalPlayer == null || Vector2.Distance(npc.Center, Main.LocalPlayer.Center) > SeenWithin)
                return;

            float size = (float)Math.Sqrt(npc.width * (double)npc.height);
            int wounds = size >= ThirdWoundFrom ? 3 : size >= SecondWoundFrom ? 2 : 1;

            for (int wound = 0; wound < wounds; wound++)
                Spew(npc, wound);
        }

        private static void Spew(NPC npc, int wound)
        {
            int facing = npc.direction == 0 ? 1 : npc.direction;
            Vector2 at = npc.Center + new Vector2(WoundAt[wound].X * npc.width * 0.5f * facing, WoundAt[wound].Y * npc.height * 0.5f);
            float outward = Math.Sign(WoundAt[wound].X) * facing;
            int beat = (int)((Main.GameUpdateCount + (uint)(wound * WoundsApart) + (uint)(npc.whoAmI * 7)) % Beat);

            if (beat < SpurtTicks)
            {
                for (int i = 0; i < SpurtDrops; i++)
                {
                    Drop(npc, at, new Vector2(outward * Main.rand.NextFloat(0.4f, 1.8f), Main.rand.NextFloat(2.2f, 4.6f)), Main.rand.NextFloat(0.55f, 0.95f), Spurt,
                        Main.rand.NextBool(StainsOneIn));
                }
            }
            else if (Main.rand.NextBool(RunsOneIn))
            {
                Drop(npc, at, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(0.4f, 1.2f)), Main.rand.NextFloat(0.4f, 0.65f), Runs, false);
            }
        }

        /// <summary>A drop out of a wound, going as the enemy goes too: it lives until it is at the ground under it and no longer, and, if asked, what is left of it lies there a while (a player's way: UltraBleedingPlayer.Drop).</summary>
        private static void Drop(NPC npc, Vector2 at, Vector2 velocity, float scale, Color colour, bool stain)
        {
            Vector2 from = at + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
            Vector2 going = velocity + npc.velocity * 0.6f;
            float ground = UltraBleedingPlayer.GroundUnder(from);
            int life = BloodDroplet.Fall(from, going, ground, DropLongest, out Vector2 down);

            if (life >= 2)
                PADParticleHandler.SpawnParticle(new BloodDroplet(from, going, life, scale, colour));

            if (stain && life >= 2 && down.Y >= ground)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(new Vector2(down.X, ground - 1f), Vector2.Zero,
                    Main.rand.Next(150, 230), Main.rand.NextFloat(0.4f, 0.7f), Runs, false));
            }
        }
    }
}
