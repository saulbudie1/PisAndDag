using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PisAndDag.Content.Particles
{
    /// <summary>
    /// A purely visual, client-side effect. Deliberately NOT a ModType — it does not
    /// autoload through tModLoader, reserves no ID and is never network-synced, which
    /// is what makes it cheap enough to spawn by the dozen. It sits between Dust
    /// (too rigid to control) and Projectile (far too expensive for decoration).
    ///
    /// Subclass it, set the fields in a constructor, and drive everything from Update().
    /// PADParticleHandler moves it, ages it and draws it.
    /// </summary>
    public abstract class PADParticle
    {
        /// <summary>Index assigned by the handler at load. Used to look up the texture.</summary>
        public int Type;

        /// <summary>Ticks since spawn. Incremented by the handler.</summary>
        public int Time;

        /// <summary>Ticks to live. Only enforced when <see cref="SetLifetime"/> is true.</summary>
        public int Lifetime;

        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Rotation;
        public float Scale;

        /// <summary>Which vertical frame of the texture to draw, when FrameVariants > 1.</summary>
        public int Variant;

        /// <summary>
        /// 0 at spawn, 1 at death. The single value to drive fades, shrinks and easing
        /// from — a cubic curve on this reads far better than a linear fade.
        /// </summary>
        public float LifetimeCompletion => Lifetime == 0 ? 0f : (float)Time / Lifetime;

        /// <summary>
        /// Texture path. Defaults to the namespace-as-folder convention, e.g.
        /// PisAndDag.Content.Particles.BloodDroplet -> PisAndDag/Content/Particles/BloodDroplet
        /// Override only when the texture lives somewhere else.
        ///
        /// This resolves itself rather than returning "" and leaving the handler to fill
        /// the gap — an empty path reaching ModContent.Request throws MissingResourceException.
        /// It uses reflection, so don't call it in a draw loop: use
        /// PADParticleHandler.GetTexture(Type) there instead.
        /// </summary>
        public virtual string Texture =>
            (GetType().Namespace?.Replace('.', '/') ?? "PisAndDag") + "/" + GetType().Name;

        /// <summary>True to have the handler kill this once Time reaches Lifetime.</summary>
        public virtual bool SetLifetime => false;

        /// <summary>True to spawn even when the particle budget is full. Use sparingly.</summary>
        public virtual bool Important => false;

        /// <summary>Number of vertical frames in the texture.</summary>
        public virtual int FrameVariants => 1;

        /// <summary>Draw additively — for anything that should glow rather than occlude.</summary>
        public virtual bool UseAdditiveBlend => false;

        /// <summary>True when the default draw can't express this particle.</summary>
        public virtual bool UseCustomDraw => false;

        /// <summary>Only called when <see cref="UseCustomDraw"/> is true.</summary>
        public virtual void CustomDraw(SpriteBatch spriteBatch) { }

        /// <summary>
        /// Runs once per tick, after the handler has applied Velocity and incremented Time.
        /// </summary>
        public virtual void Update() { }

        /// <summary>Queue this particle for removal at the end of the current tick.</summary>
        public void Kill() => PADParticleHandler.RemoveParticle(this);
    }
}
