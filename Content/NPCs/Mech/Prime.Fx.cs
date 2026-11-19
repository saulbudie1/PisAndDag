using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- what is seen and heard -------------------------------------------------------------------------------------------
    //
    //     Spark       a spark off its skull, red or its green: three short arcs, dust, a crackle, a wisp of smoke
    //     Smoke       black smoke out of it, rising
    //     Screech     its voice (PrimeScreech, made in code: a machine's, harsh and ringing), steam out of its jaw's
    //                 corners while it lasts
    //     EyesOn      its eyes light: a spark in each, a crackle
    //     Ignite      its change: its spikes out with a clang, its green over everything for a moment
    //     JawDrop     its end: its jaw drops, its light begins (PrimeCharge: a whine that rises as it grows)
    //     Slam        its arms' hands close on its mouth (PrimeSeal: the whine shut in, straining)
    //     Pulse       what is behind them throws them back: a thud, its green squirting out between them
    //     Look        every tick: where its eyes have been (for their trails), their light
    //
    // No rings, nothing thrown evenly all round (by request): sprays go the way something went, smoke rises. Everything
    // here is only seen and heard, played on every machine from what every machine knows. Particles on the two-pixel
    // grid; arcs and streaks smooth.

    public partial class Prime
    {
        private static readonly Color Soot = new(14, 12, 12);
        private static readonly Color BurnRed = new(150, 44, 18);
        private static readonly Color Steam = new(196, 198, 204);

        private static readonly Color[] RedSparks = { new(255, 70, 50), new(255, 150, 120), new(255, 226, 210), PrimeArt.Red };
        private static readonly Color[] GreenSparks = { PrimeArt.Nuclear, new(214, 255, 110), PrimeArt.NuclearHot, new(128, 204, 12) };

        public static readonly SoundStyle ScreechSound = new("PisAndDag/Content/Sounds/Mech/PrimeScreech") { Volume = 1f, MaxInstances = 2 };
        public static readonly SoundStyle BeepSound = new("PisAndDag/Content/Sounds/Mech/PrimeBeep") { Volume = 0.8f, MaxInstances = 6 };
        public static readonly SoundStyle ChargeSound = new("PisAndDag/Content/Sounds/Mech/PrimeCharge") { Volume = 1f, MaxInstances = 1 };
        public static readonly SoundStyle SealSound = new("PisAndDag/Content/Sounds/Mech/PrimeSeal") { Volume = 1f, MaxInstances = 1 };

        /// <summary>Its end's two long sounds, so they can be cut off (its hands closing, the scene skipped).</summary>
        private SlotId chargeSlot = SlotId.Invalid, sealSlot = SlotId.Invalid;

        /// <summary>A flash of its colour over its own picture, dying away (this machine's), and whether it is its green.</summary>
        private float flash;
        private bool flashGreen;

        /// <summary>Where its eyes have been, newest first (this machine's), for their trails.</summary>
        private const int TrailLong = 36;
        private const int TrailShort = 10;
        private readonly Vector2[] trailLeft = new Vector2[TrailLong], trailRight = new Vector2[TrailLong];
        private int trailKept;

        /// <summary>Sounds played this tick, all of them: a scene can ask for many at once.</summary>
        private static int soundsThisTick;
        private static uint soundTick;

        internal static void Add(PADParticle particle)
        {
            particle.Pixelate = true;
            PADParticleHandler.SpawnParticle(particle);
        }

        internal static float Room => MathHelper.Clamp(PADParticleHandler.FreeSlots / 400f, 0.2f, 1f);

        internal static bool Seen(Vector2 at, float margin) =>
            !Main.dedServ &&
            at.X > Main.screenPosition.X - margin && at.X < Main.screenPosition.X + Main.screenWidth + margin &&
            at.Y > Main.screenPosition.Y - margin && at.Y < Main.screenPosition.Y + Main.screenHeight + margin;

        internal static void Sound(SoundStyle style, Vector2 at, int most = 4)
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
        internal static void Shake(Vector2 at, float power, int time)
        {
            if (Main.dedServ)
                return;

            Vector2 camera = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float near = 1f - MathHelper.Clamp((Vector2.Distance(camera, at) - 400f) / 2200f, 0f, 1f);

            if (near > 0.01f)
                Main.LocalPlayer.PIS().BumpScreenShake(power * near, time);
        }

        internal static void Arc(Vector2 from, Vector2 to, Color colour, float width, int lifetime = 14, int forks = 1)
        {
            if (!Main.dedServ)
                Lightning.Spawn(from, to, colour, width, lifetime, forks);
        }

        /// <summary>A fleck of light thrown off it, keeping its colour as it goes.</summary>
        private static void Fleck(Vector2 at, Vector2 velocity, bool green, float scale, int life, float lift = 0.04f)
        {
            Color[] sparks = green ? GreenSparks : RedSparks;
            Add(new HoneyMote(at, velocity, sparks[Main.rand.Next(sparks.Length)], scale, life, lift));
        }

        /// <summary>A place on its skull, picked by a number.</summary>
        private static Vector2 OnSkull(NPC npc, Random rng) =>
            OnFace(npc, ((float)rng.NextDouble() * 2f - 1f) * 36f, ((float)rng.NextDouble() * 2f - 1f) * 44f);

        private void StopSounds()
        {
            if (Main.dedServ || !chargeSlot.IsValid && !sealSlot.IsValid)
                return;

            if (chargeSlot.IsValid && SoundEngine.TryGetActiveSound(chargeSlot, out ActiveSound charge))
                charge?.Stop();

            if (sealSlot.IsValid && SoundEngine.TryGetActiveSound(sealSlot, out ActiveSound seal))
                seal?.Stop();

            chargeSlot = SlotId.Invalid;
            sealSlot = SlotId.Invalid;
        }

        // ---- every tick -----------------------------------------------------------------------------------------------------

        /// <summary>Every tick, whatever moves it: where its eyes have been, their light, its flash dying away; its green drifting off it once it has changed.</summary>
        private void Look(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 left = Eye(npc, -1), right = Eye(npc, 1);

            // Put somewhere all at once: its trails begin again.
            if (trailKept > 0 && Vector2.DistanceSquared(trailLeft[0], left) > 260f * 260f)
                trailKept = 0;

            for (int i = TrailLong - 1; i > 0; i--)
            {
                trailLeft[i] = trailLeft[i - 1];
                trailRight[i] = trailRight[i - 1];
            }

            trailLeft[0] = left;
            trailRight[0] = right;
            trailKept = Math.Min(TrailLong, trailKept + 1);

            flash *= 0.9f;

            if (flash < 0.01f)
                flash = 0f;

            EyeLook(out bool lit, out bool green, out float glow);

            if (lit)
            {
                Color c = green ? PrimeArt.Nuclear : PrimeArt.Red;
                float k = 0.4f * Math.Min(2.5f, glow);
                Lighting.AddLight(Middle(npc), c.R / 255f * k, c.G / 255f * k, c.B / 255f * k);
            }

            if (changed && scene == Scene.None && Main.rand.NextBool(8) && Seen(npc.Center, 200f))
                Fleck(OnFace(npc, Main.rand.NextFloat(-44f, 44f), Main.rand.NextFloat(-50f, 40f)), new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.4f, 1.3f)),
                    true, Main.rand.NextFloat(0.35f, 0.55f), Main.rand.Next(22, 38));
        }

        // ---- breaking down --------------------------------------------------------------------------------------------------

        /// <summary>A spark off its skull, red or its green: three short arcs, dust, a crackle, a wisp of smoke.</summary>
        private static void Spark(NPC npc, int pick, float power, bool green)
        {
            if (!Seen(npc.Center, 220f))
                return;

            Random rng = new(pick);
            Vector2 at = OnSkull(npc, rng);
            Color[] sparks = green ? GreenSparks : RedSparks;

            for (int i = 0; i < 3; i++)
                Arc(at, at + Main.rand.NextVector2Circular(26f, 22f) * power, sparks[Main.rand.Next(sparks.Length)], Main.rand.NextFloat(8f, 13f) * power, Main.rand.Next(12, 18), Main.rand.Next(2));

            Add(new BloomGeneric(at, Vector2.Zero, sparks[0] * 0.7f, 0.3f + 0.25f * power, 10));

            for (int i = 0; i < (int)(6 + 5 * power); i++)
            {
                Dust dust = Dust.NewDustPerfect(at, green ? DustID.CursedTorch : DustID.RedTorch, Main.rand.NextVector2Circular(2.6f, 2.6f) * power + new Vector2(0f, -0.6f), 0, default, 1.2f);
                dust.noGravity = Main.rand.NextBool(3);
            }

            for (int i = 0; i < (int)(3 * power) + 1; i++)
                Fleck(at, new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), -Main.rand.NextFloat(1f, 4f)) * power, green, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(20, 36), -0.08f);

            Add(new MuzzleSmoke(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.2f)), Main.rand.Next(26, 40), Main.rand.NextFloat(0.22f, 0.36f)));

            Sound(SoundID.Item93 with { Volume = 0.5f * power, Pitch = Main.rand.NextFloat(-0.3f, 0.25f) }, at);
            Shake(at, 1.8f * power, 6);
        }

        /// <summary>Black smoke out of it, about <paramref name="rate"/> a tick, rising off it.</summary>
        private static void Smoke(NPC npc, float rate)
        {
            if (!Seen(npc.Center, 160f))
                return;

            rate *= Room;
            int whole = (int)rate;

            if (Main.rand.NextFloat() < rate - whole)
                whole++;

            for (int i = 0; i < whole; i++)
            {
                Vector2 at = OnFace(npc, Main.rand.NextFloat(-36f, 36f), Main.rand.NextFloat(-44f, 30f));
                Add(new BurnSmoke(at, new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -Main.rand.NextFloat(0.6f, 1.6f)), Soot, BurnRed, Main.rand.Next(40, 70),
                    Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(34f, 56f), Main.rand.NextFloat(0.82f, 1f)));
            }
        }

        /// <summary>The blow that should have killed it, or the moment its last arm is gone: a jolt, sparks, a puff of smoke.</summary>
        private void Failing(NPC npc)
        {
            if (Main.dedServ)
                return;

            Spark(npc, Main.rand.Next(), 1.2f, changed);
            Spark(npc, Main.rand.Next(), 1f, changed);

            for (int i = 0; i < 6; i++)
                Add(new BurnSmoke(OnFace(npc, Main.rand.NextFloat(-36f, 36f), Main.rand.NextFloat(-40f, 30f)), new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.8f, 2f)),
                    Soot, BurnRed, Main.rand.Next(50, 80), 16f, 50f, 0.95f));

            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.45f }, npc.Center);
            Sound(SoundID.Item14 with { Volume = 0.55f, Pitch = -0.3f }, npc.Center);
            Shake(npc.Center, 6f, 18);
        }

        // ---- its coming -----------------------------------------------------------------------------------------------------

        /// <summary>Down from above: the rush of it.</summary>
        private static void DropFx(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = Main.screenPosition + new Vector2(Main.screenWidth * 0.5f, 0f);
            Sound(SoundID.DD2_WyvernDiveDown with { Volume = 0.7f, Pitch = -0.5f, MaxInstances = 2 }, at);
            Sound(SoundID.DD2_BetsyWindAttack with { Volume = 0.4f, Pitch = -0.6f, MaxInstances = 2 }, at);
        }

        /// <summary>Its screech (its voice only; what shows while it lasts is <see cref="ScreechTick"/>).</summary>
        private static void Screech(NPC npc, float pitch)
        {
            if (Main.dedServ)
                return;

            Sound(ScreechSound with { Pitch = pitch }, npc.Center, 6);
            Shake(npc.Center, 10f, 30);
        }

        /// <summary>While it screeches: steam out of the corners of its jaw, its teeth sparking, the screen kept shaking.</summary>
        private void ScreechTick(NPC npc, float voice)
        {
            if (Main.dedServ || voice <= 0.02f || !Seen(npc.Center, 260f))
                return;

            Vector2 mouth = Mouth(npc);
            Vector2 down = new Vector2(0f, 1f).RotatedBy(npc.rotation);
            Vector2 across = new Vector2(1f, 0f).RotatedBy(npc.rotation);

            for (int side = -1; side <= 1; side += 2)
            {
                if (Main.rand.NextFloat() < 0.8f * voice * Room)
                    Add(new SteamPuff(mouth + across * side * 24f * npc.scale, across * side * Main.rand.NextFloat(1.5f, 4f) + down * Main.rand.NextFloat(0.4f, 1.6f), Steam,
                        Main.rand.Next(26, 44), Main.rand.NextFloat(7f, 11f), Main.rand.NextFloat(28f, 46f), Main.rand.NextFloat(0.4f, 0.6f)));
            }

            if (Main.rand.NextFloat() < 0.5f * voice)
            {
                Dust dust = Dust.NewDustPerfect(mouth + across * Main.rand.NextFloat(-18f, 18f), DustID.Torch, down.RotatedByRandom(0.7f) * Main.rand.NextFloat(1f, 4f), 0, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = Main.rand.NextBool();
            }

            if (timer % 5 == 0)
                Shake(npc.Center, 4f * voice, 6);
        }

        /// <summary>A pair of its arms thrown out from behind it: a ballista's thump and a rush.</summary>
        private static void Deploy(NPC npc)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.9f, Pitch = -0.35f, MaxInstances = 3 }, npc.Center, 6);
            Sound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.6f, MaxInstances = 3 }, npc.Center, 6);
        }

        /// <summary>The pair out as far as it goes, and locked: a clank, a jolt.</summary>
        private static void Locked(NPC npc, bool upper)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = upper ? -0.3f : -0.5f, MaxInstances = 3 }, npc.Center, 6);
            Sound(SoundID.Mech with { Volume = 0.8f, Pitch = -0.2f, MaxInstances = 3 }, npc.Center, 6);
            Shake(npc.Center, 5f, 10);
        }

        /// <summary>Its eyes light: a spark in each and off each, a crackle, a glint of their colour over everything.</summary>
        private void EyesOn(NPC npc, bool green)
        {
            if (Main.dedServ)
                return;

            Color c = green ? PrimeArt.Nuclear : PrimeArt.Red;
            Color hot = green ? PrimeArt.NuclearHot : PrimeArt.RedHot;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = Eye(npc, side);
                Add(new BloomGeneric(eye, Vector2.Zero, c * 0.9f, 0.9f, 20));
                Add(new BloomGeneric(eye, Vector2.Zero, hot * 0.8f, 0.3f, 10));
                Arc(eye, eye + new Vector2(side * Main.rand.NextFloat(14f, 34f), -Main.rand.NextFloat(6f, 30f)), c, 9f, 14, 1);

                for (int i = 0; i < 5; i++)
                    Fleck(eye, new Vector2(side * Main.rand.NextFloat(0.5f, 3f), -Main.rand.NextFloat(0.5f, 3f)), green, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(18, 30));
            }

            flash = 0.55f;
            flashGreen = green;
            Sound(SoundID.Item93 with { Volume = 0.9f, Pitch = -0.1f }, npc.Center, 6);
            Sound(SoundID.Mech with { Volume = 0.9f, Pitch = 0.3f }, npc.Center, 6);
            Shake(npc.Center, 4f, 10);

            if (Watching(npc.Center))
                Cinematic.Flash(c, 0.07f);
        }

        /// <summary>Its eyes go out: a click, a wisp of smoke out of each socket.</summary>
        private static void EyesOut(NPC npc)
        {
            if (Main.dedServ)
                return;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 3; i++)
                    Add(new MuzzleSmoke(Eye(npc, side), new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.2f)), Main.rand.Next(30, 46), Main.rand.NextFloat(0.2f, 0.34f)));
            }

            Sound(SoundID.Mech with { Volume = 0.9f, Pitch = -0.7f }, npc.Center, 6);
            Sound(SoundID.Item93 with { Volume = 0.6f, Pitch = -0.6f }, npc.Center, 6);
        }

        /// <summary>A servo's whir and click: its arms swinging up.</summary>
        private static void Whir(NPC npc, float power)
        {
            if (Main.dedServ)
                return;

            Sound(SoundID.Mech with { Volume = 0.7f * power, Pitch = -0.4f }, npc.Center);
            Sound(SoundID.Item24 with { Volume = 0.4f * power, Pitch = -0.3f }, npc.Center);
        }

        // ---- its change -----------------------------------------------------------------------------------------------------

        /// <summary>Its spikes snap out and its eyes light its green: a clang, its green over its skull and over everything for a moment, flecks of it thrown up.</summary>
        private void Ignite(NPC npc)
        {
            if (Main.dedServ)
                return;

            EyesOn(npc, green: true);
            flash = 1f;
            flashGreen = true;

            Vector2 middle = Middle(npc);
            Add(new BloomGeneric(middle, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 2.6f, 26));

            // Off its spikes' ends, the way each points.
            foreach ((float x, float y) in new[] { (-66f, -8f), (66f, -8f), (-40f, -52f), (40f, -52f), (0f, -76f) })
            {
                Vector2 end = OnFace(npc, x, y);
                Vector2 way = (end - middle).SafeNormalize(-Vector2.UnitY);
                PADParticleHandler.SpawnParticle(new SparkShard(end, way * Main.rand.NextFloat(5f, 9f), PrimeArt.Nuclear, 18f, 3f, 60f, Main.rand.Next(14, 22)));

                for (int i = 0; i < 3; i++)
                    Fleck(end, way.RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 4f), true, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(20, 34));
            }

            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = 0.25f }, middle, 8);
            Sound(SoundID.DD2_BallistaTowerShot with { Volume = 0.9f, Pitch = 0.3f }, middle, 8);
            Sound(SoundID.Item14 with { Volume = 0.6f, Pitch = -0.5f }, middle, 8);
            Shake(middle, 16f, 28);

            if (Watching(middle))
                Cinematic.Flash(PrimeArt.Nuclear, 0.3f);
        }

        // ---- its end --------------------------------------------------------------------------------------------------------

        /// <summary>Its jaw drops: a clank; its light begins, and the whine with it (its eyes lit its green here, if they were not).</summary>
        private void JawDrop(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Mouth(npc);

            if (!changed)
                EyesOn(npc, green: true);

            Add(new BloomGeneric(mouth, Vector2.Zero, PrimeArt.Nuclear * 0.8f, 0.8f, 16));
            Sound(SoundID.NPCHit4 with { Volume = 0.9f, Pitch = -0.6f }, mouth, 8);
            Sound(SoundID.Mech with { Volume = 0.9f, Pitch = -0.5f }, mouth, 8);
            Shake(mouth, 5f, 12);

            StopSounds();
            chargeSlot = SoundEngine.PlaySound(ChargeSound, mouth);
        }

        /// <summary>While its mouth's light grows: its light on everything about it, its green drawn in to it, a crackle off its teeth now and then.</summary>
        private void AuraTick(NPC npc, float aura)
        {
            if (Main.dedServ || aura <= 0.001f)
                return;

            Vector2 mouth = Mouth(npc);
            float k = 0.5f + 2.6f * aura;
            Lighting.AddLight(mouth, 0.73f * k, 1f * k, 0.02f * k);

            if (!Seen(mouth, 300f))
                return;

            if (Main.rand.NextFloat() < (0.25f + 0.75f * aura) * Room)
            {
                // Drawn in: from about it, at its mouth.
                Vector2 off = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(50f, 110f + 80f * aura);
                Dust dust = Dust.NewDustPerfect(mouth + off, DustID.CursedTorch, -off * 0.07f, 0, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }

            if (Main.rand.NextFloat() < 0.03f + 0.09f * aura)
            {
                Vector2 on = mouth + new Vector2(Main.rand.NextFloat(-18f, 18f), Main.rand.NextFloat(-8f, 8f));
                Arc(on, on + Main.rand.NextVector2Circular(30f, 26f) * (0.6f + aura), GreenSparks[Main.rand.Next(GreenSparks.Length)], 8f + 4f * aura, Main.rand.Next(10, 16), Main.rand.Next(2));
                Sound(SoundID.Item93 with { Volume = 0.25f + 0.3f * aura, Pitch = Main.rand.NextFloat(0f, 0.5f) }, on, 3);
            }
        }

        /// <summary>
        /// Its arms' hands close on its mouth (or, with none, the moment its light is let go in the open): a great clank,
        /// the whine cut off and shut in (PrimeSeal), its green squeezed out sideways.
        /// </summary>
        private void Slam(NPC npc, int arms)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Mouth(npc);
            StopSounds();

            // With no hands on it the dark is shorter by their closing: its sound quicker to fit.
            sealSlot = SoundEngine.PlaySound(arms > 0 ? SealSound : SealSound with { Pitch = 0.36f }, mouth);

            if (arms == 0)
                return;

            Add(new BloomGeneric(mouth, Vector2.Zero, PrimeArt.Nuclear * 0.9f, 1.6f, 14));
            Squirt(npc, 1.2f, 10);

            Sound(SoundID.NPCHit4 with { Volume = 1f, Pitch = -0.7f }, mouth, 8);
            Sound(SoundID.DD2_OgreGroundPound with { Volume = 0.8f, Pitch = 0.2f }, mouth, 8);
            Sound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.8f }, mouth, 8);
            Shake(mouth, 16f, 24);

            if (Watching(mouth))
                Cinematic.Flash(PrimeArt.Nuclear, 0.16f);
        }

        /// <summary>The ways its light gets out between its arms' hands: down between its lower pair, and out to either side between each lower and upper one.</summary>
        private static readonly float[] Gaps = { 90f, 20f, 160f };

        /// <summary>Its green squirting out between its hands: thin streaks along the gaps, flecks after them.</summary>
        private void Squirt(NPC npc, float power, int many)
        {
            if (!Seen(npc.Center, 300f))
                return;

            Vector2 mouth = Mouth(npc);

            for (int i = 0; i < many; i++)
            {
                float angle = MathHelper.ToRadians(Gaps[Main.rand.Next(Gaps.Length)] + Main.rand.NextFloat(-16f, 16f)) + npc.rotation;
                Vector2 way = angle.ToRotationVector2();
                PADParticleHandler.SpawnParticle(new SparkShard(mouth + way * 14f, way * Main.rand.NextFloat(6f, 13f) * power, Main.rand.NextBool(3) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    16f, 3f, 70f, Main.rand.Next(12, 20)));

                if (Main.rand.NextBool(2))
                    Fleck(mouth + way * 20f, way * Main.rand.NextFloat(1.5f, 5f) * power, true, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(18, 32));
            }
        }

        /// <summary>What is behind its hands throws them back: a thud, the screen shaken, its green squirting out between them.</summary>
        private void Pulse(NPC npc, float power, int arms)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = Mouth(npc);
            Squirt(npc, 0.7f + 0.6f * power, 3 + (int)(5 * power));

            Sound(SoundID.Item14 with { Volume = 0.3f + 0.3f * power, Pitch = -0.9f, MaxInstances = 4 }, mouth, 6);

            if (arms > 0)
                Sound(SoundID.NPCHit4 with { Volume = 0.3f + 0.3f * power, Pitch = -0.8f, MaxInstances = 4 }, mouth, 6);

            Shake(mouth, 3f + 8f * power, 10);

            if (Watching(mouth))
                Cinematic.Flash(PrimeArt.Nuclear, 0.02f + 0.05f * power);
        }

        /// <summary>While its mouth is shut in: its light on everything about it, stronger and stronger.</summary>
        private void SealTick(NPC npc, float u)
        {
            if (Main.dedServ)
                return;

            float k = 1.2f + 3f * u;
            Lighting.AddLight(Mouth(npc), 0.73f * k, 1f * k, 0.02f * k);
        }
    }
}
