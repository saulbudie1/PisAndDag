using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Ultra Bleeding: what Foigh's sword leaves in his third phase. Every blow of it that
    /// lands is a wound, and there can be three at once: I, II and III, which take 12, 24
    /// and 36 life a second. Under any of them no life comes back by itself. It lasts
    /// <see cref="Duration"/> from the last blow, whichever that was, and when that runs
    /// out all of it stops at once.
    ///
    /// One buff for each number of wounds, as the mod's other debuffs that come in
    /// strengths are (Mana Nausea, Mana Poisoning): which of them a player has is what the
    /// game tells every machine by itself, so nothing more has to be sent.
    ///
    /// Blood spews down out of every wound, by request: "ultrableeding have blood particle
    /// spewing down" (UltraBleedingPlayer.PostUpdate).
    /// </summary>
    public abstract class UltraBleeding : ModBuff
    {
        /// <summary>How long it lasts from the last blow that added to it, in ticks.</summary>
        public const int Duration = 5 * 60;

        /// <summary>Life lost every second for each wound; and the most wounds there can be.</summary>
        public const int LifeAWound = 12;
        public const int Most = 3;

        public abstract int Wounds { get; }

        /// <summary>Life lost every second.</summary>
        public int LifePerSecond => LifeAWound * Wounds;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        /// <summary>Bleeding, as the game has it: no life comes back by itself, and it shows on them (Player.bleed).</summary>
        public override void Update(Player player, ref int buffIndex)
        {
            player.bleed = true;
        }

        /// <summary>The buff for a number of wounds, 1 to 3.</summary>
        public static int TypeFor(int wounds) => wounds switch
        {
            1 => ModContent.BuffType<UltraBleeding1>(),
            2 => ModContent.BuffType<UltraBleeding2>(),
            _ => ModContent.BuffType<UltraBleeding3>(),
        };

        /// <summary>How many wounds a player has: 0 to 3.</summary>
        public static int On(Player player)
        {
            for (int wounds = Most; wounds >= 1; wounds--)
            {
                if (player.HasBuff(TypeFor(wounds)))
                    return wounds;
            }

            return 0;
        }

        /// <summary>
        /// One more wound on this machine's own player, up to three, and the whole of its
        /// time from now. (A player's own machine puts their debuffs on them, as it works
        /// out what hits them; the game tells the others.)
        /// </summary>
        public static void Wound(Player player)
        {
            if (player == null || player.whoAmI != Main.myPlayer || player.dead)
                return;

            int wounds = Math.Min(Most, On(player) + 1);

            for (int other = 1; other <= Most; other++)
            {
                if (other != wounds)
                    player.ClearBuff(TypeFor(other));
            }

            player.AddBuff(TypeFor(wounds), Duration);
        }
    }

    public class UltraBleeding1 : UltraBleeding
    {
        public override int Wounds => 1;
    }

    public class UltraBleeding2 : UltraBleeding
    {
        public override int Wounds => 2;
    }

    public class UltraBleeding3 : UltraBleeding
    {
        public override int Wounds => 3;
    }

    /// <summary>The life the wounds take, the blood they spew, and what is said of one who dies of them.</summary>
    public class UltraBleedingPlayer : ModPlayer
    {
        /// <summary>
        /// Where each wound is on them, from their middle, in pixels: toward the way they
        /// face, and down. The first in the chest, the second in the side behind, the third
        /// at the hip.
        /// </summary>
        private static readonly Vector2[] WoundAt = { new Vector2(5f, -7f), new Vector2(-5f, 1f), new Vector2(4f, 9f) };

        /// <summary>
        /// A beat of the heart, in ticks; the ticks of each beat a wound spurts for, and the
        /// drops it spurts a tick; one tick in how many a drop runs out of it between spurts;
        /// and how many ticks apart the wounds' spurts are.
        /// </summary>
        private const int Beat = 36;
        private const int SpurtTicks = 5;
        private const int SpurtDrops = 2;
        private const int RunsOneIn = 3;
        private const int WoundsApart = 12;

        /// <summary>The blood of a spurt; and the darker that runs between.</summary>
        private static readonly Color Spurt = new Color(150, 8, 12);
        private static readonly Color Runs = new Color(112, 6, 10);

        /// <summary>A drop lives no longer than this; the ground is looked for this many tiles under a wound; and one drop of a spurt in this many leaves a stain where it comes down.</summary>
        private const int DropLongest = 90;
        private const int GroundWithin = 24;
        private const int StainsOneIn = 3;

        /// <summary>
        /// Every tick, on a machine with a screen: blood spews down out of every wound they
        /// have, each from a place of its own on them, a spurt with every beat of the heart
        /// and a run of it between; so the more wounds, the more of it. By request:
        /// "ultrableeding have blood particle spewing down".
        /// </summary>
        public override void PostUpdate()
        {
            if (Main.dedServ || Player.dead)
                return;

            int wounds = UltraBleeding.On(Player);

            for (int wound = 0; wound < wounds; wound++)
                Spew(wound);
        }

        /// <summary>One wound's blood, this tick.</summary>
        private void Spew(int wound)
        {
            Vector2 at = Player.Center + new Vector2(WoundAt[wound].X * Player.direction, WoundAt[wound].Y * Player.gravDir);
            float outward = Math.Sign(WoundAt[wound].X) * Player.direction;
            int beat = (int)((Main.GameUpdateCount + (uint)(wound * WoundsApart)) % Beat);

            if (beat < SpurtTicks)
            {
                for (int i = 0; i < SpurtDrops; i++)
                {
                    Drop(at, new Vector2(outward * Main.rand.NextFloat(0.4f, 1.8f), Main.rand.NextFloat(2.2f, 4.6f)), Main.rand.NextFloat(0.55f, 0.95f), Spurt,
                        Main.rand.NextBool(StainsOneIn));
                }
            }
            else if (Main.rand.NextBool(RunsOneIn))
            {
                Drop(at, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(0.4f, 1.2f)), Main.rand.NextFloat(0.4f, 0.65f), Runs, false);
            }
        }

        /// <summary>
        /// A drop out of a wound, going as they go too: it lives until it is at the ground
        /// under it and no longer, so that none falls through it, as his own blood does
        /// (Foigh.Drop); and, if asked, what is left of it lies there a while.
        /// </summary>
        private void Drop(Vector2 at, Vector2 velocity, float scale, Color colour, bool stain)
        {
            Vector2 from = at + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1.5f, 1.5f));
            Vector2 going = velocity + Player.velocity * 0.6f;
            float ground = GroundUnder(from);
            int life = BloodDroplet.Fall(from, going, ground, DropLongest, out Vector2 down);

            // (One that would be at the ground within the tick is not seen falling at all: a
            // drop is drawn first where its first tick has taken it.)
            if (life >= 2)
                PADParticleHandler.SpawnParticle(new BloodDroplet(from, going, life, scale, colour));

            if (stain && life >= 2 && down.Y >= ground)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(new Vector2(down.X, ground - 1f), Vector2.Zero,
                    Main.rand.Next(150, 230), Main.rand.NextFloat(0.4f, 0.7f), Runs, false));
            }
        }

        /// <summary>
        /// The ground under a point, in pixels: the top of the first solid tile there within
        /// GroundWithin tiles (where a half block's top is, or a slope's at that point across
        /// it), or of a platform under the point; or none (float.MaxValue). An enemy's bleeding
        /// asks it too (UltraBleedingNPC).
        /// </summary>
        internal static float GroundUnder(Vector2 at)
        {
            int x = (int)(at.X / 16f);
            int top = (int)(at.Y / 16f);
            float across = at.X - x * 16f;

            for (int y = top; y <= top + GroundWithin; y++)
            {
                if (!WorldGen.InWorld(x, y))
                    break;

                Tile tile = Main.tile[x, y];
                bool solid = Main.tileSolid[tile.TileType];

                if (!tile.HasTile || tile.IsActuated || (!solid && !Main.tileSolidTop[tile.TileType]))
                    continue;

                float surface = y * 16f;

                if (tile.IsHalfBlock)
                    surface += 8f;
                else if (tile.Slope == SlopeType.SlopeDownLeft)
                    surface += across;
                else if (tile.Slope == SlopeType.SlopeDownRight)
                    surface += 16f - across;

                // (A platform it is already under is not under it.)
                if (!solid && surface <= at.Y)
                    continue;

                return surface;
            }

            return float.MaxValue;
        }

        /// <summary>lifeRegen counts half points of life a second.</summary>
        public override void UpdateBadLifeRegen()
        {
            int wounds = UltraBleeding.On(Player);

            if (wounds == 0)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= UltraBleeding.LifeAWound * wounds * 2;
        }

        /// <summary>
        /// Life regen that runs out with nothing more particular to blame reports the death
        /// as burning (vanilla's "other" reason 8). Bled out, it was the sword.
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (damageSource.SourceOtherIndex == 8 && UltraBleeding.On(Player) > 0)
            {
                damageSource = PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey("Mods.PisAndDag.DeathText.FoighBled", Player.name));
            }

            return true;
        }
    }
}
