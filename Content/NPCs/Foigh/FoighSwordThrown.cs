using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// Foigh's sword, thrown: from over them, straight at where they will be, point first.
    /// Wherever it comes down it goes into the ground and stays there, standing at the angle
    /// it came in at, until he has run to it and taken it (Foigh.FourTake), or there is
    /// nobody coming for it and it fades.
    ///
    /// In the air it wears the yellow star (CCBypass): it cannot be parried, and whoever it
    /// goes through takes the sword's damage through armour and is thrown down for a second,
    /// whatever shield they have (RagdollPlayer). Once, against each machine's own player.
    /// In the ground it is only a sword in the ground, drawn behind the tiles so that the
    /// part of it in the ground is in the ground.
    ///
    /// The same picture as the one in his hand (its sharp side, from his second phase;
    /// bloodied, and burning, in his third, where it leaves the wound his sword leaves).
    /// The projectile is its point: the hitbox, the flight and the drawing are all about
    /// the point.
    /// </summary>
    public class FoighSwordThrown : ModProjectile, ICCBypassOutline
    {
        public override string Texture => FoighRig.Folder + FoighRig.Names[FoighRig.Sword];

        /// <summary>How fast it leaves his hand, and the little it gains falling each tick.</summary>
        public const float Speed = 24f;
        private const float Gravity = 0.1f;
        private const float MaxFall = 30f;

        /// <summary>How far the point goes into the ground.</summary>
        private const float Embed = 26f;

        /// <summary>The part that hurts: the blade, back from the point, this thick.</summary>
        private const float Blade = FoighRig.GripToTip - FoighRig.GripToGuard;
        private const float BladeWidth = 16f;

        /// <summary>Whoever it goes through: thrown this way (along it), and down this long after they land.</summary>
        private const float ThrownAlong = 7f;
        private const float ThrownUp = 5.5f;
        private const int Lie = 60;

        /// <summary>Ticks it takes to fade once nobody is coming for it.</summary>
        private const int FadeTicks = 30;

        private static readonly Color Steel = new Color(214, 226, 240);
        private static readonly Color Soot = new Color(12, 6, 8);
        private static readonly Color BurnRed = new Color(206, 22, 28);

        private static Asset<Texture2D> picture;

        // ai[0] his whoAmI · ai[1] 0 in the air, 1 in the ground
        // localAI[0] 1 once it has hit this machine's player

        /// <summary>Whether this one is in the ground.</summary>
        public static bool InGround(Projectile projectile) => projectile.ai[1] == 1f;

        /// <summary>Where this one's grip is: back along it from its point.</summary>
        public static Vector2 GripOf(Projectile projectile)
        {
            Vector2 along = projectile.rotation.ToRotationVector2();
            return projectile.Center - along * FoighRig.GripToTip;
        }

        public override void Load()
        {
            if (!Main.dedServ)
                picture = ModContent.Request<Texture2D>(Texture);
        }

        public override void Unload()
        {
            picture = null;
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
            Projectile.timeLeft = 3600;
            Projectile.netImportant = true;
        }

        /// <summary>In the ground, behind the tiles, so the part of it in the ground is in the ground.</summary>
        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles,
            List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCsAndTiles.Add(index);
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
            // Nobody coming for it: it goes, on every machine by itself.
            Foigh foigh = Owner();

            if (foigh == null || !foigh.WantsSword)
            {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 255 / FadeTicks + 1);

                if (Projectile.alpha >= 255)
                {
                    Projectile.Kill();
                    return;
                }
            }

            bool planted = InGround(Projectile);

            // Only what is in the ground is drawn behind the tiles; in the air it is over them, outline and all.
            Projectile.hide = planted;

            if (planted)
            {
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + Gravity, MaxFall);
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Nothing turns it aside, and it throws whoever it goes through: it says so.
            Projectile.PAD().CCBypass = true;

            Lighting.AddLight(Projectile.Center, 0.2f, 0.2f, 0.22f);

            if (!Main.dedServ && Main.rand.NextBool())
            {
                Vector2 behind = -Projectile.velocity.SafeNormalize(Vector2.UnitY);

                PADParticleHandler.SpawnParticle(new WindStreak(Projectile.Center + behind * Main.rand.NextFloat(10f, Blade),
                    behind * Main.rand.NextFloat(1f, 3f), Main.rand.NextFloat(30f, 60f), 5f, Main.rand.Next(7, 12)));
            }

            // Burning, it leaves what it burns with along its way.
            if (!Main.dedServ && foigh != null && foigh.Burning)
            {
                Vector2 behind = -Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 at = Projectile.Center + behind * Main.rand.NextFloat(6f, Blade);

                PADParticleHandler.SpawnParticle(new BurnSmoke(at, behind * Main.rand.NextFloat(0.5f, 2f), Soot, BurnRed, Main.rand.Next(26, 44),
                    Main.rand.NextFloat(8f, 14f), Main.rand.NextFloat(22f, 34f), Main.rand.NextFloat(0.75f, 1f)));

                if (Main.rand.NextBool())
                    PADParticleHandler.SpawnParticle(new BloodFume(at, behind * Main.rand.NextFloat(0.3f, 1f), Main.rand.NextFloat(0.06f, 0.12f), Main.rand.Next(14, 24)));

                Lighting.AddLight(Projectile.Center, 0.5f, 0.06f, 0.05f);
            }

            Strike();
        }

        /// <summary>Its blade against this machine's own player: the sword's damage through armour, and thrown down for a second. Once.</summary>
        private void Strike()
        {
            if (Main.dedServ || Projectile.localAI[0] != 0f || Projectile.alpha > 0)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            Vector2 along = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 point = Projectile.Center;
            float where = 0f;

            // The way it came this tick as well: it is fast.
            if (!Collision.CheckAABBvLineCollision(me.position, me.Size, point - along * (Blade + Projectile.velocity.Length()), point + along * 4f, BladeWidth, ref where))
                return;

            if (me.immune)
                return;

            Foigh foigh = Owner();

            if (foigh == null)
                return;

            int damage = Main.DamageVar(foigh.SwordDamage(), -me.luck);
            int direction = Projectile.velocity.X >= 0f ? 1 : -1;

            Projectile.localAI[0] = 1f;

            // A blow that killed them throws nothing.
            if (!Foigh.Pierce(me, "FoighSwordThrown", damage, direction, 0f) || me.dead)
                return;

            // In his third phase it is his sword still, and leaves what his sword leaves.
            if (foigh.Phase >= 3)
                UltraBleeding.Wound(me);

            me.GetModPlayer<RagdollPlayer>().Throw(new Vector2(direction * ThrownAlong, -ThrownUp), RagdollStyle.Spin, Lie);

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.7f, Volume = 1f }, me.Center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.4f, Volume = 0.7f }, me.Center);
            me.PIS().BumpScreenShake(8f, 14);

            BloodDroplet.SpawnBurst(me.Center, along, 12, 36f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!InGround(Projectile))
                Plant(oldVelocity);

            return false;
        }

        /// <summary>Into the ground, point first, at the angle it came in at, and there it stays.</summary>
        private void Plant(Vector2 oldVelocity)
        {
            Vector2 along = oldVelocity.SafeNormalize(Vector2.UnitY);

            Projectile.ai[1] = 1f;
            Projectile.tileCollide = false;
            Projectile.rotation = oldVelocity.ToRotation();
            Projectile.position += along * Embed;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;

            if (Main.dedServ)
                return;

            Vector2 at = Projectile.Center - along * Embed;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.3f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.6f, Volume = 0.8f }, at);

            SparkShard.Spray(at, Steel, 10, 6f, 26f, 16, falls: true);
            GroundWave.Slam(at, 8f, 4, 5f, 22);

            if (Vector2.Distance(Main.LocalPlayer.Center, at) < 1000f)
                Main.LocalPlayer.PIS().BumpScreenShake(6f, 12);
        }

        /// <summary>The sword as it lies: its grip back along it from the point, the clean edge up.</summary>
        private FoighRig.Part Lying()
        {
            float blade = Projectile.rotation;

            // The clean edge on the upper side of it, whichever way it flies.
            return FoighRig.LooseSword(GripOf(Projectile), blade, Math.Cos(blade) < 0.0);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (picture == null || !picture.IsLoaded)
                return false;

            FoighRig.Part part = Lying();

            // Lit where the hilt is, not where the point is (in the ground, in the dark).
            Color light = Projectile.GetAlpha(Lighting.GetColor(part.At.ToTileCoordinates()));

            // The side of it he has in his hand: the sharp one from his second phase, bloodied in his third.
            Foigh his = Owner();
            Texture2D sword = Foigh.SwordPicture(his?.Sharp ?? false, his?.Bloodied ?? false) ?? picture.Value;

            Main.EntitySpriteDraw(sword, part.At - Main.screenPosition, null, light, part.Rotation, part.Origin, 1f,
                part.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);

            return false;
        }

        /// <summary>The crowd-control tell's outline, of the sword as it is drawn.</summary>
        public void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (picture == null || !picture.IsLoaded)
                return;

            FoighRig.Part part = Lying();

            CCBypass.Outline(spriteBatch, picture.Value, null, part.At - screenPos, part.Rotation, part.Origin, 1f,
                part.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
        }
    }
}
