using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// Visual Intoxication: by request, what whoever is teleported out of the Empress's arena gets,
    /// "350HP per second". It lasts as long as they are out (EmpressArenaPlayer refreshes it every tick
    /// they are), and no life comes back by itself under it. What lies outside her is too much to look
    /// at: the screen swims and goes to her colours (VisualIntoxicationScreen).
    /// </summary>
    public class VisualIntoxication : ModBuff
    {
        /// <summary>Life lost every second.</summary>
        public const int LifePerSecond = 350;

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
            Terraria.ID.BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }
    }

    /// <summary>The life it takes, and what is said of one who dies of it.</summary>
    public class VisualIntoxicationPlayer : ModPlayer
    {
        /// <summary>lifeRegen counts half points of life a second.</summary>
        public override void UpdateBadLifeRegen()
        {
            if (!Player.HasBuff(ModContent.BuffType<VisualIntoxication>()))
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= VisualIntoxication.LifePerSecond * 2;
        }

        /// <summary>
        /// Life regen that runs out with nothing more particular to blame reports the death as burning
        /// (vanilla's "other" reason 8). Out past her border, it was what lies outside.
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (damageSource.SourceOtherIndex == 8 && Player.HasBuff(ModContent.BuffType<VisualIntoxication>()))
            {
                damageSource = PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey("Mods.PisAndDag.DeathText.VisualIntoxication", Player.name));
            }

            return true;
        }
    }

    /// <summary>
    /// What it does to the eyes of the one who has it: the screen swims (the camera swaying on slow
    /// waves), the picture washes in her colours going round, light blooms at its edges, and her stars
    /// drift across it. Eased in and out on its own clock. Only for this machine's own player.
    /// </summary>
    public class VisualIntoxicationScreen : ModSystem
    {
        private static float strength;

        /// <summary>Its stars: where (shares of the screen), how fast they drift and turn, and their phase.</summary>
        private static readonly Vector4[] Motes = MakeMotes(18);

        private static Vector4[] MakeMotes(int count)
        {
            var random = new Random(350);
            var motes = new Vector4[count];

            for (int i = 0; i < count; i++)
                motes[i] = new Vector4((float)random.NextDouble(), (float)random.NextDouble(), 0.3f + (float)random.NextDouble(), (float)random.NextDouble() * 10f);

            return motes;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;
            bool has = me != null && me.active && !me.dead && me.HasBuff(ModContent.BuffType<VisualIntoxication>());

            strength = MathHelper.Clamp(strength + (has ? 1f / 20f : -1f / 40f), 0f, 1f);
        }

        public override void OnWorldUnload()
        {
            strength = 0f;
        }

        /// <summary>The swim: slow waves on both axes, never a jump.</summary>
        public override void ModifyScreenPosition()
        {
            if (strength <= 0f || Main.gameMenu)
                return;

            float t = (float)Main.timeForVisualEffects / 60f;
            float s = strength * strength;

            Main.screenPosition += new Vector2(
                (float)Math.Sin(t * 1.7f) * 14f + (float)Math.Sin(t * 3.1f + 1f) * 5f,
                (float)Math.Sin(t * 1.3f + 2f) * 10f + (float)Math.Sin(t * 2.7f) * 4f) * s;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (strength <= 0f)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("PisAndDag: Visual Intoxication", () =>
            {
                Draw(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.None));
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            float s = strength * strength * (3f - 2f * strength);
            float t = (float)Main.timeForVisualEffects / 60f;
            int w = Main.screenWidth;
            int h = Main.screenHeight;

            // The wash, in her colours going round.
            Color wash = EmpressArt.Additive(EmpressArt.Prism(t * 0.15f), 0.16f * s);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, w, h), wash);

            // Light blooming in at the edges, each corner its own colour.
            if (EmpressArt.Ready(EmpressArt.Soft))
            {
                Texture2D soft = EmpressArt.Soft.Value;
                Vector2[] corners = { Vector2.Zero, new Vector2(w, 0f), new Vector2(0f, h), new Vector2(w, h) };

                for (int i = 0; i < corners.Length; i++)
                {
                    float breathe = 0.75f + 0.25f * (float)Math.Sin(t * 1.6f + i * 1.7f);
                    Color c = EmpressArt.Additive(EmpressArt.Prism(i * 0.25f + t * 0.1f), 0.45f * s * breathe);

                    spriteBatch.Draw(soft, corners[i], null, c, 0f, soft.Size() * 0.5f, Math.Max(w, h) * 1.1f / soft.Width * breathe, SpriteEffects.None, 0f);
                }
            }

            // Her stars, drifting across it, turning.
            if (EmpressArt.Ready(EmpressArt.Star))
            {
                Texture2D star = EmpressArt.Star.Value;

                foreach (Vector4 m in Motes)
                {
                    float x = (m.X + t * 0.02f * m.Z) % 1f * w;
                    float y = (m.Y + (float)Math.Sin(t * 0.4f + m.W) * 0.05f) * h;
                    float twinkle = 0.5f + 0.5f * (float)Math.Sin(t * 2f * m.Z + m.W);
                    Color c = EmpressArt.Additive(EmpressArt.Prism(m.W * 0.1f + t * 0.05f), 0.7f * s * twinkle);

                    spriteBatch.Draw(star, new Vector2(x, y), null, c, t * m.Z, star.Size() * 0.5f, 0.4f + 0.4f * m.Z, SpriteEffects.None, 0f);
                }
            }
        }
    }
}
