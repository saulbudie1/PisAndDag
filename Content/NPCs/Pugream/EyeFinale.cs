using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// The first time the Eye is beaten, the way the author has drawn it.
    ///
    /// The player is on one point of life (EyeFinaleEye's frenzy has put them there) and
    /// the world narrows to them. The camera goes to the Eye, which has backed off, and
    /// it comes in, slowly, four seconds of it, the view closing in with it. At six blocks
    /// it stops, and gathers itself, and an arrow comes in from somewhere nobody was
    /// looking, past the player's head and into it. From there the camera follows the
    /// arrow's line back the way it came, a long way, to a man standing on his own in a
    /// gale with a sword on his back that is not his. Then it walks home, the player can
    /// move, and he is not there any more. Only then does the world count the Eye as
    /// beaten, and only then is he on the shore to be found (PugreamPresence).
    ///
    /// Everything runs from his AI (PugreamAtTheEye), on every machine, from four synced
    /// numbers and a start point: which Eye, which player, how far in, which way he
    /// faces. The Eye is dead halfway through; something has to still be there to finish
    /// the shot, and it is him. Where the Eye goes is worked out from where the player
    /// and he are standing, so it is the same everywhere without being sent. The camera,
    /// the zoom, the light, the wind and the arrow are each machine's own.
    ///
    /// The timeline, in ticks from the moment the player hits one:
    ///
    ///     0      the Eye is taken; bars in, UI out, the camera pans to it
    ///     45     it has backed off; it starts coming in
    ///     285    it is six blocks out, and stops, and trembles
    ///     295    the arrow
    ///     335    the camera goes to find where it came from
    ///     445    him, in the light, in the wind
    ///     645    the camera walks home
    ///     745    the player can move; the Eye is beaten; he is gone
    /// </summary>
    public static class EyeFinale
    {
        // ============================================================
        // THE TIMELINE
        // ============================================================

        /// <summary>The camera's pan from the player to the Eye.</summary>
        public const int PanIn = 60;

        /// <summary>The Eye backing off from the hit that put the player on one.</summary>
        public const int Recoil = 45;

        /// <summary>Its approach. Four seconds, as asked.</summary>
        public const int Approach = 240;

        public const int ApproachEnd = Recoil + Approach;

        /// <summary>It stops at arm's length and gathers itself for this long before the arrow.</summary>
        public const int Beat = 10;

        public const int HitAt = ApproachEnd + Beat;

        /// <summary>The camera stays on the kill for this long.</summary>
        public const int AfterHit = 40;

        public const int PanToPugAt = HitAt + AfterHit;

        /// <summary>The pan along the arrow's line to him.</summary>
        public const int PanToPug = 110;

        public const int HoldAt = PanToPugAt + PanToPug;

        /// <summary>On him.</summary>
        public const int HoldPug = 200;

        public const int PanBackAt = HoldAt + HoldPug;

        /// <summary>The walk home.</summary>
        public const int PanBack = 100;

        public const int End = PanBackAt + PanBack;

        /// <summary>After the end he is gone the moment nobody can see him, and gone regardless after this.</summary>
        private const int Linger = 600;

        /// <summary>The Eye's servants are faded out over the first ticks. They are not in this.</summary>
        private const int ServantFade = 20;

        // ============================================================
        // THE GEOMETRY
        // ============================================================

        /// <summary>Six blocks: where the Eye stops, from the player's centre.</summary>
        public const float Radius = 96f;

        /// <summary>Where it backs off to before it comes in.</summary>
        private const float StartDistance = 416f;

        /// <summary>
        /// The arrow's line passes this far above the player's centre: over the head, close
        /// enough to feel. The Eye stops on that line, so the arrow cannot miss it.
        /// </summary>
        private const float HeadClear = 44f;

        /// <summary>The approach bows upward by this much at its middle, and bobs, less as it closes.</summary>
        private const float Arc = 34f;
        private const float Bob = 6f;
        private const float BobRate = 0.09f;

        /// <summary>How hard it shakes at the end of its approach, in pixels.</summary>
        private const float TrembleMax = 2.5f;

        /// <summary>In the beat it draws back this far, the way something does before it bites.</summary>
        private const float PullBack = 8f;

        /// <summary>How much of the dash it was in carries into its backing off, and the most that can.</summary>
        private const float Carry = 0.3f;
        private const float CarryMax = 160f;

        // ============================================================
        // WHERE HE STANDS
        // ============================================================

        /// <summary>Tiles out from the player he is looked for, on the far side from the Eye, and how far either way of it.</summary>
        private const int Distance = 84;
        private const int Spread = 16;

        /// <summary>Rows up and down from the player's feet searched for ground in each column.</summary>
        private const int Rise = 30;

        /// <summary>Where the arrow leaves him, above his feet.</summary>
        private const float BowHeight = 27f;

        /// <summary>A player further than this from the one on one is not in the scene: their camera is their own.</summary>
        private const float SceneReach = 3000f;

        // ============================================================
        // THE ARROW
        // ============================================================

        /// <summary>Pixels a tick. Fast enough to be a streak; slow enough to be seen coming the last half second.</summary>
        private const float ArrowSpeed = 54f;

        /// <summary>Ichor, lit.</summary>
        private static readonly Color IchorGold = new Color(255, 206, 90);

        // ============================================================
        // THE CAMERA
        // ============================================================

        /// <summary>
        /// The zoom the approach closes to: this, or the player's own plus the lift if
        /// that is further in. Never past the game's own limit.
        /// </summary>
        private const float ZoomClose = 1.6f;
        private const float ZoomLift = 0.4f;

        /// <summary>The kick in on the hit, and the slow push in while the camera stays on it.</summary>
        private const float ZoomPunch = 0.12f;
        private const float ZoomCreep = 0.04f;

        // ============================================================
        // THE LIGHT
        // ============================================================

        /// <summary>The dark coming in with the Eye: how dark, and the pool around the two of them, wide to close.</summary>
        private const float ApproachShade = 0.45f;
        private const float ApproachWide = 720f;
        private const float ApproachClose = 300f;

        /// <summary>After the hit the dark lifts a little: the flash has only just gone.</summary>
        private const float KillShade = 0.25f;
        private const float KillPool = 380f;

        /// <summary>
        /// Him. The rest of the world goes most of the way to black and he is in the
        /// one place it has not, lit cold. The book will not let him glow; this is the
        /// world going dark around him instead.
        /// </summary>
        private const float RevealShade = 0.62f;
        private const float RevealPool = 260f;
        private const float RevealPoolEnd = 230f;

        /// <summary>The cold light on him, as a share of full, per channel. Enough to show the ground he is on.</summary>
        private static readonly Vector3 Moonlit = new Vector3(0.22f, 0.23f, 0.3f);

        // ============================================================
        // THE WEATHER
        // ============================================================

        /// <summary>
        /// The gale. Past anything vanilla blows (0.85 is its most): it only has to last
        /// a few seconds, and it has to be seen. It blows from the player toward him, so
        /// his scarf streams out behind him, away from them.
        /// </summary>
        private const float Gale = 1f;

        /// <summary>How fast the wind rises to it after the arrow, per tick.</summary>
        private const float GaleStep = 0.03f;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        // ============================================================
        // STATE, READ ONCE A TICK
        // ============================================================

        private static uint readAt = uint.MaxValue;
        private static bool running;
        private static bool sceneRunning;

        /// <summary>Any Eye in its frenzy or held, or any scene before its end.</summary>
        public static bool Running
        {
            get
            {
                Read();
                return running;
            }
        }

        /// <summary>A scene is on, from the player hitting one until the camera is home.</summary>
        public static bool SceneRunning
        {
            get
            {
                Read();
                return sceneRunning;
            }
        }

        /// <summary>Nobody dies while this is going on: EyeFinaleGuard.</summary>
        public static bool Guarding => Running;

        private static void Read()
        {
            if (readAt == Main.GameUpdateCount)
                return;

            readAt = Main.GameUpdateCount;
            running = false;
            sceneRunning = false;

            int actor = ModContent.NPCType<PugreamAtTheEye>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == actor && npc.ModNPC is PugreamAtTheEye him && him.Tick < End)
                {
                    running = true;
                    sceneRunning = true;
                }
                else if (npc.type == NPCID.EyeofCthulhu && npc.TryGetGlobalNPC(out EyeFinaleEye eye)
                    && (eye.Stage == EyeStage.Frenzy || eye.Stage == EyeStage.Held))
                {
                    running = true;
                }
            }
        }

        /// <summary>Is a scene holding this Eye.</summary>
        public static bool HeldBy(int eyeIndex)
        {
            int actor = ModContent.NPCType<PugreamAtTheEye>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == actor && npc.ModNPC is PugreamAtTheEye him && him.EyeIndex == eyeIndex)
                    return true;
            }

            return false;
        }

        /// <summary>This player has been dodging for longer than the scene will wait.</summary>
        public static bool FrenzyOverdue(int player)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == NPCID.EyeofCthulhu && npc.TryGetGlobalNPC(out EyeFinaleEye eye)
                    && eye.Stage == EyeStage.Frenzy && eye.Victim == player && eye.FrenzyTicks >= EyeFinaleEye.FailsafeTicks)
                    return true;
            }

            return false;
        }

        // ============================================================
        // THE START
        // ============================================================

        /// <summary>
        /// The player is on one. The server (or singleplayer) finds him somewhere to stand,
        /// puts him there, and hands him the Eye. If there is no room in the world for
        /// him the frenzy simply goes on and this is asked again next tick.
        /// </summary>
        public static void Begin(NPC eye, EyeFinaleEye state, Player victim)
        {
            // The other side of the player from the Eye, so the arrow comes past them.
            int side = eye.Center.X >= victim.Center.X ? -1 : 1;

            if (!FindSpot(victim, side, out Vector2 feet) && !FindSpot(victim, -side, out feet))
                feet = new Vector2(victim.Center.X + side * Distance * 16f, victim.Bottom.Y);

            int faces = feet.X >= victim.Center.X ? -1 : 1;

            int index = NPC.NewNPC(eye.GetSource_FromAI(), (int)feet.X, (int)feet.Y, ModContent.NPCType<PugreamAtTheEye>(),
                0, eye.whoAmI, victim.whoAmI, 0f, faces);

            if (index < 0 || index >= Main.maxNPCs || Main.npc[index].ModNPC is not PugreamAtTheEye actor)
                return;

            // Set before the first sync, which NewNPC does not send: he arrives on every
            // client already knowing where the Eye was and how it was moving.
            actor.From = eye.Center;
            actor.FromVelocity = eye.velocity;
            actor.NPC.netUpdate = true;

            state.Stage = EyeStage.Held;
            eye.velocity = Vector2.Zero;
            eye.netUpdate = true;
        }

        /// <summary>
        /// Ground about Distance tiles out on this side, near the player's own height,
        /// that a man can stand on. Searched outward from Distance, and each column from
        /// the player's feet outward; the first spot with a clear line from him to just
        /// over the player's head wins, and failing that the first spot at all.
        /// </summary>
        private static bool FindSpot(Player victim, int side, out Vector2 feet)
        {
            feet = Vector2.Zero;

            int px = (int)(victim.Center.X / 16f);
            int py = (int)(victim.Bottom.Y / 16f);
            Vector2 clear = victim.Center + new Vector2(0f, -HeadClear);
            Vector2 first = Vector2.Zero;

            for (int step = 0; step <= Spread; step++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (step == 0 && sign < 0)
                        continue;

                    int x = px + side * (Distance + sign * step);

                    if (x < 50 || x >= Main.maxTilesX - 50)
                        continue;

                    if (!GroundNear(x, py, out int y))
                        continue;

                    Vector2 at = new Vector2(x * 16 + 8, y * 16);

                    if (Open(at, clear))
                    {
                        feet = at;
                        return true;
                    }

                    if (first == Vector2.Zero)
                        first = at;
                }
            }

            feet = first;
            return first != Vector2.Zero;
        }

        /// <summary>The standable ground row in this column nearest to row <paramref name="near"/>.</summary>
        private static bool GroundNear(int x, int near, out int row)
        {
            for (int d = 0; d <= Rise; d++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (d == 0 && sign < 0)
                        continue;

                    int y = near + sign * d;

                    if (Standable(x, y))
                    {
                        row = y;
                        return true;
                    }
                }
            }

            row = 0;
            return false;
        }

        /// <summary>Something under this tile's top to stand on, and room for a man over it, dry.</summary>
        internal static bool Standable(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 10) || !Floor(x, y))
                return false;

            for (int cx = x - 1; cx <= x + 1; cx++)
            {
                for (int cy = y - 3; cy <= y - 1; cy++)
                {
                    Tile tile = Main.tile[cx, cy];

                    if (tile.LiquidAmount > 0 || Wall(tile))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Stood on: solid, or a platform.</summary>
        internal static bool Floor(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        /// <summary>In the way: solid and not a platform.</summary>
        private static bool Wall(Tile tile)
        {
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>Nothing solid on the line from where his bow would be to just over the player's head.</summary>
        private static bool Open(Vector2 feet, Vector2 clear)
        {
            Vector2 bow = feet - new Vector2(0f, BowHeight);
            int samples = (int)(Vector2.Distance(bow, clear) / 16f);

            for (int i = 1; i < samples; i++)
            {
                Vector2 p = Vector2.Lerp(bow, clear, i / (float)samples);
                int tx = (int)(p.X / 16f);
                int ty = (int)(p.Y / 16f);

                if (!WorldGen.InWorld(tx, ty, 1) || Wall(Main.tile[tx, ty]))
                    return false;
            }

            return true;
        }

        // ============================================================
        // THE SCENE
        // ============================================================

        /// <summary>One tick of it, from his AI, on every machine.</summary>
        public static void Run(PugreamAtTheEye actor)
        {
            int t = ++actor.Tick;

            Player victim = VictimOf(actor);
            NPC eye = EyeOf(actor);

            if (Authority)
            {
                // The player went, or the Eye went some other way before the shot. What
                // has been done stays done; the rest does not happen.
                if (victim == null || (eye == null && !actor.Struck))
                {
                    if (actor.Struck)
                        Finish(actor);

                    Remove(actor);
                    return;
                }
            }
            else if (victim == null)
            {
                // The server will say.
                return;
            }

            if (!actor.Struck && eye != null)
                Steer(actor, eye, victim, t);

            if (t <= ServantFade)
                FadeServants(t);

            if (!Main.dedServ)
                Arrow(actor, eye, victim, t);

            if (!actor.Struck && t >= HitAt)
                Strike(actor, eye, victim);

            if (!Main.dedServ)
                Show(actor, eye, victim, t);

            if (Authority)
                Keep(actor, t);
        }

        private static Player VictimOf(PugreamAtTheEye actor)
        {
            int who = actor.VictimIndex;

            if (who < 0 || who >= Main.maxPlayers)
                return null;

            Player player = Main.player[who];
            return player.active && !player.dead ? player : null;
        }

        private static NPC EyeOf(PugreamAtTheEye actor)
        {
            int index = actor.EyeIndex;

            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];
            return npc.active && npc.type == NPCID.EyeofCthulhu ? npc : null;
        }

        /// <summary>Where the arrow leaves him.</summary>
        private static Vector2 Bow(PugreamAtTheEye actor) => actor.NPC.Center + new Vector2(actor.Faces * 8f, -6f);

        /// <summary>
        /// Where the Eye stops: on the arrow's line from his bow through the point just
        /// over the player's head, on the far side of them, Radius from their centre.
        /// Worked out from where the two of them are right now, so if the player is
        /// still falling when the Eye comes in, the Eye comes in to where they land.
        /// </summary>
        internal static Vector2 EndPoint(PugreamAtTheEye actor, Vector2 centre)
        {
            Vector2 clear = centre + new Vector2(0f, -HeadClear);
            Vector2 along = (clear - Bow(actor)).SafeNormalize(new Vector2(actor.Faces, 0f));

            // |clear - centre + along·s| = Radius, beyond the clear point.
            Vector2 w = clear - centre;
            float b = Vector2.Dot(w, along);
            float c = w.LengthSquared() - Radius * Radius;
            float s = -b + (float)Math.Sqrt(Math.Max(0f, b * b - c));

            return clear + along * s;
        }

        // ---- the Eye ----------------------------------------------------------------

        private static void Steer(PugreamAtTheEye actor, NPC eye, Player victim, int t)
        {
            // A client can hear of him a packet before it hears the Eye is held. It is.
            if (eye.TryGetGlobalNPC(out EyeFinaleEye state) && state.Stage == EyeStage.Frenzy)
                state.Stage = EyeStage.Held;

            Vector2 centre = victim.Center;
            Vector2 end = EndPoint(actor, centre);
            Vector2 way = (end - centre) / Radius;
            Vector2 start = centre + way * StartDistance;

            Vector2 at;

            if (t <= Recoil)
            {
                // Off the back of the dash that did it: carried on a little, then pulled
                // round to where it will come in from, and stopped there.
                Vector2 carry = actor.FromVelocity * Recoil * Carry;

                if (carry.Length() > CarryMax)
                    carry = carry.SafeNormalize(Vector2.Zero) * CarryMax;

                at = Hermite(actor.From, carry, start, Vector2.Zero, t / (float)Recoil);
            }
            else if (t <= ApproachEnd)
            {
                float k = Creep((t - Recoil) / (float)Approach);

                // Bowed upward, never down into the ground.
                Vector2 side = new Vector2(-way.Y, way.X);

                if (side.Y > 0f)
                    side = -side;

                at = Vector2.Lerp(start, end, k)
                    + side * (Arc * (float)Math.Sin(MathHelper.Pi * k))
                    + new Vector2(0f, Bob * (float)Math.Sin(t * BobRate) * (1f - k))
                    + Tremble(t, k);
            }
            else
            {
                float b = (t - ApproachEnd) / (float)Beat;
                at = end + way * (PullBack * (float)Math.Sin(MathHelper.Pi * MathHelper.Clamp(b, 0f, 1f))) + Tremble(t, 1f);
            }

            eye.Center = at;
            eye.velocity = Vector2.Zero;
            eye.timeLeft = NPC.activeTime;
            eye.rotation = Utils.AngleLerp(eye.rotation, (centre - at).ToRotation() - MathHelper.PiOver2, 0.25f);

            if (!Main.dedServ && Main.rand.NextBool(5))
            {
                // It is hurt. It drips.
                Dust dust = Dust.NewDustDirect(eye.position, eye.width, eye.height, DustID.Blood, 0f, 1f, 0, default, 1.2f);
                dust.velocity *= 0.3f;
            }
        }

        /// <summary>Two sines that never line up, on each axis, growing as it closes. The same on every machine.</summary>
        private static Vector2 Tremble(int t, float k)
        {
            float size = TrembleMax * k * k;

            return new Vector2(
                (float)(Math.Sin(t * 1.9) + Math.Sin(t * 3.7 + 1.0)) * 0.5f * size,
                (float)(Math.Sin(t * 2.3 + 2.0) + Math.Sin(t * 4.1)) * 0.5f * size);
        }

        /// <summary>Mostly an ease, a little linear, so it never quite stops coming.</summary>
        private static float Creep(float u) => 0.35f * u + 0.65f * SmoothStep(u);

        private static Vector2 Hermite(Vector2 p0, Vector2 m0, Vector2 p1, Vector2 m1, float s)
        {
            float s2 = s * s;
            float s3 = s2 * s;

            return (2f * s3 - 3f * s2 + 1f) * p0 + (s3 - 2f * s2 + s) * m0 + (-2f * s3 + 3f * s2) * p1 + (s3 - s2) * m1;
        }

        private static float SmoothStep(float x)
        {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        private static float SmootherStep(float x)
        {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * x * (x * (x * 6f - 15f) + 10f);
        }

        /// <summary>The Eye's servants are not in this. They thin out and go.</summary>
        private static void FadeServants(int t)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type != NPCID.ServantofCthulhu)
                    continue;

                npc.alpha = Math.Min(255, npc.alpha + 13);
                npc.velocity *= 0.9f;
                npc.damage = 0;
                npc.dontTakeDamage = true;

                if (!Main.dedServ && Main.rand.NextBool(3))
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, 0f, 0f, 100, default, 1f);

                if (Authority && (npc.alpha >= 255 || t >= ServantFade))
                {
                    npc.active = false;

                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
            }
        }

        // ---- the arrow --------------------------------------------------------------

        /// <summary>
        /// Loosed so that it arrives on the tick of the hit, from wherever he is, and
        /// told every tick where the Eye is now, so it cannot miss. A distant string, and
        /// as it passes the player, the air.
        /// </summary>
        private static void Arrow(PugreamAtTheEye actor, NPC eye, Player victim, int t)
        {
            if (actor.Struck)
                return;

            Vector2 target = eye?.Center ?? EndPoint(actor, victim.Center);

            if (actor.Flight != null)
            {
                actor.Flight.Aim(target);

                if (!actor.Whistled && (actor.Flight.Position.X - victim.Center.X) * actor.Faces >= 0f)
                {
                    actor.Whistled = true;
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.8f, Volume = 0.9f }, actor.Flight.Position);
                }

                return;
            }

            if (actor.Fired)
                return;

            Vector2 bow = Bow(actor);
            int flight = Math.Max(6, (int)Math.Ceiling(Vector2.Distance(bow, target) / ArrowSpeed));

            if (t < HitAt - flight)
                return;

            actor.Fired = true;
            actor.Flight = new IchorArrowFlight(bow, target, HitAt - t + 1);
            PADParticleHandler.SpawnParticle(actor.Flight);

            SoundEngine.PlaySound(SoundID.Item5 with { Pitch = -0.25f }, bow);
        }

        /// <summary>The arrow lands. Everywhere it is seen; and where the world is decided, the Eye dies of it.</summary>
        private static void Strike(PugreamAtTheEye actor, NPC eye, Player victim)
        {
            actor.Struck = true;

            Vector2 at = eye?.Center ?? EndPoint(actor, victim.Center);
            actor.KillPoint = at;

            if (!Main.dedServ)
                Impact(actor, at, victim);

            if (!Authority || eye == null || !eye.TryGetGlobalNPC(out EyeFinaleEye state))
                return;

            state.Stage = EyeStage.Dead;
            eye.dontTakeDamage = false;
            eye.StrikeInstantKill();

            // Vanilla has just counted the Eye as beaten. Not yet: Keep holds it off
            // until the camera is home.
            HoldTheFlag();

            actor.NPC.netUpdate = true;
        }

        private static void Impact(PugreamAtTheEye actor, Vector2 at, Player victim)
        {
            Vector2 travel = actor.Flight?.Heading ?? new Vector2(actor.Faces, 0f);

            Cinematic.Flash(IchorGold, 0.32f);

            if (InScene(victim))
                Main.LocalPlayer.PIS().BumpScreenShake(12f, 24);

            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.35f }, at);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.5f }, at);

            for (int i = 0; i < 44; i++)
            {
                Vector2 velocity = travel.RotatedByRandom(0.6) * Main.rand.NextFloat(2f, 11f);
                Dust dust = Dust.NewDustPerfect(at, DustID.Ichor, velocity, 0, default, Main.rand.NextFloat(1.1f, 1.8f));
                dust.noGravity = !Main.rand.NextBool(4);
            }

            // Thrown on along the arrow's way, as the arrow goes through.
            for (int i = 0; i < 16; i++)
            {
                float length = Main.rand.NextFloat(60f, 110f);

                PADParticleHandler.SpawnParticle(new SparkShard(
                    at + Main.rand.NextVector2Circular(6f, 6f),
                    travel.RotatedByRandom(0.75) * Main.rand.NextFloat(6f, 14f),
                    Color.Lerp(IchorGold, Color.White, Main.rand.NextFloat(0.3f)),
                    length * 0.5f,
                    Math.Max(18f, length * 1.08f),
                    length,
                    Main.rand.Next(18, 30),
                    falls: true));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, IchorGold, 1.4f, 28));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White * 0.6f, 0.7f, 16, lights: false));

            BloodDroplet.SpawnBurst(at, travel, 18, 35f);
            BloodDroplet.SpawnBurst(at, -travel, 8, 50f);
        }

        // ---- the flag, the end ------------------------------------------------------

        /// <summary>Between the arrow and the end, the Eye is dead and not yet beaten.</summary>
        private static void HoldTheFlag()
        {
            if (!NPC.downedBoss1)
                return;

            NPC.downedBoss1 = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        private static void Keep(PugreamAtTheEye actor, int t)
        {
            if (actor.Struck && !actor.Finished && t < End)
                HoldTheFlag();

            if (t < End)
                return;

            if (!actor.Finished)
                Finish(actor);

            // He goes the moment nobody can see him go.
            if (!Watched(actor.NPC.Center) || t >= End + Linger)
                Remove(actor);
        }

        /// <summary>The Eye is beaten, now, and he is owed to the shore nearest where he stood.</summary>
        private static void Finish(PugreamAtTheEye actor)
        {
            if (actor.Finished)
                return;

            actor.Finished = true;

            NPC.SetEventFlagCleared(ref NPC.downedBoss1, GameEventClearedID.DefeatedEyeOfCthulu);
            PugreamPresence.SawTheEye(actor.NPC.Center.X);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>Save and quit halfway through: whatever has already happened is kept.</summary>
        internal static void FinishNow()
        {
            if (!Authority)
                return;

            int type = ModContent.NPCType<PugreamAtTheEye>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.type == type && npc.ModNPC is PugreamAtTheEye actor && actor.Struck)
                    Finish(actor);
            }
        }

        private static void Remove(PugreamAtTheEye actor)
        {
            actor.NPC.active = false;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, actor.NPC.whoAmI);
        }

        /// <summary>Inside anybody's view.</summary>
        private static bool Watched(Vector2 where)
        {
            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.dead && Math.Abs(player.Center.X - where.X) <= 1100f && Math.Abs(player.Center.Y - where.Y) <= 700f)
                    return true;
            }

            return false;
        }

        // ============================================================
        // THE SHOW — each machine's own
        // ============================================================

        /// <summary>Is the local player in this scene at all.</summary>
        private static bool InScene(Player victim)
        {
            Player me = Main.LocalPlayer;
            return !me.dead && Vector2.Distance(me.Center, victim.Center) <= SceneReach;
        }

        private static void Show(PugreamAtTheEye actor, NPC eye, Player victim, int t)
        {
            Weather(actor, victim, t);

            if (t >= PanToPugAt)
            {
                actor.Lit = MathHelper.Clamp((t - PanToPugAt) / 60f, 0f, 1f);
                Lighting.AddLight(actor.NPC.Center, Moonlit * actor.Lit);
            }

            if (!InScene(victim) || t >= End)
                return;

            Cinematic.Hold();
            PisPlayer.HideUI();

            if (t <= End - Cinematic.Bars)
                Cinematic.Frame();

            Camera(actor, eye, victim, t);
            Zoom(actor, t);
            Light(actor, eye, victim, t);
        }

        /// <summary>The Eye, or where it would be, for anything that wants to look at it.</summary>
        private static Vector2 EyeAt(PugreamAtTheEye actor, NPC eye, Player victim)
        {
            if (actor.Struck)
                return actor.KillPoint;

            return eye?.Center ?? EndPoint(actor, victim.Center);
        }

        /// <summary>His upper body: what the camera sits on.</summary>
        private static Vector2 HimAt(PugreamAtTheEye actor) => actor.NPC.Bottom + new Vector2(0f, -30f);

        private static float ApproachProgress(int t) => MathHelper.Clamp((t - Recoil) / (float)Approach, 0f, 1f);

        private static void Camera(PugreamAtTheEye actor, NPC eye, Player victim, int t)
        {
            if (t >= PanBackAt)
            {
                // Home, the same walk every other hold in the mod takes, from wherever
                // the camera is. Once.
                if (!actor.PannedHome)
                {
                    actor.PannedHome = true;
                    PisPlayer.PanHome(HimAt(actor), PanBack);
                }

                return;
            }

            Vector2 focus;
            float blend = 1f;

            if (!actor.Struck)
            {
                // To the Eye, then onto the space between it and the player as it closes,
                // so at the end both are in the shot.
                Vector2 eyeAt = EyeAt(actor, eye, victim);
                Vector2 between = Vector2.Lerp(victim.Center, eyeAt, 0.5f);

                focus = Vector2.Lerp(eyeAt, between, SmoothStep(ApproachProgress(t)));
                blend = SmoothStep(t / (float)PanIn);

                actor.KillFocus = focus;
            }
            else if (t < PanToPugAt)
            {
                focus = actor.KillFocus;
            }
            else if (t < HoldAt)
            {
                float p = (t - PanToPugAt) / (float)PanToPug;
                focus = Vector2.Lerp(actor.KillFocus, HimAt(actor), SmootherStep(p));
            }
            else
            {
                focus = HimAt(actor);
            }

            PisPlayer.BlendCamera(focus, blend);
        }

        private static void Zoom(PugreamAtTheEye actor, int t)
        {
            float own = EyeFinaleView.Borrow();
            float close = MathHelper.Clamp(Math.Max(ZoomClose, own + ZoomLift), 1f, 2f);
            float talk = Dialogue.SceneZoom;

            // Where the zoom is when the camera leaves the kill, so the pan starts from it.
            float kept = close + 0.02f + ZoomCreep;

            float zoom;

            if (t < ApproachEnd)
            {
                zoom = MathHelper.Lerp(own, close, Creep(ApproachProgress(t)));
            }
            else if (t < HitAt)
            {
                zoom = close + 0.02f * ((t - ApproachEnd) / (float)Beat);
            }
            else if (t < PanToPugAt)
            {
                float since = t - HitAt;
                zoom = close + 0.02f + ZoomPunch * (float)Math.Exp(-since / 7f) + ZoomCreep * (since / AfterHit);
            }
            else if (t < HoldAt)
            {
                zoom = MathHelper.Lerp(kept, talk, SmoothStep((t - PanToPugAt) / (float)PanToPug));
            }
            else if (t < PanBackAt)
            {
                // What talking to someone looks like in this mod.
                zoom = talk;
            }
            else
            {
                zoom = MathHelper.Lerp(talk, own, SmoothStep((t - PanBackAt) / (float)PanBack));
            }

            EyeFinaleView.Want(zoom);
        }

        private static void Light(PugreamAtTheEye actor, NPC eye, Player victim, int t)
        {
            if (!actor.Struck)
            {
                Vector2 between = Vector2.Lerp(victim.Center, EyeAt(actor, eye, victim), 0.5f);
                float rise = SmoothStep((t - 20) / 60f);
                float pool = MathHelper.Lerp(ApproachWide, ApproachClose, Creep(ApproachProgress(t)));

                Cinematic.Shade(between, pool, ApproachShade * rise);
                return;
            }

            if (t < PanToPugAt)
            {
                Cinematic.Shade(actor.KillFocus, KillPool, KillShade);
            }
            else if (t < HoldAt)
            {
                float p = SmoothStep((t - PanToPugAt) / (float)PanToPug);
                Vector2 focus = Vector2.Lerp(actor.KillFocus, HimAt(actor), SmootherStep((t - PanToPugAt) / (float)PanToPug));

                Cinematic.Shade(focus, MathHelper.Lerp(KillPool + 40f, RevealPool, p), MathHelper.Lerp(KillShade + 0.05f, RevealShade, p));
            }
            else if (t < PanBackAt)
            {
                float h = SmoothStep((t - HoldAt) / (float)HoldPug);
                Cinematic.Shade(HimAt(actor), MathHelper.Lerp(RevealPool, RevealPoolEnd, h), RevealShade);
            }

            // From the walk home on, it is not asked for, and eases away by itself.
        }

        /// <summary>
        /// The gale, from the arrow to the end, rising over a second. Around him it is
        /// seen as well as felt: streaks of it across the shot, leaves, dust off the
        /// ground at his feet.
        /// </summary>
        private static void Weather(PugreamAtTheEye actor, Player victim, int t)
        {
            if (!actor.Struck || t >= End)
                return;

            float gale = Gale * -actor.Faces;
            float wind = Main.windSpeedCurrent;

            wind = wind < gale ? Math.Min(gale, wind + GaleStep) : Math.Max(gale, wind - GaleStep);
            EyeFinaleView.HoldWind(wind);

            if (t < PanToPugAt || t >= PanBackAt + 40 || !InScene(victim))
                return;

            float zoom = Math.Max(1f, Main.GameViewMatrix.Zoom.X);
            Vector2 half = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f / zoom;
            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            int downwind = Math.Sign(gale);

            // In the air over the ground he stands on, not across the ground itself.
            float top = middle.Y - half.Y;
            float floor = Math.Max(top + 32f, actor.NPC.Bottom.Y);

            for (int i = 0; i < 2; i++)
            {
                if (i > 0 && Main.rand.NextBool())
                    break;

                float speed = Main.rand.NextFloat(18f, 28f);
                Vector2 from = new Vector2(middle.X - downwind * (half.X + 60f), Main.rand.NextFloat(top, floor));
                int life = (int)((half.X * 2f + 160f) / speed);

                PADParticleHandler.SpawnParticle(new WindStreak(from, new Vector2(downwind * speed, Main.rand.NextFloat(-0.6f, 0.6f)),
                    Main.rand.NextFloat(40f, 120f), Main.rand.NextFloat(1.5f, 3f), life));
            }

            if (t % 3 == 0)
            {
                Vector2 from = new Vector2(middle.X - downwind * (half.X + 20f), Main.rand.NextFloat(top, floor));
                Vector2 velocity = new Vector2(downwind * Main.rand.NextFloat(6f, 12f), Main.rand.NextFloat(-2f, 1f));

                Gore.NewGore(actor.NPC.GetSource_FromAI(), from, velocity, GoreID.TreeLeaf_Normal, Main.rand.NextFloat(0.8f, 1.1f));
            }

            if (t % 2 == 0)
            {
                Vector2 feet = actor.NPC.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -2f);
                Dust dust = Dust.NewDustPerfect(feet, DustID.Smoke, new Vector2(downwind * Main.rand.NextFloat(3f, 6f), Main.rand.NextFloat(-1f, 0f)),
                    150, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// What the scene borrows and has to give back: the zoom and the wind. Both are
    /// kept alive by being asked for every tick and given back the tick after they stop
    /// being asked for, so a scene cut short cannot keep either.
    ///
    /// GameZoomTarget is a saved setting, as it is to BossIntro and Dialogue, and gets
    /// the same care around every save. The wind is the world's: its target is put back
    /// and its current left to settle there the way vanilla settles it, slowly, so the
    /// gale dies down rather than stopping.
    /// </summary>
    public class EyeFinaleView : ModSystem
    {
        private static bool holdingZoom;
        private static float savedZoom = 1f;
        private static int zoomLeft;

        private static bool holdingWind;
        private static float windTargetBefore;
        private static int windLeft;

        internal static bool HoldingZoom => holdingZoom;
        internal static float SavedZoom => savedZoom;

        /// <summary>
        /// Takes the zoom for this tick if it is not already taken, and returns the
        /// player's own. Anything else holding it lets go first, so what is saved is
        /// the player's and not somebody's borrowed number.
        /// </summary>
        internal static float Borrow()
        {
            if (!holdingZoom)
            {
                if (BossIntro.HoldingZoom)
                    BossIntro.Clear();

                if (Dialogue.HoldingZoom)
                    Dialogue.ReleaseZoom();

                savedZoom = Main.GameZoomTarget;
                holdingZoom = true;
            }

            zoomLeft = 2;
            return savedZoom;
        }

        internal static void Want(float zoom)
        {
            Borrow();
            Main.GameZoomTarget = MathHelper.Clamp(zoom, 1f, 2f);
        }

        internal static void HoldWind(float wind)
        {
            if (!holdingWind)
            {
                windTargetBefore = Main.windSpeedTarget;
                holdingWind = true;
            }

            Main.windSpeedCurrent = wind;
            Main.windSpeedTarget = wind;
            windLeft = 2;
        }

        private static void ReleaseZoom()
        {
            if (!holdingZoom)
                return;

            Main.GameZoomTarget = savedZoom;
            holdingZoom = false;
        }

        private static void ReleaseWind()
        {
            if (!holdingWind)
                return;

            Main.windSpeedTarget = windTargetBefore;
            holdingWind = false;
        }

        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.SaveSettings += RestoreAroundSave;
        }

        private static bool RestoreAroundSave(On_Main.orig_SaveSettings orig)
        {
            bool holding = holdingZoom;
            float borrowed = Main.GameZoomTarget;

            if (holding)
                Main.GameZoomTarget = savedZoom;

            bool result = orig();

            if (holding)
                Main.GameZoomTarget = borrowed;

            return result;
        }

        /// <summary>After every NPC has had its say this tick: whatever was not asked for again goes back.</summary>
        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            if (holdingZoom && --zoomLeft <= 0)
                ReleaseZoom();

            if (holdingWind && --windLeft <= 0)
                ReleaseWind();
        }

        public override void PreSaveAndQuit()
        {
            EyeFinale.FinishNow();
            ReleaseZoom();
            ReleaseWind();
        }

        public override void OnWorldUnload()
        {
            ReleaseZoom();
            ReleaseWind();
        }
    }

    /// <summary>
    /// Nobody dies while this is going on. The frenzy takes the victim down to one and
    /// no further; nothing else takes anybody past one either; at one, nothing lands at
    /// all; and if something kills them anyway, they are not dead.
    ///
    /// Also the failsafe on the frenzy: a player who has kept it from landing for long
    /// enough is put on one by their own machine, which tells the server.
    /// </summary>
    public class EyeFinaleGuard : ModPlayer
    {
        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
        {
            return EyeFinale.Guarding && Player.statLife <= 1;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers)
        {
            if (!EyeFinale.Guarding)
                return;

            int most = Math.Max(1, Player.statLife - 1);

            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) =>
            {
                if (info.Damage > most)
                    info.Damage = most;
            };
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust,
            ref PlayerDeathReason damageSource)
        {
            if (!EyeFinale.Guarding)
                return true;

            Player.statLife = 1;
            return false;
        }

        /// <summary>
        /// The failsafe, on the player's own machine; for the Everclear Mirror's copy (the frenzy's
        /// victim if it was the one the Eye was after), on its owner's, as if it were the one at the
        /// keyboard (MimicSystem.AlsoOnCopy).
        /// </summary>
        public override void PostUpdate()
        {
            if (Player.whoAmI == Main.myPlayer)
                Failsafe();
            else if (MimicSystem.Is(Player))
                MimicSystem.AlsoOnCopy(Failsafe);
        }

        /// <summary>Dodging the frenzy for longer than the scene will wait: on one.</summary>
        private void Failsafe()
        {
            if (Player.statLife <= 1 || !EyeFinale.FrenzyOverdue(Player.whoAmI))
                return;

            Player.statLife = 1;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerLifeMana, -1, -1, null, Player.whoAmI);
        }
    }
}
