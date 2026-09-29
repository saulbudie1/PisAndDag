using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// The star of the CC bypass tell. A particle, not a draw hook: it is spawned once by
    /// CCBypass.Warn, rides on the thing it warns for, blinks, and is gone in half a
    /// second.
    ///
    /// The texture is white on transparent, a soft four-point star that fades to
    /// nothing at its edges with a faint bloom round it, the way Calamity's sparkles are
    /// made (Particles/Sparkle2 there). Premultiplied. Drawn additively in the warning
    /// yellow, so it lights what is under it rather than sitting on top of it. At scale
    /// 0.85 it is twenty-nine pixels tall: two thirds of a player.
    /// </summary>
    public class CCBypassStar : PADParticle
    {
        /// <summary>Ticks on, then ticks off, and again. Five flashes in the thirty.</summary>
        private const int Blink = 3;

        public override bool SetLifetime => true;
        public override bool UseAdditiveBlend => true;

        /// <summary>Texture height 34 times this is the star's height on screen.</summary>
        private const float Size = 0.85f;

        /// <summary>What it rides on. The star sits on its centre until either is gone.</summary>
        private readonly Entity follow;

        /// <summary>Or, for a part that is no entity of its own, where that part is, asked each tick.</summary>
        private readonly Func<Vector2> where;

        public CCBypassStar(Entity follow)
        {
            this.follow = follow;
            Position = follow.Center;
            Lifetime = CCBypass.WarnTicks;
            Scale = Size;
            Color = CCBypass.Warning;
        }

        public CCBypassStar(Func<Vector2> where)
        {
            this.where = where;
            Position = where();
            Lifetime = CCBypass.WarnTicks;
            Scale = Size;
            Color = CCBypass.Warning;
        }

        public override void Update()
        {
            if (follow != null && follow.active)
                Position = follow.Center;
            else if (where != null)
                Position = where();

            // Flashing: whole ticks on, whole ticks off. No fade; a fade is not a flash.
            bool on = Time / Blink % 2 == 0;

            Color = on ? CCBypass.Warning : Color.Transparent;
        }
    }
}
