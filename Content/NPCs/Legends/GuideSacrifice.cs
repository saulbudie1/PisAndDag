using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// The Guide's sacrifice, drawn out.
    ///
    /// Vanilla resolves the whole thing in one frame: the burning doll strikes the Guide
    /// for 9999 and calls <see cref="NPC.SpawnWOF"/> on the very next line. This pulls
    /// those apart — the camera goes to him, he smoulders, he comes apart, and only
    /// once the shot is back on the player does the Wall arrive, far off, with its own
    /// intro (WallOfFleshIntro) to walk in on.
    ///
    /// Nothing here waits for vanilla's version and then undoes it. The doll is consumed
    /// before vanilla's lava check can see it, so the strike and the summon simply never
    /// happen and there is nothing to unpick. That also makes this independent of WHERE
    /// vanilla keeps that code, which moved between 1.4.4 and 1.4.5.
    ///
    /// Owned by a ModSystem rather than by the Guide, deliberately. He stops existing two
    /// thirds of the way through and something still has to hold the camera and spawn the
    /// Wall afterwards, which is the same reason the Megashark's death outro lives on
    /// PisPlayer instead of on the boss.
    /// </summary>
    public class GuideSacrifice : ModSystem
    {
        /// <summary>
        /// The rite: the camera goes to the Guide, he smoulders and comes apart, the
        /// shot walks home. With it off the doll is left to vanilla, which kills him
        /// and summons the Wall in one frame. static readonly rather than const so the
        /// guarded code is not flagged as unreachable by the compiler.
        /// </summary>
        public static readonly bool RitualEnabled = true;

        /// <summary>
        /// The old tail — a hush, a building quake, the Wall breaching at its peak, an
        /// aftershock. OFF: the Wall now arrives the frame the shot is home, far away,
        /// and its own intro (WallOfFleshIntro) supplies the one roar. Flip this to
        /// have the ground build up to it again.
        /// </summary>
        public static readonly bool QuakeEnabled = false;

        // ---- the beats ------------------------------------------------------

        private const int PanOut = 7*60+30;
        private const int Settle = 60;
        private const int Smoulder = 180;
        private const int Dissolve = 120;
        private const int Linger = 60;
        private const int PanBack = 7 * 60 + 30;

        private const int SettleAt = PanOut;
        private const int SmoulderAt = SettleAt + Settle;
        private const int DissolveAt = SmoulderAt + Smoulder;
        private const int LingerAt = DissolveAt + Dissolve;
        private const int BackAt = LingerAt + Linger;

        /// <summary>The shot is home. Everything after this happens on the player.</summary>
        private const int PansDoneAt = BackAt + PanBack;

        // ---- the quake ------------------------------------------------------

        /// <summary>Beat of nothing once the camera is back, before the ground moves.</summary>
        private const int Hush = 120;

        /// <summary>The build. Mild at the start, violent by the end.</summary>
        private const int Quake = 300;

        /// <summary>The shake falling away after the Wall is already through.</summary>
        private const int Aftershock = 120;

        private const int QuakeAt = PansDoneAt + Hush;

        /// <summary>
        /// The frame the Wall arrives on: the quake's peak, or — with the quake off —
        /// the frame the shot is home. Properties rather than consts only because
        /// QuakeEnabled is a runtime switch.
        /// </summary>
        private static int BreachAt => QuakeEnabled ? QuakeAt + Quake : PansDoneAt;

        /// <summary>Done. With the quake off there is no aftershock to wait out.</summary>
        private static int RiteEnd => QuakeEnabled ? BreachAt + Aftershock : BreachAt;

        private const float QuakeFloor = 1.5f;
        private const float QuakePeak = 18f;

        /// <summary>
        /// Curve on the build.
        /// </summary>
        ///
        /// <remarks>
        /// Squared, not linear. A linear ramp spends most of its length at a middling
        /// shake that reads as constant; squaring keeps it almost gentle for the first
        /// three seconds and puts nearly all the violence in the last two, so there is a
        /// moment where it is obviously about to give rather than five seconds of rumble.
        /// </remarks>
        private const float QuakeCurve = 2f;

        /// <summary>
        /// Tiles knocked loose per tick at full intensity.
        ///
        /// There is a ceiling on how much this can actually buy: Terraria keeps 6000 dust
        /// at once, and at this rate — with each fleck living roughly forty ticks — the
        /// quake alone is already sitting near that. Past here the extra requests are
        /// simply refused, and worse, they refuse everything ELSE's dust too. If it still
        /// needs to read denser, scale is the lever that costs nothing.
        /// </summary>
        private const int ShedRate = 176;

        /// <summary>Margin in tiles beyond the screen edge to shake loose as well.</summary>
        private const int ShedMargin = 3;

        /// <summary>
        /// Shape of both pans.
        ///
        /// The spec is "normal, then exponentially faster to the halfway point, then
        /// slowing to arrive as gently as it left". That is a symmetric sigmoid, and this
        /// power form gives one with a genuinely steep middle — smoothstep is far too
        /// polite over five seconds and reads as a slow constant drift.
        ///
        /// A little linear is mixed back in because a pure sigmoid has zero speed at both
        /// ends, which over this distance looks like the camera is stuck before it starts.
        /// </summary>
        private const float PanPower = 3f;
        private const float PanShape = 0.85f;

        // ---- effects --------------------------------------------------------

        /// <summary>
        /// Smoke puffs per tick, at the first moment and at the last.
        ///
        /// Stated as a rate rather than as a loop count, because a third of the old
        /// output is less than one puff a tick and an integer loop cannot express that —
        /// it floors to nothing. The fraction is spent as a chance instead.
        ///
        /// This replaces three throttles stacked on each other: an integer count, a half
        /// multiplier on the loop bound, and a separate probability gate. Their peaks
        /// multiplied out to two puffs a tick in a way that was nearly impossible to
        /// predict from the numbers, which is presumably why they kept needing tuning.
        /// </summary>
        private const float SmoulderPeak = 0.265f;
        private const float SmoulderOpening = 0.014f;
        private const float SmoulderReach = 15f;

        // ---- state ----------------------------------------------------------

        /// <summary>Whether a sacrifice is running. Only ever one at a time.</summary>
        public static bool Active;

        private static int timer;

        /// <summary>Index of the doomed Guide, or -1 once he is gone.</summary>
        private static int guide = -1;

        /// <summary>True only for the one Guide this ritual has claimed.</summary>
        public static bool Claimed(NPC npc) => Active && guide >= 0 && npc.whoAmI == guide;

        /// <summary>Where the Guide is standing. The camera travels here.</summary>
        private static Vector2 altar;

        /// <summary>
        /// Where the doll burned, which is where the Wall has to come from.
        ///
        /// Kept apart from the altar on purpose. SpawnWOF refuses any position above
        /// maxTilesY - 205, and the Guide is almost always in a house on the surface —
        /// summoning at HIS feet would silently do nothing at all.
        /// </summary>
        private static Vector2 wallSpot;

        /// <summary>Who is watching. The pans start and end on them.</summary>
        private static int watcher;

        public override void ClearWorld()
        {
            Active = false;
            timer = 0;
            guide = -1;
        }

        /// <summary>
        /// Claims the Guide, given where the doll went into the lava.
        ///
        /// Returns false when there is no Guide to sacrifice or one is already being
        /// sacrificed, in which case the caller should leave the doll alone and let
        /// vanilla have it.
        /// </summary>
        public static bool Begin(Vector2 dollPosition)
        {
            if (!RitualEnabled || Active || Main.netMode == NetmodeID.MultiplayerClient)
                return false;

            int found = -1;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.Guide)
                {
                    found = i;
                    break;
                }
            }

            if (found < 0)
                return false;

            NPC npc = Main.npc[found];

            npc.TargetClosest(false);

            Player player = Main.player[npc.target];

            if (!player.active)
                return false;

            Active = true;
            timer = 0;
            guide = found;
            altar = npc.Center;
            wallSpot = dollPosition;
            watcher = npc.target;

            npc.dontTakeDamage = true;
            npc.netUpdate = true;

            return true;
        }

        public override void PostUpdateEverything()
        {
            if (!Active)
                return;

            NPC doomed = guide >= 0 && guide < Main.maxNPCs ? Main.npc[guide] : null;

            if (doomed != null && doomed.active && doomed.type == NPCID.Guide)
            {
                // Held exactly where he was caught. Letting him keep walking during the
                // pan would mean the camera arrives somewhere he no longer is.
                doomed.Center = altar;
                doomed.velocity = Vector2.Zero;
                doomed.dontTakeDamage = true;
            }
            else
            {
                guide = -1;
            }

            Camera();
            Effects(doomed);

            Tremor();

            timer++;

            if (timer == DissolveAt)
                ComeApart(doomed);

            if (timer == LingerAt)
                Announce(doomed);

            if (timer == BreachAt)
                Breach();

            if (timer >= RiteEnd)
                Finish();
        }

        /// <summary>
        /// Out to him, held, and back — with the hold covering everything between the two
        /// pans so the shot never once returns to the player mid-sacrifice.
        /// </summary>
        private static void Camera()
        {
            if (Main.dedServ)
                return;

            // Handed back entirely once the shot is home. Holding blend at 1 on the
            // player's own centre would look right but quietly cancel the normal
            // cursor-led camera offset for the whole nine seconds that follow.
            if (timer >= PansDoneAt)
                return;

            Player player = Main.player[watcher];

            if (!player.active)
                return;

            Vector2 from = player.Center;

            float t =
                timer < PanOut ? Pan(timer / (float)PanOut)
                : timer < BackAt ? 1f
                : 1f - Pan((timer - BackAt) / (float)PanBack);

            // Blend is pinned at one and the FOCUS is what travels. Easing the blend
            // instead would have the camera drifting toward a point that is itself
            // moving, and the shape of the move would stop being the shape written above.
            PisPlayer.BlendCamera(Vector2.Lerp(from, altar, t), 1f);
        }

        /// <summary>Slow, then sharply faster, then slow again — symmetric about the middle.</summary>
        private static float Pan(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);

            float a = MathF.Pow(t, PanPower);
            float b = MathF.Pow(1f - t, PanPower);

            float sigmoid = a + b <= 0f ? t : a / (a + b);

            return MathHelper.Lerp(t, sigmoid, PanShape);
        }

        /// <summary>
        /// Smoke building on him across the two seconds before he goes — a wisp at first,
        /// pouring by the end. The ramp is the whole point: a constant plume would say
        /// nothing about what is coming.
        /// </summary>
        private static void Effects(NPC doomed)
        {
            if (Main.dedServ || doomed == null || !doomed.active)
                return;

            if (timer < SmoulderAt || timer >= DissolveAt)
                return;

            float ramp = (timer - SmoulderAt) / (float)Smoulder;

            float rate = MathHelper.Lerp(SmoulderOpening, SmoulderPeak, ramp);

            int puffs = (int)rate;

            // The leftover fraction is a chance at one more, so a rate under one puff a
            // tick still emits — just not every tick.
            if (Main.rand.NextFloat() < rate - puffs)
                puffs++;

            for (int i = 0; i < puffs; i++)
            {
                Vector2 at = doomed.Center + Main.rand.NextVector2Circular(
                    doomed.width * 0.5f, doomed.height * 0.5f);

                Vector2 drift = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-2f, -0.6f) * (0.6f + ramp));

                PADParticleHandler.SpawnParticle(new MuzzleSmoke(at, drift,
                    Main.rand.Next(40, 75), Main.rand.NextFloat(0.35f, 0.7f) * (0.7f + ramp)));
            }

            if (Main.rand.NextFloat() < ramp)
            {
                Dust ember = Dust.NewDustPerfect(
                    doomed.Center + Main.rand.NextVector2Circular(SmoulderReach, SmoulderReach),
                    DustID.Torch, new Vector2(0f, Main.rand.NextFloat(-2f, -0.5f)), 0, default, 1.1f);

                ember.noGravity = true;
            }
        }

        /// <summary>He burns away. No gore — the ash IS the body.</summary>
        private static void ComeApart(NPC doomed)
        {
            if (doomed == null || !doomed.active)
                return;

            if (!Main.dedServ)
            {
                AshDissolve.FromNPC(doomed, new Vector2(0f, -1.2f));

                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = -0.4f, Volume = 0.8f }, altar);
                Main.LocalPlayer.PIS().BumpScreenShake(4f, 16);
            }

            // Stops drawing from this frame. The ash cloud is standing in his place and
            // holds his shape for a moment, so the swap is not visible.
            doomed.alpha = 255;
        }

        /// <summary>
        /// Kills him for real, purely so vanilla prints its own death line with his own
        /// given name rather than something reconstructed here.
        ///
        /// No relocation needed. His death events only summon the Wall if he dies standing
        /// in lava, and he is standing wherever he lives — the summon is ours to make,
        /// seven seconds from now.
        /// </summary>
        private static void Announce(NPC doomed)
        {
            if (doomed == null || !doomed.active)
                return;

            doomed.dontTakeDamage = false;
            doomed.life = 0;
            doomed.checkDead();

            guide = -1;
        }

        /// <summary>
        /// The ground giving way, and the ceiling with it.
        ///
        /// Runs the build and the aftershock as one function off a single strength value,
        /// because they are the same effect read forwards and backwards — the tiles shed
        /// at whatever the shake currently is, so nothing has to know which half it is in.
        /// </summary>
        private static void Tremor()
        {
            if (!QuakeEnabled || Main.dedServ || timer < QuakeAt)
                return;

            float strength;

            if (timer < BreachAt)
            {
                float t = (timer - QuakeAt) / (float)Quake;
                strength = MathHelper.Lerp(QuakeFloor, QuakePeak, MathF.Pow(t, QuakeCurve));
            }
            else
            {
                // Eased down rather than dropped, and driven for the whole two seconds.
                float t = (timer - BreachAt) / (float)Aftershock;
                strength = QuakePeak * (1f - Ease(t));
            }

            // AddScreenShake, not the bump. The bump only ever raises, which is right for
            // effects that overlap but useless here: this needs to come DOWN on command,
            // and a two-tick duration means it stops the moment this stops asking.
            Main.LocalPlayer.PIS().AddScreenShake(strength, 2);

            Shed(strength / QuakePeak);
        }

        /// <summary>Smoothstep. Zero slope at both ends, so the aftershock tails off.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Knocks dust off every exposed face on screen.
        ///
        /// Tiles are sampled at random across the view rather than walked, which is what
        /// keeps this cheap at any intensity — turning the effect up costs more samples,
        /// not a bigger search. The dust type comes from the tile itself, so stone sheds
        /// stone and mud sheds mud, and it falls from a ceiling but lifts off a floor.
        /// </summary>
        private static void Shed(float intensity)
        {
            int tries = (int)(ShedRate * intensity);

            if (tries <= 0)
                return;

            int left = (int)(Main.screenPosition.X / 16f) - ShedMargin;
            int right = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + ShedMargin;
            int top = (int)(Main.screenPosition.Y / 16f) - ShedMargin;
            int bottom = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + ShedMargin;

            left = Math.Max(left, 5);
            top = Math.Max(top, 5);
            right = Math.Min(right, Main.maxTilesX - 5);
            bottom = Math.Min(bottom, Main.maxTilesY - 5);

            if (right <= left || bottom <= top)
                return;

            for (int i = 0; i < tries; i++)
            {
                int x = Main.rand.Next(left, right);
                int y = Main.rand.Next(top, bottom);

                Tile tile = Framing.GetTileSafely(x, y);

                if (!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType])
                    continue;

                bool ceiling = !Framing.GetTileSafely(x, y + 1).HasUnactuatedTile;
                bool floor = !Framing.GetTileSafely(x, y - 1).HasUnactuatedTile;

                // Buried tiles have no face for anything to fall off.
                if (!ceiling && !floor)
                    continue;

                int index = WorldGen.KillTile_MakeTileDust(x, y, tile);

                // 6000 is the "this tile has no dust" answer, and also one past the end.
                if (index < 0 || index >= Main.maxDust)
                    continue;

                Dust dust = Main.dust[index];

                dust.velocity = ceiling
                    ? new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.6f, 2.2f) * intensity)
                    : new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.6f, -0.3f) * intensity);

                dust.scale = Main.rand.NextFloat(0.7f, 1.1f + intensity * 0.5f);
            }
        }

        // ---- where the Wall comes from ------------------------------------

        /// <summary>
        /// Half a screen, in world pixels, at the usual 1920 width and zoom 1. A
        /// constant rather than Main.screenWidth because the spawn is decided on the
        /// server, which has no screen.
        /// </summary>
        private const float HalfScreen = 960f;

        /// <summary>Slack on top of the maths, so "half a screen" is never a pixel short.</summary>
        private const float SpawnMargin = 64f;

        /// <summary>
        /// The Wall's full-life speed, from its AI (NPC.cs, aiStyle 27): 1.5, expert
        /// ×1.35 + 0.35, the good world ×1.1 + 0.2. Master runs the expert branch.
        /// </summary>
        private static float WallSpeed()
        {
            float speed = 1.5f;

            if (Main.expertMode)
                speed = speed * 1.35f + 0.35f;

            if (Main.getGoodWorld)
                speed = speed * 1.1f + 0.2f;

            return speed;
        }

        /// <summary>
        /// Far enough that when its intro is over — WallOfFleshIntro.Ticks of it
        /// walking at the player the whole time — it is still half a screen away. Placed
        /// on the side vanilla would put it (away from the world's centre, so it walks
        /// inward), and handed to SpawnWOF, which keeps its own rules for depth and for
        /// never landing within 1200 of anyone; this is always further than that.
        /// </summary>
        private static Vector2 FarSpot()
        {
            Player player = Main.player.IndexInRange(watcher) && Main.player[watcher].active
                ? Main.player[watcher]
                : Main.player[Player.FindClosest(wallSpot, 16, 16)];

            float distance = WallSpeed() * WallOfFleshIntro.Ticks + HalfScreen + SpawnMargin;
            int side = wallSpot.X < Main.maxTilesX * 8f ? -1 : 1;

            return new Vector2(player.Center.X + side * distance, wallSpot.Y);
        }

        /// <summary>
        /// The Wall arrives. With the quake on, this is its peak and the ground answers;
        /// with it off, the shot has just come home and the Wall's own intro takes over
        /// from here — its roar is the one this used to make.
        /// </summary>
        private static void Breach()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.SpawnWOF(FarSpot());

            if (!QuakeEnabled || Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.ForceRoar with { Volume = 1f, Pitch = -0.55f }, Main.LocalPlayer.Center);
            Main.LocalPlayer.PIS().AddScreenShake(QuakePeak, 6);
        }

        /// <summary>The shake is spent and the Wall is already here.</summary>
        private static void Finish()
        {
            Active = false;
            timer = 0;
            guide = -1;
        }
    }

    /// <summary>
    /// Takes the Guide Voodoo Doll away from vanilla before it can burn.
    ///
    /// Vanilla's handling of a doll in lava is one unbroken frame: strike the Guide for
    /// 9999, print his death, summon the Wall. There is no hook inside that sequence to
    /// stretch it out, so the only way to own the moment is to make sure it never starts —
    /// the doll is destroyed here, and everything it would have done is done by the ritual
    /// instead, in order, over sixteen seconds.
    /// </summary>
    public class SacrificialDoll : GlobalItem
    {
        /// <summary>
        /// How far below the doll counts as "in lava" for our purposes.
        ///
        /// Deliberately more generous than vanilla's own test, and that margin is the
        /// whole mechanism: it means this fires a tick or two BEFORE vanilla decides the
        /// doll has burned. Matching vanilla exactly would be a race with its own lava
        /// check in the same frame, and losing that race means the Guide is already dead.
        /// </summary>
        private const int LavaLookahead = 10;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            return entity.type == ItemID.GuideVoodooDoll;
        }

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
        {
            // Rite off: the doll is left to vanilla, which strikes the Guide and calls
            // SpawnWOF in the same frame. The Wall's intro then finds the Wall.
            if (!GuideSacrifice.RitualEnabled)
                return;

            if (Main.netMode == NetmodeID.MultiplayerClient || GuideSacrifice.Active)
                return;

            if (item.type != ItemID.GuideVoodooDoll)
                return;

            bool touching = Collision.LavaCollision(
                item.position - new Vector2(0f, LavaLookahead),
                item.width,
                item.height + LavaLookahead * 2);

            if (!touching)
                return;

            // No Guide to sacrifice, or one already burning: leave the doll alone and let
            // vanilla do whatever it would have done with it.
            if (!GuideSacrifice.Begin(item.Center))
                return;

            // Consumed. From here vanilla has nothing to act on, so its strike and its
            // summon never happen and the ritual owns the whole sequence.
            int slot = item.whoAmI;

            item.TurnToAir();

            if (Main.netMode == NetmodeID.Server && slot >= 0 && slot < Main.maxItems)
                NetMessage.SendData(MessageID.SyncItem, number: slot);
        }
    }

    /// <summary>
    /// Catches the Guide on the frame he would have burnt, and hands him to the ritual.
    /// </summary>
    public class SacrificialGuide : GlobalNPC
    {
        /// <summary>
        /// Keeps the claimed Guide alive until the ritual is ready to kill him.
        ///
        /// The ritual is no longer started from here. The old version tested whether the
        /// GUIDE was standing in lava, which is almost never true — the doll is what
        /// burns, and vanilla kills whichever Guide exists wherever he happens to be. That
        /// test was false in the ordinary case and the whole thing never fired.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            return npc.type != NPCID.Guide || !GuideSacrifice.Claimed(npc);
        }

        /// <summary>
        /// He stands still and takes nothing for the length of it. Returning false here is
        /// what stops him fleeing across hell while the camera is on its way to him.
        /// </summary>
        public override bool PreAI(NPC npc)
        {
            // Claimed, not merely "a Guide during a sacrifice". The first version compared
            // whoAmI against itself, which is never unequal — so every other Guide in the
            // world froze for the length of the ritual too.
            if (npc.type != NPCID.Guide || !GuideSacrifice.Claimed(npc))
                return true;

            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;

            return false;
        }
    }
}
