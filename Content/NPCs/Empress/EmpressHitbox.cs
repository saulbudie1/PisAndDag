using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// Where one of the Empress's Legends acts hurts (EmpressOfLight.Legends, EmpressOfLight.Second): nothing to see (the act draws
    /// what it is), a shape to be inside. The server makes it; every player's own machine is hurt by it, as
    /// by any hostile shot of the game's, with its immunity and its dodges.
    ///
    ///     ai[0]  the shape        ai[1]  its number (a radius)        ai[2]  hers (her index)
    ///
    /// All but the round blow ask her, each tick, where they are as her act stands on this machine (so they
    /// hurt exactly where that machine draws them), and go when her act is over. Only players inside her
    /// arena are hurt, and never anyone else (no town folk, no critters). Its life is sent with it: a
    /// client's copy would otherwise die at the game's default.
    /// </summary>
    public class EmpressHitbox : ModProjectile
    {
        public enum Shape
        {
            /// <summary>A round blow about where it was made: ai[1] its radius. (The mirror's shatter, the grab's blast.)</summary>
            Circle,

            /// <summary>The laser pillars' row, each beam while it is whole.</summary>
            Pillars,

            /// <summary>The eclipse's ring going out, but in its notch.</summary>
            Ring,

            /// <summary>The prism's white ray and its seven.</summary>
            Rays,

            /// <summary>The saw's blade.</summary>
            Saw,

            /// <summary>The aurora's curtains.</summary>
            Curtains,

            /// <summary>Her second half's: the cage of curtains' walls.</summary>
            Cage,

            /// <summary>The constellation's lasers.</summary>
            Constellation,

            /// <summary>The seven out of the prism at her heart.</summary>
            Heart,

            /// <summary>The lightspeed shot's line.</summary>
            Lightspeed,

            /// <summary>The dawnbreak's sear, everywhere but her shadow.</summary>
            Sear,

            /// <summary>The rain of light's columns.</summary>
            Rain,

            /// <summary>The blade of everlasting rainbow, as it sweeps.</summary>
            Blade,
        }

        /// <summary>How thick the eclipse's ring is, all of it hurting.</summary>
        public const float RingThickness = 44f;

        public override string Texture => "PisAndDag/Content/Graphics/GlowSoft";

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
            Projectile.netImportant = true;

            // Long: the server gives each its own life (sent), and ends it.
            Projectile.timeLeft = 3600;
        }

        private Shape Kind => (Shape)(int)Projectile.ai[0];

        /// <summary>Whose it is (null once she is gone).</summary>
        private EmpressOfLight Her => EmpressOfLight.Of((int)Projectile.ai[2]);

        /// <summary>The act each shape that follows her belongs to.</summary>
        private static EmpressOfLight.Act ActOf(Shape kind) => kind switch
        {
            Shape.Pillars => EmpressOfLight.Act.Pillars,
            Shape.Ring => EmpressOfLight.Act.Eclipse,
            Shape.Rays => EmpressOfLight.Act.Prism,
            Shape.Saw => EmpressOfLight.Act.Saw,
            Shape.Curtains => EmpressOfLight.Act.Aurora,
            Shape.Cage => EmpressOfLight.Act.Cage,
            Shape.Constellation => EmpressOfLight.Act.Constellation,
            Shape.Heart => EmpressOfLight.Act.Heart,
            Shape.Lightspeed => EmpressOfLight.Act.Lightspeed,
            Shape.Sear => EmpressOfLight.Act.Dawn,
            Shape.Rain => EmpressOfLight.Act.Rain,
            Shape.Blade => EmpressOfLight.Act.Blade,
            _ => EmpressOfLight.Act.None,
        };

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            EmpressOfLight her = Her;

            // Gone with her; and those that follow her act, with it.
            if (her == null || Kind != Shape.Circle && !her.ActRunning(ActOf(Kind)))
                Projectile.Kill();
        }

        public override void SendExtraAI(BinaryWriter writer) => writer.Write(Projectile.timeLeft);

        public override void ReceiveExtraAI(BinaryReader reader) => Projectile.timeLeft = reader.ReadInt32();

        public override bool? CanDamage() => true;

        /// <summary>Hers are for the players in her arena alone: no town folk, no critters, nobody's pets.</summary>
        public override bool? CanHitNPC(NPC target) => false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            EmpressOfLight her = Her;

            if (her == null || !her.InArena(targetHitbox))
                return false;

            return Kind switch
            {
                Shape.Circle => Near(targetHitbox, Projectile.Center, Projectile.ai[1]),
                Shape.Pillars => her.PillarsHit(targetHitbox),
                Shape.Ring => her.RingHits(targetHitbox),
                Shape.Rays => her.PrismHits(targetHitbox),
                Shape.Saw => her.SawHits(targetHitbox),
                Shape.Curtains => her.CurtainsHit(targetHitbox),
                Shape.Cage => her.CageHits(targetHitbox),
                Shape.Constellation => her.ConstellationHits(targetHitbox),
                Shape.Heart => her.HeartHits(targetHitbox),
                Shape.Lightspeed => her.LightspeedHits(targetHitbox),
                Shape.Sear => her.SearHits(targetHitbox),
                Shape.Rain => her.RainHits(targetHitbox),
                Shape.Blade => her.BladeHits(targetHitbox),
                _ => false,
            };
        }

        /// <summary>A box within this far of a point.</summary>
        internal static bool Near(Rectangle box, Vector2 point, float radius)
        {
            Vector2 nearest = Vector2.Clamp(point, box.TopLeft(), box.BottomRight());
            return Vector2.DistanceSquared(nearest, point) <= radius * radius;
        }

        /// <summary>A box this near a line of light (from, to), as wide as given.</summary>
        internal static bool OnLine(Rectangle box, Vector2 from, Vector2 to, float width)
        {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(box.TopLeft(), box.Size(), from, to, width, ref point);
        }

        /// <summary>
        /// A box touched by a ring of this radius about a point (its band this thick), but in its notch (the way it
        /// leaves open, and half the notch's width either side of it).
        /// </summary>
        internal static bool OnRing(Rectangle box, Vector2 centre, float radius, float notchWay, float notchHalf)
        {
            Vector2 nearest = Vector2.Clamp(centre, box.TopLeft(), box.BottomRight());
            Vector2 farthest = new Vector2(
                Math.Abs(box.Left - centre.X) > Math.Abs(box.Right - centre.X) ? box.Left : box.Right,
                Math.Abs(box.Top - centre.Y) > Math.Abs(box.Bottom - centre.Y) ? box.Top : box.Bottom);

            float inner = Vector2.Distance(nearest, centre);
            float outer = Vector2.Distance(farthest, centre);

            // The ring's band crosses the body somewhere between its nearest and farthest point.
            if (radius + RingThickness * 0.5f < inner || radius - RingThickness * 0.5f > outer)
                return false;

            // In the notch, it passes them by.
            float way = (box.Center.ToVector2() - centre).ToRotation();
            return Math.Abs(MathHelper.WrapAngle(way - notchWay)) > notchHalf;
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
