using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// A tail of Scorpius Servus's (the item is Content/Items/Summoner/ScorpiusServus.cs), by request: "Spawn a
    /// curved 9 segment tail that is attached to the butt, and if it detechs anything within range of 13 tiles, it
    /// reaches and pierce it, poisoning it too, make this beautiful"; and each summoned is another tail ("Does add
    /// extra tail"). One minion slot each.
    ///
    /// Nine plates of Harenaeum on a turquoise sinew, from the small of the owner's back up over their shoulder to
    /// a sting that hangs over their head, swaying as they breathe; more tails fan out from the same place, the
    /// first in the middle. What is within 13 tiles of the root and in sight, it strikes: drawn back tight, then
    /// thrown over in an arc onto it, the sinew stretched and the plates spread along it, the sting held in it a
    /// moment, and back. A strike hurts whatever the sting passes through and poisons it for five seconds; then the
    /// tail waits half a second before the next. Its tails strike in turn, not all at once.
    ///
    /// Mine, the rest: the numbers below; the shine that runs up the plates now and then; venom on the sting at
    /// rest, a drop now and then, and thrown off it where it goes in.
    ///
    /// The picture is a placeholder (PlaceholderTextures.md): two frames, a plate and the sting, both pointing right.
    /// </summary>
    public class ScorpiusTail : ModProjectile
    {
        /// <summary>By request: it strikes at what is within 13 tiles of its root.</summary>
        public const float Range = 13f * 16f;

        /// <summary>The marked enemy, from further: a little more than the reach, as it must still get to it.</summary>
        public const float MarkedRange = Range;

        public const int Segments = 9;

        /// <summary>What it is doing (ai[0]): at rest, drawn back, striking, held in, coming back.</summary>
        public const int Rest = 0, Coil = 1, Strike = 2, Hold = 3, Recoil = 4;

        /// <summary>Ticks of each. Mine.</summary>
        public const int CoilTicks = 9, StrikeTicks = 6, HoldTicks = 5, RecoilTicks = 14, Wait = 30;

        /// <summary>Five seconds of the game's Poisoned. Mine.</summary>
        public const int PoisonTicks = 300;

        /// <summary>The picture's frames: a plate, the sting.</summary>
        public const int FrameWidth = 18, FrameHeight = 14;

        private int Mode
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        private ref float Timer => ref Projectile.ai[1];

        private NPC Target
        {
            get
            {
                int i = (int)Projectile.ai[2] - 1;

                if (i < 0 || i >= Main.maxNPCs)
                    return null;

                NPC npc = Main.npc[i];
                return npc.active && npc.CanBeChasedBy(Projectile) ? npc : null;
            }
        }

        /// <summary>Ticks it has been out, on this machine: the sway, the shine.</summary>
        private int life;

        /// <summary>The sting's last two places, for what it went through and the streak behind it.</summary>
        private Vector2 lastTip;
        private readonly Vector2[] trail = new Vector2[8];

        /// <summary>The curve's four points this tick, as if the owner faced right, from the root.</summary>
        private Vector2 p1, p2, p3;
        private Vector2 root;
        private int facing = 1;

        /// <summary>The shape it had when it began what it is doing now, eased out of; and what that was.</summary>
        private Vector2 fromP1, fromP2, fromP3;
        private int shapedMode = -1;

        /// <summary>The plates' places and angles, worked out to draw.</summary>
        private readonly Vector2[] places = new Vector2[Segments];
        private readonly float[] angles = new float[Segments];

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 2;
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => true;

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Mode == Strike || Mode == Hold ? null : false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<ScorpiusServusBuff>();

            if (!owner.active || owner.dead)
            {
                owner.ClearBuff(buff);
                return;
            }

            if (owner.HasBuff(buff))
                Projectile.timeLeft = 2;

            bool mine = Projectile.owner == Main.myPlayer;
            Group(out int index, out int count);
            life++;

            facing = owner.direction;
            root = owner.MountedCenter + new Vector2(-facing * 6f, 5f * owner.gravDir);

            if (mine)
                PickTarget(index, count);

            NPC target = Target;
            Timer++;

            switch (Mode)
            {
                case Rest:
                    if (mine && target != null && Timer >= Wait + index * 7)
                        Go(Coil);
                    break;

                case Coil:
                    if (Timer >= CoilTicks)
                    {
                        if (target == null)
                            Go(Recoil);
                        else
                        {
                            Go(Strike);

                            if (!Main.dedServ)
                                SoundEngine.PlaySound(SoundID.DD2_JavelinThrowersAttack with { Volume = 0.45f, Pitch = 0.45f, PitchVariance = 0.15f, MaxInstances = 3 }, root);
                        }
                    }
                    break;

                case Strike:
                    if (target == null)
                        Go(Recoil);
                    else if (Timer >= StrikeTicks)
                        Go(Hold);
                    break;

                case Hold:
                    if (Timer >= HoldTicks || target == null)
                        Go(Recoil);
                    break;

                default:
                    if (Timer >= RecoilTicks)
                        Go(Rest);
                    break;
            }

            Shape(owner, index, count, target);

            Vector2 tip = Tip();
            Projectile.Center = tip;

            if (lastTip == Vector2.Zero)
                lastTip = tip;

            for (int i = trail.Length - 1; i > 0; i--)
                trail[i] = trail[i - 1];

            trail[0] = tip;

            if (!Main.dedServ)
                Effects(owner, tip);

            lastTip = tip;
        }

        /// <summary>On to the next thing it does, the timer from naught. Every machine works the shape out for itself (Shape).</summary>
        private void Go(int mode)
        {
            Mode = mode;
            Timer = 0f;

            if (Projectile.owner == Main.myPlayer)
                Projectile.netUpdate = true;
        }

        /// <summary>Which of its owner's tails it is (by identity, the same order on every machine), and how many they have.</summary>
        private void Group(out int index, out int count)
        {
            index = count = 0;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner == Projectile.owner && p.type == Type)
                {
                    count++;

                    if (p.identity < Projectile.identity)
                        index++;
                }
            }
        }

        /// <summary>
        /// The marked enemy, if near enough; or the nearest within reach of the root and in sight of it. Tails that
        /// have the choice go for different ones: of what is in reach, a tail takes the one its place among its
        /// owner's tails comes to, so two tails and two enemies are one each.
        /// </summary>
        private void PickTarget(int index, int count)
        {
            int pick = -1;
            NPC marked = Projectile.OwnerMinionAttackTargetNPC;

            if (marked != null && marked.CanBeChasedBy(Projectile) && Vector2.Distance(Nearest(marked, root), root) < MarkedRange)
                pick = marked.whoAmI;
            else
            {
                List<(float distance, int who)> near = new();

                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (!npc.CanBeChasedBy(Projectile))
                        continue;

                    float distance = Vector2.Distance(Nearest(npc, root), root);

                    if (distance > Range || !Collision.CanHitLine(root - new Vector2(4f), 8, 8, npc.position, npc.width, npc.height))
                        continue;

                    near.Add((distance, npc.whoAmI));
                }

                if (near.Count > 0)
                {
                    near.Sort((a, b) => a.distance.CompareTo(b.distance));

                    // Kept on what it has, while that is still in reach, mid-strike or not.
                    int now = (int)Projectile.ai[2] - 1;
                    pick = near.Exists(n => n.who == now) && Mode != Rest ? now : near[index % near.Count].who;
                }
            }

            if ((int)Projectile.ai[2] != pick + 1)
            {
                Projectile.ai[2] = pick + 1;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>The point of <paramref name="npc"/> nearest <paramref name="to"/>.</summary>
        private static Vector2 Nearest(NPC npc, Vector2 to) => Vector2.Clamp(to, npc.Hitbox.TopLeft(), npc.Hitbox.BottomRight());

        /// <summary>
        /// The tail's curve this tick, a Bezier from the root, as if facing right (x ahead, y down): at rest an arch
        /// up the back and over, fanned by its place among the tails and swaying; drawn back tight; thrown over onto
        /// where its target is (its middle, or the nearest of it when that is far); and between them, eased.
        /// </summary>
        private void Shape(Player owner, int index, int count, NPC target)
        {
            float spread = count <= 1 ? 0f : (index - (count - 1) * 0.5f);
            float fan = spread * 0.38f;
            float shrink = 1f - Math.Abs(spread) * 0.07f;
            float sway = (float)Math.Sin(life * 0.045f + index * 1.7f);
            float breathe = (float)Math.Sin(life * 0.03f + index);

            Vector2 restP1 = Turn(new Vector2(-20f, -4f), fan) * shrink;
            Vector2 restP2 = Turn(new Vector2(-30f + sway * 2f, -50f + breathe * 2f), fan) * shrink;
            Vector2 restP3 = Turn(new Vector2(0f + sway * 4f, -54f + breathe * 3f), fan) * shrink;

            Vector2 coilP1 = Turn(new Vector2(-18f, -10f), fan) * shrink;
            Vector2 coilP2 = Turn(new Vector2(-38f, -40f), fan) * shrink;
            Vector2 coilP3 = Turn(new Vector2(-16f, -48f), fan) * shrink;

            Vector2 strikeP1 = restP1, strikeP2 = restP2, strikeP3 = restP3;

            if (target != null)
            {
                Vector2 at = Vector2.Distance(target.Center, root) < Range ? target.Center : Nearest(target, root);
                Vector2 d = Upright(owner, at - root);
                float length = d.Length();

                if (length > Range)
                    d *= Range / length;

                float height = MathHelper.Clamp(d.Length() * 0.4f, 26f, 90f);
                strikeP1 = new Vector2(-10f, -height * 1.1f);
                strikeP2 = new Vector2(d.X * 0.6f, d.Y - height);
                strikeP3 = d;
            }

            // A new thing to do, on this machine (whether it chose it or was told): ease from the shape it had.
            if (Mode != shapedMode)
            {
                fromP1 = p1;
                fromP2 = p2;
                fromP3 = p3;
                shapedMode = Mode;
            }

            float t = Timer;

            switch (Mode)
            {
                case Coil:
                    Ease(coilP1, coilP2, coilP3, Smooth(t / CoilTicks));
                    break;

                case Strike:
                    float k = t / StrikeTicks;
                    Ease(strikeP1, strikeP2, strikeP3, k * k);
                    break;

                case Hold:
                    p1 = strikeP1;
                    p2 = strikeP2;
                    p3 = strikeP3;
                    break;

                case Recoil:
                    Ease(restP1, restP2, restP3, Smooth(t / RecoilTicks));
                    break;

                default:
                    p1 = restP1;
                    p2 = restP2;
                    p3 = restP3;
                    break;
            }
        }

        private void Ease(Vector2 to1, Vector2 to2, Vector2 to3, float k)
        {
            if (fromP3 == Vector2.Zero)
            {
                fromP1 = to1;
                fromP2 = to2;
                fromP3 = to3;
            }

            k = MathHelper.Clamp(k, 0f, 1f);
            p1 = Vector2.Lerp(fromP1, to1, k);
            p2 = Vector2.Lerp(fromP2, to2, k);
            p3 = Vector2.Lerp(fromP3, to3, k);
        }

        private static float Smooth(float k)
        {
            k = MathHelper.Clamp(k, 0f, 1f);
            return k * k * (3f - 2f * k);
        }

        private static Vector2 Turn(Vector2 v, float a) => v.RotatedBy(a);

        /// <summary>A world offset as if the owner faced right and stood the right way up.</summary>
        private static Vector2 Upright(Player owner, Vector2 world) => new Vector2(world.X * owner.direction, world.Y * owner.gravDir);

        /// <summary>The curve at <paramref name="s"/> (0 at the root, 1 at the sting), in the world.</summary>
        private Vector2 Curve(float s)
        {
            float u = 1f - s;
            Vector2 local = 3f * u * u * s * p1 + 3f * u * s * s * p2 + s * s * s * p3;
            Player owner = Main.player[Projectile.owner];
            return root + new Vector2(local.X * facing, local.Y * owner.gravDir);
        }

        /// <summary>The nine places along the curve, evenly by length, and the way it runs at each.</summary>
        public void Places(Vector2[] at, float[] angle)
        {
            const int Fine = 48;
            Span<Vector2> points = stackalloc Vector2[Fine + 1];
            Span<float> lengths = stackalloc float[Fine + 1];

            for (int i = 0; i <= Fine; i++)
            {
                points[i] = Curve(i / (float)Fine);
                lengths[i] = i == 0 ? 0f : lengths[i - 1] + Vector2.Distance(points[i], points[i - 1]);
            }

            float total = Math.Max(1f, lengths[Fine]);
            int j = 1;

            for (int k = 0; k < Segments; k++)
            {
                float want = total * (k + 1f) / Segments;

                while (j < Fine && lengths[j] < want)
                    j++;

                float span = Math.Max(0.0001f, lengths[j] - lengths[j - 1]);
                float f = MathHelper.Clamp((want - lengths[j - 1]) / span, 0f, 1f);
                at[k] = Vector2.Lerp(points[j - 1], points[j], f);
                angle[k] = (points[j] - points[j - 1]).ToRotation();
            }
        }

        /// <summary>The sting's point.</summary>
        private Vector2 Tip() => Curve(1f);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 tip = Projectile.Center;
            Rectangle sting = new Rectangle((int)tip.X - 12, (int)tip.Y - 12, 24, 24);

            if (sting.Intersects(targetHitbox))
                return true;

            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), lastTip, tip, 14f, ref point);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, PoisonTicks);

            if (Main.dedServ)
                return;

            Vector2 at = Vector2.Clamp(Projectile.Center, target.Hitbox.TopLeft(), target.Hitbox.BottomRight());
            Vector2 way = (Projectile.Center - lastTip).SafeNormalize(Vector2.UnitY);

            for (int i = 0; i < 8; i++)
            {
                PADParticleHandler.SpawnParticle(new BloodDroplet(at, -way.RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4.5f) + new Vector2(0f, -1.5f),
                    Main.rand.Next(24, 40), Main.rand.NextFloat(0.6f, 1f), Venom));
            }

            PADParticleHandler.SpawnParticle(new SparkShard(at, way * 2f, Harenaeum.PaleGold, 10f, 3f, 26f, 10));
            Harenaeum.Glint(at, 0.9f);
        }

        /// <summary>The venom: a sour green, lit.</summary>
        public static readonly Color Venom = new Color(150, 226, 92);

        private void Effects(Player owner, Vector2 tip)
        {
            Lighting.AddLight(tip, Harenaeum.Turquoise.ToVector3() * 0.25f);

            // A drop of venom off the sting now and then, at rest.
            if (Mode == Rest && Main.rand.NextBool(70))
                PADParticleHandler.SpawnParticle(new BloodDroplet(tip + new Vector2(0f, 3f), new Vector2(0f, 0.5f), 40, 0.55f, Venom));

            // Sand off the plates as it is thrown.
            if (Mode == Strike && Main.rand.NextBool(2))
            {
                Vector2 along = Curve(Main.rand.NextFloat(0.3f, 1f));
                PADParticleHandler.SpawnParticle(new SandGrain(along, Main.rand.NextVector2Circular(1.5f, 1.5f), Harenaeum.AnySand, Main.rand.Next(14, 24), Main.rand.NextFloat(1f, 1.6f)));
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Player owner = Main.player[Projectile.owner];

            Vector2[] at = places;
            float[] angle = angles;
            Places(at, angle);

            Vector2 rootWorld = root;
            float stretch = Vector2.Distance(at[Segments - 1], rootWorld) / 70f;

            // The sinew: a turquoise line from the root through every plate to the sting, brighter the more it is stretched.
            Color sinew = Harenaeum.Turquoise * MathHelper.Clamp(0.35f + 0.25f * stretch, 0.35f, 0.9f);
            sinew.A = 60;
            Vector2 previous = rootWorld;

            for (int k = 0; k < Segments; k++)
            {
                DrawLine(pixel, previous, at[k], sinew, 2f);
                previous = at[k];
            }

            // The strike's streak: where the sting has just been, pale gold, gone quickly.
            if (Mode == Strike || Mode == Hold)
            {
                for (int i = trail.Length - 1; i > 0; i--)
                {
                    if (trail[i] == Vector2.Zero || trail[i - 1] == Vector2.Zero)
                        continue;

                    float f = 1f - i / (float)trail.Length;
                    Color c = Harenaeum.PaleGold * f * 0.7f;
                    c.A = 0;
                    DrawLine(pixel, trail[i], trail[i - 1], c, 1f + 3f * f);
                }
            }

            // The shine: a brightness that runs up the plates from the root to the sting every few seconds.
            float run = (life % 200) / 60f;

            for (int k = 0; k < Segments; k++)
            {
                bool sting = k == Segments - 1;
                Rectangle frame = new Rectangle(0, sting ? FrameHeight + 2 : 0, FrameWidth, FrameHeight);
                float scale = sting ? 1f : MathHelper.Lerp(1.15f, 0.8f, k / (float)(Segments - 2));

                // Spread along a stretched sinew, the plates lengthen a little too.
                Vector2 size = new Vector2(scale * MathHelper.Clamp(0.9f + 0.15f * stretch, 0.9f, 1.4f), scale);
                float rotation = angle[k];

                // At rest the sting curls down at the end, as a scorpion's does.
                if (sting && (Mode == Rest || Mode == Coil || Mode == Recoil))
                    rotation += 0.7f * facing * owner.gravDir;

                SpriteEffects flip = facing * owner.gravDir < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None;
                Color lit = Lighting.GetColor(at[k].ToTileCoordinates());
                lit = Color.Lerp(lit, Color.White, 0.15f);

                Main.EntitySpriteDraw(texture, at[k] - Main.screenPosition, frame, lit, rotation, frame.Size() * 0.5f, size, flip, 0);

                float near = 1f - Math.Abs(run - k / (float)Segments) * 4f;

                if (near > 0f)
                {
                    Color shine = Harenaeum.Shine * near * 0.55f;
                    shine.A = 0;
                    Main.EntitySpriteDraw(texture, at[k] - Main.screenPosition, frame, shine, rotation, frame.Size() * 0.5f, size, flip, 0);
                }
            }

            // The sting's own light.
            Texture2D glow = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft").Value;
            Color tipGlow = Harenaeum.PaleTurquoise * (Mode == Strike || Mode == Hold ? 0.35f : 0.18f);
            tipGlow.A = 0;
            Main.EntitySpriteDraw(glow, at[Segments - 1] - Main.screenPosition, null, tipGlow, 0f, glow.Size() * 0.5f, 0.12f, SpriteEffects.None, 0);

            return false;
        }

        private static void DrawLine(Texture2D pixel, Vector2 a, Vector2 b, Color colour, float thickness)
        {
            Vector2 d = b - a;
            float length = d.Length();

            if (length < 0.5f)
                return;

            Main.EntitySpriteDraw(pixel, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, d.ToRotation(), new Vector2(0f, 0.5f),
                new Vector2(length, thickness), SpriteEffects.None, 0);
        }
    }
}
