using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Mantrap's jaws: an iron trap, thrown open.
    ///
    /// In the air (Flying) it turns over and falls. The first enemy it meets, it shuts on
    /// (Clamped): one the Meat Hook would drag (MeatHookRules.Hauled) is held where it is for
    /// <see cref="HoldTicks"/> (MantrapNPC: its own AI does not run), anything bigger is bitten and
    /// the jaws drop off. Landing on a floor it lies open (Set): after <see cref="ArmTicks"/> a click,
    /// and from then the first thing to walk into it is shut on, for <see cref="AmbushDamage"/> times
    /// the damage; the owner's own foot as well (Snared: <see cref="SnareTicks"/> held, and a bite of
    /// <see cref="SnareLife"/> of their life), unless they were standing on it when it set, in which
    /// case they have to step off it first. A wall or a ceiling it bounces off.
    ///
    /// Three at most lie open for one player: a fourth set shuts the oldest on nothing. One left
    /// lying <see cref="SetTicks"/> rusts shut. Whatever it has shut on, it lets go of and drops
    /// (Spent), turning over, and is gone.
    ///
    /// The owner decides (the hit, the landing, a foot in it) and says so (netUpdate); every
    /// machine moves and draws it, and every machine holds what it holds. ai[0] is the state, ai[1]
    /// the enemy or the player it has; where on them it sits is sent with it (SendExtraAI).
    /// </summary>
    public class MantrapJaws : ModProjectile
    {
        private const int Flying = 0, Set = 1, Clamped = 2, Spent = 3, Snared = 4;

        /// <summary>One frame of the sheet: open (the first) and shut (the second), one under the other.</summary>
        public const int FrameWide = 22, FrameHigh = 14;

        /// <summary>Ticks after it lands before it shuts on anything.</summary>
        public const int ArmTicks = 24;

        /// <summary>How long it lies open before it rusts shut: half a minute.</summary>
        public const int SetTicks = 30 * 60;

        /// <summary>Open on the ground at once for one player, at the most.</summary>
        public const int MostSet = 3;

        /// <summary>Ticks it holds an enemy it can hold, and its owner's foot.</summary>
        public const int HoldTicks = 60;
        public const int SnareTicks = 40;

        /// <summary>Ticks it hangs on to what it cannot hold before it drops off.</summary>
        private const int BiteTicks = 8;

        /// <summary>What it does to something that walks into it, of its damage.</summary>
        public const float AmbushDamage = 1.5f;

        /// <summary>What it takes out of its owner's foot: this share of their life at the most, and at least <see cref="SnareLeast"/>.</summary>
        public const float SnareLife = 0.06f;
        public const int SnareLeast = 8;

        private const float Gravity = 0.22f;
        private const float MostFall = 12f;

        /// <summary>Ticks it flies before it gives up and falls as it is.</summary>
        private const int FlightTicks = 150;

        /// <summary>Ticks it takes to fall away and go once it has let go.</summary>
        private const int SpentTicks = 45;

        private static readonly Color[] Sparks = { new Color(255, 240, 200), new Color(255, 184, 104), new Color(214, 120, 70) };

        private int State
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private int Target
        {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        /// <summary>Ticks in the state it is in. Each machine counts its own.</summary>
        private int ticks;
        private int lastState = -1;

        /// <summary>Where it sits on what it has, from its middle (sent).</summary>
        private Vector2 offset;

        /// <summary>Owner only: when it was set, to find the oldest; whether the owner has to step off it first.</summary>
        private uint setAt;
        private bool waitForOwner;

        /// <summary>A little jolt while it holds something, for every machine's eyes.</summary>
        private Vector2 struggle;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 10;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = SetTicks + 600;
            Projectile.netImportant = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.WriteVector2(offset);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            offset = reader.ReadVector2();
        }

        private bool Mine => Projectile.owner == Main.myPlayer;

        private Player Owner => Main.player[Projectile.owner];

        public override void AI()
        {
            if (State != lastState)
            {
                Entered(lastState, State);
                lastState = State;
                ticks = 0;
            }

            ticks++;
            struggle = Vector2.Zero;

            switch (State)
            {
                case Flying:
                    Fly();
                    break;
                case Set:
                    Lie();
                    break;
                case Clamped:
                    Hold();
                    break;
                case Snared:
                    Bite();
                    break;
                default:
                    FallAway();
                    break;
            }
        }

        /// <summary>What happens on every machine as it comes into a state: the sounds and the sparks.</summary>
        private void Entered(int from, int to)
        {
            Projectile.ResetLocalNPCHitImmunity();

            if (Main.dedServ || from < 0)
                return;

            if (to == Set)
            {
                SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.25f, Volume = 0.55f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.5f, Volume = 0.35f }, Projectile.Center);
                Collision.HitTiles(Projectile.position, new Vector2(0f, 3f), Projectile.width, Projectile.height);
            }
            else if (to == Clamped || to == Snared)
            {
                Snap(Projectile.Center, to == Snared ? 1.3f : 1f);
            }
            else if (to == Spent && from == Set)
            {
                // Shut on nothing: rusted, or the oldest of too many.
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.2f, Volume = 0.45f }, Projectile.Center);

                for (int i = 0; i < 5; i++)
                    Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Iron, 0f, -1f, 0, default, 0.7f).noGravity = true;
            }
        }

        /// <summary>The jaws meeting: a hard clack, sparks off the teeth, a flash.</summary>
        private static void Snap(Vector2 at, float strength)
        {
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.6f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Pitch = 0.15f, Volume = 0.7f }, at);

            SparkShard.Spray(at, Sparks[0], (int)(8 * strength), 4.5f * strength, 18f, 12, falls: true, palette: Sparks);
            PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, new Color(255, 196, 140) * 0.6f, 0.3f * strength, 12));
        }

        // ---- in the air ------------------------------------------------------------------------

        private void Fly()
        {
            Projectile.friendly = true;

            if (ticks > 6)
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MostFall);

            Projectile.rotation += 0.12f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);

            if (ticks > FlightTicks && Mine)
            {
                State = Spent;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>
        /// A floor met going down: it lands and lies open there. A wall or a ceiling: it comes off
        /// it, slowly. Set, or let go, it only stops.
        /// </summary>
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            bool floor = oldVelocity.Y > 0f && Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f;

            if (State == Flying)
            {
                if (floor)
                {
                    Projectile.velocity = Vector2.Zero;
                    Projectile.rotation = 0f;

                    if (Mine)
                        Lay();

                    return false;
                }

                if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f)
                    Projectile.velocity.X = -oldVelocity.X * 0.3f;

                if (oldVelocity.Y < 0f && Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f)
                    Projectile.velocity.Y = -oldVelocity.Y * 0.3f;

                return false;
            }

            if (State == Spent && floor && Math.Abs(oldVelocity.Y) > 2f)
            {
                Projectile.velocity.Y = -oldVelocity.Y * 0.25f;
                Projectile.velocity.X *= 0.6f;
                return false;
            }

            Projectile.velocity.X = 0f;

            if (floor)
                Projectile.velocity.Y = 0f;

            return false;
        }

        /// <summary>Owner only: it lies open here; if that makes one too many, the oldest of them shuts on nothing.</summary>
        private void Lay()
        {
            State = Set;
            setAt = Main.GameUpdateCount;
            waitForOwner = Owner.Hitbox.Intersects(Trigger());
            Projectile.netUpdate = true;

            int count = 0;
            MantrapJaws oldest = null;

            foreach (Projectile other in Main.ActiveProjectiles)
            {
                if (other.owner != Projectile.owner || other.type != Type || other.ModProjectile is not MantrapJaws jaws || jaws.State != Set)
                    continue;

                count++;

                if (other.whoAmI != Projectile.whoAmI && (oldest == null || jaws.setAt < oldest.setAt))
                    oldest = jaws;
            }

            if (count > MostSet && oldest != null)
            {
                oldest.State = Spent;
                oldest.Projectile.netUpdate = true;
            }
        }

        // ---- lying open --------------------------------------------------------------------------

        /// <summary>What sets it off: the plate and the jaws, and a little over them, where a foot comes down.</summary>
        private Rectangle Trigger() => new Rectangle((int)Projectile.position.X, (int)Projectile.position.Y - 4, Projectile.width, Projectile.height + 4);

        private bool Armed => ticks >= ArmTicks;

        private void Lie()
        {
            Projectile.velocity.X = 0f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MostFall);
            Projectile.rotation = 0f;

            // Nothing is hit unless it is shutting, and it says so only on the tick it does.
            Projectile.friendly = false;

            if (ticks == ArmTicks && !Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Mech with { Pitch = -0.3f, Volume = 0.5f }, Projectile.Center);
                PADParticleHandler.SpawnParticle(new CrownGlint(Projectile.Center + new Vector2(Main.rand.NextFloat(-6f, 6f), -4f), 9f, 14, new Color(255, 236, 210)));
            }

            // Now and then a tooth catches the light.
            if (Armed && !Main.dedServ && Main.rand.NextBool(220))
                PADParticleHandler.SpawnParticle(new CrownGlint(Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), -4f), 7f, 16, new Color(255, 236, 210) * 0.8f));

            if (!Mine)
                return;

            if (ticks >= SetTicks)
            {
                State = Spent;
                Projectile.netUpdate = true;
                return;
            }

            if (!Armed)
                return;

            Rectangle trigger = Trigger();
            Player owner = Owner;

            // The owner's own foot.
            if (owner.active && !owner.dead)
            {
                bool on = owner.Hitbox.Intersects(trigger);

                if (waitForOwner)
                {
                    if (!on)
                        waitForOwner = false;
                }
                else if (on)
                {
                    CatchOwner(owner);
                    return;
                }
            }

            // Anything else that walks into it: the hit itself is the game's, this tick (friendly).
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!MantrapRules.Shuts(npc) || !npc.Hitbox.Intersects(trigger))
                    continue;

                Projectile.friendly = true;
                break;
            }
        }

        /// <summary>Owner only: their own foot found it.</summary>
        private void CatchOwner(Player owner)
        {
            State = Snared;
            Target = owner.whoAmI;
            offset = new Vector2(0f, owner.height * 0.5f - Projectile.height * 0.5f);
            Projectile.netUpdate = true;

            int bite = Math.Max(SnareLeast, (int)(owner.statLifeMax2 * SnareLife));
            int side = Projectile.Center.X < owner.Center.X ? 1 : -1;

            owner.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.DeathText.Mantrap", owner.name)),
                bite, side, dodgeable: false, knockback: 0f);

            owner.GetModPlayer<MantrapSnare>().Snare(SnareTicks);
            owner.PIS().BumpScreenShake(6f, 10);
        }

        /// <summary>Lying open it shuts only on a body it can bite (the one that set it off), never on a critter beside it.</summary>
        public override bool? CanHitNPC(NPC target)
        {
            if (State == Set && !MantrapRules.Shuts(target))
                return false;

            return null;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            if (State == Set)
            {
                modifiers.SourceDamage *= AmbushDamage;
                modifiers.Knockback *= 0f;
            }
        }

        /// <summary>Owner only, from the game's hit: what it hit, it has shut on.</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (State != Flying && State != Set)
                return;

            State = Clamped;
            Target = target.whoAmI;
            offset = Seat(target, Projectile.Center);
            Projectile.netUpdate = true;
            Projectile.friendly = false;

            if (Mine)
                Owner.PIS().BumpScreenShake(3f, 6);
        }

        /// <summary>
        /// Where on an enemy it sits, from its middle: at its feet if it stands on anything, where
        /// it was hit if it flies, never off its edges.
        /// </summary>
        private Vector2 Seat(NPC npc, Vector2 at)
        {
            float halfW = Math.Max(0f, npc.width * 0.5f - 6f);
            float x = MathHelper.Clamp(at.X - npc.Center.X, -halfW, halfW);

            if (!npc.noGravity)
                return new Vector2(x, npc.height * 0.5f - Projectile.height * 0.5f);

            float halfH = Math.Max(0f, npc.height * 0.5f - 4f);
            return new Vector2(x, MathHelper.Clamp(at.Y - npc.Center.Y, -halfH, halfH));
        }

        // ---- holding -----------------------------------------------------------------------------

        private void Hold()
        {
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;

            NPC npc = Target >= 0 && Target < Main.maxNPCs ? Main.npc[Target] : null;

            if (npc == null || !npc.active || npc.life <= 0)
            {
                LetGo(Vector2.Zero);
                return;
            }

            Projectile.Center = npc.Center + offset;

            bool holds = MantrapRules.Holds(npc);
            int lasts = holds ? HoldTicks : BiteTicks;

            if (holds)
            {
                npc.GetGlobalNPC<MantrapNPC>().Hold();

                // It pulls against the chain.
                if (ticks % 6 < 2)
                    struggle = new Vector2(Main.rand.NextFloat(-1f, 1f), 0f);

                if (!Main.dedServ && Main.rand.NextBool(14))
                    SparkShard.Spray(Projectile.Center, Sparks[1], 1, 2.5f, 10f, 8, falls: true, palette: Sparks);
            }

            if (ticks >= lasts)
                LetGo(npc.velocity * 0.3f);
        }

        private void Bite()
        {
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = 0f;

            Player player = Target >= 0 && Target < Main.maxPlayers ? Main.player[Target] : null;

            if (player == null || !player.active || player.dead)
            {
                LetGo(Vector2.Zero);
                return;
            }

            Projectile.Center = player.Center + offset;

            if (ticks % 6 < 2)
                struggle = new Vector2(Main.rand.NextFloat(-1f, 1f), 0f);

            if (ticks >= SnareTicks)
                LetGo(Vector2.Zero);
        }

        /// <summary>It lets go and drops. Every machine counts down the same; only the owner says so.</summary>
        private void LetGo(Vector2 thrown)
        {
            if (!Mine)
                return;

            State = Spent;
            Projectile.velocity = thrown + new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -2.5f);
            Projectile.netUpdate = true;
        }

        private void FallAway()
        {
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.velocity.X *= 0.98f;
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MostFall);
            Projectile.rotation += 0.1f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X) * Math.Min(1f, Projectile.velocity.Length() / 3f);
            Projectile.alpha = (int)MathHelper.Clamp(255f * ticks / SpentTicks, 0f, 255f);

            if (ticks >= SpentTicks)
                Projectile.Kill();
        }

        // ---- drawing -----------------------------------------------------------------------------

        private int Frame => State == Clamped || State == Snared || State == Spent ? 1 : 0;

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Rectangle frame = texture.Frame(1, 2, 0, Frame);
            float fade = 1f - Projectile.alpha / 255f;
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());

            // Lying open, held or holding: drawn standing on its base. In the air: about its middle.
            bool upright = State == Set || State == Clamped || State == Snared;
            Vector2 origin = upright ? new Vector2(FrameWide * 0.5f, FrameHigh) : frame.Size() * 0.5f;
            Vector2 at = (upright ? Projectile.Bottom + new Vector2(0f, 2f) : Projectile.Center) + struggle - Main.screenPosition;

            // In the air, a few shadows of itself along the way it came.
            if (State == Flying)
            {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    float strength = 0.28f * (1f - i / (float)Projectile.oldPos.Length);
                    Vector2 was = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;

                    Main.EntitySpriteDraw(texture, was, frame, light * strength, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
                }
            }

            Main.EntitySpriteDraw(texture, at.Floor(), frame, light * fade, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>What the Mantrap's jaws shut on, and what they can hold.</summary>
    public static class MantrapRules
    {
        /// <summary>An enemy's body it can bite: what the Meat Hook sinks into, less the critters.</summary>
        public static bool Shuts(NPC npc) => MeatHookRules.Latches(npc) && !NPCID.Sets.CountsAsCritter[npc.type];

        /// <summary>What it holds where it stands: what the Meat Hook would drag (MeatHookRules.Hauled).</summary>
        public static bool Holds(NPC npc) => MeatHookRules.Hauled(npc);
    }
}
