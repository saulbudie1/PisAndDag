using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A lacewing of light flying off (the Empress's mirage, its copies coming apart; her death bursts into real butterflies,
    /// EmpressButterflies). The game's own Prismatic Lacewing (NPC 661), read at run time, never copied: its wings beating,
    /// tinted one of her colours, a ring of her rainbow round it as the game rings its own, a soft light behind.
    ///
    /// Thrown out fast the way it is given, it slows to a flutter, wandering: turning a little this way and that, bobbing
    /// as it beats its wings, now and then a mote of light off it; it fades and is gone. Only seen, never sent.
    /// </summary>
    public class LacewingFlight : PADParticle
    {
        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override bool SetLifetime => true;
        public override bool UseCustomDraw => true;

        /// <summary>Few, and the end of something: never left out for want of room.</summary>
        public override bool Important => true;

        private readonly float cruise;
        private readonly float wander;
        private readonly float bob;
        private readonly float size;
        private float turn;
        private int facing = 1;

        /// <param name="velocity">Its first speed and way: it is thrown out so, and slows to a flutter.</param>
        /// <param name="colour">Its tint, one of hers.</param>
        public LacewingFlight(Vector2 position, Vector2 velocity, Color colour, int lifetime, float scale = 1.3f)
        {
            Position = position;
            Velocity = velocity;
            Color = colour;
            Lifetime = Math.Max(30, lifetime);
            Scale = scale;
            size = scale;

            cruise = Main.rand.NextFloat(2.2f, 3.4f);
            wander = Main.rand.NextFloat(0.012f, 0.03f) * (Main.rand.NextBool() ? 1f : -1f);
            bob = Main.rand.NextFloat(MathHelper.TwoPi);
            turn = 0f;
            Variant = Main.rand.Next(4);
        }

        public override void Update()
        {
            // Slowed from its throw to a flutter, keeping its way but wandering off it.
            float speed = Velocity.Length();

            if (speed > cruise)
                Velocity *= Math.Max(cruise / speed, 0.94f);

            turn = MathHelper.Lerp(turn, wander * (float)Math.Sin(Time * 0.05f + bob) * 2f, 0.1f);
            Velocity = Velocity.RotatedBy(turn);

            // A bob as it beats its wings.
            Position.Y += (float)Math.Sin(Time * 0.32f + bob) * 0.7f;

            if (Math.Abs(Velocity.X) > 0.15f)
                facing = Velocity.X > 0f ? 1 : -1;

            Rotation = MathHelper.Clamp(Velocity.X * 0.09f, -0.45f, 0.45f);

            if (Time % 9 == 0 && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Position, DustID.RainbowMk2, -Velocity * 0.15f, 0, Color, 0.7f);
                dust.noGravity = true;
                dust.fadeIn = 0.8f;
            }

            Lighting.AddLight(Position, Color.R / 255f * 0.35f, Color.G / 255f * 0.35f, Color.B / 255f * 0.35f);
        }

        public override void CustomDraw(SpriteBatch spriteBatch)
        {
            Main.instance.LoadNPC(NPCID.EmpressButterfly);

            Texture2D sheet = TextureAssets.Npc[NPCID.EmpressButterfly].Value;
            Texture2D soft = PADParticleHandler.GetTexture(Type);

            if (sheet == null)
                return;

            // In fast, out slow.
            float p = LifetimeCompletion;
            float alpha = Math.Min(1f, Time / 6f) * (p < 0.7f ? 1f : 1f - (p - 0.7f) / 0.3f);

            if (alpha <= 0.01f)
                return;

            int frames = Math.Max(1, Main.npcFrameCount[NPCID.EmpressButterfly]);
            int frame = (Time / 3 + Variant) % frames;
            Rectangle source = sheet.Frame(1, frames, 0, frame);
            Vector2 at = Position - Main.screenPosition;
            Vector2 origin = source.Size() * 0.5f;
            SpriteEffects effects = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float scale = size * (1f - 0.25f * p);

            // A soft light behind it, in its colour.
            if (soft != null)
            {
                Color glow = Color * (0.45f * alpha);
                glow.A = 0;
                spriteBatch.Draw(soft, at, null, glow, 0f, soft.Size() * 0.5f, 26f * scale / (soft.Width * 0.5f), SpriteEffects.None, 0f);
            }

            // A ring of her rainbow round it, turning, as the game rings its own lacewing (added light).
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * MathHelper.TwoPi + Time * 0.08f;
                Color ring = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.5f + i / 4f) % 1f, 1f, 0.6f) * (0.5f * alpha);
                ring.A = 0;
                spriteBatch.Draw(sheet, at + a.ToRotationVector2() * 2.5f, source, ring, Rotation, origin, scale, effects, 0f);
            }

            // Itself, in its colour, and white light through it.
            Color body = Color.Lerp(Color, Color.White, 0.45f) * alpha;
            spriteBatch.Draw(sheet, at, source, body, Rotation, origin, scale, effects, 0f);

            Color shine = Color.White * (0.25f * alpha);
            shine.A = 0;
            spriteBatch.Draw(sheet, at, source, shine, Rotation, origin, scale, effects, 0f);
        }
    }
}
