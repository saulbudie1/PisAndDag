using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// The staff held to the mouth while Cold Call is cast. A prop: it deals nothing and fires
    /// nothing. The icicles leave from <see cref="HeadOf"/>, which is where the staff's head is
    /// held.
    ///
    /// This projectile is the pose and its timing, and nothing is drawn from it (it is hidden).
    /// It turns the front arm across the chest to hold the shaft (<see cref="ArmRotation"/>)
    /// and marks the player as holding (<see cref="ColdCallHoldPlayer"/>).
    /// The staff itself is drawn by <see cref="ColdCallStaffLayer"/>, among the player's own
    /// layers: over the body and head, under the front arm, with vanilla's drawing Of The held
    /// staff hidden meanwhile.
    ///
    /// Not the player's heldProj on purpose: vanilla draws a heldProj from inside its held-item
    /// layer, which is the layer hidden here, so a heldProj would not be drawn at all.
    ///
    /// ai[0] is the ticks left: every Cold Call cast sets it to the length of that cast, the
    /// wait after it, and two ticks more (ColdCall.Cast), so the staff stays up from cast to
    /// cast without blinking, and goes down after the last one even if something else is cast
    /// next. ai[1] is the light at the staff's head: 0 Cold Call's ice, 1 Dancing Sand's warm
    /// dust (<see cref="Keep"/>, for any spell held at the mouth).
    /// </summary>
    public class ColdCallHold : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Items/Mage/Staves/TreeBranchStaff";

        /// <summary>
        /// Where the staff's head is held, from the player's centre, facing right, the right way
        /// up. Measured off the player's sprite: the mouth is about (6, -5.5), the front Of The
        /// face 7 to 9 out; this is just in front Of The mouth.
        /// </summary>
        public static readonly Vector2 Mouth = new Vector2(10f, -6f);

        /// <summary>
        /// The staff's head on its own sprite, where the branches part, as a fraction Of The
        /// sprite: (28, 12) of TreeBranchStaff.png's 40 by 40.
        /// </summary>
        public static readonly Vector2 HeadOnSprite = new Vector2(0.7f, 0.3f);

        /// <summary>The shoulder the front arm turns about, facing right: Player.GetFrontHandPosition's.</summary>
        private static readonly Vector2 Shoulder = new Vector2(-4f, -2f);

        /// <summary>Where the staff's head is for this player, in the world: where the icicles come from.</summary>
        public static Vector2 HeadOf(Player player) =>
            player.RotatedRelativePoint(player.MountedCenter, true, true)
            + new Vector2(Mouth.X * player.direction, Mouth.Y * player.gravDir);

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 3;
            Projectile.netImportant = true;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        private static readonly Color SandLight = new Color(214, 170, 100);

        /// <summary>
        /// The staff to this player's mouth for so many ticks more: the hold that is up given the
        /// new count, or a new one. <paramref name="light"/> is ai[1] (the light at its head).
        /// What ColdCall.Cast does by hand, for any other spell held at the mouth.
        /// </summary>
        public static void Keep(Player player, IEntitySource source, float ticks, float light)
        {
            int hold = ModContent.ProjectileType<ColdCallHold>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == player.whoAmI && p.type == hold)
                {
                    p.ai[0] = ticks;
                    p.ai[1] = light;

                    // Not netUpdate: this is no part of its own update, and the game forgets
                    // that one before it looks (Projectile.Update). This one it keeps.
                    p.netUpdate2 = true;
                    return;
                }
            }

            Projectile.NewProjectile(source, HeadOf(player), Vector2.Zero, hold, 0, 0f, player.whoAmI, ticks, light);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem?.ModItem is not Catalyst)
            {
                Projectile.Kill();
                return;
            }

            // Counts down the cast. The owner puts it away when the count runs out; everyone
            // else waits to be told, so a late refresh from the next cast does not make it
            // blink, and gives up on their own only if they are never told.
            Projectile.ai[0]--;

            if (Projectile.ai[0] < 0f && (Projectile.owner == Main.myPlayer || Projectile.ai[0] < -30f))
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = HeadOf(player);
            Projectile.velocity = Vector2.Zero;

            player.GetModPlayer<ColdCallHoldPlayer>().Holding = true;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, ArmRotation(player));

            Lighting.AddLight(Projectile.Center, (Projectile.ai[1] == 1f ? SandLight : ColdCallIcicle.Ice).ToVector3() * 0.3f);
        }

        /// <summary>
        /// The front arm, bent across the chest, the hand on the shaft where it passes nearest
        /// the shoulder. Worked out facing right and the right way up, then mirrored: the arm is
        /// drawn with the player's own flips, so under reversed gravity the same pose is the
        /// negated angle.
        /// </summary>
        private static float ArmRotation(Player player)
        {
            int dir = player.direction;
            Vector2 shoulder = new Vector2(Shoulder.X * dir, Shoulder.Y);
            Vector2 head = new Vector2(Mouth.X * dir, Mouth.Y);

            // Down the shaft, toward its foot: the sprite's diagonal.
            Vector2 down = new Vector2(-dir, 1f) / (float)Math.Sqrt(2.0);
            Vector2 grip = head + down * Vector2.Dot(shoulder - head, down);

            // A quarter-stretched arm puts the hand 6 along it and 3 across it, toward the
            // front: aim short Of The grip by that much, so the hand lands on it.
            float angle = (grip - shoulder).ToRotation() - dir * (float)Math.Atan2(3.0, 6.0);

            return (angle - MathHelper.PiOver2) * player.gravDir;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// Whether the player is holding the staff to the mouth this tick. Cleared every tick and
    /// set again by ColdCallHold while it is up; while it is set, vanilla's held staff is hidden
    /// and ColdCallStaffLayer draws it instead.
    /// </summary>
    public class ColdCallHoldPlayer : ModPlayer
    {
        public bool Holding;

        public override void ResetEffects() => Holding = false;

        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (Holding)
                PlayerDrawLayers.HeldItem.Hide();
        }
    }

    /// <summary>
    /// The staff at the mouth, drawn in the place of vanilla's held item: after the body and
    /// head, before the front arm, so the hand closes over the shaft. Unturned, since the
    /// sprite already lies on the diagonal and pixel art keeps its pixels only at right angles:
    /// facing right, its head is at the mouth and its foot on the ground behind. Whichever staff
    /// is in the hand, pinned by <see cref="ColdCallHold.HeadOnSprite"/>.
    /// </summary>
    public class ColdCallStaffLayer : PlayerDrawLayer
    {
        // Vanilla's own slot for an item held behind the front arm. Not relative to HeldItem:
        // that layer moves between three places depending on how the item is held.
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<ColdCallHoldPlayer>().Holding;

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;
            Item item = drawInfo.heldItem;

            if (item == null || item.IsAir)
                return;

            Main.instance.LoadItem(item.type);
            Texture2D staff = TextureAssets.Item[item.type].Value;

            Vector2 origin = new Vector2(staff.Width * ColdCallHold.HeadOnSprite.X, staff.Height * ColdCallHold.HeadOnSprite.Y);
            SpriteEffects effects = SpriteEffects.None;

            if (player.direction < 0)
            {
                effects |= SpriteEffects.FlipHorizontally;
                origin.X = staff.Width - origin.X;
            }

            if (player.gravDir < 0f)
            {
                effects |= SpriteEffects.FlipVertically;
                origin.Y = staff.Height - origin.Y;
            }

            Vector2 head = ColdCallHold.HeadOf(player);
            Color light = Lighting.GetColor(head.ToTileCoordinates());

            // Whole pixels, as the player is drawn, so the staff does not swim against the body.
            drawInfo.DrawDataCache.Add(new DrawData(staff, (head - Main.screenPosition).Floor(), null,
                item.GetAlpha(light), 0f, origin, 1f, effects));
        }
    }
}
