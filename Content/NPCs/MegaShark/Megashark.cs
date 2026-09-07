using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
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
    /// Only phase 1 and the enrage are implemented so far. Phases 2 and 3 hang off the
    /// same state machine — add their states to <see cref="State"/> and give
    /// <see cref="ChooseNextAttack"/> a branch on the life ratio.
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

            /// <summary>Enrage attack 1 — aimed shots while chasing.</summary>
            RageAimed,

            /// <summary>Enrage attack 2 — sustained minigun fire.</summary>
            RageMinigun
        }

        // ---------------------------------------------------------------- tuning

        /// <summary>Legends multiplies the health pool, nothing else.</summary>
        private const float LegendsHealth = 1.5f;

        /// <summary>Every phase-1 attack's damage is scaled by this.</summary>
        private const float PhaseOneDamage = 1.2f;

        /// <summary>
        /// How far the player can get before the fight turns into a hunt. Stated in
        /// blocks, so it is converted here rather than being a bare pixel count.
        /// </summary>
        private const float EnrageDistance = 750f * 16f;

        /// <summary>Ticks the boss spends rising before it can be hurt at all.</summary>
        private const int RiseInvulnerable = 90;

        /// <summary>Ticks the whole entrance lasts, damage ramping in over the tail of it.</summary>
        private const int RiseLength = 200;

        // Attack 1 — hover
        private const float HoverHeight = 300f;
        private const float HoverBobHeight = 20f;
        private const float HoverSweepWidth = 90f;

        /// <summary>
        /// Ticks the aimed shot lags behind the player, so it is fired at where they WERE
        /// rather than where they are. A high-velocity bullet aimed at your current
        /// position with no travel time is not a dodgeable attack — it is a hitscan.
        /// </summary>
        private const int AimLag = 30;

        /// <summary>Radians per tick. The vertical bob is the faster of the two.</summary>
        private const float HoverBobSpeed = 0.055f;
        private const float HoverSweepSpeed = 0.017f;
        private const int HoverShotInterval = 180;

        // Attack 2 — barrage
        private const int BarrageBrake = 60;
        private const int BarrageShake = 60;
        private const int BarrageInterval = 9;
        private const float BarrageSpread = 5f;

        // Attack 3 — dashes
        private const int HeatWindup = 12;

        /// <summary>How far one dash travels, in pixels.</summary>
        private const float DashMin = 600f;
        private const float DashMax = 800f;

        private const int DashGap = 300;
        private const int DashGapFar = 10;
        private const float DashFar = 900f;

        // ---------------------------------------------------------------- state

        private State Phase
        {
            get => (State)(int)NPC.ai[0];
            set => NPC.ai[0] = (int)value;
        }

        /// <summary>Ticks spent in the current state.</summary>
        private ref float Timer => ref NPC.ai[1];

        /// <summary>How long the current state should run before handing over.</summary>
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

        /// <summary>Ring buffer of recent player positions, for the lagged shot.</summary>
        private readonly Vector2[] trail = new Vector2[AimLag];
        private int trailIndex;
        private bool trailReady;

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

            NPC.lifeMax = 20000;
            NPC.defense = 20;
             
            NPC.damage = 0;

            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.npcSlots = 10f;

            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(0, 15, 0, 0);

            NPC.SpawnWithHigherTime(30);
        }

        /// <summary>
        /// Exact health per difficulty. Overridden rather than left to the automatic
        /// expert scaling, which would double whatever SetDefaults left behind and land
        /// nowhere near the intended numbers.
        /// </summary>
        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            int health = Main.masterMode ? 50000 : Main.expertMode ? 35000 : 20000;
            NPC.defense = Main.masterMode ? 60 : Main.expertMode ? 30 : 20;

            if (PADWorld.Legends)
                health = (int)(health * LegendsHealth);

            NPC.lifeMax = health;
            NPC.life = health;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Ocean
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
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
        /// Converts an intended damage number into the value to hand a hostile projectile.
        ///
        /// Terraria multiplies hostile projectile damage by the game mode on the way to
        /// the player — roughly double in Expert, triple in Master. Passing the intended
        /// figure straight through is what made these one-shot a 400 HP endgame build:
        /// the per-difficulty table was ALREADY raising the number, and the mode
        /// multiplier then stacked on top of it.
        /// </summary>
        private static int Hostile(int intended)
        {
            float mode = Main.masterMode ? 3f : Main.expertMode ? 2f : 1f;
            return Math.Max(1, (int)Math.Round(intended / mode));
        }

        /// <summary>
        /// The entrance is not a free window to burst the boss down, but it should not be
        /// a wall either: damage ramps from nothing to full across the back half of the
        /// rise instead of switching on in one frame.
        /// </summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (Phase != State.Rise)
                return;

            float ramp = Utils.GetLerpValue(RiseInvulnerable, RiseLength, Timer, true);
            modifiers.FinalDamage *= ramp;
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

            // Contact damage is opt-in per state; the dash turns it on.
            NPC.damage = 0;

            RecordTrail(player);
            CheckEnrage(player);
            FadeGlows();

            switch (Phase)
            {
                case State.Rise: DoRise(player); break;
                case State.Hover: DoHover(player); break;
                case State.Barrage: DoBarrage(player); break;
                case State.Heat: DoHeat(player); break;
                case State.RageAimed: DoRageAimed(player); break;
                case State.RageMinigun: DoRageMinigun(player); break;
            }

            FaceAndTilt(player);
            Timer++;
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

            bool hot = Phase == State.Heat;
            heatGlow = MathHelper.Clamp(heatGlow + (hot ? 0.04f : -0.06f), 0f, 1f);
        }

        private void SwitchTo(State next, float duration)
        {
            Phase = next;
            Timer = 0f;
            Duration = duration;
            Sub = 0f;
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

            switch (Phase)
            {
                case State.Hover:
                    SwitchTo(State.Barrage, Main.rand.Next(840, 1201));
                    break;

                case State.Barrage:
                    SwitchTo(State.Heat, Main.rand.Next(840, 961));
                    break;

                default:
                    SwitchTo(State.Hover, Main.rand.Next(600, 901));
                    break;
            }

            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------- entrance

        private void DoRise(Player player)
        {
            if (Timer == 0f)
            {
                // Drop below the spawn point so there is something to rise out of, and
                // pin the camera on the boss for the whole entrance.
                NPC.position.Y += 520f;
                NPC.dontTakeDamage = true;
                PisPlayer.FocusCamera(NPC.Center, RiseLength, 0.85f);
            }

            NPC.direction = player.Center.X > NPC.Center.X ? 1 : -1;

            // Eases out, so it breaches hard and settles rather than sliding up at a
            // constant rate.
            float progress = Timer / RiseLength;
            NPC.velocity = new Vector2(0f, -7f * (1f - progress) - 0.4f);

            if (Main.netMode != NetmodeID.Server)
                PisPlayer.FocusCamera(NPC.Center, 2, 0.85f);

            if (Timer >= RiseInvulnerable)
                NPC.dontTakeDamage = false;

            if (Timer >= RiseLength)
                SwitchTo(State.Hover, Main.rand.Next(600, 901));
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
                FireAimed(player, 14f, Hostile(Difficulty(60, 90, 150, PhaseOneDamage)));

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
                    shot.RotatedBy(spread) * Main.rand.NextFloat(7f, 11f),
                    Hostile(Difficulty(35, 60, 80, PhaseOneDamage)));
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

        private void StartDash(Player player)
        {
            float distance = Main.rand.NextFloat(DashMin, DashMax);

            dashFrom = NPC.Center;
            dashTo = NPC.Center + (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX) * distance;

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
            NPC.damage = Hostile(Difficulty(60, 90, 150, PhaseOneDamage));

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

            Sub = -(far ? DashGapFar : DashGap);
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
                FireAimed(player, 19f, Hostile(300));

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
                    shot.RotatedBy(spread) * Main.rand.NextFloat(6f, 13f), Hostile(150));
            }

            if (Timer >= Duration)
                ChooseNextAttack();
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

            // Bleeds off sideways drift so it settles rather than orbiting the target.
            NPC.velocity *= 0.985f;
        }

        /// <summary>
        /// The end of the barrel, so shots leave the gun and not the middle of the fish.
        ///
        /// Reads the true nose angle rather than NPC.rotation, so it stays correct
        /// whichever way the sprite happens to be mirrored.
        /// </summary>
        private Vector2 Muzzle()
        {
            return NPC.Center + aim.ToRotationVector2() * (NPC.width * 0.45f);
        }

        /// <summary>Remembers where the player has been, for the lagged shot.</summary>
        private void RecordTrail(Player player)
        {
            if (!trailReady)
            {
                for (int i = 0; i < trail.Length; i++)
                    trail[i] = player.Center;

                trailReady = true;
            }

            trail[trailIndex] = player.Center;
            trailIndex = (trailIndex + 1) % trail.Length;
        }

        /// <summary>
        /// The player's position AimLag ticks ago. The slot about to be overwritten is
        /// the oldest one in the ring, so it needs no arithmetic.
        /// </summary>
        private Vector2 LaggedTarget(Player player)
        {
            return trailReady ? trail[trailIndex] : player.Center;
        }

        /// <summary>
        /// Places the aimed shot half a second behind the player.
        ///
        /// Fired at their live position this is unavoidable — the bullet is fast enough
        /// that there is no travel time to react in. Aiming at where they were instead
        /// means standing still is what gets you hit, and moving is what saves you.
        /// </summary>
        private void FireAimed(Player player, float speed, int damage)
        {
            Vector2 muzzle = Muzzle();
            Vector2 shot = (LaggedTarget(player) - muzzle).SafeNormalize(Vector2.UnitX) * speed;

            SoundEngine.PlaySound(SoundID.Item36 with { Volume = 0.7f }, NPC.Center);
            Fire(ProjectileID.BulletHighVelocity, shot, damage);
        }

        /// <summary>
        /// Fires a vanilla round and flips it hostile.
        ///
        /// ProjectileID.Bullet and BulletHighVelocity are authored as player ammo, so
        /// straight out of NewProjectile they are friendly — they would pass through the
        /// player and hit NPCs instead. Swapping the two flags after the spawn is how an
        /// NPC borrows vanilla ammo, and it saves carrying near-identical copies of
        /// projectiles the game already ships.
        /// </summary>
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

        /// <summary>
        /// Works out where the nose should point. Drawing is handled in PreDraw, so this
        /// only has to produce a true world-space angle — no mirror compensation.
        /// </summary>
        private void FaceAndTilt(Player player)
        {
            bool dashing = Phase == State.Heat && Sub > 0f;

            // Mid-dash it commits to where it is going; otherwise it tracks the player.
            Vector2 forward = dashing && NPC.velocity.LengthSquared() > 1f
                ? NPC.velocity
                : player.Center - NPC.Center;

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

        // ---------------------------------------------------------------- drawing

        /// <summary>
        /// Draws the boss instead of letting vanilla do it.
        ///
        /// Vanilla decides the horizontal flip from spriteDirection, and which value means
        /// "flipped" depends on the convention the art was drawn to — that is what had the
        /// shark swimming backwards. Owning the draw removes the guesswork: the art faces
        /// RIGHT, so facing left is a mirror, and a mirrored sprite needs a half turn on
        /// its rotation to put the nose back on <see cref="aim"/>.
        /// </summary>
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Vector2 origin = NPC.frame.Size() * 0.5f;
            Vector2 position = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

            bool mirrored = NPC.direction == -1;

            SpriteEffects effects = mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float rotation = mirrored ? aim + MathHelper.Pi : aim;

            spriteBatch.Draw(texture, position, NPC.frame, NPC.GetAlpha(drawColor), rotation,
                origin, NPC.scale, effects, 0f);

            float wash = Math.Max(rageGlow, heatGlow);

            if (wash > 0.01f)
            {
                // Rage wins over heat: if it is hunting, that is the more important read.
                Color tint = rageGlow > heatGlow
                    ? new Color(255, 40, 40)
                    : new Color(255, 150, 40);

                float pulse = 0.55f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f);

                spriteBatch.Draw(texture, position, NPC.frame, tint * (wash * pulse), rotation,
                    origin, NPC.scale, effects, 0f);
            }

            return false;
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frame.Y = 0;
        }

        // ---------------------------------------------------------------- net

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(enraged);
            writer.WriteVector2(dashFrom);
            writer.WriteVector2(dashTo);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            enraged = reader.ReadBoolean();
            dashFrom = reader.ReadVector2();
            dashTo = reader.ReadVector2();
        }
    }
}
