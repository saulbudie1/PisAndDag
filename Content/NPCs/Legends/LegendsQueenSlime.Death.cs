using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- her death --------------------------------------------------------------------------------
    //
    // The blow that would kill her starts this instead (CheckDead), in every world:
    //
    //     SPASM     72   she cries out and convulses, hanging where she was, wings beating at nothing
    //     FALL     ...   her wings break into splinters and she drops, and lands hard
    //     FRENZY   480   eight seconds of it: she hurls herself about, slam after slam, near her
    //                    and onto the player; she hurts again while she does
    //     STILL    ...   she comes down one last time and stays there
    //     SHUDDER  180   three seconds of it, harder and harder, whiter and whiter; the camera
    //                    goes over to her, the bars come in and it leans in on her
    //     BURST     30   she bursts into gel and light and dust, every way; every slime of hers
    //                    pops with her, and gives nothing; then the loot, and the camera home
    //
    // Untouchable throughout. The server throws her about and ends her; every machine plays it.

    public partial class LegendsQueenSlime
    {
        private const int DeathSpasm = 0;
        private const int DeathFall = 1;
        private const int DeathFrenzy = 2;
        private const int DeathStill = 3;
        private const int DeathShudder = 4;
        private const int DeathBurst = 5;

        private const int SpasmTicks = 72;
        private const int DeathFallMost = 150;
        private const int FrenzyTicks = 480;
        private const int StillMost = 90;
        private const int ShudderTicks = 180;
        private const int BurstEnd = 30;

        /// <summary>How often a slam of the frenzy is at the player, not just near her; how near is near; the furthest one goes.</summary>
        private const float FrenzyOnto = 0.6f;
        private const float FrenzyNear = 190f;
        private const float FrenzyReach = 520f;

        /// <summary>How high she throws herself, the least and the most, over where she starts or lands, whichever is higher.</summary>
        private const float FrenzyLow = 70f;
        private const float FrenzyHigh = 280f;

        /// <summary>Her slam's shockwave, vanilla's own damage for it (AI_121: 40).</summary>
        private const int SmashDamage = 40;

        /// <summary>NPC gravity and the fastest an NPC falls (NPC.UpdateNPC_UpdateGravity, on land).</summary>
        private const float NpcGravity = 0.3f;
        private const float NpcMaxFall = 10f;

        /// <summary>The server's: ticks on the ground before the next throw, and whether she was in the air last tick.</summary>
        private int frenzyWait;
        private bool frenzyAir;

        /// <summary>Ticks the camera takes to go over to her at the start of her last three seconds.</summary>
        private const int DeathPanTicks = 60;

        /// <summary>How far the shot leans in on her, at the most (a multiple of the player's own zoom).</summary>
        private const float DeathLean = 0.12f;

        /// <summary>This machine's player was in her death's shot on the last tick: the camera is walked home for them when she goes.</summary>
        private bool deathWatching;

        /// <summary>The death has started, on whichever machine the killing blow was dealt; the server's is the one that holds.</summary>
        private void BeginDeath(NPC npc)
        {
            dying = true;

            // Whatever she was doing is over: her core back in her, whoever she held let go.
            coreOut = false;
            grabbed = -1;
            dashStretch = 0f;
            frenzyAir = false;

            npc.velocity *= 0.3f;
            npc.damage = 0;
            npc.chaseable = false;

            Go(npc, Scene.Death, DeathSpasm);
        }

        private void RunDeath(NPC npc, bool entered)
        {
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.damage = beat == DeathFrenzy ? baseDamage : 0;
            npc.rotation = 0f;

            int t = timer;
            bool winged = transitioned && !wingsBroken;

            switch (beat)
            {
                case DeathSpasm:
                    // Hung where she was if her wings still hold her; on the ground if not.
                    npc.noGravity = winged;
                    npc.noTileCollide = false;

                    if (winged)
                        npc.velocity *= 0.88f;
                    else
                        Settle(npc);

                    if (entered)
                        DeathCry(npc);

                    Spasm(npc, 1f);

                    if (Authority && t >= SpasmTicks)
                    {
                        wingsBroken = transitioned;
                        Next(npc, DeathFall);
                    }

                    break;

                case DeathFall:
                    npc.noGravity = false;
                    npc.noTileCollide = false;
                    npc.velocity.X *= 0.97f;

                    if (entered)
                    {
                        // Let go in the air: not yet a speed of nothing, which is what standing
                        // on something reads as.
                        if (npc.velocity.Y == 0f && transitioned)
                            npc.velocity.Y = 0.01f;

                        if (wingsBroken)
                            BreakWings(npc);
                    }

                    Spasm(npc, 0.5f);

                    if (Authority && ((t > 2 && npc.velocity.Y == 0f) || t >= DeathFallMost))
                    {
                        frenzyWait = 18;
                        frenzyAir = false;
                        Next(npc, DeathFrenzy);
                    }

                    break;

                case DeathFrenzy:
                    npc.noGravity = false;
                    npc.noTileCollide = false;

                    Spasm(npc, 0.6f);

                    if (npc.velocity.Y == 0f)
                    {
                        Settle(npc);

                        if (Authority)
                        {
                            // Down: her slam's shockwave where she came down.
                            if (frenzyAir)
                            {
                                frenzyAir = false;
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom, Vector2.Zero, ProjectileID.QueenSlimeSmash, SmashDamage, 0f, Main.myPlayer);
                            }

                            if (frenzyWait > 0)
                            {
                                frenzyWait--;
                            }
                            else if (t < FrenzyTicks)
                            {
                                FrenzyThrow(npc);
                                frenzyWait = Main.rand.NextBool(4) ? Main.rand.Next(2, 6) : Main.rand.Next(8, 24);
                            }
                        }
                    }
                    else
                    {
                        frenzyAir = true;

                        // No hold on herself at all, up there.
                        if (!Main.dedServ && Main.rand.NextBool(5))
                            Jerk(Main.rand.NextFloat(-0.09f, 0.09f));
                    }

                    if (Authority && t >= FrenzyTicks && (npc.velocity.Y == 0f || t >= FrenzyTicks + 180))
                        Next(npc, DeathStill);

                    break;

                case DeathStill:
                    npc.noGravity = false;
                    npc.noTileCollide = false;
                    Settle(npc);
                    Spasm(npc, 0.3f);

                    if (Authority && ((t >= 24 && npc.velocity.Y == 0f) || t >= StillMost))
                        Next(npc, DeathShudder);

                    break;

                case DeathShudder:
                    {
                        npc.noGravity = false;
                        npc.noTileCollide = false;
                        Settle(npc);

                        float p = MathHelper.Clamp(t / (float)ShudderTicks, 0f, 1f);

                        Spasm(npc, 0.8f + 1.6f * p);
                        Shudder(npc, t, p);
                        DeathShot(npc, Ease(t / (float)DeathPanTicks), 1f + DeathLean * Ease(p * 2f));

                        if (Authority && t >= ShudderTicks)
                            Next(npc, DeathBurst);

                        break;
                    }

                case DeathBurst:
                    npc.velocity = Vector2.Zero;
                    npc.noGravity = true;

                    if (entered)
                    {
                        Burst(npc);

                        if (Authority)
                            KillHerSlimes();
                    }

                    // The shot stays on her through the burst, and goes home as she goes.
                    if (t < BurstEnd)
                        DeathShot(npc, 1f, 1f + DeathLean);
                    else
                        DeathShotHome(npc);

                    if (Authority && t >= BurstEnd)
                        End(npc);

                    break;
            }
        }

        /// <summary>
        /// A throw of the frenzy: at the player (where they will be) or somewhere near her, over a
        /// height of her choosing, worked out to come down where it was aimed.
        /// </summary>
        private void FrenzyThrow(NPC npc)
        {
            npc.TargetClosest(false);
            Player target = Main.player[npc.target];

            float toX;

            if (target.active && !target.dead && Main.rand.NextFloat() < FrenzyOnto)
                toX = target.Center.X + MathHelper.Clamp(target.velocity.X, -8f, 8f) * 24f;
            else
                toX = npc.Center.X + Main.rand.NextFloat(-1f, 1f) * FrenzyNear;

            float dx = MathHelper.Clamp(toX - npc.Center.X, -FrenzyReach, FrenzyReach);

            if (!SlimeRite.Ground(npc.Center.X + dx, npc.Bottom.Y, out float land))
                land = npc.Bottom.Y;

            float apex = Math.Min(npc.Bottom.Y, land) - Main.rand.NextFloat(FrenzyLow, FrenzyHigh);
            float up = (float)Math.Sqrt(2f * NpcGravity * (npc.Bottom.Y - apex));
            float ticks = up / NpcGravity + FallTicks(land - apex);

            npc.velocity = new Vector2(MathHelper.Clamp(dx / Math.Max(1f, ticks), -13f, 13f), -up);
            npc.netUpdate = true;
        }

        /// <summary>Ticks for an NPC to fall this far from still, speeding up under gravity to its fastest and no faster.</summary>
        private static float FallTicks(float drop)
        {
            if (drop <= 0f)
                return 0f;

            float capped = NpcMaxFall * NpcMaxFall / (2f * NpcGravity);

            if (drop <= capped)
                return (float)Math.Sqrt(2f * drop / NpcGravity);

            return NpcMaxFall / NpcGravity + (drop - capped) / NpcMaxFall;
        }

        /// <summary>A landing of her death: the weight of all of her, the ground heaved out from under her.</summary>
        private void DeathLanded(NPC npc)
        {
            float weight = beat == DeathFall ? 1f : MathHelper.Clamp(peakFall / NpcMaxFall, 0.5f, 1f);

            ripple = Math.Min(1f, ripple + 0.2f);
            QueenLand(npc, weight, heave: true);
        }

        /// <summary>Her gel convulsing: kicks to her shape, jerks off true, a ripple always running, gel thrown, wet sounds.</summary>
        private void Spasm(NPC npc, float strength)
        {
            if (Main.dedServ)
                return;

            ripple = Math.Max(ripple, Math.Min(1f, 0.2f * strength));

            if (Main.rand.NextFloat() < 0.18f * strength)
            {
                Kick(Main.rand.NextFloat(-0.2f, 0.2f) * strength, Main.rand.NextFloat(-0.06f, 0.06f) * strength, 0.1f * strength);
                Jerk(Main.rand.NextFloat(-0.07f, 0.07f) * strength);
            }

            if (Main.rand.NextFloat() < 0.07f * strength)
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = Main.rand.NextFloat(-0.8f, 0.2f), Volume = 0.35f + 0.15f * Math.Min(1f, strength), MaxInstances = 4 }, npc.Center);

            if (Main.rand.NextFloat() < 0.22f * strength)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 from = npc.Center + way * new Vector2(npc.width * 0.4f, npc.height * 0.35f);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, way * Main.rand.NextFloat(2f, 5f) - new Vector2(0f, 2f), Main.rand.NextFloat(0.5f, 1f),
                    Main.rand.Next(60, 90), GelColour()));
            }
        }

        /// <summary>She cries out: a roar gone wrong, higher and weaker, and something wet in it.</summary>
        private void DeathCry(NPC npc)
        {
            Kick(0.2f, 0f, 0.5f);

            if (Main.dedServ)
                return;

            Cinematic.Roar(npc, 60, 0.6f, SoundID.Roar with { Pitch = 0.5f, Volume = 0.9f });
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.1f, Volume = 1f }, npc.Center);
        }

        /// <summary>
        /// Her wings break: splinters thrown off the length of both of them, tumbling down, sparks
        /// in her colours, the crack of it.
        /// </summary>
        private void BreakWings(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.1f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Pitch = -0.4f, Volume = 0.8f }, npc.Center);

            float near = Near(npc.Center);

            if (near > 0f)
            {
                Cinematic.Flash(Color.White, 0.2f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(6f * near, 20);
            }

            Pose pose = Posed(npc);
            float reach = QueenSlimeArt.WingWidth * 0.8f * FlightWingSize;

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 root = pose.At(WingRootLocal(npc, s));

                for (int i = 0; i < 40; i++)
                {
                    float angle = -Main.rand.NextFloat(0.1f, 1f);
                    Vector2 out_ = new Vector2(s * (float)Math.Cos(angle), (float)Math.Sin(angle));
                    Vector2 at = root + out_ * Main.rand.NextFloat(0.08f, 1f) * reach + Main.rand.NextVector2Circular(10f, 10f);
                    Vector2 velocity = out_ * Main.rand.NextFloat(1f, 5f) + new Vector2(0f, Main.rand.NextFloat(-4f, -1f)) + npc.velocity * 0.5f;

                    PADParticleHandler.SpawnParticle(new QueenShard(at, velocity, Main.rand.Next(60, 110), Main.rand.NextFloat(0.8f, 1.4f)));
                }

                Vector2 middle = root + new Vector2(s * reach * 0.45f, -reach * 0.25f);
                SparkShard.Spray(middle, Color.White, 10, 6f, 50f, 18, palette: QueenSlimeArt.Sparks);
                PADParticleHandler.SpawnParticle(new BloomGeneric(middle, Vector2.Zero, QueenSlimeArt.Violet, 200f / 160f, 18));
            }
        }

        /// <summary>The last three seconds: the wet sounds closer and higher, gel off her, light in her, coming out of her.</summary>
        private void Shudder(NPC npc, int t, float p)
        {
            if (Main.dedServ)
                return;

            if (t % Math.Max(4, 18 - (int)(14 * p)) == 0)
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f + 1.1f * p, Volume = 0.5f + 0.4f * p, MaxInstances = 4 }, npc.Center);

            if (t == 90 || t == 140 || t == 168)
                SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Pitch = -0.2f + 0.4f * p, Volume = 0.6f + 0.3f * p }, npc.Center);

            if (Main.rand.NextFloat() < 0.3f + 0.6f * p)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 from = npc.Center + way * new Vector2(npc.width * 0.45f, npc.height * 0.4f);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, way * Main.rand.NextFloat(2f, 5f + 5f * p) - new Vector2(0f, 2.5f), Main.rand.NextFloat(0.5f, 1.1f),
                    Main.rand.Next(60, 90), GelColour()));
            }

            if (Main.rand.NextFloat() < p)
            {
                Vector2 at = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.45f);
                PADParticleHandler.SpawnParticle(new HoneyMote(at, Main.rand.NextVector2Circular(1.5f, 1.5f) - new Vector2(0f, 1f), QueenSlimeArt.Shade(Main.rand.NextFloat(3f)),
                    Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(20, 34), 0.03f));
            }

            float near = Near(npc.Center);

            if (near > 0f && p > 0.5f)
                Main.LocalPlayer.PIS().BumpScreenShake(3f * p * near, 2);
        }

        /// <summary>
        /// Her last three seconds and her burst, for a player near: the camera <paramref name="blend"/>
        /// of the way over to her, the bars, and the lean, which is on her because the camera is.
        /// Nobody is held. A player who leaves the shot (too far, dead) is let go: the camera walks
        /// home by itself (PisPlayer).
        /// </summary>
        private void DeathShot(NPC npc, float blend, float lean)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            deathWatching = me.active && !me.dead && Vector2.Distance(me.Center, npc.Center) <= SceneRange;

            if (!deathWatching)
                return;

            PisPlayer.BlendCamera(npc.Center, blend);
            Cinematic.Frame();
            Cinematic.Lean(lean);
        }

        /// <summary>She is gone: the camera walked home from her, once, for a player who was in the shot. The bars and the lean go by themselves.</summary>
        private void DeathShotHome(NPC npc)
        {
            if (deathWatching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, ReturnTicks);

            deathWatching = false;
        }

        /// <summary>
        /// She bursts: the flash and the shake, the sound of it, gel and light and dust every way,
        /// her crown and pieces of her thrown, the ground heaved out from under where she was.
        /// </summary>
        private void Burst(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = npc.Center;
            float near = Near(at);

            if (near > 0f)
            {
                Cinematic.Flash(new Color(255, 190, 245), 0.6f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(16f * near, 40);
            }

            // Her own death cry, which vanilla would have played (End silences its copy), in the burst.
            SoundEngine.PlaySound(SoundID.NPCDeath64, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.5f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.3f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 1f }, at);

            for (int i = 0; i < 70; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 from = at + way * new Vector2(npc.width * 0.35f, npc.height * 0.3f);
                Vector2 velocity = way * Main.rand.NextFloat(3f, 15f) - new Vector2(0f, Main.rand.NextFloat(1f, 5f));

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.7f, 1.6f), Main.rand.Next(120, 160), GelColour()));
            }

            // Her dust, as vanilla throws it when she dies, and more.
            for (int i = 0; i < 150; i++)
            {
                Color tint = NPC.AI_121_QueenSlime_GetDustColor();
                tint.A = 200;

                Dust dust = Dust.NewDustDirect(npc.position - new Vector2(10f), npc.width + 20, npc.height + 20, DustID.TintableDust,
                    8f * Main.rand.NextFloatDirection(), -2f + 6f * Main.rand.NextFloatDirection(), 50, tint, 1f + Main.rand.NextFloat() * 0.5f);

                dust.velocity *= Main.rand.NextFloat(0.8f, 1.6f);
                dust.noGravity = Main.rand.NextBool(5);
            }

            for (int i = 0; i < 40; i++)
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Smoke, 0f, 0f, 60, default, Main.rand.NextFloat(1.3f, 2.2f));
                dust.velocity = Main.rand.NextVector2Circular(9f, 7f);
                dust.noGravity = true;
            }

            for (int i = 0; i < 12; i++)
            {
                Vector2 from = new Vector2(npc.Center.X + Main.rand.NextFloat(-1f, 1f) * npc.width, npc.Bottom.Y - Main.rand.NextFloat(4f, 30f));
                PADParticleHandler.SpawnParticle(new DustFog(from, new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-1.5f, -0.3f)),
                    new Color(190, 170, 200), Main.rand.Next(60, 100), Main.rand.NextFloat(0.9f, 1.5f)));
            }

            for (int i = 0; i < 40; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                PADParticleHandler.SpawnParticle(new HoneyMote(at + way * 20f, way * Main.rand.NextFloat(3f, 9f), QueenSlimeArt.Shade(Main.rand.NextFloat(3f)),
                    Main.rand.NextFloat(0.6f, 1.2f), Main.rand.Next(30, 50), 0.02f));
            }

            SparkShard.Spray(at, Color.White, 24, 11f, 130f, 26, palette: QueenSlimeArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Pink, 520f / 160f, 32));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Violet, 300f / 160f, 24));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White, 1f, 16));

            GroundWave.Slam(npc.Bottom, npc.width * 0.5f, 34, 22f, 40);
            LegendsKingSlime.GroundDust(npc.position.X - 80f, npc.position.X + npc.width + 80f, npc.Bottom.Y, 1.2f);

            // Her crown thrown up off her, as vanilla throws it. (Its wing gores are not: her wings
            // are long gone, in splinters.)
            Gore.NewGore(npc.GetSource_Death(), npc.Top + new Vector2(-41f, -40f), new Vector2(Main.rand.NextFloat(-3f, 3f), -9f), GoreID.QueenSlimeCrown, 1f);
        }

        /// <summary>Every slime of hers pops with her, as it would at its thirty seconds, and gives nothing (QueenSlimeMinions). The server's.</summary>
        private static void KillHerSlimes()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && other.life > 0 && QueenSlimeMinions.Hers(other.type))
                    QueenSlimeMinions.Expire(other);
            }
        }

        /// <summary>
        /// Handed back to vanilla to die. CheckDead lets this one through: the loot. Her sound was
        /// the burst's, so vanilla's is not played again; and every client is told she is gone, as
        /// nothing struck her.
        /// </summary>
        private void End(NPC npc)
        {
            ending = true;
            npc.DeathSound = null;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>A glow behind her: a flicker while she convulses, and all of it coming as she shudders at the last.</summary>
        private float DeathBackLight()
        {
            switch (beat)
            {
                case DeathSpasm:
                    return 0.18f * Math.Abs((float)Math.Sin(timer * 0.35f));

                case DeathShudder:
                    return 0.15f + 0.8f * (float)Math.Pow(MathHelper.Clamp(timer / (float)ShudderTicks, 0f, 1f), 1.5);

                default:
                    return 0f;
            }
        }

        /// <summary>How white she is: coming, and flickering, as she shudders at the last.</summary>
        private float DeathOverLight()
        {
            if (beat != DeathShudder)
                return 0f;

            float p = MathHelper.Clamp(timer / (float)ShudderTicks, 0f, 1f);

            return 0.8f * p * p + 0.12f * p * (float)Math.Sin(timer * 1.3f);
        }

        // ---- the blow that kills her -------------------------------------------------------------

        /// <summary>
        /// The blow that would kill her only starts her death (CheckDead refuses it), but vanilla
        /// shows the blow before that is asked, and shows it as her death: her pieces thrown off her
        /// and a burst of her dust (NPC.StrikeNPC runs HitEffect, then checkDead). While her death is
        /// still to be refused, the blow is shown the way vanilla shows one she lives through. Her
        /// pieces come off her at the burst.
        /// </summary>
        internal static void HitNotKilled(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            if (self.type != NPCID.QueenSlimeBoss || self.life > 0 || !self.TryGetGlobalNPC(out LegendsQueenSlime queen) || queen.ending)
            {
                orig(self, hitDirection, dmg, instantKill);
                return;
            }

            int life = self.life;
            self.life = 1;

            try
            {
                orig(self, hitDirection, dmg, instantKill);
            }
            finally
            {
                self.life = life;
            }
        }
    }
}
