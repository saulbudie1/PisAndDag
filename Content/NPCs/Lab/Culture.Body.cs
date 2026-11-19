using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Lab
{
    public partial class Culture
    {
        /// <summary>A blob of its knot: where about its middle (a turn and a share of its size), which picture, its own time; a haze one is drawn faint, behind.</summary>
        private struct Lobe
        {
            public float Angle, Out, Phase;
            public int Kind;
            public bool Haze;
        }

        /// <summary>An eye in it: which of the three, where, and its blinking (ticks to the next, ticks shut).</summary>
        private struct Eye
        {
            public int Kind;
            public float Angle, Out;
            public int Next, Shut;
        }

        /// <summary>A short strand curling out of it: its way out, its length, its own time, which way it curls.</summary>
        private struct Curl
        {
            public float Angle, Length, Phase, Sense;
        }

        private const int Lobes = 14, Hazes = 6, Curls = 16;

        private Lobe[] lobes;
        private Eye[] eyes;
        private Curl[] curls;

        /// <summary>The knot thrown about by what hits it (not the mass: only how it is drawn), and coming back.</summary>
        private Vector2 jolt, joltSpeed;

        /// <summary>Ticks of its hurt colour left.</summary>
        private int flash;


        /// <summary>Its six tentacles, its knot, its eyes and its curls, each its own; thick ones and thin ones.</summary>
        private void Grow()
        {
            var r = new Random(NPC.whoAmI * 7919 + (int)(NPC.position.X / 16f));

            arms = new Tentacle[6];

            for (int k = 0; k < arms.Length; k++)
                arms[k] = new Tentacle(NPC.Center, k / 6f * MathHelper.TwoPi + (float)r.NextDouble() * 0.6f, 6, (float)r.NextDouble() * 6.3f);

            lobes = new Lobe[Lobes];

            for (int k = 0; k < Lobes; k++)
            {
                bool haze = k < Hazes;
                lobes[k] = new Lobe
                {
                    Angle = (float)r.NextDouble() * MathHelper.TwoPi,
                    Out = haze ? 0.65f + (float)r.NextDouble() * 0.25f : (float)r.NextDouble() * 0.7f,
                    Phase = (float)r.NextDouble() * 6.3f,
                    Kind = haze ? r.Next(0, 2) : r.Next(1, 5),
                    Haze = haze,
                };
            }

            int many = 1 + r.Next(2);
            eyes = new Eye[many];

            for (int k = 0; k < many; k++)
                eyes[k] = new Eye { Kind = r.Next(3), Angle = (float)r.NextDouble() * MathHelper.TwoPi, Out = 0.2f + (float)r.NextDouble() * 0.35f, Next = 60 + r.Next(240) };

            curls = new Curl[Curls];

            for (int k = 0; k < Curls; k++)
                curls[k] = new Curl
                {
                    Angle = k / (float)Curls * MathHelper.TwoPi + (float)r.NextDouble() * 0.6f - 0.3f,
                    Length = 16f + (float)r.NextDouble() * 20f,
                    Phase = (float)r.NextDouble() * 6.3f,
                    Sense = r.Next(2) == 0 ? -1f : 1f,
                };
        }

        // ---- the holds -----------------------------------------------------------------------------------------------

        /// <summary>Most hooks out at once (holding or on their way); the rest writhe free.</summary>
        private const int Hooks = 3;

        /// <summary>How near (pixels) the mass comes to a hold before it lets go of it; the least a new one is thrown.</summary>
        private const float Reached = 40f;
        private const float Throws = 48f;

        /// <summary>How much further (pixels) than the rope it has let out the mass may get from a hold before it lets go: it does not stretch.</summary>
        private const float Slack = 20f;

        /// <summary>
        /// Its hooks, each tick of its crawl: one that has just taken hold yanks the mass toward it; one it has come up
        /// to, gone past, stretched too far, or that a wall has come between it and (it has gone round a corner), let
        /// go of; one the mass is getting further from than the rope it let out (it is not rubber: it lets go rather
        /// than stretch) let go of too; one still on its way, if a wall comes between, the same; while fewer than three are out, a free one thrown, a few ticks
        /// apart, as far ahead as there is a hold (FindHold).
        /// </summary>
        private void Hold(Player prey)
        {
            int out_ = 0;

            foreach (Tentacle t in arms)
                if (t.State != Tentacle.Hold.Free)
                    out_++;

            for (int k = 0; k < arms.Length; k++)
            {
                Tentacle t = arms[k];

                if (t.State == Tentacle.Hold.Holding)
                {
                    Vector2 to = t.Anchor - NPC.Center;
                    float d = to.Length();

                    if (t.JustHeld)
                    {
                        t.JustHeld = false;
                        t.Rope = d;

                        if (d > 1f)
                            NPC.velocity += to / d * Yank;

                        Dust.NewDust(t.Anchor - new Vector2(3f), 6, 6, DustID.Blood, 0f, 0f);
                    }

                    bool reached = d < Reached;
                    bool behind = d > Reached && Vector2.Dot(to / d, way) < -0.1f;
                    bool through = !Clear(t.At[0], t.Anchor);
                    bool stretched = d > t.Rope + Slack || d > Tentacle.Reach * Tentacle.Stretch;

                    if (reached || behind || through || stretched)
                    {
                        t.LetGo(Main.rand.Next(3, 11));
                        out_--;
                    }

                    continue;
                }

                if (t.State == Tentacle.Hold.Reaching)
                {
                    if (!Clear(t.At[0], t.Anchor))
                    {
                        t.LetGo(4);
                        out_--;
                    }

                    continue;
                }

                if (t.Rest > 0 || out_ >= Hooks)
                    continue;

                if (FindHold(MathF.Atan2(way.Y, way.X), out Vector2 hold))
                {
                    t.ReachFor(hold);
                    out_++;

                    // The next one a few ticks after, so that they go out one after another.
                    foreach (Tentacle other in arms)
                        if (other.State == Tentacle.Hold.Free && other.Rest < 4)
                            other.Rest = 4;
                }
                else
                {
                    t.Rest = 6;
                }
            }
        }

        /// <summary>Nothing solid on the straight line between these two places.</summary>
        private static bool Clear(Vector2 from, Vector2 to) => Collision.CanHitLine(from - Vector2.One, 2, 2, to - Vector2.One, 2, 2);

        /// <summary>
        /// Where to throw a hook, looking out from it along this way: of where a hook would take hold along each of
        /// fourteen ways in a fan about it (each a little off at random; a block, or a wall behind: FirstSolid), the one
        /// furthest off and most ahead, not where another
        /// hook already is; at least three tiles off if any is, else whatever there is nearer, so that it can still go on
        /// where there is little room.
        /// </summary>
        private bool FindHold(float angle, out Vector2 hold)
        {
            hold = default;
            float best = 0f, bestNear = 0f;
            Vector2 near = default;

            for (int k = 0; k < 14; k++)
            {
                float off = -1.3f + k * 0.2f + Main.rand.NextFloat(-0.08f, 0.08f);

                if (!FirstSolid(angle + off, out Vector2 at, out float gone) || gone <= 12f)
                    continue;

                float score = gone * (0.35f + Math.Max(0f, MathF.Cos(off)));

                foreach (Tentacle t in arms)
                    if (t.State != Tentacle.Hold.Free && Vector2.Distance(t.Anchor, at) < 48f)
                        score *= 0.3f;

                if (gone >= Throws && score > best)
                {
                    best = score;
                    hold = at;
                }
                else if (gone < Throws && score > bestNear)
                {
                    bestNear = score;
                    near = at;
                }
            }

            if (best > 0f)
                return true;

            hold = near;
            return bestNear > 0f;
        }

        /// <summary>
        /// Where a hook along this way would take hold within a tentacle's reach, and how far that is: the open point
        /// before the first solid thing on it; or, short of one, the furthest point on it with a wall behind (by request:
        /// "it doesnt just hook to tiles it hooks to any wall too"), as far as a hook thrown goes this time (seven
        /// tenths of its reach to all of it, so that hooks thrown the same way do not all take hold at one place).
        /// </summary>
        private bool FirstSolid(float angle, out Vector2 hold, out float gone)
        {
            Vector2 step = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 6f;
            Vector2 at = NPC.Center;
            float throws = Tentacle.Reach * Main.rand.NextFloat(0.7f, 1f);
            Vector2 walled = default;
            float walledGone = 0f;
            hold = default;

            for (gone = 0f; gone < throws; gone += 6f)
            {
                Vector2 next = at + step;
                int x = (int)(next.X / 16f), y = (int)(next.Y / 16f);

                // Never into the safe room, nor out of the world.
                if (!WorldGen.InWorld(x, y, 2) || CulturePath.InLab && CulturePath.InSafe(x, y))
                    break;

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                {
                    hold = at;
                    return true;
                }

                at = next;

                if (tile.WallType > 0)
                {
                    walled = at;
                    walledGone = gone + 6f;
                }
            }

            hold = walled;
            gone = walledGone;
            return walledGone > 0f;
        }

        // ---- its tentacles moving ---------------------------------------------------------------------------------------

        /// <summary>
        /// Every tentacle's tick: its root on the edge of the knot, its way out of it; a free one feeling about, wildly, out
        /// to its side of the knot; while it draws itself in to leap, every one held close; flying, trailing behind it.
        /// </summary>
        private void Writhe(Player prey)
        {
            float edge = Math.Min(NPC.width, NPC.height) * 0.3f;

            for (int k = 0; k < arms.Length; k++)
            {
                Tentacle t = arms[k];
                float turn = t.Angle + MathF.Sin(time * 2.1f + t.Phase) * 0.75f;
                Vector2 outward = new Vector2(MathF.Cos(turn), MathF.Sin(turn));

                // A hook comes out of the side of the knot that faces its hold.
                if (t.State != Tentacle.Hold.Free)
                    outward = (t.Anchor - NPC.Center).SafeNormalize(outward);
                Vector2 root = NPC.Center + outward * edge;
                Vector2 feel;

                if (act == Act.Leap)
                    feel = NPC.Center - NPC.velocity * 3.5f + outward * 18f;
                else if (act == Act.Draw)
                    feel = NPC.Center + outward * 14f;
                else
                    feel = NPC.Center + outward * (44f + 26f * MathF.Sin(time * 6.3f + t.Phase * 3f));

                t.Update(root, feel, time);
            }
        }

        // ---- shots on its tentacles -------------------------------------------------------------------------------------

        /// <summary>
        /// Every shot of a player's that is not on its mass but is on a tentacle: the tentacle thrown back where it was hit,
        /// the way the shot was going (and a hold near its tip maybe knocked loose); and, on the machine whose shot it is,
        /// the Culture hurt as a hit on it would hurt it and the shot spent as a hit spends it.
        ///
        /// Only what could hit it at all, by the game's own rules (CanHits): not a weapon held and not swinging, a pet, a
        /// sword that could not reach it through a wall; by the shot's own shape (Projectile.Colliding: a blade, a flail,
        /// a beam); and as often as it could hit its mass, the two sharing the game's own wait between hits (the Culture's
        /// for the player, the shot's own, or its kind's).
        /// </summary>
        private void Shot()
        {
            if (NPC.dontTakeDamage)
                return;

            Rectangle all = arms[0].Bounds;

            for (int k = 1; k < arms.Length; k++)
                all = Rectangle.Union(all, arms[k].Bounds);

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (!p.friendly || p.hostile || p.damage <= 0 || p.npcProj || p.owner < 0 || p.owner >= Main.maxPlayers || Main.projPet[p.type])
                    continue;

                Rectangle box = p.Hitbox;

                // Near a tentacle at all (cheaply first), and not on the mass (the game's own hit sees to that).
                if (!box.Intersects(all) && !p.Colliding(box, all))
                    continue;

                if (!Ready(p) || !CanHits(p) || p.Colliding(box, NPC.Hitbox))
                    continue;

                Player owner = Main.player[p.owner];

                foreach (Tentacle t in arms)
                {
                    if (!p.Colliding(box, t.Bounds))
                        continue;

                    int hit = -1;

                    for (int i = 1; i < Tentacle.Points && hit < 0; i++)
                    {
                        int reach = t.ThickAt(i) / 2 + 5;
                        Vector2 shown = t.Shown(i);
                        Rectangle part = new Rectangle((int)shown.X - reach, (int)shown.Y - reach, reach * 2, reach * 2);

                        if (!p.Colliding(box, part))
                            continue;

                        if (p.ownerHitCheck && !Collision.CanHitLine(owner.position, owner.width, owner.height, part.TopLeft(), part.Width, part.Height))
                            continue;

                        hit = i;
                    }

                    if (hit < 0)
                        continue;

                    Vector2 going = p.velocity.SafeNormalize(Vector2.UnitX * p.direction);
                    t.Struck(hit, going * (6f + p.knockBack * 1.6f));

                    for (int b = 0; b < 3; b++)
                        Dust.NewDust(t.Shown(hit) - new Vector2(3f), 6, 6, DustID.Blood, going.X * 2f, going.Y * 2f - 1f);

                    if (p.owner == Main.myPlayer)
                    {
                        bool crit = Main.rand.Next(100) < p.CritChance;

                        NPC.SimpleStrikeNPC(p.damage, going.X >= 0f ? 1 : -1, crit, 0f, p.DamageType, true, owner.luck);
                        Wait(p);

                        if (p.penetrate == 1)
                            p.Kill();
                        else if (p.penetrate > 1)
                            p.penetrate--;
                    }

                    break;
                }
            }
        }

        /// <summary>Whether this shot could hit it now, by the game's own wait between hits: the shot's, its kind's, or the Culture's for its player.</summary>
        private bool Ready(Projectile p)
        {
            int me = NPC.whoAmI;

            if (p.usesLocalNPCImmunity)
                return p.localNPCImmunity[me] == 0;

            if (p.usesIDStaticNPCImmunity)
                return Projectile.perIDStaticNPCImmunity[p.type][me] <= Main.GameUpdateCount;

            return NPC.immune[p.owner] <= 0;
        }

        /// <summary>The wait after a hit, as the game would have it after a hit on the mass.</summary>
        private void Wait(Projectile p)
        {
            int me = NPC.whoAmI;

            if (p.usesLocalNPCImmunity)
                p.localNPCImmunity[me] = p.localNPCHitCooldown == -1 ? -1 : Math.Max(1, p.localNPCHitCooldown);
            else if (p.usesIDStaticNPCImmunity)
                Projectile.perIDStaticNPCImmunity[p.type][me] = Main.GameUpdateCount + (uint)Math.Max(1, p.idStaticNPCHitCooldown);
            else
                NPC.immune[p.owner] = 10;
        }

        /// <summary>Whether this shot may hit it at all, by every say the game and the mods have in it: not a weapon held and not swinging, nothing told not to.</summary>
        private bool CanHits(Projectile p)
        {
            if (ProjectileLoader.CanDamage(p) == false)
                return false;

            if (ProjectileLoader.CanHitNPC(p, NPC) == false)
                return false;

            if (PlayerLoader.CanHitNPCWithProj(Main.player[p.owner], p, NPC) == false)
                return false;

            return NPCLoader.CanBeHitByProjectile(NPC, p) != false;
        }

        // ---- blood, and the knot settling ---------------------------------------------------------------------------------

        /// <summary>Blood off it as it goes, now and then: off the knot's underside, and off a tentacle somewhere along it.</summary>
        private void Bleed(int much)
        {
            if (Main.dedServ)
                return;

            for (int k = 0; k < much; k++)
            {
                if (Main.rand.NextBool(7))
                {
                    Vector2 at = NPC.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * NPC.width, NPC.height * 0.3f);
                    Dust drop = Dust.NewDustDirect(at, 2, 2, DustID.Blood, 0f, 0.6f, 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                    drop.velocity.X *= 0.3f;
                }

                if (Main.rand.NextBool(9))
                {
                    Tentacle t = arms[Main.rand.Next(arms.Length)];
                    Vector2 at = t.At[Main.rand.Next(2, Tentacle.Points)];
                    Dust drop = Dust.NewDustDirect(at, 2, 2, DustID.Blood, 0f, 0.4f, 0, default, Main.rand.NextFloat(0.7f, 1f));
                    drop.velocity.X *= 0.2f;
                }
            }
        }

        /// <summary>The knot springing back from what threw it about; the eyes blinking, each in its own time (open while it leaps).</summary>
        private void Settle()
        {
            joltSpeed += -jolt * 0.22f;
            joltSpeed *= 0.78f;
            jolt += joltSpeed;

            if (flash > 0)
                flash--;

            for (int k = 0; k < eyes.Length; k++)
            {
                ref Eye e = ref eyes[k];

                if (act == Act.Draw || act == Act.Leap)
                {
                    e.Shut = 0;
                    continue;
                }

                if (e.Shut > 0)
                {
                    e.Shut--;
                    continue;
                }

                if (--e.Next <= 0)
                {
                    e.Shut = 10;
                    e.Next = 90 + Main.rand.Next(300);
                }
            }
        }

        /// <summary>
        /// Hit: the knot thrown about the way the hit went and lit with hurt a moment, blood out of it; at its end, come apart
        /// into pieces that dissolve (CultureGore), and a burst of blood.
        /// </summary>
        public override void HitEffect(NPC.HitInfo hit)
        {
            joltSpeed += new Vector2(hit.HitDirection * 3.5f, -1f);
            flash = 5;

            if (Main.dedServ)
                return;

            for (int k = 0; k < 6; k++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection * 2f, -1.5f);

            if (NPC.life > 0)
                return;

            for (int k = 0; k < 40; k++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, 2f), 0, default, 1.4f);

            var source = NPC.GetSource_Death();
            int lump = ModContent.GoreType<CultureLump>(), strand = ModContent.GoreType<CultureStrand>();

            for (int k = 0; k < 5; k++)
                Gore.NewGore(source, NPC.Center + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 2f, lump);

            if (arms != null)
                foreach (Tentacle t in arms)
                    for (int i = 3; i < Tentacle.Points; i += 4)
                        Gore.NewGore(source, t.At[i], Main.rand.NextVector2Circular(2f, 2f), strand);

            if (eyes != null)
                foreach (Eye e in eyes)
                    Gore.NewGore(source, NPC.Center, Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 3f, ModContent.GoreType<CultureEyeGore>());

            Gore.NewGore(source, NPC.Center, new Vector2(hit.HitDirection * 2f, -3f), ModContent.GoreType<CultureTeeth>());
        }

        /// <summary>Nothing: what it leaves dissolves (by request: "gore that dissolves itself thats it").</summary>
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
        }
    }
}
