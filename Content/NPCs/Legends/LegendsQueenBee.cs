using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>Where the Queen Bee is in one of her own scenes. None is the fight.</summary>
    public enum BeeScene : byte
    {
        None,

        // Her arrival, in every world.
        Pan,
        Emerge,
        Level,
        Shake,
        Roar,
        Hold,

        // The honey, in Legends, at a little over a third of her life.
        Cry,
        Seek,
        Plunge,
        Soak,
        Surge,

        // Her death, in every world.
        Nest,
        Settle,
        Birth,
        Swell,
        Burst,
    }

    /// <summary>
    /// The Queen Bee.
    ///
    /// IN EVERY WORLD
    ///
    ///     ARRIVAL   the camera goes to the nearest honey (or to the pool she makes, where
    ///               there is none near: QueenBeeGround); she comes up out of it with the
    ///               honey running off her; flies up level with the player, faintly gold;
    ///               shakes herself dry; roars, and her name comes up with it; the camera
    ///               walks home and the fight starts
    ///     DEATH     she does not die when she is killed: she goes to the nearest flat piece
    ///               of hive (any flat ground, failing that), settles on it, strains, and
    ///               leaves a larva there; swells, and bursts, her pieces thrown out every
    ///               way from where she was; then the loot
    ///     LOOKS     gold ghosts behind her dashes and the air torn past her, honey off her
    ///               when she hangs, pollen off her wings, a light on her, a glow on her
    ///               stingers, and a puff of honey where each bee comes out of her
    ///
    /// IN LEGENDS
    ///
    ///     NUMBERS   her own life, her body a third again as hard, her stingers three
    ///               quarters again, and three quarters her size
    ///     DASHES    vanilla's charging run, the same number of dashes, but she lines up for
    ///               each half again as fast and comes out of each half again as fast; each
    ///               dash carries her twice as far past the player, and lets out five to ten
    ///               bees on the way (Charge)
    ///     HONEY     at 35% of her life she cries out, goes to the nearest honey (or makes
    ///               some), goes down into it and comes up gold: a pulsing glow with bands of
    ///               light climbing her and a pale gold glint sliding over her (HoneyGlint),
    ///               fifteen more defense, and twenty life back a second, never past the 35%,
    ///               for the rest of the fight
    ///
    /// The server runs every scene and sends where it is and when (SendExtraAI); every
    /// machine plays them from that. A scene is untouchable and harmless.
    /// </summary>
    public class LegendsQueenBee : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.QueenBee;

        // ---- Legends numbers -------------------------------------------------------------

        public const float BodyDamageScale = 1.3f;
        public const float StingerDamageScale = 1.75f;
        public const float SizeScale = 0.75f;

        /// <summary>How much faster she lines up for a dash, and comes out of one: all of that motion's speed by this, its pushes by this squared.</summary>
        private const float Pace = 1.5f;

        /// <summary>How far past the player a dash carries her, against vanilla.</summary>
        private const float Reach = 2f;

        /// <summary>Bees let out on each dash, and the ticks between them.</summary>
        private const int BeesLeast = 5;
        private const int BeesMost = 10;
        private const int BeeEvery = 4;

        /// <summary>The honey: when, how much defense, how much life a second, how often.</summary>
        public const float HoneyAt = 0.35f;
        public const int HoneyDefense = 15;
        public const int HoneyLife = 20;
        private const int RegenEvery = 60;

        // ---- the arrival -------------------------------------------------------------------

        private const int PanTicks = 70;
        private const int EmergeTicks = 90;
        private const int LevelTicks = 70;
        private const int ShakeTicks = 48;
        private const int RoarTicks = 90;

        /// <summary>
        /// Held on her while her plate finishes, as the King's is: the plain plate's rise, fly
        /// and settle, less what the roar has spent, and a moment. Follows the plate.
        /// </summary>
        private const int HoldTicks = BossIntro.PlainPlateTicks - BossIntro.PlainLook - RoarTicks + 5;

        /// <summary>The camera's walk home, and the zoom let go over the same.</summary>
        private const int ReturnTicks = 60;

        /// <summary>How far from the player her level is, to one side of them.</summary>
        private const float LevelGap = 360f;

        /// <summary>A player this near her or the honey is in the scene.</summary>
        private const float SceneRange = 2600f;

        /// <summary>How far round the player honey is looked for when she comes, in tiles.</summary>
        private const int ArrivalReachX = 90;
        private const int ArrivalReachY = 60;

        /// <summary>Her name's plate: the plain one, a lean in, let go as the camera walks home.</summary>
        private static readonly BossIntroProfile Plate = new()
        {
            ZoomIn = 1.15f,
            ReturnTicks = ReturnTicks,
        };

        // ---- the honey ---------------------------------------------------------------------

        private const int CryTicks = 45;
        private const int SeekMost = 240;
        private const int PlungeTicks = 26;
        private const int SoakTicks = 60;
        private const int SurgeTicks = 36;

        /// <summary>How far over the honey she comes to before she goes in, and how high she comes out.</summary>
        private const float SeekAbove = 160f;
        private const float SurgeTo = 170f;

        /// <summary>How far round her honey is looked for, in tiles.</summary>
        private const int PhaseReachX = 180;
        private const int PhaseReachY = 110;

        // ---- the death ---------------------------------------------------------------------

        private const int NestMost = 300;
        private const int SettleTicks = 40;
        private const int BirthTicks = 100;
        private const int SwellTicks = 70;

        /// <summary>
        /// After the burst, before the server takes her away: long enough for every machine to
        /// have been told of the burst and played it while she is still there to play it from.
        /// </summary>
        private const int BurstTicks = 30;

        /// <summary>The camera starts home this far into the burst, before she is gone.</summary>
        private const int HomeAt = 22;

        /// <summary>The larva comes out of her over these ticks of the birth, and is there from the last.</summary>
        private const int LarvaFrom = 60;
        private const int LarvaAt = 84;

        /// <summary>The strains of the birth: when each starts, and how long it is.</summary>
        private static readonly int[] Strains = { 10, 36, 62 };
        private const int StrainTicks = 18;

        /// <summary>How far round her a nest is looked for, in tiles.</summary>
        private const int NestReachX = 150;
        private const int NestReachY = 90;

        private const int DeathPan = 40;

        // ---- her sheet ---------------------------------------------------------------------
        //
        // NPC_222 is 172 x 1824: twelve frames of 152, the first four her dash (side on,
        // head to the left) and the rest her hover (face on, the comb of her abdomen at the
        // bottom). Measured: her opaque rows run from 60 above the middle of a frame to 74
        // below it; on a dash frame her abdomen is 54 behind the middle and 19 down, on a
        // hover frame 9 aside and 54 down. Vanilla draws the frame's middle at her middle
        // across and four under her feet less half a frame up (NPCAddHeight is 0 for her).

        private const float FrameHalf = 76f;
        private const float SpriteTop = 60f;
        private const float SpriteBottom = 74f;

        // ---- colours -----------------------------------------------------------------------

        private static readonly Color Gold = new Color(255, 205, 90);
        private static readonly Color HoneyTint = new Color(255, 178, 40);
        private static readonly Color Heat = new Color(255, 245, 205);

        /// <summary>The bands that climb her when she is honeyed: gold near to white.</summary>
        private static readonly Color Band = new Color(255, 242, 170);

        // ---- state -------------------------------------------------------------------------

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        private BeeScene scene;
        private int timer;

        /// <summary>The scene this machine last played the start of, so each start is played once, on every machine.</summary>
        private BeeScene seen;

        /// <summary>Set when the server moves her on this tick, so the clock starts the next scene at nothing.</summary>
        private bool moved;

        /// <summary>The top of the honey she came out of or goes into; or the ground under the middle of the larva.</summary>
        private Vector2 spot;

        /// <summary>The larva's bottom middle tile.</summary>
        private Point nest;

        /// <summary>Whether there is honey (arrival, honey) or a nest (death) to go to.</summary>
        private bool hasSpot;

        /// <summary>Which side of the player she comes up level with: the server's, fixed as she sets off, so she does not swap sides on the way.</summary>
        private int levelSide;

        private bool honeyed;
        private bool dying;

        /// <summary>The server's final death: CheckDead lets it through.</summary>
        private bool ending;

        /// <summary>Where a move of hers started, on this machine.</summary>
        private Vector2 from;

        /// <summary>Her damage as she was made, for after a scene has taken it.</summary>
        private int baseDamage = -1;

        private int regen;
        private int beesLeft;
        private int beeClock;

        /// <summary>This machine's player was in the shot on its last tick: the camera is walked home for them when it ends.</summary>
        private bool watching;

        private bool plated;
        private int deathClock;

        /// <summary>Her middle a tick ago, for when she crosses the top of the honey.</summary>
        private float lastY;

        /// <summary>Where she has been, newest first, for the gold behind her dashes.</summary>
        private const int TrailLength = 7;
        private readonly Vector2[] trail = new Vector2[TrailLength];
        private int trailCount;

        private bool InScene => scene != BeeScene.None;

        /// <summary>Whether she has been down into the honey and come up gold (HoneyGlint asks).</summary>
        internal bool Honeyed => honeyed;

        // ---- made ----------------------------------------------------------------------------

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            // Before vanilla scales her hitbox by her size, which it does straight after.
            entity.scale *= SizeScale;
            entity.damage = (int)(entity.damage * BodyDamageScale);
        }

        /// <summary>
        /// The server's, as she is made: whoever called her (the one the boss spawn names, or
        /// the nearest), where she comes from, and the start of it.
        /// </summary>
        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            baseDamage = npc.damage;

            if (source is EntitySource_BossSpawn spawn && spawn.Target is Player summoner && summoner.active)
                npc.target = summoner.whoAmI;
            else if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active)
                npc.TargetClosest(false);

            Player target = Main.player[npc.target];

            hasSpot = target.active
                && (QueenBeeGround.FindHoney(target.Center, ArrivalReachX, ArrivalReachY, out spot)
                    || QueenBeeGround.BuildPool(target, out spot));

            if (hasSpot)
            {
                npc.Center = spot + new Vector2(0f, Sink(npc));
                Go(npc, BeeScene.Pan);
            }
            else
            {
                // Nothing wet near and nowhere to make it: she comes from where vanilla put
                // her, straight to the player's level.
                levelSide = SideOf(npc, target);
                Go(npc, BeeScene.Level);
            }

            npc.velocity = Vector2.Zero;
            Hush(npc);
        }

        // ---- ai ------------------------------------------------------------------------------

        public override bool PreAI(NPC npc)
        {
            if (baseDamage < 0)
                baseDamage = npc.damage;

            if (dying)
            {
                RunScene(npc);
                return false;
            }

            // A scene has just ended here: she is given back to herself.
            if (!InScene && seen != BeeScene.None)
                Leave(npc);

            if (InScene)
            {
                RunScene(npc);
                return false;
            }

            if (!PADWorld.Legends)
                return true;

            if (Authority && !honeyed && npc.life <= npc.lifeMax * HoneyAt)
            {
                BeginHoney(npc);
                RunScene(npc);
                return false;
            }

            Regen(npc);

            if (npc.ai[0] == 0f)
                return Charge(npc);

            return true;
        }

        public override void PostAI(NPC npc)
        {
            lastY = npc.Center.Y;

            for (int i = TrailLength - 1; i > 0; i--)
                trail[i] = trail[i - 1];

            trail[0] = npc.Center;
            trailCount = Math.Min(trailCount + 1, TrailLength);

            if (!Main.dedServ)
                Airs(npc);
        }

        /// <summary>Untouchable and harmless, and kept from despawning, for the length of a scene.</summary>
        private static void Hush(NPC npc)
        {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.localAI[0] = 0f;

            if (npc.timeLeft < 600)
                npc.timeLeft = 600;
        }

        /// <summary>A scene is over: her damage back, touchable again, level, and the camera walked home if it was on her.</summary>
        private void Leave(NPC npc)
        {
            BeeScene was = seen;
            seen = BeeScene.None;

            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.damage = baseDamage;
            npc.rotation = 0f;

            if (was == BeeScene.Hold && watching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, ReturnTicks);

            watching = false;
        }

        /// <summary>On to a scene, from its first tick. The server's call, and sent.</summary>
        private void Go(NPC npc, BeeScene next)
        {
            scene = next;
            timer = 0;
            moved = true;
            npc.netUpdate = true;
        }

        private void RunScene(NPC npc)
        {
            bool entered = seen != scene;
            seen = scene;
            moved = false;

            // Asked afresh each tick (Shot), so it says whether the player was in it on the last.
            watching = false;

            Hush(npc);

            if (scene != BeeScene.Shake)
                npc.rotation = 0f;

            int t = timer;
            Player target = Main.player[npc.target];

            switch (scene)
            {
                case BeeScene.Pan: Pan(npc, t); break;
                case BeeScene.Emerge: Emerge(npc, t); break;
                case BeeScene.Level: Level(npc, target, t, entered); break;
                case BeeScene.Shake: Shake(npc, target, t, entered); break;
                case BeeScene.Roar: Roar(npc, target, t, entered); break;
                case BeeScene.Hold: HoldOn(npc, target, t); break;

                case BeeScene.Cry: Cry(npc, target, t, entered); break;
                case BeeScene.Seek: Seek(npc, t); break;
                case BeeScene.Plunge: Plunge(npc, t, entered); break;
                case BeeScene.Soak: Soak(npc, t); break;
                case BeeScene.Surge: Surge(npc, t, entered); break;

                case BeeScene.Nest: NestTo(npc, target, t); break;
                case BeeScene.Settle: Settle(npc, target, t, entered); break;
                case BeeScene.Birth: Birth(npc, t); break;
                case BeeScene.Swell: Swell(npc, t, entered); break;
                case BeeScene.Burst: BurstOut(npc, t, entered); break;
            }

            if (dying)
                deathClock++;

            if (!moved)
                timer++;
        }

        // ---- the arrival, run ------------------------------------------------------------------

        /// <summary>Down in the honey, still, while the camera goes over and the honey stirs.</summary>
        private void Pan(NPC npc, int t)
        {
            Place(npc, spot + new Vector2(0f, Sink(npc)));
            Shot(npc, spot - new Vector2(0f, 30f), Ease(t / (float)PanTicks), 1.2f);

            if (!Main.dedServ)
            {
                float stir = t / (float)PanTicks;

                if (Main.rand.NextFloat() < 0.12f + 0.55f * stir)
                    Bubble(spot + new Vector2(Main.rand.NextFloat(-44f, 44f), 0f), 0.6f + stir);

                if (t % 18 == 0)
                    Ripple(spot, 10f, 60f + 40f * stir, 40, 0.35f + 0.35f * stir);

                if (t == 12 || t == 44)
                    SoundEngine.PlaySound(SoundID.Item85 with { Pitch = -0.6f, Volume = 0.55f }, spot);

                if (t == 40)
                    SoundEngine.PlaySound(SoundID.Zombie125 with { Pitch = -0.8f, Volume = 0.35f }, spot);
            }

            if (Authority && t >= PanTicks)
                Go(npc, BeeScene.Emerge);
        }

        /// <summary>Up out of it, slow, the honey running off her back into it.</summary>
        private void Emerge(NPC npc, int t)
        {
            Vector2 low = spot + new Vector2(0f, Sink(npc));
            Vector2 high = spot - new Vector2(0f, 60f + Below(npc));

            Place(npc, Vector2.Lerp(low, high, Ease(t / (float)EmergeTicks)));

            Vector2 focus = new Vector2(spot.X, Math.Min(spot.Y - 30f, npc.Center.Y));
            Shot(npc, focus, 1f, 1.25f);

            if (Authority && t >= EmergeTicks)
                EmergeDone(npc);

            if (Main.dedServ)
                return;

            float top = DrawCentre(npc).Y - SpriteTop * npc.scale;
            float bottom = DrawCentre(npc).Y + SpriteBottom * npc.scale;

            // Her head breaks the surface, and then all of her is out: worked from where the
            // emergence puts her on this tick and the last, the same on every machine.
            if (Crossed(TopAt(npc, t - 1), TopAt(npc, t)))
                Breach(npc, 0.7f);

            if (Crossed(BottomAt(npc, t - 1), BottomAt(npc, t)))
            {
                Breach(npc, 1f);
                SoundEngine.PlaySound(SoundID.DripSplash with { Volume = 0.8f, Pitch = -0.3f }, spot);
            }

            // Running off her, from whatever of her is out, back into the pool.
            if (top < spot.Y && t % 2 == 0)
            {
                float lowest = Math.Min(bottom, spot.Y) - 6f;
                Vector2 at = new Vector2(npc.Center.X + Main.rand.NextFloat(-24f, 24f) * npc.scale,
                    MathHelper.Lerp(top + 20f * npc.scale, lowest, Main.rand.NextFloat(0.4f, 1f)));

                Drip(at, Main.rand.NextFloat(0.5f, 0.95f));
            }

            if (t % 10 == 0 && top < spot.Y)
                Ripple(spot, 20f, 70f, 36, 0.45f);
        }

        /// <summary>The end of the emergence, the server's: which side of the player she will be level on, and off.</summary>
        private void EmergeDone(NPC npc)
        {
            levelSide = SideOf(npc, Main.player[npc.target]);
            Go(npc, BeeScene.Level);
        }

        /// <summary>Up to the player's level, drying, faintly gold.</summary>
        private void Level(NPC npc, Player target, int t, bool entered)
        {
            if (entered)
            {
                from = npc.Center;

                if (!Main.dedServ)
                    SoundEngine.PlaySound(SoundID.Zombie125 with { Pitch = -0.2f, Volume = 0.6f }, npc.Center);
            }

            Vector2 goal = LevelPoint(npc, target);

            Place(npc, Vector2.Lerp(from, goal, Ease(t / (float)LevelTicks)));
            Face(npc, target);

            // From the honey the shot is already on her; from nowhere it goes to her now.
            Shot(npc, npc.Center, hasSpot ? 1f : Ease(t / 40f), 1.15f);

            if (!Main.dedServ && Main.rand.NextFloat() < 0.5f * (1f - t / (float)LevelTicks))
                Drip(Abdomen(npc) + Main.rand.NextVector2Circular(14f, 6f) * npc.scale, Main.rand.NextFloat(0.4f, 0.8f));

            if (Authority && t >= LevelTicks)
                Go(npc, BeeScene.Shake);
        }

        /// <summary>She shakes herself dry: side to side and rocking, the honey flung off her.</summary>
        private void Shake(NPC npc, Player target, int t, bool entered)
        {
            float decay = MathHelper.Clamp(1f - t / (float)ShakeTicks, 0f, 1f);

            Place(npc, LevelPoint(npc, target) + new Vector2(Jitter(t) * decay, 0f));
            npc.rotation = (float)Math.Sin(t * 1.25f + 0.5f) * 0.22f * decay;
            Face(npc, target);

            Shot(npc, npc.Center, 1f, 1f);

            if (Main.dedServ)
                return;

            if (entered)
                SoundEngine.PlaySound(SoundID.Zombie125 with { Pitch = 0.35f, Volume = 0.8f }, npc.Center);

            if (t == 4 || t == 12 || t == 20)
                SoundEngine.PlaySound(SoundID.DripSplash with { Volume = 0.7f, Pitch = Main.rand.NextFloat(-0.2f, 0.2f) }, npc.Center);

            if (t < 26 && t % 2 == 0)
            {
                Vector2 middle = DrawCentre(npc) + new Vector2(0f, 20f * npc.scale);

                for (int i = 0; i < 3; i++)
                {
                    Vector2 way = Main.rand.NextVector2Unit();
                    PADParticleHandler.SpawnParticle(new SlimeGlob(middle + way * 30f * npc.scale,
                        way * Main.rand.NextFloat(3f, 7.5f) - new Vector2(0f, 1.5f), Main.rand.NextFloat(0.45f, 0.85f),
                        Main.rand.Next(50, 80), HoneyTint));
                }

                PADParticleHandler.SpawnParticle(new HoneyMote(middle + Main.rand.NextVector2Circular(40f, 30f) * npc.scale,
                    Main.rand.NextVector2Circular(2f, 2f), Gold * 0.8f, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(24, 40), 0.02f));
            }

            if (Authority && t >= ShakeTicks)
                Go(npc, BeeScene.Roar);
        }

        /// <summary>The roar, loud, and her name coming up with it.</summary>
        private void Roar(NPC npc, Player target, int t, bool entered)
        {
            Place(npc, LevelPoint(npc, target) + new Vector2(0f, Bob(t)));
            Face(npc, target);

            Shot(npc, npc.Center, 1f, 0f);

            if (!Main.dedServ && entered)
            {
                Cinematic.Roar(npc, RoarTicks, 1.25f, SoundID.ForceRoar with { Pitch = 0.3f, Volume = 0.9f });
                SoundEngine.PlaySound(SoundID.Zombie125 with { Pitch = -0.35f, Volume = 1f }, npc.Center);
            }

            Announce(npc);

            if (Authority && t >= RoarTicks)
                Go(npc, BeeScene.Hold);
        }

        /// <summary>Held on her while the name settles. Then the fight, and the camera goes home.</summary>
        private void HoldOn(NPC npc, Player target, int t)
        {
            Place(npc, LevelPoint(npc, target) + new Vector2(0f, Bob(t + RoarTicks)));
            Face(npc, target);

            Shot(npc, npc.Center, 1f, 0f);
            Announce(npc);

            if (!Authority || t < HoldTicks)
                return;

            // Over her first: up to hang over them and let out her bees, rather than a dash
            // at someone whose camera is still on its way back to them.
            npc.ai[0] = 2f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;

            scene = BeeScene.None;
            moved = true;
            npc.netUpdate = true;
        }

        /// <summary>Her name, once, on this machine: the plain plate, from the roar.</summary>
        private void Announce(NPC npc)
        {
            if (plated || Main.dedServ)
                return;

            plated = true;
            BossIntro.PlayNameOnly(npc.whoAmI, Plate);
        }

        /// <summary>Where she hangs for the player: to her side of them, level with their middle.</summary>
        private Vector2 LevelPoint(NPC npc, Player target)
        {
            if (target == null || !target.active)
                return npc.Center;

            int side = levelSide != 0 ? levelSide : SideOf(npc, target);

            return target.Center + new Vector2(side * LevelGap, -10f);
        }

        /// <summary>Which side of the player she is on; straight over or under them, the side they have their back to.</summary>
        private static int SideOf(NPC npc, Player target)
        {
            if (target == null || !target.active)
                return 1;

            int side = Math.Sign(npc.Center.X - target.Center.X);

            return side != 0 ? side : -target.direction;
        }

        /// <summary>
        /// The shot, for a player near enough to be in it: the camera on the focus, the
        /// bars, the interface away, the player held, and the lean, as a framing that
        /// counts the player's own zoom. A lean of 0 leaves it to the name's plate.
        /// </summary>
        private void Shot(NPC npc, Vector2 focus, float blend, float lean)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            if (Vector2.Distance(me.Center, focus) > SceneRange && Vector2.Distance(me.Center, npc.Center) > SceneRange)
                return;

            watching = true;

            PisPlayer.BlendCamera(focus, blend);
            PisPlayer.HideUI();
            Cinematic.Hold();
            Cinematic.Frame();

            if (lean > 0f)
                Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)));
        }

        // ---- the honey, run --------------------------------------------------------------------

        /// <summary>The server's: where the honey is, or where it will be, and the cry.</summary>
        private void BeginHoney(NPC npc)
        {
            npc.TargetClosest(false);
            Player target = Main.player[npc.target];

            hasSpot = QueenBeeGround.FindHoney(npc.Center, PhaseReachX, PhaseReachY, out spot)
                || (target.active && QueenBeeGround.BuildPool(target, out spot));

            Go(npc, BeeScene.Cry);
        }

        /// <summary>A cry, not loud, and a stop.</summary>
        private void Cry(NPC npc, Player target, int t, bool entered)
        {
            npc.velocity *= 0.9f;
            Face(npc, target);

            if (entered && !Main.dedServ)
                Cinematic.Roar(npc, 50, 0.45f, SoundID.Zombie125 with { Pitch = -0.15f, Volume = 0.85f });

            if (Authority && t >= CryTicks)
                Go(npc, hasSpot ? BeeScene.Seek : BeeScene.Surge);
        }

        /// <summary>To the honey, fast, to hang over it.</summary>
        private void Seek(NPC npc, int t)
        {
            Vector2 goal = spot - new Vector2(0f, SeekAbove);

            Approach(npc, goal, 0.12f, 24f);

            if (Math.Abs(npc.velocity.X) > 0.5f)
            {
                npc.direction = npc.velocity.X > 0f ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            if (Authority && (Vector2.Distance(npc.Center, goal) < 24f || t >= SeekMost))
                Go(npc, BeeScene.Plunge);
        }

        /// <summary>Down into it, faster as she goes.</summary>
        private void Plunge(NPC npc, int t, bool entered)
        {
            if (entered)
                from = npc.Center;

            float p = MathHelper.Clamp(t / (float)PlungeTicks, 0f, 1f);

            Place(npc, Vector2.Lerp(from, spot + new Vector2(0f, Sink(npc)), p * p));

            if (!Main.dedServ && Crossed(lastY, npc.Center.Y))
                Splash(npc, 1f, false);

            if (Authority && t >= PlungeTicks)
                Go(npc, BeeScene.Soak);
        }

        /// <summary>Under. The honey bubbles, and something gold comes up through it.</summary>
        private void Soak(NPC npc, int t)
        {
            Place(npc, spot + new Vector2(0f, Sink(npc)));

            if (!Main.dedServ)
            {
                float rise = t / (float)SoakTicks;

                Lighting.AddLight(spot, new Vector3(0.9f, 0.65f, 0.15f) * (0.3f + 0.9f * rise));

                if (Main.rand.NextFloat() < 0.2f + 0.6f * rise)
                    Bubble(spot + new Vector2(Main.rand.NextFloat(-40f, 40f), 0f), 0.7f + rise);

                if (t % 12 == 0)
                    Ripple(spot, 12f, 70f + 50f * rise, 36, 0.4f + 0.5f * rise);

                if (t == 30)
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 0.6f }, spot);
            }

            if (Authority && t >= SoakTicks)
            {
                honeyed = true;
                Go(npc, BeeScene.Surge);
            }
        }

        /// <summary>Up out of it, gold: the burst, the flash, and from here the glow and the climbing light.</summary>
        private void Surge(NPC npc, int t, bool entered)
        {
            if (entered)
            {
                honeyed = true;
                from = hasSpot ? spot + new Vector2(0f, Sink(npc)) : npc.Center;

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.25f, Volume = 1f }, npc.Center);
                    Splash(npc, 1.3f, true);

                    float near = 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, npc.Center) - 800f) / 1200f, 0f, 1f);

                    if (near > 0f)
                        Cinematic.Flash(Gold, 0.3f * near);
                }
            }

            Vector2 goal = hasSpot ? spot - new Vector2(0f, SurgeTo) : from - new Vector2(0f, 60f);
            float p = MathHelper.Clamp(t / (float)SurgeTicks, 0f, 1f);

            Place(npc, Vector2.Lerp(from, goal, 1f - (1f - p) * (1f - p) * (1f - p)));

            if (!Authority || t < SurgeTicks)
                return;

            // Straight into a charging run.
            npc.ai[0] = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.localAI[0] = 0f;

            scene = BeeScene.None;
            moved = true;
            npc.netUpdate = true;
        }

        /// <summary>The server's, once a second while she is honeyed: twenty life, never past the 35%.</summary>
        private void Regen(NPC npc)
        {
            if (!honeyed || !Authority)
                return;

            if (++regen < RegenEvery)
                return;

            regen = 0;

            int cap = (int)(npc.lifeMax * HoneyAt);

            if (npc.life >= cap)
                return;

            int gain = Math.Min(HoneyLife, cap - npc.life);

            npc.life += gain;
            npc.HealEffect(gain, true);
            npc.netUpdate = true;
        }

        /// <summary>Honeyed, she is harder to hurt.</summary>
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (PADWorld.Legends && honeyed)
                modifiers.Defense.Flat += HoneyDefense;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !InScene && !dying;

        // ---- the dashes ----------------------------------------------------------------------------

        /// <summary>
        /// Vanilla's charging run (aiStyle 43, ai[0] 0), as vanilla has it, with three
        /// differences: she lines up for a dash and comes out of one half again as fast (the
        /// speeds of that motion by Pace, its pushes by Pace squared, its slowing by the
        /// power Pace); a dash carries her twice as far past the player; and each dash lets
        /// out five to ten bees. The part vanilla runs ahead of every state is here too.
        /// Returns whether vanilla should run this tick after all, which it does for a state
        /// this has just handed it.
        /// </summary>
        private bool Charge(NPC npc)
        {
            if (Main.expertMode)
                npc.defense = npc.defDefense + (int)(20f * (1f - npc.life / (float)npc.lifeMax));

            if (npc.target < 0 || npc.target == 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
                npc.TargetClosest();

            Player target = Main.player[npc.target];
            bool dead = target.dead;

            float enraged = 0f;

            if (npc.position.Y / 16f < Main.worldSurface)
                enraged++;

            if (!target.ZoneJungle)
                enraged++;

            if (Main.getGoodWorld)
                enraged += 0.5f;

            float distance = Vector2.Distance(npc.Center, target.Center);

            if (npc.timeLeft < 60)
                npc.timeLeft = 60;

            if (distance > 3000f)
            {
                npc.ai[0] = 4f;
                npc.netUpdate = true;
            }

            if (dead)
            {
                npc.ai[0] = 5f;
                npc.netUpdate = true;
            }

            if (npc.ai[0] != 0f)
                return true;

            int dashes = 2;

            if (Main.expertMode)
            {
                if (npc.life < npc.lifeMax / 2)
                    dashes++;

                if (npc.life < npc.lifeMax / 3)
                    dashes++;

                if (npc.life < npc.lifeMax / 5)
                    dashes++;
            }

            dashes += (int)(1f * enraged);

            if (npc.ai[1] > 2 * dashes && npc.ai[1] % 2f == 0f)
            {
                npc.ai[0] = -1f;
                npc.ai[1] = 0f;
                npc.ai[2] = 0f;
                npc.netUpdate = true;
                return false;
            }

            if (npc.ai[1] % 2f == 0f)
            {
                LineUp(npc, enraged);
                return false;
            }

            Dash(npc, enraged);
            return false;
        }

        /// <summary>Level with the player, then off: vanilla's, half again as fast.</summary>
        private void LineUp(NPC npc, float enraged)
        {
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            float band = 20f + 20f * enraged;

            if (Math.Abs(npc.Center.Y - target.Center.Y) < band)
            {
                npc.localAI[0] = 1f;
                npc.ai[1]++;
                npc.ai[2] = 0f;
                npc.netUpdate = true;

                float speed = 12f;

                if (Main.expertMode)
                {
                    speed = 16f;

                    if (npc.life < npc.lifeMax * 0.75)
                        speed += 2f;

                    if (npc.life < npc.lifeMax * 0.5)
                        speed += 2f;

                    if (npc.life < npc.lifeMax * 0.25)
                        speed += 2f;

                    if (npc.life < npc.lifeMax * 0.1)
                        speed += 2f;
                }

                speed += 7f * enraged;

                Vector2 to = target.Center - npc.Center;
                npc.velocity = to * (speed / Math.Max(1f, to.Length()));
                npc.spriteDirection = npc.direction;

                SoundEngine.PlaySound(SoundID.Zombie125, npc.position);

                beesLeft = Main.rand.Next(BeesLeast, BeesMost + 1);
                beeClock = 0;
                return;
            }

            npc.localAI[0] = 0f;

            float most = 12f;
            float push = 0.15f;

            if (Main.expertMode)
            {
                if (npc.life < npc.lifeMax * 0.75)
                {
                    most++;
                    push += 0.05f;
                }

                if (npc.life < npc.lifeMax * 0.5)
                {
                    most++;
                    push += 0.05f;
                }

                if (npc.life < npc.lifeMax * 0.25)
                {
                    most += 2f;
                    push += 0.05f;
                }

                if (npc.life < npc.lifeMax * 0.1)
                {
                    most += 2f;
                    push += 0.1f;
                }
            }

            most += 3f * enraged;
            push += 0.5f * enraged;

            most *= Pace;
            push *= Pace * Pace;

            npc.velocity.Y += npc.Center.Y < target.Center.Y ? push : -push;
            npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y, -most, most);

            float across = Math.Abs(npc.Center.X - target.Center.X);
            float sideways = 0.15f * Pace * Pace;

            if (across > 600f)
                npc.velocity.X += sideways * npc.direction;
            else if (across < 300f)
                npc.velocity.X -= sideways * npc.direction;
            else
                npc.velocity.X *= (float)Math.Pow(0.8f, Pace);

            npc.velocity.X = MathHelper.Clamp(npc.velocity.X, -16f * Pace, 16f * Pace);
            npc.spriteDirection = npc.direction;
        }

        /// <summary>Through and past them, twice vanilla's distance, and slowing out of it half again as fast. The bees on the way.</summary>
        private void Dash(NPC npc, float enraged)
        {
            Player target = Main.player[npc.target];

            npc.direction = npc.velocity.X < 0f ? -1 : 1;
            npc.spriteDirection = npc.direction;

            int past = 600;

            if (Main.expertMode)
            {
                if (npc.life < npc.lifeMax * 0.1)
                    past = 300;
                else if (npc.life < npc.lifeMax * 0.25)
                    past = 450;
                else if (npc.life < npc.lifeMax * 0.5)
                    past = 500;
                else if (npc.life < npc.lifeMax * 0.75)
                    past = 550;
            }

            int side = npc.Center.X < target.Center.X ? -1 : 1;
            past -= (int)(100f * enraged);
            past = (int)(past * Reach);

            bool through = false;

            if (npc.direction == side && Math.Abs(npc.Center.X - target.Center.X) > past)
            {
                npc.ai[2] = 1f;
                through = true;
            }

            if (Math.Abs(npc.Center.Y - target.Center.Y) > past * 1.5f)
            {
                npc.ai[2] = 1f;
                through = true;
            }

            if (enraged > 0f && through)
                npc.velocity *= 0.5f;

            if (npc.ai[2] == 1f)
            {
                // Whatever bees this dash still owes, now.
                while (beesLeft > 0)
                    Bee(npc, target);

                npc.TargetClosest();
                npc.spriteDirection = npc.direction;
                npc.localAI[0] = 0f;

                float slow = (float)Math.Pow(0.9f, Pace);
                npc.velocity *= slow;

                float still = 0.1f;

                if (Main.expertMode)
                {
                    if (npc.life < npc.lifeMax / 2)
                    {
                        npc.velocity *= slow;
                        still += 0.05f;
                    }

                    if (npc.life < npc.lifeMax / 3)
                    {
                        npc.velocity *= slow;
                        still += 0.05f;
                    }

                    if (npc.life < npc.lifeMax / 5)
                    {
                        npc.velocity *= slow;
                        still += 0.05f;
                    }
                }

                if (enraged > 0f)
                    npc.velocity *= (float)Math.Pow(0.7f, Pace);

                if (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y) < still)
                {
                    npc.ai[2] = 0f;
                    npc.ai[1]++;
                    npc.netUpdate = true;
                }

                return;
            }

            npc.localAI[0] = 1f;

            if (beesLeft > 0 && ++beeClock >= BeeEvery)
            {
                beeClock = 0;
                Bee(npc, target);
            }
        }

        /// <summary>One bee out of the back of her, thrown the way she is going and toward them. The server's.</summary>
        private void Bee(NPC npc, Player target)
        {
            beesLeft--;

            if (!Authority)
                return;

            Vector2 at = Abdomen(npc) + Main.rand.NextVector2Circular(6f, 6f);
            int index = NPC.NewNPC(npc.GetSource_FromAI(), (int)at.X, (int)at.Y, Main.rand.Next(NPCID.Bee, NPCID.BeeSmall + 1));

            if (index >= Main.maxNPCs)
                return;

            NPC bee = Main.npc[index];
            Vector2 aim = (target.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);

            bee.velocity = aim.RotatedByRandom(0.7f) * Main.rand.NextFloat(4f, 7f);

            // As vanilla sends out her own.
            bee.CanBeReplacedByOtherNPCs = true;
            bee.localAI[0] = 60f;
            bee.netUpdate = true;
        }

        // ---- the death -------------------------------------------------------------------------------

        public override void Load()
        {
            On_NPC.VanillaHitEffect += HitNotKilled;
        }

        /// <summary>
        /// The blow that would kill her only starts her death (CheckDead refuses it), but
        /// vanilla shows the blow before that is asked, and shows it as her death: her six
        /// pieces thrown off her and a burst of her blood (NPC.StrikeNPC runs HitEffect, then
        /// checkDead). While her death is still to be refused, the blow is shown the way
        /// vanilla shows one she lives through. Her pieces come off her at the burst.
        /// </summary>
        private static void HitNotKilled(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            if (self.type != NPCID.QueenBee || self.life > 0 || !self.TryGetGlobalNPC(out LegendsQueenBee bee) || bee.ending)
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

        /// <summary>
        /// Refuses the first death and hands her to the death instead: life pinned at one,
        /// untouchable, and the nest found. The death zeroes the life itself at its end and
        /// calls checkDead again, and that one is let through: the frame the loot drops on.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (ending)
                return true;

            if (!dying)
            {
                dying = true;
                deathClock = 0;

                hasSpot = QueenBeeGround.FindNest(npc.Center, NestReachX, NestReachY, out nest, out _);
                spot = hasSpot ? new Vector2(nest.X * 16f + 8f, (nest.Y + 1) * 16f) : npc.Center;

                Go(npc, BeeScene.Nest);
            }

            npc.life = 1;
            npc.dontTakeDamage = true;
            npc.active = true;
            npc.netUpdate = true;

            return false;
        }

        /// <summary>To the nest, and the camera after her.</summary>
        private void NestTo(NPC npc, Player target, int t)
        {
            DeathShot(npc);

            if (!hasSpot)
            {
                npc.velocity *= 0.9f;

                if (Authority && t >= 30)
                    Go(npc, BeeScene.Swell);

                return;
            }

            Vector2 perch = Perch(npc);

            Approach(npc, perch, 0.1f, 18f);

            if (Math.Abs(npc.velocity.X) > 0.5f)
            {
                npc.direction = npc.velocity.X > 0f ? 1 : -1;
                npc.spriteDirection = npc.direction;
            }

            if (!Main.dedServ && Main.rand.NextBool(4))
                Drip(Abdomen(npc), Main.rand.NextFloat(0.4f, 0.7f));

            if (Authority && (Vector2.Distance(npc.Center, perch) < 16f || t >= NestMost))
                Go(npc, BeeScene.Settle);
        }

        /// <summary>Down onto it, her abdomen just over where the larva will be.</summary>
        private void Settle(NPC npc, Player target, int t, bool entered)
        {
            if (entered)
                from = npc.Center;

            DeathShot(npc);
            Place(npc, Vector2.Lerp(from, Perch(npc), Ease(t / (float)SettleTicks)));
            Face(npc, target);

            if (!Main.dedServ && Main.rand.NextBool(3))
                Drip(Abdomen(npc), Main.rand.NextFloat(0.4f, 0.8f));

            if (Authority && t >= SettleTicks)
                Go(npc, BeeScene.Birth);
        }

        /// <summary>Three strains, and the larva out of her and onto the hive.</summary>
        private void Birth(NPC npc, int t)
        {
            DeathShot(npc);
            Place(npc, Perch(npc));

            if (!Main.dedServ)
            {
                foreach (int at in Strains)
                {
                    if (t == at)
                    {
                        SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = 0.2f + 0.1f * Array.IndexOf(Strains, at), Volume = 0.65f }, npc.Center);
                        SoundEngine.PlaySound(SoundID.DripSplash with { Volume = 0.6f }, npc.Center);
                    }
                }

                if (Main.rand.NextBool(3))
                    Drip(Abdomen(npc) + Main.rand.NextVector2Circular(8f, 4f), Main.rand.NextFloat(0.5f, 0.9f));

                if (t == LarvaAt)
                    Born(npc);
            }

            // Placed only where it still can go: nothing built there since she chose it. If
            // something has been, there is no larva, and nobody is shown one coming.
            if (Authority && t == LarvaAt)
            {
                if (QueenBeeGround.NestFree(nest))
                {
                    QueenBeeGround.PlaceLarva(nest);
                }
                else
                {
                    hasSpot = false;
                    npc.netUpdate = true;
                }
            }

            if (Authority && t >= BirthTicks)
                Go(npc, BeeScene.Swell);
        }

        /// <summary>Up off it, swelling, trembling, heating to white.</summary>
        private void Swell(NPC npc, int t, bool entered)
        {
            if (entered)
                from = npc.Center;

            DeathShot(npc);

            float p = MathHelper.Clamp(t / (float)SwellTicks, 0f, 1f);

            Place(npc, from - new Vector2(0f, 40f * Ease(t / 30f)));

            if (Authority && t >= SwellTicks)
                Go(npc, BeeScene.Burst);

            if (Main.dedServ)
                return;

            Lighting.AddLight(npc.Center, new Vector3(1f, 0.8f, 0.3f) * (0.4f + 1.2f * p));

            // A whine, three times, higher each time.
            int whine = t == 0 ? 0 : t == 25 ? 1 : t == 48 ? 2 : -1;

            if (whine >= 0)
                SoundEngine.PlaySound(SoundID.Zombie125 with { Pitch = -0.1f + 0.35f * whine, Volume = 0.8f }, npc.Center);

            // Light drawn in to her from round about.
            if (Main.rand.NextFloat() < 0.35f + 0.5f * p)
            {
                Vector2 way = Main.rand.NextVector2Unit();
                Vector2 at = DrawCentre(npc) + way * Main.rand.NextFloat(90f, 160f);

                PADParticleHandler.SpawnParticle(new HoneyMote(at, -way * Main.rand.NextFloat(4f, 7f), Gold,
                    Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(18, 26), 0f));
            }
        }

        /// <summary>She bursts, and every machine sees it; a moment later the server takes her.</summary>
        private void BurstOut(NPC npc, int t, bool entered)
        {
            npc.velocity = Vector2.Zero;

            // Held to the moment it starts home; that moment says whether it was ours to take home.
            if (t <= HomeAt)
                DeathShot(npc);

            if (t == HomeAt && watching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, ReturnTicks);

            if (entered && !Main.dedServ)
                Explode(npc);

            if (!Authority || t < BurstTicks)
                return;

            // Handed back to vanilla. CheckDead lets this one through: the loot. Her sound
            // was the burst's, so vanilla's is not played again; and every client is told
            // she is gone, as nothing struck her.
            ending = true;
            npc.DeathSound = null;
            npc.life = 0;
            npc.checkDead();

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
        }

        /// <summary>The camera on her through her death, for a player near: in over the first moments, then held.</summary>
        private void DeathShot(NPC npc)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead || Vector2.Distance(me.Center, npc.Center) > SceneRange)
                return;

            watching = true;

            PisPlayer.BlendCamera(npc.Center, Ease(deathClock / (float)DeathPan));
            PisPlayer.HideUI();
            Cinematic.Frame();
        }

        /// <summary>Where she sits for the birth: her abdomen just over the top of where the larva goes.</summary>
        private Vector2 Perch(NPC npc)
        {
            float larvaTop = spot.Y - 48f;

            return new Vector2(spot.X, larvaTop - 2f - Below(npc));
        }

        /// <summary>The larva is out: a wet slap, and honey thrown off it.</summary>
        private void Born(NPC npc)
        {
            Vector2 at = spot - new Vector2(0f, 24f);

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.2f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.DripSplash with { Volume = 1f, Pitch = -0.4f }, at);

            for (int i = 0; i < 16; i++)
            {
                Vector2 way = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.3f, 1f)).SafeNormalize(-Vector2.UnitY);

                PADParticleHandler.SpawnParticle(new SlimeGlob(at + way * 20f, way * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(50, 80), HoneyTint));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Gold * 0.6f, 1.2f, 24));
        }

        /// <summary>
        /// The burst: the flash and the shake, the sound of it, her own pieces thrown out from
        /// her middle each its own way round, honey and light every way, and the light in the room.
        /// </summary>
        private void Explode(NPC npc)
        {
            Vector2 at = DrawCentre(npc) + new Vector2(0f, 10f * npc.scale);
            float near = 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 800f) / 1200f, 0f, 1f);

            if (near > 0f)
            {
                Cinematic.Flash(new Color(255, 215, 120), 0.5f * near);
                Main.LocalPlayer.PIS().BumpScreenShake(14f * near, 30);
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.NPCDeath66 with { Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.5f, Volume = 0.9f }, at);

            // Her six pieces, spread evenly round her, each a little off its own line.
            IEntitySource source = npc.GetSource_Death();
            float turn = Main.rand.NextFloat(MathHelper.TwoPi);

            for (int i = 0; i < 6; i++)
            {
                float angle = turn + MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 way = angle.ToRotationVector2();

                int type = 303 + i;

                Main.instance.LoadGore(type);
                Vector2 half = TextureAssets.Gore[type].Size() * 0.5f * npc.scale;

                Gore gore = Gore.NewGorePerfect(source, at + way * 16f - half, way * Main.rand.NextFloat(8f, 13f), type, npc.scale);

                gore.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            for (int i = 0; i < 44; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new SlimeGlob(at + way * 18f, way * Main.rand.NextFloat(4f, 12f),
                    Main.rand.NextFloat(0.5f, 1.1f), Main.rand.Next(60, 100), HoneyTint));
            }

            for (int i = 0; i < 18; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new SparkShard(at, way * Main.rand.NextFloat(9f, 18f), Gold,
                    18f, 5f, 60f, Main.rand.Next(18, 28)));
            }

            for (int i = 0; i < 50; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(24f, 24f), DustID.Honey,
                    Main.rand.NextVector2Circular(9f, 9f), 0, default, Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = Main.rand.NextBool();
            }

            for (int i = 0; i < 20; i++)
            {
                PADParticleHandler.SpawnParticle(new HoneyMote(at, Main.rand.NextVector2Circular(8f, 8f), Gold,
                    Main.rand.NextFloat(0.8f, 1.4f), Main.rand.Next(30, 50), 0.02f));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Gold, 3f, 30));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Heat, 1.4f, 16));
        }

        // ---- motion -----------------------------------------------------------------------------------

        /// <summary>
        /// There on the next tick. Handed to velocity, not written into position, because the
        /// move after the AI would then carry her on by whatever velocity was left.
        /// </summary>
        private static void Place(NPC npc, Vector2 goal) => npc.velocity = goal - npc.Center;

        private static void Approach(NPC npc, Vector2 goal, float gain, float most)
        {
            Vector2 want = (goal - npc.Center) * gain;

            if (want.Length() > most)
                want = want.SafeNormalize(Vector2.Zero) * most;

            npc.velocity = want;
        }

        private static void Face(NPC npc, Player target)
        {
            if (target == null || !target.active)
                return;

            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;
        }

        /// <summary>How far under the top of the honey her middle is kept while she is in it: all of her under.</summary>
        private static float Sink(NPC npc) => SpriteTop * npc.scale + 50f * npc.scale + 10f;

        /// <summary>How far her drawn bottom is under her middle.</summary>
        private static float Below(NPC npc) => DrawCentre(npc).Y - npc.Center.Y + SpriteBottom * npc.scale;

        /// <summary>Side to side as she shakes.</summary>
        private static float Jitter(int t) => (float)Math.Sin(t * 1.9f) * 7f;

        private static float Bob(int t) => (float)Math.Sin(t * 0.08f) * 4f;

        /// <summary>Where her top and her bottom were on a tick of the emergence: the same sum the emergence moves her by.</summary>
        private float TopAt(NPC npc, int t) => EmergeY(npc, t) - (npc.Center.Y - DrawCentre(npc).Y) - SpriteTop * npc.scale;

        private float BottomAt(NPC npc, int t) => EmergeY(npc, t) - (npc.Center.Y - DrawCentre(npc).Y) + SpriteBottom * npc.scale;

        private float EmergeY(NPC npc, int t)
        {
            float low = spot.Y + Sink(npc);
            float high = spot.Y - 60f - Below(npc);

            return MathHelper.Lerp(low, high, Ease(t / (float)EmergeTicks));
        }

        /// <summary>Whether something going up or down passed the top of the honey between two heights.</summary>
        private bool Crossed(float before, float now) =>
            (before >= spot.Y && now < spot.Y) || (before < spot.Y && now >= spot.Y);

        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        // ---- where things are on her ------------------------------------------------------------------

        /// <summary>Where vanilla puts the middle of her frame (Main.DrawNPCDirect_Inner).</summary>
        private static Vector2 DrawCentre(NPC npc) =>
            new Vector2(npc.position.X + npc.width / 2, npc.position.Y + npc.height + 4f - FrameHalf * npc.scale + npc.gfxOffY);

        /// <summary>
        /// The comb of her abdomen: behind her in the dash pose, under her in the hover. The
        /// sheet faces left; turned, the other way. The pose is the one vanilla's frames follow
        /// (localAI[0]), which the server has as well as the clients.
        /// </summary>
        private static Vector2 Abdomen(NPC npc)
        {
            float away = npc.spriteDirection == 1 ? -1f : 1f;
            bool dashing = npc.localAI[0] == 1f;

            Vector2 offset = dashing ? new Vector2(54f * away, 19f) : new Vector2(9f * away, 54f);

            return DrawCentre(npc) + offset * npc.scale;
        }

        // ---- the looks: what comes off her --------------------------------------------------------------

        /// <summary>Every tick, on the machines that see her: her light, and whatever her flight throws off.</summary>
        private void Airs(NPC npc)
        {
            bool hidden = scene == BeeScene.Pan || scene == BeeScene.Soak || scene == BeeScene.Burst;

            if (hidden)
                return;

            float pulse = Pulse();
            Vector3 light = honeyed ? new Vector3(0.9f, 0.62f, 0.12f) * (0.8f + 0.5f * pulse) : new Vector3(0.45f, 0.32f, 0.08f);

            Lighting.AddLight(DrawCentre(npc), light);

            bool dashing = npc.localAI[0] == 1f && npc.velocity.Length() > 8f && !InScene;

            if (dashing)
            {
                // The air torn past her, and honey thrown back off her.
                if (Main.GameUpdateCount % 2 == 0)
                {
                    Vector2 at = DrawCentre(npc) + Main.rand.NextVector2Circular(50f, 40f) * npc.scale;

                    PADParticleHandler.SpawnParticle(new WindStreak(at, npc.velocity * 0.15f, Main.rand.NextFloat(70f, 130f),
                        Main.rand.NextFloat(3f, 5f), Main.rand.Next(14, 22)));
                }

                if (Main.rand.NextBool(3))
                    Drip(Abdomen(npc), Main.rand.NextFloat(0.4f, 0.8f), -npc.velocity * 0.1f);
            }
            else if (!InScene && Main.rand.NextBool(20))
            {
                Drip(Abdomen(npc) + Main.rand.NextVector2Circular(8f, 3f) * npc.scale, Main.rand.NextFloat(0.4f, 0.75f));
            }

            // Pollen off her wings.
            if (Main.rand.NextBool(7))
            {
                Vector2 at = DrawCentre(npc) + new Vector2(Main.rand.NextFloat(-60f, 60f), Main.rand.NextFloat(-50f, 0f)) * npc.scale;

                PADParticleHandler.SpawnParticle(new HoneyMote(at, Main.rand.NextVector2Circular(0.6f, 0.6f) - npc.velocity * 0.05f,
                    Gold * 0.55f, Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(40, 70), -0.01f));
            }

            // Honeyed: flecks climbing her, the way the light does.
            if (honeyed && Main.GameUpdateCount % 3 == 0)
            {
                Vector2 at = DrawCentre(npc) + new Vector2(Main.rand.NextFloat(-40f, 40f), Main.rand.NextFloat(20f, 60f)) * npc.scale;

                PADParticleHandler.SpawnParticle(new HoneyMote(at, new Vector2(0f, -Main.rand.NextFloat(1f, 2.2f)), Gold,
                    Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(26, 40), 0.06f));
            }
        }

        /// <summary>A drop of honey off her, falling, that splats where it lands.</summary>
        private static void Drip(Vector2 at, float size, Vector2 push = default)
        {
            PADParticleHandler.SpawnParticle(new SlimeGlob(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.2f, 1.2f)) + push,
                size, Main.rand.Next(60, 90), HoneyTint));
        }

        /// <summary>A bubble coming up and breaking on the top of the honey.</summary>
        private static void Bubble(Vector2 at, float strength)
        {
            PADParticleHandler.SpawnParticle(new SlimeGlob(at, new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(1.5f, 3.5f) * strength),
                Main.rand.NextFloat(0.35f, 0.6f), Main.rand.Next(30, 50), HoneyTint));

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.Honey, new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1f, 3f)),
                    0, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = false;
            }
        }

        private static void Ripple(Vector2 at, float from, float to, int ticks, float strength)
        {
            PADParticleHandler.SpawnParticle(new HoneyRipple(at, from, to, ticks, Gold * strength));
        }

        /// <summary>Her head, or all of her, coming up through the top of the honey.</summary>
        private void Breach(NPC npc, float strength)
        {
            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = 0.6f * strength }, spot);

            for (int i = 0; i < (int)(14 * strength); i++)
            {
                Vector2 way = new Vector2(Main.rand.NextFloat(-0.7f, 0.7f), -1f).SafeNormalize(-Vector2.UnitY);

                PADParticleHandler.SpawnParticle(new SlimeGlob(new Vector2(npc.Center.X + Main.rand.NextFloat(-30f, 30f) * npc.scale, spot.Y - 2f),
                    way * Main.rand.NextFloat(2f, 6f) * strength, Main.rand.NextFloat(0.5f, 0.9f), Main.rand.Next(50, 80), HoneyTint));
            }

            Ripple(spot, 20f, 110f * strength, 44, 0.7f * strength);
        }

        /// <summary>Into or out of the honey: honey thrown up round her, rings, and the sound of it.</summary>
        private void Splash(NPC npc, float strength, bool gold)
        {
            Vector2 at = hasSpot ? new Vector2(npc.Center.X, spot.Y) : npc.Center;

            SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = Math.Min(1f, 0.8f * strength) }, at);
            SoundEngine.PlaySound(SoundID.Item85 with { Pitch = -0.4f, Volume = 0.6f }, at);

            for (int i = 0; i < (int)(24 * strength); i++)
            {
                Vector2 way = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -1f).SafeNormalize(-Vector2.UnitY);

                PADParticleHandler.SpawnParticle(new SlimeGlob(at + new Vector2(Main.rand.NextFloat(-36f, 36f), -2f),
                    way * Main.rand.NextFloat(3f, 9f) * strength, Main.rand.NextFloat(0.5f, 1f), Main.rand.Next(50, 90), HoneyTint));
            }

            if (gold)
            {
                for (int i = 0; i < 24; i++)
                {
                    PADParticleHandler.SpawnParticle(new HoneyMote(at + Main.rand.NextVector2Circular(40f, 10f),
                        new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f)), Gold,
                        Main.rand.NextFloat(0.7f, 1.2f), Main.rand.Next(30, 50), 0.05f));
                }

                PADParticleHandler.SpawnParticle(new BloomGeneric(at - new Vector2(0f, 30f), Vector2.Zero, Gold, 2f, 26));
            }

            if (hasSpot)
            {
                Ripple(spot, 24f, 140f * strength, 50, 0.8f);
                Ripple(spot, 10f, 80f * strength, 40, 0.5f);
            }
        }

        // ---- drawn ------------------------------------------------------------------------------------------

        /// <summary>How she is drawn this tick: what the scene does to her shape and her colour.</summary>
        private struct Look
        {
            /// <summary>On top of her size.</summary>
            public Vector2 Stretch;

            /// <summary>Stretched from her bottom rather than her middle, so she stays sat on what she is on.</summary>
            public bool FromBottom;

            public Vector2 Shift;

            /// <summary>The gold round her, 0 to 1.</summary>
            public float Glow;

            /// <summary>Honey over her, 0 to 1.</summary>
            public float Wet;

            /// <summary>White-gold through her, as she swells.</summary>
            public float Heat;

            /// <summary>A world height below which none of her is drawn: the top of the honey she is in.</summary>
            public float Cut;

            public bool Hidden;
        }

        private Look Posed(NPC npc)
        {
            Look look = new Look
            {
                Stretch = Vector2.One,
                Cut = float.PositiveInfinity,
            };

            int t = timer;

            switch (scene)
            {
                case BeeScene.Pan:
                case BeeScene.Soak:
                case BeeScene.Burst:
                    look.Hidden = true;
                    break;

                case BeeScene.Emerge:
                    look.Cut = spot.Y;
                    look.Wet = 1f;
                    break;

                case BeeScene.Level:
                    look.Wet = 1f - Ease(t / (float)LevelTicks);
                    look.Glow = 0.35f * Math.Min(1f, t / 12f);
                    break;

                case BeeScene.Shake:
                    look.Glow = 0.35f;
                    break;

                case BeeScene.Roar:
                {
                    // Up on herself for the roar, and down again at the end of it.
                    float rear = Ease(t / 12f) * (1f - Ease((t - (RoarTicks - 20)) / 20f));

                    look.Stretch = new Vector2(1f - 0.05f * rear, 1f + 0.08f * rear);
                    look.FromBottom = true;
                    look.Glow = 0.35f * (1f - t / (float)RoarTicks);
                    break;
                }

                case BeeScene.Plunge:
                case BeeScene.Surge:
                    if (hasSpot)
                        look.Cut = spot.Y;

                    break;

                case BeeScene.Birth:
                    foreach (int at in Strains)
                    {
                        float u = (t - at) / (float)StrainTicks;

                        if (u < 0f || u > 1f)
                            continue;

                        float press = (float)Math.Sin(u * MathHelper.Pi);
                        look.Stretch = new Vector2(1f + 0.09f * press, 1f - 0.11f * press);
                    }

                    look.FromBottom = true;
                    break;

                case BeeScene.Swell:
                {
                    float p = MathHelper.Clamp(t / (float)SwellTicks, 0f, 1f);
                    float grow = 1f + 0.3f * p * p;

                    look.Stretch = new Vector2(grow, grow);
                    look.Shift = Main.rand.NextVector2Circular(1f, 1f) * (1f + 4f * p);
                    look.Heat = p * p;
                    look.Glow = 0.2f + 0.8f * p;
                    break;
                }
            }

            if (honeyed && !look.Hidden)
                look.Glow = Math.Max(look.Glow, 0.5f + 0.3f * Pulse());

            return look;
        }

        /// <summary>The honeyed pulse: 0 to 1 and back, about two a second.</summary>
        private static float Pulse() => 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7.5f);

        /// <summary>
        /// Draws her here instead of vanilla, as vanilla would, so the scenes can stretch her,
        /// shift her, cut her off at the top of the honey and colour her: the larva coming out
        /// of her under her, the gold behind a dash, the gold round her, her, the honey on
        /// her, the heat of the swell, and the light climbing her while she is honeyed.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.IsABestiaryIconDummy)
                return true;

            Look look = Posed(npc);

            if (look.Hidden)
                return false;

            // The larva coming out of her, until the real one is there (it is the server's, and
            // comes a moment later on a client).
            bool larva = hasSpot && (scene == BeeScene.Birth && timer >= LarvaFrom || scene == BeeScene.Swell || scene == BeeScene.Burst);

            if (larva && !LarvaThere())
                DrawLarva(spriteBatch, screenPos, scene == BeeScene.Birth ? (timer - LarvaFrom) / (float)(LarvaAt - LarvaFrom) : 1f);

            Main.instance.LoadNPC(npc.type);

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = new Vector2(texture.Width / 2, texture.Height / Main.npcFrameCount[npc.type] / 2);
            Vector2 scale = look.Stretch * npc.scale;

            Vector2 at = DrawCentre(npc) + look.Shift - screenPos;

            if (look.FromBottom)
                at.Y += SpriteBottom * npc.scale * (1f - look.Stretch.Y);

            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color colour = npc.GetAlpha(npc.GetNPCColorTintedByBuffs(drawColor));

            // How much of the frame is above the cut, from its top.
            float cutLocal = frame.Height;

            if (!float.IsPositiveInfinity(look.Cut))
            {
                float frameTop = at.Y + screenPos.Y - origin.Y * scale.Y;
                cutLocal = MathHelper.Clamp((look.Cut - frameTop) / Math.Max(0.01f, scale.Y), 0f, frame.Height);

                if (cutLocal <= 0f)
                    return false;
            }

            Rectangle shown = new Rectangle(frame.X, frame.Y, frame.Width, (int)cutLocal);

            // The gold behind a dash.
            bool dashing = npc.localAI[0] == 1f && npc.velocity.Length() > 6f && !InScene;

            if (dashing)
            {
                Vector2 draw = DrawCentre(npc) - npc.Center;

                for (int i = 1; i < trailCount; i++)
                {
                    float fade = 1f - i / (float)trailCount;
                    Color ghost = Additive(Gold) * (0.42f * fade);

                    spriteBatch.Draw(texture, trail[i] + draw - screenPos, shown, ghost, npc.rotation, origin,
                        scale * (1f - 0.03f * i), flip, 0f);
                }
            }

            // The gold round her: her own shape, eight times, a few pixels out, under her.
            if (look.Glow > 0f)
            {
                Color rim = Additive(Gold) * (0.5f * look.Glow);
                float reach = 4f + 4f * look.Glow;

                for (int i = 0; i < 8; i++)
                {
                    Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * reach;
                    spriteBatch.Draw(texture, at + offset, shown, rim, npc.rotation, origin, scale, flip, 0f);
                }
            }

            spriteBatch.Draw(texture, at, shown, colour, npc.rotation, origin, scale, flip, 0f);

            if (look.Wet > 0f)
                spriteBatch.Draw(texture, at, shown, Additive(HoneyTint) * (0.35f * look.Wet), npc.rotation, origin, scale, flip, 0f);

            if (look.Heat > 0f)
                spriteBatch.Draw(texture, at, shown, Additive(Heat) * (0.8f * look.Heat), npc.rotation, origin, scale, flip, 0f);

            if (honeyed)
            {
                DrawClimb(spriteBatch, texture, shown, at, origin, scale, flip, npc.rotation);

                // The glint over all of it, in her shape (HoneyGlint).
                Texture2D glint = HoneyGlint.For(npc);

                if (glint != null)
                {
                    Rectangle part = new Rectangle(0, 0, glint.Width, Math.Min(glint.Height, (int)cutLocal));

                    spriteBatch.Draw(glint, at, part, Additive(Color.White) * (0.75f + 0.25f * Pulse()), npc.rotation, origin,
                        scale, flip, 0f);
                }
            }

            return false;
        }

        /// <summary>A colour that adds to what is under it: no alpha, in the premultiplied batch the world is drawn in.</summary>
        private static Color Additive(Color colour)
        {
            colour.A = 0;
            return colour;
        }

        /// <summary>
        /// The honeyed look: all of her lifted a little toward gold, pulsing, and bands of light
        /// going up her, the way a creature's shape shines when it is made stronger. Drawn as
        /// strips of her own frame, each as bright as the band passing it, so the light is in
        /// her shape and nowhere else.
        /// </summary>
        private static void DrawClimb(SpriteBatch spriteBatch, Texture2D texture, Rectangle shown, Vector2 at, Vector2 origin,
            Vector2 scale, SpriteEffects flip, float rotation)
        {
            float pulse = Pulse();
            float ticks = Main.GlobalTimeWrappedHourly * 60f;

            spriteBatch.Draw(texture, at, shown, Additive(Gold) * (0.14f + 0.2f * pulse), rotation, origin, scale, flip, 0f);

            const int Strip = 4;
            const float Spacing = 26f;
            const float Speed = 1.6f;

            for (int y = 0; y < shown.Height; y += Strip)
            {
                int height = Math.Min(Strip, shown.Height - y);
                float along = (y + Strip * 0.5f + ticks * Speed) / Spacing;
                float band = along - (float)Math.Floor(along);
                float bright = Math.Max(0f, 1f - Math.Abs(band - 0.5f) * 3f);

                bright *= bright;

                if (bright < 0.03f)
                    continue;

                Rectangle strip = new Rectangle(shown.X, shown.Y + y, shown.Width, height);
                Vector2 stripOrigin = new Vector2(origin.X, origin.Y - y);

                spriteBatch.Draw(texture, at, strip, Additive(Band) * (bright * (0.6f + 0.4f * pulse)), rotation, stripOrigin,
                    scale, flip, 0f);
            }
        }

        /// <summary>Whether the larva she left is in the world, where she left it.</summary>
        private bool LarvaThere()
        {
            if (!WorldGen.InWorld(nest.X, nest.Y, 2))
                return false;

            Tile tile = Main.tile[nest.X, nest.Y];

            return tile.HasTile && tile.TileType == TileID.Larva;
        }

        /// <summary>The larva coming out of her: its own sheet, growing up from the hive to its size, a little past it and back.</summary>
        private void DrawLarva(SpriteBatch spriteBatch, Vector2 screenPos, float grown)
        {
            Main.instance.LoadTiles(TileID.Larva);

            Texture2D sheet = TextureAssets.Tile[TileID.Larva].Value;

            float g = Ease(grown);
            float size = g * (1f + 0.12f * (float)Math.Sin(grown * MathHelper.Pi));

            if (size <= 0.02f)
                return;

            int animation = Main.tileFrame[TileID.Larva] * 54;
            Vector2 bottom = spot;

            for (int col = 0; col < 3; col++)
            {
                for (int row = 0; row < 3; row++)
                {
                    Rectangle piece = new Rectangle(col * 18, row * 18 + animation, 16, 16);
                    Vector2 corner = new Vector2((col - 1) * 16f - 8f, (row - 3) * 16f);
                    Vector2 world = bottom + corner * size;

                    Color light = Lighting.GetColor((int)((world.X + 8f) / 16f), (int)((world.Y + 8f) / 16f));

                    spriteBatch.Draw(sheet, world - screenPos, piece, light, 0f, Vector2.Zero, size, SpriteEffects.None, 0f);
                }
            }
        }

        // ---- sync ------------------------------------------------------------------------------------------
        //
        // The scene and its clock, where it is, and whether she is honeyed or dying. Each
        // machine counts the clock on by itself between packets; a packet moves it only when
        // the scene has changed or the count has drifted.

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(hasSpot);
            bitWriter.WriteBit(honeyed);
            bitWriter.WriteBit(dying);

            binaryWriter.Write((byte)scene);
            binaryWriter.Write((short)Math.Min(timer, short.MaxValue));
            binaryWriter.WriteVector2(spot);
            binaryWriter.Write((short)nest.X);
            binaryWriter.Write((short)nest.Y);
            binaryWriter.Write((sbyte)levelSide);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            hasSpot = bitReader.ReadBit();
            bool wasHoneyed = bitReader.ReadBit();
            bool wasDying = bitReader.ReadBit();

            BeeScene got = (BeeScene)binaryReader.ReadByte();
            int clock = binaryReader.ReadInt16();

            spot = binaryReader.ReadVector2();
            nest = new Point(binaryReader.ReadInt16(), binaryReader.ReadInt16());
            levelSide = binaryReader.ReadSByte();

            honeyed |= wasHoneyed;

            if (wasDying && !dying)
            {
                dying = true;
                deathClock = 0;
            }

            if (got != scene || Math.Abs(clock - timer) > 6)
            {
                scene = got;
                timer = clock;
            }

            // Told of a scene before its first tick here: untouchable from now, so nothing
            // (the name's plate included) takes her for fighting in the meantime.
            if (scene != BeeScene.None || dying)
                npc.dontTakeDamage = true;
        }
    }

    /// <summary>Her name comes up in her own arrival, at the roar, not when the camera has sat on her a beat.</summary>
    public class LegendsQueenBeePlate : ModSystem
    {
        public override void PostSetupContent()
        {
            BossPlate.HasOwnIntro(NPCID.QueenBee);
            BossPlate.NamesItself(NPCID.QueenBee);
        }
    }

    /// <summary>
    /// Her stingers: three quarters again as hard in Legends, as she is; and in every world
    /// gold behind them and a glow on them.
    /// </summary>
    public class LegendsQueenBeeStingers : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.QueenBeeStinger;

        private const int TrailLength = 6;
        private readonly Vector2[] trail = new Vector2[TrailLength];
        private int count;

        private static readonly Color Gold = new Color(255, 205, 90, 0);

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (!PADWorld.Legends)
                return;

            if (source is EntitySource_Parent parent && parent.Entity is NPC npc && npc.type == NPCID.QueenBee)
                projectile.damage = (int)Math.Round(projectile.damage * LegendsQueenBee.StingerDamageScale);
        }

        public override void PostAI(Projectile projectile)
        {
            for (int i = TrailLength - 1; i > 0; i--)
                trail[i] = trail[i - 1];

            trail[0] = projectile.Center;
            count = Math.Min(count + 1, TrailLength);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            Main.instance.LoadProjectile(projectile.type);

            Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            for (int i = 1; i < count; i++)
            {
                float fade = 1f - i / (float)count;

                Main.EntitySpriteDraw(texture, trail[i] - Main.screenPosition, null, Gold * (0.4f * fade), projectile.rotation, origin,
                    projectile.scale * (1f - 0.08f * i), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, projectile.Center - Main.screenPosition, null, Gold * 0.3f, projectile.rotation, origin,
                projectile.scale * 1.4f, SpriteEffects.None, 0);

            return true;
        }
    }

    /// <summary>A puff of honey and light where each bee comes out of her, in every world.</summary>
    public class LegendsQueenBeeBees : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
            entity.type == NPCID.Bee || entity.type == NPCID.BeeSmall;

        private bool puffed;

        private const float Near = 220f;

        public override void PostAI(NPC npc)
        {
            if (puffed)
                return;

            puffed = true;

            if (Main.dedServ)
                return;

            int queen = NPC.FindFirstNPC(NPCID.QueenBee);

            if (queen < 0 || Vector2.Distance(Main.npc[queen].Center, npc.Center) > Near)
                return;

            for (int i = 0; i < 6; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center, DustID.Honey, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }

            PADParticleHandler.SpawnParticle(new HoneyMote(npc.Center, Main.rand.NextVector2Circular(1f, 1f),
                new Color(255, 205, 90), 0.7f, 22, 0.01f));
        }
    }
}
