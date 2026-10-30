using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// Calamity's bosses' beams, laser walls, warnings and blasts, told to the copy as the game hits with them and as the
    /// trainer shows them (its bosses/*.cs: a beam as a line, its warning as the line to come with the wait before it
    /// hurts, a turning beam turning). The game itself would tell the copy of most of these as their box, which is tiny
    /// (a laser wall is a 1 px projectile whose Colliding draws a grid of lines) or nothing at all (a telegraph does no
    /// damage).
    ///
    /// The mod does not depend on Calamity and does not reference it. A projectile of Calamity's is known by its mod's
    /// name and its own (ModProjectile.Name); the game's own fields (ai, localAI, timeLeft, velocity, rotation, scale) are
    /// read as they are, and anything of Calamity's own (a field, a property, a method) is found by reflection once for
    /// each kind of projectile and kept. A kind whose members are not found (another version of Calamity) is asked where
    /// it hurts as a shape (MimicBeams.Probe), or, if it does no harm of its own, is not told of.
    ///
    /// Any other mod's hostile shot whose ModProjectile has a Colliding of its own, and is not one of these, is asked
    /// where it hurts as a shape too while it is near, and from the first time it is seen to hurt out past its own box
    /// it is always asked so; a kind seen often enough to hurt only about its box is left to be told of as its box.
    /// </summary>
    public static class MimicCalamity
    {
        private const string CalamityName = "CalamityMod";

        private enum Kind : byte
        {
            /// <summary>Told of as its box (MimicSense).</summary>
            None,
            Laserbeam,
            AresArmBeam,
            BrimstoneRay,
            HolyRay,
            AresDeathWarn,
            ArtemisDeathWarn,
            ThanatosWarn,
            BrimstoneWarn,
            ArtemisChargeWarn,
            ApolloChargeWarn,
            WarnedLaser,
            LaserWalls,
            BigBeam,
            Boom,
            PlasmaBurst,
            TeslaOrb,
            Border,
            Probe,
        }

        /// <summary>What is known of one kind of projectile, found the first time one is seen.</summary>
        private sealed class Info
        {
            public Kind Kind;

            // A BaseLaserbeamProjectile's properties, and whether it turns as the base turns it (by ai[0] a tick) and hurts
            // only from half its size.
            public PropertyInfo LaserLength, Time, Lifetime, MaxScale, ExpandRate;
            public bool OwnMotion, ScaleGate;

            // The laser walls' and the big beam's fields; a warned laser's held velocity; Apollo's charge points; the big
            // beam's length; the tesla orb's partner.
            public FieldInfo WallTime, AttackTime, Done, Glow, Stored, BeamStart, BeamDir, Held, Charges;
            public PropertyInfo BeamLength;
            public MethodInfo Partner;

            /// <summary>A warning's: the damage, as Calamity gives it, of the beam it warns of.</summary>
            public int Raw;

            /// <summary>A warned laser of Artemis's (ArtemisLaser): its wait kept in localAI[1], and speeding up after.</summary>
            public bool Artemis;

            /// <summary>Asked as a shape: it has been seen to hurt out past its own box (or is known to).</summary>
            public bool Shape;

            /// <summary>Times it was asked near the copy and hurt only about its own box.</summary>
            public int BoxLooks;
        }

        private static Info[] infos = new Info[0];

        // ---- telling ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// A shot of Calamity's read as the game hits with it, or another mod's asked where it hurts: its threats written
        /// into <paramref name="into"/> from <paramref name="n"/> on, and true. False for anything to be told of as its box.
        /// </summary>
        public static bool Read(Projectile p, Threat proto, Threat[] into, ref int n, Vector2 me, Rectangle body)
        {
            if (p.ModProjectile == null)
                return false;

            Info info = InfoOf(p);

            if (info.Kind == Kind.None)
                return false;

            try
            {
                return ReadAs(p, info, proto, into, ref n, me, body);
            }
            catch (Exception)
            {
                // Something of it is not as this reads it (another version): asked as a shape from now on, or told of as
                // its box if asking it fails too.
                info.Kind = info.Kind == Kind.Probe ? Kind.None : Kind.Probe;
                return false;
            }
        }

        /// <summary>
        /// A harmless shot of Calamity's that warns of a beam to come (or of a boss's charge), and what that will land for:
        /// the beam's damage as Calamity gives it (the boss's static, its comment's Expert number halved twice), twice it
        /// as any hostile shot (Projectile.Damage), times the difficulty's (as MimicBeams.HitOf); for a charge, the boss's
        /// touch (NPC.defDamage, as the game gave it at its spawn).
        /// </summary>
        public static bool Warns(Projectile p, out int hit)
        {
            hit = 0;

            if (p.friendly || p.ModProjectile == null)
                return false;

            Info info = InfoOf(p);

            switch (info.Kind)
            {
                case Kind.AresDeathWarn:
                case Kind.ArtemisDeathWarn:
                case Kind.ThanatosWarn:
                case Kind.BrimstoneWarn:
                    hit = (int)(info.Raw * 2 * Main.GameModeInfo.EnemyDamageMultiplier);
                    return true;

                case Kind.ArtemisChargeWarn:
                case Kind.ApolloChargeWarn:
                {
                    int o = (int)p.ai[1];

                    if (Main.npc.IndexInRange(o) && Main.npc[o].active)
                        hit = Math.Max(Main.npc[o].damage, Main.npc[o].defDamage);

                    return true;
                }
            }

            return false;
        }

        private static bool ReadAs(Projectile p, Info info, Threat proto, Threat[] into, ref int n, Vector2 me, Rectangle body)
        {
            object mp = p.ModProjectile;

            switch (info.Kind)
            {
                // Calamity's BaseLaserbeamProjectile (BaseProjectiles/BaseLaserbeamProjectile.cs) and the bosses' beams made
                // from it (AresDeathBeamStart, ThanatosBeamStart, ArtemisSpinLaserbeam; Ravager's and Permafrost's too): a
                // line from its middle along its velocity (kept a unit), LaserLength long, as wide as its box's diagonal
                // times its scale (Colliding: Size.Length() * scale); the scale a sine of Time over Lifetime, times
                // ScaleExpandRate, at most MaxScale (DetermineScale), so near its full width for all but its ends; it dies
                // at Lifetime. It turns RotationalSpeed (ai[0]) a tick (UpdateLaserMotion) unless its own UpdateLaserMotion
                // steers it (the Exo Mechs' follow their boss): then as it was seen to turn since last tick. The Exo Mechs'
                // three hurt only from half their size (CanHitPlayer: scale >= 0.5): from Time 24 of Ares's and Artemis's
                // 600, 8 of Thanatos's 180 (a wait, Delay), to as long before their end.
                case Kind.Laserbeam:
                {
                    float length = F(info.LaserLength.GetValue(mp));
                    float time = F(info.Time.GetValue(mp));
                    float lifetime = F(info.Lifetime.GetValue(mp));
                    float maxScale = info.MaxScale != null ? F(info.MaxScale.GetValue(mp)) : 1f;
                    float rate = info.ExpandRate != null ? F(info.ExpandRate.GetValue(mp)) : 4f;
                    float angle = p.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out float seenTurn);

                    int delay = 0;
                    int life = (int)(lifetime - time);

                    if (info.ScaleGate && rate * maxScale > 0.5f)
                    {
                        float first = lifetime * (float)Math.Asin(0.5f / (rate * maxScale)) / MathHelper.Pi;
                        delay = Math.Max(0, (int)Math.Ceiling(first - time));
                        life = (int)(lifetime - first - time) + 1;
                    }

                    if (life <= delay)
                        return true;

                    float scale = time < lifetime * 0.5f ? Math.Max(p.scale, maxScale) : p.scale;
                    Threat th = LineOf(proto, p.Center, angle, Math.Max(length, 16f), p.Size.Length() * scale * 0.5f);
                    th.Vel = vel;
                    th.Turn = info.OwnMotion ? seenTurn : p.ai[0];
                    th.Delay = delay;
                    th.Life = Math.Max(1, life);
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Ares's laser arm's beam (AresLaserBeamStart, its own AI): from under or beside the arm (its middle) along
                // its velocity, as long as localAI[1] (scanned to the first blocks; all 2400 when the arm cannot see its
                // target), 30 wide at full size (Colliding), which it reaches in its first tick (scale = 10 sin); 60 ticks
                // (localAI[0]). It keeps its way; the arm carries it across (told as its foot was seen to move).
                case Kind.AresArmBeam:
                {
                    float angle = p.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out float turn);
                    Threat th = LineOf(proto, p.Center, angle, Math.Max(p.localAI[1], 16f), 15f);
                    th.Vel = vel;
                    th.Turn = turn;
                    th.Life = Math.Max(1, 60 - (int)p.localAI[0]);
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // The Brimstone Elemental's ray (BrimstoneRay, Revengeance's laser state): from beside her head along its
                // velocity, localAI[1] long (scanned to blocks), 22 wide (Colliding: 22 * scale, full within two ticks);
                // 45 ticks (localAI[0]); turning a degree a tick her way only when made so (ai[0] > 0): as seen.
                case Kind.BrimstoneRay:
                {
                    float angle = p.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out float turn);
                    Threat th = LineOf(proto, p.Center, angle, Math.Max(p.localAI[1], 16f), 11f);
                    th.Vel = vel;
                    th.Turn = turn;
                    th.Life = Math.Max(1, 45 - (int)p.localAI[0]);
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Providence's Holy Ray (ProvidenceHolyRay): from 32 below her middle along its velocity, localAI[1] long
                // (scanned to blocks), 22 wide at full size (Colliding: 22 * scale; full within a few ticks, harmless
                // below half of it, CanHitPlayer), turning ai[0] a tick; the hit player's box made ai[2] times as big first
                // (ExtraWidth, 2: so the line is wider by the box's own reach across it, as the trainer has it); 180 ticks
                // (localAI[0]; 100 in her scissor form, not known here: the longer is told).
                case Kind.HolyRay:
                {
                    float age = p.localAI[0];

                    if (age >= 20f && p.scale < 0.5f)
                        return true;

                    Vector2 dir = p.velocity.SafeNormalize(Vector2.UnitY);
                    float angle = dir.ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out _);
                    float extra = Math.Max(1f, p.ai[2]) - 1f;
                    float across = Math.Abs(dir.Y) * body.Width * 0.5f + Math.Abs(dir.X) * body.Height * 0.5f;
                    float scale = age < 20f ? Math.Max(p.scale, 1f) : p.scale;
                    Threat th = LineOf(proto, p.Center, angle, Math.Max(p.localAI[1], 16f), 11f * scale + extra * across);
                    th.Vel = vel;
                    th.Turn = p.ai[0];
                    th.Life = Math.Max(1, 180 - (int)age);
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Ares's deathray warnings (AresDeathBeamTelegraph, damage 0): each a line from under his middle along the
                // way its beam will go (its velocity until its first tick, then kept in OldVelocity and its rotation less
                // a quarter turn); it lasts as long as the warning (timeLeft: 120 ticks in Expert), and his eight
                // AresDeathBeamStart come then along the same ways: 2400 long, 42 wide, hurting from their 24th tick to
                // their 576th. Told of as that beam, waiting out the warning and those first ticks (the trainer's: Delay).
                case Kind.AresDeathWarn:
                case Kind.ArtemisDeathWarn:
                {
                    Vector2 dir = p.velocity != Vector2.Zero ? p.velocity.SafeNormalize(Vector2.UnitY) : (p.rotation - MathHelper.PiOver2).ToRotationVector2();
                    float angle = dir.ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out _);

                    // Artemis's (ArtemisDeathrayTelegraph, damage 0): from 70 before her along her spin's start, 60 ticks;
                    // then ArtemisSpinLaserbeam from 50 before her, 4800 long (to blocks while she sees her target), 42
                    // wide, hurting from its 24th tick of 600, swept round by her spin.
                    float length = info.Kind == Kind.ArtemisDeathWarn ? 4800f : 2400f;
                    Threat th = LineOf(proto, p.Center, angle, length, 21.21f);
                    th.Vel = vel;
                    th.Delay = Math.Max(1, p.timeLeft + 23);
                    th.Life = th.Delay + 553;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Thanatos's deathray warning (ThanatosBeamTelegraph: not even hostile; seven of them, ai[0] its head): six
                // swing in from +-0.74 rad and the seventh (ai[1] 0) lies along the head's heading all along, where they
                // all end (ConvergenceAngle 0) and where ThanatosBeamStart then fires (UpdateLaserMotion: the head's
                // velocity) from 174 before the head's middle, 3600 long, 56.6 wide, hurting from its 8th tick of 180 to
                // its 172nd. Told of once, by the seventh, as that beam turning as the head was seen to turn.
                case Kind.ThanatosWarn:
                {
                    if (Math.Abs(p.ai[1]) > 0.001f)
                        return true;

                    int h = (int)p.ai[0];

                    if (!Main.npc.IndexInRange(h) || !Main.npc[h].active)
                        return true;

                    NPC head = Main.npc[h];
                    Vector2 dir = head.velocity.SafeNormalize(Vector2.UnitY);
                    float angle = dir.ToRotation();
                    Vector2 foot = head.Center + dir * 174f;
                    Motion(p, foot, angle, out _, out float turn);
                    Threat th = LineOf(proto, foot, angle, 3600f, 28.28f);
                    th.Vel = head.velocity;
                    th.Turn = turn;
                    th.Delay = Math.Max(1, p.timeLeft + 8);
                    th.Life = th.Delay + 164;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // The Brimstone Elemental's aiming ray (BrimstoneTargetRay, damage 0; her laser state, ai[1] her): the thin
                // line from beside her head, localAI[1] long, along which her BrimstoneRay will fire (22 wide, 45 ticks).
                // While it follows her target (ai[0] 0) her ai[1] counts to 150 by 1.5 a tick (2 on her second laser), it
                // then holds still (ai[0] 1) while ai[1] counts on by 1 to 180, and the ray fires (BrimstoneElemental.AI).
                case Kind.BrimstoneWarn:
                {
                    int o = (int)p.ai[1];

                    if (!Main.npc.IndexInRange(o) || !Main.npc[o].active)
                        return true;

                    NPC boss = Main.npc[o];
                    int delay = p.ai[0] == 0f
                        ? (int)Math.Ceiling((150f - boss.ai[1]) / (boss.ai[2] == 0f ? 1.5f : 2f)) + 31
                        : (int)(180f - boss.ai[1]) + 1;
                    Threat th = LineOf(proto, p.Center, p.velocity.SafeNormalize(Vector2.UnitY).ToRotation(), Math.Max(p.localAI[1], 64f), 11f);
                    th.Delay = Math.Max(1, delay);
                    th.Life = th.Delay + 45;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Artemis's charge warning (ArtemisChargeTelegraph, damage 0, 45 ticks): a line 2000 long from 50 before
                // her along her charge, riding her. As the trainer has it: 100 either side (her touch), and gone just as
                // her charge begins (Delay = Life: it never hurts itself; her body is the threat then), weighed as her
                // touch.
                case Kind.ArtemisChargeWarn:
                {
                    float angle = p.velocity.SafeNormalize(Vector2.UnitY).ToRotation();
                    Motion(p, p.Center, angle, out Vector2 vel, out _);
                    Threat th = LineOf(proto, p.Center, angle, 2000f, 100f);
                    th.Vel = vel;
                    th.Delay = th.Life = Math.Max(1, p.timeLeft);
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Apollo's charge combo warning (ApolloChargeTelegraph, damage 0, 75 ticks in Expert): the legs between his
                // charge points (ChargePositions), each dashed in about 10 ticks (943 px at 97.5 a tick, Apollo.AI) after
                // the warning ends. As the trainer has it: 100 either side, gone as the dash along it begins.
                case Kind.ApolloChargeWarn:
                {
                    if (!(info.Charges.GetValue(mp) is Vector2[] at))
                        return true;

                    for (int k = 0; k + 1 < at.Length && k < 3; k++)
                    {
                        if (at[k] == Vector2.Zero || at[k + 1] == Vector2.Zero)
                            continue;

                        Vector2 leg = at[k + 1] - at[k];
                        Threat th = LineOf(proto, at[k], leg.ToRotation(), leg.Length(), 100f);
                        th.Delay = th.Life = Math.Max(1, p.timeLeft + 1 + 10 * k);
                        MimicBeams.Add(into, ref n, th, me);
                    }

                    return true;
                }

                // The Exo Mechs' lasers while they wait (ThanatosLaser, THanosSideLaser: TelegraphDelay ai[0] to 60;
                // ArtemisLaser: localAI[1] to 20): still and harmless (CanHitPlayer) while their telegraph line shows,
                // counting up once an update (two a tick then), riding their segment or arm; then off with the velocity
                // held in Velocity (7.5 an update toward the aim, a side laser's 12, Artemis's 1 speeding up by
                // 1 + ai[0] / 600 an update) at four updates a tick. Told of as already flying from where they will be when
                // the wait is over (the trainer's LaunchVel), 14 either side (a circle of 15.6, Colliding). Once flying,
                // their box (MimicSense).
                case Kind.WarnedLaser:
                {
                    float waited = info.Artemis ? p.localAI[1] : p.ai[0];
                    float wait = info.Artemis ? 20f : 60f;

                    if (waited > wait || !(info.Held.GetValue(mp) is Vector2 held) || held == Vector2.Zero)
                        return false;

                    int after = Main.getGoodWorld ? 5 : 4;
                    Vector2 launch = held * after * (info.Artemis ? 1f + p.ai[0] * 0.0016667f : 1f);
                    int delay = (int)Math.Ceiling((wait - waited) / (1 + p.extraUpdates)) + 1;
                    Threat th = proto;
                    th.Line = false;
                    th.Pos = p.Center - launch * delay;
                    th.Vel = launch;
                    th.Acc = Vector2.Zero;
                    th.Homing = 0f;
                    th.HomeTurn = 0f;
                    th.HalfW = th.HalfH = 14f;
                    th.Parryable = false;
                    th.Delay = delay;
                    th.Life = delay + Math.Max(1, (int)((p.timeLeft - (wait - waited)) / after));
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                case Kind.LaserWalls:
                    Walls(p, info, proto, into, ref n, me);
                    return true;

                // The Devourer's big beam (DoGLaserWallsBigBeam, below 25 %): one line from beamStart (3000 px out from
                // where its target stood, turned up to 0.4 rad off an axis) along directionToTarget, twice laserLength
                // long, 30 * scale wide (scale 2: 60), harmless until its time (going up by attackSpeed, ai[0], a tick)
                // reaches attackTime, then hurting while its glow (laserFX: 2.5, less by 7 % a tick) is at least 1
                // (canDamage, Colliding): some 13 ticks.
                case Kind.BigBeam:
                {
                    if (!(info.BeamStart.GetValue(mp) is Vector2 start) || !(info.BeamDir.GetValue(mp) is Vector2 dir) || dir == Vector2.Zero)
                        return true;

                    if (!Timing(p, info, mp, 2.5f, 0.07f, out int delay, out int life))
                        return true;

                    float length = info.BeamLength != null ? F(info.BeamLength.GetValue(mp)) : 3000f;
                    Threat th = LineOf(proto, start, dir.ToRotation(), length * 2f, 15f * p.scale);
                    th.Delay = delay;
                    th.Life = life;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Ares's gauss nuke's blast (AresGaussNukeProjectileBoom, a BaseMassiveExplosionProjectile; of the three the
                // nuke leaves only the last, MaxRadius ai[1] 740, does damage): its radius (ai[0]) eases to ai[1] by a
                // quarter a tick and its scale grows from 1.2 to 5 over its Lifetime (60); it hurts a player whose box
                // a circle of radius * scale * 0.4 reaches, while more than 6 ticks are left, once each (CanHitPlayer).
                // Told of as a circle (a line of no length) as big as it will be at the last tick it hurts (some 1350 px):
                // within it the hit cannot be outrun, but near its edge it can be kept out of.
                case Kind.Boom:
                {
                    if (p.timeLeft <= 6)
                        return true;

                    float lifetime = Math.Max(1f, F(info.Lifetime.GetValue(mp)));
                    float last = MathHelper.Lerp(1.2f, 5f, MathHelper.Clamp((lifetime - 7f) / lifetime, 0f, 1f));
                    Threat th = LineOf(proto, p.Center, 0f, 0f, Math.Max(p.ai[0], p.ai[1]) * last * 0.4f);
                    th.Life = p.timeLeft - 6;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Ares's and Apollo's plasma fireballs (AresPlasmaFireball, 36 px, 120 ticks; ApolloFireball, 48 px, 60
                // ticks): they fly straight on, and when they die (their time out, or blocks once near where their target
                // stood) they become a 90 px box for one hit (OnKill: width = height = 90, Damage()) and throw bolts.
                // Told of as themselves and the burst to come where their time runs out.
                case Kind.PlasmaBurst:
                {
                    MimicBeams.Add(into, ref n, proto, me);
                    int steps = 1 + p.extraUpdates;
                    int ticks = Math.Max(1, (p.timeLeft + steps - 1) / steps);
                    Threat th = proto;
                    th.Line = false;
                    th.Pos = p.Center + p.velocity * p.timeLeft;
                    th.Vel = Vector2.Zero;
                    th.Acc = Vector2.Zero;
                    th.Homing = 0f;
                    th.HomeTurn = 0f;
                    th.HalfW = th.HalfH = 45f;
                    th.Parryable = false;
                    th.Delay = ticks;
                    th.Life = ticks + 1;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                // Ares's tesla orbs (AresTeslaOrb): the orb's box, and the lightning to the next orb of its burst
                // (GetOrbToAttachTo: the one whose ai[0] is one more, within 1200 px in Expert), 8 wide (Colliding).
                case Kind.TeslaOrb:
                {
                    MimicBeams.Add(into, ref n, proto, me);

                    if (info.Partner.Invoke(mp, null) is Projectile next && next.active)
                    {
                        Vector2 arc = next.Center - p.Center;
                        float angle = arc.ToRotation();
                        Motion(p, p.Center, angle, out Vector2 vel, out float turn);
                        Threat th = LineOf(proto, p.Center, angle, arc.Length(), 4f);
                        th.Vel = vel;
                        th.Turn = turn;
                        MimicBeams.Add(into, ref n, th, me);
                    }

                    return true;
                }

                // Yharon's border (InfernadoRevenge): a column from its bottom up 8800 px (TornadoHeight), 72 wide
                // (Colliding), for the whole fight.
                case Kind.Border:
                {
                    Threat th = LineOf(proto, p.Bottom, -MathHelper.PiOver2, 8800f, 36f);
                    th.Life = 0;
                    MimicBeams.Add(into, ref n, th, me);
                    return true;
                }

                case Kind.Probe:
                    return Ask(p, info, proto, into, ref n, me, body);
            }

            return false;
        }

        // ---- the Devourer's laser walls -----------------------------------------------------------------------------------

        private const float Diagonal = 0.70710677f;

        private static readonly float[] nearOff = new float[4];
        private static readonly int[] nearIdx = new int[4];

        /// <summary>
        /// The Devourer's laser walls (DoGLaserWalls: a 1 px projectile; its Colliding draws the lines). Two sets of lines
        /// laserDist (ai[1]) apart, laserCount = 6000 / laserDist of each, each laserLength * 2 long (laserLength =
        /// laserDist * laserCount / 2), 10 wide, about its middle: one set along X spaced along Y, the other along Y spaced
        /// along X, both turned an eighth of a turn for laserType (ai[2]) 1, 3 and 5. For laserType 2 and 3 the first set,
        /// for 4 and 5 the second, is every other line's start aimed at storedTargetPos (where the nearest player stood
        /// when they came) instead. Harmless until time (going up by attackSpeed, ai[0], a tick) reaches attackTime, then
        /// hurting while laserFX (3, less by 12 % a tick) is at least 1: nine ticks (canDamage). Laser type 6 (rings, For
        /// the Worthy) is not told. Told of: the five lines of a straight set nearest the copy, the four of an aimed set.
        /// </summary>
        private static void Walls(Projectile p, Info info, Threat proto, Threat[] into, ref int n, Vector2 me)
        {
            object mp = p.ModProjectile;
            int type = (int)p.ai[2];

            if (type == 6 || !(info.Stored.GetValue(mp) is Vector2 stored))
                return;

            if (!Timing(p, info, mp, 3f, 0.12f, out int delay, out int life))
                return;

            float dist = p.ai[1] > 0f ? p.ai[1] : 250f;
            int count = (int)(6000 / Math.Max(dist, 1f));
            float half = dist * count / 2f;
            bool turned = type == 1 || type == 3 || type == 5;
            Vector2 x = turned ? new Vector2(Diagonal, Diagonal) : Vector2.UnitX;
            Vector2 y = turned ? new Vector2(-Diagonal, Diagonal) : Vector2.UnitY;
            int n0 = n;

            for (int l = 0; l < 2; l++)
            {
                bool horizontal = l != 0;
                bool cross = !horizontal && (type == 2 || type == 3) || horizontal && (type == 4 || type == 5);
                Vector2 along = horizontal ? y : x;
                Vector2 across = horizontal ? x : y;

                // Line i starts at first + across * laserDist * i.
                Vector2 first = p.Center - along * half - across * half;

                if (!cross)
                {
                    int at = (int)Math.Round(Vector2.Dot(me - first, across) / dist);
                    float angle = along.ToRotation();

                    for (int i = Math.Max(0, at - 2); i <= Math.Min(count - 1, at + 2); i++)
                    {
                        Threat th = LineOf(proto, first + across * dist * i, angle, half * 2f, 5f);
                        th.Delay = delay;
                        th.Life = life;
                        MimicBeams.Add(into, ref n, th, me);
                    }

                    continue;
                }

                // Not aimed yet (its first tick, before its AI stored where the target stood).
                if (stored == Vector2.Zero)
                    continue;

                int kept = 0;

                for (int i = 0; i < count; i += 2)
                {
                    Vector2 from = first + across * dist * i;
                    Vector2 dir = (stored - from).SafeNormalize(along);
                    float off = Off(me, from, dir, half * 2f);

                    if (kept == nearOff.Length && off >= nearOff[kept - 1])
                        continue;

                    int k = Math.Min(kept, nearOff.Length - 1);

                    while (k > 0 && nearOff[k - 1] > off)
                    {
                        nearOff[k] = nearOff[k - 1];
                        nearIdx[k] = nearIdx[k - 1];
                        k--;
                    }

                    nearOff[k] = off;
                    nearIdx[k] = i;
                    kept = Math.Min(kept + 1, nearOff.Length);
                }

                for (int k = 0; k < kept; k++)
                {
                    Vector2 from = first + across * dist * nearIdx[k];
                    Vector2 dir = (stored - from).SafeNormalize(along);
                    Threat th = LineOf(proto, from, dir.ToRotation(), half * 2f, 5f);
                    th.Delay = delay;
                    th.Life = life;
                    MimicBeams.Add(into, ref n, th, me);
                }
            }

            n = Math.Min(n, n0 + MaxPerShot);
        }

        /// <summary>
        /// When a wall or the big beam hurts (DoGLaserWalls / DoGLaserWallsBigBeam AI): the tick its time, going up by
        /// attackSpeed (ai[0]; 0.5 if unset) after each check, is first seen at attackTime or more, and on while its glow
        /// (set to <paramref name="fire"/> then, less by <paramref name="fade"/> of it a tick) is at least 1. False if
        /// it is done hurting.
        /// </summary>
        private static bool Timing(Projectile p, Info info, object mp, float fire, float fade, out int delay, out int life)
        {
            float time = F(info.WallTime.GetValue(mp));
            float attackTime = F(info.AttackTime.GetValue(mp));
            bool done = info.Done.GetValue(mp) is bool b && b;
            float speed = p.ai[0] > 0f ? p.ai[0] : 0.5f;

            if (!done)
            {
                // It is gone once its time has gone 10 past where it struck, whatever its glow.
                delay = 1 + Math.Max(0, (int)Math.Ceiling((attackTime - time) / speed));
                life = delay + Math.Min(Lit(fire, fade), (int)Math.Ceiling(10f / speed));
                return true;
            }

            delay = 1;
            life = 1 + Lit(F(info.Glow.GetValue(mp)) * (1f - fade), fade);
            return life > 1;
        }

        /// <summary>Ticks a glow, less by <paramref name="fade"/> of it a tick, stays at 1 or more.</summary>
        private static int Lit(float glow, float fade)
        {
            int k = 0;

            while (glow >= 1f && k < 60)
            {
                k++;
                glow *= 1f - fade;
            }

            return k;
        }

        /// <summary>How far a point is from a line from <paramref name="from"/> along <paramref name="dir"/>, <paramref name="length"/> long.</summary>
        private static float Off(Vector2 point, Vector2 from, Vector2 dir, float length)
        {
            float along = MathHelper.Clamp(Vector2.Dot(point - from, dir), 0f, length);
            return Vector2.Distance(point, from + dir * along);
        }

        // ---- any other shape --------------------------------------------------------------------------------------------

        /// <summary>An unknown kind is asked only while this near the copy.</summary>
        private const float AskNear = 1200f;

        /// <summary>A kind known to hurt out past its box (a beam) is asked while this near: its line may reach.</summary>
        private const float AskFar = 4800f;

        /// <summary>How far past its box a place it hurts at must be for a kind to count as hurting out past it.</summary>
        private const int PastBox = 48;

        /// <summary>Looks at a kind hurting only about its own box after which it is told of as its box for good.</summary>
        private const int BoxAfter = 24;

        /// <summary>Shapes asked a tick at most (each is 81 asks of its Colliding); half of them for unknown kinds.</summary>
        private const int AsksEachTick = 16;

        /// <summary>Most threats one shot is told of as.</summary>
        private const int MaxPerShot = 12;

        private static uint askTick;
        private static int asked;

        /// <summary>
        /// Another mod's shot with its own Colliding (or one of Calamity's whose members were not found): asked where it
        /// hurts about the copy (MimicBeams.Probe, as the Empress's shapes). If it hurts somewhere well out past its own box,
        /// it is told of so, and its kind is asked so from then on; if only about its box, it is told of as its box, and
        /// once that has been seen often enough its kind is not asked again.
        /// </summary>
        private static bool Ask(Projectile p, Info info, Threat proto, Threat[] into, ref int n, Vector2 me, Rectangle body)
        {
            float d2 = Vector2.DistanceSquared(p.Center, me);

            if (info.Shape ? d2 > AskFar * AskFar : info.BoxLooks >= BoxAfter || d2 > AskNear * AskNear)
                return false;

            if (askTick != Main.GameUpdateCount)
            {
                askTick = Main.GameUpdateCount;
                asked = 0;
            }

            if (asked >= (info.Shape ? AsksEachTick : AsksEachTick / 2))
                return false;

            asked++;
            int n0 = n;
            Motion(p, p.Center, p.rotation, out Vector2 vel, out _);
            int hits = MimicBeams.Probe(p, proto, into, ref n, me, body, vel, PastBox, out bool beyond);

            if (beyond)
                info.Shape = true;

            // (A probe tells of at most one box for each run of places in each of its nine rows.)
            if (info.Shape)
                return true;

            n = n0;

            if (hits > 0)
                info.BoxLooks++;

            return false;
        }

        // ---- motion ---------------------------------------------------------------------------------------------------

        /// <summary>The fastest a beam's foot is taken to move a tick; more is a jump (a teleport), not motion.</summary>
        private const float MaxFootSpeed = 64f;

        /// <summary>The most a beam is taken to turn a tick.</summary>
        private const float MaxTurn = 0.2f;

        // What each projectile slot's beam was last tick (its identity, foot and angle), so its turning and how its foot
        // moves can be told: the game does not say, but two ticks show it (as MimicSense does for shots, and as the
        // trainer tells a beam's SeenTurn).
        private static readonly int[] seenIdentity = new int[Main.maxProjectiles + 1];
        private static readonly uint[] seenTick = new uint[Main.maxProjectiles + 1];
        private static readonly Vector2[] seenFoot = new Vector2[Main.maxProjectiles + 1];
        private static readonly float[] seenAngle = new float[Main.maxProjectiles + 1];
        private static readonly Vector2[] footMoved = new Vector2[Main.maxProjectiles + 1];
        private static readonly float[] angleMoved = new float[Main.maxProjectiles + 1];

        /// <summary>How a beam's foot moved and how far it turned since it was seen last tick (nothing if it was not).</summary>
        private static void Motion(Projectile p, Vector2 foot, float angle, out Vector2 vel, out float turn)
        {
            int i = p.whoAmI;
            uint now = Main.GameUpdateCount;

            if (seenTick[i] == now && seenIdentity[i] == p.identity)
            {
                vel = footMoved[i];
                turn = angleMoved[i];
                return;
            }

            bool was = seenIdentity[i] == p.identity && seenTick[i] == now - 1;
            vel = was ? foot - seenFoot[i] : Vector2.Zero;
            turn = was ? MathHelper.WrapAngle(angle - seenAngle[i]) : 0f;

            if (vel.LengthSquared() > MaxFootSpeed * MaxFootSpeed)
                vel = Vector2.Zero;

            turn = MathHelper.Clamp(turn, -MaxTurn, MaxTurn);

            seenIdentity[i] = p.identity;
            seenTick[i] = now;
            seenFoot[i] = foot;
            seenAngle[i] = angle;
            footMoved[i] = vel;
            angleMoved[i] = turn;
        }

        /// <summary>A line from a threat as a box: harmless to parry, not bending, still unless told otherwise.</summary>
        private static Threat LineOf(Threat proto, Vector2 foot, float angle, float length, float halfW)
        {
            Threat th = proto;
            th.Line = true;
            th.Pos = foot;
            th.Vel = Vector2.Zero;
            th.Acc = Vector2.Zero;
            th.Angle = angle;
            th.Length = length;
            th.HalfW = halfW;
            th.HalfH = halfW;
            th.Turn = 0f;
            th.Homing = 0f;
            th.HomeTurn = 0f;
            th.Parryable = false;
            th.Delay = 0;
            return th;
        }

        private static float F(object value) => value == null ? 0f : Convert.ToSingle(value);

        // ---- knowing the kinds ----------------------------------------------------------------------------------------

        private static Info InfoOf(Projectile p)
        {
            int type = p.type;

            if (type >= infos.Length)
                Array.Resize(ref infos, Math.Max(type + 1, ProjectileLoader.ProjectileCount));

            Info info = infos[type];

            if (info == null)
            {
                try
                {
                    info = Classify(p.ModProjectile);
                }
                catch (Exception)
                {
                    info = new Info();
                }

                infos[type] = info;
            }

            return info;
        }

        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static Info Classify(ModProjectile mp)
        {
            var info = new Info();

            if (mp == null)
                return info;

            Type t = mp.GetType();

            // The mod's own are as MimicBeams and MimicSense tell them (and as the trainer shows them).
            if (t.Assembly == typeof(MimicCalamity).Assembly)
                return info;

            if (mp.Mod?.Name == CalamityName && Calamity(mp.Name, t, info))
                return info;

            info.Kind = OwnColliding(t) ? Kind.Probe : Kind.None;
            return info;
        }

        /// <summary>A kind of Calamity's: what it is and the members it is read by. False for one not known here.</summary>
        private static bool Calamity(string name, Type t, Info info)
        {
            switch (name)
            {
                case "AresLaserBeamStart":
                    info.Kind = Kind.AresArmBeam;
                    return true;

                case "BrimstoneRay":
                    info.Kind = Kind.BrimstoneRay;
                    return true;

                case "ProvidenceHolyRay":
                    info.Kind = Kind.HolyRay;
                    return true;

                // The warnings, with the damage Calamity gives the beam each warns of (AresBody.BlenderDamage,
                // Artemis.BeamDamage, ThanatosHead.BeamDamage, BrimstoneElemental.RayDamage).
                case "AresDeathBeamTelegraph":
                    info.Kind = Kind.AresDeathWarn;
                    info.Raw = 105;
                    return true;

                case "ArtemisDeathrayTelegraph":
                    info.Kind = Kind.ArtemisDeathWarn;
                    info.Raw = 105;
                    return true;

                case "ThanatosBeamTelegraph":
                    info.Kind = Kind.ThanatosWarn;
                    info.Raw = 135;
                    return true;

                case "BrimstoneTargetRay":
                    info.Kind = Kind.BrimstoneWarn;
                    info.Raw = 35;
                    return true;

                case "ArtemisChargeTelegraph":
                    info.Kind = Kind.ArtemisChargeWarn;
                    return true;

                case "ApolloChargeTelegraph":
                    info.Charges = t.GetField("ChargePositions", Members);
                    info.Kind = info.Charges != null ? Kind.ApolloChargeWarn : Kind.None;
                    return true;

                case "ThanatosLaser":
                case "THanosSideLaser":
                case "ArtemisLaser":
                    info.Held = t.GetField("Velocity", Members);
                    info.Artemis = name == "ArtemisLaser";
                    info.Kind = info.Held != null ? Kind.WarnedLaser : Kind.None;
                    return true;

                case "DoGLaserWalls":
                case "DoGLaserWallsBigBeam":
                {
                    info.WallTime = t.GetField("time", Members);
                    info.AttackTime = t.GetField("attackTime", Members);
                    info.Done = t.GetField("doneAttack", Members);
                    info.Glow = t.GetField("laserFX", Members);
                    bool read = info.WallTime != null && info.AttackTime != null && info.Done != null && info.Glow != null;

                    if (name == "DoGLaserWalls")
                    {
                        info.Stored = t.GetField("storedTargetPos", Members);
                        read &= info.Stored != null;
                    }
                    else
                    {
                        info.BeamStart = t.GetField("beamStart", Members);
                        info.BeamDir = t.GetField("directionToTarget", Members);
                        info.BeamLength = t.GetProperty("laserLength", Members);
                        read &= info.BeamStart != null && info.BeamDir != null;
                    }

                    Fallback(info, read, name == "DoGLaserWalls" ? Kind.LaserWalls : Kind.BigBeam);
                    return true;
                }

                case "AresGaussNukeProjectileBoom":
                    info.Lifetime = t.GetProperty("Lifetime", Members);
                    info.Kind = info.Lifetime != null ? Kind.Boom : Kind.None;
                    return true;

                case "AresPlasmaFireball":
                case "ApolloFireball":
                    info.Kind = Kind.PlasmaBurst;
                    return true;

                case "AresTeslaOrb":
                    info.Partner = t.GetMethod("GetOrbToAttachTo", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    Fallback(info, info.Partner != null && typeof(Projectile).IsAssignableFrom(info.Partner.ReturnType), Kind.TeslaOrb);
                    return true;

                case "InfernadoRevenge":
                    info.Kind = Kind.Border;
                    return true;

                // Harmless markers (Anahita's ring of telegraphs): the trainer leaves them out. And the round shots of these
                // bosses, whose circle (CircularHitboxCollision) is about their box: told of as their box, never asked.
                case "AnahitaTelegraph":
                case "BrimstoneBarrage":
                case "DarkEnergyBall":
                case "DarkEnergyBall2":
                case "FlareBomb":
                case "FlareDust":
                case "FlareDust2":
                case "MoltenBlast":
                case "LeviathanBomb":
                    info.Kind = Kind.None;
                    return true;
            }

            Type laser = BaseOf(t, "BaseLaserbeamProjectile");

            if (laser == null)
                return false;

            info.LaserLength = laser.GetProperty("LaserLength", Members);
            info.Time = laser.GetProperty("Time", Members);
            info.Lifetime = laser.GetProperty("Lifetime", Members);
            info.MaxScale = laser.GetProperty("MaxScale", Members);
            info.ExpandRate = laser.GetProperty("ScaleExpandRate", Members);
            MethodInfo colliding = t.GetMethod("Colliding", new[] { typeof(Rectangle), typeof(Rectangle) });
            MethodInfo motion = t.GetMethod("UpdateLaserMotion", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            // One with its own Colliding hurts as it says, not as the base's line: asked as a shape.
            bool known = info.LaserLength != null && info.Time != null && info.Lifetime != null && colliding?.DeclaringType == laser;
            info.OwnMotion = motion == null || motion.DeclaringType != laser;
            info.ScaleGate = name == "AresDeathBeamStart" || name == "ThanatosBeamStart" || name == "ArtemisSpinLaserbeam";
            Fallback(info, known, Kind.Laserbeam);
            return true;
        }

        /// <summary>The kind as read, or, its members not found, a shape to be asked where it hurts.</summary>
        private static void Fallback(Info info, bool read, Kind kind)
        {
            info.Kind = read ? kind : Kind.Probe;
            info.Shape = !read;
        }

        /// <summary>The type, or the one it comes from, of that name in Calamity's code.</summary>
        private static Type BaseOf(Type t, string name)
        {
            for (Type b = t; b != null && b != typeof(ModProjectile); b = b.BaseType)
            {
                if (b.Name == name)
                    return b;
            }

            return null;
        }

        // ---- the copy's cursor, as Calamity keeps it ---------------------------------------------------------------------

        private static bool mouseLooked;
        private static ModPlayer calamityPlayer;
        private static FieldInfo mouseDelta, mouseRotation, mouseRightField;
        private static uint syncedAt = uint.MaxValue;
        private static Vector2 syncedCentre, syncedAim;
        private static bool syncedRight;

        /// <summary>
        /// Calamity's own record of where its player's cursor is and whether the right button is down (CalamityPlayer:
        /// mouseWorldDeltaFromPlayer, mouseRotationFromPlayer, mouseRight; its mouseWorld is the player's MountedCenter plus
        /// the first), kept for the copy as it is for the player at the keyboard. Calamity writes them only for that player
        /// (in its PreUpdate, when Main.myPlayer is the player), so for the copy they stayed at nothing: its weapons and
        /// their held shots that aim by them aimed at the copy itself, and some (the Ark of the Cosmos's parry, at
        /// Math.Sign of a direction normalised from nothing) came to NaN (§17.35, 4). Found by name once (no dependency); a
        /// Calamity without them is let be.
        /// </summary>
        public static void SyncMouse(Player copy, Vector2 aim, bool right)
        {
            if (!mouseLooked)
            {
                mouseLooked = true;

                if (ModLoader.TryGetMod(CalamityName, out Mod cal) && cal.TryFind("CalamityPlayer", out ModPlayer template))
                {
                    calamityPlayer = template;
                    Type t = template.GetType();
                    mouseDelta = t.GetField("mouseWorldDeltaFromPlayer", Members);
                    mouseRotation = t.GetField("mouseRotationFromPlayer", Members);
                    mouseRightField = t.GetField("mouseRight", Members);

                    if (mouseDelta?.FieldType != typeof(Vector2))
                        mouseDelta = null;

                    if (mouseRotation?.FieldType != typeof(float))
                        mouseRotation = null;

                    if (mouseRightField?.FieldType != typeof(bool))
                        mouseRightField = null;
                }
            }

            if (calamityPlayer == null || copy == null)
                return;

            // Once for each place, aim and button in a tick (the copy is run as the player at the keyboard many times a tick:
            // its every shot's update).
            Vector2 centre = copy.MountedCenter;

            if (Main.GameUpdateCount == syncedAt && centre == syncedCentre && aim == syncedAim && right == syncedRight)
                return;

            syncedAt = Main.GameUpdateCount;
            syncedCentre = centre;
            syncedAim = aim;
            syncedRight = right;

            try
            {
                ModPlayer mine = copy.GetModPlayer(calamityPlayer);
                Vector2 delta = aim - centre;

                // A cursor on the copy itself has no direction: a hair's breadth the way it faces.
                if (delta.LengthSquared() < 1f)
                    delta = new Vector2(copy.direction == 0 ? 1 : copy.direction, 0f);

                mouseDelta?.SetValue(mine, delta);
                mouseRotation?.SetValue(mine, (float)Math.Atan2(delta.Y, delta.X));
                mouseRightField?.SetValue(mine, right);
            }
            catch (Exception)
            {
                calamityPlayer = null;
            }
        }

        /// <summary>Its ModProjectile hurts by a Colliding of its own (not ModProjectile's, which leaves it to the box).</summary>
        private static bool OwnColliding(Type t)
        {
            MethodInfo colliding = t.GetMethod("Colliding", new[] { typeof(Rectangle), typeof(Rectangle) });
            return colliding != null && colliding.DeclaringType != typeof(ModProjectile);
        }
    }
}
