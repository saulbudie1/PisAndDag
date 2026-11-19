using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- what is seen and heard --------------------------------------------------------------------------------------------
    //
    //     Tremor      the ground shaking over it before it rises: dust thrown up off the ground, chips, a low rumble
    //     Breach      its head through the ground, either way: rock and dust thrown up and out along its way
    //     Spill       a segment through the ground: a little of the same
    //     HeadGlow    its eye's light, in its colour
    //     Bellow      its roar: the sound, the screen shaken and leaned in, dimmed about it, steam and sparks out of its
    //                 mouth the way it faces, its lightning crackling over its head
    //     Decay       its body breaking down: black smoke out of its creases, the Megashark's sparks now and then
    //     Explosion   one of the explosions along its body at its end
    //     Spark       the Megashark's spark: three short arcs, fire, a crackle, a wisp of smoke (its orange: by request)
    //     SegmentBurst, HeadBurst   a segment, and at the last its head, blown into its pieces
    //     and for its Legends moves: its lightning (Arc), its charge (ChargeWave), its mouth gathering and crackling,
    //     strikes, its body's zap, steam, a segment's pop
    //
    // No rings, no stars, nothing thrown out evenly all round (by request: "stop using the radial particle stuff").
    // Everything here is played on every machine from what every machine knows, and is only seen: nothing here hurts.
    // Particles on the half-size canvas, so on the two-pixel grid.

    public partial class Destroyer
    {
        private static readonly Color SparkColor = new(255, 186, 66);
        private static readonly Color Soot = new(14, 12, 12);
        private static readonly Color BurnRed = new(150, 44, 18);
        private static readonly Color Fire = new(255, 132, 48);
        private static readonly Color Steamy = new(214, 228, 228);

        private static readonly Color[] Sparks =
        {
            new(255, 186, 66),
            new(255, 222, 140),
            new(255, 140, 40),
            new(255, 250, 220),
        };

        private static readonly Color[] TealSparks =
        {
            new(127, 212, 209),
            new(196, 246, 242),
            new(90, 180, 200),
            new(240, 255, 255),
        };

        private static readonly Color[] RedSparks =
        {
            new(255, 52, 36),
            new(255, 120, 80),
            new(255, 200, 160),
        };

        /// <summary>Sounds played this tick, all of it: its end can ask for many at once.</summary>
        private static int soundsThisTick;

        /// <summary>Whether its head was in the ground last tick (this machine's), and a pause between eruptions.</summary>
        private bool headSolid, headSolidKnown;
        private int breachCool;

        private static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        /// <summary>Its lightning, drawn smooth (by request: "not everything need pixelation").</summary>
        private static void Arc(Vector2 from, Vector2 to, Color colour, float width, int lifetime = 16, int forks = 1)
        {
            if (!Main.dedServ)
                Lightning.Spawn(from, to, colour, width, lifetime, forks);
        }

        /// <summary>Room for this many more particles; the cosmetic ones thin out when it is short.</summary>
        private static float Room => MathHelper.Clamp(PADParticleHandler.FreeSlots / 400f, 0.2f, 1f);

        /// <summary>Whether a place is on screen, or this near it.</summary>
        private static bool Seen(Vector2 at, float margin) =>
            at.X > Main.screenPosition.X - margin && at.X < Main.screenPosition.X + Main.screenWidth + margin &&
            at.Y > Main.screenPosition.Y - margin && at.Y < Main.screenPosition.Y + Main.screenHeight + margin;

        /// <summary>
        /// Where its sounds are played from this tick (this machine's): the part of it, head or segment, nearest the middle
        /// of the screen, where the game hears from (its player, but for a cinematic's camera). By request: "sounds should
        /// be play at the segment nearest to the player". Null before its head's first tick here.
        /// </summary>
        private static Vector2? hearAt;

        /// <summary>The part of it nearest where sounds are heard from, found for this tick (after its body is known).</summary>
        private void Listen(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 ear = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            Vector2 best = npc.Center;
            float bestD = Vector2.DistanceSquared(best, ear);

            foreach (int i in chain)
            {
                NPC segment = Main.npc[i];

                if (segment.TryGetGlobalNPC(out DestroyerPart part) && part.Exploded)
                    continue;

                float d = Vector2.DistanceSquared(segment.Center, ear);

                if (d < bestD)
                {
                    bestD = d;
                    best = segment.Center;
                }
            }

            hearAt = best;
        }

        /// <summary>Where its sounds come from: the part of it nearest the listener (or its head, before that is known).</summary>
        private static Vector2 Here(NPC npc) => hearAt ?? npc.Center;

        /// <summary>A sound (from the part of it nearest the listener), unless this many have been played already this tick.</summary>
        private static void Sound(SoundStyle style, Vector2 at, int most)
        {
            if (Main.dedServ || soundsThisTick >= most)
                return;

            soundsThisTick++;
            SoundEngine.PlaySound(style, hearAt ?? at);
        }

        /// <summary>The screen shaken, less the farther the camera is from it.</summary>
        private static void Shake(Vector2 at, float power, int time)
        {
            if (Main.dedServ)
                return;

            Vector2 camera = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float near = 1f - MathHelper.Clamp((Vector2.Distance(camera, at) - 400f) / 2200f, 0f, 1f);

            if (near > 0.01f)
                Main.LocalPlayer.PIS().BumpScreenShake(power * near, time);
        }

        /// <summary>Where its head's front is: its eye, or its mouth a little further.</summary>
        private static Vector2 Front(NPC npc, float ahead = 14f) => npc.Center + (npc.rotation - MathHelper.PiOver2).ToRotationVector2() * ahead;

        /// <summary>The way its head faces.</summary>
        private static Vector2 Facing(NPC npc) => (npc.rotation - MathHelper.PiOver2).ToRotationVector2();

        /// <summary>The ground's colour at a place, near enough for its dust.</summary>
        private static Color GroundColour(Vector2 at)
        {
            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f);

            for (int k = 0; k < 4; k++)
            {
                if (!WorldGen.InWorld(x, y + k, 2))
                    break;

                Tile tile = Main.tile[x, y + k];

                if (!tile.HasTile)
                    continue;

                switch (tile.TileType)
                {
                    case TileID.Sand:
                    case TileID.Ebonsand:
                    case TileID.Crimsand:
                    case TileID.Pearlsand:
                    case TileID.HardenedSand:
                    case TileID.Sandstone:
                        return new Color(196, 168, 112);

                    case TileID.SnowBlock:
                    case TileID.IceBlock:
                        return new Color(198, 212, 228);

                    case TileID.Mud:
                    case TileID.JungleGrass:
                    case TileID.MushroomGrass:
                        return new Color(98, 74, 72);

                    case TileID.Stone:
                    case TileID.Ebonstone:
                    case TileID.Crimstone:
                    case TileID.Pearlstone:
                    case TileID.Granite:
                    case TileID.Marble:
                        return new Color(122, 122, 130);

                    case TileID.Ash:
                    case TileID.Hellstone:
                        return new Color(86, 80, 84);
                }

                return new Color(138, 100, 72);
            }

            return new Color(138, 100, 72);
        }

        // ---- the ground ---------------------------------------------------------------------------------------------------

        /// <summary>The ground shaking over it before it rises, harder as <paramref name="f"/> goes to one.</summary>
        private void Tremor(Vector2 at, float f)
        {
            if (Main.dedServ)
                return;

            f = MathHelper.Clamp(f, 0f, 1f);
            Shake(at, 1.5f + 9f * f * f, 8);

            int gap = (int)MathHelper.Lerp(30f, 13f, f);

            if (timer % gap == 0)
                Sound(SoundID.WormDig with { Volume = 0.3f + 0.6f * f, Pitch = -0.75f + 0.45f * f }, at, 4);

            float many = (0.6f + 2.6f * f) * Room;
            int whole = (int)many;

            if (Main.rand.NextFloat() < many - whole)
                whole++;

            int near = (int)(at.Y / 16f);
            int reach = 4 + (int)(10f * f);

            for (int i = 0; i < whole; i++)
            {
                int x = (int)(at.X / 16f) + Main.rand.Next(-reach, reach + 1);

                if (!WorldGen.InWorld(x, near, 10))
                    continue;

                int y = Ground(x, near);
                Vector2 on = new(x * 16 + 8, y * 16);
                Color ground = GroundColour(on);

                // The tile's own dust, thrown up.
                if (WorldGen.InWorld(x, y, 2))
                {
                    int d = WorldGen.KillTile_MakeTileDust(x, y, Main.tile[x, y]);

                    if (d >= 0 && d < Main.maxDust)
                    {
                        Main.dust[d].position = on + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f);
                        Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1f, 2.5f + 4f * f));
                    }
                }

                Add(new DustFog(on + new Vector2(Main.rand.NextFloat(-8f, 8f), -6f), new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.9f + 1.4f * f)),
                    ground * 0.9f, Main.rand.Next(34, 56), Main.rand.NextFloat(0.35f, 0.6f + 0.4f * f), 0.26f));

                if (f > 0.35f && Main.rand.NextBool(3))
                    Add(new StoneChip(on + new Vector2(0f, -4f), new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 3f + 5f * f)),
                        ground, Main.rand.Next(40, 70), Main.rand.NextFloat(0.8f, 1.2f)));
            }
        }

        /// <summary>Its head through the ground, whichever way: rock thrown where it went.</summary>
        private void Breach(NPC npc, float power)
        {
            if (breachCool > 0)
                breachCool--;

            bool solid = Solid(npc.Center);

            if (!headSolidKnown)
            {
                headSolid = solid;
                headSolidKnown = true;
                return;
            }

            if (solid == headSolid)
                return;

            headSolid = solid;

            if (breachCool > 0)
                return;

            breachCool = 10;
            Eruption(npc.Center, npc.velocity, power, !solid);
        }

        /// <summary>
        /// Rock thrown out of the ground where something went through it: out of it (<paramref name="outward"/>), along its
        /// way and up; into it, up and back. A spout, not a ring: thrown the way it went.
        /// </summary>
        private static void Eruption(Vector2 at, Vector2 way, float power, bool outward)
        {
            if (Main.dedServ)
                return;

            way = way.SafeNormalize(-Vector2.UnitY);
            Vector2 throwWay = outward ? (way * 0.7f - Vector2.UnitY * 0.6f).SafeNormalize(-Vector2.UnitY) : (-way * 0.35f - Vector2.UnitY).SafeNormalize(-Vector2.UnitY);
            Color ground = GroundColour(at);
            float room = Room;

            for (int i = 0; i < (int)(16 * power * room); i++)
                Add(new StoneChip(at + Main.rand.NextVector2Circular(12f, 6f), throwWay.RotatedByRandom(0.45f) * Main.rand.NextFloat(4f, 11f) * power,
                    Color.Lerp(ground, Color.Black, Main.rand.NextFloat(0f, 0.35f)), Main.rand.Next(50, 90), Main.rand.NextFloat(0.9f, 1.4f)));

            for (int i = 0; i < (int)(8 * power * room); i++)
                Add(new DustFog(at + throwWay * Main.rand.NextFloat(0f, 30f) + Main.rand.NextVector2Circular(14f, 8f), throwWay.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 3.2f) * power,
                    ground * 0.85f, Main.rand.Next(46, 76), Main.rand.NextFloat(0.6f, 1.05f) * power, 0.3f));

            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f);

            for (int i = 0; i < 12; i++)
            {
                int tx = x + Main.rand.Next(-2, 3), ty = y + Main.rand.Next(-2, 3);

                if (!WorldGen.InWorld(tx, ty, 2) || !Main.tile[tx, ty].HasTile)
                    continue;

                int d = WorldGen.KillTile_MakeTileDust(tx, ty, Main.tile[tx, ty]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = throwWay.RotatedByRandom(0.6f) * Main.rand.NextFloat(3f, 8f);
            }

            Sound(SoundID.Item14 with { Volume = 0.45f * power, Pitch = -0.7f, PitchVariance = 0.15f }, at, 5);
            Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.8f * Math.Min(1f, power), Pitch = -0.2f, PitchVariance = 0.2f }, at, 5);
            Shake(at, 5f * power, 14);
        }

        /// <summary>A segment through the ground: a little of the same.</summary>
        internal static void Spill(NPC segment, bool outward)
        {
            if (Main.dedServ)
                return;

            Vector2 at = segment.Center;
            Vector2 way = segment.velocity.SafeNormalize(-Vector2.UnitY);
            Vector2 throwWay = outward ? (way * 0.5f - Vector2.UnitY).SafeNormalize(-Vector2.UnitY) : -Vector2.UnitY;
            Color ground = GroundColour(at);

            if (Main.rand.NextFloat() < Room)
            {
                Add(new DustFog(at + Main.rand.NextVector2Circular(12f, 6f), throwWay.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.5f, 1.6f),
                    ground * 0.8f, Main.rand.Next(36, 56), Main.rand.NextFloat(0.45f, 0.75f), 0.26f));

                Add(new StoneChip(at, throwWay.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 6f),
                    Color.Lerp(ground, Color.Black, Main.rand.NextFloat(0f, 0.3f)), Main.rand.Next(40, 70), Main.rand.NextFloat(0.8f, 1.2f)));
            }

            int x = (int)(at.X / 16f), y = (int)(at.Y / 16f);

            if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile)
            {
                int d = WorldGen.KillTile_MakeTileDust(x, y, Main.tile[x, y]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = throwWay.RotatedByRandom(0.6f) * Main.rand.NextFloat(1.5f, 4f);
            }
        }

        /// <summary>Whether a place is in the ground (for its body).</summary>
        internal static bool InGround(Vector2 at) => Solid(at);

        // ---- its head --------------------------------------------------------------------------------------------------------

        /// <summary>Its eye's light in its colour; <paramref name="flare"/> a flash off it.</summary>
        private static void HeadGlow(NPC npc, float strength, bool flare = false)
        {
            if (Main.dedServ || strength <= 0f)
                return;

            Vector2 eye = Front(npc);
            Color teal = DestroyerArt.Teal;
            Lighting.AddLight(eye, teal.R / 255f * 0.9f * strength, teal.G / 255f * 0.9f * strength, teal.B / 255f * 0.9f * strength);
            Add(new BloomGeneric(eye, npc.velocity, teal * MathHelper.Clamp(0.35f * strength, 0f, 0.9f), 0.16f + 0.1f * strength, 2, lights: false));

            if (flare)
                Add(new BloomGeneric(eye, npc.velocity, DestroyerArt.TealHot * 0.8f, 0.55f, 10));
        }

        /// <summary>
        /// Its roar (no rings: by request). The sound; the screen shaken and leaned in a little, dimmed about it; steam and
        /// sparks out of its mouth the way it faces; its lightning crackling over its head while it lasts (<see cref="BellowTick"/>).
        /// </summary>
        private static void Bellow(NPC npc, SoundStyle sound, float strength)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(sound, Here(npc));
            Shake(npc.Center, 12f * strength, 50);

            Vector2 mouth = Front(npc, 22f);
            Vector2 way = Facing(npc);
            Add(new BloomGeneric(mouth, Vector2.Zero, DestroyerArt.Teal * 0.8f, 1.1f * strength, 26));

            for (int i = 0; i < (int)(22 * strength * Room); i++)
            {
                Vector2 v = way.RotatedByRandom(0.32f) * Main.rand.NextFloat(3f, 9f);
                Add(new DustFog(mouth + way * Main.rand.NextFloat(0f, 20f), v, Steamy * 0.8f, Main.rand.Next(30, 50), Main.rand.NextFloat(0.5f, 0.9f), 0.22f));
            }

            for (int i = 0; i < (int)(10 * strength); i++)
                Add(new Ember(mouth, way.RotatedByRandom(0.4f) * Main.rand.NextFloat(4f, 10f), TealSparks[Main.rand.Next(TealSparks.Length)], Main.rand.NextFloat(0.5f, 0.8f), Main.rand.Next(20, 34)));
        }

        /// <summary>While it roars: the screen dimmed about it and leaned in, its lightning crackling about its head, its mouth steaming.</summary>
        private static void BellowTick(NPC npc, float voice)
        {
            if (Main.dedServ || voice <= 0.01f)
                return;

            if (Watching(npc.Center))
                Cinematic.Shade(npc.Center, 520f, 0.35f * voice);

            Vector2 mouth = Front(npc, 22f);
            Vector2 way = Facing(npc);

            if (Main.rand.NextFloat() < 0.35f * voice)
                Arc(npc.Center + Main.rand.NextVector2Circular(20f, 20f), npc.Center + Main.rand.NextVector2CircularEdge(70f, 70f) * Main.rand.NextFloat(0.6f, 1f), DestroyerArt.Teal, Main.rand.NextFloat(5f, 9f), 12, 1);

            if (Main.rand.NextFloat() < 0.8f * voice * Room)
                Add(new DustFog(mouth, way.RotatedByRandom(0.3f) * Main.rand.NextFloat(2f, 5f) * voice, Steamy * 0.7f, Main.rand.Next(26, 40), Main.rand.NextFloat(0.4f, 0.7f), 0.2f));
        }

        // ---- its body breaking down ----------------------------------------------------------------------------------

        /// <summary>
        /// Its body breaking down: black smoke out of its creases, about <paramref name="rate"/> a tick, to either side of
        /// it; now and then the Megashark's spark at one (unless <paramref name="sparks"/> is off). On screen first.
        /// </summary>
        private void Decay(NPC npc, float rate, bool sparks = true)
        {
            if (Main.dedServ || chain.Count < 2)
                return;

            rate *= Room;
            int whole = (int)rate;

            if (Main.rand.NextFloat() < rate - whole)
                whole++;

            for (int n = 0; n < whole; n++)
            {
                if (OnScreenCrease(npc, out Vector2 at, out Vector2 along))
                    CreaseSmoke(at, along);
            }

            if (sparks && Main.rand.NextFloat() < 0.03f + 0.05f * rate && OnScreenCrease(npc, out Vector2 spark, out _))
                Spark(spark);
        }

        /// <summary>A crease on screen (four tries), not of a segment already blown up.</summary>
        private bool OnScreenCrease(NPC npc, out Vector2 at, out Vector2 along)
        {
            at = default;
            along = Vector2.UnitY;

            if (chain.Count < 2)
                return false;

            for (int tries = 0; tries < 4; tries++)
            {
                int i = Main.rand.Next(-1, chain.Count - 1);
                NPC behind = Main.npc[chain[i + 1]];

                if (behind.TryGetGlobalNPC(out DestroyerPart part) && part.Exploded)
                    continue;

                at = Crease(npc, i, out along);

                if (Seen(at, 80f))
                    return true;
            }

            return false;
        }

        /// <summary>A puff of black smoke out of a crease, to one side or the other of its body.</summary>
        private static void CreaseSmoke(Vector2 at, Vector2 along)
        {
            Vector2 side = along.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
            Vector2 velocity = side * Main.rand.NextFloat(0.7f, 2.2f) + new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.7f));

            Add(new BurnSmoke(at + side * 6f, velocity, Soot, BurnRed, Main.rand.Next(34, 58),
                Main.rand.NextFloat(8f, 13f), Main.rand.NextFloat(26f, 42f), Main.rand.NextFloat(0.82f, 1f)));

            if (Main.rand.NextBool(4))
                Add(new FlameSmoke(at, velocity * 0.6f, new Color(30, 26, 26), Main.rand.Next(30, 46), 6f, 22f, 0.5f));
        }

        /// <summary>Steam out of a crease, to one side or the other, rising (its second wind).</summary>
        private static void Steam(Vector2 at, Vector2 along, float strength)
        {
            Vector2 side = along.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
            Vector2 velocity = side * Main.rand.NextFloat(1.2f, 3.2f) * strength + new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f));
            Add(new DustFog(at + side * 8f, velocity, Steamy * 0.75f, Main.rand.Next(30, 52), Main.rand.NextFloat(0.4f, 0.75f) * (0.6f + 0.4f * strength), 0.2f));
        }

        /// <summary>The Megashark's spark: three short arcs, fire, a crackle, a wisp of smoke.</summary>
        private static void Spark(Vector2 at)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < 3; i++)
                Arc(at, at + Main.rand.NextVector2Circular(18f, 15f), SparkColor, Main.rand.NextFloat(9f, 14f), Main.rand.Next(14, 19), Main.rand.Next(2));

            for (int i = 0; i < 7; i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.Torch, Main.rand.NextVector2Circular(2.2f, 2.2f), 0, default, 1.1f);
                dust.noGravity = true;
            }

            Add(new MuzzleSmoke(at + Main.rand.NextVector2Circular(3f, 3f),
                new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.2f)), Main.rand.Next(26, 40), Main.rand.NextFloat(0.2f, 0.34f)));

            Sound(SoundID.Item93 with { Volume = 0.5f, Pitch = Main.rand.NextFloat(-0.3f, 0.25f) }, at, 4);
            Shake(at, 1.6f, 6);
        }

        // ---- its end -----------------------------------------------------------------------------------------------------------

        /// <summary>An explosion on its body: a flash, fire and black smoke, sparks thrown, its own lightning out of it.</summary>
        private static void Explosion(Vector2 at, float scale, bool loud)
        {
            if (Main.dedServ)
                return;

            SoundStyle sound = (loud ? SoundID.Item62 : SoundID.Item14) with { Volume = loud ? 0.8f : 0.6f, Pitch = Main.rand.NextFloat(-0.35f, 0.1f) };

            // Off screen it is only heard (its body is long, and most of it blows up out of sight).
            if (!Seen(at, 240f))
            {
                Sound(sound, at, 3);
                return;
            }

            float room = Room;

            Add(new BloomGeneric(at, Vector2.Zero, Fire * 0.9f, 0.8f * scale, 14));
            Add(new BloomGeneric(at, Vector2.Zero, new Color(255, 236, 200) * 0.8f, 0.35f * scale, 8));

            for (int i = 0; i < (int)(6 * scale * room) + 1; i++)
                Add(new FlameSmoke(at + Main.rand.NextVector2Circular(10f, 10f), Main.rand.NextVector2Circular(2.6f, 2.6f) * scale,
                    new Color(40, 34, 32), Main.rand.Next(34, 56), 10f * scale, 34f * scale, 0.7f));

            for (int i = 0; i < (int)(5 * scale * room) + 1; i++)
                Add(new BurnSmoke(at + Main.rand.NextVector2Circular(12f, 12f), Main.rand.NextVector2Circular(2.2f, 2.2f) + new Vector2(0f, -0.5f),
                    Soot, BurnRed, Main.rand.Next(40, 64), Main.rand.NextFloat(10f, 15f) * scale, Main.rand.NextFloat(32f, 50f) * scale, Main.rand.NextFloat(0.85f, 1f)));

            SparkShard.Spray(at, SparkColor, (int)(8 * scale * room) + 2, 9f * scale, 30f * scale, 16, falls: true, palette: Sparks);

            for (int i = 0; i < 2; i++)
                Arc(at, at + Main.rand.NextVector2Circular(40f, 34f) * scale, DestroyerArt.Teal, 11f * scale, 16, 1);

            for (int i = 0; i < (int)(6 * scale * room); i++)
                Add(new Ember(at, Main.rand.NextVector2Circular(3.5f, 3.5f) * scale + new Vector2(0f, -1f), Fire, Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(24, 40)));

            for (int i = 0; i < 10; i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.Torch, Main.rand.NextVector2Circular(4f, 4f) * scale, 0, default, 1.6f);
                dust.noGravity = true;
            }

            Sound(sound, at, 3);
            Shake(at, (loud ? 7f : 4.5f) * scale, 16);
        }

        /// <summary>A segment blown into its pieces, in its chain from its tail up.</summary>
        private void SegmentBurst(NPC segment, int index)
        {
            bool nearHead = index < NearHead;
            Explosion(segment.Center, nearHead ? 1f : 0.75f, index % 4 == 0 || nearHead && index % 2 == 0);

            if (Main.dedServ)
                return;

            Vector2 thrown = segment.velocity * 0.3f + Main.rand.NextVector2Circular(3f, 3f) + new Vector2(0f, -1.5f);
            Gore.NewGore(segment.GetSource_Death(), segment.position, thrown, 156);

            if (index % 2 == 0)
                Gore.NewGore(segment.GetSource_Death(), segment.Center - new Vector2(16f), Main.rand.NextVector2Circular(1.5f, 1.5f), Main.rand.Next(61, 64));
        }

        /// <summary>Its head, last, and the biggest of them.</summary>
        private static void HeadBurst(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = npc.Center;

            for (int i = 0; i < 5; i++)
                Explosion(at + Main.rand.NextVector2Circular(34f, 34f), 1.35f, i == 0);

            Add(new BloomGeneric(at, Vector2.Zero, new Color(255, 200, 150), 2.4f, 26));
            Add(new BloomGeneric(at, Vector2.Zero, DestroyerArt.TealHot * 0.7f, 1.4f, 18));
            SparkShard.Spray(at, SparkColor, 26, 15f, 48f, 22, falls: true, palette: Sparks);

            for (int i = 0; i < 6; i++)
                Arc(at, at + Main.rand.NextVector2Circular(160f, 120f), DestroyerArt.Teal, Main.rand.NextFloat(12f, 18f), 22, 2);

            for (int i = 0; i < 2; i++)
                Gore.NewGore(npc.GetSource_Death(), npc.position, Main.rand.NextVector2Circular(4f, 4f) + new Vector2(0f, -2f), 156);

            for (int i = 0; i < 6; i++)
                Gore.NewGore(npc.GetSource_Death(), at - new Vector2(16f) + Main.rand.NextVector2Circular(20f, 20f), Main.rand.NextVector2Circular(2.5f, 2.5f), Main.rand.Next(61, 64));

            if (Watching(at))
                Cinematic.Flash(new Color(255, 214, 170), 0.45f);

            soundsThisTick = 0;
            Sound(SoundID.NPCDeath14 with { Volume = 1f }, at, 8);
            Sound(SoundID.Item62 with { Volume = 1f, Pitch = -0.45f }, at, 8);
            Shake(at, 24f, 46);
        }

        // ---- its Legends moves --------------------------------------------------------------------------------------------

        /// <summary>
        /// Its charge from tail to head (by request: "it charges up from the tail and glow spark red"): every segment behind
        /// <paramref name="front"/> (an index into its body, the tail's count down to 0) glows red, and the front throws
        /// red sparks as it passes.
        /// </summary>
        private void ChargeWave(NPC npc, float front)
        {
            chargeFront = front;

            if (Main.dedServ)
                return;

            int at = (int)front;

            for (int k = at; k <= at + 1 && k < chain.Count; k++)
            {
                if (k < 0)
                    continue;

                NPC segment = Main.npc[chain[k]];

                if (!Seen(segment.Center, 60f))
                    continue;

                if (Main.rand.NextBool(2))
                {
                    Vector2 side = (segment.rotation + (Main.rand.NextBool() ? 0f : MathHelper.Pi)).ToRotationVector2();
                    Add(new Ember(segment.Center, side.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f), RedSparks[Main.rand.Next(RedSparks.Length)], Main.rand.NextFloat(0.45f, 0.7f), Main.rand.Next(16, 28)));
                }

                Add(new BloomGeneric(segment.Center, segment.velocity, DestroyerArt.ChargeRed * 0.7f, 0.35f, 8));
                Lighting.AddLight(segment.Center, 1f, 0.2f, 0.12f);
            }
        }

        /// <summary>Its mouth gathering its light before it fires: growing, crackling.</summary>
        private static void MouthGather(NPC npc, float power)
        {
            if (Main.dedServ || power <= 0f)
                return;

            Vector2 mouth = Front(npc, 26f);
            Add(new BloomGeneric(mouth, npc.velocity, DestroyerArt.Teal * (0.5f * power), 0.3f + 0.9f * power, 3));
            Add(new BloomGeneric(mouth, npc.velocity, Color.White * (0.45f * power), 0.12f + 0.35f * power, 3));
            Lighting.AddLight(mouth, 0.5f * power, 0.85f * power, 0.85f * power);

            // Its light drawn in toward its mouth from ahead of it.
            if (Main.rand.NextFloat() < 0.6f * power)
            {
                Vector2 from = mouth + Facing(npc).RotatedByRandom(1.2f) * Main.rand.NextFloat(60f, 140f);
                Add(new BloomGeneric(from, (mouth - from) / 14f + npc.velocity, DestroyerArt.TealHot * 0.7f, 0.12f, 14));
            }

            if (Main.rand.NextFloat() < 0.25f * power)
                MouthCrackle(npc, 30f + 40f * power);
        }

        /// <summary>Its lightning crackling at its mouth.</summary>
        private static void MouthCrackle(NPC npc, float reach)
        {
            Vector2 mouth = Front(npc, 24f);
            Arc(mouth, mouth + Facing(npc).RotatedByRandom(1.1f) * Main.rand.NextFloat(0.5f, 1f) * reach, DestroyerArt.Teal, Main.rand.NextFloat(4f, 8f), Main.rand.Next(8, 13), 0);
        }

        /// <summary>A strike (harmless): its lightning from its mouth to a place, and a flash where it lands.</summary>
        private static void Strike(Vector2 from, Vector2 to)
        {
            if (Main.dedServ)
                return;

            Arc(from, to, DestroyerArt.Teal, Main.rand.NextFloat(16f, 22f), 20, 2);
            Arc(from, to, Color.White * 0.6f, 6f, 10, 0);
            Add(new BloomGeneric(to, Vector2.Zero, DestroyerArt.Teal * 0.8f, 0.9f, 16));
            Add(new BloomGeneric(to, Vector2.Zero, Color.White * 0.6f, 0.35f, 8));
            for (int i = 0; i < 6; i++)
                Add(new Ember(to, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 6f)), TealSparks[Main.rand.Next(TealSparks.Length)], Main.rand.NextFloat(0.45f, 0.7f), Main.rand.Next(18, 30)));

            if (Solid(to + new Vector2(0f, 10f)))
            {
                for (int i = 0; i < 4; i++)
                    Add(new DustFog(to, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), GroundColour(to) * 0.7f, Main.rand.Next(30, 46), Main.rand.NextFloat(0.4f, 0.7f)));
            }

            Lighting.AddLight(to, 0.6f, 1f, 1f);
            Sound(SoundID.Item94 with { Volume = 0.55f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) }, to, 4);
            Shake(to, 3f, 10);
        }

        /// <summary>Its body's zap (its storm): its lightning along it and off it, a little way, all down its length on screen.</summary>
        private void ZapBody(NPC npc)
        {
            if (Main.dedServ)
                return;

            for (int i = -1; i < chain.Count - 1; i++)
            {
                Vector2 at = Crease(npc, i, out Vector2 along);

                if (!Seen(at, 60f))
                    continue;

                if (i % 3 == 0)
                    Arc(at, at + along * -88f + along.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-14f, 14f), DestroyerArt.Yellow, 6f, 12, 0);

                if (Main.rand.NextBool(4))
                    Arc(at, at + Main.rand.NextVector2CircularEdge(48f, 48f), DestroyerArt.Teal, 5f, 10, 0);
            }

            Sound(SoundID.Item94 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center, 6);
            Sound(SoundID.Item93 with { Volume = 0.7f, Pitch = 0.2f }, npc.Center, 6);
            Shake(npc.Center, 3.5f, 12);
        }

        /// <summary>A segment pops out a Probe: a flash, sparks and steam thrown the way it goes.</summary>
        private static void PopBurst(Vector2 at, Vector2 way)
        {
            if (Main.dedServ || !Seen(at, 100f))
                return;

            Add(new BloomGeneric(at, Vector2.Zero, DestroyerArt.Teal * 0.8f, 0.5f, 12));
            for (int i = 0; i < 4; i++)
                Add(new Ember(at, way.RotatedByRandom(0.45f) * Main.rand.NextFloat(3f, 7f), TealSparks[Main.rand.Next(TealSparks.Length)], Main.rand.NextFloat(0.45f, 0.7f), Main.rand.Next(18, 30)));

            for (int i = 0; i < 3; i++)
                Add(new DustFog(at, way.RotatedByRandom(0.4f) * Main.rand.NextFloat(2f, 4.5f), Steamy * 0.7f, Main.rand.Next(26, 42), Main.rand.NextFloat(0.4f, 0.7f), 0.2f));
        }
    }
}
