using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- the arrival ------------------------------------------------------------------------------
    //
    // The Gelatin Crystal is what she comes out of. Every world, every time she is summoned:
    //
    //     RISE    70   the crystal flies up out of the hand that raised it, turning; the camera
    //                  goes with it and leans in
    //     CRACK  120   it hangs there and cracks, three times, shaking at each, more light out
    //                  of it every time
    //     BURST   24   it bursts: a flash, and all that is left is its shape, white, glowing her
    //                  colours round it
    //     MELT    48   the shape runs, weightless, like something liquid, into the shape of her core
    //     GLOW   120   and hangs there glowing, two seconds, light drawn into it
    //     GROW    54   she swells out of it, white, and her colours come into her as she settles
    //     CROWN   36   her crown drops onto her
    //     FALL   ...   she falls to the ground, and it shakes
    //     ROAR    90   she roars, and her name comes up with it
    //     HOLD    41   held on her while the name settles; then the fight, the camera walking home
    //
    // The server decides where (BeginRite) and when each step starts; every machine plays it.
    // From the first tick to the last, anything hostile in the scene with less than 500 life is
    // killed on the spot and gives nothing (ClearScene), so nothing walks into it.

    public partial class LegendsQueenSlime
    {
        private const int RiteRise = 0;
        private const int RiteCrack = 1;
        private const int RiteBurst = 2;
        private const int RiteMelt = 3;
        private const int RiteGlow = 4;
        private const int RiteGrow = 5;
        private const int RiteCrown = 6;
        private const int RiteFall = 7;
        private const int RiteRoar = 8;
        private const int RiteHold = 9;

        private const int RiseTicks = 70;
        private const int CrackTicks = 120;
        private const int BurstTicks = 24;
        private const int MeltTicks = 48;
        private const int GlowTicks = 120;
        private const int GrowTicks = 54;
        private const int CrownTicks = 36;
        private const int FallMost = 150;
        private const int RoarTicks = 90;

        /// <summary>
        /// Held on her while her plate finishes, as the King's and the Queen Bee's are: the plain
        /// plate's rise, fly and settle, less what the roar has spent, and a moment. Follows the plate.
        /// </summary>
        private const int HoldTicks = BossIntro.PlainPlateTicks - BossIntro.PlainLook - RoarTicks + 5;

        /// <summary>The camera's walk home, and the zoom let go over the same.</summary>
        private const int ReturnTicks = 60;

        /// <summary>The three cracks, ticks into CRACK, and how long each shakes the crystal.</summary>
        private static readonly int[] CrackAt = { 24, 58, 92 };
        private const int CrackShake = 14;

        /// <summary>Ticks into CROWN when the crown lands on her, and how far above her it falls from.</summary>
        private const int CrownLands = 18;
        private const float CrownFrom = 150f;

        /// <summary>The crystal's size once it hangs, and the size of the white core she grows out of.</summary>
        private const float CrystalSize = 3f;
        private const float CoreGlowSize = 1.5f;

        /// <summary>How much of herself she is the tick she starts to come out of it.</summary>
        private const float GrowFrom = 0.08f;

        /// <summary>From her feet down to her core, at her standing frame: where to put her so her core is on the light.</summary>
        private const float CoreBelow = 48f;

        /// <summary>How high over the ground her core hangs where she forms.</summary>
        private const float FormHeight = 190f;

        /// <summary>How far up the crystal arcs over its way, above the higher of its two ends.</summary>
        private const float RiseArc = 60f;

        /// <summary>How far to the summoner's front she is formed, nearest to the ideal first, and how far up or down that ground may be.</summary>
        private static readonly float[] RiteAway = { 300f, 240f, 360f, 180f, 420f, 120f, 480f };
        private const float RiteClimb = 240f;

        /// <summary>A player this near is in the scene: camera, bars, held still.</summary>
        private const float SceneRange = 3000f;

        /// <summary>Anything hostile in the scene with less life than this is killed as she comes (ClearScene).</summary>
        private const int ClearBelow = 500;

        /// <summary>
        /// The camera's lean, as a framing that counts the player's own zoom: close on the crystal,
        /// closer as it cracks, back a little as it bursts, and out to her size as she comes.
        /// </summary>
        private const float CrystalLean = 1.75f;
        private const float CrackLean = 1.9f;
        private const float BurstLean = 1.55f;
        private const float GlowLean = 1.7f;
        private const float HerLean = 1.35f;
        private const float FallLean = 1.25f;

        /// <summary>Her name's plate: the plain one, a lean in, let go as the camera walks home.</summary>
        private static readonly BossIntroProfile RitePlate = new()
        {
            ZoomIn = 1.15f,
            ReturnTicks = ReturnTicks,
        };

        /// <summary>Where the crystal leaves the summoner's hand, where it hangs (and she forms), and the ground under that.</summary>
        private Vector2 riteStart;
        private Vector2 riteFocus;
        private float riteGround;

        /// <summary>This machine's player was in the shot on the last tick: the camera is walked home for them when it ends.</summary>
        private bool watching;

        private bool plated;
        private bool riteLanded;

        // ---- begun --------------------------------------------------------------------------------

        /// <summary>The server's: where the crystal goes, and her hidden there until she comes out of it.</summary>
        private void BeginRite(NPC npc, Player summoner)
        {
            // Held up over their head, as the summoning holds it.
            riteStart = summoner.Top + new Vector2(summoner.direction * 6f, -12f);
            FindRiteSpot(summoner, out riteFocus, out riteGround);

            npc.Bottom = riteFocus + new Vector2(0f, CoreBelow);
            npc.velocity = Vector2.Zero;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.direction = summoner.Center.X < riteFocus.X ? -1 : 1;

            Go(npc, Scene.Rite, RiteRise);
        }

        /// <summary>
        /// Ground to the summoner's front, near their own height, with room over it for all of her
        /// and for the fall: nearest to the ideal first, then the other side. If there is none
        /// (a tunnel), over the summoner, as high as the room lets her be.
        /// </summary>
        private static void FindRiteSpot(Player summoner, out Vector2 focus, out float ground)
        {
            int facing = summoner.direction == 0 ? 1 : summoner.direction;

            foreach (float away in RiteAway)
            {
                for (int k = 0; k < 2; k++)
                {
                    float x = summoner.Center.X + (k == 0 ? facing : -facing) * away;

                    if (!SlimeRite.Ground(x, summoner.Bottom.Y, out float y, water: false))
                        continue;

                    if (Math.Abs(y - summoner.Bottom.Y) > RiteClimb)
                        continue;

                    // From just over her crown, where she forms, down to the ground, as wide as she is.
                    Vector2 corner = new Vector2(x - 66f, y - FormHeight - 90f);

                    if (Collision.SolidCollision(corner, 132, (int)(FormHeight + 86f)))
                        continue;

                    focus = new Vector2(x, y - FormHeight);
                    ground = y;
                    return;
                }
            }

            ground = summoner.Bottom.Y;

            float rise = FormHeight;

            while (rise > 60f && Collision.SolidCollision(new Vector2(summoner.Center.X - 66f, summoner.Bottom.Y - rise - 90f), 132, (int)(rise + 30f)))
                rise -= 16f;

            focus = new Vector2(summoner.Center.X, summoner.Bottom.Y - rise);
        }

        // ---- run ------------------------------------------------------------------------------------

        private void RunRite(NPC npc, bool entered)
        {
            // Asked afresh every tick (RiteShot), so it says whether the player was in the shot
            // on the last tick of it.
            watching = false;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;

            Player summoner = Main.player[npc.target];

            if (summoner.active)
                npc.direction = summoner.Center.X < npc.Center.X ? -1 : 1;

            // Nothing small and hostile is let live near her while she comes.
            if (Authority)
                ClearScene();

            // Hung where she forms until she is whole; then hers to fall.
            bool hung = beat < RiteFall;
            npc.noGravity = hung;
            npc.noTileCollide = hung;
            npc.velocity.X = 0f;

            if (hung)
            {
                npc.velocity.Y = 0f;
                npc.Bottom = riteFocus + new Vector2(0f, CoreBelow);
            }

            int t = timer;

            switch (beat)
            {
                case RiteRise:
                    {
                        float p = Ease(t / (float)RiseTicks);
                        RiteShot(CrystalAt(), p, MathHelper.Lerp(1f, CrystalLean, p));

                        if (entered)
                            CrystalLeaves();

                        CrystalTrail(0.6f);

                        if (Authority && t >= RiseTicks)
                            Next(npc, RiteCrack);

                        break;
                    }

                case RiteCrack:
                    RiteShot(riteFocus, 1f, MathHelper.Lerp(CrystalLean, CrackLean, t / (float)CrackTicks));

                    for (int i = 0; i < CrackAt.Length; i++)
                    {
                        if (Crossed(CrackAt[i]))
                            Crack(i);
                    }

                    CrystalTrail(0.25f + 0.25f * CrackStage());
                    RiteLight(0.35f + 0.2f * CrackStage());

                    if (Authority && t >= CrackTicks)
                        Next(npc, RiteBurst);

                    break;

                case RiteBurst:
                    RiteShot(riteFocus, 1f, BurstLean, 0.2f);

                    if (entered)
                        CrystalBursts();

                    RiteLight(1.4f - 0.4f * t / BurstTicks);

                    if (Authority && t >= BurstTicks)
                        Next(npc, RiteMelt);

                    break;

                case RiteMelt:
                    RiteShot(riteFocus, 1f, BurstLean);
                    RiteLight(1f);
                    Motes(0.35f);

                    if (entered && !Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.6f, Volume = 0.55f }, riteFocus);

                    if (Authority && t >= MeltTicks)
                        Next(npc, RiteGlow);

                    break;

                case RiteGlow:
                    RiteShot(riteFocus, 1f, MathHelper.Lerp(BurstLean, GlowLean, t / (float)GlowTicks));
                    RiteLight(0.85f + 0.15f * (float)Math.Sin(t * 0.1f));
                    Motes(0.5f + 0.4f * t / GlowTicks);

                    // A slow pulse in it, and a breath in before she comes.
                    if (!Main.dedServ && t % 40 == 0 && t < GlowTicks - 20)
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { Pitch = 0.4f, Volume = 0.35f }, riteFocus);

                    if (!Main.dedServ && Crossed(GlowTicks - 24))
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.7f }, riteFocus);

                    if (Authority && t >= GlowTicks)
                        Next(npc, RiteGrow);

                    break;

                case RiteGrow:
                    // Out, fast, as she comes out of it: the size of her is the point.
                    RiteShot(npc.Center, 1f, HerLean, 0.2f);

                    if (entered)
                        SheComes(npc);

                    if (Authority && t >= GrowTicks)
                        Next(npc, RiteCrown);

                    break;

                case RiteCrown:
                    RiteShot(npc.Center, 1f, HerLean);

                    if (!Main.dedServ && t < CrownLands)
                        crownTilt = 0.3f * (1f - t / (float)CrownLands) * (float)Math.Sin(t * 0.55f);

                    if (Crossed(CrownLands))
                        CrownOnHer(npc);

                    if (Authority && t >= CrownTicks)
                        Next(npc, RiteFall);

                    break;

                case RiteFall:
                    RiteShot(npc.Center, 1f, FallLean);

                    // Let go in the air: not yet a speed of nothing, which is what standing on
                    // something reads as (vanilla's own trick in her slam).
                    if (entered && npc.velocity.Y == 0f)
                        npc.velocity.Y = 0.01f;

                    if (Authority && ((t > 2 && npc.velocity.Y == 0f) || t >= FallMost))
                        Next(npc, RiteRoar);

                    break;

                case RiteRoar:
                    RiteShot(npc.Center, 1f, 0f);

                    if (entered)
                    {
                        // Landed where this machine never saw her land (a late packet): the landing first.
                        if (!riteLanded)
                            RiteLanded(npc);

                        RiteRoars(npc);
                    }

                    Announce(npc);
                    Spit(npc, t);

                    if (Authority && t >= RoarTicks)
                        Next(npc, RiteHold);

                    break;

                case RiteHold:
                    RiteShot(npc.Center, 1f, 0f);
                    Announce(npc);

                    if (Authority && t >= HoldTicks)
                        Release(npc);

                    break;
            }
        }

        /// <summary>The arrival is over, on this machine (Restore): the camera home, and vanilla's opening pause cut to a second.</summary>
        private void EndRite(NPC npc)
        {
            if (watching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, ReturnTicks);

            watching = false;

            npc.noGravity = false;
            npc.noTileCollide = false;

            // Vanilla's first tick of her AI makes her sit a hundred ticks more before her first
            // move (ai[1] = -100); she has sat long enough. Its own marker that the first tick is
            // done is this, so it starts at rest instead, the move a second off.
            if (npc.localAI[0] == 0f)
                npc.localAI[0] = npc.lifeMax;

            if (Authority)
                npc.TargetClosest(false);
        }

        /// <summary>
        /// The server's, every tick of her arrival: anything hostile within the scene with less
        /// than ClearBelow life is killed on the spot, and gives nothing (PADGlobalNPC.KillForNothing).
        /// Whatever turns up while she comes goes the same way.
        /// </summary>
        private void ClearScene()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (Cleared(other) && Vector2.Distance(other.Center, riteFocus) <= SceneRange)
                    PADGlobalNPC.KillForNothing(other);
            }
        }

        /// <summary>
        /// Hostile and weak: not town folk, critters or anything friendly; not a boss or any part
        /// of a boss's fight (BossArrivalWait.OfAFight); nothing that cannot be hurt; and a worm
        /// that shares one life is taken by its head, and its body goes with it.
        /// </summary>
        private static bool Cleared(NPC other) =>
            other.active && other.life > 0 && other.life < ClearBelow
            && !other.friendly && !other.townNPC
            && !other.CountsAsACritter && !NPCID.Sets.CountsAsCritter[other.type]
            && !NPCID.Sets.TownCritter[other.type] && !NPCID.Sets.ActsLikeTownNPC[other.type]
            && !other.boss && !NPCID.Sets.ShouldBeCountedAsBoss[other.type] && !BossArrivalWait.OfAFight(other.type)
            && !other.immortal && !other.dontTakeDamage
            && (other.realLife < 0 || other.realLife == other.whoAmI);

        /// <summary>
        /// The shot, for a player near enough to be in it: the camera on the focus, the bars, the
        /// interface away, the player held, and the lean, as a framing that counts the player's
        /// own zoom. A lean of 0 leaves the zoom to ease back (and to the name's plate).
        /// </summary>
        private void RiteShot(Vector2 focus, float blend, float lean, float rate = Cinematic.LeanRate)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || Vector2.Distance(me.Center, riteFocus) > SceneRange)
                return;

            watching = true;

            PisPlayer.BlendCamera(focus, blend);
            PisPlayer.HideUI();
            Cinematic.Hold();
            Cinematic.Frame();

            if (lean > 0f)
                Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)), rate);
        }

        // ---- the crystal ------------------------------------------------------------------------------

        /// <summary>How many times it has cracked: 0 to 3.</summary>
        private int CrackStage()
        {
            if (scene != Scene.Rite || beat < RiteCrack)
                return 0;

            if (beat > RiteCrack)
                return CrackAt.Length;

            int stage = 0;

            foreach (int at in CrackAt)
            {
                if (timer >= at)
                    stage++;
            }

            return stage;
        }

        /// <summary>The shake of the last crack, dying away: world pixels.</summary>
        private Vector2 CrackJolt()
        {
            if (scene != Scene.Rite || beat != RiteCrack)
                return Vector2.Zero;

            for (int i = CrackAt.Length - 1; i >= 0; i--)
            {
                int since = timer - CrackAt[i];

                if (since < 0)
                    continue;

                if (since >= CrackShake)
                    break;

                float fade = 1f - since / (float)CrackShake;
                float size = 3f + 1.5f * i;

                return new Vector2((float)Math.Sin(timer * 2.9f), (float)Math.Cos(timer * 3.7f) * 0.6f) * (size * fade);
            }

            // Past the last crack, it trembles more and more, waiting.
            if (timer > CrackAt[CrackAt.Length - 1] + CrackShake)
            {
                float build = (timer - CrackAt[CrackAt.Length - 1] - CrackShake) / (float)(CrackTicks - CrackAt[CrackAt.Length - 1] - CrackShake);
                return new Vector2((float)Math.Sin(timer * 4.3f), (float)Math.Cos(timer * 5.1f)) * (1.4f * build);
            }

            return Vector2.Zero;
        }

        /// <summary>A weightless bob, for what hangs in the air: world pixels.</summary>
        private float Floating() => 4f * (float)Math.Sin(shapeClock * 0.07f);

        /// <summary>Where the crystal is: on its way up, over the arc; hanging, bobbing, shaken by its cracks.</summary>
        private Vector2 CrystalAt()
        {
            if (beat == RiteRise)
            {
                float p = 1f - (float)Math.Pow(1f - MathHelper.Clamp(timer / (float)RiseTicks, 0f, 1f), 3.0);
                Vector2 control = new Vector2(MathHelper.Lerp(riteStart.X, riteFocus.X, 0.25f), Math.Min(riteStart.Y, riteFocus.Y) - RiseArc);

                return (1f - p) * (1f - p) * riteStart + 2f * p * (1f - p) * control + p * p * riteFocus;
            }

            return riteFocus + new Vector2(0f, Floating()) + CrackJolt();
        }

        /// <summary>How it is turned: spinning on its way up and slowing to upright; rocked by the cracks.</summary>
        private float CrystalTurn()
        {
            if (beat == RiteRise)
            {
                float p = MathHelper.Clamp(timer / (float)RiseTicks, 0f, 1f);
                return MathHelper.TwoPi * 2f * (1f - (1f - p) * (1f - p));
            }

            return CrackJolt().X * 0.03f;
        }

        private float CrystalScale()
        {
            if (beat == RiteRise)
                return MathHelper.Lerp(1.1f, CrystalSize, Ease(timer / (float)RiseTicks));

            // A swell at each crack.
            float swell = 0f;

            foreach (int at in CrackAt)
            {
                int since = timer - at;

                if (since >= 0 && since < 10)
                    swell = Math.Max(swell, 0.08f * (1f - since / 10f));
            }

            return CrystalSize * (1f + swell);
        }

        /// <summary>The light coming out of it: a little on the way up, more with every crack, all at the end.</summary>
        private float CrystalLight()
        {
            if (beat == RiteRise)
                return 0.2f;

            int last = CrackAt[CrackAt.Length - 1] + CrackShake;
            float build = timer > last ? (timer - last) / (float)(CrackTicks - last) : 0f;

            return 0.3f + 0.2f * CrackStage() + 0.3f * build;
        }

        /// <summary>It leaves the hand: a clear ring, and a few sparkles off it.</summary>
        private void CrystalLeaves()
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.3f, Volume = 0.8f }, riteStart);
            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.2f, Volume = 0.5f }, riteStart);

            SparkShard.Spray(riteStart, Color.White, 6, 4f, 22f, 16, palette: QueenSlimeArt.Sparks);
        }

        /// <summary>Flecks of her colours left behind it, and off it as it hangs.</summary>
        private void CrystalTrail(float chance)
        {
            if (Main.dedServ || Main.rand.NextFloat() >= chance)
                return;

            Vector2 at = CrystalAt() + Main.rand.NextVector2Circular(5f, 6f) * CrystalScale();

            PADParticleHandler.SpawnParticle(new HoneyMote(at, Main.rand.NextVector2Circular(1.2f, 1.2f), QueenSlimeArt.Shade(Main.rand.NextFloat(3f)),
                Main.rand.NextFloat(0.4f, 0.8f), Main.rand.Next(20, 36), 0.02f));
        }

        /// <summary>A crack: the sound of it, the crystal jolted, splinters, and light out through it.</summary>
        private void Crack(int index)
        {
            if (Main.dedServ)
                return;

            float weight = (index + 1) / (float)CrackAt.Length;
            Vector2 at = riteFocus;

            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Pitch = -0.2f + 0.2f * index, Volume = 0.75f + 0.2f * weight }, at);
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.5f, Volume = 0.3f + 0.2f * weight }, at);

            float near = Near(at);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake((2f + 3f * weight) * near, 10 + 4 * index);

            for (int i = 0; i < 6 + 4 * index; i++)
            {
                int type = Main.rand.Next(3) switch
                {
                    0 => DustID.PinkCrystalShard,
                    1 => DustID.PurpleCrystalShard,
                    _ => DustID.BlueCrystalShard,
                };

                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(6f, 7f) * CrystalSize, type, Main.rand.NextVector2Circular(3.5f, 3.5f) - new Vector2(0f, 1f),
                    0, default, Main.rand.NextFloat(0.8f, 1.2f));

                dust.noGravity = Main.rand.NextBool(3);
            }

            SparkShard.Spray(at, Color.White, 4 + 2 * index, 5f, 26f + 10f * index, 14, palette: QueenSlimeArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Shade(index), (60f + 30f * index) / 160f, 14));
        }

        /// <summary>
        /// It bursts. A flash in her colours, the crystal thrown off in splinters, sparks, a ball of
        /// light; and what is left is its shape, white.
        /// </summary>
        private void CrystalBursts()
        {
            if (Main.dedServ)
                return;

            Vector2 at = riteFocus;
            float near = Near(at);

            if (near > 0f)
            {
                Cinematic.Flash(QueenSlimeArt.Pink, 0.45f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(7f * near, 22);
            }

            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.2f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.3f, Volume = 0.8f }, at);

            for (int i = 0; i < 34; i++)
            {
                int type = (i % 3) switch
                {
                    0 => DustID.PinkCrystalShard,
                    1 => DustID.PurpleCrystalShard,
                    _ => DustID.BlueCrystalShard,
                };

                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(5f, 6f) * CrystalSize, type, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(3f, 8f),
                    0, default, Main.rand.NextFloat(0.9f, 1.4f));

                dust.noGravity = Main.rand.NextBool();
            }

            SparkShard.Spray(at, Color.White, 16, 8f, 70f, 22, palette: QueenSlimeArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Pink, 280f / 160f, 26));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White, 120f / 160f, 16));

            for (int i = 0; i < 18; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new HoneyMote(at + way * 10f, way * Main.rand.NextFloat(3f, 7f), QueenSlimeArt.Shade(Main.rand.NextFloat(3f)),
                    Main.rand.NextFloat(0.6f, 1.1f), Main.rand.Next(24, 40), 0f));
            }
        }

        /// <summary>Its light on the world round it, in her colour of the moment.</summary>
        private void RiteLight(float strength)
        {
            if (Main.dedServ)
                return;

            Lighting.AddLight(riteFocus, QueenSlimeArt.Shade(shapeClock * 0.02f).ToVector3() * strength);
        }

        /// <summary>Light drawn in to it from round about, in her colours.</summary>
        private void Motes(float chance)
        {
            if (Main.dedServ || Main.rand.NextFloat() >= chance)
                return;

            Vector2 way = Main.rand.NextVector2Unit();
            Vector2 at = riteFocus + way * Main.rand.NextFloat(80f, 140f);

            PADParticleHandler.SpawnParticle(new HoneyMote(at, -way * Main.rand.NextFloat(3.5f, 5.5f), QueenSlimeArt.Shade(Main.rand.NextFloat(3f)),
                Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(20, 28), 0f));
        }

        // ---- her, out of it -------------------------------------------------------------------------

        /// <summary>How much of her size she is: 1 but while she comes out of the core, when she swells past it and settles back.</summary>
        private float RiteGrowth()
        {
            if (scene != Scene.Rite || beat != RiteGrow)
                return 1f;

            float t = MathHelper.Clamp(timer / (float)GrowTicks, 0f, 1f);

            return 1f - (1f - GrowFrom) * (float)Math.Exp(-4.5f * t) * (float)Math.Cos(7f * t);
        }

        /// <summary>The glow behind her while she comes out: all of it at first, gone as she settles.</summary>
        private float RiteBackLight()
        {
            if (beat != RiteGrow)
                return 0f;

            return 1f - Smooth(0.25f, 1f, timer / (float)GrowTicks);
        }

        /// <summary>How white she still is: all light as she swells, her colours coming into her after.</summary>
        private float RiteOverLight()
        {
            if (beat != RiteGrow)
                return 0f;

            return 1f - Smooth(0.35f, 0.9f, timer / (float)GrowTicks);
        }

        /// <summary>The core, still white, as she grows round it; and how big it is, from the light's size to its own.</summary>
        private float CoreWhite() => scene == Scene.Rite && beat == RiteGrow ? 1f - Smooth(0.3f, 1f, timer / (float)GrowTicks) : 0f;

        /// <summary>
        /// She comes out of it: a flash in her colours, a wet sound and a clear one, gel thrown off
        /// her that is all pulled back into her, sparks and a ball of light.
        /// </summary>
        private void SheComes(NPC npc)
        {
            ripple = 0.6f;

            if (Main.dedServ)
                return;

            Vector2 at = riteFocus;
            float near = Near(at);

            if (near > 0f)
            {
                Cinematic.Flash(QueenSlimeArt.Violet, 0.35f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(6f * near, 20);
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.4f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.5f, Volume = 0.8f }, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.4f, Volume = 0.35f }, at);

            for (int i = 0; i < 26; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new SlimeGlob(at + way * 16f, way * Main.rand.NextFloat(4f, 9f), Main.rand.NextFloat(0.7f, 1.3f), 110,
                    GelColour(), falls: false).HomeTo(npc, Main.rand.Next(18, 34)));
            }

            SparkShard.Spray(at, Color.White, 14, 7f, 80f, 20, palette: QueenSlimeArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, QueenSlimeArt.Violet, 360f / 160f, 24));
        }

        /// <summary>Whether her crown is on her (or on its way down to her).</summary>
        private bool CrownOn() => scene != Scene.Rite || beat >= RiteCrown;

        /// <summary>How far over her head the crown still is, falling to her: world pixels.</summary>
        private float CrownDrop()
        {
            if (scene != Scene.Rite || beat != RiteCrown || timer >= CrownLands)
                return 0f;

            float p = MathHelper.Clamp(timer / (float)CrownLands, 0f, 1f);
            return CrownFrom * (1f - p * p);
        }

        /// <summary>The crown lands on her: a clink, a glint, and she gives under it.</summary>
        private void CrownOnHer(NPC npc)
        {
            crownTilt = 0f;
            crownLiftSpeed = 2.2f;
            crownTiltSpeed += Main.rand.NextBool() ? 0.05f : -0.05f;
            Kick(-0.09f, 0f, 0.15f);

            if (Main.dedServ)
                return;

            Pose pose = Posed(npc);
            Texture2D crown = TextureAssets.Extra[ExtrasID.QueenSlimeCrown].Value;
            Vector2 top = pose.At(new Vector2(0f, -npc.height - 12f + QueenSlimeArt.CrownRise[pose.Frame])) - new Vector2(0f, crown.Height * 0.2f);

            SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.3f, Volume = 0.9f }, top);
            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = 0.2f, Volume = 0.7f }, top);

            PADParticleHandler.SpawnParticle(new CrownGlint(top, 40f, 30, new Color(255, 236, 250)));
        }

        /// <summary>She comes down on the ground: all of her weight, the ground heaved out from under her.</summary>
        private void RiteLanded(NPC npc)
        {
            if (riteLanded)
                return;

            riteLanded = true;
            Kick(-0.1f, 0f, 0.3f);

            if (Main.dedServ)
                return;

            QueenLand(npc, 1.1f, heave: true);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.6f, Volume = 0.45f }, npc.Bottom);
        }

        /// <summary>She roars (Cinematic.Roar: the rings, the dim, the shake), and something wet in it.</summary>
        private void RiteRoars(NPC npc)
        {
            Kick(0.14f, 0f, 0.4f);

            if (Main.dedServ)
                return;

            Cinematic.Roar(npc, RoarTicks, 1.1f, SoundID.Roar with { Pitch = 0.2f, Volume = 1f });
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.6f, Volume = 0.9f }, npc.Center);
        }

        /// <summary>Her name, once, on this machine: the plain plate, from the roar.</summary>
        private void Announce(NPC npc)
        {
            if (plated || Main.dedServ)
                return;

            plated = true;
            BossIntro.PlayNameOnly(npc.whoAmI, RitePlate);
        }

        /// <summary>Gel shaken off the top of her while she roars, and left to fall.</summary>
        private void Spit(NPC npc, int t)
        {
            if (Main.dedServ || t >= 60 || t % 3 != 0)
                return;

            for (int i = 0; i < 2; i++)
            {
                float across = Main.rand.NextFloat(-0.42f, 0.42f);
                Vector2 from = new Vector2(npc.Center.X + across * npc.width, npc.position.Y + npc.height * 0.3f);
                Vector2 velocity = new Vector2(across * Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(-7f, -3f));

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.6f, 1.1f), 70, GelColour()));
            }
        }

        // ---- drawn ----------------------------------------------------------------------------------

        /// <summary>
        /// The arrival before she is herself: the crystal and its cracks and its light; then its
        /// shape in white, running into her core's, in a glow of her colours.
        /// </summary>
        private void DrawRiteLight(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (QueenSlimeArt.Morph == null || QueenSlimeArt.MorphHalo == null || QueenSlimeArt.Cracks == null)
                return;

            Texture2D halo = QueenSlimeArt.MorphHalo.Value;
            Texture2D morph = QueenSlimeArt.Morph.Value;

            if (beat <= RiteCrack)
            {
                Main.instance.LoadItem(ItemID.QueenSlimeCrystal);
                Texture2D crystal = TextureAssets.Item[ItemID.QueenSlimeCrystal].Value;

                Vector2 at = CrystalAt() - screenPos;
                float turn = CrystalTurn();
                float size = CrystalScale();
                float light = CrystalLight();
                int stage = CrackStage();

                Rectangle haloFrame = QueenSlimeArt.MorphHaloFrame(0);
                Rectangle shape = QueenSlimeArt.MorphFrame(0);

                // Its glow, in her colours.
                DrawGlow(spriteBatch, halo, haloFrame, at, haloFrame.Size() * 0.5f, turn, new Vector2(size), SpriteEffects.None,
                    0.35f + 0.55f * light, 1.1f, 1.6f, 2.3f);

                spriteBatch.Draw(crystal, at, null, Color.White, turn, crystal.Size() * 0.5f, size, SpriteEffects.None, 0f);

                if (stage > 0)
                {
                    Rectangle cracks = QueenSlimeArt.CrackFrame(stage - 1);
                    spriteBatch.Draw(QueenSlimeArt.Cracks.Value, at, cracks, Color.White, turn, cracks.Size() * 0.5f, size, SpriteEffects.None, 0f);
                }

                // The light inside it, coming up.
                spriteBatch.Draw(morph, at, shape, QueenSlimeArt.Additive(Color.White, 0.3f * light), turn, shape.Size() * 0.5f, size, SpriteEffects.None, 0f);
                return;
            }

            // Its shape, white, and running into hers.
            int frame;
            Vector2 scale;
            float glow;
            float wobble = 0f;

            if (beat == RiteBurst)
            {
                float p = timer / (float)BurstTicks;
                float pop = (float)Math.Sin(Math.Min(1f, timer / 10f) * MathHelper.Pi) * (1f - p);

                frame = 0;
                scale = new Vector2(CrystalSize * (1f + 0.3f * pop));
                glow = 1.25f - 0.25f * p;
            }
            else if (beat == RiteMelt)
            {
                float p = MathHelper.Clamp(timer / (float)MeltTicks, 0f, 1f);
                float size = MathHelper.Lerp(CrystalSize, CoreGlowSize, Ease(p));
                float liquid = 0.1f * (float)Math.Sin(p * MathHelper.Pi * 4f) * (float)Math.Sin(p * MathHelper.Pi);

                frame = Math.Min(QueenSlimeArt.MorphFrames - 1, (int)(p * QueenSlimeArt.MorphFrames));
                scale = new Vector2(size * (1f + liquid), size * (1f - liquid));
                glow = 1f;
                wobble = 0.06f * (float)Math.Sin(p * MathHelper.Pi * 3f);
            }
            else
            {
                float breath = (float)Math.Sin(shapeClock * 0.12f);

                frame = QueenSlimeArt.MorphFrames - 1;
                scale = new Vector2(CoreGlowSize * (1f + 0.03f * breath), CoreGlowSize * (1f - 0.02f * breath));
                glow = 0.85f + 0.15f * breath;
                wobble = 0.04f * (float)Math.Sin(shapeClock * 0.05f);
            }

            Vector2 where = riteFocus + new Vector2(0f, Floating()) - screenPos;
            Rectangle haloAt = QueenSlimeArt.MorphHaloFrame(frame);
            Rectangle shapeAt = QueenSlimeArt.MorphFrame(frame);
            Vector2 haloOrigin = haloAt.Size() * 0.5f;

            // The glow of a thing made of light, in her colours.
            DrawGlow(spriteBatch, halo, haloAt, where, haloOrigin, wobble, scale, SpriteEffects.None, glow, 1.2f, 1.8f, 2.8f);

            // And the thing, white.
            spriteBatch.Draw(morph, where, shapeAt, Color.White, wobble, shapeAt.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }
    }
}
