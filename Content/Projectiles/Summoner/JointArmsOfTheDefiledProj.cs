using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Summoner;
using PisAndDag.Content.NPCs.Abomanation_Small;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// Joint Arms Of The Defiled, swung (the item is JointArmsOfTheDefiled). Raw meat, as long
    /// as a Firecracker, with the animal's own tail end at the tip.
    ///
    /// Each swing tells the streak whether it landed (JointArmsStreak). Each hit throws the
    /// whip's blood off where it struck, the way the animal bleeds when it is cut
    /// (AbomanationChain.Slashed, and its numbers). A swing that spends the streak (ai[2] 1,
    /// the item's call) glows red all the way out and bursts at the crack.
    ///
    /// Its sheet, 16 wide, each piece pointing down, two empty rows between them: the handle
    /// (24 tall: a bone joint behind the hand, the grip after it), three links of 18 thinning
    /// for the first, middle and last third Of The lash, and the tail end (18) with the
    /// opening at its far end.
    /// </summary>
    public class JointArmsOfTheDefiledProj : BaseWhip
    {
        /// <summary>Firecracker's reach (1.4 on a 30-tick swing), on this whip's 35-tick swing.</summary>
        public override float RangeMultiplier => 1.4f * 30f / 35f;

        public override float MultihitKeep => 0.8f;

        /// <summary>The sinew between the links, where they pull apart.</summary>
        protected override Color LineColour => new Color(120, 20, 26);

        /// <summary>Whether this swing spends the streak: the item's call as it throws it, sent in ai[2].</summary>
        public bool Spends => Projectile.ai[2] == 1f;

        /// <summary>The red round a swing that spends the streak. No alpha: it adds.</summary>
        private static readonly Color Glow = new Color(220, 30, 40, 0);

        private static readonly Color[] Reds =
        {
            Abomanation.Blood,
            new Color(168, 36, 48),
            new Color(193, 62, 62),
            new Color(215, 118, 118),
        };

        // ---- the sheet ------------------------------------------------------------------------

        private static readonly Rectangle Handle = new Rectangle(0, 0, 16, 24);
        private static readonly Rectangle ThickLink = new Rectangle(0, 26, 16, 18);
        private static readonly Rectangle Link = new Rectangle(0, 46, 16, 18);
        private static readonly Rectangle ThinLink = new Rectangle(0, 66, 16, 18);
        private static readonly Rectangle TailEnd = new Rectangle(0, 86, 16, 18);

        protected override Rectangle Frame(int i, int count)
        {
            if (i == 0)
                return Handle;

            if (i == count - 2)
                return TailEnd;

            // The links, as a share Of The way along them: thick, then thinner, then thin.
            float along = (i - 1) / (float)Math.Max(1, count - 3);

            if (along < 1f / 3f)
                return ThickLink;

            return along < 2f / 3f ? Link : ThinLink;
        }

        protected override Vector2 Origin(int i, int count, Rectangle frame)
        {
            // The hand holds the handle just under the joint.
            if (i == 0)
                return new Vector2(8f, 8f);

            // The tail end hangs from its top, so it swells out past the last point.
            if (i == count - 2)
                return new Vector2(8f, 5f);

            return new Vector2(8f, 9f);
        }

        // ---- hits -----------------------------------------------------------------------------

        public override void OnWhipHit(NPC target, NPC.HitInfo hit, int damageDone, bool first)
        {
            if (first)
                Owner.GetModPlayer<JointArmsStreak>().Landed();

            Spatter(target, hit);
        }

        public override void OnCrackEnd(bool landed)
        {
            if (!landed)
                Owner.GetModPlayer<JointArmsStreak>().Missed();
        }

        /// <summary>
        /// The whip is meat, and it spatters where it strikes: droplets from the point Of The
        /// lash that met the target, thrown on away from the one swinging it, in a fan. As
        /// many and as fast as the hit was hard, on the animal's own scale.
        /// </summary>
        private void Spatter(NPC target, NPC.HitInfo hit)
        {
            if (Main.dedServ)
                return;

            List<Vector2> points = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, points);

            if (points.Count == 0)
                return;

            // Where it met: the point Of The lash nearest the target, brought onto its box.
            Rectangle box = target.Hitbox;
            Vector2 met = points[points.Count - 1];
            float nearest = float.MaxValue;

            foreach (Vector2 point in points)
            {
                Vector2 on = Vector2.Clamp(point, box.TopLeft(), box.BottomRight());
                float distance = Vector2.DistanceSquared(point, on);

                if (distance < nearest)
                {
                    nearest = distance;
                    met = on;
                }
            }

            Vector2 away = (met - Owner.Center).SafeNormalize(Vector2.UnitX * Owner.direction);
            float force = MathHelper.Clamp(hit.Damage / Abomanation.BloodReference, Abomanation.BloodLeast, Abomanation.BloodMost);
            int count = (int)(Abomanation.SlashDroplets * force);

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = away.RotatedBy(Main.rand.NextFloat(-Abomanation.SlashFan, Abomanation.SlashFan))
                    * Abomanation.SlashSpeed * force * Main.rand.NextFloat(0.6f, 1.25f);

                PADParticleHandler.SpawnParticle(new BloodDroplet(met, velocity, Main.rand.Next(26, 48),
                    Main.rand.NextFloat(0.55f, 1f), Abomanation.Blood));
            }
        }

        // ---- the swing that spends the streak -----------------------------------------------------

        public override void SafePostAI()
        {
            if (Spends && !Main.dedServ)
                Lighting.AddLight(Tip(), 0.55f, 0.06f, 0.08f);
        }

        /// <summary>It cracks in a burst of blood and red light, and a wet report under the crack.</summary>
        public override void OnCrack(Vector2 tip)
        {
            if (!Spends || Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.45f, Volume = 0.85f }, tip);

            for (int i = 0; i < 16; i++)
            {
                Vector2 way = Main.rand.NextVector2Unit();

                PADParticleHandler.SpawnParticle(new BloodDroplet(tip + way * 6f, way * Main.rand.NextFloat(2.5f, 6.5f),
                    Main.rand.Next(30, 50), Main.rand.NextFloat(0.6f, 1.05f), Abomanation.Blood));
            }

            SparkShard.Spray(tip, Reds[0], count: 8, speed: 3f, maxLength: 34f, lifetime: 18, palette: Reds);
            PADParticleHandler.SpawnParticle(new BloomGeneric(tip, Vector2.Zero, new Color(200, 20, 30), 0.8f, 20));
        }

        /// <summary>The swing that spends the streak has a pulsing red rim round every piece of it.</summary>
        protected override void DrawWhip(List<Vector2> points)
        {
            if (Spends)
            {
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 20f);
                Color rim = Glow * (0.55f * pulse);

                for (int k = 0; k < 8; k++)
                {
                    Vector2 offset = (MathHelper.TwoPi * k / 8f).ToRotationVector2() * 2f;
                    DrawPieces(points, rim, offset);
                }
            }

            base.DrawWhip(points);
        }
    }
}
