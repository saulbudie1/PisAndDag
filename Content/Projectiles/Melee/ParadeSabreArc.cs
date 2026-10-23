using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The arc a Parade Sabre leaves as it is swung: a ribbon of added light from the forte of the
    /// blade out to its point, along the way the point has come, brightest at the blade and gone
    /// behind it. Gold while it is gilded (ai[0] 0), cold steel once it is bare (ai[0] 1).
    ///
    /// The sabre is the game's own swung sword; this reads where the game has put it this tick
    /// (Player.itemLocation and itemRotation: the sprite's corner by the hilt, turned about it) and
    /// works out where the point and the forte are, a few times between last tick and this so the
    /// ribbon bends instead of breaking. Spent when the swing is; it hurts nothing.
    /// </summary>
    public class ParadeSabreArc : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Graphics/Smear";

        /// <summary>Places along the swing kept: the ribbon's length.</summary>
        private const int Keep = 16;

        /// <summary>Of the way from the hilt's corner to the point, where the ribbon starts.</summary>
        private const float Forte = 0.42f;

        /// <summary>The point's texel, in from the sprite's corner, and the blade's own corner by the hilt.</summary>
        private const float PointIn = 3f;

        private static readonly Color GoldCore = new Color(255, 238, 178);
        private static readonly Color GoldEdge = new Color(232, 168, 64);
        private static readonly Color SteelCore = new Color(236, 244, 255);
        private static readonly Color SteelEdge = new Color(132, 152, 186);

        private static Asset<Texture2D> line;

        private readonly Vector2[] tips = new Vector2[Keep];
        private readonly Vector2[] fortes = new Vector2[Keep];
        private int count;

        private float lastRotation = float.NaN;
        private int fading;

        /// <summary>The swing's count of ticks left, last tick: when it goes up, a new swing has begun and this arc is the last one's.</summary>
        private int lastAnimation = -1;

        private bool Bare => Projectile.ai[0] == 1f;

        public override void Load()
        {
            if (!Main.dedServ)
                line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            line = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            Projectile.Center = player.Center;

            bool swinging = player.active && !player.dead && player.itemAnimation > 0 && player.HeldItem.ModItem is ParadeSabreBase;

            // Swung again at once (the sabre swings for as long as it is held down): the new swing
            // has its own arc, and this one, once it has stopped, does not take up again.
            if (swinging && lastAnimation >= 0 && player.itemAnimation > lastAnimation)
                swinging = false;

            if (swinging)
                lastAnimation = player.itemAnimation;

            if (!swinging || fading > 0)
            {
                if (++fading > 8)
                    Projectile.Kill();

                return;
            }

            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 10);

            if (Main.dedServ)
                return;

            float rotation = player.itemRotation;
            float from = float.IsNaN(lastRotation) ? rotation : lastRotation;
            lastRotation = rotation;

            // Three places between last tick and this: it turns a long way in one at the fast part.
            for (int k = 1; k <= 3; k++)
                Push(player, MathHelper.Lerp(from, rotation, k / 3f));
        }

        /// <summary>Where the point and the forte are with the sprite turned to <paramref name="rotation"/> about the hilt's corner.</summary>
        private void Push(Player player, float rotation)
        {
            Texture2D texture = TextureAssets.Item[player.HeldItem.type].Value;
            float scale = player.GetAdjustedItemScale(player.HeldItem);

            // The sprite is drawn from its corner by the hilt (bottom left, facing right), the point at
            // the far corner; mirrored with the player, and over under reversed gravity.
            Vector2 point = new Vector2((texture.Width - PointIn) * player.direction, -(texture.Height - PointIn) * player.gravDir) * scale;
            Vector2 tip = player.itemLocation + point.RotatedBy(rotation);
            Vector2 forte = player.itemLocation + (point * Forte).RotatedBy(rotation);

            for (int i = Keep - 1; i > 0; i--)
            {
                tips[i] = tips[i - 1];
                fortes[i] = fortes[i - 1];
            }

            tips[0] = tip;
            fortes[0] = forte;
            count = Math.Min(Keep, count + 1);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (count < 2 || line == null)
                return false;

            float fade = 1f - fading / 8f;
            Color core = Bare ? SteelCore : GoldCore;
            Color edge = Bare ? SteelEdge : GoldEdge;
            Texture2D smear = TextureAssets.Projectile[Type].Value;

            Main.spriteBatch.End();

            // The band: from the forte (faint) to the point (strong), fading along the way back.
            Strip.Begin();
            for (int i = 0; i < count; i++)
            {
                float k = 1f - i / (float)(count - 1);
                float strength = k * k * fade * (Bare ? 0.45f : 0.55f);

                Color inner = edge * (strength * 0.25f);
                Color outer = Color.Lerp(edge, core, 0.5f) * strength;
                inner.A = 0;
                outer.A = 0;

                Strip.Pair(fortes[i], tips[i], inner, outer, i / (float)(Keep - 1));
            }
            Strip.Draw(smear);

            // A bright thread along the point's own path.
            Strip.Begin();
            for (int i = 0; i < count; i++)
            {
                float k = 1f - i / (float)(count - 1);
                Vector2 along = tips[i] - fortes[i];
                Vector2 inward = along.SafeNormalize(Vector2.Zero) * 5f;

                Color hot = core * (k * k * k * fade * 0.9f);
                hot.A = 0;

                Strip.Pair(tips[i] - inward, tips[i] + inward * 0.4f, hot, hot, i / (float)(Keep - 1));
            }
            Strip.Draw(line.Value);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }
    }
}
