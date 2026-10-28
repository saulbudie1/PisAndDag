using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- his own moves (Legends, his hands gone) ------------------------------------------------------------------------
    //
    // By request, nine:
    //
    //     DIVE      his head-smash, reworked: he circles out to a place off the player from which a line through them
    //               meets something solid; the path is marked all the way; he spins, and comes; whoever is in his way
    //               dies; what he hits cracks him (a crack more each time) and takes a fifteenth of his life
    //     BREATH    the indicated skulls, reworked: up over the player, his skull lifted off his jaw, six skulls breathed
    //               out and up; each draws the bright line it will take, and takes it
    //     CHOMP     after them, jaws snapping, a quarter again as fast as they are going
    //     INFERNO   up over them, and a Diabolist's fire breathed down round them, five bursts
    //     RAISE     a roar, and skeletons up out of the ground round them (on platforms too)
    //     BLAZE     his eyes on fire; a breath of fire from them, purple at his mouth going red, fifty tiles at most,
    //               turned slowly after them as he comes slowly on
    //     SPIN      the rolling head: vanilla's own spin
    //     VOLLEY    homing skulls, three spits of four
    //     JAW       he spins where he is and throws his jaw: out past them, turning, and back to him

    public partial class LegendsSkeletron
    {
        /// <summary>A move's own state on this machine, cleared when a new one starts here.</summary>
        private void FreshMove()
        {
            landed = false;
            pathFound = false;
            riseKnown = false;
            ballLands = int.MaxValue;
            ballFlight = 0f;
        }

        /// <summary>After the server's word: a dive's path found again from its mark and its way.</summary>
        private void AfterReceive(NPC npc)
        {
            if (move == Move.Dive && moveTimer >= DiveRise && !landed)
                Found(npc);
        }

        // =================================================================================================================
        // DIVE
        // =================================================================================================================

        private const int DiveRise = 60;
        private const int DiveMark = 75;
        private const int DiveFall = 45;
        private const int DiveRest = 60;
        private const int DiveTicks = DiveRise + DiveMark + DiveFall + DiveRest;

        /// <summary>How far off the player he dives from, whichever side; how fast he gathers speed and his most.</summary>
        private const float DiveFrom = 520f;
        private const float DiveAccel = 2f;
        private const float DiveMaxFall = 38f;

        /// <summary>How far past the player his path is looked along for something to hit (fifty tiles), a step at a time.</summary>
        private const float DiveReach = 800f;
        private const float PathStep = 4f;

        /// <summary>Tries at a side to dive from before he settles for straight down.</summary>
        private const int DiveTries = 16;

        /// <summary>The share of his life the ground takes from him.</summary>
        private const int SelfDamageShare = 15;

        /// <summary>How wide the marked path is, against his own width.</summary>
        private const float PathWidth = 1.4f;

        /// <summary>The path's two colours, swung between: slowly as he winds up, fast as he comes.</summary>
        private static readonly Color PathRed = new Color(255, 45, 35);
        private static readonly Color PathOrange = new Color(255, 150, 30);

        /// <summary>The dive's line, as this machine found it from the mark: how far past the mark his middle stops, whether against something, where and facing which way; and whether he is there.</summary>
        private bool pathFound;
        private float stop;
        private bool strikes;
        private Vector2 contact;
        private Vector2 facingOut;
        private bool landed;

        /// <summary>Where he was, off the player, when the dive began: the arc he circles out along starts there.</summary>
        private bool riseKnown;
        private float riseAngle;
        private float riseDistance;

        private void Dive(NPC npc, Player target)
        {
            float t = moveClock;
            Vector2 way = aim.ToRotationVector2();

            headHurt = 1f;
            headHarmless = t < DiveRise + DiveMark;

            if (t < DiveRise)
            {
                // Round the player to where he dives from, along an arc about them, never through them.
                if (!riseKnown)
                {
                    riseKnown = true;

                    Vector2 off = npc.Center - target.Center;

                    riseAngle = off == Vector2.Zero ? -MathHelper.PiOver2 : off.ToRotation();
                    riseDistance = MathHelper.Clamp(off.Length(), 200f, 900f);
                }

                float p = Ease(t / (float)DiveRise);
                float from = (-way).ToRotation();
                float angle = riseAngle + MathHelper.WrapAngle(from - riseAngle) * p;
                float distance = MathHelper.Lerp(riseDistance, DiveFrom, p);

                Approach(npc, target.Center + angle.ToRotationVector2() * distance, 0.3f, 48f);
                npc.rotation *= 0.9f;
                eyesWant = p;
                eyeColour = new Color(255, 60, 40);
            }
            else if (t < DiveRise + DiveMark)
            {
                if (At(DiveRise))
                {
                    // At them where they are now if he can come down cleanly there, else where they were when he chose.
                    Fix(npc, CleanFrom(target.Center, way, Radius(npc)) ? target.Center : mark);
                    Found(npc);
                    Sound(SkeletronSounds.RoarBig with { Volume = 0.7f, Pitch = 0.2f }, npc.Center);
                }

                if (!pathFound)
                    Found(npc);

                // Where he will come from, fixed (glided to, if they moved off it), and turning faster and faster.
                Approach(npc, mark - way * DiveFrom, 0.4f, 64f);

                float wind = (t - DiveRise) / (float)DiveMark;
                npc.rotation += MathHelper.Lerp(0.05f, 0.6f, wind * wind);
                eyesWant = 1f;
                jawWant = 16f * wind;

                if (!Main.dedServ && Main.GameUpdateCount % 8 == 0)
                    SoundEngine.PlaySound(SoundID.Item7 with { Pitch = -0.5f + wind, Volume = 0.7f, MaxInstances = 3 }, npc.Center);
            }
            else if (t < DiveRise + DiveMark + DiveFall && !landed)
            {
                if (!pathFound)
                    Found(npc);

                npc.rotation += 0.6f;
                eyesWant = 1f;
                jawWant = 20f;

                // Down the line, faster each tick, and held to it.
                Vector2 start = mark - way * DiveFrom;
                Vector2 off = npc.Center - start;
                float along = Vector2.Dot(off, way);
                Vector2 drift = off - way * along;
                float speed = Math.Min(Math.Max(0f, Vector2.Dot(npc.velocity, way)) + DiveAccel, DiveMaxFall);
                float end = DiveFrom + stop;

                if (along + speed >= end)
                {
                    speed = Math.Max(0f, end - along);
                    landed = true;
                }

                npc.velocity = way * speed - drift;

                // Whoever is in his way, on their own machine: no hit, no immunity, dead.
                if (!Main.dedServ)
                    Crush(npc);

                if (landed && strikes)
                    Landing(npc);
            }
            else
            {
                // Landed: the spin he came down in let go (from the angle he is at, not the turns he made).
                npc.velocity = Vector2.Zero;
                npc.rotation = MathHelper.WrapAngle(npc.rotation) * 0.8f;
                eyesWant = 0.5f;
            }

            if (Authority && t >= DiveTicks)
                End(npc);
        }

        /// <summary>His size, as the ball he is taken for when the path is found: half his width.</summary>
        private static float Radius(NPC npc) => npc.width * 0.5f;

        /// <summary>The path from the mark along his way, found on this machine.</summary>
        private void Found(NPC npc)
        {
            pathFound = true;
            strikes = FindPath(mark, aim.ToRotationVector2(), Radius(npc), out stop, out contact, out facingOut, out _);
        }

        /// <summary>
        /// The server's: which way he dives, as an angle. Any side of the player, the sides tried at random, the first taken
        /// that he can come down cleanly: from open air, with nothing between there and them, his line through them meeting
        /// something solid within DiveReach past them after a clear stretch (so never along the floor they stand on, nor up
        /// out of the ground). None: straight down.
        /// </summary>
        private static float ChooseWay(NPC npc, Player target)
        {
            for (int i = 0; i < DiveTries; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);

                if (CleanFrom(target.Center, angle.ToRotationVector2(), Radius(npc)))
                    return angle;
            }

            return MathHelper.PiOver2;
        }

        /// <summary>Whether he can dive cleanly through a point along a way: from open air, nothing between, and a clear stretch before what he hits.</summary>
        private static bool CleanFrom(Vector2 at, Vector2 way, float radius)
        {
            Vector2 start = at - way * DiveFrom;

            if (!WorldGen.InWorld((int)(start.X / 16f), (int)(start.Y / 16f), 10))
                return false;

            if (Touching(start, radius, way, out _) || !Collision.CanHitLine(start, 1, 1, at, 1, 1))
                return false;

            return FindPath(at, way, radius, out _, out _, out _, out bool clean) && clean;
        }

        /// <summary>
        /// Along <paramref name="way"/> from the player at <paramref name="mark"/>, where his middle stops: taken for a ball
        /// of <paramref name="radius"/>, stepped along the line from a little short of the mark, stopped the step before it
        /// first touches something solid once it has been clear (<paramref name="clean"/>), or where he is if it touches
        /// from the start. Nothing to hit within DiveReach past them, or the world's edge first: false, and he stops at the
        /// last of it.
        /// </summary>
        internal static bool FindPath(Vector2 mark, Vector2 way, float radius, out float stop, out Vector2 contact, out Vector2 facing, out bool clean)
        {
            bool clear = false;
            float lastClear = 0f;
            float lastIn = 0f;
            clean = false;

            for (float d = -radius; d <= DiveReach; d += PathStep)
            {
                Vector2 centre = mark + way * d;

                if (!WorldGen.InWorld((int)(centre.X / 16f), (int)(centre.Y / 16f), 10))
                    break;

                lastIn = d;

                if (!Touching(centre, radius, way, out Vector2 near))
                {
                    clear = true;
                    lastClear = d;
                    continue;
                }

                if (clear)
                {
                    stop = lastClear;
                    contact = near;
                    facing = (centre - near).SafeNormalize(-way);
                    clean = true;
                    return true;
                }

                if (d >= radius)
                {
                    Touching(mark, radius, way, out near);

                    stop = 0f;
                    contact = near;
                    facing = (mark - near).SafeNormalize(-way);
                    return true;
                }
            }

            stop = Math.Max(0f, lastIn);
            contact = mark + way * (stop + radius);
            facing = -way;
            return false;
        }

        /// <summary>Whether a ball touches a solid tile, and the nearest point of one to its middle. Platforms only from above.</summary>
        private static bool Touching(Vector2 centre, float radius, Vector2 way, out Vector2 near)
        {
            int left = (int)((centre.X - radius) / 16f);
            int right = (int)((centre.X + radius) / 16f);
            int top = (int)((centre.Y - radius) / 16f);
            int bottom = (int)((centre.Y + radius) / 16f);

            float best = radius * radius;
            bool touching = false;
            near = centre;

            for (int x = left; x <= right; x++)
            {
                for (int y = top; y <= bottom; y++)
                {
                    if (!WorldGen.InWorld(x, y, 2))
                        continue;

                    Tile tile = Main.tile[x, y];

                    if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                        continue;

                    float tileTop = y * 16f + (tile.IsHalfBlock ? 8f : 0f);
                    float tileBottom = y * 16f + 16f;

                    if (Main.tileSolidTop[tile.TileType])
                    {
                        if (way.Y <= 0f || centre.Y >= tileTop)
                            continue;

                        tileBottom = tileTop;
                    }

                    Vector2 point = new Vector2(MathHelper.Clamp(centre.X, x * 16f, x * 16f + 16f), MathHelper.Clamp(centre.Y, tileTop, tileBottom));
                    float distance = Vector2.DistanceSquared(centre, point);

                    if (distance < best)
                    {
                        best = distance;
                        near = point;
                        touching = true;
                    }
                }
            }

            return touching;
        }

        /// <summary>The local player, if the skull's path this tick covers them.</summary>
        private static void Crush(NPC npc)
        {
            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || me.ghost)
                return;

            float ignored = 0f;

            bool caught = npc.Hitbox.Intersects(me.Hitbox)
                || Collision.CheckAABBvLineCollision(me.position, new Vector2(me.width, me.height), npc.Center, npc.Center + npc.velocity, npc.width, ref ignored);

            if (!caught)
                return;

            me.KillMe(PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.UnderTheSkull", me.name)), 9999.0, npc.direction, false);
        }

        /// <summary>He hits it: it heaves, floor, wall or ceiling; his skull cracks; it takes its share of him (the server's).</summary>
        private void Landing(NPC npc)
        {
            if (!Main.dedServ)
            {
                float half = npc.width * 0.5f;

                if (facingOut.Y < 0f && Math.Abs(facingOut.Y) >= Math.Abs(facingOut.X))
                {
                    GroundWave.Slam(contact, half, 44, 28f, 40);
                    LegendsKingSlime.GroundDust(contact.X - half, contact.X + half, contact.Y, 1f);
                }
                else
                {
                    GroundWave.Strike(contact, facingOut, half, 44, 28f, 40);
                }

                SoundEngine.PlaySound(SkeletronSounds.Thud, contact);
                SoundEngine.PlaySound(SkeletronSounds.SelfCrack, npc.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath2 with { Pitch = -0.3f, Volume = 0.8f }, contact);

                Vector2 across = new Vector2(-facingOut.Y, facingOut.X);

                for (int i = 0; i < 40; i++)
                {
                    Dust.NewDustPerfect(contact + across * Main.rand.NextFloat(-half, half) + facingOut * Main.rand.NextFloat(0f, 30f),
                        DustID.Bone, across * Main.rand.NextFloat(-7f, 7f) + facingOut * Main.rand.NextFloat(2f, 8f), 0, default, Main.rand.NextFloat(1.1f, 1.8f));
                }

                // Bits of his own skull, off where it cracked.
                for (int i = 0; i < 14; i++)
                {
                    Vector2 speed = facingOut.RotatedByRandom(1.2f) * Main.rand.NextFloat(3f, 9f);
                    PADParticleHandler.SpawnParticle(new StoneChip(npc.Center + Main.rand.NextVector2Circular(30f, 30f), speed, new Color(214, 206, 170), Main.rand.Next(50, 80), 1.4f));
                }

                EmpressWarp.Ripple(contact, 18f, 30f, 12f, 26);
                Cinematic.Flash(new Color(255, 230, 190), 0.25f * Near(contact));
                Shake(contact, 16f, 40);
            }

            if (!Authority)
                return;

            cracked = Math.Min(3, cracked + 1);

            // Never the last blow: that is the player's.
            int damage = Math.Min(Math.Max(1, npc.lifeMax / SelfDamageShare), npc.life - 1);

            if (damage > 0)
            {
                NPC.HitInfo hit = new NPC.HitInfo
                {
                    Damage = damage,
                    HitDirection = 0,
                };

                npc.StrikeNPC(hit, false, true);

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendStrikeNPC(npc, in hit, -1);
            }

            npc.netUpdate = true;
        }

        /// <summary>
        /// The band he will come down, from him to what he hits: see-through, its colour swinging between red and orange,
        /// faster as the fall comes; chevrons running down it the way he will go; a ring where he lands.
        /// </summary>
        private void DrawDivePath(NPC npc, SpriteBatch spriteBatch)
        {
            if (moveTimer < DiveRise || landed || !pathFound)
                return;

            Vector2 way = aim.ToRotationVector2();
            Vector2 start = npc.Center;
            Vector2 end = mark + way * (stop + (strikes ? Radius(npc) : 0f));
            float length = Vector2.Dot(end - start, way);

            if (length <= 0f)
                return;

            float t = moveClock - DiveRise;
            float phase = t < DiveMark ? 0.15f * t + 0.45f * t * t / (2f * DiveMark) : 0.15f * DiveMark + 0.45f * DiveMark / 2f + 0.9f * (t - DiveMark);
            float swing = 0.5f + 0.5f * (float)Math.Sin(phase);
            float fade = Ease(t / 12f);

            Color hue = Color.Lerp(PathRed, PathOrange, swing);
            float width = npc.width * PathWidth;
            Vector2 across = new Vector2(-way.Y, way.X) * (width * 0.5f - 1f);

            SkeletronRig.Beam(spriteBatch, start, start + way * length, hue * ((0.1f + 0.06f * swing) * fade), width);
            SkeletronRig.Line(spriteBatch, start + across, start + across + way * length, hue * ((0.3f + 0.15f * swing) * fade), 2f);
            SkeletronRig.Line(spriteBatch, start - across, start - across + way * length, hue * ((0.3f + 0.15f * swing) * fade), 2f);

            // Chevrons running down it.
            float run = (phase * 18f) % 70f;

            for (float d = run; d < length; d += 70f)
            {
                Vector2 at = start + way * d;
                Vector2 back = -way * 18f;
                Vector2 side = new Vector2(-way.Y, way.X) * 18f;

                SkeletronRig.Line(spriteBatch, at, at + back + side, hue * (0.5f * fade), 3f);
                SkeletronRig.Line(spriteBatch, at, at + back - side, hue * (0.5f * fade), 3f);
            }

            if (strikes)
                SkeletronRig.Ring(spriteBatch, contact, Radius(npc) * (1.2f - 0.2f * swing), hue * (0.6f * fade), 3f, 40);
        }

        // =================================================================================================================
        // BREATH (the indicated skulls)
        // =================================================================================================================

        private const int BreathRise = 50;
        private const int BreathOpen = 20;
        private const int BreathSkulls = 6;
        private const int BreathGap = 6;
        private const int BreathClose = 24;
        private const int BreathTicks = BreathRise + BreathOpen + BreathSkulls * BreathGap + BreathClose;
        private const float BreathHeight = 300f;
        private const float BreathSpeed = 9f;
        private const float BreathFan = 1.05f;

        private void Breath(NPC npc, Player target)
        {
            float t = moveClock;
            int outFrom = BreathRise + BreathOpen;

            if (t < outFrom + BreathSkulls * BreathGap)
                Approach(npc, target.Center + new Vector2(0f, -BreathHeight), 0.12f, 16f);
            else
                npc.velocity *= 0.9f;

            npc.rotation *= 0.85f;

            float open = t < BreathRise ? 0f : t < outFrom ? Ease((t - BreathRise) / (float)BreathOpen) : t < outFrom + BreathSkulls * BreathGap ? 1f : 1f - Ease((t - outFrom - BreathSkulls * BreathGap) / (float)BreathClose);
            liftWant = 18f * open;
            jawWant = 22f * open;
            hatWant = 10f * open;
            eyesWant = open;
            eyeColour = new Color(255, 220, 80);

            if (At(BreathRise))
                Sound(SkeletronSounds.Rattle with { Pitch = -0.2f }, npc.Center);

            for (int i = 0; i < BreathSkulls; i++)
            {
                if (!At(outFrom + i * BreathGap))
                    continue;

                Vector2 mouth = MouthOf(npc);
                Sound(SkeletronSounds.Clack with { Volume = 0.6f, Pitch = 0.2f }, mouth);

                if (!Main.dedServ)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(10f, 6f), DustID.Bone, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1f)), 80, default, 1.1f);
                        dust.noGravity = true;
                    }
                }

                float angle = -MathHelper.PiOver2 + MathHelper.Lerp(-BreathFan, BreathFan, i / (float)(BreathSkulls - 1));
                Shoot(npc, mouth, angle.ToRotationVector2() * BreathSpeed, ModContent.ProjectileType<SkeletronSkull>(), Damage(npc, 17f, 17f), SkeletronSkull.Indicated, i);
            }

            if (Authority && t >= BreathTicks)
                End(npc);
        }

        // =================================================================================================================
        // CHOMP
        // =================================================================================================================

        private const int ChompWarn = 20;
        private const int ChompUntil = 200;
        private const int ChompEnd = 230;
        private const int ChompEvery = 14;

        private void Chomp(NPC npc, Player target)
        {
            float t = moveClock;
            eyesWant = 1f;
            eyeColour = new Color(255, 50, 40);
            faceRate = 0.6f;

            if (t < ChompWarn)
            {
                npc.velocity *= 0.85f;
                jawWant = 30f * EaseOut(t / 10f);
                npc.rotation = 0.05f * (float)Math.Sin(t * 1.3f);

                if (At(0))
                    Sound(SkeletronSounds.Clack, npc.Center);
            }
            else if (t < ChompUntil)
            {
                // A quarter again as fast as they are going (by request), and never slower than a walk.
                float speed = MathHelper.Clamp(1.25f * target.velocity.Length(), 4.5f, 17f);
                Vector2 want = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * speed;
                npc.velocity = Vector2.Lerp(npc.velocity, want, 0.12f);
                npc.rotation = npc.velocity.X * 0.03f;

                float bite = (t - ChompWarn) % ChompEvery;
                jawWant = 26f * Math.Abs((float)Math.Sin(MathHelper.Pi * bite / ChompEvery));
                headHurt = 1.3f;

                if (Each(ChompWarn, ChompEvery))
                    Sound(SkeletronSounds.Chomp, npc.Center);

                if (Authority && Each(0, 10))
                    npc.netUpdate = true;
            }
            else
            {
                npc.velocity *= 0.9f;
                npc.rotation *= 0.85f;
            }

            if (Authority && t >= ChompEnd)
                End(npc);
        }

        // =================================================================================================================
        // INFERNO
        // =================================================================================================================

        private const int InfernoOpen = 40;
        private const int InfernoFirst = 56;
        private const int InfernoEvery = 14;
        private const int InfernoBursts = 5;
        private const int InfernoEnd = 150;

        /// <summary>Where each burst lands, round where they are.</summary>
        private static readonly Vector2[] InfernoAround = { new(0f, 0f), new(-90f, 10f), new(90f, 10f), new(-45f, -30f), new(45f, -30f) };

        private void Inferno(NPC npc, Player target)
        {
            float t = moveClock;
            Approach(npc, target.Center + new Vector2(0f, -340f), 0.08f, 14f);
            npc.rotation *= 0.85f;

            float open = t < InfernoOpen ? 0f : t < InfernoEnd - 20 ? Ease((t - InfernoOpen) / 12f) : 1f - Ease((t - InfernoEnd + 20) / 16f);
            jawWant = 24f * open;
            liftWant = 8f * open;
            eyesWant = open;
            eyeColour = new Color(255, 140, 40);

            if (At(InfernoOpen))
                Sound(SoundID.Item74 with { Pitch = -0.4f, Volume = 0.7f }, npc.Center);

            for (int i = 0; i < InfernoBursts; i++)
            {
                if (!At(InfernoFirst + i * InfernoEvery))
                    continue;

                Vector2 mouth = MouthOf(npc);
                Vector2 point = target.Center + InfernoAround[i];
                Vector2 way = (point - mouth).SafeNormalize(Vector2.UnitY);

                // (The bolt makes the Diabolist's own sound as it is made.)
                Sound(SkeletronSounds.Clack with { Volume = 0.5f, Pitch = -0.3f }, mouth);

                if (!Main.dedServ)
                {
                    for (int k = 0; k < 10; k++)
                    {
                        Dust dust = Dust.NewDustPerfect(mouth, DustID.InfernoFork, way.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f), 0, default, 1.4f);
                        dust.noGravity = true;
                    }
                }

                int bolt = Shoot(npc, mouth, way * 10f, ProjectileID.InfernoHostileBolt, Damage(npc, 20f, 17f), point.X - 6f, point.Y - 6f);

                // The game's bolt bursts once it is past its point both ways, which a bolt going straight down or across
                // never is: it bursts when it gets there, then, whichever way it came (its death makes the burst).
                if (bolt >= 0 && bolt < Main.maxProjectiles)
                {
                    Main.projectile[bolt].timeLeft = (int)(Vector2.Distance(mouth, point) / 10f) + 2;
                    Main.projectile[bolt].netUpdate = true;
                }
            }

            if (Authority && t >= InfernoEnd)
                End(npc);
        }

        // =================================================================================================================
        // RAISE
        // =================================================================================================================

        private const int RaiseRoar = 20;
        private const int RaiseUp = 60;
        private const int RaiseEnd = 90;

        /// <summary>No more than this many of his skeletons about him at once.</summary>
        private const int RaiseMost = 6;

        private static readonly int[] Raised = { NPCID.Skeleton, NPCID.HeadacheSkeleton, NPCID.MisassembledSkeleton, NPCID.PantlessSkeleton };

        private void Raise(NPC npc, Player target)
        {
            float t = moveClock;
            Hover(npc, target, new Vector2(0f, -300f), 0.04f, 6f);
            npc.rotation *= 0.85f;

            float open = t < RaiseRoar ? EaseOut(t / 14f) : t < RaiseUp ? 1f : 1f - Ease((t - RaiseUp) / 20f);
            jawWant = 30f * open;
            liftWant = 10f * open;
            hatWant = 14f * open;
            eyesWant = open;
            eyeColour = new Color(255, 60, 40);

            if (At(RaiseRoar))
            {
                if (Authority)
                    ChooseRisings(npc, target);

                Sound(SkeletronSounds.RoarBig with { Volume = 0.8f, Pitch = 0.15f }, npc.Center);

                if (!Main.dedServ)
                    Cinematic.Roar(npc, 60, 0.6f, quiet: true, rim: new Color(150, 20, 16));
            }

            // Bone coming up out of the ground where they will stand.
            if (!Main.dedServ && t > RaiseRoar && t < RaiseUp && Main.GameUpdateCount % 3 == 0)
            {
                for (int i = 0; i < spotCount; i++)
                {
                    Vector2 at = spots[i] + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f);
                    Dust dust = Dust.NewDustPerfect(at, DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(2f, 5f)), 0, default, 1.2f);
                    dust.noGravity = false;
                }

                if (Each(0, 12))
                    Shake(target.Center, 1.5f, 6);
            }

            if (At(RaiseUp))
            {
                for (int i = 0; i < spotCount; i++)
                {
                    Vector2 at = spots[i];

                    if (!Main.dedServ)
                    {
                        SoundEngine.PlaySound(SkeletronSounds.Pop with { Volume = 0.6f, Pitch = 0.3f + i * 0.1f }, at);

                        for (int k = 0; k < 16; k++)
                            Dust.NewDustPerfect(at, DustID.Bone, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 8f)), 0, default, Main.rand.NextFloat(1.1f, 1.5f));
                    }

                    if (Authority)
                    {
                        int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)at.X, (int)at.Y, Raised[Main.rand.Next(Raised.Length)]);

                        if (index >= 0 && index < Main.maxNPCs)
                        {
                            Main.npc[index].velocity = new Vector2(0f, -4f);

                            if (Main.netMode == NetmodeID.Server)
                                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
                        }
                    }
                }
            }

            if (Authority && t >= RaiseEnd)
                End(npc);
        }

        /// <summary>
        /// The server's: where the skeletons come up, round the player: ground they can stand on (a platform as well as a
        /// block), with room over it, not on the player; as many as there is room for under the most of them.
        /// </summary>
        private void ChooseRisings(NPC npc, Player target)
        {
            int about = 0;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && Array.IndexOf(Raised, other.type) >= 0 && other.WithinRange(npc.Center, 2500f))
                    about++;
            }

            int want = Math.Min(4, RaiseMost - about);
            spotCount = 0;

            int px = (int)(target.Center.X / 16f);
            int py = (int)(target.Bottom.Y / 16f);

            for (int tries = 0; tries < 200 && spotCount < want; tries++)
            {
                int x = px + Main.rand.Next(-30, 31);

                if (Math.Abs(x - px) < 4)
                    continue;

                for (int y = py - 12; y <= py + 12; y++)
                {
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    Tile ground = Main.tile[x, y];

                    if (!ground.HasUnactuatedTile || !Main.tileSolid[ground.TileType] && !Main.tileSolidTop[ground.TileType])
                        continue;

                    bool room = true;

                    for (int k = 1; k <= 3 && room; k++)
                    {
                        Tile above = Main.tile[x, y - k];
                        room = !(above.HasUnactuatedTile && Main.tileSolid[above.TileType]);
                    }

                    if (!room)
                        continue;

                    Vector2 spot = new Vector2(x * 16f + 8f, y * 16f);
                    bool apart = true;

                    for (int k = 0; k < spotCount && apart; k++)
                        apart = Math.Abs(spots[k].X - spot.X) > 48f;

                    if (apart)
                        spots[spotCount++] = spot;

                    break;
                }
            }

            npc.netUpdate = true;
        }

        /// <summary>A red glow under the ground where each skeleton is coming up.</summary>
        private void DrawRisings(NPC npc, SpriteBatch spriteBatch)
        {
            float t = moveClock;

            if (t < RaiseRoar || t >= RaiseUp + 6)
                return;

            float p = (t - RaiseRoar) / (float)(RaiseUp - RaiseRoar);

            for (int i = 0; i < spotCount; i++)
            {
                SkeletronRig.Glow(spriteBatch, spots[i], new Color(220, 40, 30) * (0.4f * p), 30f + 20f * p);
                SkeletronRig.Hot(spriteBatch, spots[i], new Color(255, 150, 100) * (0.5f * p), 8f + 6f * p);
            }
        }

        // =================================================================================================================
        // BLAZE
        // =================================================================================================================

        private const int BlazeIgnite = 30;
        private const int BlazeUntil = 180;
        private const int BlazeEnd = 210;
        private const int BlazeGrow = 20;

        private void Blaze(NPC npc, Player target)
        {
            float t = moveClock;
            float toward = (target.Center - npc.Center).ToRotation();

            // Turned slowly after them.
            if (t < BlazeUntil)
                aim = aim.AngleTowards(toward, t < BlazeIgnite ? 0.08f : 0.022f);

            if (Authority && Each(0, 15))
                npc.netUpdate = true;

            // Slowly on (by request: "head moves slowly"), but not into them.
            float distance = Vector2.Distance(target.Center, npc.Center);
            Vector2 way = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            npc.velocity = Vector2.Lerp(npc.velocity, way * (distance > 260f ? 1.2f : -1.2f), 0.1f);
            npc.rotation *= 0.9f;

            flameWant = t < BlazeUntil ? 1f : 0f;
            eyeColour = BlazePurple;
            eyesWant = flameWant;

            float open = t < BlazeIgnite ? Ease(t / (float)BlazeIgnite) : t < BlazeUntil ? 1f : 1f - Ease((t - BlazeUntil) / 20f);
            jawWant = 24f * open;
            liftWant = 8f * open;

            if (At(0))
            {
                Sound(SkeletronSounds.Ignite, npc.Center);

                if (!Main.dedServ)
                {
                    for (int k = 0; k < 2; k++)
                        PADParticleHandler.SpawnParticle(new BloomGeneric(SkeletronRig.Eye(k == 0, npc.Center, npc.rotation, npc.scale, liftShown), Vector2.Zero, BlazePurple, 0.4f, 16));
                }
            }

            if (At(BlazeIgnite))
            {
                Sound(SkeletronSounds.FlameBreath, npc.Center);
                Shoot(npc, MouthOf(npc), Vector2.Zero, ModContent.ProjectileType<SkeletronBreath>(), Damage(npc, 18f, 15f), npc.whoAmI, 0f, serial);
            }

            if (Authority && t >= BlazeEnd)
                End(npc);
        }

        /// <summary>His breath of fire's line now (read by SkeletronBreath): from his mouth, the way he breathes it, as far as it has reached.</summary>
        internal bool BreathLine(NPC npc, out Vector2 from, out Vector2 way, out float length)
        {
            from = MouthOf(npc);
            way = aim.ToRotationVector2();
            length = 0f;

            float t = moveClock;

            if (move != Move.Blaze || t < BlazeIgnite || t >= BlazeUntil)
                return false;

            // Alive through the whole breath (made on its first tick, when it has no length yet).
            float grow = Ease((t - BlazeIgnite + 1) / (float)BlazeGrow);
            float die = t > BlazeUntil - 10 ? 1f - (t - BlazeUntil + 10) / 10f : 1f;
            length = SkeletronBreath.Reach * grow * die;
            return true;
        }

        // =================================================================================================================
        // SPIN (vanilla's)
        // =================================================================================================================

        private const int SpinTicks = 240;

        /// <summary>Vanilla's own spin (it was set going as this began: OnBegin), with a whirl of its sound; vanilla runs him.</summary>
        private bool Spin(NPC npc, Player target)
        {
            float t = moveClock;
            eyesWant = 1f;
            eyeColour = new Color(255, 60, 40);

            if (!Main.dedServ && Each(24, 24))
                SoundEngine.PlaySound(SoundID.Item7 with { Pitch = -0.3f + 0.5f * Math.Min(1f, t / 120f), Volume = 0.7f, MaxInstances = 3 }, npc.Center);

            if (Authority && t >= SpinTicks)
            {
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
                End(npc);
            }

            return true;
        }

        // =================================================================================================================
        // VOLLEY
        // =================================================================================================================

        private const int VolleyFirst = 20;
        private const int VolleyEvery = 30;
        private const int VolleyBursts = 3;
        private const int VolleyEnd = 110;

        private void Volley(NPC npc, Player target)
        {
            float t = moveClock;
            Hover(npc, target, new Vector2(0f, -260f), 0.05f, 7f);
            npc.rotation *= 0.85f;

            float open = 0f;

            for (int b = 0; b < VolleyBursts; b++)
            {
                int when = VolleyFirst + b * VolleyEvery;
                open = Math.Max(open, 1f - Math.Abs(t - when - 3) / 7f);
            }

            open = MathHelper.Clamp(open, 0f, 1f);
            liftWant = 18f * open;
            jawWant = 14f * open;
            hatWant = 12f * open;
            eyesWant = 0.6f;
            eyeColour = new Color(255, 220, 80);
            faceRate = 0.6f;

            for (int b = 0; b < VolleyBursts; b++)
            {
                if (!At(VolleyFirst + b * VolleyEvery))
                    continue;

                Vector2 mouth = MouthOf(npc);
                Sound(SkeletronSounds.Clack, mouth);
                Sound(SkeletronSounds.Rattle with { Volume = 0.5f }, mouth);

                Vector2 toward = (target.Center - mouth).SafeNormalize(Vector2.UnitY);

                for (int k = 0; k < 4; k++)
                {
                    float spread = MathHelper.Lerp(-0.45f, 0.45f, k / 3f);
                    Shoot(npc, mouth, toward.RotatedBy(spread) * 7f, ModContent.ProjectileType<SkeletronSkull>(), Damage(npc, 15f, 13f), SkeletronSkull.Homing, b * 4 + k);
                }
            }

            if (Authority && t >= VolleyEnd)
                End(npc);
        }

        // =================================================================================================================
        // JAW
        // =================================================================================================================

        private const int JawSpin = 40;
        private const int JawOut = 36;
        private const int JawBack = 30;
        private const int JawCaught = JawSpin + JawOut + JawBack;
        private const int JawEnd = JawCaught + 20;

        /// <summary>Where his jaw sits on him, from his middle.</summary>
        private static Vector2 JawHome(NPC npc) => npc.Center + new Vector2(0f, 27f * npc.scale).RotatedBy(npc.rotation);

        private void Jaw(NPC npc, Player target)
        {
            float t = moveClock;
            npc.velocity *= 0.9f;

            if (t < JawSpin)
            {
                float p = t / (float)JawSpin;
                npc.rotation += 0.05f + 0.45f * p * p;
                eyesWant = p;
            }
            else
            {
                npc.rotation = MathHelper.WrapAngle(npc.rotation) * 0.85f;
                eyesWant = 1f;
            }

            eyeColour = new Color(255, 60, 40);
            jawAway = t >= JawSpin && t < JawCaught;

            if (At(JawSpin))
            {
                Vector2 toward = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                float reach = Math.Min(Vector2.Distance(target.Center, npc.Center) + 220f, 760f);
                FixSecond(npc, npc.Center + toward * reach);

                Sound(SkeletronSounds.JawWhir, npc.Center);
                Sound(SkeletronSounds.Clack with { Pitch = -0.4f }, npc.Center);
                Shoot(npc, JawHome(npc), Vector2.Zero, ModContent.ProjectileType<JawBoomerang>(), Damage(npc, 22f, 18f), npc.whoAmI, 0f, serial);
            }

            if (At(JawSpin + 20))
                Sound(SkeletronSounds.Cackle with { Volume = 0.7f }, npc.Center);

            if (At(JawCaught))
            {
                Sound(SkeletronSounds.Clack with { Volume = 1f }, npc.Center);

                if (!Main.dedServ)
                {
                    for (int i = 0; i < 12; i++)
                        Dust.NewDustPerfect(JawHome(npc), DustID.Bone, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.2f);

                    Shake(npc.Center, 4f, 8);
                }
            }

            if (Authority && t >= JawEnd)
                End(npc);
        }

        /// <summary>Where his thrown jaw is now (read by JawBoomerang), and its turn: out to past them, then back to him.</summary>
        internal bool JawSpot(NPC npc, out Vector2 at, out float turn)
        {
            at = Vector2.Zero;
            turn = 0f;
            float t = moveClock;

            if (move != Move.Jaw || t < JawSpin || t >= JawCaught)
                return false;

            Vector2 home = JawHome(npc);
            turn = (t - JawSpin) * 0.55f;

            if (t < JawSpin + JawOut)
                at = Vector2.Lerp(home, mark2, EaseOut((t - JawSpin) / (float)JawOut));
            else
                at = Vector2.Lerp(mark2, home, EaseIn((t - JawSpin - JawOut) / (float)JawBack));

            return true;
        }
    }
}
