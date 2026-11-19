using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- their attacks together, in Legends ---------------------------------------------------------------------------------
    //
    // By request ("Twin attacks: happens more rarely, both pauses whatever they are doing to do this ... During this twin
    // attack they are invincible"). The Retinazer's step and clock are the pair's (Twins.Legends.RunDuo). No shield is
    // drawn for it (by request: the shield is only the first to change's).
    //
    // BEFORE BOTH HAVE CHANGED
    //   PINCER   "Flies to 2 points of the screen afar, both anchored to a Y level above ... the player by half screen height,
    //            then point down, one shooting laser column, one shooting toxic spear, both in interval of 30 ticks and closing
    //            in until one reaches other's X position". FLY 70 to either side (880 px out, 500 over the player, a side
    //            each by chance), eyes down. CLOSE 300: they come together along that level till they meet; every thirty
    //            ticks the Retinazer a column straight down (a line for sixteen ticks, the column for twelve), the Spazmatism
    //            a Toxic Spear straight down between them.
    //   BINARY   "close butt to butt to each other, then click and they connected like 2 balls, and spin with anchor being the
    //            point of contact, in place and shooting fiery laser and cursed flame, one at interval of 45 and the other at
    //            25 for 7 secs". JOIN 60 over the player, backs together, a click. SPIN 420 about where they touch (up to a
    //            turn in two seconds), each firing the way it faces: the Retinazer a flaming laser every BinRetEvery
    //            ticks, the Spazmatism a cursed flame every BinSpazEvery (asked for as 45 and 25; set by hand since to
    //            17 and 10).
    //
    // AFTER
    //   GAS      "Spaz pauses and shakes, and start vommiting flamamble gas while moving near and uncontrably near the player
    //            and somtimes random places for 7 secs, with the gas leak sound ... gas projectile doesnt harm player but its
    //            slows down from release and gets bigger, at the same time Ret floats from afar with his cannon sparking flame
    //            like a lighter ... then it shoots flies down at contact with any gas projectile, it explode in chain".
    //            SHAKE 45. VOMIT 420: the Spazmatism lurching from place to place (two times in three near the player),
    //            a cloud of gas out of its mouth every four ticks (TwinsFlammableGas: harmless, slowing, swelling); the
    //            Retinazer far off and high, its cannon struck like a lighter. LIGHT 50: it rolls itself into a fireball.
    //            DIVE: at the cloud nearest the player; the first cloud it touches goes up, and each sets off those beside
    //            it, the fire running through them all.
    //   CLASH    "They look at each other and each glow the outline glow of their respective color and clash into each other,
    //            causing serious shock wave that dies down as it goes". FACE 70: thirty tiles either side of where they
    //            will meet, an outline of light growing on each. RUN 22. HIT: a shockwave out from where they meet
    //            (TwinsShockwave: eighteen pixels a tick to seventy tiles, full within ten tiles and less the further, it
    //            throws back what it hits); they are thrown apart, spinning.

    public partial class Twins
    {
        public static readonly SoundStyle GasHiss = new("PisAndDag/Content/Sounds/Mech/TwinsGasLeak")
        {
            Volume = 0.9f,
            MaxInstances = 1,
        };

        public static readonly SoundStyle Clang = new("PisAndDag/Content/Sounds/Mech/TwinsClash")
        {
            Volume = 1f,
            MaxInstances = 1,
        };

        // ---- PINCER -------------------------------------------------------------------------------------------------------------

        private const int PinFly = 0, PinClose = 1, PinPart = 2;
        private const int PinFlyTicks = 70, PinCloseTicks = 300, PinPartTicks = 30, PinEvery = 30, PinWarn = 16, PinLife = 12, PinSpear = 1;

        /// <summary>The ticks of a round the column and the spear fall on, kept inside the round whatever it is set to.</summary>
        private static readonly int PinWarnAt = Math.Clamp(PinWarn, 0, Math.Max(0, PinEvery - 1));
        private static readonly int PinSpearAt = Math.Clamp(PinSpear, 0, Math.Max(0, PinEvery - 1));

        /// <summary>How high over the player their level is (half a screen), how far out each begins, and how near they come.</summary>
        private const float PinHigh = 500f;
        private const float PinOut = 880f;
        private const float PinGap = 70f;

        private void RunPincer(NPC npc, bool lead, bool fresh, Twins clock, Player p, NPC other, Twins twin)
        {
            int t = duoT;

            if (lead && Authority && fresh && step == PinFly)
            {
                float level = p.Center.Y - PinHigh;
                markA = new Vector2(p.Center.X - side * PinOut, level);
                markB = new Vector2(p.Center.X + side * PinOut, level);
                npc.netUpdate = true;
            }

            Vector2 mine = lead ? clock.markA : clock.markB, theirs = lead ? clock.markB : clock.markA;
            float toward = Math.Sign(theirs.X - mine.X);

            switch (clock.step)
            {
                case PinFly:
                    {
                        Hover(npc, mine, 36f, 0.2f);
                        Turn(npc, Vector2.UnitY, 0.14f);

                        if (lead && Authority && timer >= PinFlyTicks)
                            Next(npc, PinClose);

                        break;
                    }

                case PinClose:
                    {
                        float u = Math.Min(1f, t / (float)PinCloseTicks);
                        float meet = (mine.X + theirs.X) * 0.5f - toward * PinGap;
                        Move(npc, new Vector2(MathHelper.Lerp(mine.X, meet, u), mine.Y + 6f * MathF.Sin(t * 0.08f + (lead ? 0f : 2f))));
                        npc.rotation = npc.rotation.AngleLerp(0f, 0.3f);

                        int c = t % PinEvery;

                        if (lead)
                        {
                            if (c < PinWarnAt)
                                MouthHeat(npc, c / (float)PinWarnAt);

                            if (c == PinWarnAt)
                            {
                                if (Authority)
                                    TwinsShots.FireColumn(npc, ColumnWide, PinLife);

                                ColumnFx(npc, 0.6f);
                            }
                        }
                        else if (c == PinSpearAt)
                        {
                            Vector2 mouth = Front(npc, 62f);

                            if (Authority)
                                TwinsShots.Spear(npc, mouth, new Vector2(0f, 13f));

                            if (!Main.dedServ)
                            {
                                PADFire.Puff(mouth, Vector2.UnitY, 36f, FirePalette.Toxic, 0.5f, 0.3f);
                                Sound(SoundID.DD2_OgreSpit with { Volume = 0.8f, Pitch = 0.2f }, mouth, 6);
                            }
                        }

                        if (lead && Authority && timer >= PinCloseTicks)
                            Next(npc, PinPart);

                        break;
                    }

                default:
                    {
                        Hover(npc, mine + new Vector2(0f, -100f), 12f, 0.06f);
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (lead && Authority && timer >= PinPartTicks)
                            EndDuo(npc, other, twin);

                        break;
                    }
            }
        }

        // ---- BINARY -------------------------------------------------------------------------------------------------------------

        private const int BinJoin = 0, BinSpin = 1, BinPart = 2;
        private const int BinJoinTicks = 60, BinSpinTicks = 420, BinPartTicks = 30, BinRetEvery = 17, BinSpazEvery = 10;

        /// <summary>
        /// How far each eye's middle is from where they touch (their pictures reach 93 behind their middles, their eyes
        /// 55: at 74 their nerves knot in the gap between their eyes, neither laid over the other's eye); how fast they
        /// turn; how high over the player they join.
        /// </summary>
        private const float BinApart = 74f;
        private const float BinTurn = 0.055f;
        private const float BinHigh = 300f;

        // Each fires half way through its own round, whatever the round is set to. (r82bm fired on ticks 20 and 10 of
        // them: set shorter than that by hand, 17 and 10, neither ever came, and nothing was fired.)

        /// <summary>How far round they are, this many ticks into their spin (up to speed over fifty).</summary>
        private static float BinAngle(int t) => t < 50 ? BinTurn * t * t / 100f : BinTurn * (t - 25f);

        private void RunBinary(NPC npc, bool lead, bool fresh, Twins clock, Player p, NPC other, Twins twin)
        {
            int t = duoT;

            if (lead && Authority && fresh && step == BinJoin)
            {
                markA = p.Center + new Vector2(0f, -BinHigh);
                npc.netUpdate = true;
            }

            Vector2 m = clock.markA;
            float mySide = lead ? -1f : 1f;

            switch (clock.step)
            {
                case BinJoin:
                    {
                        float u = Math.Min(1f, t / (float)BinJoinTicks);
                        Hover(npc, m + new Vector2(mySide * (BinApart + 90f * (1f - Smooth(u))), 0f), 28f, 0.2f);

                        // Its back to its twin.
                        Turn(npc, new Vector2(mySide, 0f), 0.16f);

                        if (lead && Authority && timer >= BinJoinTicks)
                            Next(npc, BinSpin);

                        break;
                    }

                case BinSpin:
                    {
                        if (fresh && lead)
                            ClickFx(m);

                        Vector2 arm = BinAngle(t).ToRotationVector2() * mySide;
                        Move(npc, m + arm * BinApart);
                        npc.rotation = LookAlong(arm);

                        Vector2 mouth = m + arm * (BinApart + 60f);

                        if (lead && t % BinRetEvery == BinRetEvery / 2)
                        {
                            if (Authority)
                                TwinsShots.Laser(npc, mouth, arm * 9f);

                            if (!Main.dedServ)
                            {
                                Sound(SoundID.Item33 with { Volume = 0.8f, Pitch = -0.2f }, mouth, 6);
                                PADFire.Puff(mouth, arm, 30f, FirePalette.Fire, 0.4f, 0.3f);
                            }
                        }
                        else if (!lead && t % BinSpazEvery == BinSpazEvery / 2)
                        {
                            if (Authority)
                                TwinsShots.CurseBall(npc, mouth, arm * 10f);

                            if (!Main.dedServ)
                                PADFire.Puff(mouth, arm, 30f, FirePalette.Toxic, 0.4f, 0.3f);
                        }

                        // Where they are joined: sparks.
                        if (lead && !Main.dedServ && t % 5 == 0)
                        {
                            PADFire.Spark(m, Main.rand.NextVector2Circular(5f, 5f), Main.rand.Next(12, 22));
                            PADFire.Spark(m, Main.rand.NextVector2Circular(5f, 5f), Main.rand.Next(12, 22), FirePalette.Toxic);
                            Lighting.AddLight(m, 0.9f, 0.9f, 0.5f);
                        }

                        if (lead && Authority && timer >= BinSpinTicks)
                            Next(npc, BinPart);

                        break;
                    }

                default:
                    {
                        if (fresh && lead)
                            ClickFx(m);

                        npc.velocity = Vector2.Lerp(npc.velocity, new Vector2(mySide * 7f, -2f), 0.1f);
                        Turn(npc, p.Center - npc.Center, 0.1f);

                        if (lead && Authority && timer >= BinPartTicks)
                            EndDuo(npc, other, twin);

                        break;
                    }
            }
        }

        /// <summary>They lock together (or let go): a click, sparks of both their colours.</summary>
        private static void ClickFx(Vector2 at)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Unlock with { Volume = 1f, Pitch = -0.3f }, at, 6);
            Sound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.5f }, at, 6);

            for (int i = 0; i < 12; i++)
                PADFire.Spark(at, Main.rand.NextVector2Circular(7f, 7f), Main.rand.Next(14, 28), i % 2 == 0 ? FirePalette.Fire : FirePalette.Toxic);

            Shake(at, 5f, 10);
        }

        // ---- GAS ----------------------------------------------------------------------------------------------------------------

        private const int GasShake = 0, GasVomit = 1, GasLight = 2, GasDive = 3, GasAfter = 4;
        private const int GasShakeTicks = 45, GasVomitTicks = 420, GasLightTicks = 50, GasAfterTicks = 100, GasEvery = 4, GasLeg = 40;

        /// <summary>Where the Retinazer waits, far off and high, on its side of the player.</summary>
        private static Vector2 GasFar(Player p, Twins clock) => p.Center + new Vector2(clock.side * 36f * 16f, -22f * 16f);

        /// <summary>The Spazmatism clear of what it has made, the other side of the player.</summary>
        private static void GasBackOff(NPC npc, Player p, Twins clock)
        {
            Hover(npc, p.Center + new Vector2(-clock.side * 38f * 16f, -24f * 16f), 16f, 0.08f);
            Turn(npc, p.Center - npc.Center, 0.1f);
        }

        private void RunGasLeak(NPC npc, bool lead, bool fresh, Twins clock, Player p, NPC other, Twins twin)
        {
            int t = duoT;

            // Not a fireball till it lights itself.
            if (clock.step < GasLight)
                heat = Math.Max(0f, heat - 0.05f);

            switch (clock.step)
            {
                case GasShake:
                    {
                        if (lead)
                        {
                            Hover(npc, GasFar(p, clock), 14f, 0.07f);
                            Turn(npc, p.Center - npc.Center, 0.12f);

                            if (Authority && timer >= GasShakeTicks)
                                Next(npc, GasVomit);
                        }
                        else
                        {
                            npc.velocity *= 0.85f;
                            shake = 1f + 3.5f * Math.Min(1f, t / (float)GasShakeTicks);

                            if (fresh && !Main.dedServ)
                                Sound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.7f }, npc.Center, 6);
                        }

                        break;
                    }

                case GasVomit:
                    {
                        if (lead)
                        {
                            Hover(npc, GasFar(p, clock), 12f, 0.06f);
                            Turn(npc, p.Center - EyeCentre(npc), 0.12f);

                            // Its cannon struck like a lighter, in fits.
                            if (t % 46 < 22)
                                LighterSparks(npc, 0.3f);

                            if (Authority && timer >= GasVomitTicks)
                                Next(npc, GasLight);

                            break;
                        }

                        if (fresh && !Main.dedServ)
                            SoundEngine.PlaySound(GasHiss, npc.Center);

                        // Lurching from place to place: near the player two times in three, anywhere about them the third.
                        Random dice = clock.Dice(t / GasLeg);
                        bool near = dice.NextDouble() < 0.65;
                        float dx = (float)(dice.NextDouble() * 2.0 - 1.0), dy = (float)(dice.NextDouble() * 2.0 - 1.0);
                        Vector2 off = near ? new Vector2(dx * 14f, dy * 10f) * 16f : new Vector2(dx * 40f, -Math.Abs(dy) * 26f + 6f) * 16f;
                        Hover(npc, p.Center + off, 15f, 0.07f);
                        npc.velocity += new Vector2(MathF.Sin(t * 0.37f), MathF.Cos(t * 0.53f)) * 0.5f;

                        Vector2 heading = npc.velocity.LengthSquared() > 1f ? npc.velocity : Facing(npc);
                        npc.rotation = npc.rotation.AngleLerp(LookAlong(heading) + 0.6f * MathF.Sin(t * 0.21f), 0.2f);
                        shake = 1.5f;

                        Vector2 mouth = Front(npc, 62f);

                        if (Authority && t % GasEvery == 0)
                            TwinsShots.Fumes(npc, mouth, Facing(npc).RotatedByRandom(0.35f) * Main.rand.NextFloat(5f, 9f) + npc.velocity * 0.5f);

                        if (!Main.dedServ)
                        {
                            if (t % 2 == 0)
                                PADFire.Gas(mouth, Facing(npc).RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 5f) + npc.velocity * 0.4f, Main.rand.NextFloat(30f, 50f), Main.rand.Next(30, 50));

                            Lighting.AddLight(mouth, 0.25f, 0.8f, 0.25f);
                        }

                        break;
                    }

                case GasLight:
                    {
                        if (!lead)
                        {
                            GasBackOff(npc, p, clock);
                            break;
                        }

                        float u = Math.Min(1f, t / (float)GasLightTicks);
                        npc.velocity *= 0.9f;
                        heat = Math.Max(heat, Smooth(u));
                        npc.rotation += side * (0.06f + 0.4f * u * u);
                        shake = 2f * u;

                        if (fresh)
                            RollFx(npc, false);

                        if (!Main.dedServ)
                            PADFire.Burn(EyeCentre(npc) + new Vector2(0f, 20f), 12f + 34f * u, 1f + 2.5f * u);

                        if (Authority && timer >= GasLightTicks)
                        {
                            // At the cloud nearest the player (or at the player, if there is none).
                            Vector2 to = TwinsShots.NearestFumes(p.Center, out Vector2 cloud) ? cloud : p.Center;
                            Vector2 way = to - npc.Center;
                            float far = way.Length();
                            way = far > 1f ? way / far : Vector2.UnitY;
                            markA = npc.Center;
                            markB = to + way * 90f;
                            span = MathHelper.Clamp((far + 90f) / 26f, 18f, 60f);
                            Next(npc, GasDive);
                        }

                        break;
                    }

                case GasDive:
                    {
                        if (!lead)
                        {
                            GasBackOff(npc, p, clock);
                            break;
                        }

                        float u = Math.Min(1f, (t + 1f) / Math.Max(1f, span));
                        Vector2 was = npc.Center;
                        Move(npc, Vector2.Lerp(markA, markB, 0.6f * u + 0.4f * u * u));
                        heat = 1f;
                        npc.rotation += side * 0.46f;
                        RollTrail(npc, was);

                        if (fresh)
                            RollFx(npc, true);

                        if (Authority)
                        {
                            // The first cloud it touches goes up; if it touched none, the nearest catches off it.
                            bool lit = TwinsShots.TouchFumes(npc.Center, 76f);

                            if (lit || timer + 1 >= span)
                            {
                                if (!lit)
                                    TwinsShots.LightNearest(npc.Center);

                                Next(npc, GasAfter);
                            }
                        }

                        break;
                    }

                default:
                    {
                        if (!lead)
                        {
                            GasBackOff(npc, p, clock);
                            break;
                        }

                        npc.velocity *= 0.93f;
                        heat = 1f - Smooth(t / 40f);

                        if (t < 30)
                            npc.rotation += side * 0.4f * (1f - t / 30f);
                        else
                            Turn(npc, p.Center - npc.Center, 0.12f);

                        if (Authority && timer >= GasAfterTicks)
                            EndDuo(npc, other, twin);

                        break;
                    }
            }
        }

        // ---- CLASH --------------------------------------------------------------------------------------------------------------

        private const int ClashFace = 0, ClashRun = 1, ClashHit = 2, ClashRest = 3;
        private const int ClashFaceTicks = 70, ClashRunTicks = 22, ClashHitTicks = 50, ClashRestTicks = 30;

        /// <summary>How far back each draws, how near their middles come, and how far each is thrown.</summary>
        private const float ClashBack = 30f * 16f;
        private const float ClashTouch = 52f;
        private const float ClashThrown = 260f;

        private void RunClash(NPC npc, bool lead, bool fresh, Twins clock, Player p, NPC other, Twins twin)
        {
            int t = duoT;

            if (lead && Authority && fresh && step == ClashFace)
            {
                markA = (npc.Center + other.Center) * 0.5f;
                angle = (npc.Center - other.Center).ToRotation();
                npc.netUpdate = true;
            }

            // Where they meet, and the line they meet along (from the Spazmatism to the Retinazer).
            Vector2 m = clock.markA;
            Vector2 axis = clock.angle.ToRotationVector2() * (lead ? 1f : -1f);
            Vector2 back = m + axis * ClashBack, touch = m + axis * ClashTouch;

            switch (clock.step)
            {
                case ClashFace:
                    {
                        Hover(npc, back, 24f, 0.14f);
                        Turn(npc, m - npc.Center, 0.2f);
                        glow = Smooth(t / (float)ClashFaceTicks);
                        shake = 1.5f * glow;

                        if (fresh && !Main.dedServ)
                            Sound(SoundID.Mech with { Volume = 1f, Pitch = lead ? -0.3f : -0.5f }, npc.Center, 6);

                        if (!Main.dedServ && Main.rand.NextFloat() < glow)
                            PADFire.Spark(EyeCentre(npc) + Main.rand.NextVector2Circular(50f, 50f), Main.rand.NextVector2Circular(1.5f, 1.5f) + new Vector2(0f, -1f), Main.rand.Next(14, 26), lead ? FirePalette.Fire : FirePalette.Toxic);

                        if (lead && Authority && timer >= ClashFaceTicks)
                            Next(npc, ClashRun);

                        break;
                    }

                case ClashRun:
                    {
                        float u = Math.Min(1f, (t + 1f) / ClashRunTicks);
                        Vector2 was = npc.Center;
                        Move(npc, Vector2.Lerp(back, touch, u * u));
                        npc.rotation = LookAlong(-axis);
                        glow = 1f;

                        if (fresh && !Main.dedServ)
                            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 1f, Pitch = lead ? 0.1f : -0.1f }, npc.Center, 6);

                        if (!Main.dedServ)
                            PADFire.Trail(was, npc.Center + npc.velocity, 60f, lead ? FirePalette.Fire : FirePalette.Toxic, 0.9f, smokes: false);

                        if (lead && Authority && timer + 1 >= ClashRunTicks)
                            Next(npc, ClashHit);

                        break;
                    }

                case ClashHit:
                    {
                        if (fresh && lead)
                        {
                            if (Authority)
                                TwinsShots.Shockwave(npc, m, clock.angle);

                            ClashFx(m, axis);
                        }

                        // Thrown apart, spinning.
                        float u = Math.Min(1f, (t + 1f) / ClashHitTicks);
                        Move(npc, Vector2.Lerp(touch, m + axis * (ClashTouch + ClashThrown), 1f - (1f - u) * (1f - u)));
                        npc.rotation += (lead ? 1f : -1f) * 0.4f * (1f - u);
                        glow = 1f - u;

                        if (lead && Authority && timer >= ClashHitTicks)
                            Next(npc, ClashRest);

                        break;
                    }

                default:
                    {
                        npc.velocity *= 0.9f;
                        Turn(npc, p.Center - npc.Center, 0.12f);
                        glow = Math.Max(0f, glow - 0.05f);

                        if (lead && Authority && timer >= ClashRestTicks)
                            EndDuo(npc, other, twin);

                        break;
                    }
            }
        }

        /// <summary>They meet: the clang of it, both their fires thrown out across the line they met along, the screen white and shaken.</summary>
        private static void ClashFx(Vector2 at, Vector2 axis)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(Clang, at);
            Sound(SoundID.Item14 with { Volume = 1f, Pitch = -0.6f }, at, 8);
            Sound(SoundID.Thunder with { Volume = 0.6f }, at, 8);

            PADFire.Burst(at + axis * 30f, 84f, FirePalette.Fire, 1f);
            PADFire.Burst(at - axis * 30f, 84f, FirePalette.Toxic, 1f, smokes: false);

            // Thrown out across their line, both ways.
            Vector2 across = new(-axis.Y, axis.X);

            for (int i = 0; i < 60; i++)
            {
                float way = i % 2 == 0 ? 1f : -1f;
                PADFire.Spark(at, across * way * Main.rand.NextFloat(6f, 20f) + axis * Main.rand.NextFloat(-5f, 5f), Main.rand.Next(20, 44), i % 4 < 2 ? FirePalette.Fire : FirePalette.Toxic);
            }

            Lighting.AddLight(at, 2.5f, 2.5f, 1.6f);

            Player me = Main.LocalPlayer;
            float near = Vector2.Distance(me.Center, at);

            if (near < 2400f)
            {
                me.PIS().BumpScreenShake(28f * (1f - near / 2400f), 44);
                Cinematic.Flash(new Color(255, 250, 230), 0.4f * (1f - near / 2400f));
            }
        }

        // ---- what they warn with ----------------------------------------------------------------------------------------------

        private void DrawDuoMarks(NPC npc, SpriteBatch batch, Twins clock)
        {
            bool lead = IsRet(npc);

            if (act == Act.Pincer && clock.step == PinClose)
            {
                int c = duoT % PinEvery;
                Vector2 from = Front(npc, 60f);

                if (lead)
                {
                    if (c < PinWarnAt)
                        DrawWarn(batch, from, MathHelper.PiOver2, 1500f, c / (float)PinWarnAt, true, TwinsArt.FireBright, ColumnWide);
                }
                else if (c >= PinEvery - 12)
                {
                    DrawMark(batch, from, MathHelper.PiOver2, 1200f, 0.5f, (c - (PinEvery - 12)) / 12f, TwinsArt.ToxicBright);
                }
            }
            else if (act == Act.Clash && clock.step <= ClashRun && glow > 0.05f)
            {
                // The line they will meet along.
                float flick = 0.7f + 0.3f * MathF.Sin(Main.GameUpdateCount * 0.9f);
                DestroyerArt.DrawLine(batch, EyeCentre(npc), clock.markA, 3f, DestroyerArt.Lit(TwinsArt.Bright(npc) * (0.5f * glow * flick)));
            }
        }
    }
}
