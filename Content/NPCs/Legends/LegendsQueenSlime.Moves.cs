using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.NPCs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- her moves, Legends -------------------------------------------------------------------------
    //
    // Two moves more, each in place of one vanilla has just picked, never twice running:
    //
    // THE CORE (on the ground, her first half)
    //     TELL     50   her core glows red and rises in her; she gathers herself
    //     OUT     ...   she throws it: spinning end over end, out through where the player stood and
    //                   round like a boomerang, half a screen wide, and back, slowly (QueenSlimeCore:
    //                   exactly 150 a hit); she waits for it without it
    //     CATCH    20   it goes back into her, and the red goes out of it
    //
    //     Parried on its way (QueenSlimeCore), her core is sent straight back into her, and takes a
    //     tenth of her full life as it goes in (CoreReturned, CoreShattered); then the catch, as ever.
    //
    // THE DASH (in the air, her second half)
    //     LINE    ...   she flies out to one side of the player, level with them
    //     TELL     30   draws back a little, wings raised, and cries out
    //     CHARGE  ...   straight across at them, as the Queen Bee does, and on past
    //     HOLD    246   whoever she meets is inside her: fifty life out of them a second, four times,
    //                   and a hundred and fifty into her, never past half her life
    //     SPIT     24   and out
    //     (END     24   a dash that meets nobody slows and is over)

    public partial class LegendsQueenSlime
    {
        private const int BoomTell = 0;
        private const int BoomOut = 1;
        private const int BoomCatch = 2;

        private const int BoomTellTicks = 50;
        private const int CatchTicks = 20;

        private const int DashLine = 0;
        private const int DashTell = 1;
        private const int DashCharge = 2;
        private const int DashHold = 3;
        private const int DashSpit = 4;
        private const int DashEnd = 5;

        private const int LineMost = 80;
        private const int DashTellTicks = 30;
        private const int ChargeMost = 60;
        private const int GrabTicks = 246;
        private const int SpitTicks = 24;
        private const int DashEndTicks = 24;

        /// <summary>A pick of vanilla's that becomes one of hers: this often, and only this near.</summary>
        private const float BoomChance = 0.3f;
        private const float DashChance = 0.35f;
        private const float BoomRange = 1300f;
        private const float DashRange = 1500f;

        /// <summary>How far to the player's side she lines up, and how fast she comes across.</summary>
        private const float DashGap = 400f;
        private const float DashSpeed = 22f;

        /// <summary>Life into her a second, while she has someone.</summary>
        private const int HealPerSecond = 150;

        /// <summary>The player inside her, or -1. The server's word, sent.</summary>
        private int grabbed = -1;

        /// <summary>The server's: the thrown core, and how long its way is.</summary>
        private int coreProjectile = -1;
        private int coreFlight;

        /// <summary>Whether she is waiting on her thrown core: the core's own test of whether it is still wanted.</summary>
        internal static bool WaitingForCore(int index)
        {
            LegendsQueenSlime queen = Of(index);
            return queen != null && queen.scene == Scene.Boomerang && queen.beat == BoomOut;
        }

        /// <summary>
        /// The server's, after vanilla's AI: if vanilla has just picked a move from rest, sometimes
        /// one of hers instead. The core in her first half, on the ground; the dash in her second,
        /// flying. The pick after one of hers is always vanilla's.
        /// </summary>
        private void ConsiderMove(NPC npc)
        {
            bool picked = aiStateBefore == 0f && npc.ai[0] >= 3f && npc.ai[0] <= 5f;

            if (!picked)
                return;

            if (lastMove != Scene.None)
            {
                lastMove = Scene.None;
                return;
            }

            Player target = Main.player[npc.target];

            if (!target.active || target.dead)
                return;

            float distance = Vector2.Distance(npc.Center, target.Center);

            if (!Halved(npc))
            {
                if (npc.velocity.Y != 0f || distance > BoomRange || Main.rand.NextFloat() >= BoomChance)
                    return;

                BeginBoomerang(npc);
            }
            else
            {
                if (!transitioned || distance > DashRange || Main.rand.NextFloat() >= DashChance)
                    return;

                BeginDash(npc, target);
            }
        }

        // ---- the core ---------------------------------------------------------------------------------

        private void BeginBoomerang(NPC npc)
        {
            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;

            coreProjectile = -1;
            lastMove = Scene.Boomerang;

            Go(npc, Scene.Boomerang, BoomTell);
        }

        private void RunBoomerang(NPC npc, bool entered)
        {
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.rotation = 0f;
            Settle(npc);

            Player target = Main.player[npc.target];

            if (target.active)
                npc.direction = target.Center.X < npc.Center.X ? -1 : 1;

            int t = timer;

            switch (beat)
            {
                case BoomTell:
                    if (entered && !Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Pitch = -0.2f, Volume = 0.8f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.7f, Volume = 0.5f }, npc.Center);
                    }

                    RedGathering(npc, t);

                    if (Authority && t >= BoomTellTicks)
                    {
                        if (!target.active || target.dead)
                        {
                            Release(npc);
                            break;
                        }

                        Throw(npc, target);
                        Next(npc, BoomOut);
                    }

                    break;

                case BoomOut:
                    if (entered)
                        Thrown(npc);

                    if (Authority && (!CoreStillOut(npc) || t >= coreFlight + 90))
                    {
                        if (CoreStillOut(npc))
                            Main.projectile[coreProjectile].Kill();

                        coreProjectile = -1;
                        Next(npc, BoomCatch);
                    }

                    break;

                case BoomCatch:
                    if (entered)
                        Caught(npc);

                    if (Authority && t >= CatchTicks)
                        Release(npc);

                    break;
            }
        }

        /// <summary>The server's: the core out of her, at where the player stands.</summary>
        private void Throw(NPC npc, Player target)
        {
            Vector2 from = npc.Center + new Vector2(0f, -12f);

            coreProjectile = QueenSlimeCore.Throw(npc, from, target.Center, out coreFlight);
            coreOut = true;
            npc.netUpdate = true;
        }

        /// <summary>Whether the core she threw is still out. The server's.</summary>
        private bool CoreStillOut(NPC npc)
        {
            if (coreProjectile < 0 || coreProjectile >= Main.maxProjectiles)
                return false;

            Projectile core = Main.projectile[coreProjectile];

            return core.active && core.type == ModContent.ProjectileType<QueenSlimeCore>() && (int)core.ai[0] == npc.whoAmI;
        }

        /// <summary>Red drawn into her core from round about as it heats, and its light.</summary>
        private void RedGathering(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            float p = MathHelper.Clamp(t / (float)BoomTellTicks, 0f, 1f);
            Vector2 core = npc.Center + new Vector2(0f, -10f * p);

            Lighting.AddLight(core, new Vector3(1f, 0.15f, 0.25f) * (0.3f + 0.9f * p));

            if (Main.rand.NextFloat() < 0.4f + 0.5f * p)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 at = core + way * Main.rand.NextFloat(60f, 110f);

                PADParticleHandler.SpawnParticle(new HoneyMote(at, -way * Main.rand.NextFloat(3f, 5f), QueenSlimeArt.Red, Main.rand.NextFloat(0.5f, 0.9f),
                    Main.rand.Next(18, 26), 0f));
            }

            if (t == BoomTellTicks - 16)
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 0.6f }, core);
        }

        /// <summary>It is out of her: a gulp the wrong way, gel after it, and she rocks back.</summary>
        private void Thrown(NPC npc)
        {
            coreOut = true;
            Kick(0.16f, -npc.direction * 0.06f, 0.35f);
            Jerk(-npc.direction * 0.08f);

            if (Main.dedServ)
                return;

            Vector2 from = npc.Center + new Vector2(0f, -12f);

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = 0.3f, Volume = 0.7f }, from);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.2f, Volume = 0.9f }, from);

            for (int i = 0; i < 10; i++)
            {
                Vector2 velocity = new Vector2(npc.direction * Main.rand.NextFloat(2f, 7f), Main.rand.NextFloat(-5f, -1f));
                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.6f, 1f), 80, GelColour()));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(from, Vector2.Zero, QueenSlimeArt.Red, 150f / 160f, 14));
        }

        /// <summary>It is back in her: she gives under it, a wet gulp, and the red goes out of it.</summary>
        private void Caught(NPC npc)
        {
            coreOut = false;
            Kick(-0.14f, 0f, 0.4f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DripSplash with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);

            Splash(npc, 0.6f);
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, QueenSlimeArt.Red, 180f / 160f, 16));
        }

        /// <summary>
        /// The server's: her own core, parried, has come back into her. It takes a tenth of her
        /// full life from her, whatever her armour (QueenSlimeCore.ParriedShare), and whoever
        /// parried it has had a hand in her. A blow like any other after that: it can take her
        /// to her wings, or to her death.
        /// </summary>
        internal static void CoreReturned(NPC npc, int by, int way)
        {
            if (!Authority || !npc.active || npc.type != NPCID.QueenSlimeBoss || npc.life <= 0)
                return;

            if (by >= 0 && by < Main.maxPlayers && Main.player[by].active)
                npc.PlayerInteraction(by);

            Parry.Take(npc, Math.Max(1, npc.lifeMax / QueenSlimeCore.ParriedShare), way);
        }

        /// <summary>
        /// Every machine's, as the core it sent back is ended in her: she caves round it and is
        /// knocked the way it was going, her crown jumps, crystal breaks, and her gel is thrown
        /// out of the far side of her. <paramref name="came"/> is the way the core came in.
        /// </summary>
        internal static void CoreShattered(NPC npc, Vector2 came)
        {
            if (Main.dedServ)
                return;

            Vector2 way = came.SafeNormalize(new Vector2(npc.direction == 0 ? 1 : -npc.direction, 0f));
            int side = way.X < 0f ? -1 : 1;
            Vector2 at = npc.Center;
            float half = npc.width * 0.5f;

            if (npc.TryGetGlobalNPC(out LegendsQueenSlime queen))
            {
                queen.Kick(-0.3f, side * 0.14f, 0.7f);
                queen.Jerk(side * 0.1f);
                queen.PopCrown(0.22f);
            }

            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.1f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.3f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.2f, Volume = 0.9f }, at);

            PADParticleHandler.SpawnParticle(new ParryRing(at, 14f, half + 90f, 20, ParryEffects.Steel));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Red, 220f / 160f, 16));
            SparkShard.Spray(at, QueenSlimeArt.RedHot, 16, 10f, 32f, 18, falls: true, palette: QueenSlimeArt.Gel);

            // Out of the far side of her, the way it was going.
            Vector2 far = at + way * (half * 0.6f);

            for (int i = 0; i < 18; i++)
            {
                Vector2 velocity = way.RotatedByRandom(0.8f) * Main.rand.NextFloat(4f, 12f) + new Vector2(0f, Main.rand.NextFloat(-3f, -0.5f));

                PADParticleHandler.SpawnParticle(new SlimeGlob(far + Main.rand.NextVector2Circular(14f, 14f), velocity, Main.rand.NextFloat(0.7f, 1.25f), 90, GelColour()));
            }

            QueenDust(new Vector2(far.X - 24f, far.Y - 24f), 48, 48, 22, 7f);

            Player local = Main.LocalPlayer;

            if (local != null && local.active && !local.dead)
            {
                float near = 1f - MathHelper.Clamp((Vector2.Distance(local.Center, at) - 500f) / 1100f, 0f, 1f);

                if (near > 0f)
                    local.PIS().BumpScreenShake(9f * near, 16);
            }
        }

        /// <summary>How red her core is: coming as she means to throw it, going once it is back.</summary>
        private float RedGlow()
        {
            if (scene != Scene.Boomerang)
                return 0f;

            if (beat == BoomTell)
                return Ease(timer / (float)BoomTellTicks) * (0.85f + 0.15f * (float)Math.Sin(timer * 0.8f));

            if (beat == BoomCatch)
                return 1f - Ease(timer / (float)CatchTicks);

            return 0f;
        }

        /// <summary>
        /// How big her core is drawn: out of the light's size as she comes out of it, swelling as it
        /// heats, a gulp as it comes back.
        /// </summary>
        private float CoreSize()
        {
            if (scene == Scene.Rite && beat == RiteGrow)
                return MathHelper.Lerp(CoreGlowSize, 1f, Ease(timer / (float)GrowTicks));

            if (scene == Scene.Boomerang && beat == BoomTell)
                return 1f + 0.12f * Ease(timer / (float)BoomTellTicks);

            if (scene == Scene.Boomerang && beat == BoomCatch)
                return 1f + 0.2f * (1f - Ease(timer / 10f));

            return 1f;
        }

        /// <summary>
        /// The red on her core: a glow in its own shape, as tints so it shows on a bright sky, and
        /// a little light; and the heat on the core itself.
        /// </summary>
        private static void DrawRed(SpriteBatch spriteBatch, Vector2 at, float turn, float size, float red)
        {
            if (QueenSlimeArt.MorphHalo == null)
                return;

            Texture2D halo = QueenSlimeArt.MorphHalo.Value;
            Rectangle frame = QueenSlimeArt.MorphHaloFrame(QueenSlimeArt.MorphFrames - 1);
            Texture2D core = TextureAssets.Extra[ExtrasID.QueenSlimeCrystalCore].Value;

            QueenSlimeArt.DrawRed(spriteBatch, halo, frame, core, at, turn, new Vector2(size), red);
        }

        // ---- the dash -----------------------------------------------------------------------------------

        private void BeginDash(NPC npc, Player target)
        {
            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;

            side = npc.Center.X >= target.Center.X ? 1 : -1;
            grabbed = -1;
            lastMove = Scene.Dash;

            Go(npc, Scene.Dash, DashLine);
        }

        private void RunDash(NPC npc, bool entered)
        {
            npc.noGravity = true;
            npc.noTileCollide = true;

            Player target = Main.player[npc.target];

            if (Authority && (!target.active || target.dead))
            {
                npc.TargetClosest(false);
                target = Main.player[npc.target];
            }

            bool hasTarget = target.active && !target.dead;
            int t = timer;

            switch (beat)
            {
                case DashLine:
                    {
                        if (!hasTarget)
                        {
                            if (Authority)
                                Release(npc);

                            break;
                        }

                        Vector2 spot = target.Center + new Vector2(side * DashGap, -8f);

                        Steer(npc, spot, 18f, 0.12f);
                        npc.direction = -side;
                        npc.rotation = MathHelper.Clamp(npc.velocity.X * 0.03f, -0.25f, 0.25f);

                        if (Authority && (Vector2.Distance(npc.Center, spot) < 32f || t >= LineMost))
                            Next(npc, DashTell);

                        break;
                    }

                case DashTell:
                    {
                        if (!hasTarget)
                        {
                            if (Authority)
                                Release(npc);

                            break;
                        }

                        // Level with them, drawing back a little: the breath before.
                        Vector2 spot = target.Center + new Vector2(side * (DashGap + 40f * Ease(t / (float)DashTellTicks)), -8f);

                        npc.velocity = (spot - npc.Center) * 0.18f;
                        npc.direction = -side;
                        npc.rotation *= 0.8f;

                        if (entered)
                            DashCry(npc);

                        if (!Main.dedServ && t % 5 == 0)
                            Kick(Main.rand.NextFloat(-0.05f, 0.05f), side * 0.02f);

                        if (Authority && t >= DashTellTicks)
                            Next(npc, DashCharge);

                        break;
                    }

                case DashCharge:
                    {
                        npc.velocity.X = -side * DashSpeed;
                        npc.velocity.Y = hasTarget ? MathHelper.Clamp((target.Center.Y - npc.Center.Y) * 0.03f, -2f, 2f) : 0f;
                        npc.rotation = 0f;
                        dashStretch = 1f;

                        if (entered)
                            Charge(npc);

                        Gust(npc);

                        if (Authority)
                            Catch(npc, target, hasTarget, t);

                        break;
                    }

                case DashHold:
                    {
                        Player held = grabbed >= 0 && grabbed < Main.maxPlayers ? Main.player[grabbed] : null;

                        if (held == null || !held.active || held.dead)
                        {
                            if (Authority)
                                Next(npc, DashSpit);

                            break;
                        }

                        // Asked every tick: stop asking and they are out.
                        held.GetModPlayer<QueenSlimeGrab>().Hold(npc.whoAmI);

                        npc.velocity *= 0.9f;
                        npc.rotation *= 0.9f;

                        // Out of anything solid, so whoever she lets go is not let go inside the ground.
                        if (Collision.SolidCollision(npc.position, npc.width, npc.height))
                            npc.velocity.Y = Math.Min(npc.velocity.Y, -2f);

                        if (entered)
                            Swallow(npc, held);

                        Digest(npc, t);

                        if (Authority)
                        {
                            for (int k = 1; k * 60 <= GrabTicks; k++)
                            {
                                if (Crossed(k * 60))
                                    Heal(npc);
                            }

                            if (t >= GrabTicks)
                                Next(npc, DashSpit);
                        }

                        break;
                    }

                case DashSpit:
                    if (entered)
                        SpitOut(npc);

                    grabbed = -1;
                    npc.velocity *= 0.9f;

                    if (Authority && t >= SpitTicks)
                        Release(npc);

                    break;

                case DashEnd:
                    npc.velocity *= 0.9f;
                    npc.rotation *= 0.9f;

                    if (Authority && t >= DashEndTicks)
                        Release(npc);

                    break;
            }
        }

        /// <summary>Toward a point, easing into it: at most this fast, turning this much of the way a tick.</summary>
        private static void Steer(NPC npc, Vector2 spot, float speed, float turn)
        {
            Vector2 want = spot - npc.Center;
            float distance = want.Length();

            want = distance > 1f ? want * (Math.Min(speed, distance * 0.15f + 2f) / distance) : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, want, turn);
        }

        /// <summary>
        /// The server's, while she comes across: whoever she meets is taken, anyone at all; past the
        /// player by as far as she started, or out of time, the dash is over.
        /// </summary>
        private void Catch(NPC npc, Player target, bool hasTarget, int t)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead || player.GetModPlayer<QueenSlimeGrab>().Held)
                    continue;

                if (!npc.Hitbox.Intersects(player.Hitbox))
                    continue;

                grabbed = i;
                Next(npc, DashHold);
                return;
            }

            bool past = hasTarget && -side * (npc.Center.X - target.Center.X) > DashGap;

            if (past || t >= ChargeMost)
                Next(npc, DashEnd);
        }

        /// <summary>Life into her, and the green of it, never past half (her winged half is for good). The server's.</summary>
        private static void Heal(NPC npc)
        {
            int amount = Math.Min(HealPerSecond, npc.lifeMax / 2 - npc.life);

            if (amount <= 0)
                return;

            npc.life += amount;
            npc.HealEffect(amount);
            npc.netUpdate = true;
        }

        /// <summary>Her cry before the dash, her wings up, and a beat of them.</summary>
        private void DashCry(NPC npc)
        {
            Kick(0.1f, 0f, 0.2f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.4f, Volume = 0.6f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.3f, Volume = 0.8f }, npc.Center);
        }

        /// <summary>Across: the rush of air, a beat of her wings.</summary>
        private void Charge(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = 0.2f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item32 with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
        }

        /// <summary>Air torn past her as she goes, and gel off the back of her.</summary>
        private static void Gust(NPC npc)
        {
            if (Main.dedServ)
                return;

            if (Main.rand.NextBool(2))
            {
                Vector2 at = npc.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * npc.width, Main.rand.NextFloat(-0.5f, 0.5f) * npc.height);
                PADParticleHandler.SpawnParticle(new WindStreak(at, npc.velocity * Main.rand.NextFloat(0.25f, 0.45f), Main.rand.NextFloat(90f, 160f),
                    Main.rand.NextFloat(5f, 9f), Main.rand.Next(12, 20)));
            }

            if (Main.rand.NextBool(3))
            {
                Vector2 back = npc.Center - Vector2.Normalize(npc.velocity + new Vector2(0.001f, 0f)) * npc.width * 0.45f;
                PADParticleHandler.SpawnParticle(new SlimeGlob(back, -npc.velocity * 0.15f + new Vector2(0f, Main.rand.NextFloat(-2f, 0f)),
                    Main.rand.NextFloat(0.5f, 0.9f), 70, GelColour()));
            }
        }

        /// <summary>Taken: a wet gulp, she gives round them, the screen of whoever it is shaken.</summary>
        private void Swallow(NPC npc, Player held)
        {
            Kick(-0.16f, 0f, 0.6f);
            dashStretch = 0f;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.7f, Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.DripSplash with { Pitch = -0.4f, Volume = 0.9f }, npc.Center);

            if (held.whoAmI == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(12f, 30);

            Splash(npc, 0.8f);
        }

        /// <summary>While she has them: her gel working round them, a swallow each second, bubbles.</summary>
        private void Digest(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            if (t > 0 && t % 60 == 0)
            {
                Kick(-0.07f, 0f, 0.3f);
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f, Volume = 0.6f, MaxInstances = 3 }, npc.Center);
            }

            ripple = Math.Max(ripple, 0.12f);

            if (Main.rand.NextBool(4))
            {
                Color tint = NPC.AI_121_QueenSlime_GetDustColor();
                tint.A = 150;

                Dust dust = Dust.NewDustDirect(npc.Center - new Vector2(30f, 20f), 60, 40, DustID.TintableDust, 0f, -1f, 80, tint, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
                dust.velocity *= 0.4f;
            }
        }

        /// <summary>Out: spat, with gel after them.</summary>
        private void SpitOut(NPC npc)
        {
            Kick(0.2f, 0f, 0.4f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.3f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = 0.1f, Volume = 0.6f }, npc.Center);

            for (int i = 0; i < 14; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 4f) - new Vector2(0f, 4f);
                PADParticleHandler.SpawnParticle(new SlimeGlob(npc.Center, velocity, Main.rand.NextFloat(0.6f, 1.1f), 80, GelColour()));
            }
        }

        // ---- whoever she holds ----------------------------------------------------------------------------

        /// <summary>Whether anyone is inside her now.</summary>
        internal static bool HoldingAnyone(NPC npc)
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (!player.active || player.dead)
                    continue;

                QueenSlimeGrab grab = player.GetModPlayer<QueenSlimeGrab>();

                if (grab.Held && grab.HeldBy == npc.whoAmI)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Her body, her crown and her light, over whoever she holds, for every Queen holding
        /// someone: after the players are drawn, since she is drawn before them. Her body goes
        /// through vanilla's shader for her gel, which player draw data cannot carry, so it is
        /// drawn here in a batch of its own rather than as a layer of the player's.
        /// </summary>
        internal static void DrawHolding()
        {
            if (Main.gameMenu)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            bool begun = false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (!npc.active || npc.type != NPCID.QueenSlimeBoss || !npc.TryGetGlobalNPC(out LegendsQueenSlime queen))
                    continue;

                if (!HoldingAnyone(npc))
                    continue;

                if (!begun)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                    begun = true;
                }

                queen.DrawOver(npc, spriteBatch);
            }

            if (begun)
                spriteBatch.End();
        }

        /// <summary>What goes over whoever she holds: lit as her own draw lights her.</summary>
        private void DrawOver(NPC npc, SpriteBatch spriteBatch)
        {
            Main.instance.LoadNPC(npc.type);

            Vector2 screenPos = Main.screenPosition;
            Color light = Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f));
            Color lit = Color.Lerp(Color.White, light, 0.5f);
            Pose pose = Posed(npc, RiteGrowth());

            DrawBody(npc, spriteBatch, screenPos, lit, pose);
            DrawCrown(npc, spriteBatch, screenPos, lit, pose);
            DrawOverLight(npc, spriteBatch, screenPos, pose);
        }
    }
}
