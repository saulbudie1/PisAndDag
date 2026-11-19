using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- what is seen and heard -------------------------------------------------------------------------------------------
    //
    //     Spark       a spark off it in its colour (the Retinazer's fire, the Spazmatism's toxic green): three short arcs,
    //                 its dust, a crackle, a wisp of smoke
    //     Smoke       black smoke out of it, rising (trailing, as it falls)
    //     EyeLight    its eye's light in its colour, guttering if asked
    //     Impact      it hits the ground: rock and dust out to both sides along it, a thud that shakes the screen
    //     Charging    its mark held on the player: the Spazmatism's gas leaking out of its mouth, the Retinazer's mouth
    //                 red-hot and spitting; ChargeTrail, the same left behind as they fly
    //     and the small things of their coming and their end: whooshes, flares, the flash, rattles, a whir
    //
    // No rings, nothing thrown evenly all round (by request): sprays go the way something went, smoke rises. Everything
    // here is only seen, played on every machine from what every machine knows. Particles on the two-pixel grid; arcs
    // smooth.

    public partial class Twins
    {
        private static readonly Color Soot = new(14, 12, 12);
        private static readonly Color BurnRed = new(150, 44, 18);
        private static readonly Color Gas = new(92, 186, 70);

        private static readonly Color[] FireSparks = { new(255, 120, 50), new(255, 190, 110), new(255, 236, 190), Fire };
        private static readonly Color[] ToxicSparks = { new(150, 255, 90), new(210, 255, 150), new(240, 255, 210), Toxic };

        private static Color[] SparksOf(NPC npc) => IsRet(npc) ? FireSparks : ToxicSparks;

        private static int DustOf(NPC npc) => IsRet(npc) ? DustID.Torch : DustID.CursedTorch;

        /// <summary>Sounds played this tick, all of them: their end can ask for many at once.</summary>
        private static int soundsThisTick;
        private static uint soundTick;

        private static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        private static float Room => MathHelper.Clamp(PADParticleHandler.FreeSlots / 400f, 0.2f, 1f);

        private static bool Seen(Vector2 at, float margin) =>
            at.X > Main.screenPosition.X - margin && at.X < Main.screenPosition.X + Main.screenWidth + margin &&
            at.Y > Main.screenPosition.Y - margin && at.Y < Main.screenPosition.Y + Main.screenHeight + margin;

        private static void Sound(SoundStyle style, Vector2 at, int most = 4)
        {
            if (Main.dedServ)
                return;

            if (soundTick != Main.GameUpdateCount)
            {
                soundTick = Main.GameUpdateCount;
                soundsThisTick = 0;
            }

            if (soundsThisTick >= most)
                return;

            soundsThisTick++;
            SoundEngine.PlaySound(style, at);
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

        private static void Arc(Vector2 from, Vector2 to, Color colour, float width, int lifetime = 14, int forks = 1)
        {
            if (!Main.dedServ)
                Lightning.Spawn(from, to, colour, width, lifetime, forks);
        }

        /// <summary>
        /// A fleck of its colour thrown off it: the Retinazer's an ember, cooling as fire does; the Spazmatism's keeping
        /// its green as it goes (an ember would cool it to orange).
        /// </summary>
        private static void Fleck(NPC npc, Vector2 at, Vector2 velocity, float scale, int life, float lift = 0.04f)
        {
            Color[] sparks = SparksOf(npc);
            Color c = sparks[Main.rand.Next(sparks.Length)];

            if (IsRet(npc))
                Add(new Ember(at, velocity, c, scale, life, lift));
            else
                Add(new HoneyMote(at, velocity, c, scale, life, lift));
        }

        /// <summary>A place on its eyeball, picked by a number.</summary>
        private static Vector2 OnBall(NPC npc, Random rng, float reach = 44f) =>
            EyeCentre(npc) + new Vector2((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f) * reach * npc.scale;

        // ---- breaking down --------------------------------------------------------------------------------------------------

        /// <summary>A spark off it in its colour: three short arcs, its dust, a crackle, a wisp of smoke.</summary>
        private static void Spark(NPC npc, int pick, float power)
        {
            if (Main.dedServ || !Seen(npc.Center, 200f))
                return;

            Random rng = new(pick);
            Vector2 at = OnBall(npc, rng);
            Color[] sparks = SparksOf(npc);

            for (int i = 0; i < 3; i++)
                Arc(at, at + Main.rand.NextVector2Circular(22f, 18f) * power, sparks[Main.rand.Next(sparks.Length)], Main.rand.NextFloat(8f, 13f) * power, Main.rand.Next(12, 18), Main.rand.Next(2));

            Add(new BloomGeneric(at, Vector2.Zero, sparks[0] * 0.7f, 0.3f + 0.25f * power, 10));

            for (int i = 0; i < (int)(6 + 5 * power); i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustOf(npc), Main.rand.NextVector2Circular(2.6f, 2.6f) * power + new Vector2(0f, -0.6f), 0, default, 1.2f);
                dust.noGravity = Main.rand.NextBool(3);
            }

            for (int i = 0; i < (int)(3 * power) + 1; i++)
                Fleck(npc, at, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1f, 4f)) * power, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(20, 36), lift: -0.08f);

            Add(new MuzzleSmoke(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.2f)), Main.rand.Next(26, 40), Main.rand.NextFloat(0.22f, 0.36f)));

            Sound(SoundID.Item93 with { Volume = 0.5f * power, Pitch = Main.rand.NextFloat(-0.3f, 0.25f) }, at);
            Shake(at, 1.8f * power, 6);
        }

        /// <summary>Black smoke out of it, about <paramref name="rate"/> a tick: rising off it, or left hanging behind as it falls.</summary>
        private static void Smoke(NPC npc, float rate, bool falling = false)
        {
            if (Main.dedServ || !Seen(npc.Center, 160f))
                return;

            rate *= Room;
            int whole = (int)rate;

            if (Main.rand.NextFloat() < rate - whole)
                whole++;

            for (int i = 0; i < whole; i++)
            {
                Vector2 at = EyeCentre(npc) + Main.rand.NextVector2Circular(40f, 40f) * npc.scale;
                Vector2 v = falling
                    ? npc.velocity * 0.15f + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1.2f))
                    : new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.6f, 1.6f));

                Add(new BurnSmoke(at, v, Soot, BurnRed, Main.rand.Next(40, 70), Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(34f, 56f), Main.rand.NextFloat(0.82f, 1f)));
            }
        }

        /// <summary>Its eye's light in its colour; <paramref name="flicker"/> how much it gutters.</summary>
        private static void EyeLight(NPC npc, float strength, float flicker = 0f)
        {
            if (Main.dedServ)
                return;

            if (flicker > 0f && Main.rand.NextFloat() < flicker * 0.5f)
                strength *= Main.rand.NextFloat(0.1f, 0.6f);

            if (strength <= 0.01f)
                return;

            Color c = ElementOf(npc);
            Vector2 eye = Front(npc, 50f);
            Lighting.AddLight(eye, c.R / 255f * strength, c.G / 255f * strength, c.B / 255f * strength);
            Add(new BloomGeneric(eye, npc.velocity, c * MathHelper.Clamp(0.32f * strength, 0f, 0.9f), 0.18f + 0.12f * strength, 2, lights: false));
        }

        /// <summary>The blow that should have killed it: a jolt, a burst of its sparks, a puff of smoke.</summary>
        private static void Failing(NPC npc)
        {
            if (Main.dedServ)
                return;

            Spark(npc, Main.rand.Next(), 1.2f);
            Spark(npc, Main.rand.Next(), 1f);

            for (int i = 0; i < 6; i++)
                Add(new BurnSmoke(EyeCentre(npc) + Main.rand.NextVector2Circular(36f, 36f), new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.8f, 2f)), Soot, BurnRed, Main.rand.Next(50, 80), 16f, 50f, 0.95f));

            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.45f }, npc.Center);
            Sound(SoundID.Item14 with { Volume = 0.55f, Pitch = -0.3f }, npc.Center);
            Shake(npc.Center, 6f, 18);
        }

        /// <summary>It gives out and drops: a last spark, a falling whine.</summary>
        private static void Dropping(NPC npc)
        {
            if (Main.dedServ)
                return;

            Spark(npc, Main.rand.Next(), 1.3f);
            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = -0.6f }, npc.Center);
            Sound(SoundID.NPCHit4 with { Volume = 0.7f, Pitch = -0.7f }, npc.Center);
        }

        /// <summary>
        /// It hits the ground hard (by request: "falls with big impact"): rock and dust out to both sides along it, the
        /// ground's own dust thrown up, its sparks, smoke; a thud that shakes the screen. Not a ring: two spouts and a roll.
        /// </summary>
        private void Impact(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 ground = new(npc.Center.X, groundY);
            Color dust = new(120, 104, 90);
            float room = Room;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 spout = new Vector2(side * 0.75f, -1f).SafeNormalize(-Vector2.UnitY);

                for (int i = 0; i < (int)(16 * room); i++)
                    Add(new StoneChip(ground + new Vector2(side * Main.rand.NextFloat(10f, 50f), -6f), spout.RotatedByRandom(0.4f) * Main.rand.NextFloat(4f, 12f),
                        Color.Lerp(dust, Color.Black, Main.rand.NextFloat(0f, 0.4f)), Main.rand.Next(50, 90), Main.rand.NextFloat(0.9f, 1.5f)));

                for (int i = 0; i < (int)(12 * room); i++)
                    Add(new DustFog(ground + new Vector2(side * Main.rand.NextFloat(0f, 60f), -8f), new Vector2(side * Main.rand.NextFloat(1.5f, 5.5f), -Main.rand.NextFloat(0.2f, 1.2f)),
                        dust * 0.85f, Main.rand.Next(50, 90), Main.rand.NextFloat(0.7f, 1.2f), 0.3f));
            }

            int x0 = (int)(ground.X / 16f), y0 = (int)(ground.Y / 16f);

            for (int i = 0; i < 22; i++)
            {
                int tx = x0 + Main.rand.Next(-4, 5), ty = y0 + Main.rand.Next(0, 2);

                if (!WorldGen.InWorld(tx, ty, 2) || !Main.tile[tx, ty].HasTile)
                    continue;

                int d = WorldGen.KillTile_MakeTileDust(tx, ty, Main.tile[tx, ty]);

                if (d >= 0 && d < Main.maxDust)
                    Main.dust[d].velocity = new Vector2(Math.Sign(tx - x0 + 0.5f) * Main.rand.NextFloat(1f, 5f), -Main.rand.NextFloat(2f, 7f));
            }

            for (int i = 0; i < 8; i++)
                Add(new BurnSmoke(EyeCentre(npc) + Main.rand.NextVector2Circular(40f, 30f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.6f, 2f)), Soot, BurnRed, Main.rand.Next(60, 100), 18f, 60f, 0.95f));

            Spark(npc, Main.rand.Next(), 1.3f);
            Spark(npc, Main.rand.Next(), 1.1f);

            Sound(SoundID.DD2_OgreGroundPound with { Volume = 1f, Pitch = -0.3f }, ground, 8);
            Sound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.65f }, ground, 8);
            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.6f }, ground, 8);
            Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.8f, Pitch = -0.2f }, ground, 8);
            Shake(ground, 20f, 36);
        }

        // ---- their coming ---------------------------------------------------------------------------------------------------

        /// <summary>Across the screen: far off, a faint rush; near, a loud one.</summary>
        private static void Whoosh(NPC npc, bool far)
        {
            if (Main.dedServ)
                return;

            Vector2 at = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

            if (far)
            {
                Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 2 }, at);
            }
            else
            {
                Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.9f, Pitch = 0.1f, MaxInstances = 2 }, at);
                Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.6f, Pitch = 0.3f, MaxInstances = 2 }, at);
                Shake(at, 5f, 14);
            }
        }

        /// <summary>Down from above: the rush of it.</summary>
        private static void DropFx(NPC npc)
        {
            if (!Main.dedServ)
                Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.8f, Pitch = -0.1f, MaxInstances = 2 }, npc.Center);
        }

        /// <summary>As it drops in: its eye lit, embers of its colour off it.</summary>
        private static void DropTrail(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            EyeLight(npc, 0.6f + 0.4f * u);

            if (Main.rand.NextFloat() < 0.4f * (1f - u))
                Fleck(npc, EyeCentre(npc) + Main.rand.NextVector2Circular(40f, 40f), -npc.velocity * 0.1f + new Vector2(0f, -1f), Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(20, 34));
        }

        /// <summary>In its place over the player, its eye flares.</summary>
        private static void EyeFlare(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 eye = Front(npc, 50f);
            Color c = ElementOf(npc);
            Add(new BloomGeneric(eye, Vector2.Zero, c * 0.9f, 1.1f, 24));
            Add(new BloomGeneric(eye, Vector2.Zero, Color.White * 0.7f, 0.4f, 12));

            for (int i = 0; i < 6; i++)
                Fleck(npc, eye, Facing(npc).RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f), Main.rand.NextFloat(0.45f, 0.75f), Main.rand.Next(20, 34));

            Sound(SoundID.Mech with { Volume = 0.9f, Pitch = -0.2f, MaxInstances = 2 }, eye);
        }

        private static void HoldGlow(NPC npc, float strength) => EyeLight(npc, 0.6f + 0.8f * strength);

        // ---- its change ------------------------------------------------------------------------------------------------------

        /// <summary>Waiting on its twin, untouchable: a faint light of its colour about it (and its sheen: DrawOver).</summary>
        private static void WaitGlow(NPC npc)
        {
            if (Main.dedServ)
                return;

            Color c = ElementOf(npc) * 0.5f;
            Lighting.AddLight(npc.Center, c.R / 255f, c.G / 255f, c.B / 255f);

            if (Main.rand.NextBool(10))
                Fleck(npc, EyeCentre(npc) + Main.rand.NextVector2Circular(50f, 50f), new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.5f)), Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(20, 34));
        }

        /// <summary>Its twin has changed too: let go, a flare and a clank.</summary>
        private static void ReleaseFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Add(new BloomGeneric(EyeCentre(npc), Vector2.Zero, ElementOf(npc) * 0.8f, 1.6f, 22));
            Sound(SoundID.NPCHit4 with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
            Sound(SoundID.Mech with { Volume = 0.8f }, npc.Center);
        }

        // ---- their end -------------------------------------------------------------------------------------------------------

        /// <summary>One of its flashes coming on: a bloom of its colour, the light thrown round.</summary>
        private static void FlashFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Color c = ElementOf(npc);
            Add(new BloomGeneric(EyeCentre(npc), Vector2.Zero, c * 0.85f, 2.2f, 18));
            Lighting.AddLight(npc.Center, c.R / 255f * 2f, c.G / 255f * 2f, c.B / 255f * 2f);

            if (Watching(npc.Center))
                Cinematic.Flash(c, 0.05f);
        }

        /// <summary>It turns: a servo's whir and click.</summary>
        private static void Whir(NPC npc, float power)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Mech with { Volume = 0.7f * power, Pitch = -0.4f }, npc.Center);
            Sound(SoundID.NPCHit4 with { Volume = 0.4f * power, Pitch = -0.8f }, npc.Center);
        }

        /// <summary>Its twin shaking on the ground: a rattle of metal.</summary>
        private static void Rattle(NPC npc, float power)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.NPCHit4 with { Volume = 0.35f * power, Pitch = Main.rand.NextFloat(-0.6f, -0.2f) }, npc.Center);

            if (Main.rand.NextBool(2))
            {
                int x = (int)(npc.Center.X / 16f) + Main.rand.Next(-3, 4), y = (int)((npc.Center.Y + 40f) / 16f);

                if (WorldGen.InWorld(x, y, 2) && Main.tile[x, y].HasTile)
                {
                    int d = WorldGen.KillTile_MakeTileDust(x, y, Main.tile[x, y]);

                    if (d >= 0 && d < Main.maxDust)
                        Main.dust[d].velocity = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1f, 3f));
                }
            }
        }

        /// <summary>It stirs: a jolt off the ground, a scrape.</summary>
        private void Stir(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 ground = new(npc.Center.X, groundY);

            for (int i = 0; i < (int)(8 * Room); i++)
                Add(new DustFog(ground + new Vector2(Main.rand.NextFloat(-40f, 40f), -6f), new Vector2(-spin * Main.rand.NextFloat(0.5f, 2.5f), -Main.rand.NextFloat(0.2f, 1f)), new Color(110, 96, 84) * 0.85f, Main.rand.Next(40, 70), Main.rand.NextFloat(0.6f, 1f), 0.28f));

            Spark(npc, Main.rand.Next(), 1f);
            Sound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.4f }, npc.Center);
            Sound(SoundID.DeerclopsRubbleAttack with { Volume = 0.5f, Pitch = 0.2f }, ground);
            Shake(ground, 6f, 14);
        }

        /// <summary>
        /// Its mark held on the player, <paramref name="u"/> of the way to the lock: the Spazmatism's gas leaking out of
        /// its mouth and hanging, thicker and thicker; the Retinazer's mouth going red-hot, spitting fire.
        /// </summary>
        private void Charging(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Front(npc, 66f);
            Vector2 way = Facing(npc);

            if (!warned)
            {
                warned = true;
                Sound(IsRet(npc) ? SoundID.DD2_KoboldIgnite with { Volume = 0.8f } : SoundID.DD2_DrakinBreathIn with { Volume = 0.9f, Pitch = -0.2f }, mouth, 6);
            }

            if (IsRet(npc))
            {
                Color hot = Color.Lerp(Fire, new Color(255, 214, 120), u);
                Add(new BloomGeneric(mouth, npc.velocity, hot * (0.4f + 0.4f * u), 0.3f + 0.8f * u, 3, lights: false));
                Lighting.AddLight(mouth, 1.2f * (0.4f + u), 0.5f * (0.4f + u), 0.15f);

                if (Main.rand.NextFloat() < 0.4f + 0.6f * u)
                {
                    Dust dust = Dust.NewDustPerfect(mouth, DustID.Torch, way.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 4f), 0, default, Main.rand.NextFloat(1.2f, 2f));
                    dust.noGravity = true;
                }

                if (Main.rand.NextFloat() < 0.25f * u)
                    Add(new Ember(mouth, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(2f, 5f), FireSparks[Main.rand.Next(FireSparks.Length)], Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(20, 34)));
            }
            else
            {
                Add(new BloomGeneric(mouth, npc.velocity, Toxic * (0.3f + 0.3f * u), 0.25f + 0.6f * u, 3, lights: false));
                Lighting.AddLight(mouth, 0.2f, 0.7f * (0.4f + u), 0.2f);

                if (Main.rand.NextFloat() < (0.3f + 1.4f * u) * Room)
                    Add(new SteamPuff(mouth, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.8f, 2.6f), Gas, Main.rand.Next(60, 110), Main.rand.NextFloat(10f, 16f), Main.rand.NextFloat(40f, 70f), Main.rand.NextFloat(0.5f, 0.75f)));

                if (Main.rand.NextFloat() < 0.3f * u)
                {
                    Dust dust = Dust.NewDustPerfect(mouth, DustID.CursedTorch, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3f), 0, default, 1.4f);
                    dust.noGravity = true;
                }
            }
        }

        /// <summary>This machine has heard its charging begin.</summary>
        private bool warned;

        /// <summary>Their mark locks: a click, its eye flaring; the Spazmatism's gas trail begun again.</summary>
        private static void Locked(NPC npc)
        {
            if (!IsRet(npc))
                GasTrail.Clear();

            if (Main.dedServ)
                return;

            Vector2 eye = Front(npc, 50f);
            Add(new BloomGeneric(eye, Vector2.Zero, ElementOf(npc) * 0.9f, 1f, 14));
            Add(new BloomGeneric(eye, Vector2.Zero, Color.White * 0.7f, 0.35f, 8));
            Sound(SoundID.Mech with { Volume = 1f, Pitch = 0.4f }, eye, 6);
            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.9f, Pitch = 0.2f }, npc.Center, 6);
        }

        /// <summary>As they fly at the mark: the Spazmatism's gas left hanging behind it (and kept, for the fire to catch), the Retinazer's fire.</summary>
        private static void ChargeTrail(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Front(npc, 66f);

            if (!IsRet(npc) && Main.GameUpdateCount % 2 == 0)
                GasTrail.Add(npc.Center);

            if (IsRet(npc))
            {
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.Torch, -npc.velocity * 0.15f + Main.rand.NextVector2Circular(1.5f, 1.5f), 0, default, Main.rand.NextFloat(1.6f, 2.4f));
                    dust.noGravity = true;
                }

                if (Main.rand.NextFloat() < 0.6f * Room)
                    Add(new FlameSmoke(npc.Center + Main.rand.NextVector2Circular(30f, 30f), -npc.velocity * 0.05f, new Color(40, 30, 26), Main.rand.Next(40, 60), 16f, 46f, 0.7f));

                Add(new BloomGeneric(mouth, npc.velocity, new Color(255, 170, 70) * 0.7f, 1f, 3, lights: false));
                Lighting.AddLight(npc.Center, 1.6f, 0.7f, 0.2f);
            }
            else
            {
                if (Main.rand.NextFloat() < 1.4f * Room)
                    Add(new SteamPuff(npc.Center + Main.rand.NextVector2Circular(34f, 34f), -npc.velocity * 0.05f + Main.rand.NextVector2Circular(0.8f, 0.8f), Gas, Main.rand.Next(80, 130), Main.rand.NextFloat(18f, 26f), Main.rand.NextFloat(60f, 96f), Main.rand.NextFloat(0.55f, 0.8f)));

                Add(new BloomGeneric(mouth, npc.velocity, Toxic * 0.6f, 0.8f, 3, lights: false));
                Lighting.AddLight(npc.Center, 0.3f, 1f, 0.3f);
            }
        }
    }
}
