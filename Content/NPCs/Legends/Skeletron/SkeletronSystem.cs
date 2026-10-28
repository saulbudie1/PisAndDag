using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Legends
{
    /// <summary>
    /// What Skeletron's rework needs of the game round him:
    ///
    ///     his body's pictures, loaded with the mod (SkeletronRig)
    ///     his hands not drawn by the game (Main.DrawNPCDirect, which draws a hand's arm bones before any hook can stop
    ///     it): he draws them, with his arms (LegendsSkeletron.DrawBody); and he is always drawn, so a hand on screen
    ///     with him off it is still drawn
    ///     his name his own to put up (BossPlate: his coming has its own camera and names him as he roars), and his bar
    ///     up with it
    ///     his own bones flying when he and his hands die (<see cref="Dies"/>), in the look he had
    /// </summary>
    public class SkeletronSystem : ModSystem
    {
        /// <summary>His gores, by name (Content/Gores, loaded by the game from that folder): his own look, then the red hat's.</summary>
        internal static int[] Upper = new int[2];
        internal static int[] JawGore = new int[2];
        internal static int[] Palm = new int[2];
        internal static int[] Finger = new int[2];
        internal static int[] Thumb = new int[2];
        internal static int[] Bone = new int[2];
        internal static int Hat;

        public override void Load()
        {
            SkeletronRig.Load();

            On_NPC.VanillaHitEffect += HitEffect;

            if (!Main.dedServ)
                On_Main.DrawNPCDirect += SkipHands;
        }

        public override void Unload()
        {
            SkeletronRig.Unload();
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MustAlwaysDraw[NPCID.SkeletronHead] = true;
        }

        public override void PostSetupContent()
        {
            BossPlate.HasOwnIntro(NPCID.SkeletronHead);
            BossPlate.NamesItself(NPCID.SkeletronHead);
            BossPlate.BarWaitsForName(NPCID.SkeletronHead);

            for (int red = 0; red < 2; red++)
            {
                string look = red == 1 ? "Red_Hat_" : "";

                Upper[red] = FindGore(look + "SkeletronUpperGore");
                JawGore[red] = FindGore(look + "SkeletronJawGore");
                Palm[red] = FindGore(look + "SkeletronPalmGore");
                Finger[red] = FindGore(look + "SkeletronFingerGore");
                Thumb[red] = FindGore(look + "SkeletronThumbGore");
                Bone[red] = FindGore(look + "SkeletronBoneGore");
            }

            Hat = FindGore("Red_Hat_SkeletronHatGore");
        }

        private static int FindGore(string name) => ModContent.TryFind("PisAndDag", name, out ModGore gore) ? gore.Type : -1;

        /// <summary>A hand of his, in the world (not a picture of one, as the bestiary has): he draws it, the game does not.</summary>
        private static void SkipHands(On_Main.orig_DrawNPCDirect orig, Main self, SpriteBatch mySpriteBatch, NPC rCurrentNPC, bool behindTiles, Vector2 screenPos)
        {
            if (rCurrentNPC.type == NPCID.SkeletronHand && !rCurrentNPC.IsABestiaryIconDummy && rCurrentNPC.whoAmI >= 0
                && rCurrentNPC.whoAmI < Main.maxNPCs && ReferenceEquals(Main.npc[rCurrentNPC.whoAmI], rCurrentNPC)
                && SkeletronRig.Ready && LegendsSkeletronHand.HeadOf(rCurrentNPC) != null)
                return;

            orig(self, mySpriteBatch, rCurrentNPC, behindTiles, screenPos);
        }

        /// <summary>His and his hands' deaths: their own bones fly, not the game's; their hits are the game's.</summary>
        private static void HitEffect(On_NPC.orig_VanillaHitEffect orig, NPC self, int hitDirection, double dmg, bool instantKill)
        {
            if (Main.dedServ || self.life > 0 || self.type != NPCID.SkeletronHead && self.type != NPCID.SkeletronHand || Upper[0] <= 0)
            {
                orig(self, hitDirection, dmg, instantKill);
                return;
            }

            Dies(self, hitDirection);
        }

        /// <summary>
        /// Dead, in the look it had. His skull: its two halves and the hat, flung apart, a burst of bone. A hand: its palm,
        /// its fingers and its thumb, and the bones of its arm along where it was, all falling.
        /// </summary>
        internal static void Dies(NPC npc, int hitDirection)
        {
            bool red = false;
            NPC head = npc.type == NPCID.SkeletronHead ? npc : LegendsSkeletronHand.HeadOf(npc);

            if (head != null && head.TryGetGlobalNPC(out LegendsSkeletron state))
                red = state.redHat;

            int look = red ? 1 : 0;
            var source = npc.GetSource_Death();

            for (int i = 0; i < 150; i++)
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Bone, 2.5f * hitDirection, -2.5f);

            SoundEngine.PlaySound(SkeletronSounds.Rattle with { Volume = 0.8f, Pitch = -0.3f }, npc.Center);

            if (npc.type == NPCID.SkeletronHead)
            {
                Vector2 up = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(4f, 7f));

                Fling(source, npc.Center + new Vector2(0f, -10f), npc.velocity + up, Upper[look]);
                Fling(source, npc.Center + new Vector2(0f, 30f), npc.velocity + new Vector2(Main.rand.NextFloat(-3f, 3f), 1f), JawGore[look]);

                if (red)
                    Fling(source, npc.Center + new Vector2(0f, -40f), npc.velocity + new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(8f, 11f)), Hat);

                PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, new Color(255, 230, 190), 2.6f, 22));
                return;
            }

            float side = LegendsSkeletron.SideOf(npc);

            Fling(source, npc.Center, npc.velocity * 0.5f + new Vector2(hitDirection * 2f, -3f), Palm[look]);
            Fling(source, npc.Center + new Vector2(side * 14f, 4f), new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(2f, 5f)), Thumb[look]);

            for (int i = 0; i < 4; i++)
                Fling(source, npc.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), 18f), new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(1f, 5f)), Finger[look]);

            // The arm's bones, along where it hung.
            if (head != null)
            {
                Vector2 top = npc.TryGetGlobalNPC(out LegendsSkeletronHand loose) && loose.armKnown ? loose.armTop : SkeletronRig.Shoulder(head.Center, side, head.scale);
                SkeletronRig.ArmJoints(top, npc.Center, new Vector2(side, 0.7f), head.scale, out Vector2 shoulder, out Vector2 elbow);

                // The upper arm and the forearm, each the game's arm bone.
                Fling(source, Vector2.Lerp(shoulder, elbow, 0.5f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0f, 3f)), Bone[look]);
                Fling(source, Vector2.Lerp(elbow, npc.Center, 0.5f), new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0f, 3f)), Bone[look]);
            }
        }

        private static void Fling(Terraria.DataStructures.IEntitySource source, Vector2 centre, Vector2 velocity, int type)
        {
            if (type <= 0)
                return;

            Vector2 size = TextureAssets.Gore[type].Size();
            Terraria.Gore.NewGore(source, centre - size * 0.5f, velocity, type);
        }
    }

    /// <summary>
    /// The old man, cursed (vanilla sets his ai[3] as it makes Skeletron of him): he is not killed by the game in a spray
    /// of his own blood any more, but simply taken off the world, quietly; Skeletron's coming plays what becomes of him
    /// (LegendsSkeletron.Arrival). Not after Skeletron is beaten, when the same mark means only that he moves out.
    /// </summary>
    public class OldManCursed : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.OldMan;

        public override bool PreAI(NPC npc)
        {
            if (npc.ai[3] != 1f || NPC.downedBoss3)
                return true;

            npc.active = false;
            npc.netUpdate = true;
            return false;
        }
    }

    /// <summary>
    /// A player in Skeletron's way. In his scenes (his coming, the loss of his hands): nothing hurts them while they
    /// watch, and they can still move (by request: "player can still move"). In his hand: held where it holds them, unable
    /// to move or use anything, for as long as he asks (every tick); then thrown.
    /// </summary>
    public class SkeletronPlayer : ModPlayer
    {
        private int heldTicks;
        private Vector2 heldAt;
        private bool throwing;
        private Vector2 thrown;
        private int sheltered;

        /// <summary>Thrown by him: no fall hurts them until they are down again (or a few seconds are out); and whether they have left the ground since.</summary>
        private int fallSafe;
        private bool fallSafeAloft;
        private const int FallSafeTicks = 240;

        public bool Held => heldTicks > 0 && !Player.dead;

        /// <summary>Held there this tick (asked every tick it lasts).</summary>
        public void Hold(Vector2 at)
        {
            heldTicks = 3;
            heldAt = at;
        }

        /// <summary>Let go of, thrown this hard.</summary>
        public void Throw(Vector2 velocity)
        {
            if (heldTicks <= 0 && !throwing)
                return;

            heldTicks = 0;
            thrown = velocity;
            throwing = true;
            fallSafe = FallSafeTicks;
            fallSafeAloft = false;
        }

        /// <summary>In one of his scenes this tick: nothing hurts them.</summary>
        public void Shelter() => sheltered = 3;

        public override void ResetEffects()
        {
            if (sheltered > 0)
                sheltered--;

            if (!Held)
            {
                if (heldTicks > 0)
                    Player.fullRotation = 0f;

                heldTicks = 0;
                return;
            }

            heldTicks--;
            Player.noItems = true;

            if (heldTicks == 0)
                Player.fullRotation = 0f;
        }

        public override void SetControls()
        {
            if (!Held)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override void PreUpdateMovement()
        {
            if (throwing)
            {
                throwing = false;
                Player.velocity = thrown;
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.fullRotation = 0f;
                return;
            }

            if (!Held)
                return;

            Player.Center = heldAt;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fullRotationOrigin = Player.Size * 0.5f;
            Player.fullRotation = 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.35f);
        }

        public override void PostUpdateEquips()
        {
            if (fallSafe > 0)
                Player.noFallDmg = true;

            if (sheltered <= 0)
                return;

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
        }

        /// <summary>In his scene, nothing at all: not the ground's spikes or lava either, which the game lets through a player's immunity.</summary>
        public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => sheltered > 0;

        public override void PostUpdate()
        {
            if (fallSafe <= 0)
                return;

            // Down again, after being in the air (the landing itself still covered: the game reads it next tick).
            fallSafe--;

            if (Player.velocity.Y != 0f)
                fallSafeAloft = true;
            else if (fallSafeAloft)
                fallSafe = 0;
        }

        public override void OnRespawn()
        {
            heldTicks = 0;
            throwing = false;
            fallSafe = 0;
            Player.fullRotation = 0f;
        }
    }

    /// <summary>
    /// Half of the old man, torn off him and cast aside by Skeletron's hands (LegendsSkeletron.Arrival): his own picture,
    /// cut down the middle, flung turning, bleeding as it flies; it lands, rocks over onto its side and lies there, then
    /// fades. Each machine's own.
    /// </summary>
    public class OldManHalf : PADParticle
    {
        public override string Texture => SkeletronRig.Folder + "Skeletron_OldMan";
        public override bool UseCustomDraw => true;
        public override bool SetLifetime => true;
        public override bool Important => true;

        private const float Weight = 0.32f;
        private const int Fades = 60;

        /// <summary>Which half (the picture's left, or right), mirrored or not; turning; and come to rest.</summary>
        private readonly bool right;
        private readonly bool flip;
        private float spin;
        private bool resting;
        private int bounces;

        /// <param name="position">Where the half's cut edge is, at his middle (world).</param>
        public OldManHalf(Vector2 position, Vector2 velocity, float rotation, bool right, bool flip)
        {
            Position = position;
            Velocity = velocity;
            Rotation = rotation;
            Lifetime = 420;
            Scale = 1f;

            this.right = right;
            this.flip = flip;
            spin = (right != flip ? 1f : -1f) * Main.rand.NextFloat(0.12f, 0.2f);
        }

        public override void Update()
        {
            if (resting)
            {
                Velocity = Vector2.Zero;

                // Rocked over onto its side.
                float lie = (right != flip ? 1f : -1f) * MathHelper.PiOver2;
                Rotation = Rotation.AngleLerp(lie, 0.08f);

                if (Time % 9 == 0 && Time < 200)
                {
                    Dust blood = Dust.NewDustPerfect(Position + Main.rand.NextVector2Circular(6f, 4f), DustID.Blood, new Vector2(0f, 0.5f), 0, default, 1.1f);
                    blood.noGravity = false;
                }

                return;
            }

            Velocity.Y = Math.Min(Velocity.Y + Weight, 14f);
            Velocity.X *= 0.995f;
            Rotation += spin;

            if (Main.rand.NextBool(2))
                Dust.NewDustPerfect(Position + Main.rand.NextVector2Circular(6f, 10f), DustID.Blood, Velocity * 0.3f + Main.rand.NextVector2Circular(1f, 1f), 0, default, 1.2f);

            // The ground: once off it, and then lying there.
            if (Velocity.Y > 0f && Collision.SolidCollision(Position + Velocity - new Vector2(6f, 0f), 12, 12))
            {
                if (bounces < 1 && Velocity.Y > 3f)
                {
                    bounces++;
                    Velocity.Y *= -0.3f;
                    Velocity.X *= 0.5f;
                    spin *= -0.6f;
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = -0.4f, MaxInstances = 2 }, Position);
                }
                else
                {
                    resting = true;
                    Velocity = Vector2.Zero;
                }
            }

            // Into a wall: stopped there, and falls.
            if (Collision.SolidCollision(Position + new Vector2(Velocity.X, 0f) - new Vector2(6f, 6f), 12, 12))
                Velocity.X *= -0.2f;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Texture2D texture = SkeletronRig.OldMan;

            if (texture == null)
                return;

            int frameHeight = texture.Height / SkeletronRig.OldManFrames;
            int half = texture.Width / 2;

            // The picture's halves: the one on the screen's left of the cut is the picture's left, unless it is mirrored.
            bool pictureRight = right != flip;
            Rectangle frame = new Rectangle(pictureRight ? half : 0, 0, half, frameHeight);

            // About the cut, at his middle: the picture's middle across, two-thirds of the way down.
            Vector2 origin = new Vector2(pictureRight ? 0f : half, frameHeight * 0.62f);

            if (flip)
                origin.X = half - origin.X;

            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)(Position.Y / 16f));
            float fade = Time > Lifetime - Fades ? (Lifetime - Time) / (float)Fades : 1f;

            spriteBatch.Draw(texture, Position - Main.screenPosition, frame, light * fade, Rotation, origin, Scale,
                flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// The game's own homing skulls (Skeletron's, Projectile_270), in Legends: they lose whoever they chase once that player
    /// cuts more than about 65 degrees off their nose at speed, or once they have gone by them; then they fly on as the
    /// game flies them when their homing is over (by request: "homing skulls following too persistently, if i break their
    /// ankle they better stopp following me"). The game homes them while ai[1] runs from 30 to 110: a lost one is put at 110.
    /// </summary>
    public class SkeletronSkullTracking : GlobalProjectile
    {
        private const float HomingFrom = 30f;
        private const float HomingTo = 110f;

        /// <summary>The nearest it has come to whoever it chases.</summary>
        private float closest = float.MaxValue;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.Skull;

        public override void PostAI(Projectile projectile)
        {
            if (!PADWorld.Legends || !projectile.hostile || projectile.ai[1] <= HomingFrom || projectile.ai[1] >= HomingTo)
                return;

            Player target = Main.player[Player.FindClosest(projectile.Center, 1, 1)];

            if (!target.active || target.dead)
                return;

            Vector2 to = target.Center - projectile.Center;
            float distance = to.Length();
            float off = Math.Abs(MathHelper.WrapAngle(to.ToRotation() - projectile.velocity.ToRotation()));

            bool lost = projectile.velocity.Length() > 6f && off > 1.15f
                || closest < 150f && distance > closest + 20f;

            closest = Math.Min(closest, distance);

            if (lost)
                projectile.ai[1] = HomingTo;
        }
    }
}
