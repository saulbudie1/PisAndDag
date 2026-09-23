using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>Where the Eye is in its last night. None is an ordinary fight.</summary>
    public enum EyeStage : byte
    {
        None,

        /// <summary>At the hold, untouchable, dashing at one player until they are at 1.</summary>
        Frenzy,

        /// <summary>The scene has it. EyeFinale moves it; it does nothing of its own.</summary>
        Held,

        /// <summary>Shot. The one stage it is allowed to die in.</summary>
        Dead,
    }

    /// <summary>
    /// The Eye of Cthulhu, the first time it is beaten in a world (Lore Planning §3.1,
    /// as the author has since drawn it).
    ///
    /// It will not go below Hold. The hit that would take it there stops at it, and from
    /// then on nothing lands on it at all. It stops being a fight: every piece of its own
    /// AI is set aside and it throws itself at one player, dash after dash, faster each
    /// time, each one taking a fixed share of their life, until they are standing on one
    /// point of it. Nobody dies in this. Then EyeFinale takes it and the camera goes to it.
    ///
    /// Only while NPC.downedBoss1 is false. Once the world has seen this it is an
    /// ordinary Eye again, forever.
    ///
    /// The state is the server's (or singleplayer's) and is sent with the Eye. The dashes
    /// run on every machine from that state, so a client between packets carries on the
    /// same motion; the one roll in them, the jitter on each dash, is the server's alone
    /// and arrives with the sync that follows it.
    /// </summary>
    public class EyeFinaleEye : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.EyeofCthulhu;

        // ============================================================
        // THE HOLD
        // ============================================================

        /// <summary>The life it stops at. Not 1: it is not nearly dead, it is done being hurt.</summary>
        public const int Hold = 300;

        /// <summary>Each dash that lands takes this share of the victim's whole life. Three and they are on one.</summary>
        private const float Chunk = 0.38f;

        // ============================================================
        // THE FRENZY
        // ============================================================

        /// <summary>
        /// Ticks it gathers itself before each dash: the first is the longest, each one
        /// after is a tick shorter, down to the last. By the tenth it barely stops.
        /// </summary>
        private const int WindupFirst = 14;
        private const int WindupLast = 5;

        /// <summary>Dash speed, pixels a tick: from here, up by the gain with every dash, to the cap.</summary>
        private const float DashSpeed = 15f;
        private const float DashSpeedGain = 0.9f;
        private const float DashSpeedMax = 23f;

        /// <summary>How far ahead of a running player it aims, as a share of where they would be on arrival.</summary>
        private const float Lead = 0.6f;

        /// <summary>The server's roll on each dash's heading, radians either way. Frantic, not precise.</summary>
        private const float Jitter = 0.08f;

        /// <summary>
        /// How hard a dash bends toward the victim mid-flight, radians a tick. Barely at
        /// first, so a dash can be dodged; hard once this has gone on long enough that
        /// dodging has become the whole game.
        /// </summary>
        private const float Homing = 0.012f;
        private const float HomingLate = 0.07f;
        private const int HomingLateAfter = 600;

        /// <summary>A dash runs to the victim and this many ticks past them, within these bounds.</summary>
        private const int DashOvershoot = 14;
        private const int DashShortest = 18;
        private const int DashLongest = 40;

        /// <summary>Ticks it spends pulling up after a dash, and how much speed each of them keeps.</summary>
        private const int Recover = 6;
        private const float RecoverDrag = 0.86f;

        /// <summary>Speed kept each tick while it winds up.</summary>
        private const float WindupDrag = 0.8f;

        /// <summary>
        /// Twenty seconds of a player dodging everything and the victim is put on one
        /// anyway (EyeFinaleGuard). The scene must happen; it must not wait forever.
        /// </summary>
        public const int FailsafeTicks = 1200;

        /// <summary>Positions kept for the afterimages, one a tick; every other one is drawn.</summary>
        private const int Ghosts = 9;

        private enum Phase : byte
        {
            Windup,
            Dash,
            Recover,
        }

        // ---- sent -------------------------------------------------------------------

        public EyeStage Stage;

        /// <summary>The player it is after. -1 before the frenzy.</summary>
        public int Victim = -1;

        /// <summary>Ticks since the frenzy began.</summary>
        public int FrenzyTicks;

        private Phase phase;
        private int timer;
        private int dashes;
        private int length;

        // ---- local ------------------------------------------------------------------

        /// <summary>The stage this machine last reacted to. What makes a stage change something that happens once.</summary>
        private EyeStage seen;

        /// <summary>The dashes this machine has already heard. The roar goes once per dash, whoever started it.</summary>
        private int heard;

        /// <summary>Ticks the Eye has been held with nothing holding it.</summary>
        private int orphaned;

        private readonly Vector2[] ghostAt = new Vector2[Ghosts];
        private readonly float[] ghostTurn = new float[Ghosts];
        private int ghostCount;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        // ============================================================
        // AI
        // ============================================================

        public override bool PreAI(NPC npc)
        {
            if (Stage == EyeStage.None && Authority && !NPC.downedBoss1 && npc.life <= Hold && !EyeFinale.Running)
                BeginFrenzy(npc);

            if (Stage != seen)
            {
                Entered(npc, Stage);
                seen = Stage;
            }

            switch (Stage)
            {
                case EyeStage.Frenzy:
                    Frenzy(npc);
                    Present(npc);
                    return false;

                case EyeStage.Held:
                case EyeStage.Dead:
                    // EyeFinale puts it where it goes. It keeps nothing of its own.
                    npc.velocity = Vector2.Zero;
                    npc.timeLeft = NPC.activeTime;
                    ghostCount = 0;
                    Orphan(npc);
                    return false;
            }

            return true;
        }

        private void BeginFrenzy(NPC npc)
        {
            int who = PickVictim(npc);

            if (who < 0)
                return;

            Stage = EyeStage.Frenzy;
            Victim = who;
            FrenzyTicks = 0;
            phase = Phase.Windup;
            timer = 0;
            dashes = 0;
            length = 0;

            npc.life = Hold;
            npc.target = who;

            // Its second face, the one with the teeth, whatever it was showing.
            npc.ai[0] = 3f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;

            npc.netUpdate = true;
        }

        /// <summary>Whoever it was already after, if they are still there; otherwise the nearest.</summary>
        private static int PickVictim(NPC npc)
        {
            if (npc.target >= 0 && npc.target < Main.maxPlayers)
            {
                Player current = Main.player[npc.target];

                if (current.active && !current.dead)
                    return npc.target;
            }

            int best = -1;
            float bestDistance = float.MaxValue;

            foreach (Player player in Main.ActivePlayers)
            {
                if (player.dead)
                    continue;

                float distance = Vector2.DistanceSquared(player.Center, npc.Center);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = player.whoAmI;
                }
            }

            return best;
        }

        /// <summary>Once per stage change, on every machine.</summary>
        private void Entered(NPC npc, EyeStage stage)
        {
            // Untouchable while it is at the hold and while the scene has it. Dead has
            // to be killable, and None is an ordinary Eye again.
            npc.dontTakeDamage = stage == EyeStage.Frenzy || stage == EyeStage.Held;

            if (stage != EyeStage.Frenzy)
                return;

            // Nothing that was on it keeps working on it, and nothing new takes.
            for (int i = 0; i < npc.buffType.Length; i++)
            {
                npc.buffType[i] = 0;
                npc.buffTime[i] = 0;
            }

            for (int b = 0; b < npc.buffImmune.Length; b++)
                npc.buffImmune[b] = true;

            npc.ai[0] = 3f;

            if (!Main.dedServ)
                Snap(npc);
        }

        /// <summary>The moment it stops taking hits: a scream, a burst of it, and the ground shakes.</summary>
        private static void Snap(NPC npc)
        {
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.35f, Volume = 1.1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f }, npc.Center);

            for (int i = 0; i < 3; i++)
                BloodDroplet.SpawnBurst(npc.Center, Main.rand.NextVector2CircularEdge(1f, 1f), 10, 40f);

            for (int i = 0; i < 30; i++)
            {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Blood,
                    Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 0, default, Main.rand.NextFloat(1.1f, 1.8f));
                dust.noGravity = Main.rand.NextBool(3);
            }

            if (Vector2.Distance(Main.LocalPlayer.Center, npc.Center) < 1600f)
                Main.LocalPlayer.PIS().BumpScreenShake(9f, 20);
        }

        private void Frenzy(NPC npc)
        {
            FrenzyTicks = Math.Min(FrenzyTicks + 1, short.MaxValue);
            npc.timeLeft = NPC.activeTime;

            Player victim = Alive(Victim) ? Main.player[Victim] : null;

            if (victim == null)
            {
                // The server says who is next. A client waits to be told.
                if (!Authority)
                    return;

                int who = PickVictim(npc);

                if (who < 0)
                {
                    Release(npc);
                    return;
                }

                Victim = who;
                npc.netUpdate = true;
                victim = Main.player[who];
            }

            npc.target = Victim;

            Vector2 to = victim.Center - npc.Center;
            float face = to.ToRotation() - MathHelper.PiOver2;

            switch (phase)
            {
                case Phase.Windup:
                    npc.velocity *= WindupDrag;
                    npc.rotation = Utils.AngleLerp(npc.rotation, face, 0.35f);

                    if (++timer >= Math.Max(WindupLast, WindupFirst - dashes))
                        Dash(npc, victim);

                    break;

                case Phase.Dash:
                    float homing = FrenzyTicks > HomingLateAfter ? HomingLate : Homing;
                    float speed = npc.velocity.Length();
                    float heading = Utils.AngleTowards(npc.velocity.ToRotation(), to.ToRotation(), homing);

                    npc.velocity = heading.ToRotationVector2() * speed;
                    npc.rotation = heading - MathHelper.PiOver2;

                    if (++timer >= length)
                    {
                        phase = Phase.Recover;
                        timer = 0;
                    }

                    break;

                case Phase.Recover:
                    npc.velocity *= RecoverDrag;
                    npc.rotation = Utils.AngleLerp(npc.rotation, face, 0.2f);

                    if (++timer >= Recover)
                    {
                        phase = Phase.Windup;
                        timer = 0;
                    }

                    break;
            }

            if (Authority && victim.statLife <= 1)
                EyeFinale.Begin(npc, this, victim);
        }

        private void Dash(NPC npc, Player victim)
        {
            dashes = Math.Min(dashes + 1, byte.MaxValue);

            float speed = Math.Min(DashSpeed + DashSpeedGain * dashes, DashSpeedMax);
            float distance = Vector2.Distance(victim.Center, npc.Center);

            Vector2 aim = victim.Center + victim.velocity * (distance / speed) * Lead;
            Vector2 way = (aim - npc.Center).SafeNormalize(Vector2.UnitY);

            if (Authority)
                way = way.RotatedBy(Main.rand.NextFloat(-Jitter, Jitter));

            npc.velocity = way * speed;
            npc.rotation = way.ToRotation() - MathHelper.PiOver2;

            length = (int)MathHelper.Clamp(distance / speed + DashOvershoot, DashShortest, DashLongest);
            phase = Phase.Dash;
            timer = 0;

            if (Authority)
                npc.netUpdate = true;
        }

        /// <summary>Nobody left to go after: an ordinary Eye again, at whatever life it has.</summary>
        private void Release(NPC npc)
        {
            Stage = EyeStage.None;
            Victim = -1;
            npc.netUpdate = true;
        }

        /// <summary>
        /// Held, but nothing is running the scene it is held for: the actor never
        /// arrived, or went. Half a second of that and it is back in the frenzy, which
        /// finds its player on one and begins the scene again.
        /// </summary>
        private void Orphan(NPC npc)
        {
            if (Stage != EyeStage.Held || !Authority)
                return;

            if (EyeFinale.HeldBy(npc.whoAmI))
            {
                orphaned = 0;
                return;
            }

            if (++orphaned < 30)
                return;

            orphaned = 0;
            Stage = EyeStage.Frenzy;
            phase = Phase.Recover;
            timer = 0;
            npc.netUpdate = true;
        }

        private static bool Alive(int who)
        {
            return who >= 0 && who < Main.maxPlayers && Main.player[who].active && !Main.player[who].dead;
        }

        // ============================================================
        // WHAT IT LOOKS AND SOUNDS LIKE
        // ============================================================

        /// <summary>Client-side, once a tick in the frenzy.</summary>
        private void Present(NPC npc)
        {
            if (Main.dedServ)
                return;

            for (int i = Ghosts - 1; i > 0; i--)
            {
                ghostAt[i] = ghostAt[i - 1];
                ghostTurn[i] = ghostTurn[i - 1];
            }

            ghostAt[0] = npc.Center;
            ghostTurn[0] = npc.rotation;
            ghostCount = Math.Min(ghostCount + 1, Ghosts);

            if (dashes > heard)
            {
                heard = dashes;

                SoundEngine.PlaySound(SoundID.ForceRoar with { Pitch = Main.rand.NextFloat(0.15f, 0.45f), Volume = 0.85f }, npc.Center);
                BloodDroplet.SpawnBurst(npc.Center, -npc.velocity, 6, 30f);
            }

            if (phase == Phase.Dash)
            {
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Blood,
                        -npc.velocity.X * 0.2f, -npc.velocity.Y * 0.2f, 0, default, Main.rand.NextFloat(1f, 1.5f));
                    dust.noGravity = true;
                }
            }
            else if (phase == Phase.Windup)
            {
                // Shaking with it.
                npc.rotation += Main.rand.NextFloat(-0.09f, 0.09f);
            }
        }

        /// <summary>Flushed while it is at the hold: the same eye, redder.</summary>
        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Stage == EyeStage.Frenzy)
                drawColor = drawColor.MultiplyRGB(new Color(255, 185, 185));
        }

        /// <summary>
        /// The afterimages, under the Eye itself, where vanilla draws it: centred sideways,
        /// sat on the bottom of the hitbox plus four, one frame of the sheet.
        /// </summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (Stage != EyeStage.Frenzy || ghostCount < 3)
                return true;

            Main.instance.LoadNPC(npc.type);

            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() * 0.5f;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            float drop = npc.height * 0.5f + 4f - frame.Height * npc.scale * 0.5f + npc.gfxOffY;
            Color tint = npc.GetAlpha(drawColor).MultiplyRGBA(new Color(255, 110, 110, 255));

            for (int i = Math.Min(ghostCount, Ghosts) - 1; i >= 2; i -= 2)
            {
                // Only when it is actually going somewhere; a stack of them in place is a smear.
                if (Vector2.Distance(ghostAt[i], npc.Center) < 14f)
                    continue;

                float fade = 1f - i / (float)Ghosts;
                Vector2 at = ghostAt[i] + new Vector2(0f, drop) - screenPos;

                spriteBatch.Draw(texture, at, frame, tint * (0.5f * fade), ghostTurn[i], origin, npc.scale, flip, 0f);
            }

            return true;
        }

        // ============================================================
        // DAMAGE, BOTH WAYS
        // ============================================================

        /// <summary>Held and dead, it touches no one. In the frenzy, what touches the victim takes a fixed share.</summary>
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            return Stage != EyeStage.Held && Stage != EyeStage.Dead;
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (Stage != EyeStage.Frenzy || target.whoAmI != Victim)
                return;

            int chunk = (int)Math.Ceiling(target.statLifeMax2 * Chunk);

            // After defence and everything else. EyeFinaleGuard's cap is added later, so
            // it runs later, so the last hit stops at one.
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => info.Damage = chunk;
        }

        /// <summary>No hit takes it past the hold. Several in one tick can shave a little; the frenzy puts it back.</summary>
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (Stage != EyeStage.None || NPC.downedBoss1 || EyeFinale.Running)
                return;

            modifiers.SetMaxDamage(Math.Max(1, npc.life - Hold));
        }

        /// <summary>
        /// The backstop: whatever got it to nothing (a burn, a debuff, a hit the client
        /// had not capped), it is at the hold instead. Only the Dead stage dies, and an
        /// Eye in a world that has already seen all this dies like any other.
        ///
        /// A client never overrules the server's decision: the death it is told about is
        /// real, and one it made up itself is corrected by the next sync.
        /// </summary>
        public override bool CheckDead(NPC npc)
        {
            if (!Authority)
                return true;

            if (Stage == EyeStage.Dead || NPC.downedBoss1)
                return true;

            // A second Eye while one is already going through this dies normally.
            if (Stage == EyeStage.None && EyeFinale.Running)
                return true;

            npc.life = Hold;

            if (Stage == EyeStage.None)
                BeginFrenzy(npc);

            return false;
        }

        // ============================================================
        // NETWORK
        // ============================================================

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)Stage);
            binaryWriter.Write((short)Victim);
            binaryWriter.Write((short)FrenzyTicks);
            binaryWriter.Write((byte)phase);
            binaryWriter.Write((byte)Math.Min(timer, byte.MaxValue));
            binaryWriter.Write((byte)dashes);
            binaryWriter.Write((byte)length);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            Stage = (EyeStage)binaryReader.ReadByte();
            Victim = binaryReader.ReadInt16();
            FrenzyTicks = binaryReader.ReadInt16();
            phase = (Phase)binaryReader.ReadByte();
            timer = binaryReader.ReadByte();
            dashes = binaryReader.ReadByte();
            length = binaryReader.ReadByte();
        }
    }

    /// <summary>Nothing new walks into the shot.</summary>
    public class EyeFinaleSpawns : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (!EyeFinale.SceneRunning)
                return;

            spawnRate = int.MaxValue;
            maxSpawns = 0;
        }
    }
}
