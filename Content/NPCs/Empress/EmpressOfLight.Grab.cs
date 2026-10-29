using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the grab (Legends, her first half) ---------------------------------------------------------------------
    //
    // By request: "instead of she js dashing and damaging, if it hits player than player is held on one hand,
    // zooms in then other hand flashes a ball of light, and pushes to the player making a huge explosion dealing
    // roughly 200 damage normal and have scalings."
    //
    //     CATCH   14   her dash stops dead on them: a ring of light off her, and they are in her hand, held out
    //     LIFT    40   held up a little; the camera leans in on the two of them; her other hand goes up and back,
    //                  and a ball of light fills it, white at its heart, her colours round it, flashing
    //     PUSH    14   the hand comes down and through, the ball into them
    //     BLAST   30   a blast of light where they are: 200 in a normal world (100, doubled as the game doubles any
    //                  hostile shot; 60 in an expert one, as her lances go from 50 to 30), a fifth again as all she
    //                  throws in Legends (EmpressShots: 240), and by day it kills as all of hers does; they are
    //                  thrown out of her hand, away from her
    //     LET     22   her arms come down; and vanilla's own pause before its next move
    //
    // A dash that meets nobody goes by as vanilla's does.
    //
    // Her second half's dash, in Legends, takes them too (by request: "Her vanilla dash in phase two grabs the player in
    // one hand and with that hand, burns the lights pierces thru the body of the player loosing 75 life a sec for 4 sec
    // ... and is casted aside, ragdoll style"), in the hand her bow leaves free:
    //
    //     CATCH   14   as above
    //     BURN   240   light burns out of her palm through them and out of their back in rays: 75 of their life a second
    //                  for four seconds, whatever they wear (as fire takes it: EmpressHeld; by day, as all of hers, it
    //                  kills)
    //     CAST    18   she flings them aside: thrown, limp and turning, off the walls, until they come to rest
    //     LET     22   as above
    //
    // Prettier, by request ("make every attack more pretty and aesthetics with some weird brain tickling sound
    // effect"): the world about the ball drawn into it, turning, as it fills (EmpressWarp); a shimmer played backwards
    // building to the push, cut off by it; a whispered rush as it comes; the blast a shockwave through the world, glass
    // in it.

    public partial class EmpressOfLight
    {
        /// <summary>The player in her hand, or -1.</summary>
        private int grabbed = -1;

        /// <summary>
        /// Which of her arms holds them (the screen's left): the side they were caught on, kept as the act's side
        /// (actSide, sent), so every machine has her hold them in the same hand.
        /// </summary>
        private bool holdLeft => actSide < 0;

        private const int GrabCatch = 0;
        private const int GrabLift = 1;
        private const int GrabPush = 2;
        private const int GrabBlast = 3;
        private const int GrabLet = 4;
        private const int GrabBurn = 5;
        private const int GrabCast = 6;

        private const int CatchTicks = 14;
        private const int LiftTicks = 40;
        private const int PushTicks = 14;
        private const int BlastTicks = 30;
        private const int LetTicks = 22;
        private const int BurnTicks = 240;
        private const int CastTicks = 18;

        /// <summary>Into the cast, the tick they leave her hand; how hard they are thrown, and how fast they turn.</summary>
        private const int CastLetGo = 5;
        private const float CastSpeed = 21f;
        private const float CastSpin = 0.42f;

        /// <summary>The blast's reach about the player, and what it takes (vanilla's way: normal, expert).</summary>
        private const float BlastRadius = 96f;
        private const int BlastNormal = 100;
        private const int BlastExpert = 60;

        /// <summary>How far up the held arm is raised over the lift, and where the other hand goes to fill with light.</summary>
        private const float HoldRaise = 0.42f;
        private const float WindBack = 2.15f;

        /// <summary>How much further than her body her dash takes whoever it meets, each way.</summary>
        private const int DashReach = 14;

        /// <summary>Her dash, in her Legends half (vanilla's ticks 40 to 90 of it): whoever it meets is taken. The server's.</summary>
        private void DashMeets(NPC npc)
        {
            if (npc.ai[1] < 40f || npc.ai[1] > 90f)
                return;

            // A little more than her body as she dashes (by request: "During her dash, her hitbox is slightly bigger"),
            // and where she was half a tick ago too, so nobody slips between two of her ticks.
            Rectangle her = npc.Hitbox;
            her.Inflate(DashReach, DashReach);
            Rectangle before = her;
            before.Offset((int)(-npc.velocity.X * 0.5f), (int)(-npc.velocity.Y * 0.5f));

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                // As her blow would: not through a player's moment of grace after a hit.
                if (!p.active || p.dead || p.immune || !p.Hitbox.Intersects(her) && !p.Hitbox.Intersects(before))
                    continue;

                BeginGrab(npc, p);
                return;
            }
        }

        /// <summary>Takes this player into her hand. The server's.</summary>
        private void BeginGrab(NPC npc, Player who)
        {
            grabbed = who.whoAmI;
            actSide = who.Center.X < npc.Center.X ? -1 : 1;

            npc.velocity = Vector2.Zero;
            npc.target = who.whoAmI;

            Go(npc, Scene.Grab, GrabCatch);
            timer = 0;
        }

        /// <summary>The held arm's angle and the other's, this tick (left and right are the screen's).</summary>
        private void GrabArms(out float hold, out float other)
        {
            float t = timer;
            float raise = beat == GrabCatch ? 0f : beat == GrabLift ? HoldRaise * Ease(t / LiftTicks) : beat == GrabLet ? HoldRaise * (1f - Ease(t / LetTicks)) : HoldRaise;

            // Her second half's: held out and up through the burn; swung out and away to cast them aside.
            if (beat == GrabBurn)
                raise = HoldRaise * Ease(t / 20f);
            else if (beat == GrabCast)
                raise = HoldRaise + 0.9f * Ease(t / (float)CastLetGo) - 1.2f * Ease((t - CastLetGo) / 10f);

            // Held out to its side, a little down at the catch, raised over the lift.
            float outward = holdLeft ? MathHelper.Pi : 0f;
            float up = holdLeft ? 1f : -1f;
            hold = outward + up * (raise - 0.12f);

            // The other hand: down at its side at the catch, up and back (away from the held player) over the
            // lift, over the top and through to them over the push, and back down as she lets go.
            float side = holdLeft ? 0f : MathHelper.Pi;
            float back = holdLeft ? -(MathHelper.Pi - WindBack) : -WindBack;
            float rest = holdLeft ? 0.9f : MathHelper.Pi - 0.9f;
            float at = hold;

            switch (beat)
            {
                case GrabCatch:
                    other = MathHelper.Lerp(rest, side, Ease(t / CatchTicks) * 0.3f);
                    break;
                case GrabLift:
                    other = LerpAngle(MathHelper.Lerp(rest, side, 0.3f), back, Ease(t / (LiftTicks * 0.6f)));
                    break;
                case GrabPush:
                    other = LerpAngle(back, at, (float)Math.Pow(Math.Clamp(t / PushTicks, 0f, 1f), 2.2));
                    break;
                case GrabBlast:
                    other = at;
                    break;
                default:
                    other = LerpAngle(at, rest, Ease(t / LetTicks));
                    break;
            }
        }

        private static float LerpAngle(float from, float to, float amount) => from + MathHelper.WrapAngle(to - from) * MathHelper.Clamp(amount, 0f, 1f);

        /// <summary>Is the one in her hand still in it this tick?</summary>
        private bool StillHeld => beat < GrabBlast || beat == GrabBurn || beat == GrabCast && timer < CastLetGo;

        /// <summary>Where the held player is: in the palm of the holding hand.</summary>
        private Vector2 HeldAt(NPC npc)
        {
            GrabArms(out float hold, out _);
            return Palm(npc, holdLeft, hold) + new Vector2(0f, 4f);
        }

        /// <summary>The ball of light: in the other palm, and how big.</summary>
        private void Ball(NPC npc, out Vector2 at, out float size)
        {
            GrabArms(out _, out float other);
            at = Palm(npc, !holdLeft, other);

            size = beat switch
            {
                GrabLift => 34f * Ease((timer - 8f) / 28f),
                GrabPush => 34f + 10f * timer / PushTicks,
                _ => 0f,
            };
        }

        private void RunGrab(NPC npc, bool entered)
        {
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.damage = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.Opacity = 1f;
            npc.velocity *= 0.6f;

            Player held = grabbed >= 0 && grabbed < Main.maxPlayers ? Main.player[grabbed] : null;
            bool holding = held != null && held.active && !held.dead && StillHeld;
            bool second = npc.AI_120_HallowBoss_IsInPhase2();

            if (held == null || !held.active || held.dead && StillHeld)
            {
                if (Authority)
                    EndGrab(npc);

                return;
            }

            // In her second half she holds them in the hand her bow leaves free: turned the other way, the bow in the other.
            npc.spriteDirection = npc.direction = holdLeft == second ? 1 : -1;

            if (second)
                FreeArmAt(HoldAngle(), snap: true);

            // Every machine with a screen holds them there (theirs moves them; the rest see them there at
            // once, not when the next packet comes); the server leaves where they are to their own machine.
            if (holding && !Main.dedServ)
                held.GetModPlayer<EmpressHeld>().Hold(npc.whoAmI, HeldAt(npc));

            int t = timer;

            // The one held sees it close: the camera between them, leaning in, the bars.
            if (!Main.dedServ && grabbed == Main.myPlayer && (beat <= GrabBlast || beat == GrabBurn))
            {
                PisPlayer.BlendCamera(Vector2.Lerp(npc.Center, held.Center, 0.55f), 1f);
                Cinematic.Frame();
                Cinematic.Lean(beat == GrabBlast ? 1.15f : beat == GrabBurn ? 1.5f : 1.42f, beat == GrabCatch ? 0.12f : 0.06f);
            }

            switch (beat)
            {
                case GrabCatch:
                    if (entered)
                    {
                        Caught(npc, held);

                        if (!second)
                            Remark(npc, Words.Held, always: true);
                    }

                    if (Authority && t >= CatchTicks)
                        Next(npc, second ? GrabBurn : GrabLift);

                    break;

                case GrabBurn:
                    // Burned through, on their own machine only (their life is theirs to lose: another machine's copy of
                    // them would show the numbers falling too); seen by all. The Everclear Mirror's copy, if it is the
                    // one she holds, on its owner's, as if it were the one at the keyboard (MimicSystem.AlsoOnCopy).
                    if (!Main.dedServ)
                    {
                        BurnHeld(npc, held);
                        MimicSystem.AlsoOnCopy(() => BurnHeld(npc, held));
                    }

                    Burning(npc, held, t, entered);

                    if (Authority && t >= BurnTicks)
                        Next(npc, GrabCast);

                    break;

                case GrabCast:
                    if (Crossed(CastLetGo))
                        Cast(npc, held);

                    if (Authority && t >= CastTicks)
                        Next(npc, GrabLet);

                    break;

                case GrabLift:
                    if (entered)
                        Sound(EmpressSounds.Swell, held.Center, 0.74f, 0.75f, 2);

                    if (!Main.dedServ)
                        Gathering(npc, t);

                    if (Authority && t >= LiftTicks)
                        Next(npc, GrabPush);

                    break;

                case GrabPush:
                    if (entered && !Main.dedServ)
                    {
                        Sound(SoundID.Item163, npc.Center, 0.25f, 0.9f);
                        Sound(SoundID.Item9, npc.Center, -0.5f, 0.7f);
                        Sound(EmpressSounds.WhooshUp, npc.Center, 0.4f, 0.8f, 2);
                        Scream(npc, 0.45f, 1f);
                    }

                    if (Authority && t >= PushTicks)
                        Next(npc, GrabBlast);

                    break;

                case GrabBlast:
                    if (entered)
                        Blast(npc, held);

                    if (Authority && t >= BlastTicks)
                        Next(npc, GrabLet);

                    break;

                case GrabLet:
                    if (Authority && t >= LetTicks)
                        EndGrab(npc);

                    break;
            }
        }

        /// <summary>The burn on the one she holds, if they are this machine's player for the moment.</summary>
        private static void BurnHeld(NPC npc, Player held)
        {
            if (held.whoAmI == Main.myPlayer)
                held.GetModPlayer<EmpressHeld>().Burn(npc.whoAmI, NPC.ShouldEmpressBeEnraged());
        }

        /// <summary>The holding arm's angle this tick.</summary>
        private float HoldAngle()
        {
            GrabArms(out float hold, out _);
            return hold;
        }

        /// <summary>
        /// The burn: light out of her palm through them, out of their back in rays; heard as a sear, a hum climbing, her
        /// scream as it begins; sparks thrown out behind them.
        /// </summary>
        private void Burning(NPC npc, Player held, int t, bool entered)
        {
            if (Main.dedServ)
                return;

            Vector2 palm = Palm(npc, holdLeft, HoldAngle());

            if (entered)
            {
                Scream(npc, 0.35f, 1f, rings: true);
                Sound(EmpressSounds.Sear, held.Center, 0f, 1f, 2);
                Sound(EmpressSounds.Beam, held.Center, -0.3f, 0.8f, 2);
            }

            if (t % 30 == 15)
            {
                Sound(EmpressSounds.Beam, held.Center, MathHelper.Lerp(-0.4f, 0.5f, t / (float)BurnTicks), 0.5f, 3);
                Sound(SoundID.Item15, held.Center, MathHelper.Lerp(-0.2f, 0.7f, t / (float)BurnTicks), 0.45f, 3);
            }

            if (grabbed == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(2.5f, 3);

            Vector2 through = (held.Center - palm).SafeNormalize(new Vector2(holdLeft ? -1f : 1f, 0f));

            if (Main.rand.NextBool(2))
            {
                Vector2 way = through.RotatedByRandom(0.6f) * Main.rand.NextFloat(5f, 12f);
                PADParticleHandler.SpawnParticle(new SparkShard(held.Center + through * 14f, way, EmpressArt.Prism(Main.rand.NextFloat()), 26f, 18f, 70f, 16));
            }

            Lighting.AddLight(held.Center, new Vector3(1f, 0.95f, 0.9f) * 1.4f);
        }

        /// <summary>Cast aside: out of her hand, thrown limp and turning (on their own machine); a whoosh, her voice.</summary>
        private void Cast(NPC npc, Player held)
        {
            Vector2 palm = Palm(npc, holdLeft, HoldAngle());
            Vector2 away = (held.Center - npc.Center).SafeNormalize(new Vector2(holdLeft ? -1f : 1f, 0f));

            // (The Everclear Mirror's copy, if it is the one she holds, on its owner's machine: MimicSystem.AlsoOnCopy.)
            CastOut(held, away);
            MimicSystem.AlsoOnCopy(() => CastOut(held, away));

            if (Main.dedServ)
                return;

            Sound(EmpressSounds.Slash, palm, -0.3f, 0.9f, 2);
            Sound(EmpressSounds.WhooshDown, palm, -0.2f, 0.8f, 2);
            Scream(npc, 0.15f, 0.8f);
            EmpressWarp.Ripple(palm, 16f, 50f, 10f, 22);
        }

        /// <summary>Cast aside: the one she held thrown limp and turning, if they are this machine's player for the moment.</summary>
        private void CastOut(Player held, Vector2 away)
        {
            if (held.whoAmI != Main.myPlayer)
                return;

            float spin = (holdLeft ? -1f : 1f) * CastSpin;
            held.GetModPlayer<EmpressHeld>().Throw(away * CastSpeed + new Vector2(0f, -6f), spin, EmpressHeld.RagdollTicks);
            held.PIS().BumpScreenShake(10f, 20);
        }

        /// <summary>The catch: a ring of light off her, a crack of her voice, the world jolted.</summary>
        private void Caught(NPC npc, Player held)
        {
            if (Main.dedServ)
                return;

            Scream(npc, 0.4f, 0.85f);
            Sound(SoundID.Item29, held.Center, -0.2f, 0.7f);
            Sound(SoundID.Item14, held.Center, -0.9f, 0.5f);
            Sound(EmpressSounds.Tingle, held.Center, 0.3f, 0.7f);
            EmpressWarp.Ripple(held.Center, 11f, 34f, 7f, 22);

            PADParticleHandler.SpawnParticle(new BloomGeneric(held.Center, Vector2.Zero, EmpressArt.Pink, 200f / 160f, 16));
            Cinematic.Roar(held.Center, 30, 0.5f, quiet: true, rim: EmpressArt.Sky);

            if (grabbed == Main.myPlayer)
                Main.LocalPlayer.PIS().BumpScreenShake(6f, 12);
        }

        /// <summary>Light running into the other hand as it fills.</summary>
        private void Gathering(NPC npc, int t)
        {
            Ball(npc, out Vector2 at, out float size);

            if (t == 8)
                Sound(SoundID.Item162, at, 0.3f, 0.7f);

            // The ball filling: a chime climbing from low to high.
            if (t >= 8 && t % 6 == 2)
                Sound(SoundID.Item4, at, MathHelper.Lerp(-0.6f, 0.9f, (t - 8) / (float)(LiftTicks - 8)), MathHelper.Lerp(0.3f, 0.6f, (t - 8) / (float)(LiftTicks - 8)));

            if (t % 3 == 0 && size > 2f)
            {
                Vector2 from = at + Main.rand.NextVector2CircularEdge(120f, 120f);
                Dust dust = Dust.NewDustPerfect(from, DustID.RainbowMk2, (at - from) * 0.09f, 0, EmpressArt.Prism(Main.rand.NextFloat()), 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        /// <summary>The blast: where they are, the server's blow; light, a ring, glass, and them thrown clear.</summary>
        private void Blast(NPC npc, Player held)
        {
            Vector2 at = held.Center;

            Hitbox(npc, EmpressHitbox.Shape.Circle, at, BlastRadius, Hurt(npc, BlastNormal, BlastExpert), 4);

            // (The Everclear Mirror's copy, if it is the one she holds, on its owner's machine: MimicSystem.AlsoOnCopy.)
            BlastOut(npc, held, at);
            MimicSystem.AlsoOnCopy(() => BlastOut(npc, held, at));

            if (Main.dedServ)
                return;

            Sound(SoundID.Item14, at, -0.2f, 1f);
            Sound(SoundID.Item14, at, -0.95f, 0.9f);
            Sound(SoundID.Item29, at, -0.6f, 1f);
            Sound(SoundID.Item29, at, 0.95f, 0.6f);
            Sound(SoundID.Shatter, at, 0.3f, 0.6f);
            Sound(EmpressSounds.Shock, at, 0f, 1f, 2);
            Sound(EmpressSounds.Shards, at, 0.1f, 0.7f, 2);
            EmpressWarp.Ripple(at, 26f, 84f, 22f, 36);

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White, 420f / 160f, 22));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, EmpressArt.Pink, 300f / 160f, 30));
            Cinematic.Roar(at, 46, 1.2f, quiet: true, rim: EmpressArt.Pink);

            for (int i = 0; i < 70; i++)
            {
                Vector2 way = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 17f);
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, way, 0, EmpressArt.Prism(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2.2f));
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }

            blastAt = at;
            blastTick = Main.GameUpdateCount;
        }

        /// <summary>The blast throws the one she held clear, if they are this machine's player for the moment.</summary>
        private void BlastOut(NPC npc, Player held, Vector2 at)
        {
            if (held.whoAmI != Main.myPlayer)
                return;

            Vector2 away = (at - npc.Center).SafeNormalize(new Vector2(holdLeft ? -1f : 1f, 0f));
            held.GetModPlayer<EmpressHeld>().Throw(away * 13f + new Vector2(0f, -4f));
            held.PIS().BumpScreenShake(14f, 26);
        }

        /// <summary>Where the blast was, and when (for its rings and its glass, drawn over the players).</summary>
        private Vector2 blastAt;
        private uint blastTick;

        /// <summary>Let go: vanilla's pause before its next move. The server's.</summary>
        private void EndGrab(NPC npc)
        {
            grabbed = -1;
            EndAct(npc);
        }

        // ---- drawing --------------------------------------------------------------------------------------------

        private void DrawGrab(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            // Her second half: the bow in her other hand, the held arm hers as the burn has it.
            if (SecondLook(npc))
            {
                float glow = beat == GrabBurn ? 0.6f + 0.2f * (float)Math.Sin(timer * 0.3f) : 0.45f;
                float lit = beat == GrabBurn ? 0.25f : beat == GrabCatch ? 0.3f * (1f - timer / (float)CatchTicks) : 0f;
                DrawSecondHer(npc, spriteBatch, screenPos, lit, glow);
                return;
            }

            GrabArms(out float hold, out float other);

            float left = holdLeft ? hold : other;
            float right = holdLeft ? other : hold;
            float bright = beat == GrabBlast ? 0.5f * (1f - Ease(timer / 18f)) : beat == GrabCatch ? 0.3f * (1f - timer / (float)CatchTicks) : 0f;

            DrawHerActing(spriteBatch, npc, screenPos, Arms.At(left, right), bright, 0.45f);
        }

        /// <summary>The ball over the held player, and the blast's rings and glass.</summary>
        private void DrawGrabOver(NPC npc, SpriteBatch spriteBatch)
        {
            if (beat == GrabBurn)
                DrawBurn(npc, spriteBatch);

            if (beat == GrabLift || beat == GrabPush)
            {
                Ball(npc, out Vector2 at, out float size);

                if (size > 1f)
                {
                    float flash = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.9f);
                    float hue = Main.GameUpdateCount / 90f;

                    EmpressArt.DrawBloom(spriteBatch, at, size * 3.2f, EmpressArt.Additive(EmpressArt.Prism(hue), 0.55f * flash));
                    EmpressArt.DrawBloom(spriteBatch, at, size * 1.6f, EmpressArt.Additive(EmpressArt.Pink, 0.8f * flash));
                    EmpressArt.DrawBloom(spriteBatch, at, size, EmpressArt.Additive(Color.White, 1f));

                    for (int k = 0; k < 4; k++)
                    {
                        float turn = Main.GameUpdateCount * 0.05f + k * MathHelper.PiOver2;
                        EmpressArt.DrawStar(spriteBatch, at, size * (1.8f + 0.4f * flash), turn * 0.3f + k * 0.4f, EmpressArt.Additive(EmpressArt.Prism(hue + k * 0.25f), 0.35f));
                    }

                    // Sparks circling in to it, and the world about it drawn in, turning.
                    for (int k = 0; k < 6; k++)
                    {
                        float a = Main.GameUpdateCount * 0.13f + k * MathHelper.TwoPi / 6f;
                        float r = size * (2.6f + 0.8f * (float)Math.Sin(Main.GameUpdateCount * 0.2f + k));
                        Vector2 spark = at + a.ToRotationVector2() * r;
                        EmpressArt.DrawStar(spriteBatch, spark, 18f, a, EmpressArt.Additive(Color.White, 0.8f));
                        EmpressArt.DrawBloom(spriteBatch, spark, 14f, EmpressArt.Additive(EmpressArt.Prism(hue + k / 6f), 0.6f));
                    }

                    EmpressWarp.Lens(at, size * 7f + 40f, -0.16f, 0.9f);
                }
            }

            uint since = Main.GameUpdateCount - blastTick;

            if (blastTick != 0 && since < 40)
            {
                float p = since / 40f;
                float fade = 1f - p;

                EmpressArt.DrawBloom(spriteBatch, blastAt, 260f * (0.6f + p), EmpressArt.Additive(Color.White, 0.9f * fade * fade));

                for (int k = 0; k < 3; k++)
                {
                    float r = (90f + 220f * k) * (float)Math.Pow(p, 0.6) + 20f;
                    DrawRing(spriteBatch, blastAt, r, 10f * fade + 2f, EmpressArt.Additive(EmpressArt.Prism(k / 3f + p), 0.7f * fade));
                }
            }
        }

        /// <summary>
        /// The burn: a beam of white out of her palm into them, and out of their back rays of her light fanning away,
        /// flickering, long and short; light about them; the air along it shimmering.
        /// </summary>
        private void DrawBurn(NPC npc, SpriteBatch spriteBatch)
        {
            if (grabbed < 0 || grabbed >= Main.maxPlayers)
                return;

            Player held = Main.player[grabbed];
            float time = (float)Main.timeForVisualEffects;
            float p = Ease(timer / 16f) * (1f - Ease((timer - BurnTicks + 10) / 10f));
            Vector2 palm = Palm(npc, holdLeft, HoldAngle());
            Vector2 through = (held.Center - palm).SafeNormalize(new Vector2(holdLeft ? -1f : 1f, 0f));
            float way = through.ToRotation();

            EmpressArt.DrawBeam(spriteBatch, palm, held.Center, 30f, EmpressArt.Additive(EmpressArt.Pink, 0.35f * p));
            EmpressArt.DrawBeam(spriteBatch, palm, held.Center, 9f, EmpressArt.Additive(Color.White, 0.95f * p));

            for (int k = -3; k <= 3; k++)
            {
                float a = way + k * 0.16f + 0.05f * (float)Math.Sin(time * 0.2f + k);
                float length = (220f + 380f * (0.5f + 0.5f * (float)Math.Sin(time * 0.37f + k * 1.9f))) * (1f - Math.Abs(k) * 0.12f);
                Color colour = EmpressArt.Prism(k / 7f + time / 120f);

                EmpressArt.DrawRay(spriteBatch, held.Center, a, length * p, 44f - Math.Abs(k) * 5f, EmpressArt.Additive(colour, 0.5f * p));
                EmpressArt.DrawRay(spriteBatch, held.Center, a, length * 0.8f * p, 12f, EmpressArt.Additive(Color.White, 0.7f * p));
            }

            float beatOf = 0.8f + 0.2f * (float)Math.Sin(time * 0.6f);
            EmpressArt.DrawBloom(spriteBatch, held.Center, 150f * beatOf, EmpressArt.Additive(Color.White, 0.75f * p));
            EmpressArt.DrawBloom(spriteBatch, held.Center, 300f, EmpressArt.Additive(EmpressArt.Pink, 0.35f * p));
            EmpressArt.DrawStar(spriteBatch, held.Center, 160f * beatOf, time * 0.04f, EmpressArt.Additive(Color.White, 0.7f * p));
            EmpressArt.DrawBloom(spriteBatch, palm, 80f, EmpressArt.Additive(Color.White, 0.8f * p));

            EmpressWarp.Haze(palm, held.Center + through * 500f, 60f, 3f * p, 70f, 0.5f);
        }

        /// <summary>
        /// A thin ring of light, drawn as short lines round it: enough of them that it reads as round, its lines turning
        /// no more than a few degrees at each joint (so a thick ring shows no beads where they overlap).
        /// </summary>
        internal static void DrawRing(SpriteBatch spriteBatch, Vector2 at, float radius, float width, Color colour, float gapWay = 0f, float gapHalf = 0f)
        {
            int steps = Math.Clamp((int)(radius * 0.25f + width * 1.5f), 48, 220);
            Vector2 previous = Vector2.Zero;
            bool havePrevious = false;

            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * MathHelper.TwoPi;
                bool inGap = gapHalf > 0f && Math.Abs(MathHelper.WrapAngle(a - gapWay)) < gapHalf;
                Vector2 point = at + a.ToRotationVector2() * radius;

                if (havePrevious && !inGap)
                    EmpressArt.DrawLine(spriteBatch, previous, point, width, colour);

                previous = point;
                havePrevious = !inGap;
            }
        }
    }

    /// <summary>
    /// Being in the Empress's hand (EmpressOfLight.Grab): held where her palm is, unable to move or use anything, for as
    /// long as she asks (every tick); burned through, in her second half (75 life a second, as fire takes it); then
    /// thrown, as the blast leaves them, or cast aside limp: turning, bouncing off her walls, no use of themselves until
    /// they come to rest (a ragdoll, by request).
    /// </summary>
    public class EmpressHeld : ModPlayer
    {
        /// <summary>Life a second the burn takes (by request: "loosing 75 life a sec"), as the game counts a loss (twice it, a second).</summary>
        private const int BurnRegen = 150;

        /// <summary>By day, as all of hers, it kills.</summary>
        private const int BurnKills = 6000;

        /// <summary>The most a cast lasts, and the least before they may come to rest; how much of their speed a wall gives back.</summary>
        public const int RagdollTicks = 90;
        private const int RagdollLeast = 24;
        private const float WallKeeps = 0.55f;

        /// <summary>The way they die of it (the mod's voice).</summary>
        private const string SeenThrough = " was seen through.";

        private int heldBy = -1;
        private int heldTicks;
        private Vector2 at;
        private Vector2 thrown;
        private bool throwing;

        private int burnTicks;
        private bool burnKills;

        private int ragdoll;
        private int ragdollAge;
        private float spin;
        private int righting;

        public bool Held => heldBy >= 0 && heldTicks > 0 && !Player.dead;

        /// <summary>Thrown limp: no use of themselves, turning, bounced off her walls.</summary>
        public bool Ragdolled => ragdoll > 0 && !Player.dead;

        public void Hold(int npcIndex, Vector2 where)
        {
            heldBy = npcIndex;
            heldTicks = 3;
            at = where;
        }

        /// <summary>Burned through this tick (asked every tick it lasts).</summary>
        public void Burn(int npcIndex, bool kills)
        {
            burnTicks = 3;
            burnKills = kills;
        }

        public void Throw(Vector2 velocity, float turning = 0f, int limp = 0)
        {
            heldBy = -1;
            heldTicks = 0;
            thrown = velocity;
            throwing = true;
            spin = turning;
            ragdoll = limp;
            ragdollAge = 0;
            righting = 0;
        }

        public override void ResetEffects()
        {
            if (burnTicks > 0)
                burnTicks--;

            if (Ragdolled)
                Player.noItems = true;

            if (!Held)
            {
                if (heldBy >= 0)
                {
                    Player.fullRotation = 0f;
                    heldBy = -1;
                }

                heldTicks = 0;
                return;
            }

            heldTicks--;
            Player.noItems = true;
        }

        public override void SetControls()
        {
            if (!Held && !Ragdolled)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override void UpdateBadLifeRegen()
        {
            if (burnTicks <= 0)
                return;

            // As fire takes it: no healing of its own while it burns, and the loss counted by the game.
            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= burnKills ? BurnKills : BurnRegen;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (burnTicks > 0)
                damageSource = PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Player.name + SeenThrough));

            return true;
        }

        public override void PreUpdateMovement()
        {
            if (throwing)
            {
                throwing = false;
                Player.velocity = thrown;
                Player.fallStart = (int)(Player.position.Y / 16f);

                if (ragdoll <= 0)
                    Player.fullRotation = 0f;

                return;
            }

            if (Ragdolled)
            {
                Tumble();
                return;
            }

            if (righting > 0)
            {
                // Righted again, after the cast.
                righting--;
                Player.fullRotation = MathHelper.WrapAngle(Player.fullRotation) * 0.7f;

                if (righting == 0)
                    Player.fullRotation = 0f;
            }

            if (!Held)
                return;

            Player.Center = at;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fullRotation = 0.12f * (float)Math.Sin(Main.GameUpdateCount * 0.2f);
            Player.fullRotationOrigin = Player.Size * 0.5f;
        }

        /// <summary>Thrown limp: turning about their middle, slowing; no fall counted; done when the time is up, or they lie still.</summary>
        private void Tumble()
        {
            ragdollAge++;
            ragdoll--;

            Player.fullRotationOrigin = Player.Size * 0.5f;
            Player.fullRotation += spin;
            spin *= 0.975f;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.noFallDmg = true;

            bool still = ragdollAge >= RagdollLeast && Player.velocity.LengthSquared() < 1.5f;

            if (ragdoll <= 0 || still)
            {
                ragdoll = 0;
                righting = 12;
            }
        }

        /// <summary>Thrown against her wall (EmpressArenaPlayer): what it gave back, and the turn knocked the other way; the thud of it.</summary>
        internal void Bounced(Vector2 at)
        {
            spin = -spin * 0.8f + Main.rand.NextFloat(-0.08f, 0.08f);

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(EmpressSounds.Thunk with { Pitch = -0.6f, Volume = 0.6f, MaxInstances = 3 }, at);
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.4f, Volume = 0.5f, MaxInstances = 3 }, at);

            for (int i = 0; i < 8; i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2Circular(4f, 4f), 0, EmpressArt.Prism(Main.rand.NextFloat()), 1.1f);
                dust.noGravity = true;
            }
        }

        public override void OnRespawn()
        {
            ragdoll = 0;
            righting = 0;
            burnTicks = 0;
            Player.fullRotation = 0f;
        }
    }
}
