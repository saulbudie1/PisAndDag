using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// What happens at the ticks his moves mark (Cue): the sound and the light of it on every
    /// machine, and what he lets go of (crescents, the lancet, the blades of light, fire and
    /// the sun) where the world is decided, once. Every machine plays his moves on its own
    /// clock (GildedKnight.Fight), so the swing, the slam and the burst are heard and seen on
    /// each at the tick they happen on it; the shots are the server's, and come to the others
    /// as shots do.
    /// </summary>
    public partial class GildedKnight
    {
        /// <summary>The middle of his body, a little up: where what comes out of him comes from.</summary>
        private Vector2 Chest => Feet + new Vector2(0f, -50f);

        private void Cued(Cue what, Player target)
        {
            KnightRig.Frame f = KnightRig.Build(Pose, Feet, facing, null, SunSwing, GraceSwing);

            switch (what)
            {
                case Cue.Step:
                    Step();
                    break;

                case Cue.Swing:
                    SwingSound(SwingBlade(), false, f);
                    break;

                case Cue.HeavySwing:
                    SwingSound(SwingBlade(), true, f);
                    break;

                case Cue.Slam:
                    Slammed(f);
                    break;

                case Cue.Leap:
                    Leapt();
                    break;

                case Cue.Land:
                    Landed(f);
                    break;

                case Cue.Gather:
                    Gathered(f);
                    break;

                case Cue.Crescent:
                    LetCrescent(f, target, false);
                    break;

                case Cue.RisingCrescent:
                    LetCrescent(f, target, true);
                    break;

                case Cue.Line:
                    LineUp(f);
                    break;

                case Cue.LineLocked:
                    LineLocked(f);
                    break;

                case Cue.Lancet:
                    LetLancet(f);
                    break;

                case Cue.Votive:
                    CallVotive(f);
                    break;

                case Cue.Loose:
                    if (!Main.dedServ)
                    {
                        KnightFx.Glint(f.GraceTip, 22f, 12, KnightFx.Pale);
                        KnightFx.Bloom(f.GraceTip, KnightFx.Gold * 0.5f, 0.3f, 10);
                    }
                    break;

                case Cue.Wheel:
                    Wheeled(f);
                    break;

                case Cue.Glint:
                case Cue.Shing:
                    if (!Main.dedServ)
                    {
                        KnightFx.Glint(f.SunTip, 26f, 18, KnightFx.White);
                        KnightFx.Glint(f.GraceTip, 26f, 18, KnightFx.White);
                    }
                    break;

                case Cue.Cross:
                    KnightFx.Crossed(this);
                    Gleam(KnightRig.Sun, 30);
                    Gleam(KnightRig.Grace, 30);
                    break;

                case Cue.Reel:
                    Rocked();
                    break;

                case Cue.GreatCrescent:
                    LetGreat(f);
                    break;

                case Cue.Burst:
                    // The great crescent bursts by its own clock (KnightGreat.BurstAt, the same tick).
                    break;

                case Cue.Procession:
                    Plunged(f);
                    break;

                case Cue.Grow:
                    Grown(f);
                    break;

                case Cue.Pillar:
                    LongDown(f);
                    break;

                case Cue.Sweep:
                    LongSwept(f);
                    break;

                case Cue.SunForm:
                    SunFormed(f);
                    break;

                case Cue.SunFall:
                    SunBrought(target);
                    break;

                case Cue.Roar:
                    Roared();
                    break;

                case Cue.Ignite:
                    Ignited(f);
                    break;

                case Cue.Pyres:
                    Pyred(f);
                    break;

                case Cue.Dash:
                    Dashed();
                    break;

                case Cue.Kneel:
                    Knelt();
                    break;

                case Cue.Fade:
                    Fading();
                    break;

            }
        }

        // ============================================================
        // HIS BLADES
        // ============================================================

        /// <summary>The blade whose cut is starting now or next in the move (the swing's sound and its light are that blade's).</summary>
        private int SwingBlade()
        {
            if (State != Act.Move || move == null)
                return KnightRig.Sun;

            int best = -1;
            int nearest = int.MaxValue;

            foreach (Strike s in move.Strikes)
            {
                int gap = s.From - Timer;

                if (gap >= -3 && gap < nearest)
                {
                    nearest = gap;
                    best = s.Blade;
                }
            }

            return best < 0 ? KnightRig.Sun : best;
        }

        /// <summary>A blade's swing: its sound (the Sun on fire in his second half), a little light along it.</summary>
        private void SwingSound(int blade, bool heavy, KnightRig.Frame f)
        {
            if (Main.dedServ)
                return;

            Vector2 tip = blade == KnightRig.Sun ? f.SunTip : f.GraceTip;

            if (second && blade == KnightRig.Sun)
                KnightSounds.Play(KnightSounds.FlameSwing with { Pitch = heavy ? -0.25f : 0f }, tip);
            else
                KnightSounds.Play(KnightSounds.Swing with { Pitch = heavy ? -0.3f : blade == KnightRig.Grace ? 0.12f : 0f }, tip);

            if (heavy)
                Gleam(blade, 16);

            if (second && blade == KnightRig.Sun)
                KnightFx.Embers(tip, 5, 10f, 2.4f);
        }

        /// <summary>
        /// Both blades into the ground before him (the twin cut's last blow), or at his end the Grace
        /// driven in: where each point goes in, the ground heaves, sparks and dust, the screen shaken.
        /// </summary>
        private void Slammed(KnightRig.Frame f)
        {
            if (Main.dedServ)
                return;

            float ground = Feet.Y;

            if (State == Act.End)
            {
                // The Grace driven in at his end: seen, not heard.
                Vector2 at = new Vector2(f.GraceTip.X, ground);
                KnightFx.Sparks(at + new Vector2(0f, -4f), new Vector2(-facing * 0.3f, -1f), 10, KnightFx.Gold, 4.5f, 0.9f);
                KnightFx.Dust(at, 1f);
                GroundWave.Slam(at, 10f, 2, 5f, 14);
                KnightFx.Shake(at, 3f, 10);
                return;
            }

            KnightSounds.Play(KnightSounds.Slam, Feet);

            foreach (Vector2 tip in new[] { f.SunTip, f.GraceTip })
            {
                Vector2 at = new Vector2(tip.X, ground);

                KnightFx.Sparks(at + new Vector2(0f, -4f), new Vector2(facing * 0.4f, -1f), 12, second ? KnightFx.Fire : KnightFx.Gold, 6f, 1f);
                KnightFx.Dust(at, 1.3f);
                KnightFx.Bloom(at + new Vector2(0f, -6f), KnightFx.Gold * 0.6f, 0.45f, 18);
                GroundWave.Slam(at, 18f, 4, 8f, 22);

                if (second)
                    KnightFx.Embers(at + new Vector2(0f, -8f), 10, 12f, 3.5f);
            }

            KnightFx.Shake(Feet, 8f, 18);
        }

        /// <summary>He leaves the ground: dust and leaves thrown back, the air he leaves through, afterimages.</summary>
        private void Leapt()
        {
            ghostTicks = Math.Max(ghostTicks, MoveId == Moves.Leap ? (int)Math.Max(20f, arg) + 8 : 22);

            if (Main.dedServ)
                return;

            KnightSounds.Step(Feet);
            KnightSounds.Play(KnightSounds.Swing with { Pitch = -0.55f, Volume = 0.7f }, Feet);
            KnightFx.Dust(Feet, 1.4f);
            KnightFx.Leaves(Feet, 6, 3.2f);
        }

        /// <summary>
        /// He comes down out of the leap, both blades into the ground where he lands: the ground
        /// struck, and a wave of gold running out along it either way (KnightShock), bigger in his
        /// second half; fire out of the ground at it too, then.
        /// </summary>
        private void Landed(KnightRig.Frame f)
        {
            if (Authority)
            {
                IEntitySource source = NPC.GetSource_FromAI();

                foreach (int way in new[] { -1, 1 })
                {
                    Vector2 at = Feet + new Vector2(way * 18f, -15f);
                    Projectile.NewProjectile(source, at, Vector2.Zero, ModContent.ProjectileType<KnightShock>(), 0, 0f, Main.myPlayer, way, second ? 1f : 0f);
                }

                if (second)
                {
                    foreach (int way in new[] { -1, 1 })
                    {
                        for (int k = 1; k <= 2; k++)
                        {
                            float x = Feet.X + way * (70f + 90f * k);
                            float ground = GroundUnder(new Vector2(x, Feet.Y - 40f), out bool found);

                            if (found)
                                Projectile.NewProjectile(source, new Vector2(x, ground - 8f), Vector2.Zero, ModContent.ProjectileType<KnightPyre>(), 0, 0f, Main.myPlayer, 18 + 10 * k);
                        }
                    }
                }
            }

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Slam, Feet);
            KnightSounds.Play(KnightSounds.Shockwave, Feet);
            KnightFx.Dust(Feet, 2.2f);
            KnightFx.Leaves(Feet, 10, 4f);
            KnightFx.Bloom(Feet + new Vector2(0f, -10f), KnightFx.Gold, 1f, 26);
            KnightFx.Ring(Feet + new Vector2(0f, -6f), 16f, 150f, 26, KnightFx.Gold * 0.8f);
            KnightFx.Sparks(f.SunTip, -Vector2.UnitY, 10, second ? KnightFx.Fire : KnightFx.Gold, 6f, 1.1f);
            KnightFx.Sparks(f.GraceTip, -Vector2.UnitY, 10, KnightFx.Pale, 6f, 1.1f);
            GroundWave.Slam(Feet, 30f, 8, 11f, 32);
            KnightFx.Shake(Feet, 11f, 24);
        }

        /// <summary>The blades a move lets light out of, by the move.</summary>
        private (bool sun, bool grace) Gathering()
        {
            switch (MoveId)
            {
                case Moves.Crescents:
                case Moves.Lancet:
                case Moves.Votive3:
                case Moves.Votive4:
                case Moves.Votive5:
                case Moves.Crown:
                case Moves.Long:
                    return (false, true);

                case Moves.Great:
                case Moves.Procession:
                case Moves.Sun:
                    return (true, false);

                default:
                    return (true, true);
            }
        }

        /// <summary>Light drawn into a blade before what it lets go of: it brightens, motes and sparks coming in to it.</summary>
        private void Gathered(KnightRig.Frame f)
        {
            (bool sun, bool grace) = Gathering();

            gatherSun |= sun;
            gatherGrace |= grace;

            if (Main.dedServ)
                return;

            if (MoveId != Moves.Lancet)
            {
                Vector2 at = sun ? f.SunTip : f.GraceTip;
                KnightSounds.Play(KnightSounds.LancetCharge with { Volume = 0.5f, Pitch = sun && second ? -0.25f : 0.05f }, at);
            }

            if (sun)
                Drawn(f.SunGuard, f.SunTip, second ? KnightFx.Fire : KnightFx.Gold);

            if (grace)
                Drawn(f.GraceGuard, f.GraceTip, second ? KnightFx.White : KnightFx.Pale);
        }

        /// <summary>Shards of light coming in to a blade from round it.</summary>
        private static void Drawn(Vector2 guard, Vector2 tip, Color colour)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 onBlade = Vector2.Lerp(guard, tip, Main.rand.NextFloat(0.2f, 1f));
                Vector2 from = onBlade + Main.rand.NextVector2CircularEdge(70f, 70f);
                Vector2 v = (onBlade - from) / Main.rand.NextFloat(14f, 22f);

                PADParticleHandler.SpawnParticle(new SparkShard(from, v, colour, 8f, 1.3f, 20f, 18, false, true, 1f));
            }

            KnightFx.Motes(tip, 4, 16f, colour);
        }

        // ============================================================
        // WHAT HE LETS GO OF
        // ============================================================

        /// <summary>
        /// A crescent of light off a blade: the Grace's along the ground at them (in his second
        /// half a second with it, rising a little); the Sun's rising at them, from low.
        /// </summary>
        private void LetCrescent(KnightRig.Frame f, Player target, bool rising)
        {
            if (!Main.dedServ)
            {
                Vector2 tip = rising ? f.SunTip : f.GraceTip;
                KnightFx.Bloom(tip, rising ? KnightFx.Gold : KnightFx.Pale, 0.55f, 18);
                KnightFx.Sparks(tip, new Vector2(facing, rising ? -0.6f : 0f), 8, rising ? KnightFx.Gold : KnightFx.Pale, 6f, 0.5f, false);
                Gleam(rising ? KnightRig.Sun : KnightRig.Grace, 12);
            }

            if (!Authority)
                return;

            IEntitySource source = NPC.GetSource_FromAI();
            int type = ModContent.ProjectileType<KnightCrescent>();
            float burning = second ? 1f : 0f;

            if (!rising)
            {
                Vector2 at = Feet + new Vector2(facing * 44f, -30f);
                Projectile.NewProjectile(source, at, new Vector2(facing * 10.5f, 0f), type, 0, 0f, Main.myPlayer, 0f, burning);

                if (second)
                    Projectile.NewProjectile(source, at + new Vector2(0f, -10f), new Vector2(facing * 10f, 0f).RotatedBy(-facing * 0.2f), type, 0, 0f, Main.myPlayer, 0f, burning);

                return;
            }

            // Rising: at them, but never down at the ground nor straight up.
            Vector2 from = Feet + new Vector2(facing * 46f, -56f);
            Vector2 to = target != null ? target.Center - from : new Vector2(facing, -0.3f);
            float angle = (float)Math.Atan2(to.Y, Math.Max(1f, to.X * facing));
            angle = MathHelper.Clamp(angle, -0.85f, 0.12f);

            Vector2 way = new Vector2(facing * (float)Math.Cos(angle), (float)Math.Sin(angle));
            Projectile.NewProjectile(source, from, way * 11.5f, type, 0, 0f, Main.myPlayer, 1f, burning);
        }

        /// <summary>The lancet's line shows (the drawing follows the way the fight aims it), and the charge is heard.</summary>
        private void LineUp(KnightRig.Frame f)
        {
            lineShown = 0.01f;
            lineLocked = false;

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.LancetCharge, f.GraceTip);
            KnightFx.Glint(f.GraceTip, 24f, 16, KnightFx.Pale);
        }

        /// <summary>The line stops following them: it flashes, and holds.</summary>
        private void LineLocked(KnightRig.Frame f)
        {
            lineLocked = true;
            lineFlash = 1f;

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Point with { Pitch = 0.35f, Volume = 0.8f }, f.GraceTip);
            KnightFx.Glint(f.GraceTip, 34f, 18, KnightFx.White);
        }

        /// <summary>The thrust: the lance of light from the Grace's point down the line (KnightLancet).</summary>
        private void LetLancet(KnightRig.Frame f)
        {
            lineShown = 0f;
            Gleam(KnightRig.Grace, 10);

            if (Authority)
            {
                float world = facing > 0 ? arg : MathHelper.Pi - arg;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), f.GraceTip, Vector2.Zero, ModContent.ProjectileType<KnightLancet>(), 0, 0f, Main.myPlayer, world);

                // His second half: two more, either side of it, a breath behind (KnightLancet's own delay).
                if (second)
                {
                    foreach (float off in new[] { -0.16f, 0.16f })
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), f.GraceTip, Vector2.Zero, ModContent.ProjectileType<KnightLancet>(), 0, 0f, Main.myPlayer, world + off, 8f);
                }
            }
        }

        /// <summary>
        /// A blade of light called up: the next in the row over him (or in the crown's ring), its
        /// moment to be loosed counted from now to the move's matching Loose.
        /// </summary>
        private void CallVotive(KnightRig.Frame f)
        {
            if (!Main.dedServ)
            {
                KnightFx.Glint(f.GraceTip, 26f, 14, KnightFx.Pale);
                Gleam(KnightRig.Grace, 10);
            }

            if (!Authority || move == null)
                return;

            // Which of the move's blades this is, and the tick its Loose comes at.
            int index = 0;
            int looseAt = -1;
            int votives = 0;
            int looses = 0;

            foreach ((int Tick, Cue What) cue in move.Cues)
            {
                if (cue.What == Cue.Votive && cue.Tick < Timer)
                    votives++;
            }

            index = votives;

            foreach ((int Tick, Cue What) cue in move.Cues)
            {
                if (cue.What != Cue.Loose)
                    continue;

                if (looses == index)
                {
                    looseAt = cue.Tick;
                    break;
                }

                looses++;
            }

            if (looseAt < 0)
                return;

            float which = MoveId == Moves.Crown ? 100f + index : index;
            float loose = looseAt - Timer + 1;

            Projectile.NewProjectile(NPC.GetSource_FromAI(), f.GraceTip, Vector2.Zero, ModContent.ProjectileType<KnightVotive>(), 0, 0f, Main.myPlayer,
                which, loose, NPC.whoAmI);
        }

        /// <summary>The wheel begins: a ring of light off him, the leaves going up, afterimages through it.</summary>
        private void Wheeled(KnightRig.Frame f)
        {
            ghostTicks = Math.Max(ghostTicks, State == Act.Change ? 48 : KnightMoves.WheelSpun - KnightMoves.WheelWind + 4);

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Swing with { Pitch = -0.2f }, Chest);
            KnightFx.Ring(Chest, 20f, 120f, 26, (second || State == Act.Change ? KnightFx.Fire : KnightFx.Gold) * 0.75f, NPC);
            KnightFx.Leaves(Feet, 8, 3f);
            Gleam(KnightRig.Sun, 14);
            Gleam(KnightRig.Grace, 14);

            if (State == Act.Change)
            {
                KnightFx.Embers(f.SunTip, 12, 10f, 3f);
                KnightFx.Motes(f.GraceTip, 8, 14f, KnightFx.White);
            }
        }

        /// <summary>Rocked back (his change, his end): the clash of it on his plate, gold leaf off him.</summary>
        private void Rocked()
        {
            if (Main.dedServ)
                return;

            // (His end is without a sound: by request.)
            if (State != Act.End)
                KnightSounds.Play(KnightSounds.Clash with { Pitch = -0.3f }, Chest);

            KnightFx.Gilt(Chest + new Vector2(0f, -8f), State == Act.End ? 9 : 6, 12f, new Vector2(-facing * 1.6f, -1.2f));
            KnightFx.Sparks(Chest, new Vector2(-facing, -0.4f), 8, KnightFx.Gold, 5f, 0.9f);
            KnightFx.Shake(Chest, State == Act.End ? 6f : 4f, 14);
        }

        /// <summary>The Sun swept through its great arc: the great crescent of fire, held a breath and burst (KnightGreat).</summary>
        private void LetGreat(KnightRig.Frame f)
        {
            if (!Main.dedServ)
            {
                Gleam(KnightRig.Sun, 14);
                KnightFx.Embers(f.SunTip, 16, 16f, 4f);
            }

            if (Authority)
            {
                Vector2 at = Feet + new Vector2(facing * 6f, -44f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<KnightGreat>(), 0, 0f, Main.myPlayer, facing, 0f, NPC.whoAmI);
            }
        }

        /// <summary>The Sun driven into the ground before him: the ground heaves, and fire runs out from it at them (KnightProcession).</summary>
        private void Plunged(KnightRig.Frame f)
        {
            Vector2 at = new Vector2(f.SunTip.X, Feet.Y);
            plunge = at;

            if (Authority)
                Projectile.NewProjectile(NPC.GetSource_FromAI(), at + new Vector2(0f, -8f), Vector2.Zero, ModContent.ProjectileType<KnightProcession>(), 0, 0f, Main.myPlayer, facing, 0f, NPC.whoAmI);

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Slam, at);
            KnightFx.Sparks(at + new Vector2(0f, -4f), -Vector2.UnitY, 16, KnightFx.Fire, 7f, 1f);
            KnightFx.Embers(at + new Vector2(0f, -10f), 20, 14f, 4f);
            KnightFx.Bloom(at + new Vector2(0f, -10f), KnightFx.Fire, 0.9f, 30);
            KnightFx.Dust(at, 1.4f);
            GroundWave.Slam(at, 20f, 5, 9f, 24);
            KnightFx.Shake(at, 7f, 16);
        }

        /// <summary>The long blade grows out of the Grace (the drawing grows it, from now to his raise).</summary>
        private void Grown(KnightRig.Frame f)
        {
            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.GrowBlade, f.GraceTip);
            KnightFx.Bloom(f.GraceTip, KnightFx.White, 0.7f, 30);
            KnightFx.Motes(f.GraceTip, 10, 20f, KnightFx.White);
            KnightFx.Flash(f.GraceTip, new Color(255, 236, 190), 0.06f);
        }

        /// <summary>The long blade comes down onto them: a pillar of light where its point lands (KnightPillar).</summary>
        private void LongDown(KnightRig.Frame f)
        {
            Vector2 landing = LongLanding(f);
            longLanding = landing;

            if (Authority)
                Projectile.NewProjectile(NPC.GetSource_FromAI(), landing, Vector2.Zero, ModContent.ProjectileType<KnightPillar>(), 0, 0f, Main.myPlayer);

            if (Main.dedServ)
                return;

            KnightFx.Dust(landing, 1.6f);
            KnightFx.Leaves(landing, 10, 4f);
        }

        /// <summary>The long blade swept along the ground the length of the field (KnightSweep); it is only the Grace again after.</summary>
        private void LongSwept(KnightRig.Frame f)
        {
            if (Authority)
            {
                Vector2 at = new Vector2(Feet.X + facing * 26f, Feet.Y - 16f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), at, Vector2.Zero, ModContent.ProjectileType<KnightSweep>(), 0, 0f, Main.myPlayer, facing);
            }

            if (!Main.dedServ)
                KnightFx.Bloom(f.GraceTip, KnightFx.White, 0.8f, 20);
        }

        /// <summary>The gilded sun forms over the Sun's point (KnightSunOrb), its landing where they stood when he began.</summary>
        private void SunFormed(KnightRig.Frame f)
        {
            if (!Authority)
                return;

            Vector2 landing = sunAt;
            float ground = GroundUnder(new Vector2(landing.X, landing.Y - 20f), out bool found);

            if (found)
                landing.Y = ground;

            Projectile.NewProjectile(NPC.GetSource_FromAI(), f.SunTip + new Vector2(0f, -70f), Vector2.Zero, ModContent.ProjectileType<KnightSunOrb>(), 0, 0f, Main.myPlayer,
                landing.X, landing.Y, NPC.whoAmI);
        }

        /// <summary>The sun brought down: where it lands is where they stand now (the server says so to the orb, and it tells the rest).</summary>
        private void SunBrought(Player target)
        {
            if (!Authority)
                return;

            Vector2 landing = target != null && target.active && !target.dead ? target.Bottom : sunAt;
            float ground = GroundUnder(new Vector2(landing.X, landing.Y - 20f), out bool found);

            if (found)
                landing.Y = ground;

            if (fieldRight > fieldLeft)
                landing.X = MathHelper.Clamp(landing.X, fieldLeft + 40f, fieldRight - 40f);

            int type = ModContent.ProjectileType<KnightSunOrb>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type != type || (int)p.ai[2] != NPC.whoAmI)
                    continue;

                p.ai[0] = landing.X;
                p.ai[1] = landing.Y;
                p.netUpdate = true;
            }
        }

        /// <summary>His roar at his change: a ring of gold out of him, the screen dimmed and shaken (Cinematic.Roar, rimmed in gold).</summary>
        private void Roared()
        {
            if (Main.dedServ)
                return;

            Cinematic.Roar(NPC, 80, 1f, quiet: true, rim: KnightFx.Gold);
            KnightSounds.Play(KnightSounds.Ward with { Pitch = -0.45f }, Chest);
            KnightSounds.Play(KnightSounds.Shockwave with { Pitch = -0.2f, Volume = 0.7f }, Chest);
            KnightFx.Bloom(Chest, KnightFx.Gold, 1.6f, 50);
            KnightFx.Ring(Chest, 20f, 240f, 40, KnightFx.Gold);
            KnightFx.Gilt(Chest, 10, 14f, new Vector2(0f, -2f));
            KnightFx.Leaves(Feet, 14, 4.5f);
        }

        /// <summary>The blades take fire and light: the Sun burning, the Grace white.</summary>
        private void Ignited(KnightRig.Frame f)
        {
            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Ignite, Chest);
            KnightFx.Flash(Chest, new Color(255, 190, 110), 0.2f);
            KnightFx.Bloom(f.SunTip, KnightFx.Fire, 1.1f, 40);
            KnightFx.Bloom(f.GraceTip, KnightFx.White, 1.1f, 40);
            KnightFx.Ring(Chest, 30f, 200f, 36, KnightFx.Fire * 0.8f);

            for (int i = 0; i < 12; i++)
                KnightFx.Embers(Vector2.Lerp(f.SunGuard, f.SunTip, i / 11f), 2, 4f, 2.5f);

            for (int i = 0; i < 8; i++)
                KnightFx.Motes(Vector2.Lerp(f.GraceGuard, f.GraceTip, i / 7f), 1, 6f, KnightFx.White);

            Gleam(KnightRig.Sun, 24);
            Gleam(KnightRig.Grace, 24);
            sunLit = graceLit = 1.6f;
        }

        /// <summary>Both blades into the ground at the end of his change: fire bursts up out of it about him, outward in turn (KnightPyre).</summary>
        private void Pyred(KnightRig.Frame f)
        {
            if (Authority)
            {
                IEntitySource source = NPC.GetSource_FromAI();
                int type = ModContent.ProjectileType<KnightPyre>();
                float[] out_ = { 70f, 140f, 210f, 280f };

                for (int k = 0; k < out_.Length; k++)
                {
                    foreach (int way in new[] { -1, 1 })
                    {
                        float x = Feet.X + way * out_[k];

                        if (fieldRight > fieldLeft && (x < fieldLeft + 10f || x > fieldRight - 10f))
                            continue;

                        float ground = GroundUnder(new Vector2(x, Feet.Y - 40f), out bool found);

                        if (found)
                            Projectile.NewProjectile(source, new Vector2(x, ground - 8f), Vector2.Zero, type, 0, 0f, Main.myPlayer, 14 + 8 * k);
                    }
                }
            }

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Slam, Feet);
            KnightFx.Dust(Feet, 2f);
            KnightFx.Embers(Feet + new Vector2(0f, -10f), 24, 30f, 4f);
            GroundWave.Slam(Feet, 30f, 8, 9f, 30);
            KnightFx.Shake(Feet, 9f, 20);
        }

        /// <summary>A dash: the ground left in a blur of dust and leaves, afterimages behind him.</summary>
        private void Dashed()
        {
            ghostTicks = Math.Max(ghostTicks, KnightMoves.DashTicks + 6);

            if (Main.dedServ)
                return;

            KnightSounds.Play(KnightSounds.Swing with { Pitch = -0.6f, Volume = 0.8f }, Feet);
            KnightFx.Dust(Feet, 1.3f);
            KnightFx.Leaves(Feet, 6, 3.4f);
        }

        /// <summary>Down on one knee (his end): the plate on the ground, dust.</summary>
        private void Knelt()
        {
            if (Main.dedServ)
                return;

            // His end is without a sound (by request); the stun's knee has one.
            if (State != Act.End)
                KnightSounds.Play(KnightSounds.Kneel, Feet);

            KnightFx.Dust(Feet + new Vector2(-facing * 8f, 0f), 0.9f);
            KnightFx.Gilt(Chest, 3, 10f, new Vector2(0f, -1f));
            KnightFx.Shake(Feet, 3f, 10);
        }

        /// <summary>The gold begins to smoulder off him (Smoulder, and KnightFx.Smouldering every tick from here). No sound.</summary>
        private void Fading()
        {
            if (Main.dedServ)
                return;

            KnightFx.Bloom(Chest + new Vector2(0f, 14f), KnightFx.Gold * 0.6f, 0.9f, 90);
        }

        /// <summary>
        /// He comes apart, as the Guide does but in gold (by request): his picture as it was last
        /// painted, every filled pixel of it a golden grain where it was drawn (KnightGrain); from
        /// now nothing of him is drawn but those (Apart). On every machine that draws him; no sound.
        /// </summary>
        private void CameApart()
        {
            if (Main.dedServ)
                return;

            Texture2D body = KnightCanvas.Body(NPC.whoAmI);
            Color[] pixels = KnightGrain.Read(body);

            if (pixels != null)
            {
                // Where SpriteBatch put each of its pixels: his feet at the picture's origin, side on
                // (he is still), cut off at the ground as he is drawn.
                Vector2 feet = Feet;
                KnightGrain.FromPixels(pixels, body.Width, body.Height, 0, CanvasBottom(),
                    (x, y) => feet + new Vector2(x - KnightCanvas.Origin.X, y - KnightCanvas.Origin.Y), new Vector2(0f, -0.2f));
            }

            KnightFx.Apart(this);
        }

        /// <summary>The Sun's picture's pixels, read once (it never changes).</summary>
        private static Color[] sunPixels;

        /// <summary>The Sun where it fell goes after him: its picture as it lies, into golden grains.</summary>
        private void SunComesApart()
        {
            if (Main.dedServ || !dropped)
                return;

            Texture2D tex = KnightCanvas.Piece(KnightRig.SunBlade);

            if (tex == null)
                return;

            sunPixels ??= KnightGrain.Read(tex);

            if (sunPixels == null)
                return;

            // As DrawDropped lays it: SpriteBatch mirrors the picture inside its quad, not the quad.
            KnightRig.Part p = KnightRig.LooseSword(KnightRig.Sun, droppedGrip, droppedAngle);
            int width = tex.Width;

            KnightGrain.FromPixels(sunPixels, width, tex.Height, 0, tex.Height, (x, y) =>
            {
                Vector2 local = (new Vector2(p.FlipX ? width - x : x, y) - p.Origin) * p.Scale;
                return p.At + local.RotatedBy(p.Rotation);
            }, new Vector2(0f, -0.15f), KnightGrain.SunGrains);

            KnightFx.Bloom(droppedGrip + droppedAngle.ToRotationVector2() * 30f, KnightFx.Gold * 0.5f, 0.7f, 50);
        }
    }
}
