using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A spell or arrow of the Bone Squad's: one of the game's own (Vanilla), taken over as it is
    /// in 1.4.4 (its size, its AI step by step, what it does when it hits a block and when it dies,
    /// its sounds and its dust), turned against enemies. Its AI kind is kept, so that what the game
    /// does by it (the wind, which way it knocks back) is done as for the game's own; the game's own
    /// AI for it is not run (PreAI returns false: this is it). The dust is the game's, drawn as the
    /// squad is (BoneSquadDust).
    ///
    /// Each hurts the same enemy again only ShotCooldown ticks after any of its kind did
    /// (usesIDStaticNPCImmunity: mine), and its damage was fixed when it was made
    /// (BoneSquadMinion.ShotDamage). The debuffs the game's give a player are given to enemies
    /// where an enemy can have them (On Fire!); Slow, Cursed and Blackout do nothing to an enemy.
    /// </summary>
    public abstract class BoneSquadShot : ModProjectile
    {
        /// <summary>The game's projectile it is.</summary>
        public abstract int Vanilla { get; }

        public override string Texture => "Terraria/Images/Projectile_" + Vanilla;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.MinionShot[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = BoneSquadMinion.ShotCooldown;
            Defaults();
        }

        /// <summary>Projectile.SetDefaults for the game's.</summary>
        protected abstract void Defaults();

        public override bool PreAI()
        {
            Act();
            return false;
        }

        /// <summary>The game's AI for it.</summary>
        protected abstract void Act();

        /// <summary>The ones drawn nothing of (the game's draw nothing of them: alpha 255); their dust is what is seen.</summary>
        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// The Skeleton Archer's flaming arrow (the game's 82): the bow's sound as it goes, straight for
    /// 15 ticks, then falling, 0.1 faster each tick, never faster than 16; a torch's flame behind it
    /// all the way; a tap and a burst of flame where it ends; one enemy in three it sets on fire for
    /// seven seconds. Drawn as the squad is.
    /// </summary>
    public class BoneSquadArrow : BoneSquadShot
    {
        public override int Vanilla => ProjectileID.FlamingArrow;

        protected override void Defaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.aiStyle = 1;
        }

        /// <summary>AI_001 for it.</summary>
        protected override void Act()
        {
            if (Projectile.ai[1] == 0f)
            {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.position);
                Projectile.ai[1] = 1f;
            }

            Projectile.ai[0]++;
            if (Projectile.ai[0] >= 15f)
            {
                Projectile.ai[0] = 15f;
                Projectile.velocity.Y += 0.1f;
            }

            Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;

            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;
        }

        /// <summary>Projectile.Update's flame behind it, after each move (this is asked of it right after, by Projectile.Damage, every move).</summary>
        public override bool? CanDamage()
        {
            BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100);
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.rand.Next(3) == 0)
                target.AddBuff(BuffID.OnFire, 420);
        }

        /// <summary>Projectile.Kill for it: the dig sound, and ten of the torch's flames.</summary>
        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Dig, new Vector2((int)Projectile.position.X, (int)Projectile.position.Y));
            for (int i = 0; i < 10; i++)
                BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100);
        }

        /// <summary>Where and as the game draws an arrow (Main.DrawProj_DrawNormalProjs), as BoneSquadLook draws.</summary>
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D own = TextureAssets.Projectile[Type].Value;
            Rectangle source = new Rectangle(0, 0, own.Width, own.Height);
            float across = (float)(own.Width - Projectile.width) * 0.5f + (float)Projectile.width * 0.5f;
            Vector2 at = new Vector2(Projectile.position.X - Main.screenPosition.X + across, Projectile.position.Y - Main.screenPosition.Y + (float)(Projectile.height / 2) + Projectile.gfxOffY);
            Vector2 origin = new Vector2(across, Projectile.height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            BoneSquadLook.Ring ring = BoneSquadLook.RingOf(own, source, null, true);
            BoneSquadLook.Draw(null, own, at, source, Projectile.GetAlpha(lightColor), Projectile.rotation, origin, Projectile.scale, effects, ring);
            return false;
        }
    }

    /// <summary>
    /// The Necromancer's shadow beam (the game's 290): its sound as it goes, then a thin line of
    /// shadow, a hundred moves a tick, off every block it meets; 300 moves in all, as the
    /// Necromancer makes it; through any number of enemies.
    /// </summary>
    public class BoneSquadShadowBeam : BoneSquadShot
    {
        public override int Vanilla => ProjectileID.ShadowBeamHostile;

        protected override void Defaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.aiStyle = 48;
            Projectile.extraUpdates = 100;
            Projectile.timeLeft = 300;
            Projectile.penetrate = -1;
        }

        /// <summary>aiStyle 48 for it.</summary>
        protected override void Act()
        {
            if (Projectile.localAI[0] == 0f)
                SoundEngine.PlaySound(SoundID.Item8, Projectile.position);

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] <= 3f)
                return;

            for (int i = 0; i < 3; i++)
            {
                Vector2 at = Projectile.position - Projectile.velocity * ((float)i * 0.3334f);
                Projectile.alpha = 255;
                Dust dust = BoneSquadDust.New(at, 1, 1, DustID.ShadowbeamStaff);
                dust.position = at;
                dust.scale = (float)Main.rand.Next(70, 110) * 0.013f;
                dust.velocity *= 0.2f;
            }
        }

        /// <summary>Projectile.HandleMovement for it: off a block, back the way it came on that side.</summary>
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.velocity.X != oldVelocity.X)
            {
                Projectile.position.X += Projectile.velocity.X;
                Projectile.velocity.X = 0f - oldVelocity.X;
            }

            if (Projectile.velocity.Y != oldVelocity.Y)
            {
                Projectile.position.Y += Projectile.velocity.Y;
                Projectile.velocity.Y = 0f - oldVelocity.Y;
            }

            return false;
        }
    }

    /// <summary>
    /// The Diabolist's inferno bolt (the game's 291): its sound as it goes, a trail of fire, through
    /// blocks, until it has gone past where the enemy stood when it was cast (ai[0] and ai[1]), both
    /// across and up or down; there, or after 300 ticks, it bursts (BoneSquadInfernoBlast). Sets an
    /// enemy on fire for 8 to 15 seconds.
    /// </summary>
    public class BoneSquadInferno : BoneSquadShot
    {
        public override int Vanilla => ProjectileID.InfernoHostileBolt;

        protected override void Defaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = 50;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        /// <summary>aiStyle 50 for it (1.4.4's).</summary>
        protected override void Act()
        {
            if (Projectile.localAI[0] == 0f)
            {
                SoundEngine.PlaySound(SoundID.Item20, Projectile.position);
                Projectile.localAI[0]++;
            }

            bool across = (Projectile.velocity.X < 0f && Projectile.position.X < Projectile.ai[0]) || (Projectile.velocity.X > 0f && Projectile.position.X > Projectile.ai[0]);
            bool upOrDown = (Projectile.velocity.Y < 0f && Projectile.position.Y < Projectile.ai[1]) || (Projectile.velocity.Y > 0f && Projectile.position.Y > Projectile.ai[1]);
            if (across && upOrDown)
                Projectile.Kill();

            for (int i = 0; i < 10; i++)
            {
                Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.InfernoFork, 0f, 0f, 100, default, 1.2f);
                dust.noGravity = true;
                dust.velocity *= 0.5f;
                dust.velocity += Projectile.velocity * 0.1f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 60 * Main.rand.Next(8, 16));
        }

        /// <summary>Projectile.Kill for it: the blast where it ends, made by its owner's machine.</summary>
        public override void OnKill(int timeLeft)
        {
            if (Projectile.owner == Main.myPlayer)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<BoneSquadInfernoBlast>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
        }
    }

    /// <summary>
    /// The Diabolist's inferno blast (the game's 292): its sound, then fire welling out of a space
    /// eight tiles across for nine seconds, burning what is in it, through blocks. Sets an enemy on
    /// fire for 8 to 15 seconds.
    /// </summary>
    public class BoneSquadInfernoBlast : BoneSquadShot
    {
        public override int Vanilla => ProjectileID.InfernoHostileBlast;

        protected override void Defaults()
        {
            Projectile.width = 130;
            Projectile.height = 130;
            Projectile.aiStyle = 50;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
        }

        /// <summary>aiStyle 50 for it.</summary>
        protected override void Act()
        {
            if (Projectile.localAI[0] == 0f)
            {
                SoundEngine.PlaySound(SoundID.Item74, Projectile.position);
                Projectile.localAI[0]++;
            }

            Projectile.ai[0]++;
            float flames = 25f;
            if (Projectile.ai[0] > 540f)
                flames -= (Projectile.ai[0] - 180f) / 2f;

            if (flames <= 0f)
            {
                flames = 0f;
                Projectile.Kill();
            }

            for (int i = 0; (float)i < flames; i++)
            {
                float x = Main.rand.Next(-10, 11);
                float y = Main.rand.Next(-10, 11);
                float speed = Main.rand.Next(3, 9);
                float length = (float)Math.Sqrt(x * x + y * y);
                length = speed / length;
                x *= length;
                y *= length;

                Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.InfernoFork, 0f, 0f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.position.X = Projectile.Center.X;
                dust.position.Y = Projectile.Center.Y;
                dust.position.X += Main.rand.Next(-10, 11);
                dust.position.Y += Main.rand.Next(-10, 11);
                dust.velocity.X = x;
                dust.velocity.Y = y;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 60 * Main.rand.Next(8, 16));
        }
    }

    /// <summary>
    /// The Ragged Caster's lost soul (the game's 293): its sound as it goes, a wisp of spirit,
    /// through blocks, two moves a tick, bending slowly towards the nearest enemy within 200 (by how
    /// far across and up or down together) at its own speed; 300 ticks, as the Ragged Caster makes
    /// it. Its speed towards an enemy a half again as the game's, as its speed out is (by request).
    /// </summary>
    public class BoneSquadLostSoul : BoneSquadShot
    {
        /// <summary>The game's speed for it towards a player, 3, a half again (by request).</summary>
        public const float Homing = 3f * BoneSquadCaster.Faster;

        public override int Vanilla => ProjectileID.LostSoulHostile;

        public override void SetStaticDefaults()
        {
            base.SetStaticDefaults();

            // As the game's own lost soul, the Spectre Staff's, that seeks enemies.
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        protected override void Defaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = 51;
            Projectile.alpha = 255;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 300;
        }

        /// <summary>aiStyle 51 for it, seeking enemies where the game's seeks players.</summary>
        protected override void Act()
        {
            if (Projectile.localAI[0] == 0f)
            {
                SoundEngine.PlaySound(SoundID.Item8, Projectile.position);
                Projectile.localAI[0]++;
            }

            for (int i = 0; i < 9; i++)
            {
                Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.SpectreStaff, 0f, 0f, 100, default, 1.3f);
                dust.noGravity = true;
                dust.velocity *= 0f;
            }

            float toX = Projectile.Center.X;
            float toY = Projectile.Center.Y;
            float nearest = 200f;
            bool found = false;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float x = npc.position.X + (float)(npc.width / 2);
                float y = npc.position.Y + (float)(npc.height / 2);
                float distance = Math.Abs(Projectile.position.X + (float)(Projectile.width / 2) - x) + Math.Abs(Projectile.position.Y + (float)(Projectile.height / 2) - y);
                if (distance < nearest)
                {
                    nearest = distance;
                    toX = x;
                    toY = y;
                    found = true;
                }
            }

            if (!found)
                return;

            Vector2 middle = new Vector2(Projectile.position.X + (float)Projectile.width * 0.5f, Projectile.position.Y + (float)Projectile.height * 0.5f);
            float dx = toX - middle.X;
            float dy = toY - middle.Y;
            float length = Homing / (float)Math.Sqrt(dx * dx + dy * dy);
            dx *= length;
            dy *= length;
            Projectile.velocity.X = (Projectile.velocity.X * 100f + dx) / 101f;
            Projectile.velocity.Y = (Projectile.velocity.Y * 100f + dy) / 101f;
        }
    }

    /// <summary>
    /// The Giant Cursed Skull's shadowflame (the game's 299): straight, three moves a tick, through
    /// blocks and water and any number of enemies; on its seventh move its sound and a burst of
    /// shadowflame, and from then on a trail of it.
    /// </summary>
    public class BoneSquadShadowflames : BoneSquadShot
    {
        public override int Vanilla => ProjectileID.Shadowflames;

        protected override void Defaults()
        {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.aiStyle = 1;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 2;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
        }

        /// <summary>AI_001 for it (it never falls: AI_001 does not count its moves).</summary>
        protected override void Act()
        {
            if (Projectile.localAI[0] == 6f)
            {
                SoundEngine.PlaySound(SoundID.Item8, Projectile.position);
                for (int i = 0; i < 40; i++)
                {
                    Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GiantCursedSkullBolt, 0f, 0f, 100);
                    dust.velocity *= 3f;
                    dust.velocity += Projectile.velocity * 0.75f;
                    dust.scale *= 1.2f;
                    dust.noGravity = true;
                }
            }

            Projectile.localAI[0]++;
            if (Projectile.localAI[0] > 6f)
            {
                for (int i = 0; i < 3; i++)
                {
                    Dust dust = BoneSquadDust.New(new Vector2(Projectile.position.X, Projectile.position.Y), Projectile.width, Projectile.height, DustID.GiantCursedSkullBolt, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100);
                    dust.velocity *= 0.6f;
                    dust.scale *= 1.4f;
                    dust.noGravity = true;
                }
            }

            Projectile.rotation = (float)Math.Atan2(Projectile.velocity.Y, Projectile.velocity.X) + 1.57f;

            if (Projectile.velocity.Y > 16f)
                Projectile.velocity.Y = 16f;
        }
    }
}
