using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The calling itself: a patch of ground that fumes red and sends hands up through
    /// it, three at a time, for <see cref="Duration"/>. It sits where the cursor's
    /// ground was when the cast went off.
    ///
    /// Held past <see cref="HoldTicks"/>, it stops being a place and becomes a thing
    /// on a leash: the fume slides along the ground under the cursor, the hands keep
    /// coming, and it costs <see cref="DrainPerSecond"/> mana a second for as long as
    /// the button is down. On the leash its clock stops. Let go, or run dry, and it
    /// stays where it got to and goes on for what was left of its time at the two
    /// second mark.
    ///
    /// ai[0] is the tick. ai[1] is 0 before the leash, 1 on it, 2 after it (it does
    /// not come back). Position is the ground point; the projectile has no hitbox of
    /// its own, the hands do the hitting.
    /// </summary>
    public class CourtOfHandsRite : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        /// <summary>Five seconds.</summary>
        public const int Duration = 300;

        /// <summary>Held this long, it follows the cursor.</summary>
        public const int HoldTicks = 120;

        /// <summary>Mana a second while following.</summary>
        public const float DrainPerSecond = 14f;

        /// <summary>Ticks between one three and the next.</summary>
        public const int SetInterval = 26;

        public const int HandsPerSet = 3;
        public const float HandSpread = 22f;
        public const float MaxLean = 0.3f;

        /// <summary>How far the ground point can be from the cursor before it will not be found, in tiles.</summary>
        public const int GroundSearch = 60;

        /// <summary>How fast the fume slides toward the cursor's ground when following, per tick.</summary>
        private const float FollowEase = 0.18f;

        /// <summary>How far below the ground line the fume starts, in pixels, so it comes up out of it.</summary>
        private const float FumeDrop = 2f;

        public static readonly Color FumeDark = new Color(0x68, 0x03, 0x03);
        public static readonly Color FumeMid = new Color(0xb1, 0x25, 0x16);
        public static readonly Color FumeBright = new Color(0xbd, 0x00, 0x26);

        private Player Owner => Main.player[Projectile.owner];

        private const float Unleashed = 0f;
        private const float Leashed = 1f;
        private const float LeashSpent = 2f;

        private int Tick => (int)Projectile.ai[0];
        private bool Following => Projectile.ai[1] == Leashed;

        private float manaDue;
        private bool released;

        /// <summary>Where it is going, on a click. Owner only; the position syncs.</summary>
        private Vector2? target;
        private bool wasClicking = true;

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Duration;
            Projectile.netImportant = true;
        }

        /// <summary>
        /// The ground under (or over) a point in the world: the top of the nearest
        /// solid tile in the column, looking down from open air and up from inside
        /// rock. False if there is none within reach.
        /// </summary>
        public static bool FindGround(Vector2 world, out Vector2 ground)
        {
            Point tile = world.ToTileCoordinates();
            ground = world;

            if (tile.X < 1 || tile.X >= Main.maxTilesX - 1)
                return false;

            int y = System.Math.Clamp(tile.Y, 1, Main.maxTilesY - 2);

            if (Solid(tile.X, y))
            {
                // Inside something: up until there is air, then the ground is the tile
                // just under it.
                for (int i = 0; i < GroundSearch && y > 1; i++, y--)
                {
                    if (!Solid(tile.X, y - 1))
                    {
                        ground = new Vector2(world.X, y * 16f);
                        return true;
                    }
                }

                return false;
            }

            for (int i = 0; i < GroundSearch && y < Main.maxTilesY - 1; i++, y++)
            {
                if (Standable(tile.X, y))
                {
                    ground = new Vector2(world.X, y * 16f);
                    return true;
                }
            }

            return false;
        }

        /// <summary>A block: solid through, slopes and half blocks included. What "inside" means.</summary>
        private static bool Solid(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        /// <summary>Anything with a top to stand on: a block or a platform. What the hands come up through.</summary>
        private static bool Standable(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.ai[0]++;
            Projectile.velocity = Vector2.Zero;

            if (Projectile.owner == Main.myPlayer)
                Drive(player);

            Lighting.AddLight(Projectile.Center, 0.7f, 0.06f, 0.06f);

            if (!Main.dedServ)
                Fume();

            if (Projectile.owner == Main.myPlayer && Tick % SetInterval == 1)
                CallHands();
        }

        /// <summary>Owner only: the hold, the leash, the drain, and the player's arm.</summary>
        private void Drive(Player player)
        {
            // Only while THIS incantation is the one up on the seal in the hand. Swap
            // to another and the court is on its own: no hold, no leash, no moving it.
            bool armed = player.HeldItem?.ModItem is Catalyst seal && seal.Selected(player) is Items.Mage.Incantation.CourtOfHands;
            bool holding = armed && player.channel;

            if (holding)
            {
                // The seal stays up for as long as the button is down.
                player.itemTime = 2;
                player.itemAnimation = 2;

                if (Tick > HoldTicks && Projectile.ai[1] == Unleashed)
                {
                    Projectile.ai[1] = Leashed;
                    Projectile.netUpdate = true;
                }
            }
            else if (!released)
            {
                released = true;

                Item item = player.HeldItem;

                if (item?.ModItem is Catalyst)
                {
                    player.itemTime = item.useTime;
                    player.itemAnimation = item.useAnimation;
                }
            }

            // A fresh click anywhere while it stands moves it there, leash or no leash,
            // for the cast's own mana again, and its five seconds start over.
            bool clicking = armed && player.controlUseItem && !player.mouseInterface;

            if (clicking && !wasClicking && FindGround(Main.MouseWorld, out Vector2 clicked))
            {
                // The item overload works the cost out itself, reductions and all.
                // The int one multiplies by the reduction AGAIN, so it was paying less
                // than the seal does.
                if (player.CheckMana(player.HeldItem, -1, true, false))
                {
                    target = clicked;
                    Projectile.timeLeft = Duration;
                    Projectile.netUpdate = true;
                }
            }

            wasClicking = clicking;

            if (target is Vector2 going)
            {
                Slide(going);

                if (Vector2.DistanceSquared(Projectile.Center, going) < 1f)
                    target = null;
            }

            if (!Following)
                return;

            if (!holding)
            {
                // Let go: it stays where it is and does not follow again.
                Projectile.ai[1] = LeashSpent;
                Projectile.netUpdate = true;
                return;
            }

            // On the leash the clock stops: it lasts as long as it is held and paid
            // for, and what was left of its time at the two second mark is what it has
            // when let go. Without this it ran out under the hand and the seal cast a
            // fresh one, which meant another two seconds before it would follow.
            Projectile.timeLeft = Duration - HoldTicks;

            // Slide along the ground toward the cursor's ground and pay for it by the
            // tick.
            if (FindGround(Main.MouseWorld, out Vector2 ground))
                Slide(ground);

            // Paid straight off the bar, a point at a time. Not through CheckMana: that
            // takes (int)(1 * manaCost), which with any mana cost reduction worn is
            // (int)(0.9) = 0, and the hands followed for nothing. The reduction is
            // applied here instead, to the rate.
            manaDue += DrainPerSecond / 60f * Math.Max(0f, player.manaCost);

            while (manaDue >= 1f)
            {
                manaDue -= 1f;

                if (player.statMana < 1)
                {
                    // Dry: the leash is cut, the hands are not. It finishes its time
                    // where it stands.
                    Projectile.ai[1] = LeashSpent;
                    Projectile.netUpdate = true;
                    return;
                }

                player.statMana--;
            }
        }

        /// <summary>Eases the ground point toward another along the ground.</summary>
        private void Slide(Vector2 to)
        {
            Vector2 was = Projectile.Center;
            Projectile.Center = Vector2.Lerp(was, to, FollowEase);

            if (Vector2.DistanceSquared(was, Projectile.Center) > 0.25f)
                Projectile.netUpdate = true;
        }

        /// <summary>Three hands, spread across the fume, each with its own lean and one of eight looks.</summary>
        private void CallHands()
        {
            // No more once there is not time left for them to go back down.
            if (Projectile.timeLeft < CourtHand.Life)
                return;

            for (int i = 0; i < HandsPerSet; i++)
            {
                float across = (i - (HandsPerSet - 1) * 0.5f) * HandSpread + Main.rand.NextFloat(-6f, 6f);
                float lean = Main.rand.NextFloat(-MaxLean, MaxLean);
                int look = Main.rand.Next(CourtHand.Looks);

                Vector2 at = new Vector2(Projectile.Center.X + across, Projectile.Center.Y);

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, Vector2.Zero,
                    ModContent.ProjectileType<CourtHand>(), Projectile.damage, Projectile.knockBack, Projectile.owner, lean, look);
            }

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.45f, Pitch = -0.6f, PitchVariance = 0.15f }, Projectile.Center);
        }

        /// <summary>The red haze off the ground, and now and then a pulse of light in it.</summary>
        private void Fume()
        {
            float wide = HandSpread * 1.4f;
            int count = Main.rand.NextBool(3) ? 3 : 2;

            for (int i = 0; i < count; i++)
            {
                Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-wide, wide), FumeDrop + Main.rand.NextFloat(-2f, 2f));
                Vector2 vel = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.3f));

                PADParticleHandler.SpawnParticle(new BloodFume(at, vel, Main.rand.NextFloat(0.09f, 0.15f), Main.rand.Next(34, 56)));
            }

            if (Tick % 4 == 0)
            {
                PADParticleHandler.SpawnParticle(new BloomGeneric(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-wide * 0.6f, wide * 0.6f), FumeDrop - 2f),
                    new Vector2(0f, -0.2f), FumeMid, Main.rand.NextFloat(0.13f, 0.21f), 14, lights: false));
            }

            if (Main.rand.NextBool(4))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + new Vector2(Main.rand.NextFloat(-wide, wide), 0f), DustID.Blood,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.5f)), 0, default, Main.rand.NextFloat(0.9f, 1.3f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D bloom = TextureAssets.Projectile[Type].Value;
            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 origin = bloom.Size() * 0.5f;

            // Two flattened blooms along the ground, breathing out of step, so the glow
            // is a lit patch rather than a ball. Fades in over the first ticks and out
            // over the last.
            float life = MathHelper.Clamp(Tick / 12f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float slow = 1f + 0.1f * (float)System.Math.Sin(Tick * 0.12f);
            float quick = 1f + 0.15f * (float)System.Math.Sin(Tick * 0.31f + 1f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(bloom, at, null, FumeDark * life, 0f, origin, new Vector2(0.55f, 0.16f) * slow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(bloom, at, null, FumeBright * (0.8f * life), 0f, origin, new Vector2(0.3f, 0.1f) * quick, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
