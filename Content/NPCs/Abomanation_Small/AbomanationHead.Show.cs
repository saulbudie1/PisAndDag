using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// Everything about the animal that is only seen or heard: the ground over it, the
    /// jaws, its voice, the blood, the steam, and how the body is held and shaken. None of
    /// it decides anything, and none of it is sent: every machine shows its own from the
    /// state it was sent, and a server shows nothing.
    ///
    /// Four things are shown from here.
    ///
    /// THE GROUND. While the head is under it, the ground over the head throws its own
    /// dust up (<see cref="Tremor"/>), and where the head or any part goes through the
    /// surface, either way, it bursts (<see cref="Surface"/>, <see cref="ShowPart"/>).
    ///
    /// THE JAWS. Whenever the mouth goes from wide to shut (<see cref="Snapped"/>, read
    /// off the very frame that is drawn) the mandibles throw sparks and blood.
    ///
    /// ITS VOICE. A sound belongs to a phase begun, and is played when this machine sees
    /// the phase change (<see cref="Notice"/>), whether it changed it itself or was told.
    ///
    /// THE BODY. Each part asks the head, every tick and every frame, what it should be
    /// doing: bleeding, steaming (<see cref="ShowPart"/>), shaking (<see cref="Shiver"/>),
    /// swelling (<see cref="Swell"/>), glowing (<see cref="Glow"/>), and how its arms are
    /// held (<see cref="ArmsOf"/>).
    /// </summary>
    public partial class AbomanationHead
    {
        // ------------------------------------------------------------- what is kept ----
        //
        // All of it this machine's own. Nothing here is synced.

        /// <summary>The phase this machine last saw. The first tick, none.</summary>
        private int seenPhase = int.MinValue;

        /// <summary>Whether the spasm now on has been announced here, and how many ticks of it this machine has seen.</summary>
        private bool spasmSeen;
        private int spasmAge;

        /// <summary>The mouth's frame last tick, for the snap.</summary>
        private int lastFace = -1;

        /// <summary>When the held-wide face runs out, the mouth shuts for this long. 0: it just closes.</summary>
        private int snapAfter;

        /// <summary>Whether the head was under the ground last tick, once that is known.</summary>
        private bool surfaceKnown;
        private bool wasUnder;

        /// <summary>The same for each part: slot 0 to 14 the body, 15 the tail.</summary>
        private readonly bool[] partKnown = new bool[Abomanation.BodySegments + 1];
        private readonly bool[] partUnder = new bool[Abomanation.BodySegments + 1];

        /// <summary>Lying down: whether each part's middle was still under the ash last tick.</summary>
        private readonly bool[] partSunk = new bool[Abomanation.BodySegments + 1];

        /// <summary>The last column whose tiles were heaved, so each is heaved once as the head passes.</summary>
        private int bulgeColumn = int.MinValue;

        /// <summary>Ticks until it may whimper again.</summary>
        private int whimperWait;

        /// <summary>Whether this whip's crack has been shown.</summary>
        private bool cracked;

        // ------------------------------------------------------------------ noticing ----

        /// <summary>
        /// Sees what has changed since this machine last looked: a spasm begun, a phase
        /// begun. Runs every tick, before anything moves.
        /// </summary>
        private void Notice()
        {
            if (whimperWait > 0)
                whimperWait--;

            if (InSpasm)
            {
                if (!spasmSeen)
                {
                    spasmSeen = true;
                    spasmAge = 0;
                    SpasmBegins();
                }

                spasmAge++;
            }
            else
            {
                spasmSeen = false;
                spasmAge = 0;
            }

            if (seenPhase == int.MinValue)
            {
                seenPhase = phase;
                return;
            }

            if (phase == seenPhase)
                return;

            int was = seenPhase;

            seenPhase = phase;
            Entered(was);
        }

        /// <summary>A phase has begun here: what it sounds like, and what the face does.</summary>
        private void Entered(int was)
        {
            switch (phase)
            {
                case 2:
                    // The rush's tell. Its own voice now, not the borrowed roar; from where
                    // the one it is after stands, as the roar was, or eight hundred pixels
                    // of ground would swallow it.
                    Bark(NPC.target >= 0 && NPC.target < Main.maxPlayers ? Main.player[NPC.target].Center : NPC.Center);
                    break;

                case 3:
                    // The top of a rush: the jaws shut on whatever was there.
                    if (was == 2 && chomp <= 0)
                        chomp = Abomanation.ChompTicks;
                    break;

                case 5:
                    // The whip's tell: the star and the note, and its call, with the mouth
                    // wide for the whole of the coil and shut as the tail lets go.
                    CCBypass.Warn(NPC);
                    Gape(Abomanation.WhipLoopTicks + Abomanation.WhipHold, 12);
                    cracked = false;

                    if (!Main.dedServ)
                        SoundEngine.PlaySound(Abomanation.WhipCrySound, NPC.Center);
                    break;

                case 6:
                    // The throw. The crack itself comes later, from the tail.
                    SoundEngine.PlaySound(SoundID.Item152, NPC.Center);
                    break;

                case PukeChoke:
                    if (!Main.dedServ)
                        SoundEngine.PlaySound(Abomanation.ChokeSound, NPC.Center);
                    break;

                case LayCrawl:
                    // Out onto the hill, and coming: the ash breaks over it where it
                    // comes up. It rises from just under, so the surface never sees the
                    // head go through it (Surface); this is that, said outright.
                    Bark(NPC.Center);
                    Break(true, true);
                    break;

                case LayAct:
                    if (layKind == 1 && !Main.dedServ)
                        SoundEngine.PlaySound(Abomanation.VentSound, NPC.Center);
                    break;
            }
        }

        private void Bark(Vector2 at)
        {
            if (!Main.dedServ)
                SoundEngine.PlaySound(Abomanation.BarkSound, at);
        }

        /// <summary>The mouth held wide this long, then shut this long: which is a snap, with everything a snap throws.</summary>
        private void Gape(int wide, int shut)
        {
            roarFace = Math.Max(roarFace, wide);
            snapAfter = shut;
        }

        /// <summary>A spasm starts: it cries out, and its jaws shut on the end of the cry.</summary>
        private void SpasmBegins()
        {
            Gape(Abomanation.SpasmFace, Abomanation.SpasmSnap);

            if (!Main.dedServ)
                SoundEngine.PlaySound(Abomanation.YelpSound, NPC.Center);
        }

        /// <summary>
        /// Hit. Now and then, and not soon after the last time, it whimpers. Called by
        /// every part's HitEffect, which runs on every machine.
        /// </summary>
        public void Flinch(Vector2 at)
        {
            if (Main.dedServ || Dying || Arriving || whimperWait > 0 || roarFace > 0)
                return;

            if (Main.rand.NextFloat() >= Abomanation.WhimperChance)
                return;

            whimperWait = Main.rand.Next(Abomanation.WhimperGapLeast, Abomanation.WhimperGapMost + 1);
            SoundEngine.PlaySound(Abomanation.WhimperSound, at);
        }

        // ------------------------------------------------------------------ the jaws ----

        /// <summary>
        /// Called with the mouth's frame, every tick, from FindFrame: so it is the frame
        /// that is drawn. Wide to shut is a bite, whatever shut it.
        /// </summary>
        private void Snapped(int face)
        {
            if (lastFace == Abomanation.MouthWide && face == Abomanation.MouthShut)
                Snap();

            lastFace = face;
        }

        /// <summary>The way the head faces, as a unit vector: toward its mouth.</summary>
        private Vector2 Forward => (NPC.rotation - MathHelper.PiOver2).ToRotationVector2();

        /// <summary>
        /// The bite: sparks off the two mandibles where they meet, out to either side and
        /// forward; blood out of the mouth the same way, heavier and slower; a flash; and
        /// the clack.
        /// </summary>
        private void Snap()
        {
            if (Main.dedServ || NPC.alpha > 0)
                return;

            Vector2 way = Forward;
            Vector2 across = new Vector2(-way.Y, way.X);
            Vector2 at = NPC.Center + way * (Abomanation.JawReach * NPC.scale);

            SoundEngine.PlaySound(Abomanation.SnapSound, at);

            for (int i = 0; i < Abomanation.SnapSparks; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;

                Vector2 out_ = (way * Main.rand.NextFloat(0.15f, 1f) + across * side * Main.rand.NextFloat(0.5f, 1.2f))
                    .SafeNormalize(way);

                PADParticleHandler.SpawnParticle(new SparkShard(
                    at + across * side * 5f,
                    out_ * Main.rand.NextFloat(5f, 11f) + NPC.velocity * 0.5f,
                    i % 3 == 0 ? Abomanation.Bone : new Color(255, 206, 140),
                    Main.rand.NextFloat(8f, 12f), 9f, Main.rand.NextFloat(22f, 34f),
                    Main.rand.Next(9, 15), falls: true));
            }

            for (int i = 0; i < Abomanation.SnapBlood; i++)
            {
                float side = Main.rand.NextBool() ? 1f : -1f;

                Vector2 out_ = way * Main.rand.NextFloat(0.1f, 1f) + across * side * Main.rand.NextFloat(0.3f, 1.1f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    at + across * side * Main.rand.NextFloat(6f),
                    out_ * Main.rand.NextFloat(2.5f, 7f) + NPC.velocity * 0.4f,
                    Main.rand.Next(26, 44), Main.rand.NextFloat(0.5f, 1f), Abomanation.Blood));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, NPC.velocity * 0.3f,
                new Color(255, 196, 150) * 0.6f, 0.2f, 8));
        }

        /// <summary>Blood let go from the mouth: this many drops, falling, carried a little by how the head is moving.</summary>
        private void Drool(int drops)
        {
            Vector2 way = Forward;
            Vector2 across = new Vector2(-way.Y, way.X);

            for (int i = 0; i < drops; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    NPC.Center + way * Abomanation.ThroatReach + across * Main.rand.NextFloat(-9f, 9f),
                    NPC.velocity * 0.35f + new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), Main.rand.NextFloat(0.2f, 1.4f)),
                    Main.rand.Next(28, 46), Main.rand.NextFloat(0.55f, 1f), Abomanation.Blood));
            }
        }

        // ----------------------------------------------------------- every tick, seen ----

        /// <summary>The head's own share of what is shown, a tick of it. After it has moved.</summary>
        private void Show()
        {
            if (Main.dedServ || NPC.alpha > 0)
                return;

            bool under = Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

            Surface(under);

            if (under)
                Tremor();

            switch (phase)
            {
                case 2:
                    // Up at you with its mouth open: what is in it streams back.
                    if (!under && attackTimer % 2 == 0)
                        Drool(1);
                    break;

                case 6:
                    if (!cracked && attackTimer >= Abomanation.WhipCrackAt)
                    {
                        cracked = true;
                        CrackShow();
                    }
                    break;

                case PukeSeek:
                    if (stage >= 3 && !under && attackTimer % 3 == 0)
                        Drool(1);
                    break;

                case PukeChoke:
                    // On each catch a little comes up; between them it only runs.
                    if (Gag(attackTimer))
                        BloodDroplet.SpawnBurst(NPC.Center + Forward * Abomanation.ThroatReach, Forward, 7, 34f);
                    else if (attackTimer % 3 == 0)
                        Drool(1);
                    break;

                case PukeSpew:
                    if (attackTimer % 2 == 0)
                        Drool(1);
                    break;

                case LayAct:
                    if (layKind == 1)
                        VentShow();
                    break;
            }

            // The second half: it is never not bleeding.
            if (Enraged && !under && phase != PukeSpew && Main.rand.NextBool(Abomanation.RageDrip / 2))
                Drool(1);
        }

        /// <summary>One heave, as it looks and sounds: a jet out of the mouth along its face, a slower spill round it, and the retch.</summary>
        private void HeaveShow(int index)
        {
            if (Main.dedServ)
                return;

            Vector2 way = facing.ToRotationVector2();
            Vector2 mouth = NPC.Center + way * Abomanation.ThroatReach;

            SoundEngine.PlaySound(Abomanation.HeaveSound, mouth);

            BloodDroplet.SpawnBurst(mouth, way, 26, 24f);
            BloodDroplet.SpawnBurst(mouth, way + new Vector2(0f, 0.5f), 12, 60f);

            for (int i = 0; i < 4; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodFume(
                    mouth + way * Main.rand.NextFloat(4f, 16f),
                    way * Main.rand.NextFloat(1.5f, 4f) + new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.8f)),
                    Main.rand.NextFloat(0.16f, 0.28f), Main.rand.Next(22, 36)));
            }

            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustPerfect(mouth, DustID.Bone,
                    way.RotatedByRandom(0.5) * Main.rand.NextFloat(3f, 8f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));

                dust.noGravity = false;
            }

            Rattle(3f + index, 8, 900f);
        }

        /// <summary>The whip's crack, at the tail: the blood it has on it flung off along its swing, sparks, a flash.</summary>
        private void CrackShow()
        {
            NPC tail = AbomanationChain.PartAt(NPC.whoAmI, Abomanation.BodySegments);

            if (tail == null || tail.alpha > 0)
                return;

            Vector2 motion = tail.Center - (tail.oldPosition + tail.Size * 0.5f);
            Vector2 way = motion.SafeNormalize(Vector2.UnitY);
            float speed = MathHelper.Clamp(motion.Length(), 6f, 40f);

            for (int i = 0; i < Abomanation.CrackBlood; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    tail.Center + Main.rand.NextVector2Circular(14f, 14f),
                    way.RotatedByRandom(0.7) * speed * Main.rand.NextFloat(0.2f, 0.6f),
                    Main.rand.Next(30, 52), Main.rand.NextFloat(0.6f, 1.2f), Abomanation.Blood));
            }

            SparkShard.Spray(tail.Center, new Color(255, 120, 90), Abomanation.CrackSparks, 9f, 46f, 14,
                falls: true, palette: new[] { Abomanation.Bone, new Color(255, 120, 90), new Color(200, 30, 30) });

            PADParticleHandler.SpawnParticle(new BloomGeneric(tail.Center, Vector2.Zero, new Color(255, 150, 120) * 0.7f, 0.4f, 10));

            Rattle(5f, 10, 1100f);
        }

        /// <summary>Shakes this machine's own screen, if its player is within reach of the head.</summary>
        private void Rattle(float power, int ticks, float reach)
        {
            Player me = Main.LocalPlayer;

            if (me != null && me.active && !me.dead && me.Distance(NPC.Center) < reach)
                me.PIS().BumpScreenShake(power, ticks);
        }

        // ------------------------------------------------------------ the ground ------

        /// <summary>Whether a tile is ground: there, and solid all the way through.</summary>
        private static bool Solid(int x, int y) => WorldGen.InWorld(x, y, 2) && WorldGen.SolidTile(x, y);

        /// <summary>
        /// The top tile of the ground over a point that is in the ground: up out of
        /// whatever the point is in to the first open air, and the tile under that. -1 if
        /// the point is in the open already, or the air is further up than the tremor
        /// reaches, or is only a crack.
        /// </summary>
        private static int FloorOver(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 12))
                return -1;

            int row = y;

            // A small hollow round the point: up through it to the ground over it.
            for (int i = 0; i < 4 && !Solid(x, row); i++)
                row--;

            if (!Solid(x, row))
                return -1;

            int limit = Math.Max(12, y - Abomanation.TremorDepth);

            while (row > limit && Solid(x, row))
                row--;

            if (Solid(x, row) || Solid(x, row - 1))
                return -1;

            return row + 1;
        }

        /// <summary>The top tile of the ground in a column, within a few rows of a guess: solid with air on it. -1 if none.</summary>
        private static int FloorNear(int x, int guess, int reach)
        {
            for (int d = 0; d <= reach; d++)
            {
                if (Solid(x, guess - d) && !Solid(x, guess - d - 1))
                    return guess - d;

                if (d > 0 && Solid(x, guess + d) && !Solid(x, guess + d - 1))
                    return guess + d;
            }

            return -1;
        }

        /// <summary>One speck of a tile's own dust, thrown up off the top of it.</summary>
        private static void ThrowDust(int x, int y, float power, float drift)
        {
            Tile tile = Main.tile[x, y];

            if (!tile.HasTile)
                return;

            int index = WorldGen.KillTile_MakeTileDust(x, y, tile);

            // 6000 is the "no dust for this tile" answer, and one past the end of the array.
            if (index < 0 || index >= Main.maxDust)
                return;

            Dust dust = Main.dust[index];

            dust.position = new Vector2(x * 16f + Main.rand.NextFloat(16f), y * 16f - 3f);
            dust.velocity = new Vector2(Main.rand.NextFloat(-0.9f, 0.9f) + drift, -Main.rand.NextFloat(1.6f, 3.4f) * power);
            dust.noGravity = false;
            dust.scale = Main.rand.NextFloat(0.9f, 1.35f) * MathHelper.Clamp(0.7f + 0.3f * power, 0.8f, 1.6f);
        }

        /// <summary>How much the phase it is in stirs the ground: the chase a little, an attack on its way up a lot.</summary>
        private float TremorPush()
        {
            switch (phase)
            {
                case 0: return InSpasm ? 0.6f : 1f;
                case 1: return 1.1f;
                case 2: return 2.4f;
                case 4: return 1.3f;
                case PukeSeek: return stage >= 3 ? 2.6f : stage == 2 ? 2f : 1.4f;
                case LayOut: return stage >= 2 ? 2f : 1.5f;
                default: return 1.2f;
            }
        }

        /// <summary>
        /// The ground over the head, while the head is under it: where it is, for anyone
        /// standing on that ground to see.
        ///
        /// The top of the ground straight up from the head is found, and in a band of
        /// columns either side of it the top tile throws its own dust (ash where it is
        /// ash, sand where it is sand): most from the middle column, less to the sides,
        /// less the deeper the head is, more the faster it goes and the more it means
        /// to. Nearly out, the tiles themselves heave as it passes under them, each once.
        /// On its way up at this machine's own player, the screen takes it too.
        ///
        /// The idea is Calamity Fables' Desert Scourge's; this is written for this animal.
        /// </summary>
        private void Tremor()
        {
            int headX = (int)(NPC.Center.X / 16f);
            int headY = (int)(NPC.Center.Y / 16f);
            int floor = FloorOver(headX, headY);

            if (floor < 0)
                return;

            float depth = Math.Max(0f, NPC.Center.Y - floor * 16f);
            float near = 1f - MathHelper.Clamp(depth / (Abomanation.TremorDepth * 16f), 0f, 1f);
            float push = TremorPush();
            float pace = MathHelper.Clamp(NPC.velocity.Length() / Abomanation.Speed, 0.35f, 2.2f);
            float drift = NPC.velocity.X * 0.1f;

            int half = Abomanation.TremorHalf;

            for (int dx = -half; dx <= half; dx++)
            {
                float edge = 1f - dx * dx / (float)((half + 1) * (half + 1));
                float chance = Abomanation.TremorChance * edge * (0.2f + 0.8f * near * near) * push * pace;

                if (Main.rand.NextFloat() >= chance)
                    continue;

                int y = FloorNear(headX + dx, floor, 3);

                if (y >= 0)
                    ThrowDust(headX + dx, y, (0.5f + near) * push, drift);
            }

            // Now and then a puff of the finer stuff, which hangs.
            if (Main.rand.NextFloat() < 0.05f * near * push * pace)
            {
                PADParticleHandler.SpawnParticle(new DustFog(
                    new Vector2(NPC.Center.X + Main.rand.NextFloat(-40f, 40f), floor * 16f - Main.rand.NextFloat(2f, 8f)),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f) + drift, -Main.rand.NextFloat(0.8f, 2.2f) * Math.Min(push, 2f)),
                    GroundWave.FogColour(headX, floor),
                    Main.rand.Next(50, 90), Main.rand.NextFloat(0.35f, 0.6f), 0.3f));
            }

            // Nearly out: the ground itself lifts over it as it goes by.
            if (depth < Abomanation.TremorBulge && headX != bulgeColumn)
            {
                bulgeColumn = headX;

                float lift = MathHelper.Lerp(7f, 2f, depth / Abomanation.TremorBulge) * Math.Min(1.6f, push);

                PADParticleHandler.SpawnParticle(new LiftedTile(new Point(headX, floor), 0, 14, lift,
                    NPC.velocity.X >= 0f ? 1 : -1, GroundWave.FogColour(headX, floor)));
            }

            // Coming up, and under this machine's own player: they feel it.
            if (push >= 2f && depth < 560f)
            {
                Player me = Main.LocalPlayer;

                if (me != null && me.active && !me.dead && Math.Abs(me.Center.X - NPC.Center.X) < 260f && NPC.Center.Y > me.Center.Y)
                    me.PIS().BumpScreenShake(1.2f + 2.6f * near, 4);
            }
        }

        /// <summary>
        /// The head going through the surface, either way. An attack breaks it open: the
        /// ground heaves out from the place in a wave, its dust goes up in a fan the way
        /// the head is going, and the screen takes it. The chase only kicks some up.
        /// </summary>
        private void Surface(bool under)
        {
            if (!surfaceKnown)
            {
                surfaceKnown = true;
                wasUnder = under;
                return;
            }

            if (under == wasUnder)
                return;

            wasUnder = under;

            if (Dying)
                return;

            bool hard = phase == 2 || phase == 3 || phase == Sinking
                || (phase == PukeSeek && stage >= 2);

            Break(hard, under);
        }

        /// <summary>
        /// The ground breaking over the head. The ground's own dust in a fan the way the
        /// head is going, and the finer stuff that hangs. Hard, the ground heaves out from
        /// the place in a wave and the screen takes it; less of both if it is gentle.
        /// </summary>
        private void Break(bool hard, bool gentle)
        {
            if (Main.dedServ || NPC.alpha > 0)
                return;

            int x = (int)(NPC.Center.X / 16f);
            int y = FloorNear(x, (int)(NPC.Center.Y / 16f), 4);

            if (y < 0)
                return;

            Vector2 way = NPC.velocity.SafeNormalize(-Vector2.UnitY);
            float pace = MathHelper.Clamp(NPC.velocity.Length() / Abomanation.Speed, 0.6f, 2.4f);
            int specks = hard ? 20 : 8;

            for (int i = 0; i < specks; i++)
            {
                int column = x + Main.rand.Next(-2, 3);
                int row = FloorNear(column, y, 2);

                if (row < 0)
                    continue;

                Tile tile = Main.tile[column, row];

                if (!tile.HasTile)
                    continue;

                int index = WorldGen.KillTile_MakeTileDust(column, row, tile);

                if (index < 0 || index >= Main.maxDust)
                    continue;

                // Up and out, leaning the way the head went through.
                Vector2 fling = new Vector2(way.X * 2.2f + Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(2.5f, 6.5f)) * pace;

                Dust dust = Main.dust[index];

                dust.position = new Vector2(column * 16f + Main.rand.NextFloat(16f), row * 16f - 3f);
                dust.velocity = fling;
                dust.noGravity = false;
                dust.scale = Main.rand.NextFloat(1f, 1.6f);
            }

            Color fog = GroundWave.FogColour(x, y);

            for (int i = 0; i < (hard ? 7 : 2); i++)
            {
                PADParticleHandler.SpawnParticle(new DustFog(
                    new Vector2(NPC.Center.X + Main.rand.NextFloat(-28f, 28f), y * 16f - Main.rand.NextFloat(0f, 10f)),
                    new Vector2(way.X * 1.2f + Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(0.6f, 2.6f)),
                    fog, Main.rand.Next(70, 120), Main.rand.NextFloat(0.5f, 0.9f), Main.rand.NextFloat(0.3f, 0.42f)));
            }

            if (!hard)
                return;

            GroundWave.Slam(new Vector2(NPC.Center.X, y * 16f), NPC.width * 0.5f, 6, gentle ? 8f : 11f, 22);

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.7f, PitchVariance = 0.1f, Volume = 0.45f, MaxInstances = 2 }, NPC.Center);
            Rattle(gentle ? 4f : 7f, 10, 1000f);
        }

        // ------------------------------------------------------------------ the parts ----

        /// <summary>How far into a spasm it is, 0 to 1: in over SpasmEase, out over the same.</summary>
        private float SpasmGrip
        {
            get
            {
                if (!InSpasm)
                    return 0f;

                return Math.Min(1f, spasmAge / (float)Abomanation.SpasmEase)
                    * Math.Min(1f, spasm / (float)Abomanation.SpasmEase);
            }
        }

        /// <summary>How hot it is, 0 to 1: rising through the vent's swelling, full while the steam goes, going after.</summary>
        public float Heat
        {
            get
            {
                if (!AtVent)
                    return 0f;

                if (attackTimer <= Abomanation.VentTell)
                {
                    float t = attackTimer / (float)Abomanation.VentTell;
                    return t * t;
                }

                int after = attackTimer - Abomanation.VentTell - Abomanation.VentBlast;

                return after <= 0 ? 1f : MathHelper.Clamp(1f - after / (float)Abomanation.VentAfter, 0f, 1f);
            }
        }

        /// <summary>How hard the whole animal is shaking, in pixels.</summary>
        private float Shake
        {
            get
            {
                if (InSpasm)
                    return Abomanation.SpasmShake * SpasmGrip;

                if (phase == PukeChoke)
                {
                    foreach (int gag in Abomanation.ChokeGag)
                    {
                        if (attackTimer >= gag && attackTimer < gag + 6)
                            return 3.2f;
                    }

                    return 1.2f;
                }

                if (phase == PukeSpew)
                    return 1.1f;

                if (AtVent)
                    return attackTimer <= Abomanation.VentTell ? 3f * Heat : Venting ? 2.2f : 0.8f;

                return 0f;
            }
        }

        /// <summary>
        /// Where a part (or the head) is drawn, off where it is: thrown side to side across
        /// its own length. Drawn only; nothing moves, so nothing it touches changes.
        /// </summary>
        public Vector2 Shiver(NPC part)
        {
            float amount = Shake;

            if (amount <= 0f || Dying)
                return Vector2.Zero;

            int place = part.whoAmI == NPC.whoAmI ? -1 : (int)part.ai[3];
            float tick = Main.GlobalTimeWrappedHourly * 60f;

            // A slow writhe, each part out of step with the next, and a fast jitter on it.
            float throw_ = 0.6f * (float)Math.Sin(tick * 0.9f + place * 2.3f)
                + 0.8f * (AbomanationArms.Hash01((int)tick, place + 7) - 0.5f);

            return part.rotation.ToRotationVector2() * (throw_ * amount);
        }

        /// <summary>A lump on its way up the throat: where it is along the body now, as a place (the head is -1), or nothing.</summary>
        private static float Lump(int place, int now, int arrives, float from, int ticks)
        {
            if (now > arrives || now < arrives - ticks)
                return 0f;

            float at = MathHelper.Lerp(from, -1f, 1f - (arrives - now) / (float)ticks);
            float off = place - at;

            return (float)Math.Exp(-off * off / 1.4f);
        }

        /// <summary>
        /// How much bigger than itself a part (or the head, place -1) is drawn. The puke:
        /// a lump comes up the neck to the head for each catch and each heave, and is at
        /// the head on the tick. The vent: all of it swells as the pressure builds.
        /// </summary>
        public float Swell(int place)
        {
            float lump = 0f;

            if (phase == PukeChoke)
            {
                foreach (int gag in Abomanation.ChokeGag)
                    lump = Math.Max(lump, 0.6f * Lump(place, attackTimer, gag, 5f, 10));

                // And the first of what is really coming, on its way up as the choke ends.
                lump = Math.Max(lump, Lump(place, attackTimer, ChokeLength + Abomanation.HeaveAt[0], 7f, 16));
            }
            else if (phase == PukeSpew)
            {
                foreach (int heave in Abomanation.HeaveAt)
                    lump = Math.Max(lump, Lump(place, attackTimer, heave, 7f, 16));
            }
            else if (AtVent)
            {
                float beat = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + place * 0.9f);

                return 1f + Heat * (0.07f + 0.05f * beat);
            }

            return 1f + 0.24f * lump;
        }

        /// <summary>
        /// What is drawn over a part's own sprite, added to it: nothing, or the red of what
        /// is hot in it. In the second half a slow ember goes down the body from the head,
        /// over and over; venting, all of it is lit.
        /// </summary>
        public Color Glow(int place)
        {
            float strength = 0.6f * Heat;

            if (Enraged && !Dying)
                strength += 0.1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f - place * 0.7f);

            if (strength <= 0.01f)
                return Color.Transparent;

            Color glow = Color.Lerp(Abomanation.Ember, Abomanation.Scald, 0.5f * Heat) * Math.Min(1f, strength);

            // No alpha: it is added to what is under it, not laid over it.
            glow.A = 0;

            return glow;
        }

        /// <summary>How a part's arms are held. The walk, unless it is thrashing, or lying with its upper arms up.</summary>
        public AbomanationArms.ArmPose ArmsOf(NPC part)
        {
            AbomanationArms.ArmPose pose = default;

            pose.Clock = -1;

            if (InSpasm)
                pose.Frenzy = SpasmGrip;
            else if (phase == PukeChoke || phase == PukeSpew)
                pose.Frenzy = 0.7f;
            else if (AtVent)
                pose.Frenzy = 0.4f + 0.8f * Heat;

            if (Lying && layKind == 0)
            {
                pose.Lying = true;
                pose.Up = UpSide(part);
                pose.Lift = phase == LayCrawl ? MathHelper.Clamp(attackTimer / 30f, 0f, 1f) : 1f;
                pose.Clock = AshClock(out _);
            }

            return pose;
        }

        /// <summary>
        /// One part's share of what is shown, a tick of it: called by the part, after it
        /// has moved, with whatever it follows.
        ///
        /// Going through the surface, it kicks the ground up. Then, wherever the joint
        /// between it and the one ahead is in open air: in a spasm it bleeds from there;
        /// in the second half it drips; lashing, it throws off what blood it has on it;
        /// throwing ash, the hand lets go in a puff; and venting, the joint is what the
        /// steam comes out of, which this machine's own player had better not be in.
        /// </summary>
        public void ShowPart(NPC part, NPC leader)
        {
            if (Main.dedServ || part.alpha > 0 || Dying)
                return;

            int place = Math.Clamp((int)part.ai[3], 0, partUnder.Length - 1);
            bool under = Collision.SolidCollision(part.position, part.width, part.height);

            if (partKnown[place] && under != partUnder[place] && !Lying)
                Kick(part);

            partKnown[place] = true;
            partUnder[place] = under;

            // Lying down, it comes up out of the ash it ran in under, part after part,
            // and some of the ash comes with each.
            bool sunk = false;

            if (Lying)
            {
                float line = GroundLine((int)(part.Center.X / 16f), (int)(part.Center.Y / 16f));

                sunk = line > 0f && part.Center.Y > line + 10f;

                if (partSunk[place] && !sunk)
                    Kick(part);
            }

            partSunk[place] = sunk;

            Vector2 along = (leader.Center - part.Center).SafeNormalize(Vector2.UnitY);
            Vector2 across = new Vector2(-along.Y, along.X);
            Vector2 joint = (leader.Center + part.Center) * 0.5f;

            // Each side of the joint that has air on it.
            bool one = Open(joint + across * 20f);
            bool other = Open(joint - across * 20f);

            if (!one && !other)
                return;

            if (InSpasm && Main.rand.NextFloat() < (Enraged ? 0.6f : 0.4f) * SpasmGrip)
                Bleed(joint, one && (!other || Main.rand.NextBool()) ? across : -across, 1f);

            if (Enraged && Main.rand.NextBool(Abomanation.RageDrip))
                Bleed(joint, one && (!other || Main.rand.NextBool()) ? across : -across, 0.4f);

            if (Cracking && place >= Abomanation.BodySegments - 6)
            {
                Vector2 motion = part.Center - (part.oldPosition + part.Size * 0.5f);

                if (motion.LengthSquared() > 18f * 18f && Main.rand.NextBool())
                {
                    PADParticleHandler.SpawnParticle(new BloodDroplet(part.Center + Main.rand.NextVector2Circular(12f, 12f),
                        motion * Main.rand.NextFloat(0.15f, 0.4f), Main.rand.Next(26, 44), Main.rand.NextFloat(0.5f, 1f), Abomanation.Blood));
                }
            }

            if (Throwing)
                AshShow(part);

            if (AtVent)
            {
                if (one)
                    VentJet(joint, across);

                if (other)
                    VentJet(joint, -across);
            }
        }

        /// <summary>A part going through the surface: a little of the ground goes with it.</summary>
        private static void Kick(NPC part)
        {
            int x = (int)(part.Center.X / 16f);
            int y = FloorNear(x, (int)(part.Center.Y / 16f), 3);

            if (y < 0)
                return;

            for (int i = 0; i < 3; i++)
                ThrowDust(x + Main.rand.Next(-1, 2), y, 0.8f, 0f);
        }

        /// <summary>Blood out of a joint, to the side that is open: a drop or two, and at full strength a thread of it.</summary>
        private static void Bleed(Vector2 joint, Vector2 side, float strength)
        {
            int drops = strength >= 1f && Main.rand.NextBool(3) ? 3 : 1;

            for (int i = 0; i < drops; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(
                    joint + side * Main.rand.NextFloat(14f, 22f),
                    side * Main.rand.NextFloat(0.6f, 3.2f) * strength + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-0.8f, 0.8f)),
                    Main.rand.Next(28, 48), Main.rand.NextFloat(0.5f, 1f), Abomanation.Blood));
            }
        }

        /// <summary>The ash, as it leaves a hand: where the arm scoops, a little of it goes; where the arm lets go, a puff. The ball itself is the server's.</summary>
        private void AshShow(NPC part)
        {
            int clock = AshClock(out _);

            if (clock < 0 || !(part.ModNPC is AbomanationBody))
                return;

            int place = (int)part.ai[3];
            int up = UpSide(part);
            int count = AbomanationArms.BottomCount(place, up);

            for (int row = 0; row < count; row++)
            {
                int tick = clock - AbomanationArms.ArmNumber(place, up, row) * Abomanation.AshStep;

                if (tick != 2 && tick != Abomanation.AshWind + 1)
                    continue;

                bool letGo = tick != 2;

                Vector2 hand = AbomanationArms.Hand(part, place, up, row, count, AbomanationArms.BottomSpan, ArmSeed,
                    letGo ? AbomanationArms.RaisedRake : AbomanationArms.RaisedRake + AbomanationArms.FlingBack * 0.3f);

                if (!Open(hand))
                    continue;

                for (int i = 0; i < (letGo ? 5 : 2); i++)
                {
                    Dust dust = Dust.NewDustPerfect(hand, DustID.Ash,
                        new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.5f, letGo ? 3.5f : 1.5f)),
                        80, default, Main.rand.NextFloat(0.9f, 1.4f));

                    dust.noGravity = false;
                }

                if (letGo)
                    SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f, PitchVariance = 0.2f, Volume = 0.35f, MaxInstances = 6 }, hand);
            }
        }

        // -------------------------------------------------------------------- the vent ----

        /// <summary>The head's own part of the vent: its mouth goes wide as the steam starts, the screen takes it, and it glows.</summary>
        private void VentShow()
        {
            if (attackTimer == Abomanation.VentTell + 1)
                Gape(Abomanation.VentBlast, 10);

            float heat = Heat;

            if (heat > 0f)
                Lighting.AddLight(NPC.Center, 0.9f * heat, 0.3f * heat, 0.12f * heat);

            if (Venting && attackTimer % 6 == 0)
                Rattle(2.4f, 8, 1100f);
        }

        /// <summary>
        /// One joint's steam, out one side of it. Swelling, a wisp now and then, more as
        /// it comes on. Venting, a jet: steam thrown hard straight out, which slows, lifts
        /// and spreads; blood thrown with it; a glow at the mouth of it; and anyone of this
        /// machine's own standing in the jet is scalded.
        /// </summary>
        private void VentJet(Vector2 joint, Vector2 side)
        {
            float heat = Heat;

            if (heat <= 0f)
                return;

            Vector2 mouth = joint + side * 18f;
            Vector2 along = new Vector2(-side.Y, side.X);

            Lighting.AddLight(mouth, 0.7f * heat, 0.24f * heat, 0.1f * heat);

            if (!Venting)
            {
                if (Main.rand.NextFloat() < 0.16f * heat)
                {
                    PADParticleHandler.SpawnParticle(new SteamPuff(mouth,
                        side * Main.rand.NextFloat(1.5f, 4f) + along * Main.rand.NextFloat(-0.5f, 0.5f),
                        Abomanation.Steam, Main.rand.Next(24, 38), 10f, Main.rand.NextFloat(26f, 40f), 0.32f));
                }

                return;
            }

            if (attackTimer % 2 == 0)
            {
                PADParticleHandler.SpawnParticle(new SteamPuff(mouth,
                    side * Main.rand.NextFloat(9f, 14f) + along * Main.rand.NextFloat(-1.3f, 1.3f),
                    Abomanation.Steam, Main.rand.Next(30, 46), Main.rand.NextFloat(12f, 18f), Main.rand.NextFloat(52f, 78f),
                    Main.rand.NextFloat(0.4f, 0.55f)));
            }

            if (attackTimer % 3 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(mouth,
                    side * Main.rand.NextFloat(4.5f, 9f) + along * Main.rand.NextFloat(-1.6f, 1.6f),
                    Main.rand.Next(30, 50), Main.rand.NextFloat(0.5f, 1f), Abomanation.Blood));
            }

            if (attackTimer % 5 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(mouth, side * 1.5f,
                    Color.Lerp(Abomanation.Ember, Abomanation.Scald, 0.4f) * 0.55f, Main.rand.NextFloat(0.22f, 0.32f), 10, lights: false));
            }

            Scald(mouth, side);
        }

        /// <summary>The steam on this machine's own player, if they are standing in this jet.</summary>
        private static void Scald(Vector2 from, Vector2 side)
        {
            if (!Abomanation.VentScalds)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead || me.immune)
                return;

            float where = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, from + side * Abomanation.VentReach,
                    Abomanation.VentWidth, ref where))
                return;

            float dealt = Main.masterMode ? Abomanation.VentHitMaster
                : Main.expertMode ? Abomanation.VentHitExpert
                : Abomanation.VentHit;

            if (PADWorld.Legends)
                dealt *= Abomanation.LegendsDamage;

            int damage = Main.DamageVar(dealt, -me.luck);

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText.AmalgaermisSteam", me.name));

            me.Hurt(reason, damage, side.X >= 0f ? 1 : -1, false, false, ImmunityCooldownID.General, true, 0f, 0f, 2f);
        }
    }
}
