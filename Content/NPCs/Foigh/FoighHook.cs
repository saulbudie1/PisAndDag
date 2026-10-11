using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The hook out of Foigh's bottom hand, on its chain (from his second phase:
    /// Foigh.Reach). Fast and straight, as far as its chain is long, and stopped by the
    /// first tile it meets.
    ///
    /// Each machine tries it against its own player, as his knives are tried. Met by a
    /// parry it is knocked away and gone (Parry.AddProjectile), and he has to take it in
    /// again. One whom a hurt still shields it goes through. On them it bites: a small
    /// blow through armour, which leaves them none of the grace a hurt leaves (the blow
    /// it brings is still to come), and the server is told, with whether they were
    /// stood on the ground or not; what he does with whoever is on it is his (he goes
    /// to one on the ground along the chain; one off the ground is pulled in to him).
    ///
    /// The picture is the mod's own Meat Hook's (a stand-in: PlaceholderTextures.md), and
    /// points up; the projectile is its middle. The chain is drawn from his hand to its
    /// tail by whoever draws him (Foigh.DrawChain).
    /// </summary>
    public class FoighHook : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Projectiles/Ranged/MeatHook_Hook";

        /// <summary>Pixels a tick; and the ticks it flies: fifty tiles of chain, near enough.</summary>
        public const float Speed = 22f;
        public const int Life = 36;

        /// <summary>The part that bites: this far back along it and this far ahead of its middle, this thick.</summary>
        private const float Back = 12f;
        private const float Ahead = 14f;
        private const float Width = 12f;

        /// <summary>From its middle back to where the chain is made fast, along the way it points (the picture's (11, 30) from its (11, 16)).</summary>
        public const float Tail = 14f;

        private static readonly Color Steel = new Color(214, 226, 240);

        // ai[0] his whoAmI
        // localAI[0] 1 once it has bitten this machine's player · localAI[1] the ticks this machine has seen of it

        public override void SetStaticDefaults()
        {
            // In the air, and not already in this machine's player.
            Parry.AddProjectile(Type, projectile => projectile.localAI[0] == 0f);
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Life;
        }

        private Foigh Owner()
        {
            int i = (int)Projectile.ai[0];

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active ? npc.ModNPC as Foigh : null;
        }

        public override void AI()
        {
            Foigh foigh = Owner();

            // He is gone, or doing something else: so is it.
            if (foigh == null || !foigh.Casting)
            {
                if (Projectile.owner == Main.myPlayer)
                    Projectile.Kill();
                else
                    Projectile.alpha = 255;

                return;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Projectile.localAI[1] == 0f && !Main.dedServ)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);

            Projectile.localAI[1]++;

            if (!Main.dedServ && (int)Projectile.localAI[1] % 5 == 0)
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = 0.2f, PitchVariance = 0.2f, Volume = 0.25f }, Projectile.Center);

            Lighting.AddLight(Projectile.Center, 0.2f, 0.17f, 0.12f);

            Strike(foigh);
        }

        /// <summary>Against this machine's own player: it bites, once, and the server is told where they were.</summary>
        private void Strike(Foigh foigh)
        {
            if (Main.dedServ || Projectile.localAI[0] != 0f || Projectile.alpha >= 255)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Vector2 along = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 from = Projectile.Center - along * Back;
            float where = 0f;

            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, from, Projectile.Center + along * Ahead, Width, ref where))
                return;

            // On them: the parry first, if it came from their front. At their arc, before it
            // gets this far, the parry finds it for itself (ParryPlayer.PostUpdate).
            if (me.GetModPlayer<ParryPlayer>().Takes(Projectile, from))
                return;

            if (me.immune)
                return;

            // Where they are as it bites, before the blow moves them or lets go their own hook.
            bool onGround = !Foigh.Aloft(me) && !Foigh.Hooked(me);

            int damage = Main.DamageVar(Foigh.HookDamage, -me.luck);
            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            // (On the sword's own slot: it leaves them no grace against the blow it brings.)
            if (!FoighGripPlayer.Bite(me, "FoighHook", damage, direction))
                return;

            Projectile.localAI[0] = 1f;

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.2f, Volume = 0.8f }, me.Center);
            SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 0.7f }, me.Center);
            me.PIS().BumpScreenShake(4f, 8);

            // A blow that killed them holds nobody.
            if (!me.dead)
                foigh.Tell(Foigh.Word.Hooked, flag: onGround);

            // It is in them: on this machine it is gone, and the server's goes when it hears.
            Projectile.alpha = 255;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!Main.dedServ && Projectile.alpha < 255)
            {
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);
                SparkShard.Spray(Projectile.Center, Steel, 6, 4f, 16f, 12, falls: true);
            }

            return true;
        }

        /// <summary>Gone, however: where it was is where his chain comes back from.</summary>
        public override void OnKill(int timeLeft)
        {
            Owner()?.HookFell(Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255)
                return false;

            Foigh foigh = Owner();

            if (foigh == null)
                return false;

            Vector2 way = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 tail = Projectile.Center - way * Tail;

            Foigh.DrawChain(Main.spriteBatch, foigh.HookHand, tail, Main.screenPosition, false);
            Foigh.DrawHookHead(Main.spriteBatch, tail, way, Main.screenPosition, lightColor);
            return false;
        }
    }
}
