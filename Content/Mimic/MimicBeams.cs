using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The shots that do not hit with their box: beams that hit along a line (a deathray, a sweeping light), shots that
    /// wait before they fly (a lance), trails that hurt along where they have been (a lasting rainbow), and the shapes
    /// the Empress's acts hurt with. The game hits with these by their own Colliding; the copy is told of them as the
    /// game will hit (MimicBrain.Threat: Line, Angle, Length, Turn; Delay), each as its code has it. One shot may be told
    /// of as several threats (a curved trail as its pieces, a shape as the places near the copy it covers).
    /// </summary>
    public static class MimicBeams
    {
        /// <summary>
        /// A shot of these kinds: its threats written into <paramref name="into"/> from <paramref name="n"/> on (those near
        /// enough to matter), and true. False for anything else, which is told of as its box.
        /// </summary>
        /// <param name="proto">The shot as a box (its weight, life, velocity), to start each threat from.</param>
        /// <param name="body">The copy's own hitbox, for the shapes that can only be asked where they hurt.</param>
        public static bool Read(Projectile p, Threat proto, Threat[] into, ref int n, Vector2 me, Rectangle body)
        {
            switch (p.type)
            {
                // The Moon Lord's deathray (and a True Eye's): a line from the pupil, as long as it reaches (localAI[1]),
                // 36 wide at full size (a True Eye's two fifths of that), turning ai[0] a tick, for 180 ticks
                // (Projectile.AI_084, Colliding).
                case ProjectileID.PhantasmalDeathray:
                {
                    bool trueEye = Main.npc.IndexInRange((int)p.ai[1]) && Main.npc[(int)p.ai[1]].type == NPCID.MoonLordFreeEye;
                    Threat th = proto;
                    th.Line = true;
                    th.Pos = p.Center;
                    th.Vel = Vector2.Zero;
                    th.Angle = p.velocity.ToRotation();
                    th.Length = Math.Max(p.localAI[1], 64f);
                    th.HalfW = 18f * (trueEye ? 0.4f : 1f);
                    th.Turn = p.ai[0];
                    th.Life = Math.Max(1, 180 - (int)p.localAI[0]);
                    Add(into, ref n, th, me);
                    return true;
                }

                // The Empress's lance: still along ai[0] for 60 ticks and harmless (Projectile.Damage), then 40 px a tick
                // that way (AI_179); it hurts along 80 px of its length, 8 wide (Colliding). Told of as flying from where it
                // waits once the wait is over.
                case ProjectileID.FairyQueenLance:
                {
                    Vector2 way = p.ai[0].ToRotationVector2();
                    float age = p.localAI[0];
                    Threat th = proto;
                    th.Line = true;
                    th.Angle = p.ai[0];
                    th.Length = 80f;
                    th.HalfW = 4f;
                    th.Vel = way * 40f;
                    th.Pos = p.Center - way * 40f;
                    th.Life = Math.Max(1, 360 - (int)age);

                    if (age < 61f)
                    {
                        th.Delay = (int)(61f - age);
                        th.Pos -= th.Vel * th.Delay;
                    }

                    Add(into, ref n, th, me);
                    return true;
                }

                // Her sun dance's rays: three lines from her middle (510 long and 70 wide, 660 and 42, 800 and 7, at full
                // size), harmless for their first 60 ticks, turning a ninth of a half-turn over ticks 50 to 180 (AI_180).
                case ProjectileID.FairyQueenSunDance:
                {
                    float age = p.localAI[0];
                    Threat th = proto;
                    th.Line = true;
                    th.Pos = p.Center;
                    th.Vel = Vector2.Zero;
                    th.Angle = p.rotation;
                    th.Turn = age >= 40f ? MathHelper.Pi / 9f / 130f : 0f;
                    th.Delay = age < 61f ? (int)(61f - age) : 0;
                    th.Life = Math.Max(1, 180 - (int)age);

                    th.Length = 510f;
                    th.HalfW = 35f;
                    Add(into, ref n, th, me);
                    th.Length = 660f;
                    th.HalfW = 21f;
                    Add(into, ref n, th, me);
                    th.Length = 800f;
                    th.HalfW = 3.5f;
                    Add(into, ref n, th, me);
                    return true;
                }

                // Her lasting rainbow: it hurts with its box at its last 80 places (Colliding), a curve of some 640 px;
                // told of as its head and three straight pieces of what it leaves behind.
                case ProjectileID.HallowBossLastingRainbow:
                {
                    Add(into, ref n, proto, me);
                    Vector2 half = p.Size * 0.5f;
                    int last = Math.Min(p.oldPos.Length, 80) - 1;
                    Vector2 from = p.Center;

                    for (int k = 1; k <= 3; k++)
                    {
                        int j = Math.Min(last, k * 26);

                        if (j < 0 || p.oldPos[j] == Vector2.Zero)
                            break;

                        Vector2 to = p.oldPos[j] + half;
                        Threat th = proto;
                        th.Line = true;
                        th.Pos = from;
                        th.Vel = Vector2.Zero;
                        th.Acc = Vector2.Zero;
                        th.Angle = (to - from).ToRotation();
                        th.Length = Vector2.Distance(from, to);
                        th.HalfW = half.X;
                        th.Life = 20;
                        Add(into, ref n, th, me);
                        from = to;
                    }

                    return true;
                }
            }

            if (p.ModProjectile is EmpressArrow)
            {
                Arrow(p, proto, into, ref n, me);
                return true;
            }

            // The shapes of her acts can only be asked where they hurt: the places about the copy they cover now.
            if (p.ModProjectile is EmpressHitbox)
            {
                Probe(p, proto, into, ref n, me, body);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Her arrows (EmpressArrow): a line from its nock to its tip, 16 wide, as it flies; one stuck in her border, the
        /// burst it will give, a circle 480 about its star; a refraction arrow taking aim, flying off along it after.
        /// </summary>
        private static void Arrow(Projectile p, Threat proto, Threat[] into, ref int n, Vector2 me)
        {
            Vector2 way = p.rotation.ToRotationVector2();
            bool hurts = p.ModProjectile.CanDamage() != false;
            int kind = (int)p.ai[0];
            Threat th = proto;
            th.Line = true;
            th.Angle = p.rotation;
            th.HalfW = 8f;
            th.Acc = Vector2.Zero;
            th.Homing = 0f;

            if (p.velocity.LengthSquared() > 1f)
            {
                if (!hurts)
                    return;

                th.Pos = p.Center - way * EmpressArrow.NockBack - p.velocity;
                th.Length = EmpressArrow.NockBack + EmpressArrow.TipAhead + p.velocity.Length();
                th.Vel = p.velocity;
                Add(into, ref n, th, me);
                return;
            }

            if (kind == 0)
            {
                // Moor, in the wall: its burst to come (or bursting).
                th.Pos = p.Center + way * 20f;
                th.Vel = Vector2.Zero;
                th.Length = 0f;
                th.HalfW = EmpressArrow.BurstRadius;
                th.Life = 0;
                Add(into, ref n, th, me);
                return;
            }

            if (kind == 2 && !hurts)
            {
                // Refraction, aiming: it dives along its aim at 30 px a tick within the aim's 22 ticks.
                th.Vel = way * EmpressArrow.DiveSpeed;
                th.Delay = EmpressArrow.AimTicks / 2;
                th.Pos = p.Center - way * EmpressArrow.NockBack - th.Vel * th.Delay;
                th.Length = EmpressArrow.NockBack + EmpressArrow.TipAhead;
                Add(into, ref n, th, me);
            }
        }

        private const int Cells = 9;
        private const float CellStep = 40f;

        /// <summary>
        /// A shape asked where it hurts: the copy's box tried at a grid of places about it (9 by 9, 40 px apart), and each
        /// run of places in a row it would be hurt at told of as one box there, for as long as the shape lasts a little.
        /// </summary>
        private static void Probe(Projectile p, Threat proto, Threat[] into, ref int n, Vector2 me, Rectangle body)
        {
            Rectangle own = p.Hitbox;
            float halfW = body.Width * 0.5f, halfH = body.Height * 0.5f;

            for (int row = 0; row < Cells; row++)
            {
                int start = -1;

                for (int col = 0; col <= Cells; col++)
                {
                    bool hit = false;

                    if (col < Cells)
                    {
                        Rectangle at = body;
                        at.X += (int)((col - Cells / 2) * CellStep);
                        at.Y += (int)((row - Cells / 2) * CellStep);
                        hit = p.Colliding(own, at);
                    }

                    if (hit && start < 0)
                        start = col;

                    if (hit || start < 0)
                        continue;

                    float midCol = (start + col - 1) * 0.5f - Cells / 2;
                    Threat th = proto;
                    th.Line = false;
                    th.Pos = me + new Vector2(midCol * CellStep, (row - Cells / 2) * CellStep);
                    th.Vel = p.velocity;
                    th.Acc = Vector2.Zero;
                    th.Homing = 0f;
                    th.HalfW = Math.Max(0f, (col - 1 - start) * CellStep * 0.5f + CellStep * 0.5f - halfW);
                    th.HalfH = Math.Max(0f, CellStep * 0.5f - halfH);
                    th.Life = 12;

                    if (n < into.Length)
                        into[n++] = th;

                    start = -1;
                }
            }
        }

        private static void Add(Threat[] into, ref int n, Threat th, Vector2 me)
        {
            if (n >= into.Length || Near(ref th, me) > MimicSense.Reach)
                return;

            into[n++] = th;
        }

        /// <summary>How far a threat passes from a point: a line's nearest point, a box's middle.</summary>
        public static float Near(ref Threat th, Vector2 me)
        {
            if (!th.Line)
                return Vector2.Distance(me, th.Pos);

            Vector2 dir = new Vector2((float)Math.Cos(th.Angle), (float)Math.Sin(th.Angle));
            float along = MathHelper.Clamp(Vector2.Dot(me - th.Pos, dir), 0f, th.Length);
            return Math.Max(0f, Vector2.Distance(me, th.Pos + dir * along) - th.HalfW);
        }

        /// <summary>A hit as the game lands it on a player in this world: twice the shot's damage, times the difficulty's (CombinedHooks.ModifyHitByProjectile).</summary>
        public static int HitOf(Projectile p)
        {
            int hit = p.damage * 2;

            if (!p.reflected && !ProjectileID.Sets.PlayerHurtDamageIgnoresDifficultyScaling[p.type])
                hit = (int)(hit * Main.GameModeInfo.EnemyDamageMultiplier);

            return hit;
        }
    }
}
