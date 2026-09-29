using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Base
{
    /// <summary>
    /// A whip. ExampleMod's ExampleWhipProjectile made into a base, so a whip is its numbers
    /// and its sheet and nothing else has to be written again.
    ///
    /// WHAT A WHIP IS TO THE GAME (1.4.4)
    ///
    ///     ProjectileID.Sets.IsAWhip        collision along the whole lash rather than the
    ///                                      hitbox, and flasks and imbues apply to it
    ///     Projectile.DefaultToWhip()       vanilla's whip AI (aiStyle 165): out and back over
    ///                                      the item's use time, the crack halfway, gone at
    ///                                      the end; two updates a tick, local immunity (each
    ///                                      enemy hit once a swing), summon damage
    ///     WhipSettings                     Segments, the points along it (vanilla 20), and
    ///                                      RangeMultiplier, its reach
    ///
    /// The item: Item.DefaultToWhip(this type, damage, knockback, shootSpeed, useTime), and
    /// MeleePrefix() returning true so it rolls melee prefixes. Its reach is shootSpeed times
    /// the item's use time times RangeMultiplier (Projectile.FillWhipControlPoints), so a whip
    /// that swings faster than another reaches less on the same settings: Firecracker is
    /// 1.4 at 30 ticks, and 1.68 at 25 ticks reaches as far.
    ///
    /// WHAT THIS ADDS TO THE EXAMPLE
    ///
    ///     the numbers as properties     Segments, RangeMultiplier, MultihitKeep, TagBuff,
    ///                                   TagTicks, and the hold-to-lengthen charge (off
    ///                                   unless ChargeTicks is set, and then the item needs
    ///                                   channel)
    ///     the sheet as methods          Frame, Origin and Scale per piece, 0 the handle,
    ///                                   count - 2 the tip; the defaults are the example's
    ///                                   sheet
    ///     the moments                   OnWhipHit (every hit, and whether it was the
    ///                                   swing's first), OnCrack (the tick vanilla cracks,
    ///                                   on every machine), OnCrackEnd (the owner's, as it
    ///                                   goes: whether any of it landed)
    ///     the drawing                   DrawWhip, which subclasses can add to: the line and
    ///                                   the pieces are DrawLine and DrawPieces, and
    ///                                   DrawPieces takes a colour and an offset for glows
    ///
    /// The ai slots: ai[0] is vanilla's clock (Timer), ai[1] the charge (ChargeTime); ai[2]
    /// is the subclass's, and is sent with the whip when it is made (NewProjectile's ai2).
    /// </summary>
    public abstract class BaseWhip : ModProjectile
    {
        protected Player Owner => Main.player[Projectile.owner];

        // ---- the numbers -------------------------------------------------------------------

        /// <summary>Points along the lash, the handle's included: its links, and how finely it collides. Vanilla's is 20.</summary>
        public virtual int Segments => 20;

        /// <summary>
        /// Its reach against vanilla's sum (shootSpeed x use time). Leather Whip 0.75, Spinal
        /// Tap 0.95, Snapthorn 1, Firecracker 1.4, Cool Whip 1.5, Durendal 1.75.
        /// </summary>
        public virtual float RangeMultiplier => 1f;

        /// <summary>
        /// What each enemy hit leaves of the damage for the next one along the lash. Vanilla's
        /// run from 0.5 (Leather Whip) to 0.95 (Morning Star); the example keeps 0.5.
        /// </summary>
        public virtual float MultihitKeep => 0.8f;

        /// <summary>The tag put on whatever it hits, and for how long in ticks. 0 for none.</summary>
        public virtual int TagBuff => 0;

        public virtual int TagTicks => 240;

        /// <summary>
        /// Holding the button lengthens it for up to this many updates before it goes (the
        /// example's charge: 120 is a second, whips updating twice a tick). 0 for none; the
        /// item needs Item.channel = true for the hold to be seen.
        /// </summary>
        public virtual int ChargeTicks => 0;

        /// <summary>While charging, a link more every this many updates.</summary>
        public virtual int ChargeTicksPerSegment => 12;

        /// <summary>While charging, this much reach more over the whole of a full charge (1 doubles vanilla's).</summary>
        public virtual float ChargeRange => 1f;

        /// <summary>Whether a line is drawn under the pieces, so no gap shows between links when it stretches.</summary>
        protected virtual bool DrawsLine => true;

        /// <summary>The line's colour, lit where it is.</summary>
        protected virtual Color LineColour => Color.White;

        // ---- state -------------------------------------------------------------------------

        /// <summary>Vanilla's clock: updates since it was thrown.</summary>
        protected float Timer
        {
            get => Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        /// <summary>Updates spent charging.</summary>
        protected float ChargeTime
        {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        /// <summary>Whether this swing has hit anything yet. The owner's: only the owner's machine is told of hits.</summary>
        protected bool Landed { get; private set; }

        /// <summary>Whether this swing has cracked yet.</summary>
        protected bool Cracked { get; private set; }

        // ---- made ---------------------------------------------------------------------------

        public sealed override void SetStaticDefaults()
        {
            ProjectileID.Sets.IsAWhip[Type] = true;
            SafeSetStaticDefaults();
        }

        public virtual void SafeSetStaticDefaults()
        {
        }

        public sealed override void SetDefaults()
        {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = Segments;
            Projectile.WhipSettings.RangeMultiplier = RangeMultiplier;
            SafeSetDefaults();
        }

        public virtual void SafeSetDefaults()
        {
        }

        // ---- ai -----------------------------------------------------------------------------

        /// <summary>The charge, if there is one, while the button is held; then vanilla's whip.</summary>
        public sealed override bool PreAI()
        {
            if (ChargeTicks > 0 && Owner.channel && ChargeTime < ChargeTicks)
            {
                ChargeTime++;

                if (ChargeTicksPerSegment > 0 && ChargeTime % ChargeTicksPerSegment == 0)
                    Projectile.WhipSettings.Segments++;

                Projectile.WhipSettings.RangeMultiplier += ChargeRange / ChargeTicks;

                // Held where it is: the swing does not start until the button is let go.
                Owner.itemAnimation = Owner.itemAnimationMax;
                Owner.itemTime = Owner.itemTimeMax;

                return false;
            }

            return SafePreAI();
        }

        /// <summary>False keeps vanilla's whip from running this update.</summary>
        public virtual bool SafePreAI() => true;

        public sealed override void PostAI()
        {
            // The crack is on the update vanilla sounds it (AI_165_Whip: the clock at half the
            // time it takes to fly out). The clock only runs while the whip does, so a charge
            // holds it back as it holds the sound.
            if (!Cracked)
            {
                Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out int _, out float _);

                if (Timer >= (int)(timeToFlyOut / 2f))
                {
                    Cracked = true;
                    OnCrack(Tip());
                }
            }

            SafePostAI();
        }

        public virtual void SafePostAI()
        {
        }

        /// <summary>The swing cracks, on every machine: where its tip is.</summary>
        public virtual void OnCrack(Vector2 tip)
        {
        }

        /// <summary>Where the end of the lash is now.</summary>
        protected Vector2 Tip()
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            return points[points.Count - 1];
        }

        // ---- hits ---------------------------------------------------------------------------

        /// <summary>
        /// The tag, the owner's minions sent after what was hit, and less for the next one
        /// along the lash; then the subclass's. On the owner's machine only, as every hit a
        /// player's projectile makes is.
        /// </summary>
        public sealed override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            bool first = !Landed;
            Landed = true;

            if (TagBuff > 0)
                target.AddBuff(TagBuff, TagTicks);

            Owner.MinionAttackTargetNPC = target.whoAmI;
            Projectile.damage = (int)(Projectile.damage * MultihitKeep);

            OnWhipHit(target, hit, damageDone, first);
        }

        /// <summary>A hit, on the owner's machine: <paramref name="first"/> if it is the first thing this swing has hit.</summary>
        public virtual void OnWhipHit(NPC target, NPC.HitInfo hit, int damageDone, bool first)
        {
        }

        public sealed override void OnKill(int timeLeft)
        {
            if (Projectile.owner == Main.myPlayer)
                OnCrackEnd(Landed);

            SafeOnKill(timeLeft);
        }

        /// <summary>The swing is over, on the owner's machine: whether any of it landed.</summary>
        public virtual void OnCrackEnd(bool landed)
        {
        }

        public virtual void SafeOnKill(int timeLeft)
        {
        }

        // ---- the sheet ------------------------------------------------------------------------
        //
        // The pieces stand one above the other on the sheet, each drawn pointing down: the
        // handle, then the links, then the tip. Piece i is drawn on control point i, turned to
        // point at i + 1; there are Segments + 1 points, so i runs from 0 (the handle) to
        // count - 2 (the tip). The defaults are the example's sheet: 10 wide, the handle 26
        // tall, three links of 16 (the first five, the next five, the rest), the tip 18.

        /// <summary>The piece of the sheet drawn on control point <paramref name="i"/> of <paramref name="count"/>.</summary>
        protected virtual Rectangle Frame(int i, int count)
        {
            if (i == 0)
                return new Rectangle(0, 0, 10, 26);

            if (i == count - 2)
                return new Rectangle(0, 74, 10, 18);

            if (i > 10)
                return new Rectangle(0, 58, 10, 16);

            if (i > 5)
                return new Rectangle(0, 42, 10, 16);

            return new Rectangle(0, 26, 10, 16);
        }

        /// <summary>The point of the piece that sits on its control point: for the handle, where the hand holds it.</summary>
        protected virtual Vector2 Origin(int i, int count, Rectangle frame) => new Vector2(5f, 8f);

        /// <summary>
        /// Its size. The tip swells as the lash goes out and shrinks as it comes back, half
        /// again as big at full stretch and half as big curled (vanilla's and the example's).
        /// </summary>
        protected virtual float Scale(int i, int count)
        {
            if (i != count - 2)
                return 1f;

            Projectile.GetWhipSettings(Projectile, out float timeToFlyOut, out int _, out float _);
            float t = Timer / timeToFlyOut;

            return MathHelper.Lerp(0.5f, 1.5f, Utils.GetLerpValue(0.1f, 0.7f, t, true) * Utils.GetLerpValue(0.9f, 0.7f, t, true));
        }

        // ---- drawn ----------------------------------------------------------------------------

        public sealed override bool PreDraw(ref Color lightColor)
        {
            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            if (points.Count >= 2)
                DrawWhip(points);

            return false;
        }

        /// <summary>The whole whip: the line, then the pieces over it. Add to it, or draw it another way.</summary>
        protected virtual void DrawWhip(List<Vector2> points)
        {
            if (DrawsLine)
                DrawLine(points);

            DrawPieces(points);
        }

        /// <summary>A line through every point, so the links never show a gap between them (the example's).</summary>
        protected void DrawLine(List<Vector2> points)
        {
            Texture2D texture = TextureAssets.FishingLine.Value;
            Rectangle frame = texture.Frame();
            Vector2 origin = new Vector2(frame.Width / 2, 2);

            Vector2 at = points[0];

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 element = points[i];
                Vector2 diff = points[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color colour = Lighting.GetColor(element.ToTileCoordinates(), LineColour);
                Vector2 scale = new Vector2(1f, (diff.Length() + 2f) / frame.Height);

                Main.EntitySpriteDraw(texture, at - Main.screenPosition, frame, colour, rotation, origin, scale, SpriteEffects.None, 0);

                at += diff;
            }
        }

        /// <summary>
        /// Every piece along the lash. Lit where each is, or all in <paramref name="colour"/> when
        /// one is given (not lit: for a glow, give it no alpha and it adds); moved by
        /// <paramref name="offset"/>, for the copies an outline is made of.
        /// </summary>
        protected void DrawPieces(List<Vector2> points, Color? colour = null, Vector2 offset = default)
        {
            Main.instance.LoadProjectile(Type);
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            // The sheet points down and is drawn for a lash thrown left; thrown right, mirrored.
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            int count = points.Count;
            Vector2 at = points[0];

            for (int i = 0; i < count - 1; i++)
            {
                Rectangle frame = Frame(i, count);
                Vector2 origin = Origin(i, count, frame);
                float scale = Scale(i, count);

                Vector2 element = points[i];
                Vector2 diff = points[i + 1] - element;

                float rotation = diff.ToRotation() - MathHelper.PiOver2;
                Color drawn = colour ?? Lighting.GetColor(element.ToTileCoordinates());

                Main.EntitySpriteDraw(texture, at + offset - Main.screenPosition, frame, drawn, rotation, origin, scale, flip, 0);

                at += diff;
            }
        }
    }
}
