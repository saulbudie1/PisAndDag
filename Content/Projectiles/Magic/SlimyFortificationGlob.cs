using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// One of Slimy Fortification's slimes: ai[0] the NPC it is after (-1: none), ai[1] ticks it waits before it turns
    /// onto it. Thrown up out of the air, it falls a moment, then flies at what it is after, turning onto it harder
    /// the longer it has flown; with nothing to be after, it just falls. It lands on one thing, hurts it a little and
    /// leaves its slime on it (SlimeCasing.Slimed, on its caster's machine), or bursts on what is solid.
    /// </summary>
    public class SlimyFortificationGlob : ModProjectile
    {
        public const float Speed = 11f;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 150;
            Projectile.alpha = 40;
        }

        public override void AI()
        {
            Projectile.localAI[0]++;

            int after = (int)Projectile.ai[0];
            NPC target = after >= 0 && after < Main.maxNPCs && Main.npc[after].active && !Main.npc[after].dontTakeDamage ? Main.npc[after] : null;

            if (target == null || Projectile.localAI[0] < 10f + Projectile.ai[1])
            {
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.28f, 12f);
                Projectile.velocity.X *= 0.985f;
            }
            else
            {
                float turn = MathHelper.Clamp(0.06f + (Projectile.localAI[0] - 10f) * 0.006f, 0.06f, 0.4f);
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * Speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
            }

            // Squashed and stretched the way it goes; its two pictures in turn.
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.frame = (int)(Projectile.localAI[0] / 5f) % 2;

            if (!Main.dedServ && Main.rand.NextBool(3))
            {
                Dust drip = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.t_Slime, 0f, 0f, 150, new Color(86, 150, 255, 150), 0.9f);
                drip.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner == Main.myPlayer)
                SlimeCasing.Slimed(target);
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.4f, Pitch = 0.5f, PitchVariance = 0.2f }, Projectile.Center);

            for (int d = 0; d < 8; d++)
                Dust.NewDustPerfect(Projectile.Center, DustID.t_Slime, Main.rand.NextVector2Circular(3f, 3f), 150, new Color(86, 150, 255, 150), Main.rand.NextFloat(0.9f, 1.3f));
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D glob = TextureAssets.Projectile[Type].Value;
            Rectangle frame = glob.Frame(1, 2, 0, Projectile.frame);
            float fast = MathHelper.Clamp(Projectile.velocity.Length() / Speed, 0f, 1f);
            Vector2 scale = new Vector2(1f + 0.35f * fast, 1f - 0.2f * fast);
            Color lit = Color.Lerp(lightColor, Color.White, 0.3f) * 0.9f;

            Main.EntitySpriteDraw(glob, Projectile.Center - Main.screenPosition, frame, lit, Projectile.rotation, frame.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// Slime on an NPC, from Slimy Fortification.
    ///
    /// The build up is its caster's own (kept on their machine, where their slimes land): each slime adds a share of
    /// what is needed that goes by the NPC's size, three for something a player's size and a good many for something
    /// big; three seconds with none added and it starts to dry off. Full, the caster gives it Encased for five
    /// seconds, a buff, so the game tells everyone.
    ///
    /// Encased, on every machine: a small thing (<see cref="Small"/>) does nothing at all (its AI is not run), is let
    /// fall to the ground whatever it was, a flyer or a ghost through walls, and cannot hurt by touch; a bigger one
    /// goes on at four tenths of its speed. A boss, or a part of one, is never encased.
    ///
    /// Drawn over it: the slime that is on it so far, creeping up it from its feet; encased, all of it in a wobbling
    /// blob of slime.
    /// </summary>
    public class SlimeCasing : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>How much slime encases; what one slime adds to something small; ticks before it starts to dry off.</summary>
        public const float Needed = 100f;
        public const float Each = 34f;
        public const int Dries = 180;

        private const string CasingArt = "PisAndDag/Content/Projectiles/Magic/SlimeCasing";

        private float slime;
        private int since;
        private bool wasEncased;
        private bool keptNoCollide, keptNoGravity;

        /// <summary>Never encased: a boss, a part of one, a part of a worm, or something nothing can hurt.</summary>
        public static bool Immune(NPC npc) =>
            npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type] || npc.realLife >= 0 || npc.dontTakeDamage || npc.friendly;

        /// <summary>Small: it can be knocked back, and is no bigger than about three tiles by three.</summary>
        public static bool Small(NPC npc) => !Immune(npc) && npc.knockBackResist > 0f && npc.width * npc.height <= 2600;

        public static bool Encased(NPC npc) => npc.HasBuff(ModContent.BuffType<Encased>());

        /// <summary>A slime has landed on it, on its caster's machine.</summary>
        public static void Slimed(NPC npc)
        {
            if (Immune(npc) || Encased(npc) || !npc.TryGetGlobalNPC(out SlimeCasing casing))
                return;

            float share = MathHelper.Clamp(1400f / Math.Max(1, npc.width * npc.height), 0.2f, 1f);
            casing.slime += Each * share;
            casing.since = 0;

            if (casing.slime < Needed)
                return;

            casing.slime = 0f;
            npc.AddBuff(ModContent.BuffType<Encased>(), Buffs.Encased.Ticks);

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Item154 with { Volume = 0.9f, Pitch = -0.3f }, npc.Center);

                for (int d = 0; d < 24; d++)
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.t_Slime, 0f, -1f, 150, new Color(86, 150, 255, 150), 1.4f);
            }
        }

        public override bool PreAI(NPC npc)
        {
            bool encased = Encased(npc);

            if (encased != wasEncased)
            {
                // Let fall while it is held, whatever it is; as it was again after.
                if (encased && Small(npc))
                {
                    keptNoCollide = npc.noTileCollide;
                    keptNoGravity = npc.noGravity;
                    npc.noTileCollide = false;
                }
                else if (!encased && wasEncased && Small(npc))
                {
                    npc.noTileCollide = keptNoCollide;
                    npc.noGravity = keptNoGravity;
                }

                wasEncased = encased;
            }

            if (!encased)
            {
                if (slime > 0f && ++since > Dries)
                    slime = Math.Max(0f, slime - 0.6f);

                return true;
            }

            if (!Small(npc))
                return true;

            // Held: it does nothing, and falls.
            npc.noTileCollide = false;
            npc.velocity.X *= 0.85f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.4f, 11f);
            return false;
        }

        public override void PostAI(NPC npc)
        {
            // Too big to hold: slowed.
            if (Encased(npc) && !Small(npc))
                npc.position -= npc.velocity * 0.6f;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => !(Encased(npc) && Small(npc));

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            bool encased = Encased(npc);

            if (!encased && slime <= 0f)
                return;

            Texture2D casing = ModContent.Request<Texture2D>(CasingArt).Value;
            float t = Main.GlobalTimeWrappedHourly;
            Color lit = Color.Lerp(drawColor, Color.White, 0.25f);

            if (encased)
            {
                // All of it, in a blob a little bigger than it is, wobbling.
                Vector2 size = new Vector2(npc.width + 14f, npc.height + 12f);
                Vector2 wobble = new Vector2(1f + 0.05f * (float)Math.Sin(t * 5f + npc.whoAmI), 1f - 0.05f * (float)Math.Sin(t * 5f + npc.whoAmI));
                Vector2 scale = size / casing.Size() * wobble;
                Vector2 at = new Vector2(npc.Center.X, npc.Bottom.Y + 2f) - screenPos;
                spriteBatch.Draw(casing, at, null, lit * 0.72f, 0f, new Vector2(casing.Width * 0.5f, casing.Height), scale, SpriteEffects.None, 0f);
                return;
            }

            // What is on it so far: up it from its feet.
            float share = MathHelper.Clamp(slime / Needed, 0f, 1f);
            int rows = (int)(casing.Height * share) / 2 * 2;

            if (rows <= 0)
                return;

            Rectangle part = new Rectangle(0, casing.Height - rows, casing.Width, rows);
            Vector2 full = new Vector2(npc.width + 10f, npc.height + 8f) / casing.Size();
            Vector2 feet = new Vector2(npc.Center.X, npc.Bottom.Y + 2f) - screenPos;
            spriteBatch.Draw(casing, feet, part, lit * 0.6f, 0f, new Vector2(casing.Width * 0.5f, rows), full, SpriteEffects.None, 0f);
        }
    }
}
