using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A small ray of green light with a long tail, running round a player.
    ///
    /// Two kinds. Orbiting: one of a NurtureShield's, on its own tilted ring inside the rim,
    /// some one way round and some the other, lasting as long as the shield and as strong as
    /// it is. Gathering: one of those drawn in while Forest, Nurture Me is said, starting well
    /// out and spiralling in to the player until it is gone.
    ///
    /// The tail is kept as offsets from the centre rather than places in the world, so it
    /// stays a curve round the player however fast they run. Each ring is a circle squashed
    /// and turned, so it reads as a hoop seen at an angle, and a mote on the near side of its
    /// hoop is brighter than on the far side.
    /// </summary>
    public class NurtureMote : PADParticle
    {
        // The head's glow. The tail is drawn with vanilla's white pixel.
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";
        public override bool UseCustomDraw => true;
        public override bool UseAdditiveBlend => true;
        public override bool Important => true;

        // An orbiting mote lives exactly as long as its shield; a gathering one has a lifetime.
        public override bool SetLifetime => shield == null;

        private readonly NurtureShield shield;
        private readonly int owner;
        private readonly Vector2[] tail;
        private int filled;

        private float angle;
        private readonly float spin;
        private readonly float squash;
        private readonly float plane;
        private readonly float reach;
        private readonly float width;
        private readonly Color head;
        private readonly Color end;

        /// <summary>An orbiting mote, the index-th of its shield's, so they spread round it.</summary>
        public NurtureMote(NurtureShield shield, int index)
        {
            this.shield = shield;
            owner = shield.Owner;
            tail = new Vector2[26];

            angle = MathHelper.TwoPi * index / NurtureShield.Motes + Main.rand.NextFloat(-0.4f, 0.4f);
            spin = Main.rand.NextFloat(0.045f, 0.085f) * (index % 2 == 0 ? 1f : -1f);
            squash = Main.rand.NextFloat(0.25f, 0.95f);
            plane = MathHelper.Pi * index / NurtureShield.Motes + Main.rand.NextFloat(-0.25f, 0.25f);
            reach = Main.rand.NextFloat(0.72f, 0.98f);
            width = Main.rand.NextFloat(1f, 1.5f);
            head = NurtureShield.Pale;
            end = NurtureShield.Deep;

            Position = shield.Position;
            Color = Color.White;
        }

        /// <summary>A gathering mote: starts <paramref name="startReach"/> out and spirals in.</summary>
        public NurtureMote(int owner, float startAngle, float startReach, int lifetime)
        {
            shield = null;
            this.owner = owner;
            tail = new Vector2[12];

            angle = startAngle;
            spin = Main.rand.NextFloat(0.09f, 0.14f) * (Main.rand.NextBool() ? 1f : -1f);
            squash = Main.rand.NextFloat(0.55f, 1f);
            plane = Main.rand.NextFloat(MathHelper.TwoPi);
            reach = startReach;
            width = Main.rand.NextFloat(1.2f, 2f);
            head = NurtureShield.Light;
            end = NurtureShield.Deep;

            Lifetime = lifetime;
            Player player = Main.player[owner];
            Position = player.MountedCenter;
            Color = Color.White;
        }

        /// <summary>How strong it is now, 0 to 1.</summary>
        private float Strength => shield != null
            ? shield.Strength
            : (float)Math.Sin(LifetimeCompletion * MathHelper.Pi);

        private Vector2 Centre
        {
            get
            {
                if (shield != null)
                    return shield.Position;

                Player player = Main.player[owner];
                return player.MountedCenter + new Vector2(0f, player.gfxOffY);
            }
        }

        public override void Update()
        {
            if (shield != null && shield.Gone)
            {
                Kill();
                return;
            }

            angle += spin;

            float radius;

            if (shield != null)
            {
                radius = shield.Radius * reach;
            }
            else
            {
                // Faster the closer it gets.
                float t = LifetimeCompletion;
                radius = reach * (1f - t * t);
            }

            Vector2 local = new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius * squash).RotatedBy(plane);

            for (int i = tail.Length - 1; i > 0; i--)
                tail[i] = tail[i - 1];

            tail[0] = local;
            filled = Math.Min(filled + 1, tail.Length);

            Position = Centre + local;
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            float a = Strength;

            if (a <= 0f || filled < 2)
                return;

            // The near side of its hoop brighter than the far.
            a *= 0.6f + 0.4f * (0.5f + 0.5f * (float)Math.Sin(angle));

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new Rectangle(0, 0, 1, 1);
            Vector2 centre = Centre - Main.screenPosition;

            for (int i = 0; i < filled - 1; i++)
            {
                Vector2 from = centre + tail[i];
                Vector2 to = centre + tail[i + 1];
                Vector2 run = to - from;
                float length = run.Length();

                if (length < 0.01f)
                    continue;

                float f = i / (float)(tail.Length - 1);
                Color colour = Color.Lerp(head, end, f) * (a * (1f - f) * (1f - f) * 0.75f);

                spriteBatch.Draw(pixel, from, one, colour, run.ToRotation(), new Vector2(0f, 0.5f),
                    new Vector2(length + 0.6f, width * (1f - 0.7f * f)), SpriteEffects.None, 0f);
            }

            Texture2D bloom = PADParticleHandler.GetTexture(Type);

            if (bloom is null)
                return;

            Vector2 at = centre + tail[0];
            Vector2 middle = bloom.Size() * 0.5f;
            spriteBatch.Draw(bloom, at, null, head * (0.65f * a), 0f, middle, 0.035f, SpriteEffects.None, 0f);
            spriteBatch.Draw(bloom, at, null, Color.White * (0.5f * a), 0f, middle, 0.015f, SpriteEffects.None, 0f);
        }
    }
}
