using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the blinks (Legends act, her second half) ---------------------------------------------------------------
    //
    // By request: "teleports randomly 5 times with interval of 0.5 secs with sounds and vanilla screech and each teleport
    // bursts vanilla homing light projectiles".
    //
    //     GATHER  14   light gathers on her; a backwards pop
    //     five times, every half a second: she is gone where she was (a flash, an image of her left there going out), and
    //     somewhere else in the arena at once, never too near the player nor too far, her screech (the game's own), and out
    //     of her a ring of the game's own homing bolts of light (its rainbow ones, six of them)
    //     REST    30   a breath, before vanilla's next move
    //
    // The bolts are vanilla's own (they home as vanilla's do, and hurt as hers do). Where she goes is the server's, each
    // place chosen a blink ahead and sent in its own slot (the first as the act is planned), so every machine moves her
    // to the same place at its own moment, whichever of the server's words reaches it first.

    public partial class EmpressOfLight
    {
        private const int BlinkGather = 0;
        private const int BlinkGo = 1;
        private const int BlinkRest = 2;

        private const int BlinkGatherTicks = 14;
        private const int BlinkEvery = 30;
        private const int BlinkTimes = 5;
        private const int BlinkRestTicks = 30;

        /// <summary>How near the player she comes, and how far she may be; her bolts, how many and how fast.</summary>
        private const float BlinkNearest = 380f;
        private const float BlinkFarthest = 780f;
        private const int BlinkBolts = 6;
        private const float BlinkBoltSpeed = 9f;

        private const int BlinkBoltNormal = 50;
        private const int BlinkBoltExpert = 35;

        /// <summary>Where each blink puts her: the server's, the next chosen at each blink (and the first as planned), sent.</summary>
        private readonly Vector2[] blinkPlaces = new Vector2[BlinkTimes];

        /// <summary>Each machine's own: where she last was, and when she left it (for the image of her left behind).</summary>
        private Vector2 blinkFrom;
        private uint blinkAt;
        private readonly Vector2[] blinkTrail = new Vector2[BlinkTimes + 1];
        private int blinkTrailCount;

        private void PlanBlink(NPC npc, Player target)
        {
            blinkPlaces[0] = BlinkPlace(npc, target);
            actAt = blinkPlaces[0];
            blinkTrailCount = 0;
        }

        /// <summary>A place for her next blink: in the arena, from the player as far as given, not where she is. The server's.</summary>
        private Vector2 BlinkPlace(NPC npc, Player target)
        {
            Vector2 best = npc.Center;

            for (int tries = 0; tries < 20; tries++)
            {
                Vector2 at = target.Center + Main.rand.NextVector2Unit() * Main.rand.NextFloat(BlinkNearest, BlinkFarthest);

                if (!Decagon.Inside(at, arenaCentre, EmpressArena.Radius, 180f) || Vector2.Distance(at, npc.Center) < 320f)
                    continue;

                return at;
            }

            Vector2 none = Vector2.Zero;
            return Decagon.PushInside(best, arenaCentre, EmpressArena.Radius, 180f, ref none);
        }

        private void RunBlink(NPC npc, Player who, bool entered)
        {
            int t = timer;
            npc.velocity *= 0.8f;

            switch (beat)
            {
                case BlinkGather:
                    FreeArmFrame(3);

                    if (entered)
                        blinkTrailCount = 0;

                    if (entered && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Blink, npc.Center, -0.3f, 0.6f, 2);
                        Sound(EmpressSounds.Swell, npc.Center, 1.4f, 0.5f, 2);
                    }

                    if (Authority && t >= BlinkGatherTicks)
                        Next(npc, BlinkGo);

                    break;

                case BlinkGo:
                    FreeArmFrame(t % BlinkEvery < 10 ? 6 : 3);

                    for (int k = 0; k < BlinkTimes; k++)
                    {
                        if (Crossed(k * BlinkEvery))
                            Blinks(npc, who, k);
                    }

                    if (Authority && t >= BlinkEvery * BlinkTimes)
                        Next(npc, BlinkRest);

                    break;

                case BlinkRest:
                    if (Authority && t >= BlinkRestTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>One blink: gone from here, there at once; her screech; her bolts out of her (the server's); the next place chosen.</summary>
        private void Blinks(NPC npc, Player who, int k)
        {
            Vector2 from = npc.Center;
            Vector2 to = blinkPlaces[Math.Clamp(k, 0, BlinkTimes - 1)];

            blinkFrom = from;
            blinkAt = Main.GameUpdateCount;

            if (blinkTrailCount == 0)
                blinkTrail[blinkTrailCount++] = from;

            if (blinkTrailCount < blinkTrail.Length)
                blinkTrail[blinkTrailCount++] = to;

            // Every machine puts her there now; the server's word follows.
            npc.Center = to;
            npc.velocity = Vector2.Zero;
            npc.spriteDirection = npc.direction = who.Center.X >= to.X ? 1 : -1;

            if (Authority)
            {
                for (int i = 0; i < BlinkBolts; i++)
                {
                    float a = i / (float)BlinkBolts * MathHelper.TwoPi + k * 0.4f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), to, a.ToRotationVector2() * BlinkBoltSpeed, ProjectileID.HallowBossRainbowStreak,
                        Hurt(npc, BlinkBoltNormal, BlinkBoltExpert), 0f, Main.myPlayer, who.whoAmI, i / (float)BlinkBolts);
                }

                // The next: a blink ahead, in its own slot, sent.
                if (k + 1 < BlinkTimes)
                    blinkPlaces[k + 1] = BlinkPlace(npc, who);

                npc.netUpdate = true;
            }

            if (Main.dedServ)
                return;

            Sound(EmpressSounds.Blink, from, 0.05f * k, 0.9f, 3);
            Scream(npc, 0.5f + 0.05f * k, 0.8f, rings: k == BlinkTimes - 1);
            Sound(SoundID.Item164, to, 0.2f, 0.5f);

            EmpressWarp.Ripple(to, 14f, 40f, 9f, 20);
            PADParticleHandler.SpawnParticle(new BloomGeneric(from, Vector2.Zero, EmpressArt.Pink, 260f / 160f, 16));
            PADParticleHandler.SpawnParticle(new BloomGeneric(to, Vector2.Zero, Color.White, 300f / 160f, 14));
            PADParticleHandler.SpawnParticle(new CrownGlint(to, 140f, 20, Color.White));
            SparkShard.Spray(to, Color.White, 10, 8f, 60f, 16, palette: EmpressArt.Sparks);
        }

        private void DrawBlinkHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            uint since = Main.GameUpdateCount - blinkAt;
            float bright = beat == BlinkGather ? 0.4f * Ease(timer / (float)BlinkGatherTicks) : since < 12 ? 0.8f * (1f - since / 12f) : 0f;

            // Where she was a moment ago: an image of her, in her colours, going out.
            if (since < 16 && blinkAt != 0)
            {
                float fade = 1f - since / 16f;
                Restart(spriteBatch, true);
                EmpressArt.DrawShape(spriteBatch, npc, blinkFrom, screenPos, 1f + 0.15f * (1f - fade), EmpressArt.Additive(EmpressArt.Prism(blinkAt / 60f), 0.7f * fade), 3, 3);
                Restart(spriteBatch, false);
            }

            DrawSecondHer(npc, spriteBatch, screenPos, bright, 0.4f + bright * 0.5f);
        }

        /// <summary>The way she went: a line of light from each place to the next, going out.</summary>
        private void DrawBlinkOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;

            for (int k = 1; k < blinkTrailCount; k++)
            {
                // The newest brightest; all of them going out over the rest.
                float age = blinkTrailCount - 1 - k;
                float fade = Math.Max(0f, 1f - age * 0.28f) * (beat == BlinkRest ? 1f - timer / (float)BlinkRestTicks : 1f);

                if (fade <= 0.003f)
                    continue;

                Color colour = EmpressArt.Prism(k / 6f + time / 200f);
                EmpressArt.DrawLine(spriteBatch, blinkTrail[k - 1], blinkTrail[k], 14f, EmpressArt.Additive(colour, 0.22f * fade));
                EmpressArt.DrawLine(spriteBatch, blinkTrail[k - 1], blinkTrail[k], 2f, EmpressArt.Additive(Color.White, 0.6f * fade));
                EmpressArt.DrawStar(spriteBatch, blinkTrail[k], 50f * fade, time * 0.05f + k, EmpressArt.Additive(Color.White, 0.7f * fade));
            }
        }
    }
}
