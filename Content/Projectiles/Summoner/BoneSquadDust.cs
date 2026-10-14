using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// The game's own dust, drawn as the Bone Squad is drawn (BoneSquadLook). Each is made as the
    /// game makes it, by the game's own numbers, and then becomes one of these (Restyle); it moves
    /// as that kind of dust (UpdateType), is as bright as that kind (GetAlpha; the kinds the game
    /// leaves unlit unlit), and dies as it does. Drawn: the game's frame of it a fifth of the way to
    /// grey at 70% of the game's colour for it, its outline turned and scaled with it, the greens
    /// as faint as the dust is.
    ///
    /// One for each kind the squad makes: the torch flame of the archer's arrows, the
    /// Necromancer's shadow, the Diabolist's fire, the Ragged Caster's spirit and the Giant Cursed
    /// Skull's shadowflame.
    /// </summary>
    public abstract class BoneSquadDust : ModDust
    {
        /// <summary>The game's kind of dust this one is.</summary>
        public abstract int Vanilla { get; }

        /// <summary>The game's kinds, to these.</summary>
        private static readonly Dictionary<int, int> Restyled = new Dictionary<int, int>();

        /// <summary>No picture of its own: ModDust.Register gives it the game's dust sheet.</summary>
        public override string Texture => null;

        public override void SetStaticDefaults()
        {
            UpdateType = Vanilla;
            Restyled[Vanilla] = Type;
        }

        public override void Unload() => Restyled.Clear();

        /// <summary>The dust Dust.NewDust just made, made one of these if its kind is one of theirs. Returns the index.</summary>
        public static int Restyle(int index)
        {
            if (index >= 0 && index < Main.maxDust && Restyled.TryGetValue(Main.dust[index].type, out int type))
                Main.dust[index].type = type;

            return index;
        }

        /// <summary>Dust.NewDust, the dust made one of these.</summary>
        public static Dust New(Vector2 position, int width, int height, int type, float speedX = 0f, float speedY = 0f, int alpha = 0, Color colour = default, float scale = 1f)
        {
            return Main.dust[Restyle(Dust.NewDust(position, width, height, type, speedX, speedY, alpha, colour, scale))];
        }

        /// <summary>The kinds Main.DrawDust draws unlit (it does so only for the game's own).</summary>
        private static bool Unlit(int type) => type == DustID.Torch || type == DustID.MagicMirror || (type >= 59 && type <= 64);

        public override Color? GetAlpha(Dust dust, Color lightColor)
        {
            int own = dust.type;
            dust.type = Vanilla;
            Color colour = dust.GetAlpha(Unlit(Vanilla) ? Color.White : lightColor);
            dust.type = own;
            return colour;
        }

        public override bool PreDraw(Dust dust)
        {
            // Main.DrawDust's colour, scale and turn for the game's kind.
            int own = dust.type;
            dust.type = Vanilla;
            Color light = Unlit(Vanilla) ? Color.White : Lighting.GetColor((int)((double)dust.position.X + 4.0) / 16, (int)((double)dust.position.Y + 4.0) / 16);
            Color colour = dust.GetAlpha(light);
            float rotation = dust.GetVisualRotation();
            float scale = dust.GetVisualScale();
            Color tint = dust.color.PackedValue != 0 ? dust.GetColor(colour) : default;
            dust.type = own;

            Texture2D sheet = TextureAssets.Dust.Value;
            Vector2 at = dust.position - Main.screenPosition;
            Vector2 origin = new Vector2(4f, 4f);
            BoneSquadLook.Ring ring = BoneSquadLook.RingOf(sheet, dust.frame);

            BoneSquadLook.Draw(Main.spriteBatch, sheet, at, dust.frame, colour, rotation, origin, scale, SpriteEffects.None, ring, (255 - dust.alpha) / 255f);

            // The game draws a tinted dust a second time in its tint.
            if (tint.PackedValue != 0)
                Main.spriteBatch.Draw(BoneSquadLook.GreyerOf(sheet), at, dust.frame, tint * BoneSquadLook.Opacity, rotation, origin, scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    /// <summary>The archer's flaming arrows' torch flame.</summary>
    public class BoneSquadDustTorch : BoneSquadDust
    {
        public override int Vanilla => DustID.Torch;
    }

    /// <summary>The Necromancer's shadow (its teleport and its shadow beam).</summary>
    public class BoneSquadDustNecromancer : BoneSquadDust
    {
        public override int Vanilla => DustID.ShadowbeamStaff;
    }

    /// <summary>The Diabolist's fire (its teleport, its bolt and its blast).</summary>
    public class BoneSquadDustDiabolist : BoneSquadDust
    {
        public override int Vanilla => DustID.InfernoFork;
    }

    /// <summary>The Ragged Caster's spirit (its teleport and its lost soul).</summary>
    public class BoneSquadDustRagged : BoneSquadDust
    {
        public override int Vanilla => DustID.SpectreStaff;
    }

    /// <summary>The Giant Cursed Skull's shadowflame.</summary>
    public class BoneSquadDustShadowflame : BoneSquadDust
    {
        public override int Vanilla => DustID.GiantCursedSkullBolt;
    }
}
