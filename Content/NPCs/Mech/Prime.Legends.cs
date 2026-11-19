using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- Skeletron Prime in Legends -----------------------------------------------------------------------------------------
    //
    // By request ("Skeletron Prime Legends: ..."). Three fifths its head's life, three times its arms' ("3/5 his head
    // health and triple his arms health").
    //
    //   WHILE AN ARM IS UP   its head cannot be hurt and only moves about ("Head is locked invincible and only move around
    //                        untiil all arms are defeated much like skeletron"): vanilla's hover, its own spin never
    //                        coming. Each arm fights as vanilla's does, and between stretches of that, one of its own
    //                        attacks, one arm at a time (PrimeArm.Legends):
    //
    //                          laser    its death lasers, green     Burst: five to seven barrages of three
    //                                                               Corner: a column down, across the screen
    //                          cannon   mini missiles that home     BombRun: ten bombs off an arc over the player (rarer)
    //                          vice     clenched, vanilla's lunges  Grapple: caught, whirled five times, thrown up
    //                          saw      vanilla's                   Dig: along the ground, earth thrown
    //
    //                        More rarely (every twenty-five to forty seconds) every arm stops for one thing ("All
    //                        special attacks make other arms stop, decide on one, this happens randomly but not too
    //                        frequently. All arms attack happens as rare as special attack"): one arm's special, the
    //                        others holding its base or stopped where they are, or all of them together (Prime.Group,
    //                        PrimeArm.Group):
    //
    //                          LaserColumn   NukeDrop   ViceHold   SawTrain   |   Spin   Clash
    //
    //   NO ARM LEFT          it changes (Prime.Change), and can be hurt. Vanilla's hover, and between stretches of it
    //                        one of its own (Prime.Moves): Dash, Detonate, Beams, and very rarely Launch.
    //
    // The server (or a single player) picks every attack and every mark; every machine plays them from that
    // (SendLegends). Day's rage and its leaving are vanilla's still.

    public partial class Prime
    {
        /// <summary>By request: "3/5 his head health and triple his arms health".</summary>
        public const float HeadLifeScale = 0.6f;
        public const float ArmLifeScale = 3f;

        internal enum Act : byte
        {
            None,

            // One arm's special, the others stopped for it.
            LaserColumn, NukeDrop, ViceHold, SawTrain,

            // All its arms'.
            Spin, Clash,

            // Its own, once no arm is left.
            Dash, Detonate, Beams, Launch,
        }

        private Act act, lastGroup, lastMove;

        /// <summary>
        /// Ticks to its arms' next attack together (the server's count): the first, the least and the most between two
        /// (twenty-five to forty seconds), and how near it must not be for an arm to begin one of its own.
        /// </summary>
        private int groupIn = GroupFirst;
        private const int GroupFirst = 1080, GroupLeast = 1500, GroupMost = 2400, GroupClear = 110;

        /// <summary>Ticks before one of its arms may begin an attack of its own (one at a time, a breath between). The server's.</summary>
        private int armGap = 200;

        /// <summary>Ticks of its hover left before its next own attack (once no arm is left). The server's.</summary>
        private int restFor = 170;

        /// <summary>Its dashes in a row; ticks before it may launch itself again. The server's.</summary>
        private int dashRun, launchWait;

        /// <summary>Two places an attack is laid out from, a direction, a length, a count, a side: what each means is the attack's own.</summary>
        private Vector2 markA, markB;
        private float angle, span;
        private int count, side = 1;

        /// <summary>The player its vice holds in its special (-1: none).</summary>
        private int grabbed = -1;

        /// <summary>Its touch hurts this tick (in a scene), and by how much more than it does hovering.</summary>
        private bool hurts;
        private float hurtScale = 1f;

        /// <summary>How brightly its eyes burn for an attack, over what they do (0 none): only seen.</summary>
        private float eyeHeat;

        /// <summary>Where the player it is on has been, newest last (this machine's): what lags behind them is aimed from it.</summary>
        private readonly Vector2[] wasAt = new Vector2[32];
        private int wasAtKept;

        internal Act GroupAct => scene == Scene.Group ? act : Act.None;
        internal int Side => side;
        internal float Angle => angle;
        internal float Span => span;
        internal Vector2 MarkA => markA;
        internal Vector2 MarkB => markB;
        internal int Count => count;
        internal int Grabbed => grabbed;

        public override void SetDefaults(NPC entity)
        {
            if (PADWorld.Legends)
                entity.lifeMax = (int)(entity.lifeMax * HeadLifeScale);
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!NPC.IsMechQueenUp && scene != Scene.None && hurts && hurtScale != 1f)
                modifiers.SourceDamage *= hurtScale;
        }

        // ---- what comes next ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Between its attacks: vanilla's hover kept (its count to its own spin never runs out: its arms' attacks, and
        /// its own once it has changed, are in its place); and, on the server, what comes next.
        /// </summary>
        private void LegendsBetween(NPC npc)
        {
            if (npc.ai[1] == 0f)
            {
                npc.ai[2] = 0f;
            }
            else if (npc.ai[1] == 1f)
            {
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
            }

            eyeHeat = Math.Max(0f, eyeHeat - 0.05f);

            if (!Authority || npc.ai[1] >= 2f || Target(npc) == null || Main.IsItDay())
                return;

            if (!changed)
            {
                GroupClock(npc);
                return;
            }

            if (launchWait > 0)
                launchWait--;

            if (--restFor <= 0)
                BeginMove(npc);
        }

        private static readonly Act[] pool = new Act[6];

        /// <summary>
        /// The count to its arms' next attack together. It waits out an arm's own attack (none begins once it is near),
        /// then every arm stops for it: one arm's special, or all of them (two up at the least).
        /// </summary>
        private void GroupClock(NPC npc)
        {
            int n = FindArms(npc), up = 0;
            bool busy = false, laser = false, cannon = false, vice = false, saw = false;

            for (int k = 0; k < n; k++)
            {
                NPC limb = Main.npc[armList[k]];

                if (!limb.TryGetGlobalNPC(out PrimeArm arm) || !arm.Up)
                    continue;

                up++;
                busy |= arm.Acting;
                laser |= limb.type == NPCID.PrimeLaser;
                cannon |= limb.type == NPCID.PrimeCannon;
                vice |= limb.type == NPCID.PrimeVice;
                saw |= limb.type == NPCID.PrimeSaw;
            }

            if (armGap > 0 && !busy)
                armGap--;

            if (up == 0)
                return;

            if (groupIn > 1 || !busy)
                groupIn--;

            if (groupIn > 0)
                return;

            int m = 0;

            if (laser)
                pool[m++] = Act.LaserColumn;

            if (cannon)
                pool[m++] = Act.NukeDrop;

            if (vice)
                pool[m++] = Act.ViceHold;

            if (saw)
                pool[m++] = Act.SawTrain;

            if (up >= 2)
            {
                pool[m++] = Act.Spin;
                pool[m++] = Act.Clash;
            }

            // Not the one before, if there is another.
            if (m > 1)
            {
                for (int k = 0; k < m; k++)
                {
                    if (pool[k] == lastGroup)
                    {
                        pool[k] = pool[--m];
                        break;
                    }
                }
            }

            act = pool[Main.rand.Next(m)];
            lastGroup = act;
            groupIn = Main.rand.Next(GroupLeast, GroupMost + 1);
            seed = Main.rand.Next(int.MaxValue);
            side = Main.rand.NextBool() ? 1 : -1;
            count = 0;
            grabbed = -1;
            angle = 0f;
            span = 0f;
            markA = markB = npc.Center;
            wasAtKept = 0;

            // Its saw's train: at the player from the side its saw is already on.
            if (act == Act.SawTrain && ArmOf(npc, NPCID.PrimeSaw, out NPC blade) && Target(npc) is Player on && Math.Abs(on.Center.X - blade.Center.X) > 40f)
                side = Math.Sign(on.Center.X - blade.Center.X);

            Go(npc, Scene.Group, 0);
        }

        /// <summary>The arm that leads an attack (its special's own), if it is one arm's.</summary>
        internal static int LeadType(Act act) => act switch
        {
            Act.LaserColumn => NPCID.PrimeLaser,
            Act.NukeDrop => NPCID.PrimeCannon,
            Act.ViceHold => NPCID.PrimeVice,
            Act.SawTrain => NPCID.PrimeSaw,
            _ => -1,
        };

        /// <summary>One of its arms that is up, by its kind.</summary>
        internal static bool ArmOf(NPC head, int type, out NPC limb)
        {
            limb = null;
            int n = FindArms(head);

            for (int k = 0; k < n; k++)
            {
                NPC found = Main.npc[armList[k]];

                if (found.type == type && found.TryGetGlobalNPC(out PrimeArm arm) && arm.Up)
                {
                    limb = found;
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many of its arms are up; and how many of them come before this one (by kind): its place among them.</summary>
        internal static int ArmsUp(NPC head, NPC before, int skip, out int rank)
        {
            rank = 0;
            int n = FindArms(head), up = 0;

            for (int k = 0; k < n; k++)
            {
                NPC found = Main.npc[armList[k]];

                if (found.type == skip || !found.TryGetGlobalNPC(out PrimeArm arm) || !arm.Up)
                    continue;

                up++;

                if (before != null && Order(found.type) < Order(before.type))
                    rank++;
            }

            return up;
        }

        /// <summary>The order its arms are counted in: cannon, laser, vice, saw (as their lamps come on).</summary>
        private static int Order(int type) => LampTurn(type);

        /// <summary>An arm may begin an attack of its own now: no other is at one, a breath since the last, its arms' attack together not near.</summary>
        internal bool ArmMayAct(NPC head)
        {
            if (scene != Scene.None || armGap > 0 || groupIn <= GroupClear || changed)
                return false;

            int n = FindArms(head);

            for (int k = 0; k < n; k++)
            {
                if (Main.npc[armList[k]].TryGetGlobalNPC(out PrimeArm arm) && arm.Up && arm.Acting)
                    return false;
            }

            return true;
        }

        /// <summary>An arm's own attack is over: a breath before the next.</summary>
        internal void ArmDone() => armGap = Main.rand.Next(100, 171);

        /// <summary>Its arms' attack together over: back to vanilla's (its arms go back by themselves, seeing it).</summary>
        private void EndGroup(NPC npc)
        {
            act = Act.None;
            grabbed = -1;
            armGap = Main.rand.Next(130, 211);
            BackToHover(npc);
            Go(npc, Scene.None, 0);
        }

        private static void BackToHover(NPC npc)
        {
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.rotation = MathHelper.WrapAngle(npc.rotation);

            float speed = npc.velocity.Length();

            if (speed > 9f)
                npc.velocity *= 9f / speed;
        }

        /// <summary>Whatever of Legends it was at is dropped (its change, its end): what it had out put out, nothing held.</summary>
        private void LegendsStop(NPC npc)
        {
            act = Act.None;
            grabbed = -1;
            hurts = false;
            eyeHeat = 0f;
            PrimeShots.Clear();
        }

        // ---- how it moves, and what it does to the player ---------------------------------------------------------------

        /// <summary>Toward a place, easing in: its velocity brought toward what would take it there, by this share a tick.</summary>
        internal static void Glide(NPC npc, Vector2 to, float speed, float share)
        {
            Vector2 want = to - npc.Center;
            float far = want.Length();
            want = far > 1f ? want / far * Math.Min(speed, far * 0.14f + 0.4f) : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, want, share);
        }

        /// <summary>Its lean as it drifts (as vanilla's hover leans it), eased.</summary>
        private static void Settle(NPC npc) => npc.rotation = npc.rotation.AngleLerp(MathHelper.Clamp(npc.velocity.X / 15f, -0.5f, 0.5f), 0.2f);

        /// <summary>
        /// This machine's player carried toward a place, if within reach of it: so many pixels this tick (less the
        /// further off), not through walls. Carried, not pushed: what they stand on does not hold them, and running
        /// against it is only so much slower.
        /// </summary>
        internal static void Drag(Vector2 toward, float speed, float reach)
        {
            if (Main.dedServ || speed <= 0f)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || me.ghost || me.GetModPlayer<PrimeGrip>().Held)
                return;

            Vector2 way = toward - me.Center;
            float far = way.Length();

            if (far < 20f || far > reach)
                return;

            Vector2 step = way / far * Math.Min(speed * (1f - 0.6f * far / reach), far - 16f);
            me.position += Collision.TileCollision(me.position, step, me.width, me.height, true, true, (int)me.gravDir);
        }

        /// <summary>Numbers for this attack that every machine draws alike (from what the server drew for it).</summary>
        private Random Dice(int salt) => new(unchecked(seed * 31 + salt * 7919));

        private void TrackTarget(Player p) => wasAt[wasAtKept++ & 31] = p.Center;

        /// <summary>Where the player it is on was this many ticks ago (as this machine saw them).</summary>
        internal Vector2 Lagged(Player p, int ticks) => wasAtKept > ticks ? wasAt[(wasAtKept - 1 - ticks) & 31] : p.Center;

        // ---- the ground ---------------------------------------------------------------------------------------------------

        private static bool Solid(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 2))
                return false;

            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>The ground under a place: the top of the first solid tile at or under it, as far as so many tiles down.</summary>
        internal static bool GroundUnder(float x, float fromY, int tiles, out float top)
        {
            int tx = (int)(x / 16f), y0 = (int)(fromY / 16f);

            for (int y = y0; y <= y0 + tiles; y++)
            {
                if (Solid(tx, y))
                {
                    top = y * 16f;
                    return true;
                }
            }

            top = fromY;
            return false;
        }

        /// <summary>The floor under a place: the top of the first tile a player could stand on (a platform too), as far as so many tiles down.</summary>
        internal static bool FloorUnder(float x, float fromY, int tiles, out float top)
        {
            int tx = (int)(x / 16f), y0 = (int)(fromY / 16f);

            for (int y = y0; y <= y0 + tiles; y++)
            {
                if (!WorldGen.InWorld(tx, y, 2))
                    break;

                Tile tile = Main.tile[tx, y];

                if (tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]))
                {
                    top = y * 16f;
                    return true;
                }
            }

            top = fromY;
            return false;
        }

        /// <summary>
        /// The ground's top by a place that was on it a step ago: up out of it if it is inside (as far as
        /// <paramref name="up"/> tiles: more is a wall), down to it if it is over it (as far as <paramref name="down"/>).
        /// </summary>
        internal static bool SurfaceNear(float x, float nearY, int up, int down, out float top)
        {
            int tx = (int)(x / 16f), y0 = (int)(nearY / 16f);
            top = nearY;

            if (Solid(tx, y0))
            {
                for (int y = y0 - 1; y >= y0 - up; y--)
                {
                    if (!Solid(tx, y))
                    {
                        top = (y + 1) * 16f;
                        return true;
                    }
                }

                return false;
            }

            for (int y = y0 + 1; y <= y0 + down; y++)
            {
                if (Solid(tx, y))
                {
                    top = y * 16f;
                    return true;
                }
            }

            // Over a drop: it carries on level.
            top = (y0 + 1) * 16f;
            return true;
        }

        // ---- its beams ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// What one of its columns of laser lives by (PrimeBeamBase): whether what fires it is firing this tick, where it
        /// leaves from and the way it goes. An arm's leaves its tool the way that points; its head's, an eye.
        /// </summary>
        internal static bool BeamSource(NPC owner, int eye, out Vector2 from, out Vector2 way)
        {
            from = owner.Center;
            way = Vector2.UnitY;

            if (owner.type == NPCID.SkeletronPrime)
                return eye != 0 && owner.TryGetGlobalNPC(out Prime prime) && prime.EyeBeam(owner, eye, out from, out way);

            if (!IsArm(owner.type) || !owner.TryGetGlobalNPC(out PrimeArm arm) || !arm.Beaming)
                return false;

            way = PrimeArm.ToolWay(owner.rotation);
            from = PrimeArm.Tip(owner) - way * 4f;
            return true;
        }

        // ---- drawn ------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A mark line, smooth, straight into the world's batch, in its green: thin, flickering, brightening toward
        /// white as what it warns of nears; full for three quarters of its length, fading to nothing at its end (as the
        /// Destroyer's and the Twins' are).
        /// </summary>
        internal static void DrawMark(SpriteBatch batch, Vector2 from, float along, float length, float ready, float alpha, Color? tint = null)
        {
            if (Main.dedServ || alpha <= 0.01f || length <= 1f)
                return;

            ready = MathHelper.Clamp(ready, 0f, 1f);
            float flick = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.9f + from.X * 0.01f);
            Color colour = Color.Lerp(tint ?? PrimeArt.Nuclear, Color.White, ready * ready * 0.8f) * ((0.35f + 0.55f * ready) * flick * alpha);
            float width = 2f + 3f * ready;
            Vector2 dir = along.ToRotationVector2();
            float full = length * 0.75f;
            const int steps = 12;

            DestroyerArt.DrawLine(batch, from, from + dir * full, width * 4f, PrimeArt.Lit(colour * 0.3f));
            DestroyerArt.DrawLine(batch, from, from + dir * full, width, PrimeArt.Lit(colour));

            for (int i = 0; i < steps; i++)
            {
                float a = full + (length - full) * i / steps;
                float b = full + (length - full) * (i + 1) / steps;
                float left = 1f - (i + 0.5f) / steps;
                DestroyerArt.DrawLine(batch, from + dir * a, from + dir * b, width * 4f, PrimeArt.Lit(colour * (0.3f * left)));
                DestroyerArt.DrawLine(batch, from + dir * a, from + dir * b, width, PrimeArt.Lit(colour * left));
            }
        }

        /// <summary>
        /// A warning down a line: flashing (on and off, quicker as <paramref name="ready"/> goes to one; solid once
        /// <paramref name="locked"/>), as wide as what it warns of if a width is given (a faint band under it).
        /// <paramref name="alpha"/> fades all of it.
        /// </summary>
        internal static void DrawWarn(SpriteBatch batch, Vector2 from, float along, float length, float ready, bool locked, float band = 0f, float alpha = 1f)
        {
            if (Main.dedServ || alpha <= 0.01f)
                return;

            ready = MathHelper.Clamp(ready, 0f, 1f);
            float gap = MathHelper.Lerp(9f, 4f, ready);
            bool on = locked || Main.GameUpdateCount % (uint)(gap * 2f) < gap;
            float lit = (on ? 1f : 0.25f) * alpha;

            if (band > 0f)
            {
                Vector2 dir = along.ToRotationVector2();
                DestroyerArt.DrawLine(batch, from, from + dir * length * 0.9f, band, PrimeArt.Lit(PrimeArt.Nuclear * ((0.05f + 0.09f * ready) * lit)));
            }

            DrawMark(batch, from, along, length, locked ? 1f : ready * 0.7f, lit);
        }

        /// <summary>
        /// A mark laid along the ground from a place, a way (-1, 1), so far: over every rise and dip of it, flashing as
        /// <see cref="DrawWarn"/>, a faint band over it as high as what will come along it; fading out over its last
        /// quarter. <paramref name="gone"/>: how far along it what it warned of has already passed (that much is not drawn).
        /// </summary>
        internal static void DrawGroundMark(SpriteBatch batch, Vector2 from, int way, float length, float ready, bool locked, float high, float alpha = 1f, float gone = 0f)
        {
            if (Main.dedServ || alpha <= 0.01f || length <= 8f)
                return;

            ready = MathHelper.Clamp(ready, 0f, 1f);
            float gap = MathHelper.Lerp(9f, 4f, ready);
            bool on = locked || Main.GameUpdateCount % (uint)(gap * 2f) < gap;
            float lit = (on ? 1f : 0.25f) * alpha;
            float flick = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.9f + from.X * 0.01f);
            float shown = locked ? 1f : ready * 0.7f;
            Color colour = Color.Lerp(PrimeArt.Nuclear, Color.White, shown * shown * 0.8f) * ((0.35f + 0.55f * shown) * flick * lit);
            float width = 2f + 3f * shown;

            const float piece = 24f;
            Vector2 last = from;
            float y = from.Y;

            for (float along = piece; along <= length; along += piece)
            {
                float x = from.X + way * along;

                if (SurfaceNear(x, y + 2f, 4, 8, out float top))
                    y = top;

                Vector2 next = new(x, y - 3f);

                if (along > gone)
                {
                    float left = MathHelper.Clamp((length - along) / (length * 0.25f), 0f, 1f);
                    DestroyerArt.DrawLine(batch, last, next, width * 4f, PrimeArt.Lit(colour * (0.3f * left)));
                    DestroyerArt.DrawLine(batch, last, next, width, PrimeArt.Lit(colour * left));

                    if (high > 0f)
                        DestroyerArt.DrawLine(batch, last - new Vector2(0f, high * 0.5f), next - new Vector2(0f, high * 0.5f), high,
                            PrimeArt.Lit(PrimeArt.Nuclear * ((0.04f + 0.07f * ready) * lit * left)));
                }

                last = next;
            }
        }

        // ---- told across the network ----------------------------------------------------------------------------------------

        private void SendLegends(BinaryWriter writer)
        {
            writer.Write((byte)act);
            writer.Write((byte)Math.Clamp(count, 0, 255));
            writer.Write((sbyte)side);
            writer.Write((short)grabbed);
            writer.Write(angle);
            writer.Write(span);
            writer.Write(markA.X);
            writer.Write(markA.Y);
            writer.Write(markB.X);
            writer.Write(markB.Y);
        }

        private void ReceiveLegends(BinaryReader reader)
        {
            act = (Act)reader.ReadByte();
            count = reader.ReadByte();
            side = reader.ReadSByte();
            grabbed = reader.ReadInt16();
            angle = reader.ReadSingle();
            span = reader.ReadSingle();
            markA = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            markB = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
