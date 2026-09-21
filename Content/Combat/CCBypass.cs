using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// The tell for anything that goes through crowd-control immunity.
    ///
    /// A hit that no shield turns off is announced, always in the same way, so the
    /// player learns one signal and it holds for everything in the mod: the star (a
    /// particle, CCBypassStar) and the note when the thing enters that state, and a
    /// yellow outline on it for as long as it stays in it. The Abomanation's whip was the first; anything after it uses
    /// this and nothing of its own.
    ///
    /// HOW TO USE. An NPC or projectile sets <c>PAD().CCBypass = true</c> every tick it
    /// is in the state (the flag is cleared before each AI, so the owner does not have
    /// to clear it). The outline follows the flag on its own. On the tick the flag goes
    /// on, the star and the note fire on that entity, unless <c>PAD().CCQuiet</c> is set,
    /// which is for the parts of a many-part thing that should not all announce
    /// themselves at once; the thing then calls <see cref="Warn(NPC)"/> itself from the
    /// part that should. The outline is the entity's own sprite; an entity that draws
    /// something other than its registered texture implements
    /// <see cref="ICCBypassOutline"/> and draws the outline of what it really shows.
    /// </summary>
    public static class CCBypass
    {
        /// <summary>The yellow. The star, the outline and nothing else in the mod is this colour.</summary>
        public static readonly Color Warning = new Color(255, 222, 66);

        /// <summary>Ticks the star shows for. Half a second.</summary>
        public const int WarnTicks = 30;

        /// <summary>How far the outline stands off the sprite, in pixels.</summary>
        public const float OutlineReach = 3f;

        /// <summary>Strength of each of the eight copies that make the outline. They stack where they overlap.</summary>
        public const float OutlineGlow = 0.5f;

        /// <summary>The note. Sampled and cut from a short chime: two hits, the first pitched up, a click on each.</summary>
        public static readonly SoundStyle Note = new SoundStyle("PisAndDag/Content/Sounds/CCBypassWarn")
        {
            Volume = 0.9f,
            MaxInstances = 1,
        };

        /// <summary>
        /// The star and the note on this thing, now. For a part that was kept quiet, or
        /// for a tell ahead of the state. Client-side both: the particle handler and the
        /// sound engine do nothing on a server.
        /// </summary>
        public static void Warn(Entity entity)
        {
            PADParticleHandler.SpawnParticle(new CCBypassStar(entity));
            SoundEngine.PlaySound(Note, entity.Center);
        }

        /// <summary>
        /// The outline. Eight copies of the sprite in the warning colour with no alpha, a
        /// few pixels out in a ring, drawn BEFORE the sprite so the sprite covers the
        /// middle and only the ring shows. No alpha means the copies add to what is
        /// under them rather than covering it, and where they overlap they add up. The
        /// same method as the Greatbow's charge glow, in this colour.
        /// </summary>
        public static void Outline(SpriteBatch spriteBatch, Texture2D texture, Rectangle? frame, Vector2 at,
            float rotation, Vector2 origin, float scale, SpriteEffects flip)
        {
            Color glow = Warning * OutlineGlow;
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * OutlineReach;

                spriteBatch.Draw(texture, at + offset, frame, glow, rotation, origin, scale, flip, 0f);
            }
        }
    }

    /// <summary>
    /// For an NPC or projectile whose registered texture is not what it draws. The
    /// global outline would trace the wrong sprite; this draws the right one with
    /// <see cref="CCBypass.Outline"/>, and is called in place of the generic outline.
    /// </summary>
    public interface ICCBypassOutline
    {
        void DrawCCBypassOutline(SpriteBatch spriteBatch, Vector2 screenPos);
    }
}
