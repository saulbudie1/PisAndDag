using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.MegaShark
{
    /// <summary>
    /// Megashark. Fought after Queen Slime; killing it is how the weapon is obtained,
    /// which is why the vanilla recipe is disabled in PADRecipes.
    ///
    /// Three phases plus an enrage, all on one state machine. Health drives the phase
    /// (see <see cref="CheckPhase"/>), the phase picks the attack rotation (see
    /// <see cref="ChooseNextAttack"/>), and each phase opens with a scripted hand-over
    /// that owns the camera and cannot be shot through.
    ///
    /// The enrage sits outside all of it: once it latches it never returns to a rotation.
    /// </summary>
    [AutoloadBossHead]
    public class Megashark : ModNPC
    {
        private enum State
        {
            /// <summary>Rising out of the water while the camera catches up.</summary>
            Rise,

            /// <summary>Phase 1 attack 1 — hovers overhead and places aimed shots.</summary>
            Hover,

            /// <summary>Phase 1 attack 2 — plants itself and empties a barrage downrange.</summary>
            Barrage,

            /// <summary>Phase 1 attack 3 — glows hot and dashes.</summary>
            Heat,

            /// <summary>The phase 2 hand-over: sparks, smoke and a camera trip.</summary>
            Phase2Intro,

            /// <summary>Phase 2 attack 2 — burning dashes that arc instead of running straight.</summary>
            CurveDash,

            /// <summary>Phase 2 attack 3 — anchors, spins, and fires along its own rotation.</summary>
            Spin,

            /// <summary>The phase 3 hand-over: a storm, sparks and a long camera trip.</summary>
            Phase3Intro,

            /// <summary>Phase 3 attack 1 — hovers, stops dead, then places scoped shots.</summary>
            ScopeShots,

            /// <summary>Phase 3 attack 2 — the barrage, wider and wilder and half as long.</summary>
            WildBarrage,

            /// <summary>Phase 3 attack 3 — vents, then the curved dashes at full weight.</summary>
            SmokeDash,

            /// <summary>Phase 3 attack 4 — rings the player at speed, firing inward.</summary>
            Orbit,

            /// <summary>The death animation. Nothing else runs while this does.</summary>
            Dying,

            /// <summary>Enrage attack 1 — aimed shots while chasing.</summary>
            RageAimed,

            /// <summary>Enrage attack 2 — sustained minigun fire.</summary>
            RageMinigun
        }
         
        private const float LegendsHealth = 1.5f;
         
        private const float PhaseOneDamage = 1.2f;
         
        private const float EnrageDistance = 750f * 16f;
         
        private const float RiseAbove = 140f;

        private const int PanOut = 60;
        private const int RiseHold = 120;
        private const int PanBack = 60;

        /// <summary>Ticks the whole entrance lasts, damage ramping in over the tail of it.</summary>
        private const int RiseLength = PanOut + RiseHold + PanBack;

        // Attack 1 — hover
        private const float HoverHeight = 450f;
        private const float HoverBobHeight = 20f;
        private const float HoverSweepWidth = 140f;

        /// <summary>Radians per tick. The vertical bob is the faster of the two.</summary>
        private const float HoverBobSpeed = 0.055f;
        private const float HoverSweepSpeed = 0.037f;
        private const int HoverShotInterval = 120;

        /// <summary>
        /// How long the hover runs, in ticks. Rolled per rotation so the fight cannot be
        /// counted off.
        ///
        /// Half what it was. At one aimed shot every two seconds the old length was
        /// fifteen seconds of standing under a sniper for seven or eight rounds, and the
        /// back end of it was a wait rather than a threat.
        /// </summary>
        private const int HoverTimeMin = 300;
        private const int HoverTimeMax = 451;

        // Attack 2 — barrage
        private const int BarrageBrake = 60;
        private const int BarrageShake = 60;
        private const int BarrageInterval = 9;
        private const float BarrageSpread = 5f;

        /// <summary>Same halving, for the same reason — this one empties faster still.</summary>
        private const int BarrageTimeMin = 420;
        private const int BarrageTimeMax = 601;

        // Attack 3 — dashes
        private const int HeatWindup = 12;

        /// <summary>
        /// How far one phase-1 dash travels, in pixels. Rolled fresh per dash, so the
        /// distance is never quite the same twice and the lunges cannot be paced off.
        /// </summary>
        private const float DashMin = 600f;
        private const float DashMax = 850f;

        /// <summary>
        /// Ticks the dash aims behind the player — 0.125s. Short enough that it still
        /// tracks you, long enough that a dash committed at your exact position is not
        /// simply unreactable.
        /// </summary>
        private const int DashLag = 8;

        /// <summary>
        /// The band the dash sweeps through, relative to the player. Negative is above.
        /// Crossing their level rather than sitting off it is what makes a dash something
        /// you jump or drop out of, instead of something that always comes from one side.
        /// </summary>
        private const float DashAbove = -30f;
        private const float DashBelow = 40f;

        /// <summary>Radians per tick for the sweep through that band.</summary>
        private const float DashSweepSpeed = 0.02f;

        /// <summary>Afterimages kept behind the dash.</summary>
        private const int TrailLength = 10;

        private const int DashGap = 30;

        // ---- phase 2 -------------------------------------------------------

        /// <summary>Life fraction that hands the fight to phase 2.</summary>
        private const float Phase2At = 0.66f;

        // Hand-over: out to the boss, three seconds of it tearing itself up, back again.
        private const int P2PanOut = 60;
        private const int P2PanBack = 60;

        // The six seconds in the middle: a beat and a half of nothing, three failures a
        // second apart, then a beat and a half to let the smoke sit before the camera
        // leaves. The dead air at each end is what gives the sparks weight — cutting
        // straight to them makes the whole thing read as a stutter.
        private const int P2SparkDelay = 90;
        private const int P2SparkGap = 60;
        private const int P2SparkCount = 3;
        private const int P2SparkTail = 90;

        private const int P2Sparks = P2SparkDelay + P2SparkCount * P2SparkGap + P2SparkTail;
        private const int Phase2Length = P2PanOut + P2Sparks + P2PanBack;

        /// <summary>Warm, electrical, and nothing like the cool arc the player throws.</summary>
        private static readonly Color SparkColor = new Color(255, 186, 66);

        /// <summary>
        /// Aim scatter on the phase 2 aimed shot, in degrees.
        ///
        /// Two degrees, not five. The scatter is meant to stop the shot being sidestepped
        /// on cue, not to make it miss — at five the aimed shot had stopped reading as
        /// aimed at all, which cost the attack its whole identity.
        /// </summary>
        private const float Phase2Spread = 2f;

        private const int Phase2HoverTime = 420;
        private const int Phase2CurveTime = 600;
        private const int Phase2SpinTime = 600;

        /// <summary>
        /// How far the curved dash bends: a sixth of a full turn. A semicircle would loop
        /// back on itself; this reads as a swerve that still commits to a direction.
        /// </summary>
        private const float CurveArc = MathHelper.TwoPi / 6f;

        /// <summary>Ticks between one curved dash and the next.</summary>
        private const int CurveGap = 20;

        /// <summary>
        /// Past this range the swerve is dropped for a straight lunge. A bent path only
        /// converges on a target that is close; from across the arena the curve spends
        /// its whole sweep going somewhere the player is not.
        /// </summary>
        private const float CurveStraightRange = 300f;

        /// <summary>Radians per tick the boss turns during the spin attack.</summary>
        private const float SpinSpeed = 0.075f;

        /// <summary>Ticks between shots while spinning.</summary>
        private const int SpinInterval = 3;

        /// <summary>
        /// Ticks spent closing on the player before the spin starts. The attack is two
        /// seconds longer than it was and this is where they go — the spiral is laid down
        /// around the player instead of around wherever the previous attack happened to
        /// leave the boss.
        /// </summary>
        private const int SpinApproach = 120;

        /// <summary>How far above the player it settles before turning.</summary>
        private const float SpinStandoff = 80f;

        /// <summary>
        /// Ticks it sits anchored and shaking once it has arrived, before the first
        /// rotation. Three seconds, which is a long tell by this fight's standards and
        /// deliberately so — an expanding spiral cannot be dodged on reaction, so the
        /// warning has to land far enough ahead that there is time to pick a lane.
        /// </summary>
        private const int SpinShake = 180;

        /// <summary>Scatter on each spun shot, in degrees.</summary>
        private const float SpinSpread = 3f;

        /// <summary>
        /// Speed range for the spun shots. Varying it is what thickens the arms into
        /// bands — one fixed speed draws clean thin spirals with obvious gaps.
        /// </summary>
        private const float SpinSpeedMin = 6f;
        private const float SpinSpeedMax = 11f;
        private const int DashGapFar = 10;
        private const float DashFar = 900f;

        // ---- phase 3 -------------------------------------------------------

        /// <summary>Life fraction that hands the fight to phase 3.</summary>
        private const float Phase3At = 0.30f;

        /// <summary>
        /// What is left of the boss's defence once each phase has started. The armour
        /// coming off as the fight goes on is the reward for surviving it: the same
        /// weapon does noticeably more by phase 3 than it did at the opening.
        /// </summary>
        private const float Phase2Armour = 0.6f;
        private const float Phase3Armour = 0.25f;

        // The hand-over, in order: pan out, the storm, a beat, the sparks, a beat, pan
        // back. Sixteen seconds, and every leg is spelled out so the timings can be
        // moved one at a time without recounting the whole thing.
        private const int P3PanOut = 90;

        private const int P3BoltWindow = 180;
        private const int P3BoltGap = 48;
        private const int P3BoltCount = 3;

        private const int P3Wait = 120;

        private const int P3SparkGap = 72;
        private const int P3SparkCount = 5;
        private const int P3SparkWindow = P3SparkGap * P3SparkCount;

        private const int P3PanBack = 90;

        /// <summary>Everything between the two pans — the part the camera is parked for.</summary>
        private const int P3Body = P3BoltWindow + P3Wait + P3SparkWindow + P3Wait;

        private const int Phase3Length = P3PanOut + P3Body + P3PanBack;

        /// <summary>
        /// Where the phase 3 camera sits between the two of them. Under a half, so the
        /// player keeps the larger share of the screen — a straight midpoint puts the
        /// boss dead centre and leaves nowhere to read your own footing.
        /// </summary>
        private const float Phase3CameraBias = 0.4f;

        /// <summary>The colour of the storm the hand-over calls down.</summary>
        private static readonly Color BoltColor = new Color(196, 214, 255);

        // Attack 1 — scoped shots
        private const int ScopeHoverTime = 180;
        private const int ScopeBrake = 30;

        /// <summary>Ticks from the sight appearing to the shot leaving.</summary>
        private const int ScopeTime = 90;

        /// <summary>How much of that it spends still following the player.</summary>
        private const int ScopeTrack = 55;

        private const int ScopeShotCount = 3;

        private static readonly Color ScopeColor = new Color(184, 18, 65);

        private const int Phase3ScopeTime = ScopeHoverTime + ScopeBrake + ScopeShotCount * ScopeTime;

        // Attack 2 — the wild barrage
        private const int Phase3BarrageBrake = 30;

        /// <summary>Two seconds planted and shaking before a round leaves the barrel.</summary>
        private const int Phase3BarrageShake = 120;

        private const int Phase3BarrageTime = 240;
        private const int Phase3BarrageInterval = 5;

        /// <summary>
        /// Half what it was. At two or three rounds a burst the wide cone was filling the
        /// screen rather than threatening a part of it — tightening the scatter is what
        /// turns it back into something with a side to step to.
        /// </summary>
        private const float Phase3Spread = 7f;

        // Attack 3 — vent, then dash
        private const int SmokeWindup = 90;
        private const int Phase3DashTime = 480;

        /// <summary>Contact damage multiplier on the phase 3 dash.</summary>
        private const float Phase3DashDamage = 1.5f;

        // Attack 4 — the ring
        private const int Phase3OrbitTime = 600;
        private const float OrbitRadius = 450f;

        /// <summary>
        /// Ticks spent sliding onto the ring before anything turns, and then sitting on
        /// it shaking.
        ///
        /// The entry used to be a lerp that ran while the ring was ALREADY rotating, so
        /// the boss was chasing a point that kept moving out from under it — which is
        /// what made the opening look like it was stuttering. Locking the angle first and
        /// only turning once it has arrived is the whole fix.
        /// </summary>
        private const int OrbitApproach = 90;
        private const int OrbitShake = 120;

        /// <summary>Radians per tick. At this radius that is roughly two seconds a lap.</summary>
        private const float OrbitSpeed = 0.052f;
        /// <summary>
        /// Ticks between inward shots. Doubled — at seven the ring was laying down a
        /// solid wall of lead with no gaps to move through, which is the opposite of
        /// what the attack is for.
        /// </summary>
        private const int OrbitInterval = 14;

        /// <summary>Degrees either side of straight-out that vented smoke is thrown.</summary>
        private const float VentSpread = 2f;

        // ---- screen shake --------------------------------------------------

        /// <summary>
        /// The three magnitudes the scripted sequences shake at, kept together so they
        /// stay in proportion. Small for a spark or a wound, a real jolt for a lightning
        /// strike, and one big one at the end.
        ///
        /// Only the scripted sequences shake at all. The sparks running under phase 2 and
        /// phase 3 during the fight deliberately do not: a permanent tremor stops being
        /// punctuation and just makes the fight tiring to look at.
        /// </summary>
        private const float ShakeSpark = 2.5f;
        private const int ShakeSparkTime = 10;

        private const float ShakeBolt = 7f;
        private const int ShakeBoltTime = 20;

        private const float ShakeBlast = 18f;
        private const int ShakeBlastTime = 40;

        // ---- death ---------------------------------------------------------

        /// <summary>Ticks the camera takes to reach the boss once it has come to rest.</summary>
        private const int DeathPan = 90;

        /// <summary>
        /// The failure cascade. Each pass is a spray of blood from a fresh spot, then the
        /// spark that opens it — and once a spot has sparked it vents smoke for the rest
        /// of the animation.
        ///
        /// Four passes, each shorter than the last. The acceleration is the entire shape
        /// of the thing: evenly spaced failures read as a list, and a list has no ending.
        /// </summary>
        private static readonly int[] DeathBleed = { 90, 60, 36, 12 };
        private static readonly int[] DeathBurst = { 90, 60, 24, 6 };

        /// <summary>The pause after the last failure, before it goes.</summary>
        private const int DeathHold = 120;

        /// <summary>Ticks the camera takes to walk back to the player after the blast.</summary>
        private const int DeathOutro = 90;

        private const int DeathCascadeLength = 90 + 60 + 36 + 12 + 90 + 60 + 24 + 6;
        private const int DeathLength = DeathPan + DeathCascadeLength + DeathHold;

        /// <summary>How many wounds the cascade opens. One per pass.</summary>
        private const int Wounds = 4;

        /// <summary>
        /// Side of the square the wounds are picked from, as a multiple of the boss's own
        /// height, centred on its centre.
        ///
        /// A square and not the sprite's own rectangle. The hull is seventy pixels wide
        /// and twenty-eight tall, so spreading wounds across the full width put them out
        /// past the nose and the tail — which reads as the sea bleeding rather than the
        /// shark. One parameter, and it is the short side.
        /// </summary>
        private const float WoundBox = 1f;

        // ---------------------------------------------------------------- state

        private State Phase
        {
            get => (State)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }
         
        private ref float Timer => ref NPC.ai[1];
         
        private ref float Duration => ref NPC.ai[2];

        /// <summary>Free slot — dash progress, shot counters, whatever the state needs.</summary>
        private ref float Sub => ref NPC.ai[3];

        private bool enraged;

        /// <summary>0 to 1. Drives the red rage wash and the orange dash glow.</summary>
        private float rageGlow;
        private float heatGlow;

        private Vector2 dashFrom;
        private Vector2 dashTo;

        /// <summary>Where the sprite's nose actually points, in world space.</summary>
        private float aim;

        /// <summary>Depth the entrance starts from, recorded once it has submerged.</summary>
        private float riseFrom;

        /// <summary>1, 2 or 3. Which phase's attack rotation is running.</summary>
        private int tier = 1;

        // The curved dash is a circular arc rather than a line, so it needs a centre, a
        // radius, the angle it starts at and which way it bends.
        private Vector2 curveCenter;
        private float curveRadius;
        private float curveStart;
        private float curveTurn;

        /// <summary>Where the phase 3 sight is pointing. Tracks, then stops moving.</summary>
        private Vector2 scopeTarget;

        /// <summary>0 to 1 — how solid that sight is drawn. Zero means no sight at all.</summary>
        private float scopeAlpha;

        /// <summary>Angle around the player during the orbit.</summary>
        private float orbitAngle;

        /// <summary>Where the orbit set off from, so its entry can be a clean lerp.</summary>
        private Vector2 orbitFrom;

        /// <summary>Latched by CheckDead. Everything else in the AI defers to it.</summary>
        private bool dying;

        /// <summary>
        /// The death animation does not start its clock until the boss has actually come
        /// to rest, so the shot never opens on something still sliding.
        /// </summary>
        private bool deathStopped;

        /// <summary>Wound positions, as offsets from the centre.</summary>
        private readonly Vector2[] wounds = new Vector2[Wounds];

        /// <summary>How many of those have opened, and so are bleeding.</summary>
        private int bleedCount;

        /// <summary>How many have also sparked, and so are venting.</summary>
        private int ventCount;

        /// <summary>Where it finally went. The outro pan starts from here.</summary>
        private Vector2 deathAt;

        // The dash trail, recorded here rather than read from NPC.oldPos/oldRot. The
        // vanilla cache did not carry rotation the way its trailing modes suggest, so
        // every afterimage came out level while the boss was angled. Keeping our own also
        // means the ghosts remember which way they were mirrored.
        private readonly Vector2[] ghostPos = new Vector2[TrailLength];
        private readonly float[] ghostAim = new float[TrailLength];
        private readonly bool[] ghostFlip = new bool[TrailLength];

        /// <summary>Slots holding a real sample. Reset at the start of every dash.</summary>
        private int ghostCount;

        /// <summary>Ring of recent player positions, read by the dash for its lag.</summary>
        private readonly Vector2[] recent = new Vector2[DashLag];
        private int recentIndex;
        private bool recentReady;

        // ---------------------------------------------------------------- setup

        /// <summary>
        /// tModLoader looks for "&lt;Texture&gt;_Head_Boss" by default and the file here is
        /// named the other way round, so point at it explicitly rather than renaming an
        /// asset that is already in the repo.
        /// </summary>
        public override string BossHeadTexture => "PisAndDag/Content/NPCs/MegaShark/Megashark_Boss_Head";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

        }

        public override void SetDefaults()
        {
            NPC.width = 70;
            NPC.height = 28;

            NPC.aiStyle = -1;
            AIType = -1;

            NPC.lifeMax = LifeForMode();
            NPC.defense = DefenseForMode();
             
            NPC.damage = 0;

            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.npcSlots = 10f;

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            // No money. The kill is worth exactly one gun and nothing else, so the value
            // is zeroed rather than left to scatter coins over the wreck — NPC.value is
            // what vanilla pays out on death, independently of the loot table below.
            NPC.value = 0f;

            NPC.SpawnWithHigherTime(30);
        }

        /// <summary>
        /// Exact health per difficulty. Overridden rather than left to the automatic
        /// expert scaling, which would double whatever SetDefaults left behind and land
        /// nowhere near the intended numbers.
        /// </summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = LifeForMode();
            NPC.life = NPC.lifeMax;
            NPC.defense = DefenseForMode();
        }

        /// <summary>
        /// The health pool for the active difficulty, Legends included.
        ///
        /// Applied in BOTH SetDefaults and ApplyDifficultyAndPlayerScaling, deliberately.
        /// The hook alone was not landing — the boss kept spawning with the normal-mode
        /// 20000 in Master — and SetDefaults alone would be doubled on any path where
        /// vanilla's own scaling does run. Writing the finished number in both places is
        /// correct whichever of them fires.
        /// </summary>
        private static int LifeForMode()
        {
            int health = Main.masterMode ? 50000 : Main.expertMode ? 35000 : 20000;

            if (PADWorld.Legends)
                health = (int)(health * LegendsHealth);

            return health;
        }

        private static int DefenseForMode()
        {
            return Main.masterMode ? 60 : Main.expertMode ? 30 : 20;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Ocean
            });
        }

        /// <summary>
        /// The gun, and only the gun.
        ///
        /// Anything another mod's GlobalNPC bolts on afterwards is beyond reach from here,
        /// but nothing in this mod adds to it and the coin drop is off in SetDefaults.
        /// </summary>
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.RemoveWhere(_ => true);
            npcLoot.Add(ItemDropRule.Common(ItemID.Megashark));
        }

        // ---------------------------------------------------------------- damage

        /// <summary>
        /// Picks the value for the active difficulty and applies a multiplier.
        ///
        /// Master mode also reports expert, so master has to be tested first or every
        /// master value silently falls back to the expert one.
        /// </summary>
        private static int Difficulty(int normal, int expert, int master, float multiplier = 1f)
        {
            int value = Main.masterMode ? master : Main.expertMode ? expert : normal;
            return Math.Max(1, (int)Math.Round(value * multiplier));
        }

        /// <summary>
        /// One knob for the whole boss. Below 1 softens every shot, above 1 hardens them.
        /// Change this rather than the per-attack tables, which state intent.
        /// </summary>
        private const float ShotTrim = 1f;

        /// <summary>
        /// Turns an intended landed-damage figure into the number to hand NewProjectile.
        ///
        /// Two multiplications sit between the two. A hostile projectile's damage is
        /// doubled on its way into the player, and the game mode multiplies again on top
        /// of that — so in Master a shot spawned at 180 arrives at roughly 1080. Undoing
        /// both here is what makes the tables above mean what they say.
        ///
        /// This replaced GetAttackDamage_ForProjectiles_MultiLerp, which sounded like the
        /// right helper and was not: it left the aimed shot landing 800 against 62
        /// defense. Doing the arithmetic in the open is worth more here than a vanilla
        /// call whose behaviour has to be taken on trust.
        /// </summary>
        private static int ShotDamage(float normal, float expert, float master)
        {
            float intended = Main.masterMode ? master : Main.expertMode ? expert : normal;
            float pipeline = 2f * (Main.masterMode ? 3f : Main.expertMode ? 2f : 1f);

            return Math.Max(1, (int)Math.Round(intended * ShotTrim / pipeline));
        }

        /// <summary>True while a scripted sequence owns the boss.</summary>
        private bool InCutscene =>
            Phase == State.Rise || Phase == State.Phase2Intro || Phase == State.Phase3Intro;

        /// <summary>
        /// True only once the spin is actually turning. During its approach the boss
        /// should still face the player, so FaceAndTilt has to keep running.
        /// </summary>
        private bool Spinning => Phase == State.Spin && Timer >= SpinApproach + SpinShake;

        /// <summary>Either of the two curved-dash attacks. They share their whole body.</summary>
        private bool Curving => Phase == State.CurveDash || Phase == State.SmokeDash;

        /// <summary>True while a lunge of any kind is actually in flight.</summary>
        private bool Dashing => (Phase == State.Heat || Curving) && Sub > 0f;

        /// <summary>True while a sight is drawn, which is also when the nose follows it.</summary>
        private bool Scoping => Phase == State.ScopeShots && scopeAlpha > 0f;

        /// <summary>
        /// Contact damage multiplier for whichever dash is running. Phase 3 hits harder
        /// with the same move; reading it off the state means nothing extra to carry in
        /// the AI slots or across the network.
        /// </summary>
        private float DashWeight => Phase == State.SmokeDash ? Phase3DashDamage : 1f;

        /// <summary>
        /// Writes the defence for the current phase.
        ///
        /// Called every tick rather than once at the hand-over, because vanilla resets
        /// NPC.defense to defDefense on its own update — a one-shot write would be gone
        /// again by the next frame.
        /// </summary>
        private void ApplyArmour()
        {
            float scale = tier >= 3 ? Phase3Armour : tier >= 2 ? Phase2Armour : 1f;
            NPC.defense = Math.Max(0, (int)Math.Round(DefenseForMode() * scale));
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            // Only the dash ever connects, and it is meant to leave the player burning.
            target.AddBuff(BuffID.OnFire, 300);
        }

        // ---------------------------------------------------------------- ai

        public override void AI()
        {
            NPC.TargetClosest(false);
            Player player = Main.player[NPC.target];

            if (!player.active || player.dead)
            {
                Despawn();
                return;
            }

            // Nothing in this file despawns on distance — but vanilla does. NPC.timeLeft
            // ticks down whenever the NPC is off screen, and hitting zero removes it, so
            // a long chase or a retreat across the map would quietly end the fight.
            // Refreshing it every tick is what makes the boss stay until it or the player
            // is dead. The enrage relies on this: it is meant to hunt you across the
            // world, which is exactly the situation that would otherwise time it out.
            NPC.timeLeft = NPC.activeTime;

            // The death animation owns the boss outright. Bypassing the rest of the AI
            // rather than guarding each piece of it is deliberate — phase checks, the
            // enrage, the ambience and the phase 3 camera lock would all otherwise need a
            // "unless it is dying" clause, and one of them would eventually be missed.
            if (Phase == State.Dying)
            {
                NPC.dontTakeDamage = true;
                NPC.damage = 0;

                DoDeath(player);

                Timer++;
                return;
            }

            RecordRecent(player);

            // Contact damage is opt-in per state; the dash turns it on.
            NPC.damage = 0;

            // Untouchable for the whole of any cutscene. Ramping damage in over the tail
            // of one, as the entrance used to, just invites the player to burst it down
            // while it cannot fight back.
            NPC.dontTakeDamage = InCutscene;

            CheckPhase();
            ApplyArmour();

            if (tier >= 2 && !InCutscene)
                Damaged();

            CheckEnrage(player);
            FadeGlows();

            switch (Phase)
            {
                case State.Rise: DoRise(player); break;
                case State.Phase2Intro: DoPhase2Intro(player); break;
                case State.Phase3Intro: DoPhase3Intro(player); break;
                case State.CurveDash: DoCurveDash(player); break;
                case State.Spin: DoSpin(player); break;
                case State.Hover: DoHover(player); break;
                case State.Barrage: DoBarrage(player); break;
                case State.Heat: DoHeat(player); break;
                case State.ScopeShots: DoScopeShots(player); break;
                case State.WildBarrage: DoWildBarrage(player); break;
                case State.SmokeDash: DoSmokeDash(player); break;
                case State.Orbit: DoOrbit(player); break;
                case State.RageAimed: DoRageAimed(player); break;
                case State.RageMinigun: DoRageMinigun(player); break;
            }

            // Cutscenes and the spin drive their own facing; FaceAndTilt always points
            // at the player and would flatten both.
            if (!InCutscene && !Spinning)
                FaceAndTilt(player);

            // From phase 3 on the camera never goes back to the player. Written after
            // the attack has moved, so the frame the player sees is built on where the
            // boss actually ended up rather than where it was a tick ago.
            if (tier >= 3 && !InCutscene)
                LockCamera(player);

            Timer++;
        }

        /// <summary>
        /// The phase 3 camera. Parked between the two of them and nearer the player,
        /// with a blend of one — so it is the only thing writing the screen position and
        /// the player's own scope offset no longer shifts it.
        /// </summary>
        private void LockCamera(Player player)
        {
            if (Main.dedServ)
                return;

            PisPlayer.BlendCamera(Vector2.Lerp(player.Center, NPC.Center, Phase3CameraBias), 1f);
        }

        private void Despawn()
        {
            NPC.velocity.Y -= 0.4f;
            NPC.EncourageDespawn(10);
        }

        /// <summary>
        /// Rage latches. Once the player has run far enough the boss commits to hunting
        /// them and only their death calls it off, so backing off to reset the fight is
        /// not a strategy.
        /// </summary>
        private void CheckEnrage(Player player)
        {
            if (enraged)
                return;

            if (Vector2.Distance(NPC.Center, player.Center) < EnrageDistance)
                return;

            enraged = true;
            SwitchTo(State.RageAimed, 480f);
            NPC.netUpdate = true;
        }

        private void FadeGlows()
        {
            rageGlow = MathHelper.Clamp(rageGlow + (enraged ? 0.03f : -0.05f), 0f, 1f);

            // Phase 3 never cools down — by then the machine is running orange the whole
            // time and only the dashes are supposed to look normal by comparison.
            bool hot = tier >= 3 || Phase == State.Heat || Curving;
            heatGlow = MathHelper.Clamp(heatGlow + (hot ? 0.04f : -0.06f), 0f, 1f);
        }

        /// <summary>
        /// Hands the boss to a new state.
        ///
        /// IMPORTANT: the timer this zeroes is NOT the first value the new state sees.
        /// SwitchTo runs inside whatever was updating at the time, and AI() steps the
        /// timer at the end of that same tick — so a state entered from inside an
        /// attack's own body (via ChooseNextAttack) first runs at Timer 1, while one
        /// entered from CheckPhase or CheckEnrage, which run BEFORE the state switch,
        /// first runs at Timer 0.
        ///
        /// So a state must never do its entry work behind "Timer == 0". Anything that has
        /// to happen exactly once on entry is seeded here instead, where it is
        /// caller-independent; anything that merely has to happen early tests
        /// "Timer &lt;= 1f" and tolerates both.
        /// </summary>
        private void SwitchTo(State next, float duration)
        {
            Phase = next;
            Timer = 0f;
            Duration = duration;
            Sub = 0f;

            // Cleared here rather than at the end of the scope attack, so a phase change
            // or an enrage landing mid-lock takes the sight down with it instead of
            // leaving a line pinned to the last thing it aimed at.
            scopeAlpha = 0f;

            // The ring's entry. This used to sit behind "Timer == 0" inside DoOrbit,
            // which never fired — so orbitFrom kept its default of Vector2.Zero and the
            // first tick lerped the boss in from the top-left corner of the world.
            if (next == State.Orbit)
            {
                orbitFrom = NPC.Center;
                orbitAngle = (NPC.Center - Main.player[NPC.target].Center).ToRotation();
            }
        }

        /// <summary>
        /// Hands over to the next attack. Enrage owns its own two-attack loop and never
        /// returns to the phase-1 rotation.
        /// </summary>
        private void ChooseNextAttack()
        {
            if (enraged)
            {
                if (Phase == State.RageAimed)
                    SwitchTo(State.RageMinigun, 720f);
                else
                    SwitchTo(State.RageAimed, 480f);

                return;
            }

            if (tier >= 3)
            {
                switch (Phase)
                {
                    case State.ScopeShots:
                        SwitchTo(State.WildBarrage,
                            Phase3BarrageBrake + Phase3BarrageShake + Phase3BarrageTime);
                        break;
                    case State.WildBarrage: SwitchTo(State.SmokeDash, Phase3DashTime); break;
                    // The lead-in is added on top of the ring's own length, not taken
                    // out of it, so lengthening the wind-up never shortens the attack.
                    case State.SmokeDash:
                        SwitchTo(State.Orbit, OrbitApproach + OrbitShake + Phase3OrbitTime);
                        break;
                    default: SwitchTo(State.ScopeShots, Phase3ScopeTime); break;
                }

                NPC.netUpdate = true;
                return;
            }

            if (tier >= 2)
            {
                switch (Phase)
                {
                    case State.Hover: SwitchTo(State.CurveDash, Phase2CurveTime); break;
                    case State.CurveDash:
                        // Phase2SpinTime is the SPIRAL's length; the approach and the
                        // shake are added on top rather than taken out of it, so adding
                        // the wind-up did not quietly halve the attack.
                        SwitchTo(State.Spin, SpinApproach + SpinShake + Phase2SpinTime);
                        break;
                    default: SwitchTo(State.Hover, Phase2HoverTime); break;
                }

                NPC.netUpdate = true;
                return;
            }

            switch (Phase)
            {
                case State.Hover:
                    SwitchTo(State.Barrage, Main.rand.Next(BarrageTimeMin, BarrageTimeMax));
                    break;

                case State.Barrage:
                    SwitchTo(State.Heat, Main.rand.Next(840, 961));
                    break;

                default:
                    SwitchTo(State.Hover, Main.rand.Next(HoverTimeMin, HoverTimeMax));
                    break;
            }

            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------- entrance

        private void DoRise(Player player)
        {
            // Safe as an equality here, unlike everywhere else: Rise is the state the
            // boss spawns in rather than one it is switched into, so ai[1] is genuinely
            // still zero on the first update. See SwitchTo for why that does not hold
            // for any other state.
            if (Timer == 0f)
            {
                // Drop below the spawn point so there is something to rise out of.
                NPC.position.Y += 520f;
                riseFrom = NPC.Center.Y;
                NPC.netUpdate = true;
            }

            // Interpolated rather than driven by velocity. A velocity curve rises by
            // however much it happens to integrate to, which is why the breach kept
            // finishing below the player — where it ends up depends on the easing, not on
            // any stated height. Lerping to an explicit target lands it exactly.
            float surface = player.Center.Y - RiseAbove;

            NPC.velocity = Vector2.Zero;

            if (Timer <= PanOut)
            {
                NPC.Center = new Vector2(NPC.Center.X,
                    MathHelper.Lerp(riseFrom, surface, Ease(Timer / PanOut)));
            }
            else
            {
                // Keeps station above the player for the rest of the entrance, so walking
                // under it during the hold does not leave the boss behind.
                NPC.Center = new Vector2(NPC.Center.X,
                    MathHelper.Lerp(NPC.Center.Y, surface, 0.06f));
            }

            RiseCamera();
            RiseFacing(player);

            if (Timer >= RiseLength)
                SwitchTo(State.Hover, Main.rand.Next(HoverTimeMin, HoverTimeMax));
        }

        /// <summary>
        /// Out to the boss over a second, hold two, back over a second.
        ///
        /// The blend is eased at both ends of each leg — a linear pan starts and stops
        /// dead, which reads as a jump cut rather than a camera move.
        /// </summary>
        private void RiseCamera()
        {
            if (Main.dedServ)
                return;

            float blend;

            if (Timer < PanOut)
                blend = Ease(Timer / PanOut);
            else if (Timer < PanOut + RiseHold)
                blend = 1f;
            else
                blend = 1f - Ease((Timer - PanOut - RiseHold) / PanBack);

            PisPlayer.BlendCamera(NPC.Center, blend);
        }

        /// <summary>Smoothstep. Zero slope at both ends, so pans start and stop softly.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Nose straight up out of the water, then a slow turn onto the player across the
        /// hold — which is the whole reason the camera stays parked for two seconds.
        ///
        /// Handles its own facing rather than deferring to FaceAndTilt, which always
        /// points at the player and would rob the entrance of its turn.
        /// </summary>
        private void RiseFacing(Player player)
        {
            Vector2 toPlayer = player.Center - NPC.Center;
            float onPlayer = toPlayer.ToRotation();

            const float up = -MathHelper.PiOver2;

            if (Timer < PanOut)
            {
                // Breaching. Unmirrored, so the drawn rotation is the true angle.
                NPC.direction = 1;
                NPC.spriteDirection = 1;
                aim = up;
                return;
            }

            int facing = toPlayer.X >= 0f ? 1 : -1;
            NPC.direction = facing;
            NPC.spriteDirection = facing;

            if (Timer < PanOut + RiseHold)
            {
                // Absolute rather than incremental: lerping from the fixed `up` by a
                // progress value lands exactly on the player at the end of the hold,
                // where a per-frame lerp would still be easing when the camera left.
                aim = up.AngleLerp(onPlayer, Ease((Timer - PanOut) / RiseHold));
                return;
            }

            aim = aim.AngleLerp(onPlayer, 0.15f);
        }

        // ---------------------------------------------------------------- phase 2

        /// <summary>
        /// Hands the fight to phase 2 once the health drops through the threshold.
        ///
        /// Never fires during a cutscene: the entrance can be shot through, and letting a
        /// hand-over start on top of one would leave two scripts driving the same boss.
        /// </summary>
        private void CheckPhase()
        {
            if (enraged || InCutscene)
                return;

            if (tier == 1 && NPC.life <= NPC.lifeMax * Phase2At)
            {
                tier = 2;
                SwitchTo(State.Phase2Intro, Phase2Length);
                NPC.netUpdate = true;
                return;
            }

            if (tier == 2 && NPC.life <= NPC.lifeMax * Phase3At)
            {
                tier = 3;
                SwitchTo(State.Phase3Intro, Phase3Length);
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// The hand-over. Camera out for a second, three seconds of the boss tearing
        /// itself apart, camera back.
        ///
        /// It holds position rather than freezing outright — a boss that stops dead reads
        /// as a paused game, one that drifts reads as a machine in trouble.
        /// </summary>
        private void DoPhase2Intro(Player player)
        {
            NPC.velocity *= 0.94f;
            NPC.velocity.Y -= 0.03f;

            float blend;

            if (Timer < P2PanOut)
                blend = Ease(Timer / P2PanOut);
            else if (Timer < P2PanOut + P2Sparks)
                blend = 1f;
            else
                blend = 1f - Ease((Timer - P2PanOut - P2Sparks) / P2PanBack);

            if (!Main.dedServ)
                PisPlayer.BlendCamera(NPC.Center, blend);

            float spark = Timer - P2PanOut;

            if (spark >= 0f && spark < P2Sparks)
            {
                // Fixed moments rather than random ones, so they land as beats. Tested
                // against the exact tick, which is safe because the timer steps by one.
                float since = spark - P2SparkDelay;

                if (since >= 0f && since < P2SparkCount * P2SparkGap && since % P2SparkGap == 0f)
                    Rupture();

                // Smoke pours the whole way through, thickest right after each rupture.
                if (Main.rand.NextBool(3))
                    Smoke(1.1f);
            }

            NPC.rotation = aim;

            if (Timer >= Phase2Length)
                SwitchTo(State.Hover, Phase2HoverTime);
        }

        /// <summary>
        /// One of the two big spark events: an arc storm at a point on the hitbox, a
        /// puff of smoke and a short jolt of the camera.
        /// </summary>
        /// <param name="scale">
        /// Size of the event. The phase 3 sparks are meant to read as small and frequent
        /// rather than as three big failures, so they come through here under 1.
        /// </param>
        private void Rupture(float scale = 1f)
        {
            if (Main.dedServ)
                return;

            // Somewhere on the body, not the centre — the damage should look like it
            // found a weak spot rather than a spawn point.
            Vector2 spot = NPC.Center + new Vector2(
                Main.rand.NextFloat(-NPC.width * 0.4f, NPC.width * 0.4f),
                Main.rand.NextFloat(-NPC.height * 0.4f, NPC.height * 0.4f));

            // Six arcs, wider and held longer than they were. A rupture is the loudest
            // thing either hand-over does and it was reading quieter than the smoke
            // sitting on top of it.
            for (int i = 0; i < 6; i++)
            {
                Vector2 to = spot + Main.rand.NextVector2Circular(40f * scale, 32f * scale);
                Lightning.Spawn(spot, to, SparkColor, width: 16f * scale, lifetime: 18, forks: 1);
            }

            for (int i = 0; i < 14; i++)
            {
                Dust dust = Dust.NewDustPerfect(spot, DustID.Torch,
                    Main.rand.NextVector2Circular(3.2f, 3.2f), 0, default, 1.3f * scale);
                dust.noGravity = true;
            }

            // Deliberately under the spark's own size: the smoke is the aftermath, and at
            // full scale it swallowed the arc that caused it.
            VentSmoke(spot, scale * 0.55f);

            Shake(ShakeSpark * scale, ShakeSparkTime);

            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.7f, Pitch = -0.3f }, NPC.Center);
        }

        /// <summary>
        /// The smoke a rupture pushes out.
        ///
        /// Thrown along the line from the boss's centre out through the break, so it
        /// reads as pressure escaping a hole rather than as a puff sitting on the sprite.
        /// Most of it goes straight out and the rest is offset by two degrees either
        /// side — just enough to give the plume an edge without spreading it into a
        /// cloud, which would lose the direction that makes it legible in the first
        /// place.
        /// </summary>
        private void VentSmoke(Vector2 spot, float scale)
        {
            if (Main.dedServ)
                return;

            // Falls back to straight up rather than to zero: a rupture that happened to
            // land exactly on the centre would otherwise emit smoke with no velocity at
            // all, which just piles up in place.
            Vector2 outward = (spot - NPC.Center).SafeNormalize(-Vector2.UnitY);

            int count = Math.Max(3, (int)(9 * scale));

            for (int i = 0; i < count; i++)
            {
                float offset = i % 3 == 0
                    ? 0f
                    : MathHelper.ToRadians(i % 3 == 1 ? VentSpread : -VentSpread);

                Vector2 velocity = outward.RotatedBy(offset) *
                    Main.rand.NextFloat(1.8f, 4.4f) * scale;

                // Spawned directly rather than through MuzzleSmoke.Spawn, which fans its
                // cone across sixteen degrees. This one has to stay a jet: the direction
                // is the whole point of it, and two degrees is all the edge it wants.
                PADParticleHandler.SpawnParticle(new MuzzleSmoke(
                    spot + Main.rand.NextVector2Circular(4f, 4f), velocity,
                    Main.rand.Next(30, 55), Main.rand.NextFloat(0.45f, 0.8f) * scale));
            }
        }

        /// <summary>
        /// Smoke off the hull, rising and drifting.
        ///
        /// The mod's own powder smoke rather than vanilla's dust — same particle the guns
        /// cough out of the barrel. It slows, swells and thins out instead of just
        /// floating, which is what makes it read as smoke rather than as grey specks, and
        /// it keeps the boss looking like it belongs to the same world as the weapons.
        /// </summary>
        private void Smoke(float scale, int count = 1)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < count; i++)
            {
                Vector2 from = NPC.Center + new Vector2(
                    Main.rand.NextFloat(-NPC.width * 0.45f, NPC.width * 0.45f),
                    Main.rand.NextFloat(-NPC.height * 0.4f, NPC.height * 0.4f));

                Vector2 drift = new Vector2(
                    Main.rand.NextFloat(-0.5f, 0.5f),
                    Main.rand.NextFloat(-1.8f, -0.7f));

                PADParticleHandler.SpawnParticle(new MuzzleSmoke(from, drift,
                    Main.rand.Next(30, 55), Main.rand.NextFloat(0.3f, 0.5f) * scale));
            }
        }

        /// <summary>
        /// The damage the boss carries with it once it is past phase 1: steady smoke and
        /// the occasional small spark somewhere on the hull.
        ///
        /// Phase 3 is the same idea turned up — the smoke is close to continuous and much
        /// bigger, and the sparks come about four times as often. Nothing here does
        /// damage; it is entirely there so the machine looks like it is losing.
        /// </summary>
        private void Damaged()
        {
            if (Main.dedServ)
                return;

            bool late = tier >= 3;

            // A quarter of the smoke it used to make, at half the size. Three big puffs
            // every tick buried the sprite and left a solid rope hanging behind the
            // orbit — this is meant to say the machine is failing, not to hide it.
            if (Main.rand.NextBool(late ? 2 : 4))
                Smoke(late ? 1f : 0.85f);

            // The sparks carry phase 3 instead, so they run four times as often and much
            // bigger. Smoke reads as a haze at any volume; an arc reads as damage, and
            // that is the thing worth spending the frame on.
            if (!Main.rand.NextBool(late ? 5 : 70))
                return;

            Vector2 spot = NPC.Center + new Vector2(
                Main.rand.NextFloat(-NPC.width * 0.42f, NPC.width * 0.42f),
                Main.rand.NextFloat(-NPC.height * 0.4f, NPC.height * 0.4f));

            float reach = late ? 1.7f : 1f;

            Lightning.Spawn(spot,
                spot + Main.rand.NextVector2Circular(22f * reach, 18f * reach),
                SparkColor,
                width: late ? 15f : 7f,
                lifetime: late ? 16 : 9,
                forks: late ? 1 : 0);

            if (!late)
                return;

            // A few embers at the root of the arc. The bolt itself is additive and thin,
            // so this is what actually catches the eye against a dark sky.
            for (int i = 0; i < 5; i++)
            {
                Dust dust = Dust.NewDustPerfect(spot, DustID.Torch,
                    Main.rand.NextVector2Circular(2.2f, 2.2f), 0, default, 1.2f);
                dust.noGravity = true;
            }

            VentSmoke(spot, 0.3f);
        }

        // ---------------------------------------------------------------- phase 3 intro

        /// <summary>
        /// The phase 3 hand-over. Sixteen seconds, in six legs:
        ///
        /// pan out, three bolts out of a sky that turns to rain as the first one lands,
        /// a beat of nothing, five small failures along the hull, another beat, and a pan
        /// that ends on the phase 3 camera hold rather than back on the player.
        ///
        /// The two dead beats are doing real work. Without them the bolts run straight
        /// into the sparks and the whole thing reads as one continuous effect instead of
        /// two things happening to the same machine.
        /// </summary>
        private void DoPhase3Intro(Player player)
        {
            NPC.velocity *= 0.94f;
            NPC.velocity.Y -= 0.03f;

            Phase3IntroCamera(player);

            float since = Timer - P3PanOut;

            // The storm. Rain is forced on the first bolt rather than at tick zero, so
            // the weather turning and the first strike are the same beat.
            if (since >= 0f && since < P3BoltWindow && since % P3BoltGap == 0f
                && since / P3BoltGap < P3BoltCount)
            {
                if (since == 0f)
                    ForceRain();

                Bolt();
            }

            // The sparks, one leg and one beat later.
            float sparkAt = since - P3BoltWindow - P3Wait;

            if (sparkAt >= 0f && sparkAt < P3SparkWindow && sparkAt % P3SparkGap == 0f)
            {
                // Rupture brings its own shake, scaled to the 0.65 above. Adding a
                // second one here just stacked two jolts on the same frame.
                Rupture(0.65f);
            }

            // Smoke the whole way through, and plenty of it — this is where the phase 3
            // silhouette is established.
            if (!Main.dedServ && Main.rand.NextBool(3))
                Smoke(1.2f);

            NPC.rotation = aim;

            if (Timer >= Phase3Length)
                SwitchTo(State.ScopeShots, Phase3ScopeTime);
        }

        /// <summary>
        /// Out to the boss, parked for the body, then back — except the pan back does not
        /// go back to the player. It lands on the phase 3 hold, so the fight resumes with
        /// the camera already where it is going to stay.
        /// </summary>
        private void Phase3IntroCamera(Player player)
        {
            if (Main.dedServ)
                return;

            Vector2 hold = Vector2.Lerp(player.Center, NPC.Center, Phase3CameraBias);

            if (Timer < P3PanOut)
            {
                PisPlayer.BlendCamera(NPC.Center, Ease(Timer / P3PanOut));
                return;
            }

            if (Timer < P3PanOut + P3Body)
            {
                PisPlayer.BlendCamera(NPC.Center, 1f);
                return;
            }

            // Blend stays at 1 and the FOCUS moves instead. Dropping the blend would hand
            // the camera back to the player for the length of the pan, which is the one
            // thing phase 3 is not supposed to do.
            float t = Ease((Timer - P3PanOut - P3Body) / P3PanBack);
            PisPlayer.BlendCamera(Vector2.Lerp(NPC.Center, hold, t), 1f);
        }

        /// <summary>
        /// One strike, out of the top of the screen and onto a point near the boss.
        ///
        /// The screen is measured off the boss rather than off the camera because during
        /// the hand-over they are the same thing, and reading it off the boss keeps the
        /// strikes framed correctly even on a wider window.
        /// </summary>
        private void Bolt()
        {
            if (Main.dedServ)
                return;

            Vector2 hit = NPC.Center + new Vector2(
                Main.rand.NextFloat(-Main.screenWidth * 0.4f, Main.screenWidth * 0.4f),
                Main.rand.NextFloat(-Main.screenHeight * 0.32f, Main.screenHeight * 0.34f));

            // Starts above the top edge, so the bolt is already in flight when it enters
            // frame rather than appearing out of nothing.
            Vector2 from = new Vector2(
                hit.X + Main.rand.NextFloat(-140f, 140f),
                NPC.Center.Y - Main.screenHeight * 0.75f);

            Lightning.Spawn(from, hit, BoltColor, width: 34f, lifetime: 26, forks: 4);

            for (int i = 0; i < 16; i++)
            {
                Dust dust = Dust.NewDustPerfect(hit, DustID.Electric,
                    Main.rand.NextVector2Circular(5.5f, 5.5f), 0, default, 1.6f);
                dust.noGravity = true;
            }

            Shake(ShakeBolt, ShakeBoltTime);

            // No SoundID.Thunder to lean on here, so the crack is built: a deep boom for
            // the body of it and the electric snap on top for the strike itself.
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.7f }, hit);
            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.6f, Pitch = 0.4f }, hit);
        }

        /// <summary>
        /// Turns the weather over immediately.
        ///
        /// Server-side only, and pushed to clients afterwards — rain lives in the world
        /// state, so a client setting it locally would be corrected on the next sync and
        /// the sky would clear halfway through the phase.
        /// </summary>
        private static void ForceRain()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Main.raining = true;
            Main.rainTime = 3600 * 8;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        // ---------------------------------------------------------------- phase 3 attack 1

        /// <summary>
        /// Opens as the phase 1 hold, and then stops.
        ///
        /// The stop is the attack. Three seconds of familiar drifting, a coast to a halt,
        /// and then three deliberate shots — each one announced by a sight that swings
        /// onto the player, brightens, and freezes on the spot they were standing. The
        /// bullet goes where the sight stopped, not where the player ended up, so the
        /// answer is always to leave after the lock rather than before it.
        /// </summary>
        private void DoScopeShots(Player player)
        {
            if (Timer < ScopeHoverTime)
            {
                float bob = MathF.Sin(Timer * HoverBobSpeed) * HoverBobHeight;
                float sweep = MathF.Sin(Timer * HoverSweepSpeed) * HoverSweepWidth;

                Vector2 destination = player.Center + new Vector2(sweep, -HoverHeight + bob);
                destination.Y = Math.Min(destination.Y, player.Center.Y - HoverHeight * 0.5f);

                Approach(destination, 0.35f, 15f);
                return;
            }

            // Coasts down rather than braking hard — it should look like a machine
            // settling to aim, not like the game paused.
            NPC.velocity *= 0.9f;

            float since = Timer - ScopeHoverTime - ScopeBrake;

            if (since < 0f)
                return;

            int shot = (int)(since / ScopeTime);

            if (shot >= ScopeShotCount)
            {
                scopeAlpha = 0f;
                ChooseNextAttack();
                return;
            }

            float step = since % ScopeTime;

            if (step == 0f)
            {
                // Opens on the boss's own nose and swings out from there, so the sight is
                // seen arriving on the player rather than materialising on top of them.
                scopeTarget = Muzzle() + aim.ToRotationVector2() * 120f;
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.5f, Pitch = 0.6f }, NPC.Center);
            }

            if (step < ScopeTrack)
            {
                // Still hunting. Eased toward the player rather than snapped, and held
                // dim, because a sight that is bright while it still moves reads as a
                // threat the player cannot act on yet.
                scopeTarget = Vector2.Lerp(scopeTarget, player.Center, 0.16f);
                scopeAlpha = step / ScopeTrack * 0.5f;
            }
            else
            {
                // Locked: the point stops moving and only the brightness changes. That
                // is the entire tell, and it lasts about half a second.
                scopeAlpha = MathHelper.Lerp(0.5f, 1f, (step - ScopeTrack) / (ScopeTime - ScopeTrack));
            }

            if (step == ScopeTime - 1f)
            {
                Vector2 velocity = (scopeTarget - Muzzle()).SafeNormalize(Vector2.UnitX) * 19f;

                SoundEngine.PlaySound(SoundID.Item38 with { Volume = 0.85f }, NPC.Center);

                // Twice the phase 1 aimed shot, which is what the wind-up is paying for.
                Fire(ProjectileID.BulletHighVelocity, velocity,
                    ShotDamage(120f * PhaseOneDamage, 180f * PhaseOneDamage, 300f * PhaseOneDamage));

                scopeAlpha = 0f;

                if (!Main.dedServ)
                    Shake(3f, 8);
            }
        }

        // ---------------------------------------------------------------- phase 3 attack 2

        /// <summary>
        /// The phase 1 barrage with the discipline gone: two or three rounds a burst
        /// instead of one, three times the spread, and the boss shaking hard enough that
        /// the cone wanders on its own.
        ///
        /// Half the length of the phase 1 version, because at this rate a full one would
        /// be a wall rather than an attack.
        /// </summary>
        private void DoWildBarrage(Player player)
        {
            if (Timer < Phase3BarrageBrake)
            {
                NPC.velocity *= 0.9f;
                return;
            }

            if (Timer < Phase3BarrageBrake + Phase3BarrageShake)
            {
                NPC.velocity *= 0.8f;
                NPC.position += Main.rand.NextVector2Circular(4f, 4f);
                return;
            }

            NPC.velocity *= 0.86f;
            NPC.position += Main.rand.NextVector2Circular(4.5f, 4.5f);

            if (Timer % Phase3BarrageInterval == 0f)
            {
                int rounds = Main.rand.Next(2, 4);

                for (int i = 0; i < rounds; i++)
                {
                    Vector2 shot = (player.Center - Muzzle()).SafeNormalize(Vector2.UnitX);
                    float spread = MathHelper.ToRadians(Main.rand.NextFloat(-Phase3Spread, Phase3Spread));

                    Fire(ProjectileID.Bullet,
                        shot.RotatedBy(spread) * Main.rand.NextFloat(5f, 12f),
                        ShotDamage(35f * PhaseOneDamage, 60f * PhaseOneDamage, 80f * PhaseOneDamage));
                }
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- phase 3 attack 3

        /// <summary>
        /// The phase 2 curved dash, announced.
        ///
        /// A second and a half of shaking and venting first, which is the only warning the
        /// player gets and is worth giving because these lunges land at one and a half
        /// times the weight of the phase 2 ones.
        /// </summary>
        private void DoSmokeDash(Player player)
        {
            if (Timer < SmokeWindup)
            {
                NPC.velocity *= 0.9f;
                NPC.position += Main.rand.NextVector2Circular(5f, 5f);

                if (!Main.dedServ)
                {
                    if (Main.rand.NextBool(2))
                        Smoke(1.4f);

                    if (Timer % 15f == 0f)
                    {
                        Vector2 spot = NPC.Center + Main.rand.NextVector2Circular(
                            NPC.width * 0.4f, NPC.height * 0.4f);

                        VentSmoke(spot, 0.9f);
                    }

                    // Not "== 0": this state is entered from inside another attack's
                    // update, so its first frame is Timer 1 and the shake and the boom
                    // were being skipped outright.
                    if (Timer <= 1f)
                    {
                        Shake(6f, SmokeWindup);
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.5f }, NPC.Center);
                    }
                }

                return;
            }

            CurveBody(player);
        }

        // ---------------------------------------------------------------- phase 3 attack 4

        /// <summary>
        /// Rings the player at a fixed radius and fires inward the whole way round.
        ///
        /// Position is written straight onto the circle rather than steered toward it —
        /// at this speed a steering approach spirals in and never closes the ring. The
        /// opening ticks ease on so it slides into the orbit from wherever the previous
        /// attack left it instead of teleporting.
        ///
        /// The shots are scattered rather than aimed. A ring of perfectly aimed fire is
        /// just a kill; a ring of scattered fire has gaps in it that move, and moving
        /// through them is the attack the player is actually being asked to solve.
        /// </summary>
        private void DoOrbit(Player player)
        {
            // orbitAngle and orbitFrom are seeded by SwitchTo — see the note there on
            // why entry work cannot live behind a Timer check. The angle is locked from
            // that moment and does not move until the ring starts turning, so the entry
            // point is a fixed place on the circle. It still tracks the player, because
            // the circle is drawn around them.
            Vector2 entry = player.Center + orbitAngle.ToRotationVector2() * OrbitRadius;

            if (Timer < OrbitApproach)
            {
                // Eased along a straight line to the entry point. Nothing is rotating yet.
                NPC.Center = Vector2.Lerp(orbitFrom, entry, Ease(Timer / OrbitApproach));
                NPC.velocity = Vector2.Zero;
                return;
            }

            if (Timer < OrbitApproach + OrbitShake)
            {
                // On the mark and shaking. Two seconds of it, which is the only warning
                // that four hundred and fifty pixels of arena is about to become a ring.
                NPC.Center = entry + Main.rand.NextVector2Circular(4f, 4f);
                NPC.velocity = Vector2.Zero;
                return;
            }

            orbitAngle += OrbitSpeed;

            Vector2 was = NPC.Center;
            Vector2 target = player.Center + orbitAngle.ToRotationVector2() * OrbitRadius;

            NPC.Center = target;

            // Derived from the step, so facing follows the real path rather than a
            // velocity nothing is actually using.
            NPC.velocity = NPC.Center - was;

            // No afterimages and no dust of its own here, deliberately. A dash is over in
            // half a second, so its trail reads as speed; this runs for ten straight
            // seconds, and a permanent stack of ghosts just looks like several bosses.
            // The hull smoke from Damaged() is the only trail the orbit gets.

            if ((Timer - OrbitApproach - OrbitShake) % OrbitInterval == 0f)
            {
                Vector2 inward = (player.Center - Muzzle()).SafeNormalize(Vector2.UnitX);
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-22f, 22f));

                Fire(ProjectileID.Bullet,
                    inward.RotatedBy(spread) * Main.rand.NextFloat(7f, 13f),
                    ShotDamage(35f * PhaseOneDamage, 60f * PhaseOneDamage, 80f * PhaseOneDamage));
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- phase 2 attack 2

        /// <summary>
        /// The burning dash again, but along a sixth of a circle instead of a straight
        /// line — same distance travelled, harder to read where it ends up.
        /// </summary>
        private void DoCurveDash(Player player)
        {
            if (Timer < HeatWindup)
            {
                NPC.velocity *= 0.88f;
                NPC.position += Main.rand.NextVector2Circular(3.5f, 3.5f);
                return;
            }

            CurveBody(player);
        }

        /// <summary>
        /// The dash loop shared by the phase 2 and phase 3 versions: wait out the gap,
        /// lay out a fresh arc, run it. Only the wind-up in front of it and the weight it
        /// lands with differ between the two, so neither is worth a second copy of this.
        /// </summary>
        private void CurveBody(Player player)
        {
            if (Sub < 0f)
            {
                Sub++;
                NPC.velocity *= 0.9f;
                return;
            }

            if (Sub <= 0f)
            {
                StartCurve(player);
                return;
            }

            // curveRadius of zero is the marker for the straight fallback picked in
            // StartCurve, so both kinds of lunge share this attack's timing.
            if (curveRadius <= 0f)
                RunDash();
            else
                RunCurve();

            if (Timer >= Duration && Sub <= 0f)
                ChooseNextAttack();
        }

        /// <summary>
        /// Lays out the arc.
        ///
        /// The radius comes from the distance, not the other way round: an arc of a fixed
        /// angle covering a chosen length has exactly one radius, so the swerve keeps the
        /// same travel as the straight phase-1 dash while bending through CurveArc.
        /// </summary>
        private void StartCurve(Player player)
        {
            float distance = Main.rand.NextFloat(DashMin, DashMax);

            Vector2 target = DashTarget(player);
            target.Y = player.Center.Y + BandOffset();

            float toTarget = (target - NPC.Center).ToRotation();

            // Far away, the swerve is worse than useless — take the straight dash. The
            // curve is a close-quarters move.
            if (Vector2.Distance(NPC.Center, player.Center) > CurveStraightRange)
            {
                curveRadius = 0f;
                dashFrom = NPC.Center;
                dashTo = NPC.Center + toTarget.ToRotationVector2() * distance;

                Sub = 0.0001f;
                ghostCount = 0;
                NPC.netUpdate = true;

                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.4f, Volume = 0.5f }, NPC.Center);
                return;
            }

            curveRadius = distance / CurveArc;
            curveTurn = Main.rand.NextBool() ? CurveArc : -CurveArc;

            // Aiming the START of the arc at the player misses by design: the path bends
            // away from wherever it set off. What has to point at them is the CHORD, and a
            // circular arc's chord sits exactly half the total turn off its opening
            // heading — so backing off by half the sweep lands the far end on the target.
            float heading = toTarget - curveTurn * 0.5f;

            // The centre sits one radius off to the side it bends toward, and the start
            // angle points from there back to the boss.
            Vector2 inward = heading.ToRotationVector2().RotatedBy(curveTurn > 0f ? MathHelper.PiOver2 : -MathHelper.PiOver2);

            curveCenter = NPC.Center + inward * curveRadius;
            curveStart = (NPC.Center - curveCenter).ToRotation();

            Sub = 0.0001f;
            ghostCount = 0;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.55f, Volume = 0.5f }, NPC.Center);
        }

        /// <summary>
        /// Walks the arc. Position is set directly and velocity is derived from the step,
        /// so facing and the afterimages still follow the real motion.
        /// </summary>
        private void RunCurve()
        {
            NPC.damage = Difficulty(60, 90, 150, PhaseOneDamage * DashWeight);

            float progress = MathHelper.Clamp(Sub, 0f, 1f);
            float curve = 0.22f + 0.78f * MathF.Sin(progress * MathHelper.Pi);

            // Arc length is radius times angle, so this is the straight dash's speed
            // expressed as a fraction of the sweep.
            float step = 26f * curve / (curveRadius * MathF.Abs(curveTurn));

            float next = progress + step;

            Vector2 was = NPC.Center;
            Vector2 now = curveCenter + (curveStart + curveTurn * next).ToRotationVector2() * curveRadius;

            NPC.Center = now;
            NPC.velocity = now - was;

            PushGhost();

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 12f),
                    DustID.Torch, -NPC.velocity * 0.2f, 0, default, 1.4f);
                dust.noGravity = true;
            }

            if (next >= 1f)
            {
                NPC.velocity *= 0.2f;
                Sub = -CurveGap;
                NPC.netUpdate = true;
                return;
            }

            Sub = next;
        }

        // ---------------------------------------------------------------- phase 2 attack 3

        /// <summary>
        /// Anchors, then spins and fires along its own rotation rather than at the player.
        ///
        /// Because the shots follow the nose and the nose turns at a constant rate, they
        /// lay down an expanding spiral — the safe ground is between the arms, and it
        /// moves, so the answer is to keep circling rather than to dodge each round.
        /// </summary>
        private void DoSpin(Player player)
        {
            // Close first. A spiral centred on the player has no safe standing ground;
            // one centred fifty tiles away is just scenery.
            if (Timer < SpinApproach)
            {
                Approach(player.Center + new Vector2(0f, -SpinStandoff), 0.6f, 17f);
                return;
            }

            if (Timer < SpinApproach + SpinShake)
            {
                // Anchored, and shaking harder than it does anywhere else. Still facing
                // the player through all of it, which is what sells it as a wind-up
                // rather than as the attack having already started.
                NPC.velocity *= 0.82f;
                NPC.position += Main.rand.NextVector2Circular(4f, 4f);
                return;
            }

            NPC.velocity = Vector2.Zero;
            NPC.position += Main.rand.NextVector2Circular(2.5f, 2.5f);

            // Clockwise on screen: Y grows downward, so a rising angle turns that way.
            aim += SpinSpeed;
            aim = MathHelper.WrapAngle(aim);

            NPC.direction = MathF.Cos(aim) >= 0f ? 1 : -1;
            NPC.spriteDirection = NPC.direction;
            NPC.rotation = aim;

            if (Timer % SpinInterval == 0f)
            {
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-SpinSpread, SpinSpread));

                Fire(ProjectileID.Bullet,
                    (aim + spread).ToRotationVector2() * Main.rand.NextFloat(SpinSpeedMin, SpinSpeedMax),
                    ShotDamage(35f * PhaseOneDamage, 60f * PhaseOneDamage, 80f * PhaseOneDamage));
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- attack 1

        /// <summary>
        /// Holds station above the player on two out-of-step sine waves — a quick shallow
        /// bob and a slow wide sweep. Two different periods is what stops it tracing a
        /// visible ellipse.
        /// </summary>
        private void DoHover(Player player)
        {
            float bob = MathF.Sin(Timer * HoverBobSpeed) * HoverBobHeight;
            float sweep = MathF.Sin(Timer * HoverSweepSpeed) * HoverSweepWidth;

            Vector2 destination = player.Center + new Vector2(sweep, -HoverHeight + bob);

            // Never allowed to sink to or below the player — the attack is defined by
            // being overhead.
            destination.Y = Math.Min(destination.Y, player.Center.Y - HoverHeight * 0.5f);

            Approach(destination, 0.35f, 15f);

            if (Timer > 30f && Timer % HoverShotInterval == 0f)
            {
                // Phase 2 scatters the shot rather than placing it — the same attack, but
                // no longer something you can stand still and sidestep on cue.
                FireAimed(player, 10f, ShotDamage(60f * PhaseOneDamage, 90f * PhaseOneDamage, 150f * PhaseOneDamage),
                    tier >= 2 ? Phase2Spread : 0f);
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- attack 2

        /// <summary>
        /// Brakes to a stop, shudders while it spins up, then empties downrange. The
        /// shake is the tell — it is the player's second to get out of the cone.
        /// </summary>
        private void DoBarrage(Player player)
        {
            if (Timer < BarrageBrake)
            {
                NPC.velocity *= 0.9f;
                return;
            }

            if (Timer < BarrageBrake + BarrageShake)
            {
                NPC.velocity = Vector2.Zero;
                NPC.position += Main.rand.NextVector2Circular(3f, 3f);
                return;
            }

            NPC.velocity = Vector2.Zero;

            if (Timer % BarrageInterval == 0f)
            {
                Vector2 shot = (player.Center - Muzzle()).SafeNormalize(Vector2.UnitX);
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-BarrageSpread, BarrageSpread));

                Fire(ProjectileID.Bullet,
                    shot.RotatedBy(spread) * Main.rand.NextFloat(6f, 9f),
                    ShotDamage(35f * PhaseOneDamage, 60f * PhaseOneDamage, 80f * PhaseOneDamage));
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- attack 3

        /// <summary>
        /// Glows hot, then runs a series of dashes with contact damage on.
        /// </summary>
        private void DoHeat(Player player)
        {
            if (Timer < HeatWindup)
            {
                NPC.velocity *= 0.88f;
                NPC.position += Main.rand.NextVector2Circular(3.5f, 3.5f);
                return;
            }

            // Sub does double duty: negative is the countdown to the next dash, positive
            // is progress along the current one. The countdown has to be advanced here —
            // parking it at a negative number and testing "Sub <= 0" alone meant the very
            // next tick started another dash and the interval never happened.
            if (Sub < 0f)
            {
                Sub++;
                NPC.velocity *= 0.92f;
                NPC.position += Main.rand.NextVector2Circular(1.5f, 1.5f);

                // Keeps climbing and sinking through the band while it waits, so no two
                // dashes in a row come in at the same height. X is left alone — this is
                // a repositioning, not a chase.
                Approach(new Vector2(NPC.Center.X, player.Center.Y + BandOffset()), 0.35f, 9f);
                return;
            }

            if (Sub <= 0f)
            {
                StartDash(player);
                return;
            }

            RunDash();

            if (Timer >= Duration && Sub <= 0f)
                ChooseNextAttack();
        }

        /// <summary>
        /// Keeps the last DashLag player positions.
        ///
        /// Seeded on the first tick rather than left at Vector2.Zero — an unfilled ring
        /// would send the opening dash at the top-left corner of the world.
        /// </summary>
        private void RecordRecent(Player player)
        {
            if (!recentReady)
            {
                for (int i = 0; i < recent.Length; i++)
                    recent[i] = player.Center;

                recentReady = true;
            }

            recent[recentIndex] = player.Center;
            recentIndex = (recentIndex + 1) % recent.Length;
        }

        /// <summary>
        /// Where the player was DashLag ticks ago. The slot about to be overwritten is
        /// the oldest in the ring, so no arithmetic is needed to find it.
        /// </summary>
        private Vector2 DashTarget(Player player)
        {
            return recentReady ? recent[recentIndex] : player.Center;
        }

        /// <summary>
        /// Current height in the sweep band, relative to the player. Driven off the state
        /// timer rather than a per-dash roll, so it moves continuously and successive
        /// dashes land at different points on the same rise and fall.
        /// </summary>
        private float BandOffset()
        {
            float sweep = (MathF.Sin(Timer * DashSweepSpeed) + 1f) * 0.5f;
            return MathHelper.Lerp(DashAbove, DashBelow, sweep);
        }

        /// <summary>
        /// Records one afterimage. Newest at index 0, shifting the rest back — ten entries
        /// a tick is nothing, and it keeps the draw a straight walk from newest to oldest
        /// with no ring arithmetic.
        /// </summary>
        private void PushGhost()
        {
            for (int i = Math.Min(ghostCount, TrailLength - 1); i > 0; i--)
            {
                ghostPos[i] = ghostPos[i - 1];
                ghostAim[i] = ghostAim[i - 1];
                ghostFlip[i] = ghostFlip[i - 1];
            }

            ghostPos[0] = NPC.Center;
            ghostAim[0] = aim;
            ghostFlip[0] = NPC.direction == -1;

            if (ghostCount < TrailLength)
                ghostCount++;
        }

        private void StartDash(Player player)
        {
            float distance = Main.rand.NextFloat(DashMin, DashMax);

            // Horizontal aim keeps its lag, but the height comes from the band — so the
            // dash is aimed through the player's level rather than exactly at them.
            Vector2 target = DashTarget(player);
            target.Y = player.Center.Y + BandOffset();

            dashFrom = NPC.Center;
            dashTo = NPC.Center + (target - NPC.Center).SafeNormalize(Vector2.UnitX) * distance;

            // Each dash gets a fresh trail; carrying the last one over would draw ghosts
            // along a path the boss is no longer on.
            ghostCount = 0;

            Sub = 0.0001f;
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = 0.4f, Volume = 0.5f }, NPC.Center);
        }

        /// <summary>
        /// Moves along the dash with a sine speed profile — accelerating to the halfway
        /// point and decaying after it. A constant-speed dash reads as a slide; the
        /// build-and-fall is what makes it land like a lunge.
        /// </summary>
        private void RunDash()
        {
            NPC.damage = Difficulty(60, 90, 150, PhaseOneDamage * DashWeight);

            Vector2 path = dashTo - dashFrom;
            float total = path.Length();

            if (total < 1f)
            {
                EndDash();
                return;
            }

            float travelled = Vector2.Dot(NPC.Center - dashFrom, path / total);
            float progress = MathHelper.Clamp(travelled / total, 0f, 1f);

            if (progress >= 0.995f)
            {
                EndDash();
                return;
            }

            // Never lets the speed reach zero at the ends, or the dash stalls short of
            // its own target and the progress check never trips.
            float curve = 0.22f + 0.78f * MathF.Sin(progress * MathHelper.Pi);

            NPC.velocity = path / total * (26f * curve);
            Sub = progress + 0.0001f;

            PushGhost();

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(24f, 12f),
                    DustID.Torch, -NPC.velocity * 0.2f, 0, default, 1.4f);
                dust.noGravity = true;
            }
        }

        private void EndDash()
        {
            NPC.velocity *= 0.2f;

            // A dash that ended far from the player is re-run almost immediately, so the
            // boss cannot be kited into a slow chain of harmless lunges.
            bool far = Vector2.Distance(NPC.Center, Main.player[NPC.target].Center) > DashFar;

            // The straight runner is used by both attacks, so the gap has to follow
            // whichever one is driving it.
            Sub = -(far ? DashGapFar : Curving ? CurveGap : DashGap);
            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------- enrage

        /// <summary>
        /// Slots in beside the player and keeps pace, drifting a little so the hold does
        /// not look mechanical.
        /// </summary>
        private void RageChase(Player player)
        {
            float side = NPC.Center.X < player.Center.X ? -500f : 500f;

            Vector2 drift = new Vector2(
                MathF.Sin(Timer * 0.021f) * 60f,
                MathF.Sin(Timer * 0.034f) * 45f);

            Approach(player.Center + new Vector2(side, -300f) + drift, 0.85f, 26f);
        }

        private void DoRageAimed(Player player)
        {
            RageChase(player);

            if (Timer % 120f == 0f)
                FireAimed(player, 19f, ShotDamage(300f, 300f, 300f));

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        private void DoRageMinigun(Player player)
        {
            RageChase(player);

            // Was every 3 ticks — twenty rounds a second, which is not an attack so much
            // as a wall.
            if (Timer % 6f == 0f)
            {
                Vector2 shot = (player.Center - Muzzle()).SafeNormalize(Vector2.UnitX);
                float spread = MathHelper.ToRadians(Main.rand.NextFloat(-14f, 14f));

                Fire(ProjectileID.Bullet,
                    shot.RotatedBy(spread) * Main.rand.NextFloat(6f, 13f), ShotDamage(150f, 150f, 150f));
            }

            if (Timer >= Duration)
                ChooseNextAttack();
        }

        // ---------------------------------------------------------------- death

        /// <summary>
        /// Refuses the first death and hands the boss to the animation instead.
        ///
        /// The standard shape for this: pin the life at 1, turn off damage, and return
        /// false so vanilla does not remove the NPC. When the animation is finished it
        /// zeroes the life itself and calls checkDead again, and the flag below makes
        /// that second call the real one — which is the frame the loot drops on.
        /// </summary>
        public override bool CheckDead()
        {
            if (dying)
                return true;

            dying = true;

            NPC.life = 1;
            NPC.dontTakeDamage = true;
            NPC.active = true;

            SwitchTo(State.Dying, DeathLength);
            NPC.netUpdate = true;

            return false;
        }

        /// <summary>
        /// The death animation.
        ///
        /// Nothing is timed until it has stopped moving. A boss that is still coasting
        /// when the camera arrives reads as the game hanging, so the deceleration runs
        /// first, off the clock, and the clock is reset the moment it comes to rest.
        /// </summary>
        private void DoDeath(Player player)
        {
            if (!deathStopped)
            {
                NPC.velocity *= 0.8f;

                // The camera holds wherever it was through the slowdown, so the pan out
                // starts from a shot that is already settled rather than snapping back to
                // the player for a frame first.
                DeathCamera(player, 0f);

                if (NPC.velocity.Length() >= 0.01f)
                    return;

                NPC.velocity = Vector2.Zero;
                deathStopped = true;

                // AI() steps the timer immediately after this returns, so -1 puts the
                // first frame of the animation proper at exactly zero.
                Timer = -1f;
                NPC.netUpdate = true;
                return;
            }

            NPC.velocity = Vector2.Zero;

            DeathCamera(player, Timer < DeathPan ? Ease(Timer / DeathPan) : 1f);

            // Every wound stays open. Once a spot starts bleeding it bleeds until the
            // end, and once it sparks it vents until the end too — so the last few
            // seconds are four wounds going at once rather than four separate events
            // that each finish before the next begins.
            for (int i = 0; i < bleedCount; i++)
                BloodSpray(NPC.Center + wounds[i]);

            for (int i = 0; i < ventCount; i++)
                TinySmoke(NPC.Center + wounds[i]);

            float at = Timer - DeathPan;

            if (at >= 0f && at < DeathCascadeLength)
                DeathCascade(at);

            if (Timer >= DeathLength)
                Detonate();
        }

        /// <summary>
        /// Out to the boss and held there.
        ///
        /// The blend only eases when the shot was on the player to begin with. From phase
        /// 3 the camera is already held, so the blend stays pinned at one and the FOCUS
        /// moves instead — dropping the blend would hand the shot back to the player for
        /// the length of the pan, which is a cut, not a move.
        /// </summary>
        private void DeathCamera(Player player, float t)
        {
            if (Main.dedServ)
                return;

            if (tier >= 3)
            {
                Vector2 hold = Vector2.Lerp(player.Center, NPC.Center, Phase3CameraBias);
                PisPlayer.BlendCamera(Vector2.Lerp(hold, NPC.Center, t), 1f);
                return;
            }

            PisPlayer.BlendCamera(NPC.Center, t);
        }

        /// <summary>
        /// Walks the cascade off a single cursor rather than a table of absolute times, so
        /// the two arrays above are the only place the timing lives.
        /// </summary>
        private void DeathCascade(float at)
        {
            float cursor = 0f;

            for (int i = 0; i < Wounds; i++)
            {
                if (at < cursor + DeathBleed[i])
                {
                    // The spot is picked when the bleeding starts, and the spark later
                    // opens that same spot — so the blood is the thing that tells you
                    // where the next failure is going to be. The spray itself is driven
                    // by the loop in DoDeath, because it has to outlive this leg.
                    if (at == cursor)
                        OpenWound(i);

                    return;
                }

                cursor += DeathBleed[i];

                if (at < cursor + DeathBurst[i])
                {
                    if (at == cursor)
                        BreakWound(i);

                    return;
                }

                cursor += DeathBurst[i];
            }
        }

        /// <summary>
        /// Picks where the next failure is going to be, and starts it bleeding. From here
        /// the spot sprays for the rest of the animation.
        /// </summary>
        private void OpenWound(int index)
        {
            float half = NPC.height * WoundBox * 0.5f;

            wounds[index] = new Vector2(
                Main.rand.NextFloat(-half, half),
                Main.rand.NextFloat(-half, half));

            bleedCount = index + 1;

            NPC.netUpdate = true;
        }

        /// <summary>
        /// A steady spray out of a wound. Aimed away from the centre so it arcs off the
        /// hull rather than fountaining straight up out of the middle of the sprite.
        /// </summary>
        private void BloodSpray(Vector2 spot)
        {
            if (Main.dedServ)
                return;

            // Thinned to roughly one droplet a tick per wound. Four wounds run at once by
            // the end, and at the old rate that was several hundred live particles.
            if (!Main.rand.NextBool(2))
                return;

            Vector2 outward = (spot - NPC.Center).SafeNormalize(-Vector2.UnitY);

            BloodDroplet.SpawnBurst(spot + Main.rand.NextVector2Circular(3f, 3f),
                outward.RotatedByRandom(0.35f), 2, 26f);
        }

        /// <summary>
        /// The spark that opens a wound for good. From here it vents for the rest of the
        /// animation, which is what <see cref="DoDeath"/>'s loop is doing.
        /// </summary>
        private void BreakWound(int index)
        {
            ventCount = index + 1;

            if (Main.dedServ)
                return;

            Vector2 spot = NPC.Center + wounds[index];

            for (int i = 0; i < 3; i++)
            {
                Lightning.Spawn(spot, spot + Main.rand.NextVector2Circular(16f, 13f),
                    SparkColor, width: 9f, lifetime: 14, forks: 0);
            }

            for (int i = 0; i < 7; i++)
            {
                Dust dust = Dust.NewDustPerfect(spot, DustID.Torch,
                    Main.rand.NextVector2Circular(2f, 2f), 0, default, 1f);
                dust.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.55f, Pitch = 0.25f }, spot);
            Shake(ShakeSpark, ShakeSparkTime);
        }

        /// <summary>A thin, slow wisp. Small on purpose — several of these run at once.</summary>
        private static void TinySmoke(Vector2 spot)
        {
            if (Main.dedServ || !Main.rand.NextBool(3))
                return;

            PADParticleHandler.SpawnParticle(new MuzzleSmoke(
                spot + Main.rand.NextVector2Circular(3f, 3f),
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.1f, -0.5f)),
                Main.rand.Next(26, 44),
                Main.rand.NextFloat(0.16f, 0.3f)));
        }

        /// <summary>
        /// The end of it. Blows up, hands the camera its way home, and then gives the kill
        /// back to vanilla — which is what actually drops the loot.
        /// </summary>
        private void Detonate()
        {
            // Recorded before the NPC goes, because the outro pan starts from here and
            // there will be nothing left to ask by the time it runs.
            deathAt = NPC.Center;

            if (!Main.dedServ)
            {
                for (int i = 0; i < 70; i++)
                {
                    Vector2 from = deathAt + Main.rand.NextVector2Circular(NPC.width * 0.5f, NPC.height * 0.5f);

                    PADParticleHandler.SpawnParticle(new MuzzleSmoke(from,
                        Main.rand.NextVector2Circular(3.5f, 3.5f) + new Vector2(0f, -0.6f),
                        Main.rand.Next(40, 90),
                        Main.rand.NextFloat(0.25f, 0.5f)));
                }

                // A full circle rather than a cone — this one is not coming out of a
                // wound, it is the hull letting go all at once.
                for (int i = 0; i < 12; i++)
                {
                    BloodDroplet.SpawnBurst(deathAt,
                        (MathHelper.TwoPi * i / 12f).ToRotationVector2(), 8, 15f);
                }

                for (int i = 0; i < 26; i++)
                {
                    Dust dust = Dust.NewDustPerfect(deathAt, DustID.Torch,
                        Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.6f);
                    dust.noGravity = true;
                }

                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.95f, Pitch = -0.4f }, deathAt);
                Shake(ShakeBlast, ShakeBlastTime);

                // Handed over now, while there is still something to hand it from.
                PisPlayer.PanHome(deathAt, DeathOutro);
            }

            NPC.life = 0;
            NPC.HitEffect(0, 10.0);
            NPC.checkDead();
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Steers toward a point instead of snapping to it. Higher acceleration makes the
        /// hold tighter; the cap keeps it from overshooting on long corrections.
        /// </summary>
        private void Approach(Vector2 destination, float acceleration, float maxSpeed)
        {
            Vector2 toward = destination - NPC.Center;

            if (toward.LengthSquared() < 4f)
            {
                NPC.velocity *= 0.9f;
                return;
            }

            NPC.velocity += toward.SafeNormalize(Vector2.Zero) * acceleration;

            if (NPC.velocity.Length() > maxSpeed)
                NPC.velocity = NPC.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
             
            NPC.velocity *= 0.985f;
        }
         
        /// <summary>
        /// Screen shake, routed through the bump rather than through AddScreenShake.
        ///
        /// AddScreenShake overwrites outright, so a quiet event landing a few ticks after
        /// a loud one replaces it — which is exactly the case here, where sparks, bolts
        /// and the blast all overlap by design. The bump only ever raises.
        /// </summary>
        private static void Shake(float power, int duration)
        {
            if (Main.dedServ)
                return;

            Main.LocalPlayer.PIS().BumpScreenShake(power, duration);
        }

        private Vector2 Muzzle()
        {
            return NPC.Center + aim.ToRotationVector2() * (NPC.width * 0.45f);
        }

        /// <summary>Places a shot straight at the player.</summary>
        private void FireAimed(Player player, float speed, int damage, float spreadDegrees = 0f)
        {
            Vector2 muzzle = Muzzle();
            Vector2 shot = (player.Center - muzzle).SafeNormalize(Vector2.UnitX) * speed;

            if (spreadDegrees > 0f)
            {
                shot = shot.RotatedBy(MathHelper.ToRadians(
                    Main.rand.NextFloat(-spreadDegrees, spreadDegrees)));
            }

            SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.7f }, NPC.Center);
            Fire(ProjectileID.BulletHighVelocity, shot, damage);
        } 
        private void Fire(int type, Vector2 velocity, int damage)
        {
            // Projectile spawning is server-authoritative; a client doing it too would
            // double every shot in multiplayer.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), Muzzle(), velocity,
                type, damage, 0f, Main.myPlayer);

            if (index < 0 || index >= Main.maxProjectiles)
                return;

            Projectile shot = Main.projectile[index];
            shot.hostile = true;
            shot.friendly = false;

            // The flag flip happens after the spawn packet, so clients need telling again
            // or they keep drawing a friendly bullet that hurts nobody.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncProjectile, number: index);
        } 
        private void FaceAndTilt(Player player)
        {
            // While a sight is up the nose follows the sight, not the player — that is
            // what makes the lock legible: the boss visibly stops tracking you.
            Vector2 focus = Scoping ? scopeTarget : player.Center;

            // Mid-dash it commits to where it is going; otherwise it tracks its focus.
            Vector2 forward = Dashing && NPC.velocity.LengthSquared() > 1f
                ? NPC.velocity
                : focus - NPC.Center;

            if (forward.LengthSquared() < 0.01f)
                return;

            int facing = forward.X >= 0f ? 1 : -1;
            bool turned = facing != NPC.direction;

            NPC.direction = facing;
            NPC.spriteDirection = facing;

            float wanted = forward.ToRotation();

            // Easing through a turn would swing the nose the long way round, pointing it
            // backwards on the way past, so a change of facing snaps.
            aim = turned ? wanted : aim.AngleLerp(wanted, 0.2f);

            // Kept in step for anything vanilla that reads it, but nothing here draws
            // from it.
            NPC.rotation = aim;
        }
         
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = NPC.frame.Size() * 0.5f;
            Vector2 position = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

            bool mirrored = NPC.direction == -1;

            SpriteEffects effects = mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rotation = mirrored ? aim + MathHelper.Pi : aim;

            // Afterimages behind anything moving fast enough to smear — the dashes and
            // the phase 3 orbit. Drawn before the boss so the solid sprite sits on top of
            // its own trail.
            if (Dashing)
                DrawTrail(spriteBatch, texture, screenPos, origin);

            // Under the sprite, so the boss is never hidden behind its own sight.
            DrawScope(spriteBatch, screenPos);

            spriteBatch.Draw(texture, position, NPC.frame, NPC.GetAlpha(drawColor), rotation,
                origin, NPC.scale, effects, 0f);

            float wash = Math.Max(rageGlow, heatGlow);

            if (wash > 0.01f)
            { 
                Color tint = rageGlow > heatGlow
                    ? new Color(255, 40, 40)
                    : new Color(255, 150, 40);

                float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);

                spriteBatch.Draw(texture, position, NPC.frame, tint * (wash * pulse), rotation,
                    origin, NPC.scale, effects, 0f);
            }

            return false;
        }

        /// <summary>
        /// The phase 3 sight: a thin line from the muzzle out to wherever it is pointing,
        /// and a diamond sitting on the point itself.
        ///
        /// Both are the same white pixel stretched and rotated, which is how vanilla draws
        /// its own beams — no texture to ship and no shader to set up. The line carries
        /// the direction and the diamond carries the timing: it swells as the lock
        /// tightens, so the player reads WHERE off the line and WHEN off the dot.
        /// </summary>
        private void DrawScope(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (scopeAlpha <= 0.01f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle source = new Rectangle(0, 0, 1, 1);

            Vector2 from = Muzzle();
            Vector2 line = scopeTarget - from;

            float length = line.Length();

            if (length < 1f)
                return;

            float thickness = MathHelper.Lerp(1.5f, 3.5f, scopeAlpha);

            // Origin on the left edge, centred vertically: the stretch runs from the
            // muzzle outward and the thickness grows evenly either side of the line.
            spriteBatch.Draw(pixel, from - screenPos, source,
                ScopeColor * (scopeAlpha * 0.8f), line.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);

            float dot = MathHelper.Lerp(5f, 16f, scopeAlpha);

            spriteBatch.Draw(pixel, scopeTarget - screenPos, source,
                ScopeColor * scopeAlpha, MathHelper.PiOver4,
                new Vector2(0.5f, 0.5f), new Vector2(dot, dot), SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The dash trail.
        ///
        /// Drawn oldest first so newer ghosts sit on top, and skipping index 0 because
        /// that sample is this frame — drawing it would just double the solid sprite.
        ///
        /// Tinted to the dash's own heat rather than left white. A plain alpha copy of
        /// the sprite reads as a stack of pale boxes; colouring it ties the trail to the
        /// attack it belongs to.
        /// </summary>
        private void DrawTrail(SpriteBatch spriteBatch, Texture2D texture, Vector2 screenPos, Vector2 origin)
        {
            for (int i = ghostCount - 1; i >= 1; i--)
            {
                float fade = 1f - i / (float)ghostCount;

                Vector2 at = ghostPos[i] - screenPos + new Vector2(0f, NPC.gfxOffY);

                SpriteEffects ghostEffects = ghostFlip[i]
                    ? SpriteEffects.FlipHorizontally
                    : SpriteEffects.None;

                float ghostRotation = ghostFlip[i] ? ghostAim[i] + MathHelper.Pi : ghostAim[i];

                // Squared, so the tail drops away quickly instead of leaving a long even
                // smear of equally solid copies.
                Color tint = new Color(255, 140, 50) * (fade * fade * 0.55f);

                spriteBatch.Draw(texture, at, NPC.frame, tint, ghostRotation, origin,
                    NPC.scale * (0.82f + 0.18f * fade), ghostEffects, 0f);
            }
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frame.Y = 0;
        }

        // ---------------------------------------------------------------- net

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(enraged);
            writer.Write(tier);
            writer.Write(riseFrom);
            writer.WriteVector2(dashFrom);
            writer.WriteVector2(dashTo);
            writer.WriteVector2(curveCenter);
            writer.Write(curveRadius);
            writer.Write(curveStart);
            writer.Write(curveTurn);
            writer.WriteVector2(scopeTarget);
            writer.Write(scopeAlpha);
            writer.Write(orbitAngle);
            writer.Write(dying);
            writer.Write(deathStopped);
            writer.Write(bleedCount);
            writer.Write(ventCount);

            for (int i = 0; i < Wounds; i++)
                writer.WriteVector2(wounds[i]);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            enraged = reader.ReadBoolean();
            tier = reader.ReadInt32();
            riseFrom = reader.ReadSingle();
            dashFrom = reader.ReadVector2();
            dashTo = reader.ReadVector2();
            curveCenter = reader.ReadVector2();
            curveRadius = reader.ReadSingle();
            curveStart = reader.ReadSingle();
            curveTurn = reader.ReadSingle();
            scopeTarget = reader.ReadVector2();
            scopeAlpha = reader.ReadSingle();
            orbitAngle = reader.ReadSingle();
            dying = reader.ReadBoolean();
            deathStopped = reader.ReadBoolean();
            bleedCount = reader.ReadInt32();
            ventCount = reader.ReadInt32();

            for (int i = 0; i < Wounds; i++)
                wounds[i] = reader.ReadVector2();
        }
    }
}
