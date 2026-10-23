using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Melee
{
    /// <summary>
    /// The Defacer in the hands, for one blow: held down it is lifted up and back over
    /// <see cref="WindTicks"/> (less with melee speed), let go it comes down over <see cref="BlowTicks"/>,
    /// the toothed face leading, and is brought back. What it hits takes from
    /// <see cref="LeastDamage"/> to <see cref="FullDamage"/> times its damage, by how far it was lifted;
    /// lifted all the way, what it hits is Defaced (Defaced) and stone chips fly. The ground stops it,
    /// as it does the Flesh Maiden: the face is tried against solid tiles from the time it passes
    /// level, and it stays where it met them, dust and chips going up.
    ///
    /// Held the way VisusGrip holds a polearm, both hands on the handle; drawn in the hands by
    /// DefacerLayer (from DefacerPose); this draws what the blow leaves behind the player: shadows
    /// of the hammer along the way it came, and a smear of grey.
    ///
    /// The owner lifts it and lets it go: ai[0] goes to 1 on the release and ai[1] is how far it was
    /// lifted, both sent; ai[2] is the side it faces. Every machine lifts and brings it down alike.
    /// </summary>
    public class DefacerHoldout : ModProjectile
    {
        public const string HammerTexture = "PisAndDag/Content/Items/Melee/Defacer";

        // ---- the sprite (Defacer.png): the handle on the diagonal, butt low left, head up right ----

        /// <summary>The sprite's width and height, in texels.</summary>
        public const int Size = 68;

        /// <summary>The butt's end, in texels from the top left; the handle runs up and right from it at 45 degrees.</summary>
        public static readonly Vector2 Butt = new Vector2(5.5f, 62.5f);

        /// <summary>
        /// How much longer the haft was made, along it, by request ("defacer weapon, double range"): twenty
        /// texels up and twenty across, so the head is twice as far past the hands as it was (56 texels, not 28).
        /// </summary>
        public const float Lengthened = 20f * 1.4142136f;

        /// <summary>Along the handle from the butt: where the front hand holds it, and the middle of the head.</summary>
        public const float GripAlong = 9f;
        public const float HeadAlong = 37f + Lengthened;

        /// <summary>Across the handle from its line: the toothed face (+, toward the blow) and the other face.</summary>
        public const float FaceAcross = 11f;
        public const float PeenAcross = -11f;

        // ---- the blow ------------------------------------------------------------------------------------

        public const int WindTicks = 42;
        public const int BlowTicks = 14;
        public const int AfterTicks = 10;

        /// <summary>Ticks it stays where the ground stopped it before it is brought back.</summary>
        private const int GroundStay = 6;

        public const float LeastDamage = 0.55f;
        public const float FullDamage = 2.2f;

        /// <summary>Its wielder's run while it is lifted: this share of their speed.</summary>
        public const float LiftedSpeed = 0.7f;

        /// <summary>Degrees from straight ahead, down positive: at rest, lifted all the way, the bottom of the blow, brought back.</summary>
        private const float RestAngle = 30f, LiftedAngle = -150f, DownAngle = 75f, AfterAngle = 50f;

        /// <summary>Ticks the first thing it hits holds it still, and two more lifted all the way.</summary>
        private const int Stall = 3;

        /// <summary>How much of the blow the smear trails behind the head.</summary>
        private const float Trail = 0.22f;

        public static readonly Color Stone = new Color(176, 170, 160);
        public static readonly Color StoneDark = new Color(84, 80, 92);
        public static readonly Color StoneLight = new Color(232, 226, 210);
        private static readonly Color[] Grit = { new Color(236, 230, 214), new Color(190, 184, 172), new Color(140, 134, 132) };

        private static Asset<Texture2D> smear;
        private static Asset<Texture2D> line;

        public override string Texture => HammerTexture;

        private Player Owner => Main.player[Projectile.owner];

        private bool Mine => Projectile.owner == Main.myPlayer;

        private int Facing => Projectile.ai[2] < 0f ? -1 : 1;

        private bool Released => Projectile.ai[0] == 1f;

        /// <summary>How far it was lifted, 0 to 1: as it is being lifted, and as it was let go once it comes down.</summary>
        private float charge;

        private float lifted;
        private bool topped;

        private bool started;
        private float blowFrom;
        private float blowTick;
        private float afterTick;
        private float endAngle;

        /// <summary>Degrees, this tick and last.</summary>
        private float angle = RestAngle;
        private float previousAngle = RestAngle;

        private float ground = float.NaN;
        private int stall;
        private bool struck;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            smear = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Smear");
            line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            smear = null;
            line = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.aiStyle = -1;

            // Friendly throughout: what it can hit, and when, is Colliding's (Hitting).
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;
            Projectile.noEnchantmentVisuals = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>The wielder's speed with it: vanilla's, as for any weapon.</summary>
        private float Speed(Player player) => Math.Max(0.2f, CombinedHooks.TotalUseSpeedMultiplier(player, player.HeldItem));

        private bool Grounded => !float.IsNaN(ground);

        private bool Full => charge >= 0.999f;

        public override void AI()
        {
            Player player = Owner;

            if (!player.active || player.dead || player.CCed || player.noItems || player.HeldItem.ModItem is not Defacer)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            player.itemTime = 2;
            player.itemAnimation = 2;

            if (Mine)
            {
                Item item = player.HeldItem;
                Projectile.damage = player.GetWeaponDamage(item);
                Projectile.knockBack = player.GetWeaponKnockback(item);
                Projectile.CritChance = player.GetWeaponCrit(item);
                Projectile.ArmorPenetration = player.GetWeaponArmorPenetration(item);
            }

            if (stall > 0)
            {
                stall--;
                Pose(player);
                return;
            }

            if (!Released)
                Lift(player);
            else
                Bring(player);

            if (Projectile.active)
                Pose(player);
        }

        // ---- lifted ------------------------------------------------------------------------------------

        private void Lift(Player player)
        {
            lifted++;
            charge = MathHelper.Clamp(lifted * Speed(player) / WindTicks, 0f, 1f);

            previousAngle = angle;
            angle = MathHelper.Lerp(RestAngle, LiftedAngle, Easing.Apply(Ease.PolyOut, charge, 2));

            // Held up, it is not quite still.
            if (Full)
                angle += 3f * (float)Math.Sin(lifted * 0.2f);

            if (Mine)
            {
                int side = Main.MouseWorld.X < player.Center.X ? -1 : 1;

                if (side != Facing)
                {
                    Projectile.ai[2] = side;
                    Projectile.netUpdate = true;
                }
            }

            player.ChangeDir(Facing);
            player.GetModPlayer<DefacerPose>().Slow = 2;

            if (!Main.dedServ)
                Lifting(player);

            if (Mine && !player.channel)
            {
                Projectile.ai[0] = 1f;
                Projectile.ai[1] = charge;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>Grit off the teeth as it goes up; at the top a glint, and a tick of stone.</summary>
        private void Lifting(Player player)
        {
            Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.ThreeQuarters, Rad(angle));
            Vector2 face = VisusGrip.On(player, Rad(angle), hand, GripAlong, HeadAlong, FaceAcross);

            if (lifted == 1f)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.7f, Volume = 0.5f }, face);

            if (Main.rand.NextBool(Full ? 6 : 10))
            {
                PADParticleHandler.SpawnParticle(new StoneChip(face + Main.rand.NextVector2Circular(4f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.2f, 0.4f)), Grit[Main.rand.Next(Grit.Length)],
                    Main.rand.Next(30, 50), 0.8f));
            }

            if (Full && !topped)
            {
                topped = true;
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.6f, Volume = 0.55f }, face);
                PADParticleHandler.SpawnParticle(new CrownGlint(face, 22f, 18, StoneLight));
            }
        }

        // ---- brought down --------------------------------------------------------------------------------

        private void Bring(Player player)
        {
            if (!started)
            {
                started = true;
                charge = MathHelper.Clamp(Projectile.ai[1], 0f, 1f);
                blowFrom = angle;
                Projectile.ResetLocalNPCHitImmunity();

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = -0.7f, Volume = 0.55f + 0.3f * charge }, player.Center);

                    if (Full)
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 0.5f }, player.Center);
                }
            }

            player.ChangeDir(Facing);

            float length = Math.Max(6f, BlowTicks / Speed(player));
            previousAngle = angle;

            if (!Grounded && blowTick < length)
            {
                blowTick++;
                angle = BlowAngle(blowTick / length);

                if (angle > 5f)
                    FindGround(player);

                if (blowTick >= length && !Grounded)
                    endAngle = angle;

                return;
            }

            afterTick++;

            float from = Grounded ? ground : endAngle;
            float wait = Grounded ? GroundStay : 0f;
            float t = MathHelper.Clamp((afterTick - wait) / AfterTicks, 0f, 1f);

            angle = MathHelper.Lerp(from, AfterAngle, Easing.Apply(Ease.PolyOut, t, 2));

            if (afterTick >= wait + AfterTicks)
                Projectile.Kill();
        }

        /// <summary>Where the blow has it at <paramref name="t"/>, 0 to 1: slow off the top, fastest at the bottom.</summary>
        private float BlowAngle(float t) => MathHelper.Lerp(blowFrom, DownAngle, Easing.Apply(Ease.PolyIn, MathHelper.Clamp(t, 0f, 1f), 2));

        /// <summary>It hits from a little way into the blow until it is down, and two ticks after the ground stops it.</summary>
        private bool Hitting => Released && started && (Grounded ? afterTick <= 2f : blowTick > 1f && afterTick <= 0f);

        private static float Rad(float degrees) => MathHelper.ToRadians(degrees);

        /// <summary>The toothed face tried against solid tiles: where it first met them between last tick and this, it stops.</summary>
        private void FindGround(Player player)
        {
            const int Steps = 6;

            for (int i = 1; i <= Steps; i++)
            {
                float a = MathHelper.Lerp(previousAngle, angle, i / (float)Steps);
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, Rad(a));
                Vector2 face = VisusGrip.On(player, Rad(a), hand, GripAlong, HeadAlong, FaceAcross);

                if (!Collision.SolidCollision(face - new Vector2(2f), 4, 4))
                    continue;

                ground = a;
                angle = a;

                if (!Main.dedServ)
                    Land(player, face, a);

                return;
            }
        }

        /// <summary>Which way the head is going at <paramref name="a"/>, in the world: across the handle, toward the face.</summary>
        private Vector2 Travel(Player player, float a)
        {
            Vector2 u = Rad(a).ToRotationVector2();
            return new Vector2(-u.Y * player.direction, u.X * player.gravDir);
        }

        /// <summary>The blow meets the ground: a crack of stone, chips and dust up out of it, the floor heaved if it was lifted all the way.</summary>
        private void Land(Player player, Vector2 contact, float a)
        {
            Vector2 travel = Travel(player, a);
            float k = 0.4f + 0.6f * charge;

            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Pitch = -0.3f, Volume = 0.85f * k }, contact);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.4f, Volume = 0.7f * k }, contact);

            Collision.HitTiles(contact - new Vector2(8f), travel * 5f, 16, 16);
            Chips(contact, -travel, (int)(4 + 9 * charge), 2.5f + 3f * charge);

            PADParticleHandler.SpawnParticle(new DustFog(contact - new Vector2(0f, 6f), new Vector2(0f, -0.4f), Stone * 0.8f, 40, 0.6f + 0.5f * charge, 0.35f));

            if (Full)
            {
                SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Pitch = 0.5f, Volume = 0.3f }, contact);
                GroundWave.Strike(contact, -travel, 10f, 3, 3.5f, 22);
            }

            if (Mine)
                player.PIS().BumpScreenShake(2.5f + 4f * charge, 6 + (int)(8 * charge));
        }

        /// <summary>Stone thrown off the face: chips that fall and bounce, and a few white splinters.</summary>
        private static void Chips(Vector2 at, Vector2 away, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = away.RotatedByRandom(1.1f) * speed * Main.rand.NextFloat(0.5f, 1.2f) + new Vector2(0f, -1.5f);
                PADParticleHandler.SpawnParticle(new StoneChip(at + Main.rand.NextVector2Circular(4f, 4f), velocity,
                    Grit[Main.rand.Next(Grit.Length)], Main.rand.Next(40, 70), Main.rand.NextFloat(0.9f, 1.3f)));
            }

            SparkShard.Spray(at, StoneLight, Math.Max(2, count / 3), speed * 0.9f, 16f, 10, falls: true, palette: Grit);
        }

        /// <summary>
        /// Points down the haft that strike too, from the hands to the head: with the haft as long as it is,
        /// what is up against its wielder is inside the head's sweep, and would be missed by the head alone.
        /// </summary>
        private static readonly float[] HaftAlong = { GripAlong + 8f, GripAlong + 18f, GripAlong + 28f, GripAlong + 38f };

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!Hitting)
                return false;

            Player player = Owner;
            Rectangle box = targetHitbox;
            box.Inflate(6, 6);

            const int Steps = 6;
            float[] across = { FaceAcross, FaceAcross * 0.4f, 0f, PeenAcross * 0.6f };

            for (int i = 0; i <= Steps; i++)
            {
                float a = MathHelper.Lerp(previousAngle, angle, i / (float)Steps);
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, Rad(a));

                foreach (float c in across)
                {
                    Vector2 point = VisusGrip.On(player, Rad(a), hand, GripAlong, HeadAlong, c);

                    if (box.Contains((int)point.X, (int)point.Y))
                        return true;
                }

                foreach (float along in HaftAlong)
                {
                    Vector2 point = VisusGrip.On(player, Rad(a), hand, GripAlong, along, 0f);

                    if (box.Contains((int)point.X, (int)point.Y))
                        return true;
                }
            }

            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.SourceDamage *= MathHelper.Lerp(LeastDamage, FullDamage, charge);
            modifiers.Knockback *= 0.6f + 0.6f * charge;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Owner;
            Vector2 at = Vector2.Clamp(Projectile.Center, target.TopLeft, target.BottomRight);

            if (Full)
                target.AddBuff(ModContent.BuffType<Defaced>(), Defaced.Ticks);

            if (!struck)
            {
                struck = true;
                stall = Stall + (Full ? 2 : 0);

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.2f, Volume = 0.6f + 0.4f * charge }, at);

                    if (Full)
                        SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Pitch = 0.6f, Volume = 0.35f }, at);

                    if (Mine)
                        player.PIS().BumpScreenShake(2f + 4.5f * charge, 6 + (int)(6 * charge));
                }
            }

            if (Main.dedServ)
                return;

            Chips(at, -Travel(player, angle), Full ? 12 : (int)(2 + 5 * charge), 2f + 3.5f * charge);

            if (Full)
                PADParticleHandler.SpawnParticle(new SoftGlow(at, Vector2.Zero, StoneLight * 0.45f, 0.38f, 14, false));
        }

        // ---- the hands -----------------------------------------------------------------------------------

        private void Pose(Player player)
        {
            Player.CompositeArmStretchAmount stretch = Released ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.ThreeQuarters;
            float a = Rad(angle);

            Vector2 hand = VisusGrip.FrontHand(stretch, a);
            float back = VisusGrip.BackArmOnto(stretch, hand, a);

            player.SetCompositeArmFront(true, stretch, VisusGrip.ArmRotation(player, a));
            player.SetCompositeArmBack(true, stretch, VisusGrip.ArmRotation(player, back));

            DefacerPose pose = player.GetModPlayer<DefacerPose>();
            pose.Held = true;
            pose.Angle = a;
            pose.Hand = hand;
            pose.Glow = Released ? 0f : (Full ? 0.35f + 0.2f * (float)Math.Sin(lifted * 0.25f) : 0f);

            Projectile.Center = VisusGrip.On(player, a, hand, GripAlong, HeadAlong, 0f);
            Projectile.direction = Projectile.spriteDirection = player.direction;
        }

        // ---- what the blow leaves ----------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            if (!Released || !started)
                return false;

            Player player = Owner;
            float length = Math.Max(6f, BlowTicks / Speed(player));
            float head = MathHelper.Clamp(blowTick / length, 0f, 1f);
            float fade = MathHelper.Clamp(1f - afterTick / 8f, 0f, 1f);

            if (head <= 0.05f || fade <= 0f)
                return false;

            DrawShadows(player, head, length, fade);

            float tail = Math.Max(0f, head - Trail);

            if (smear != null && line != null && head - tail > 0.01f && charge > 0.2f)
            {
                Main.spriteBatch.End();
                DrawSmear(player, head, tail, fade);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                    Main.Rasterizer, null, Main.Transform);
            }

            return false;
        }

        /// <summary>The angle along the blow, stopped where the ground stopped it.</summary>
        private float SweptAngle(float t)
        {
            float a = BlowAngle(t);
            return Grounded ? Math.Min(a, ground) : a;
        }

        /// <summary>Three shadows of the hammer along the way it came.</summary>
        private void DrawShadows(Player player, float head, float length, float fade)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Type].Value;
            bool flip = (player.direction < 0) != (player.gravDir < 0f);

            for (int i = 3; i >= 1; i--)
            {
                float p = head - 0.06f * i;

                if (p <= 0f)
                    continue;

                float a = SweptAngle(p);

                if (Math.Abs(a - angle) < 1f)
                    continue;

                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, Rad(a));
                Vector2 at = VisusGrip.ToWorld(player, hand) - Main.screenPosition;
                DefacerLayer.Place(VisusGrip.ToWorldAngle(player, Rad(a)), flip, out float turn, out Vector2 origin, out SpriteEffects effects);

                float strength = 0.4f * (1f - i / 4f) * fade * (0.4f + 0.6f * charge);
                Main.EntitySpriteDraw(texture, at, null, new Color(40, 38, 48, 255) * strength, turn, origin, 1f, effects, 0);
            }
        }

        private const int Steps = 20;

        private static readonly Vector2[] SmearInner = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearOuter = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearEdgeIn = new Vector2[Steps + 1];
        private static readonly Vector2[] SmearEdgeOut = new Vector2[Steps + 1];
        private static readonly float[] SmearStrength = new float[Steps + 1];

        /// <summary>
        /// The smear over the sweep from <paramref name="tail"/> to <paramref name="head"/>: across the
        /// head from the far face to past the teeth, a band of dust darkening what is behind, and along
        /// the teeth a line of pale stone light, strongest at the head; stronger the further it was lifted.
        /// </summary>
        private void DrawSmear(Player player, float head, float tail, float fade)
        {
            float k = (0.3f + 0.7f * charge) * fade;

            for (int i = 0; i <= Steps; i++)
            {
                float s = i / (float)Steps;
                float a = Rad(SweptAngle(MathHelper.Lerp(head, tail, s)));
                Vector2 hand = VisusGrip.FrontHand(Player.CompositeArmStretchAmount.Full, a);

                SmearInner[i] = VisusGrip.On(player, a, hand, GripAlong, HeadAlong - 7f, PeenAcross * 0.4f);
                SmearOuter[i] = VisusGrip.On(player, a, hand, GripAlong, HeadAlong + 6f, FaceAcross + 3f);
                SmearEdgeIn[i] = VisusGrip.On(player, a, hand, GripAlong, HeadAlong, FaceAcross - 2f);
                SmearEdgeOut[i] = VisusGrip.On(player, a, hand, GripAlong, HeadAlong, FaceAcross + 2f);
                SmearStrength[i] = (float)Math.Pow(1f - s, 1.4f) * k;
            }

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color dust = new Color(34, 32, 40, 255) * (0.55f * SmearStrength[i]);
                Strip.Pair(SmearInner[i], SmearOuter[i], dust, dust, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color pale = Color.Lerp(StoneLight, Stone, i / (float)Steps) * (0.5f * SmearStrength[i]);
                pale.A = 0;
                Strip.Pair(SmearInner[i], SmearOuter[i], pale, pale, i / (float)Steps);
            }
            Strip.Draw(smear.Value);

            Strip.Begin();
            for (int i = 0; i <= Steps; i++)
            {
                Color hot = Color.Lerp(Color.White, StoneLight, i / (float)Steps) * (0.85f * SmearStrength[i]);
                hot.A = 0;
                Strip.Pair(SmearEdgeIn[i], SmearEdgeOut[i], hot, hot, i / (float)Steps);
            }
            Strip.Draw(line.Value);
        }
    }

    /// <summary>
    /// How the Defacer is held this tick, for DefacerLayer to draw it: the handle's angle and the front
    /// hand, in the player's own frame (VisusGrip's), and how much the lifted hammer glows. And the
    /// wielder's run, slowed while it is lifted (<see cref="Slow"/>, set every tick it is: it is read
    /// in the player's update, before the hammer is moved).
    /// </summary>
    public class DefacerPose : ModPlayer
    {
        public bool Held;
        public float Angle;
        public Vector2 Hand;
        public float Glow;

        /// <summary>Ticks left of the slowed run.</summary>
        public int Slow;

        public override void ResetEffects()
        {
            Held = false;
        }

        public override void PostUpdateRunSpeeds()
        {
            if (Slow <= 0)
                return;

            Player.maxRunSpeed *= DefacerHoldout.LiftedSpeed;
            Player.accRunSpeed *= DefacerHoldout.LiftedSpeed;
        }

        public override void PostUpdate()
        {
            if (Slow > 0)
                Slow--;
        }
    }

    /// <summary>
    /// Draws the Defacer in the player's hands where vanilla draws a held item behind the front arm
    /// (IronFlowLayer's slot): the hand closes over the handle. Lifted all the way, the hammer is drawn
    /// a second time in added light, breathing. Only while DefacerPose.Held.
    /// </summary>
    public class DefacerLayer : PlayerDrawLayer
    {
        private static Asset<Texture2D> hammer;

        public override void Load()
        {
            if (!Main.dedServ)
                hammer = ModContent.Request<Texture2D>(DefacerHoldout.HammerTexture);
        }

        public override void Unload()
        {
            hammer = null;
        }

        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.SolarShield, PlayerDrawLayers.ArmOverItem);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
            drawInfo.shadow == 0f && drawInfo.drawPlayer.GetModPlayer<DefacerPose>().Held;

        /// <summary>The front hand's texel on the sprite: along the handle from the butt.</summary>
        public static Vector2 Grip => DefacerHoldout.Butt + new Vector2(1f, -1f) * (DefacerHoldout.GripAlong / (float)Math.Sqrt(2.0));

        /// <summary>
        /// How to draw the sprite held at <paramref name="rotation"/> in the world, mirrored across its
        /// handle when <paramref name="flip"/>: the handle already leans an eighth of a turn up, so it
        /// turns an eighth less than it is held; mirrored top to bottom it leans an eighth down
        /// instead, and the grip's texel goes with the mirror (IronFlowLayer's rule).
        /// </summary>
        public static void Place(float rotation, bool flip, out float turn, out Vector2 origin, out SpriteEffects effects)
        {
            Vector2 grip = Grip;

            origin = flip ? new Vector2(grip.X, DefacerHoldout.Size - grip.Y) : grip;
            turn = flip ? rotation - MathHelper.PiOver4 : rotation + MathHelper.PiOver4;
            effects = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            if (hammer == null)
                return;

            Player player = drawInfo.drawPlayer;
            DefacerPose pose = player.GetModPlayer<DefacerPose>();

            Vector2 centre = drawInfo.Position + (player.MountedCenter - player.position);

            Vector2[] walks = Main.OffsetsPlayerHeadgear;
            int frame = player.bodyFrame.Height > 0 ? player.bodyFrame.Y / player.bodyFrame.Height : 0;
            Vector2 walk = frame >= 0 && frame < walks.Length ? walks[frame] : new Vector2(0f, 2f);
            walk.Y -= 2f;

            Vector2 hand = centre + new Vector2(pose.Hand.X * player.direction, pose.Hand.Y * player.gravDir) + walk * player.gravDir;
            Vector2 position = (hand - Main.screenPosition).Floor();

            bool flip = (player.direction < 0) != (player.gravDir < 0f);
            float rotation = VisusGrip.ToWorldAngle(player, pose.Angle);

            Vector2 middle = VisusGrip.ToWorld(player, pose.Hand + pose.Angle.ToRotationVector2() * ((DefacerHoldout.HeadAlong - DefacerHoldout.GripAlong) * 0.6f));
            Color light = Lighting.GetColor(middle.ToTileCoordinates());

            Place(rotation, flip, out float turn, out Vector2 origin, out SpriteEffects effects);

            drawInfo.DrawDataCache.Add(new DrawData(hammer.Value, position, null, light, turn, origin, 1f, effects));

            if (pose.Glow > 0f)
            {
                Color glow = new Color(255, 248, 230, 0) * pose.Glow;
                drawInfo.DrawDataCache.Add(new DrawData(hammer.Value, position, null, glow, turn, origin, 1f, effects));
            }
        }
    }
}
