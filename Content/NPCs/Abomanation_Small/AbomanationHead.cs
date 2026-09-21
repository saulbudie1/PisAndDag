using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Abomanation_Small
{
    /// <summary>
    /// The Abomanation. A worm that lives in the sand and comes up through it.
    ///
    /// Modelled on Calamity's Desert Scourge, but hand written rather than lifted: the
    /// Scourge's movement is one long chain of branches that reads the same variable under
    /// four names, and none of the six things asked for here are reachable from outside
    /// it. This is the same fight expressed as one target point and one steering rule, so
    /// the six knobs in <see cref="Abomanation"/> are the whole behaviour.
    ///
    /// Three parts:
    ///   the head steers and owns the health pool,
    ///   fifteen body segments follow it and carry the arms,
    ///   the tail closes the chain.
    ///
    /// The fight is a chase with one interruption. It goes for you, slightly off centre
    /// (<see cref="Abomanation.Stray"/>) so a straight line never quite works; every ten
    /// seconds spent under you or off to one side it dives, turns around a long way down,
    /// and comes straight back up through the ground you are standing on.
    ///
    /// No summon item and no loot table yet: /abomanation puts it in front of you.
    /// </summary>
    [AutoloadBossHead]
    public class AbomanationHead : ModNPC
    {
        public override string Texture => Abomanation.Folder + "Abomanation_Head";

        /// <summary>
        /// The marker on the map edge.
        ///
        /// Both halves are needed and they do different jobs. ModNPC.Register only calls
        /// AddBossHeadTexture when the class carries [AutoloadBossHead]; this says which
        /// file it loads. The default would be Texture + "_Head_Boss", which works out to
        /// Abomanation_Head_Head_Boss, so without this override the attribute asks for a
        /// file that is not there and the mod fails to load rather than quietly going
        /// without an icon.
        /// </summary>
        public override string BossHeadTexture => Abomanation.Folder + "Abomanation_Head_Boss";

        // ------------------------------------------------------------------ state -----
        //
        // Synced by hand in SendExtraAI. ai[0] carries the segment behind, which tML
        // syncs for free; everything here is the attack, and a client that guessed at it
        // would send the body somewhere the server never went.

        /// <summary>0 chasing, 1 diving, 2 rushing up, 3 falling back.</summary>
        private int phase;

        /// <summary>Ticks spent in a position worth punishing. Fires the dive at <see cref="Abomanation.RushGate"/>.</summary>
        private int chargeTimer;

        /// <summary>Ticks the current attack has taken. A watchdog, nothing else.</summary>
        private int attackTimer;

        /// <summary>World Y the rush climbs to, fixed at the moment it turns around.</summary>
        private float rushTop;

        private bool spawnedBody;

        // ---------------------------------------------------------------- defaults ----

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void Unload()
        {
            AbomanationArms.Unload();
        }

        public override void SetDefaults()
        {
            NPC.width = 56;
            NPC.height = 56;

            NPC.aiStyle = -1;
            AIType = -1;

            NPC.lifeMax = LifeForMode();
            NPC.damage = 40;
            NPC.defense = 4;

            NPC.npcSlots = 12f;
            NPC.boss = true;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;

            // Drawn behind tiles, because it spends most of the fight inside them. Without
            // this it swims over the top of the sand like a sticker on the screen.
            NPC.behindTiles = true;

            // Comes up out of nothing. The whole body fades in together, a segment at a
            // time, from AbomanationChain.Tether.
            NPC.alpha = 255;

            NPC.netAlways = true;
            NPC.value = Item.buyPrice(gold: 2);

            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;

            // The bar and the name. No OwnIntro: it has no scripted arrival of its own,
            // so it takes the universal one.
            NPC.PAD().BossName = "Abomanation";
            NPC.PAD().BossTitle = "It came up under the dunes.";
        }

        /// <summary>
        /// Health per difficulty, written rather than scaled, for the reason Megashark
        /// gives: the automatic expert scaling doubles whatever SetDefaults left behind,
        /// so a number picked in one place lands somewhere else in the other modes.
        /// </summary>
        private static int LifeForMode()
        {
            return Main.masterMode ? 12000 : Main.expertMode ? 9000 : 6000;
        }

        public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
        {
            NPC.lifeMax = LifeForMode();
            NPC.life = NPC.lifeMax;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
            {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Desert
            });
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(phase);
            writer.Write(chargeTimer);
            writer.Write(attackTimer);
            writer.Write(rushTop);
            writer.Write(spawnedBody);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            phase = reader.ReadInt32();
            chargeTimer = reader.ReadInt32();
            attackTimer = reader.ReadInt32();
            rushTop = reader.ReadSingle();
            spawnedBody = reader.ReadBoolean();
        }

        public override bool CheckActive() => false;

        // --------------------------------------------------------------------- AI -----

        public override void AI()
        {
            if (NPC.target < 0 || NPC.target == Main.maxPlayers
                || Main.player[NPC.target].dead || !Main.player[NPC.target].active)
            {
                NPC.TargetClosest(true);
            }

            Player player = Main.player[NPC.target];

            if (!spawnedBody && Main.netMode != NetmodeID.MultiplayerClient)
                SpawnBody();

            if (NPC.alpha > 0)
            {
                NPC.alpha -= 42;
                if (NPC.alpha < 0)
                    NPC.alpha = 0;
            }

            // Nobody left to chase. Dig, and take the whole body off the board once it is
            // deep enough that nothing is going to see it go.
            if (!player.active || player.dead)
            {
                Leave();
                return;
            }

            NPC.localAI[1]++;

            Advance(player);

            Vector2 aim = Aim(player);
            Steer(aim);

            if (NPC.velocity.LengthSquared() > 0.01f)
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

            // The one thing worth a packet on its own: a direction change the client's own
            // copy of this AI would not have predicted.
            if (!NPC.justHit
                && ((NPC.velocity.X > 0f) != (NPC.oldVelocity.X > 0f)
                    || (NPC.velocity.Y > 0f) != (NPC.oldVelocity.Y > 0f)))
            {
                NPC.netUpdate = true;
            }
        }

        /// <summary>
        /// The state machine. Four states, and every transition is a position it reached
        /// rather than a timer running out, so the attack cannot fire from the wrong place.
        /// The timer only decides when it is allowed to start.
        /// </summary>
        private void Advance(Player player)
        {
            switch (phase)
            {
                case 0:
                    // Only charge while it is under you or well off to one side. Charging
                    // it while it is already in your face would spend the dive on a
                    // position it is about to leave anyway.
                    if (NPC.Center.Y > player.Center.Y || Math.Abs(NPC.Center.X - player.Center.X) > 480f)
                        chargeTimer++;

                    if (chargeTimer >= Abomanation.RushGate)
                    {
                        phase = 1;
                        chargeTimer = 0;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1:
                    attackTimer++;

                    if (NPC.Center.Y >= DiveTarget(player) - 16f)
                    {
                        // Measured from where it actually turned around, not from the
                        // player, so Distance means what it says: this far up, from here.
                        rushTop = Math.Max(NPC.Center.Y - Abomanation.Distance * 16f, 320f);

                        // Straight up, at once. Letting the steering turn it around takes
                        // about a second at any sane TurnSpeed, and it sinks another two
                        // hundred pixels doing it, which eats the whole of Distance and
                        // leaves the rush surfacing level with your feet instead of over
                        // your head. The steering still runs from here; it only has to
                        // lean the climb toward you rather than reverse it.
                        NPC.velocity = new Vector2(0f, -Abomanation.RushSpeed);

                        phase = 2;
                        attackTimer = 0;
                        SoundEngine.PlaySound(SoundID.Roar, player.Center);
                        NPC.netUpdate = true;
                    }
                    break;

                case 2:
                    attackTimer++;
                    Breach();

                    if (NPC.Center.Y <= rushTop)
                    {
                        phase = 3;
                        attackTimer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                default:
                    attackTimer++;

                    // Spent once it is back under the ground, or once it is a long way
                    // below you. Waiting only on the depth would leave it falling for
                    // twenty seconds if you flew up while it came down.
                    bool back = attackTimer > 30 && Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

                    if (back || NPC.Center.Y >= player.Center.Y + Abomanation.DiveDepth * 0.5f)
                        Reset();
                    break;
            }

            // Anything that has taken this long has got itself stuck on geometry. Drop it
            // and go back to chasing rather than leaving the fight hanging on a corner.
            if (phase != 0 && attackTimer > Abomanation.RushTimeout)
                Reset();
        }

        private void Reset()
        {
            phase = 0;
            chargeTimer = 0;
            attackTimer = 0;
            rushTop = 0f;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// How deep the dive goes, kept inside the world.
        ///
        /// Unclamped, a fight in the underworld sends it past the bottom of the map, where
        /// there are no tiles to turn around in and nothing to hit, and it does not come
        /// back until the watchdog fires twenty seconds later.
        /// </summary>
        private static float DiveTarget(Player player)
        {
            return Math.Min(player.Center.Y + Abomanation.DiveDepth, (Main.maxTilesY - 60) * 16f);
        }

        /// <summary>Where it is trying to be this tick.</summary>
        private Vector2 Aim(Player player)
        {
            switch (phase)
            {
                case 1:
                    return new Vector2(player.Center.X, DiveTarget(player));

                case 2:
                    return new Vector2(player.Center.X, rushTop);

                case 3:
                    // Unsteered anyway; this only keeps the rotation pointing the way it
                    // is actually going.
                    return NPC.Center + Vector2.UnitY * 200f;

                default:
                    return player.Center + StrayOffset();
            }
        }

        /// <summary>
        /// How far off your centre it is actually driving.
        ///
        /// Two sines at rates that do not divide into each other, so the offset wanders
        /// over the whole circle instead of tracing a closed loop. A single sine, or two
        /// at a shared rate, gives an orbit, and an orbit is a pattern you can stand still
        /// and wait out.
        /// </summary>
        private Vector2 StrayOffset()
        {
            if (Abomanation.Stray <= 0f)
                return Vector2.Zero;

            float t = NPC.localAI[1] * 0.013f;
            float seed = NPC.whoAmI * 0.7f;

            return new Vector2(
                (float)Math.Sin(t * 1.7f + seed),
                (float)Math.Sin(t * 1.1f + seed * 1.6f)) * Abomanation.Stray;
        }

        /// <summary>
        /// One steering rule, three ways to be exempt from it.
        ///
        /// Converging on a desired velocity a fixed amount per tick is what vanilla's own
        /// worms do, and it is stable: it cannot overshoot by more than TurnSpeed and it
        /// cannot oscillate. The alternative, lerping toward the target direction, turns
        /// hardest when it is already pointing the right way.
        /// </summary>
        private void Steer(Vector2 aim)
        {
            if (phase == 3)
            {
                // Falling back. No steering at all: it is spent, and a worm that could
                // still correct on the way down would never miss.
                NPC.velocity.X *= 0.96f;
                NPC.velocity.Y += 0.35f;

                float terminal = Abomanation.Speed * 1.6f;
                if (NPC.velocity.Y > terminal)
                    NPC.velocity.Y = terminal;

                return;
            }

            // Close in, it commits to the pass rather than steering.
            //
            // The desired velocity reverses the instant it goes through you, and
            // converging per axis on a reversed target cancels: the two halves of the turn
            // fight and the worm slows to nothing. Simulated with no commit distance, it
            // ends up sitting inside a standing player at under one pixel a tick and never
            // leaves. Aiming down its own heading until it is clear makes it carry through
            // and come back round, which is stable and is also what a worm does.
            if (phase == 0 && NPC.velocity.LengthSquared() > 1f
                && NPC.Distance(aim) < Abomanation.CommitDistance)
            {
                aim = NPC.Center + NPC.velocity * 30f;
            }

            bool inTiles = Collision.SolidCollision(NPC.position, NPC.width, NPC.height);

            // In the ground it swims. In the open it falls, unless it is still a long way
            // off, in which case it keeps swimming through the air rather than crossing
            // half the map one parabola at a time.
            bool swims = Abomanation.Fly
                || inTiles
                || phase == 2
                || NPC.Distance(aim) > Abomanation.AirSwimDistance;

            if (!swims)
            {
                NPC.velocity.Y += 0.12f;

                if (NPC.velocity.Y > Abomanation.Speed)
                    NPC.velocity.Y = Abomanation.Speed;

                // Keeps whatever sideways push it left the ground with, bleeding off
                // slowly. Zeroing it here is what makes a worm hang in the air.
                NPC.velocity.X *= 0.995f;
                return;
            }

            if (NPC.soundDelay == 0)
            {
                NPC.soundDelay = (int)MathHelper.Clamp(NPC.Distance(aim) / 40f, 10f, 20f);
                SoundEngine.PlaySound(SoundID.WormDig, NPC.Center);
            }

            float top = phase == 2 ? Abomanation.RushSpeed : Abomanation.Speed;
            float turn = Abomanation.TurnSpeed * (phase == 0 ? 1f : 1.5f);

            Vector2 desired = (aim - NPC.Center).SafeNormalize(Vector2.UnitY) * top;

            // Turns harder while it is going the wrong way. Without this the U turn at the
            // far end of a pass takes sixty ticks and it hangs out there at walking pace
            // for a second of them.
            if (Vector2.Dot(NPC.velocity, desired) < 0f)
                turn *= Abomanation.TurnBoost;

            if (NPC.velocity.X < desired.X)
                NPC.velocity.X += turn;
            else if (NPC.velocity.X > desired.X)
                NPC.velocity.X -= turn;

            if (NPC.velocity.Y < desired.Y)
                NPC.velocity.Y += turn;
            else if (NPC.velocity.Y > desired.Y)
                NPC.velocity.Y -= turn;
        }

        /// <summary>Sand off the head while it is coming up, so the rush has a tell above ground.</summary>
        private void Breach()
        {
            if (Main.dedServ || NPC.alpha > 0)
                return;

            for (int i = 0; i < 2; i++)
            {
                int dust = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Sand,
                    NPC.velocity.X * 0.2f, NPC.velocity.Y * 0.2f, 100, default, 1.6f);

                Main.dust[dust].noGravity = true;
            }
        }

        /// <summary>
        /// Digs down and, once it is far enough under, takes the whole worm off the board.
        /// Removing the head alone would leave sixteen segments to die one per tick down
        /// the chain, in view, with nothing to follow.
        /// </summary>
        private void Leave()
        {
            NPC.velocity.Y += 0.4f;

            if (NPC.velocity.Y > 24f)
                NPC.velocity.Y = 24f;

            NPC.velocity.X *= 0.96f;

            if (NPC.velocity.LengthSquared() > 0.01f)
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

            if (NPC.Center.Y <= Main.rockLayer * 16.0 + 1200.0)
                return;

            int head = ModContent.NPCType<AbomanationHead>();
            int body = ModContent.NPCType<AbomanationBody>();
            int tail = ModContent.NPCType<AbomanationTail>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC other = Main.npc[i];

                if (other.active && (other.type == head || other.type == body || other.type == tail))
                    other.active = false;
            }
        }

        /// <summary>
        /// Builds the chain, once, on whichever machine decides things.
        ///
        /// Each segment is told who is in front of it, who the head is, and where along
        /// the body it sits. The place is what picks its texture and seeds its arms, so
        /// the five body sprites cycle in order rather than being rolled per segment.
        /// </summary>
        private void SpawnBody()
        {
            spawnedBody = true;

            int previous = NPC.whoAmI;
            int bodyType = ModContent.NPCType<AbomanationBody>();
            int tailType = ModContent.NPCType<AbomanationTail>();

            for (int i = 0; i <= Abomanation.BodySegments; i++)
            {
                bool last = i == Abomanation.BodySegments;

                int made = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y,
                    last ? tailType : bodyType, NPC.whoAmI);

                if (made < 0 || made >= Main.maxNPCs)
                    continue;

                AbomanationChain.Link(Main.npc[made], previous, NPC.whoAmI, i);

                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, made);

                // Backwards link. Nothing here reads it yet, but it costs one line and a
                // chain that only runs one way is the kind of thing that is missing at
                // exactly the moment something needs to walk it.
                Main.npc[previous].ai[0] = made;
                previous = made;
            }

            NPC.netUpdate = true;
        }

        // ------------------------------------------------------------------- draw -----

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
        {
            return AbomanationChain.Touching(NPC, target, 34f);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);

            if (NPC.life <= 0)
            {
                for (int i = 0; i < 20; i++)
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[Type].Value;

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, NPC.GetAlpha(drawColor),
                NPC.rotation, texture.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}
