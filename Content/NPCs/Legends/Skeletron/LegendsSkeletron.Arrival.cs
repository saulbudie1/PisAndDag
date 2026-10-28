using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.Empress;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- his coming ------------------------------------------------------------------------------------------------
    //
    // By request ("camera to the old man and zoom; the old man coughs up blood; his hand explodes into two Skeletron arms
    // that hold him for 2 seconds; zoom in and shake, then shred him into 2 pieces right down the middle; the Skeletron
    // head pops up and the halves are cast aside; then mouth opens and a big roar"), in every world, every time, the
    // player free to move ("player can still move") and nothing hurting them meanwhile (SkeletronPlayer.Shelter); and
    // nothing of his hurting or hurt ("invincible"):
    //
    //     LOOK     50   the camera goes to the old man and leans in; he stands there; bones stir inside him
    //     COUGH   100   he coughs, twice, blood coming up, doubled over; between, he shakes
    //     BURST    30   his hand bursts, blood and bone: two arms out of it, hands on them, that swing round him and take
    //                   hold of him from either side
    //     GRIP    120   they hold him two seconds, crushing him; the camera leans further in and shakes, harder and harder;
    //                   the tearing starts
    //     SHRED    24   they tear him in two, down the middle
    //     RISE     36   the skull pops up out of what is left of him; the hands cast the halves aside and go to him
    //     ROAR    100   his jaw drops and he roars, his hands up; his name comes up with it
    //     HOME     40   the camera walks home; the fight
    //
    // Without the old man (the Clothier's death brought him, or something else did), up out of the ground where he comes:
    //
    //     LOOK     40   the camera there; the ground cracks
    //     GATHER   60   bone and dust drawn in to the spot, faster and faster, a red glow under it
    //     BURST    30   two hands burst up out of it
    //     RISE, ROAR and HOME as above; the Clothier's red hat, if it was his death, comes down onto him as he rises
    //
    // The server decides when each beat starts (Next) and where he came (OnSpawn); every machine plays it. E held in the
    // corner skips it (CinematicSkip, single player).

    public partial class LegendsSkeletron
    {
        private const int ArriveLook = 0;
        private const int ArriveCough = 1;
        private const int ArriveBurst = 2;
        private const int ArriveGrip = 3;
        private const int ArriveShred = 4;
        private const int ArriveRise = 5;
        private const int ArriveRoar = 6;
        private const int ArriveHome = 7;
        private const int ArriveGather = 8;

        private const int LookTicks = 50;
        private const int EmergeLookTicks = 40;
        private const int CoughTicks = 100;
        private const int BurstTicks = 30;
        private const int GripTicks = 120;
        private const int ShredTicks = 24;
        private const int RiseTicks = 36;
        private const int RoarTicks = 100;
        private const int HomeTicks = 40;
        private const int GatherTicks = 60;

        /// <summary>His two coughs, into COUGH; the tearing sound's start, into GRIP (its splat lands as they tear him).</summary>
        private const int FirstCough = 8;
        private const int SecondCough = 52;
        private const int TearFrom = 44;

        /// <summary>Into RISE, the tick the halves are cast aside.</summary>
        private const int CastAt = 6;

        /// <summary>A player this near where he comes is in the scene.</summary>
        private const float SceneRange = 2600f;

        /// <summary>How far over where the old man stood his skull ends up.</summary>
        private const float RiseHeight = 190f;

        /// <summary>Where the hands take hold of the old man: either side of his middle, and up from his feet.</summary>
        private const float GripApart = 36f;
        private const float GripUp = 28f;

        /// <summary>
        /// How big his hands are while they are still the old man's (out of his hand, holding him, tearing him): about his
        /// size, so he is seen in their grip; they grow to their full size as the skull comes up out of him.
        /// </summary>
        private const float BornScale = 0.6f;

        /// <summary>His name's plate: the one with a line, a small lean in, and brisk (it comes up with his roar and goes as the scene lets go).</summary>
        private static readonly BossIntroProfile Plate = new()
        {
            ZoomIn = 1.1f,
            Brisk = true,
        };

        private static readonly Color Blood = new Color(150, 8, 12);
        private static readonly Color BoneDust = new Color(214, 206, 170);

        // ---- the old man, on the old man's canvas (his picture, facing left, his feet at 20, 54) ---------------------------

        /// <summary>His front hand and his mouth, from his feet, facing left (mirrored for right).</summary>
        private static readonly Vector2 OldHand = new Vector2(-9f, -15f);
        private static readonly Vector2 OldMouth = new Vector2(-6f, -29f);

        /// <summary>A point of the old man, from his feet, the way he faces.</summary>
        private Vector2 OnOldMan(Vector2 point) => anchor + new Vector2(point.X * -facing, point.Y);

        /// <summary>Where the arms come out: his hand burst (or, with no old man, the spot in the ground).</summary>
        private Vector2 BurstPoint => victim ? OnOldMan(OldHand) : anchor + new Vector2(0f, -8f);

        /// <summary>Where his skull comes to rest over it.</summary>
        private Vector2 RisenAt => anchor + new Vector2(0f, -RiseHeight);

        /// <summary>Where a hand takes hold of him.</summary>
        private Vector2 GripSpot(float side) => anchor + new Vector2(side * GripApart, -GripUp);

        /// <summary>Where he is torn open: his middle, where the arms come out of him once he is in two, and the skull after.</summary>
        private Vector2 TearMiddle => anchor + new Vector2(0f, -24f);

        // ---- the beats ----------------------------------------------------------------------------------------------

        /// <summary>The server's: on to another beat of the scene, from its first tick.</summary>
        private void Next(NPC npc, int to)
        {
            beat = to;
            timer = -1;
            npc.netUpdate = true;
        }

        private void RunArrival(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.rotation = 0f;
            headHarmless = true;

            int t = timer;
            bool here = InScene(anchor);
            watching = false;

            if (here)
            {
                Main.LocalPlayer.GetModPlayer<SkeletronPlayer>().Shelter();
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    SkipArrival(npc);
                    return;
                }
            }

            // His hands, hurting nothing and untouchable, wherever this beat has them.
            for (int i = 0; i < 2; i++)
            {
                if (hand[i] >= 0)
                    ArrivalHand(npc, SideOf(i), t);
            }

            switch (beat)
            {
                case ArriveLook:
                    {
                        hidden = true;
                        Place(npc, anchor + new Vector2(0f, -30f));

                        int length = victim ? LookTicks : EmergeLookTicks;
                        Shot(npc, anchor + new Vector2(0f, -30f), Ease(t / 40f), 1.25f, 0.05f);

                        if (Crossed(20))
                            Sound(victim ? SkeletronSounds.Rattle with { Volume = 0.45f, Pitch = -0.4f } : SkeletronSounds.GroundCrack, anchor);

                        if (!victim && !Main.dedServ && t > 10)
                            Cracking(t);

                        if (Authority && t >= length)
                            Next(npc, victim ? ArriveCough : ArriveGather);

                        break;
                    }

                case ArriveGather:
                    hidden = true;
                    Place(npc, anchor + new Vector2(0f, -30f));
                    Shot(npc, anchor + new Vector2(0f, -30f), 1f, 1.32f, 0.05f);

                    if (!Main.dedServ)
                        Gathering(t);

                    if (Crossed(10) || Crossed(30) || Crossed(50))
                        Sound(SkeletronSounds.Rattle with { Volume = 0.6f + t / 120f }, anchor);

                    if (Authority && t >= GatherTicks)
                        Next(npc, ArriveBurst);

                    break;

                case ArriveCough:
                    hidden = true;
                    Place(npc, anchor + new Vector2(0f, -30f));
                    Shot(npc, anchor + new Vector2(0f, -30f), 1f, 1.32f, 0.05f);

                    if (Crossed(FirstCough))
                        Cough(1);

                    if (Crossed(SecondCough))
                        Cough(2);

                    // His hand, before it goes: bones moving under the skin.
                    if (Crossed(80))
                        Sound(SkeletronSounds.Rattle with { Volume = 0.7f, Pitch = -0.2f }, BurstPoint);

                    if (Authority && t >= CoughTicks)
                        Next(npc, ArriveBurst);

                    break;

                case ArriveBurst:
                    hidden = true;
                    Place(npc, anchor + new Vector2(0f, -30f));
                    Shot(npc, anchor + new Vector2(0f, -30f), 1f, 1.42f, 0.2f);

                    if (Crossed(0))
                    {
                        if (Authority)
                        {
                            SpawnHands(npc);

                            // Asked of from their first tick, so the game never has them (nor they their full size) a moment.
                            for (int i = 0; i < 2; i++)
                            {
                                if (hand[i] >= 0)
                                    ArrivalHand(npc, SideOf(i), t);
                            }
                        }

                        Bursts();
                    }

                    if (Authority && t >= BurstTicks)
                        Next(npc, victim ? ArriveGrip : ArriveRise);

                    break;

                case ArriveGrip:
                    {
                        hidden = true;
                        Place(npc, anchor + new Vector2(0f, -30f));

                        float p = Math.Min(1f, t / (float)GripTicks);
                        Shot(npc, anchor + new Vector2(0f, -28f), 1f, MathHelper.Lerp(1.42f, 1.68f, p), 0.06f);

                        if (!Main.dedServ && t % 3 == 0)
                            Shake(anchor, 0.5f + 3.2f * p * p, 4);

                        if (Crossed(10) || Crossed(64))
                            Sound(SkeletronSounds.Rattle with { Volume = 0.8f, Pitch = -0.5f }, anchor);

                        if (Crossed(TearFrom))
                            Sound(SkeletronSounds.Tear, anchor);

                        // Squeezed: blood out of him, more and more.
                        if (!Main.dedServ && t % Math.Max(3, 14 - t / 10) == 0)
                        {
                            Vector2 at = anchor + new Vector2(Main.rand.NextFloat(-8f, 8f), -Main.rand.NextFloat(12f, 34f));
                            Dust dust = Dust.NewDustPerfect(at, DustID.Blood, new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 1f)), 0, default, 1.3f);
                            dust.noGravity = false;
                        }

                        if (Authority && t >= GripTicks)
                            Next(npc, ArriveShred);

                        break;
                    }

                case ArriveShred:
                    hidden = true;
                    Place(npc, anchor + new Vector2(0f, -30f));
                    Shot(npc, anchor + new Vector2(0f, -26f), 1f, 1.75f, 0.3f);

                    if (Crossed(0))
                        Shreds();

                    if (Authority && t >= ShredTicks)
                        Next(npc, ArriveRise);

                    break;

                case ArriveRise:
                    {
                        // Up out of him with a pop: past where he stops and back, growing as he comes.
                        float up = MathHelper.Lerp(30f, RiseHeight, Back(t / 20f, 2f));
                        Place(npc, anchor + new Vector2(0f, -up));
                        popScale = MathHelper.Lerp(0.35f, 1f, Back(t / 14f, 2.4f));

                        if (redHat && !victim)
                            hatWant = 70f * (1f - Back(MathHelper.Clamp((t - 6f) / 20f, 0f, 1f), 2.4f));

                        faceRate = 1f;
                        hatShown = hatWant;

                        Vector2 focus = Vector2.Lerp(anchor + new Vector2(0f, -30f), RisenAt + new Vector2(0f, 30f), Ease(t / 22f));
                        Shot(npc, focus, 1f, 1.38f, 0.08f);

                        if (Crossed(0))
                            Pops(npc);

                        if (Crossed(CastAt) && victim)
                            CastAside();

                        if (Authority && t >= RiseTicks)
                            Next(npc, ArriveRoar);

                        break;
                    }

                case ArriveRoar:
                    {
                        Vector2 shiver = t < 80 ? Main.rand.NextVector2Circular(1.5f, 1.5f) : Vector2.Zero;
                        Place(npc, RisenAt + shiver);

                        float open = t < 8 ? EaseOut(t / 8f) : t < 82 ? 1f : 1f - Ease((t - 82) / 18f);
                        jawWant = 30f * open;
                        liftWant = 9f * open;
                        hatWant = 14f * open;
                        eyesWant = open;
                        faceRate = 0.5f;

                        Shot(npc, RisenAt + new Vector2(0f, 30f), 1f, 1.2f, 0.05f);

                        if (Crossed(0))
                            Roars(npc);

                        if (!Main.dedServ && t < 80 && t % 4 == 0)
                            Breathe(npc, 0.6f);

                        if (Authority && t >= RoarTicks)
                            Next(npc, ArriveHome);

                        break;
                    }

                case ArriveHome:
                    Place(npc, RisenAt);

                    if (Crossed(0) && here && !Main.dedServ)
                        PisPlayer.PanHome(npc.Center, 60);

                    if (Authority && t >= HomeTicks)
                        EndArrival(npc);

                    break;

                default:
                    if (Authority)
                        EndArrival(npc);

                    break;
            }

            // A heartbeat, so a machine that missed a word catches up.
            if (Authority && t % 30 == 0)
                npc.netUpdate = true;
        }

        /// <summary>What his coming asks of a hand of his, this tick.</summary>
        private void ArrivalHand(NPC npc, float side, int t)
        {
            Vector2 burst = BurstPoint;
            Vector2 grip = GripSpot(side);
            Vector2 inward = new Vector2(-side, -0.3f);
            Aim a = Aim.At(grip, SkeletronRig.AngleFor(inward), SkeletronRig.Clutch, 1f);

            a.Hurt = 0f;
            a.Untouchable = true;
            a.HasRoot = true;
            a.Root = burst;
            a.Arm = ArmStyle.Straight;

            switch (beat)
            {
                case ArriveBurst:
                    if (victim)
                    {
                        // Out of the burst, round and onto him: growing as they come, open, then taking hold.
                        float p = EaseOut(t / 22f);
                        Vector2 round = burst + new Vector2(side * 90f, -70f);
                        a.Goal = SkeletronRig.Curve(burst, round, grip, p);
                        a.Scale = BornScale * (0.25f + 0.75f * Elastic(t / 16f));
                        a.Pose = SkeletronRig.Pose.Lerp(SkeletronRig.Spread, SkeletronRig.Clutch, Ease((t - 14f) / 14f));
                        a.Angle = SkeletronRig.AngleFor(Vector2.Lerp(new Vector2(side, -1f), inward, Ease((t - 8f) / 14f)));
                        a.Turn = 0.4f;
                        a.Shape = 0.5f;
                    }
                    else
                    {
                        // Up out of the ground, open, reaching.
                        float p = EaseOut(t / 20f);
                        Vector2 high = anchor + new Vector2(side * 120f, -150f);
                        a.Goal = SkeletronRig.Curve(burst, burst + new Vector2(side * 30f, -140f), high, p);
                        a.Scale = 0.25f + 0.75f * Elastic(t / 16f);
                        a.Pose = SkeletronRig.Spread;
                        a.Angle = SkeletronRig.AngleFor(new Vector2(side * 0.5f, -1f));
                        a.Turn = 0.4f;
                    }

                    break;

                case ArriveGrip:
                    {
                        // Crushing: the fingers closing harder, the hands shaking with it.
                        float p = Math.Min(1f, t / (float)GripTicks);
                        a.Goal = grip + new Vector2(side * -4f * p, 0f);
                        a.Scale = BornScale;
                        a.Shake = 0.6f + 2.4f * p;
                        a.Pose = SkeletronRig.Pose.Lerp(SkeletronRig.Clutch, SkeletronRig.Fist, 0.25f * p);
                        break;
                    }

                case ArriveShred:
                    {
                        // Torn apart: out, and up a little, fast.
                        float p = EaseOut(t / 8f);
                        a.Goal = grip + new Vector2(side * 78f * p, -14f * p);
                        a.Angle = SkeletronRig.AngleFor(new Vector2(-side, -0.3f - 0.5f * p));
                        a.Turn = 0.5f;
                        a.Scale = BornScale;

                        // His arms out of the tear now, pulling the halves apart from inside him.
                        a.Root = Vector2.Lerp(burst, TearMiddle, Ease(t / 6f));
                        break;
                    }

                case ArriveRise:
                    {
                        Vector2 torn = grip + new Vector2(side * 78f, -14f);

                        if (!victim)
                        {
                            a.Goal = npc.Center + new Vector2(side * 160f, 50f);
                            a.Gain = 0.2f;
                            a.Pose = SkeletronRig.Spread;
                            a.Angle = SkeletronRig.AngleFor(new Vector2(side * 0.4f, 1f));
                            a.Root = Vector2.Lerp(burst, SkeletronRig.Shoulder(npc.Center, side, npc.scale), Ease(t / 14f));
                        }
                        else if (t < CastAt)
                        {
                            // Swung back to cast the halves aside.
                            float p = EaseIn(t / (float)CastAt);
                            a.Goal = torn + new Vector2(side * -20f * p, -20f * p);
                            a.Scale = BornScale;
                            a.Root = TearMiddle;
                        }
                        else
                        {
                            // Flung out, and on round to him.
                            a.Goal = npc.Center + new Vector2(side * 160f, 50f);
                            a.Gain = 0.25f;
                            a.Most = 30f;
                            a.Pose = SkeletronRig.Pose.Lerp(SkeletronRig.Spread, SkeletronRig.Relaxed, Ease((t - CastAt) / 20f));
                            a.Shape = 0.3f;
                            a.Angle = SkeletronRig.AngleFor(new Vector2(side * 0.4f, 1f));
                            a.Root = Vector2.Lerp(TearMiddle, SkeletronRig.Shoulder(npc.Center, side, npc.scale), Ease((t - CastAt) / 16f));

                            // Grown to his full size with the skull.
                            a.Scale = MathHelper.Lerp(BornScale, 1f, Back((t - CastAt) / 14f, 2f));
                        }

                        if (t >= RiseTicks - 2)
                            a.HasRoot = false;

                        break;
                    }

                case ArriveRoar:
                    {
                        // Up beside him, open, shaking with the roar.
                        a.Goal = npc.Center + new Vector2(side * 178f, -36f);
                        a.Gain = 0.22f;
                        a.Angle = SkeletronRig.AngleFor(new Vector2(side * 0.55f, -1f));
                        a.Pose = SkeletronRig.Spread;
                        a.Shake = t < 82 ? 2.5f : 0f;
                        a.HasRoot = false;
                        a.Arm = ArmStyle.Bent;
                        break;
                    }

                case ArriveHome:
                    a.Goal = Rest(npc, side);
                    a.Gain = 0.12f;
                    a.Angle = RestAngle(npc, side);
                    a.Pose = Hanging(side);
                    a.HasRoot = false;
                    a.Arm = ArmStyle.Bent;
                    break;
            }

            aims[IndexOf(side)] = a;
        }

        /// <summary>The server's: his two hands, out of the burst. Vanilla's own making of them (its first tick), here.</summary>
        private void SpawnHands(NPC npc)
        {
            npc.ai[0] = 1f;

            for (int k = 0; k < 2; k++)
            {
                float side = SideOf(k);

                if (HandOn(side) != null)
                    continue;

                Vector2 at = BurstPoint;
                int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)at.X, (int)at.Y + 26, NPCID.SkeletronHand, npc.whoAmI);

                if (index < 0 || index >= Main.maxNPCs)
                    continue;

                NPC made = Main.npc[index];
                made.ai[0] = -side;
                made.ai[1] = npc.whoAmI;
                made.ai[3] = side < 0f ? 150f : 0f;
                made.target = npc.target;
                made.Center = at;
                made.netUpdate = true;

                hand[k] = index;
            }

            FindHands(npc);
        }

        /// <summary>His coming is over (on every machine, when the server says): the fight, his name up for whoever has not seen it.</summary>
        private void EndArrival(NPC npc)
        {
            scene = Scene.Fight;
            beat = 0;
            timer = 0;
            ranBeat = -1;
            move = Move.None;
            nextMove = FirstMove;
            npc.chaseable = true;
            npc.dontTakeDamage = PADWorld.Legends && handCount > 0;
            npc.velocity = Vector2.Zero;
            npc.netUpdate = true;

            Announce(npc);
        }

        /// <summary>
        /// Skipped (CinematicSkip; single player, so this machine decides): what is left of the old man gone in a spray of
        /// him, his hands made if they are not yet, the skull where it would have come to, his name up, the fight.
        /// </summary>
        private void SkipArrival(NPC npc)
        {
            // (This tick's beat has not played yet: what has, is what ranTimer says.)
            if (victim && (beat < ArriveRise || beat == ArriveRise && ranTimer < CastAt))
            {
                if (beat < ArriveShred || beat == ArriveShred && ranTimer < 0)
                    Shreds();

                CastAside();
            }

            if (handCount < 2)
                SpawnHands(npc);

            npc.Center = RisenAt;
            npc.velocity = Vector2.Zero;

            for (int i = 0; i < 2; i++)
            {
                if (hand[i] >= 0)
                {
                    Main.npc[hand[i]].Center = Rest(npc, SideOf(i));
                    Main.npc[hand[i]].velocity = Vector2.Zero;
                }
            }

            if (watching || InScene(anchor))
                PisPlayer.PanHome(npc.Center, 45);

            EndArrival(npc);
        }

        /// <summary>His name, once, on this machine: the brisk plate, up with his roar.</summary>
        private void Announce(NPC npc)
        {
            if (plated || Main.dedServ)
                return;

            plated = true;
            BossIntro.PlayNameOnly(npc.whoAmI, Plate);
        }

        /// <summary>This machine's player is near enough to be in the scene.</summary>
        private static bool InScene(Vector2 at)
        {
            if (Main.dedServ)
                return false;

            Player me = Main.LocalPlayer;
            return me.active && !me.dead && Vector2.Distance(me.Center, at) <= SceneRange;
        }

        /// <summary>
        /// The shot, for a player in the scene: the camera <paramref name="blend"/> of the way to the focus, the bars, the
        /// interface away, and the lean (counting the player's own zoom). Never the player held: they keep moving.
        /// </summary>
        private void Shot(NPC npc, Vector2 focus, float blend, float lean, float rate)
        {
            if (!InScene(anchor))
                return;

            watching = true;

            if (blend > 0f)
                PisPlayer.BlendCamera(focus, blend);

            PisPlayer.HideUI();
            Cinematic.Frame();
            Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)), rate);
        }

        // ---- what is seen and heard, on each machine ---------------------------------------------------------------------

        /// <summary>A cough: doubled over, blood out of his mouth, the camera jolted; the second harder.</summary>
        private void Cough(int which)
        {
            if (Main.dedServ)
                return;

            Vector2 mouth = OnOldMan(OldMouth);
            Vector2 forward = new Vector2(facing, 0.5f);

            SoundEngine.PlaySound(which == 1 ? SkeletronSounds.Cough1 : SkeletronSounds.Cough2, mouth);
            BloodDroplet.SpawnBurst(mouth, forward, which == 1 ? 10 : 18, 28f);

            for (int i = 0; i < (which == 1 ? 8 : 14); i++)
                Dust.NewDustPerfect(mouth, DustID.Blood, forward.RotatedByRandom(0.5f) * Main.rand.NextFloat(1f, 4f), 0, default, 1.2f);

            Shake(mouth, which == 1 ? 1.5f : 2.5f, 8);
        }

        /// <summary>His hand bursts: blood and bone everywhere, a flash, a thump; the arms out of it.</summary>
        private void Bursts()
        {
            if (Main.dedServ)
                return;

            Vector2 at = BurstPoint;

            SoundEngine.PlaySound(SkeletronSounds.BoneBurst, at);
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.4f, Volume = 0.6f }, at);

            if (victim)
            {
                for (int k = 0; k < 4; k++)
                    BloodDroplet.SpawnBurst(at, (MathHelper.PiOver2 * k + 0.4f).ToRotationVector2(), 10, 50f);

                for (int i = 0; i < 6; i++)
                    PADParticleHandler.SpawnParticle(new BloodFume(at, Main.rand.NextVector2Circular(2f, 2f) + new Vector2(0f, -1f), Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(40, 70)));
            }
            else
            {
                GroundWave.Slam(anchor, 30f, 18, 16f, 26);
                LegendsKingSlime.GroundDust(anchor.X - 60f, anchor.X + 60f, anchor.Y, 0.8f);
            }

            for (int i = 0; i < 30; i++)
            {
                Vector2 speed = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 9f);
                Dust.NewDustPerfect(at, DustID.Bone, speed, 0, default, Main.rand.NextFloat(1.1f, 1.7f));
                PADParticleHandler.SpawnParticle(new StoneChip(at, speed * 0.7f + new Vector2(0f, -2f), BoneDust, Main.rand.Next(50, 90), Main.rand.NextFloat(1f, 1.6f)));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, new Color(255, 80, 60), 160f / 160f, 18));
            EmpressWarp.Ripple(at, 12f, 26f, 10f, 24);
            Cinematic.Flash(new Color(140, 20, 16), 0.3f * Near(at));
            Shake(at, 7f, 18);
        }

        /// <summary>Torn in two: a spray of him up the cut, a flash, the camera thrown.</summary>
        private void Shreds()
        {
            if (Main.dedServ)
                return;

            Vector2 middle = anchor + new Vector2(0f, -22f);

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.3f, Volume = 1f }, middle);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.8f }, middle);

            PADParticleHandler.SpawnParticle(new BloodSlash(middle, Vector2.UnitY, 40f, 9f, 26, true));
            PADParticleHandler.SpawnParticle(new BloodSlash(middle, Vector2.UnitY, 36f, 6f, 26));

            for (int k = -1; k <= 1; k += 2)
            {
                BloodDroplet.SpawnBurst(middle, new Vector2(k, -0.6f), 22, 35f);
                BloodDroplet.SpawnBurst(middle + new Vector2(0f, -14f), new Vector2(k * 0.4f, -1f), 10, 20f);
            }

            for (int i = 0; i < 10; i++)
                PADParticleHandler.SpawnParticle(new BloodFume(middle + Main.rand.NextVector2Circular(6f, 20f), new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f)), Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(50, 90)));

            for (int i = 0; i < 40; i++)
                Dust.NewDustPerfect(middle + new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-24f, 20f)), DustID.Blood, new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 1f)), 0, default, Main.rand.NextFloat(1.2f, 1.8f));

            Cinematic.Flash(new Color(170, 10, 10), 0.45f * Near(middle));
            EmpressWarp.Ripple(middle, 16f, 30f, 12f, 26);
            Shake(middle, 12f, 22);
        }

        /// <summary>The halves of him cast aside, turning, bleeding (each machine's own).</summary>
        private void CastAside()
        {
            if (Main.dedServ)
                return;

            Vector2 middle = anchor + new Vector2(0f, -20f);
            bool flip = facing == 1;

            for (int k = -1; k <= 1; k += 2)
            {
                // Where the half was held as the hand wound up to cast it (HalfOffset, at the cast).
                Vector2 at = middle + new Vector2(k * 58f, -34f);
                Vector2 velocity = new Vector2(k * Main.rand.NextFloat(6f, 8f), -Main.rand.NextFloat(5f, 7f));
                PADParticleHandler.SpawnParticle(new OldManHalf(at, velocity, k * 0.35f, k > 0, flip));
            }

            SoundEngine.PlaySound(SkeletronSounds.Swoop with { Volume = 0.6f }, middle);
        }

        /// <summary>The skull up out of what is left: a pop, bone flying, the world rung by it.</summary>
        private void Pops(NPC npc)
        {
            if (Main.dedServ)
                return;

            Vector2 at = anchor + new Vector2(0f, -30f);

            SoundEngine.PlaySound(SkeletronSounds.Pop, at);

            for (int i = 0; i < 26; i++)
                Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(16f, 10f), DustID.Bone, new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(3f, 9f)), 0, default, Main.rand.NextFloat(1.1f, 1.6f));

            if (victim)
            {
                for (int i = 0; i < 12; i++)
                    Dust.NewDustPerfect(at, DustID.Blood, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(4f, 9f)), 0, default, 1.5f);
            }

            EmpressWarp.Ripple(at, 14f, 28f, 9f, 24);
            Shake(at, 5f, 12);
        }

        /// <summary>His roar: his own voice, rings off him, his name, the sky shaken.</summary>
        private void Roars(NPC npc)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SkeletronSounds.RoarBig, npc.Center);
            Cinematic.Roar(npc, RoarTicks - 10, 1.25f, quiet: true, rim: new Color(150, 20, 16));

            for (int k = 0; k < 3; k++)
                EmpressWarp.Ripple(npc.Center + new Vector2(0f, 20f), 16f + k * 3f, 34f, 10f - k * 2f, 30 + k * 6);

            if (InScene(anchor))
                Announce(npc);
        }

        /// <summary>Breath out of him as he roars: bone dust and a few motes off his mouth.</summary>
        private void Breathe(NPC npc, float strength)
        {
            Vector2 mouth = SkeletronRig.Mouth(npc.Center, npc.rotation, npc.scale, jawShown, liftShown);

            for (int i = 0; i < 3; i++)
            {
                Vector2 speed = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(1f, 5f)) * strength;
                Dust dust = Dust.NewDustPerfect(mouth + Main.rand.NextVector2Circular(14f, 4f), DustID.Bone, speed, 120, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
        }

        /// <summary>No old man: cracks running out across the ground where he will come up.</summary>
        private void Cracking(int t)
        {
            if (t % 5 != 0)
                return;

            float reach = 20f + t * 3f;
            Vector2 at = anchor + new Vector2(Main.rand.NextFloat(-reach, reach), -2f);
            Dust dust = Dust.NewDustPerfect(at, DustID.Bone, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f)), 0, default, 1.1f);
            dust.noGravity = false;

            if (t % 15 == 0)
                Shake(anchor, 1.5f, 8);
        }

        /// <summary>Bone and dust drawn in to the spot, round and round, faster and faster.</summary>
        private void Gathering(int t)
        {
            float p = Math.Min(1f, t / (float)GatherTicks);
            Vector2 centre = anchor + new Vector2(0f, -24f);
            int count = 2 + (int)(4 * p);

            for (int i = 0; i < count; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(140f, 260f) * (1f - 0.4f * p);
                Vector2 at = centre + angle.ToRotationVector2() * radius;
                Vector2 inward = (centre - at).SafeNormalize(Vector2.Zero);
                Vector2 round = new Vector2(-inward.Y, inward.X);
                Vector2 speed = inward * MathHelper.Lerp(4f, 10f, p) + round * MathHelper.Lerp(3f, 7f, p);

                Dust dust = Dust.NewDustPerfect(at, DustID.Bone, speed, 60, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            if (t % 10 == 0)
                Shake(centre, 1f + 2f * p, 8);
        }
    }
}
