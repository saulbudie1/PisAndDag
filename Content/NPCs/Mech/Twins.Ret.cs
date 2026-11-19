using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- the Retinazer's own, in Legends -------------------------------------------------------------------------------------
    //
    // Its fire gives Industrial Flames (30 life a second, 3 seconds).
    //
    // FIRST FORM
    //   (its lasers)   vanilla's, flaming (TwinsShots: drawn as a streak of fire, a trail of flames behind).
    //   ROLL           "Rolls into a fireball (fire draw behind, modify texture to make it look bright) that spins and flies
    //                  towards player, have some over shoot and shoot again thrice". CURL 45: it stops, spins up, turns to fire
    //                  (its picture run through fire's colours, lit; a comet of light behind it; flames off it). Then three
    //                  times: DASH at the player and twelve tiles past them (26 to 54 ticks by how far), quick off the mark and
    //                  carrying its speed out; BRAKE 18. COOL 30. What it touches burns.
    //   COLUMN         "pause in place and has a indicator that flashes in straight line and shoot a streak of fire column".
    //                  AIM 56: it stops; a line from its mouth, flashing, following the player till it locks (36), then solid.
    //                  FIRE 46: a column of fire three tiles wide down that line for forty ticks; it is thrown back by it.
    //
    // SECOND FORM
    //   (its barrage)  vanilla's quick fire from beside the player, flaming.
    //   STREAKS        "Spark from cannon and shoot a streak of fire 3x like attack 3 phase 1 with 3/5 indicator time interval
    //                  of 1 sec with recoil, doesnt stop in place, but hover far away from player". Thirty tiles off, drifting
    //                  round them; three times a second apart: its cannon sparks, the line (34 ticks, locked at 24), a column
    //                  for 16, its recoil.
    //   SPIN COLUMN    "Spining in place laser column trick (6 tiles thick) that accels spin with time for 10 secs". AIM 70: it
    //                  stops and turns its back on the player, a band six tiles wide marked away from them, and a fainter one
    //                  the way it will turn. Then ten seconds of that column, turning, from a full turn in seventeen seconds
    //                  to one in three and a half (1.7 turns in all). Its back is the safe side.

    public partial class Twins
    {
        public static readonly SoundStyle Lighter = new("PisAndDag/Content/Sounds/Mech/TwinsLighter")
        {
            Volume = 0.7f,
            PitchVariance = 0.25f,
            MaxInstances = 3,
        };

        /// <summary>How wide its columns are (three tiles; its turning one six), and how far they reach.</summary>
        public const float ColumnWide = 48f;
        public const float SpinWide = 96f;
        public const float ColumnReach = 2400f;

        // ---- ROLL ---------------------------------------------------------------------------------------------------------------

        private const int RollCurl = 0, RollDash = 1, RollBrake = 2, RollCool = 3;
        private const int CurlTicks = 45, BrakeTicks = 18, CoolTicks = 30, RollDashes = 3;

        /// <summary>How far past the player a charge carries (by request: "have some over shoot").</summary>
        private const float RollPast = 12f * 16f;

        private void RunRoll(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case RollCurl:
                    {
                        float u = t / (float)CurlTicks;
                        npc.velocity *= 0.9f;
                        heat = Math.Max(heat, Smooth(u));
                        npc.rotation += side * (0.06f + 0.4f * u * u);
                        shake = 2f * u;

                        if (entered)
                            RollFx(npc, false);

                        if (!Main.dedServ)
                            PADFire.Burn(EyeCentre(npc) + new Vector2(0f, 20f), 12f + 34f * u, 1f + 2.5f * u);

                        if (Authority && t >= CurlTicks)
                        {
                            AimRoll(npc, p);
                            Next(npc, RollDash);
                        }

                        break;
                    }

                case RollDash:
                    {
                        // Quick off the mark, and carrying half its speed out past them.
                        float u = Math.Min(1f, (t + 1f) / Math.Max(1f, span));
                        Vector2 was = npc.Center;
                        Move(npc, Vector2.Lerp(markA, markB, 0.5f * u + 0.5f * (1f - (1f - u) * (1f - u))));
                        heat = 1f;
                        npc.rotation += side * 0.46f;

                        if (entered)
                            RollFx(npc, true);

                        RollTrail(npc, was);

                        if (Authority && t + 1 >= span)
                        {
                            count++;
                            Next(npc, count >= RollDashes ? RollCool : RollBrake);
                        }

                        break;
                    }

                case RollBrake:
                    {
                        Vector2 was = npc.Center;
                        npc.velocity *= 0.86f;
                        heat = 1f;
                        npc.rotation += side * 0.3f;
                        RollTrail(npc, was);

                        if (Authority && t >= BrakeTicks)
                        {
                            AimRoll(npc, p);
                            Next(npc, RollDash);
                        }

                        break;
                    }

                case RollCool:
                    {
                        float u = t / (float)CoolTicks;
                        npc.velocity *= 0.9f;
                        heat = 1f - Smooth(u);

                        if (u < 0.5f)
                            npc.rotation += side * 0.3f * (1f - 2f * u);
                        else
                            Turn(npc, p.Center - npc.Center, 0.2f);

                        if (Authority && t >= CoolTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        /// <summary>A charge laid: from where it is, at where the player is going, and past them. The server's.</summary>
        private void AimRoll(NPC npc, Player p)
        {
            Vector2 way = p.Center + p.velocity * 10f - npc.Center;
            float far = way.Length();
            way = far > 1f ? way / far : Vector2.UnitY;
            markA = npc.Center;
            markB = npc.Center + way * (far + RollPast);
            span = MathHelper.Clamp((far + RollPast) / 24f, 26f, 54f);
        }

        private static void RollFx(NPC npc, bool charge)
        {
            if (Main.dedServ)
                return;

            if (charge)
            {
                Sound(SoundID.DD2_BetsyFireballShot with { Volume = 1f, Pitch = -0.25f }, npc.Center, 6);
                Sound(SoundID.Item34 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center, 6);
                // A kick of flame out behind it.
                PADFire.Puff(EyeCentre(npc), -npc.velocity, 74f, FirePalette.Fire, 1.3f, 0.6f);
                Shake(npc.Center, 7f, 14);
            }
            else
            {
                Sound(SoundID.DD2_KoboldIgnite with { Volume = 1f, Pitch = -0.3f }, npc.Center, 6);
                Sound(SoundID.Item34 with { Volume = 0.8f, Pitch = -0.6f }, npc.Center, 6);
            }
        }

        /// <summary>The fire it leaves along its way.</summary>
        private static void RollTrail(NPC npc, Vector2 was)
        {
            if (Main.dedServ)
                return;

            Vector2 lift = EyeCentre(npc) - npc.Center;
            PADFire.Trail(was + lift, npc.Center + npc.velocity + lift, 74f, FirePalette.Fire, 1.25f);
            Lighting.AddLight(npc.Center, 1.7f, 0.85f, 0.25f);
        }

        // ---- COLUMN -------------------------------------------------------------------------------------------------------------

        private const int ColAim = 0, ColFire = 1, ColRest = 2;
        private const int ColLock = 36, ColAimTicks = 56, ColLife = 40, ColFireTicks = 46, ColRestTicks = 20;

        private void RunColumn(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case ColAim:
                    {
                        npc.velocity *= 0.86f;

                        if (count > 0)
                            npc.rotation = angle;
                        else
                            Turn(npc, p.Center - EyeCentre(npc), 0.18f);

                        if (Authority && count == 0 && t >= ColLock)
                        {
                            angle = npc.rotation;
                            count = 1;
                            npc.netUpdate = true;
                        }

                        if (entered)
                            AimFx(npc);

                        MouthHeat(npc, t / (float)ColAimTicks);

                        if (Authority && t >= ColAimTicks)
                            Next(npc, ColFire);

                        break;
                    }

                case ColFire:
                    {
                        npc.rotation = angle;

                        if (entered)
                        {
                            npc.velocity = -Facing(npc) * 7f;

                            if (Authority)
                                TwinsShots.FireColumn(npc, ColumnWide, ColLife);

                            ColumnFx(npc, 1f);
                        }

                        npc.velocity *= 0.9f;

                        if (t < ColLife)
                            shake = 1.5f;

                        if (Authority && t >= ColFireTicks)
                            Next(npc, ColRest);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.9f;
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (Authority && t >= ColRestTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- STREAKS ------------------------------------------------------------------------------------------------------------

        private const int StreakGo = 0, StreakShoot = 1, StreakRest = 2;
        private const int StreakGoMost = 50, StreakCycle = 60, StreakLock = 24, StreakWarn = 34, StreakLife = 16, StreakShots = 3, StreakRestTicks = 20;

        /// <summary>The ticks of a round its line locks and its column goes on, kept inside the round whatever it is set to.</summary>
        private static readonly int StreakWarnAt = Math.Clamp(StreakWarn, 0, Math.Max(0, StreakCycle - 1));
        private static readonly int StreakLockAt = Math.Min(StreakLock, Math.Max(0, StreakWarnAt - 1));

        /// <summary>How far off it keeps (by request: "hover far away from player").</summary>
        private const float StreakFar = 30f * 16f;

        /// <summary>A place this far from the player on the side it is on, a little round from where it is (so it drifts round them); never from under them.</summary>
        private Vector2 FarSpot(NPC npc, Player p, float far)
        {
            Vector2 from = npc.Center - p.Center;
            float bearing = from.LengthSquared() > 1f ? from.ToRotation() : -MathHelper.PiOver2;
            Vector2 dir = (bearing + side * 0.012f).ToRotationVector2();

            if (dir.Y > 0.2f)
                dir = new Vector2(dir.X, 0.2f).SafeNormalize(-Vector2.UnitY);

            return p.Center + dir * far;
        }

        private void RunStreaks(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case StreakGo:
                    {
                        Hover(npc, FarSpot(npc, p, StreakFar), 18f, 0.1f);
                        Turn(npc, p.Center - EyeCentre(npc), 0.15f);

                        if (Authority && (t >= StreakGoMost || t > 10 && Vector2.Distance(npc.Center, p.Center) > StreakFar - 70f))
                            Next(npc, StreakShoot);

                        break;
                    }

                case StreakShoot:
                    {
                        int c = t % StreakCycle, k = t / StreakCycle;
                        bool locked = count > k;

                        // It does not stop: far off, drifting round them.
                        Hover(npc, FarSpot(npc, p, StreakFar), 8f, 0.05f);

                        if (locked)
                            npc.rotation = angle;
                        else
                            Turn(npc, p.Center - EyeCentre(npc), 0.22f);

                        if (Authority && !locked && c >= StreakLockAt && c < StreakWarnAt)
                        {
                            angle = npc.rotation;
                            count = k + 1;
                            npc.netUpdate = true;
                        }

                        if (c < StreakWarnAt)
                            LighterSparks(npc, c / (float)StreakWarnAt);

                        if (c == StreakWarnAt)
                        {
                            if (Authority)
                                TwinsShots.FireColumn(npc, ColumnWide, StreakLife);

                            // Its recoil.
                            npc.velocity -= Facing(npc) * 10f;
                            ColumnFx(npc, 0.8f);
                        }

                        if (c > StreakWarnAt && c <= StreakWarnAt + StreakLife)
                            shake = 1.5f;

                        if (Authority && t >= StreakCycle * StreakShots - 1)
                            Next(npc, StreakRest);

                        break;
                    }

                default:
                    {
                        Hover(npc, FarSpot(npc, p, StreakFar), 8f, 0.05f);
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (Authority && t >= StreakRestTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- SPIN COLUMN --------------------------------------------------------------------------------------------------------

        private const int SpinAim = 0, SpinGo = 1, SpinRest = 2;
        private const int SpinFaceTicks = 30, SpinAimTicks = 70, SpinTicks = 600, SpinRestTicks = 30;

        /// <summary>How fast it turns as it begins and as it ends, radians a tick (by request: "accels spin with time").</summary>
        private const float SpinSlow = 0.006f;
        private const float SpinFast = 0.03f;

        /// <summary>How far it has turned, this many ticks in.</summary>
        private static float SpinTurned(int t) => SpinSlow * t + 0.5f * (SpinFast - SpinSlow) / SpinTicks * t * t;

        private void RunSpinColumn(NPC npc, bool entered, Player p)
        {
            int t = timer;

            switch (step)
            {
                case SpinAim:
                    {
                        npc.velocity *= 0.85f;

                        // Its back to them: its column begins pointing away, and comes round.
                        if (count > 0)
                            npc.rotation = angle;
                        else
                            Turn(npc, EyeCentre(npc) - p.Center, 0.2f);

                        if (Authority && count == 0 && t >= SpinFaceTicks)
                        {
                            angle = LookAlong(EyeCentre(npc) - p.Center);
                            count = 1;
                            npc.netUpdate = true;
                        }

                        if (entered)
                            AimFx(npc);

                        MouthHeat(npc, t / (float)SpinAimTicks);

                        if (Authority && t >= SpinAimTicks)
                            Next(npc, SpinGo);

                        break;
                    }

                case SpinGo:
                    {
                        npc.velocity = Vector2.Zero;
                        npc.rotation = angle + side * SpinTurned(t);
                        shake = 1.2f;

                        if (entered)
                        {
                            if (Authority)
                                TwinsShots.FireColumn(npc, SpinWide, SpinTicks);

                            ColumnFx(npc, 1.3f);
                        }

                        if (!Main.dedServ && t % 80 == 40)
                            Sound(SoundID.DD2_BetsyFlameBreath with { Volume = 0.9f, Pitch = -0.2f }, npc.Center, 6);

                        if (Authority && t >= SpinTicks)
                            Next(npc, SpinRest);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.9f;
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (Authority && t >= SpinRestTicks)
                            EndMove(npc);

                        break;
                    }
            }
        }

        // ---- seen and heard -----------------------------------------------------------------------------------------------------

        /// <summary>It takes aim: a servo's click.</summary>
        private static void AimFx(NPC npc)
        {
            if (!Main.dedServ)
                Sound(SoundID.Mech with { Volume = 0.9f, Pitch = 0.3f }, npc.Center, 6);
        }

        /// <summary>Its mouth heating as it readies a column: a growing flame at it, its light.</summary>
        private static void MouthHeat(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Front(npc, 62f);
            PADFire.Burn(mouth, 5f + 9f * u, 0.5f + 1.3f * u, FirePalette.Fire, smokes: false);
            Lighting.AddLight(mouth, 1.2f * (0.3f + u), 0.55f * (0.3f + u), 0.15f);
        }

        /// <summary>Its cannon sparking like a lighter (by request): sparks struck off its mouth, a click, now and then a lick of flame.</summary>
        private static void LighterSparks(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Front(npc, 62f);
            Vector2 way = Facing(npc);

            if (Main.GameUpdateCount % 9 == 0)
            {
                Sound(Lighter, mouth, 6);

                for (int i = 0; i < 6; i++)
                    PADFire.Spark(mouth, way.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 7f) + npc.velocity, Main.rand.Next(12, 24));

                Arc(mouth, mouth + way.RotatedByRandom(0.8f) * Main.rand.NextFloat(14f, 30f), new Color(255, 190, 90), Main.rand.NextFloat(4f, 7f), 8, 0);
            }

            if (Main.rand.NextFloat() < 0.25f + 0.5f * u)
                PADFire.Flame(mouth, way * Main.rand.NextFloat(0.5f, 2f) + npc.velocity * 0.5f, Main.rand.NextFloat(16f, 26f + 14f * u), Main.rand.Next(10, 18), FirePalette.Fire, 0.1f, 0.6f);

            Lighting.AddLight(mouth, 0.9f, 0.45f, 0.1f);
        }

        /// <summary>A column let go: the roar of it, flame thrown out of its mouth the way it goes, the screen shaken.</summary>
        private static void ColumnFx(NPC npc, float power)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Front(npc, 62f);
            Sound(SoundID.DD2_BetsyFlameBreath with { Volume = Math.Min(1f, 0.85f * power), Pitch = 0.1f }, mouth, 6);
            Sound(SoundID.Item74 with { Volume = 0.9f, Pitch = -0.4f }, mouth, 6);
            PADFire.Puff(mouth, Facing(npc), 54f * power, FirePalette.Fire, power, 0.45f);
            Shake(mouth, 8f * power, 16);
        }

        /// <summary>What the Retinazer's attacks warn with: the line its column will go down, as wide as the column.</summary>
        private void DrawRetMarks(NPC npc, SpriteBatch batch)
        {
            Vector2 from = Front(npc, 60f);
            float along = Facing(npc).ToRotation();
            Color tint = TwinsArt.FireBright;

            switch (act)
            {
                case Act.Column when step == ColAim && timer > 4:
                    DrawWarn(batch, from, along, ColumnReach, timer / (float)ColAimTicks, count > 0, tint, ColumnWide);
                    break;

                case Act.Streaks when step == StreakShoot:
                    {
                        int c = timer % StreakCycle;

                        if (c < StreakWarnAt)
                            DrawWarn(batch, from, along, ColumnReach, c / (float)StreakWarnAt, count > timer / StreakCycle, tint, ColumnWide);

                        break;
                    }

                case Act.SpinColumn when step == SpinAim && timer > 4:
                    {
                        float ready = timer / (float)SpinAimTicks;
                        DrawWarn(batch, from, along, ColumnReach, ready, count > 0, tint, SpinWide);

                        // The way it will turn: fainter lines ahead of it.
                        if (count > 0)
                        {
                            for (int k = 1; k <= 3; k++)
                                DrawMark(batch, from, along + side * 0.14f * k, ColumnReach * 0.5f, 0.2f, ready * (0.6f - 0.15f * k), tint);
                        }

                        break;
                    }
            }
        }
    }
}
