using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- his hands gone --------------------------------------------------------------------------------------------
    //
    // By request ("After losing hands: opens mouth, roars, breathes skulls upwards (not homing), using only the
    // homing-skull texture"), in every world, the once his last hand dies; nothing of his hurting or hurt meanwhile, and
    // nothing hurting the player near him (they keep moving):
    //
    //     SNAP     20   he jerks back; his jaw snaps shut; in Legends his ward breaks (his skull can be hurt now)
    //     OPEN     24   the jaw drops wide, the skull lifts off it
    //     ROAR     90   he roars, and breathes skulls up into the sky, a stream of them: they go up, slow, and fall back
    //                   as a rain (in Legends they hurt; elsewhere they are only seen)
    //     CLOSE    24   his mouth shuts
    //     HOME     30   the camera, drawn a little toward him, back to the player
    //
    // The server begins it (PreAI, when it no longer finds a hand) and its beats; every machine plays it.

    public partial class LegendsSkeletron
    {
        private const int LossSnap = 0;
        private const int LossOpen = 1;
        private const int LossRoar = 2;
        private const int LossClose = 3;
        private const int LossHome = 4;

        private const int SnapTicks = 20;
        private const int OpenTicks = 24;
        private const int LossRoarTicks = 90;
        private const int CloseTicks = 24;
        private const int LossHomeTicks = 30;

        /// <summary>A skull breathed every this many ticks of the roar, for this long.</summary>
        private const int BreathEvery = 4;
        private const int BreathFor = 72;

        /// <summary>The server's: his last hand is gone.</summary>
        private void BeginUnhanded(NPC npc)
        {
            unhandedDone = true;
            scene = Scene.Unhanded;
            beat = LossSnap;
            timer = -1;
            ranBeat = -1;
            move = Move.None;
            grabbed = -1;
            npc.netUpdate = true;
        }

        private void RunUnhanded(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            headHarmless = true;
            npc.velocity *= 0.85f;
            npc.rotation = npc.rotation.AngleLerp(0f, 0.2f);

            int t = timer;
            bool here = InScene(npc.Center);
            watching = false;

            if (here)
                Main.LocalPlayer.GetModPlayer<SkeletronPlayer>().Shelter();

            switch (beat)
            {
                case LossSnap:
                    {
                        // Jerked back: up and away from where he was going, and back.
                        float jolt = 26f * (float)System.Math.Sin(MathHelper.Pi * MathHelper.Clamp(t / (float)SnapTicks, 0f, 1f));
                        npc.velocity = new Vector2(0f, -jolt * 0.15f);
                        faceRate = 1f;
                        eyesWant = 1f;
                        eyeColour = new Color(255, 60, 40);

                        Look(npc, 0.25f, 1.08f);

                        if (Crossed(0))
                            Snaps(npc);

                        if (Authority && t >= SnapTicks)
                            Next(npc, LossOpen);

                        break;
                    }

                case LossOpen:
                    jawWant = 34f * EaseOut(t / 14f);
                    liftWant = 12f * EaseOut(t / 14f);
                    hatWant = 18f * EaseOut(t / 14f);
                    eyesWant = 1f;
                    faceRate = 0.5f;

                    Look(npc, 0.35f, 1.12f);

                    if (Crossed(4))
                        Sound(SkeletronSounds.Rattle with { Pitch = -0.4f }, npc.Center);

                    if (Authority && t >= OpenTicks)
                        Next(npc, LossRoar);

                    break;

                case LossRoar:
                    jawWant = 34f + 3f * (float)System.Math.Sin(t * 0.6f);
                    liftWant = 12f;
                    hatWant = 18f;
                    eyesWant = 1f;
                    faceRate = 0.5f;
                    npc.velocity = Main.rand.NextVector2Circular(1.2f, 1.2f);

                    Look(npc, 0.35f, 1.12f);

                    if (Crossed(0))
                    {
                        Sound(SkeletronSounds.RoarBig, npc.Center);

                        if (!Main.dedServ)
                        {
                            Cinematic.Roar(npc, LossRoarTicks, 1.15f, quiet: true, rim: new Color(150, 20, 16));
                            EmpressWarp.Ripple(npc.Center, 16f, 34f, 10f, 30);
                        }
                    }

                    if (t < BreathFor && t % BreathEvery == 0)
                        BreatheSkull(npc, t / BreathEvery);

                    if (Authority && t >= LossRoarTicks)
                        Next(npc, LossClose);

                    break;

                case LossClose:
                    jawWant = 0f;
                    liftWant = 0f;
                    hatWant = 0f;
                    eyesWant = 0.4f;
                    faceRate = 0.3f;

                    Look(npc, 0.25f, 1.08f);

                    if (Crossed(10))
                        Sound(SkeletronSounds.Clack, npc.Center);

                    if (Authority && t >= CloseTicks)
                        Next(npc, LossHome);

                    break;

                default:
                    if (Crossed(0) && here && !Main.dedServ)
                        PisPlayer.PanHome(npc.Center, 40);

                    if (Authority && t >= LossHomeTicks)
                        EndUnhanded(npc);

                    break;
            }

            if (Authority && t % 30 == 0)
                npc.netUpdate = true;
        }

        /// <summary>Over: the fight, now his own; his first move of his own after a breath.</summary>
        private void EndUnhanded(NPC npc)
        {
            scene = Scene.Fight;
            beat = 0;
            timer = 0;
            ranBeat = -1;
            move = Move.None;
            nextMove = 90;
            npc.dontTakeDamage = false;
            npc.netUpdate = true;
        }

        /// <summary>The camera drawn this far toward him, the bars, a lean; the player keeps moving and keeps their interface.</summary>
        private void Look(NPC npc, float blend, float lean)
        {
            if (!InScene(npc.Center))
                return;

            watching = true;
            PisPlayer.BlendCamera(npc.Center, blend);
            Cinematic.Frame();
            Cinematic.Lean(System.Math.Max(1f, lean), 0.06f);
        }

        /// <summary>Jerked back by the loss: his jaw shut on nothing, and in Legends the ward off his skull broken into pieces.</summary>
        private void Snaps(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SkeletronSounds.Clack with { Pitch = -0.3f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Pitch = -0.5f }, npc.Center);

            if (PADWorld.Legends)
            {
                SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.3f, Volume = 0.9f }, npc.Center);
                SoundEngine.PlaySound(SkeletronSounds.SelfCrack with { Pitch = 0.2f, Volume = 0.7f }, npc.Center);

                SparkShard.Spray(npc.Center, WardColour, 26, 9f, 26f, 30, falls: true);

                for (int i = 0; i < 24; i++)
                {
                    Vector2 at = npc.Center + Main.rand.NextVector2CircularEdge(56f, 64f);
                    PADParticleHandler.SpawnParticle(new StoneChip(at, (at - npc.Center).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2f, 6f), WardColour, Main.rand.Next(40, 70), 1.2f));
                }

                PADParticleHandler.SpawnParticle(new ParryRing(npc.Center, 30f, 150f, 20, WardColour));
                Cinematic.Flash(WardColour, 0.25f * Near(npc.Center));
            }

            Shake(npc.Center, 6f, 14);
        }

        /// <summary>
        /// A skull breathed out of him and up (the server's to make): up and a little out, slowing, falling back as a rain.
        /// It hurts only in Legends.
        /// </summary>
        private void BreatheSkull(NPC npc, int index)
        {
            Vector2 mouth = SkeletronRig.Mouth(npc.Center, npc.rotation, npc.scale, jawShown, liftShown);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 4; i++)
                {
                    Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(12f, 4f), DustID.Bone, new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f)), 80, default, 1.2f);
                    dust.noGravity = true;
                }

                if (index % 3 == 0)
                    SoundEngine.PlaySound(SkeletronSounds.Clack with { Volume = 0.5f, Pitch = 0.3f }, mouth);
            }

            if (!Authority)
                return;

            // Up, fanned out a little either way, alternating, never twice the same.
            float spread = MathHelper.Lerp(-0.7f, 0.7f, (index * 0.618f) % 1f);
            Vector2 velocity = new Vector2(0f, -1f).RotatedBy(spread) * Main.rand.NextFloat(11f, 15f);
            int damage = PADWorld.Legends ? (int)(npc.GetAttackDamage_ForProjectiles(17f, 15f) * DamageScale) : 0;

            Projectile.NewProjectile(npc.GetSource_FromAI(), mouth, velocity, ModContent.ProjectileType<SkeletronSkull>(), damage, 0f, Main.myPlayer,
                SkeletronSkull.Rain, index);
        }

        /// <summary>His ward's colour: bone, lit cold.</summary>
        internal static readonly Color WardColour = new Color(220, 210, 255);
    }
}
