using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Items.Misc;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Magic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Summoner
{
    /// <summary>
    /// Jelle's sand, lent by her Sable Helper (the item is Content/Items/Summoner/JellesSableHelper.cs), by request:
    /// "Sand construct that is basically just particles that rotates around the body, when it faces a enemy hoard, it
    /// becomes a stationary sentry type summon that looks like a balista with range of 60 tiles and very fast
    /// traveling speed (60 tiles a second, asked), shooting sand ballista arrow with 1 sec reload time. after 5 secs
    /// of no enemies, it becomes fluid sands and circle the player again." A horde, asked: three enemies or more, or
    /// a boss. One minion slot each; each another ribbon, another ballista.
    ///
    /// Loose, it is a ribbon of sand going round its owner, a bright head and a tail thinning behind. When three or
    /// more things it could shoot are within its 60 tiles of its owner, or a boss is, it pours itself down onto the
    /// ground a little behind them (each further back than the last) and rises there as a ballista; it turns to what
    /// is nearest, or what its owner has marked, and looses a bolt of packed sand at it every second, aimed where
    /// that will be when the bolt gets there (SableBolt). Five seconds with nothing to shoot, or its owner more than
    /// 75 tiles off, and it slumps back into sand and goes back round them.
    ///
    /// The pictures are placeholders (PlaceholderTextures.md): the stand (this), the bow and its rail
    /// (SableConstructBow), the bolt (SableBolt).
    /// </summary>
    public class SableConstruct : ModProjectile
    {
        /// <summary>What it is: sand going round its owner, pouring itself into a ballista, a ballista, slumping back.</summary>
        public const int Fluid = 0, Forming = 1, Ballista = 2, Melting = 3;

        /// <summary>By request: 60 tiles.</summary>
        public const float Range = 60f * 16f;

        /// <summary>By request: three or more, or a boss.</summary>
        public const int Horde = 3;

        /// <summary>By request: a bolt a second; five seconds of nothing and it goes back.</summary>
        public const int Reload = 60, Idle = 5 * 60;

        /// <summary>By request, 60 tiles a second: 16 pixels a tick.</summary>
        public const float BoltSpeed = 16f;

        /// <summary>Ticks to rise and to slump. Mine.</summary>
        public const int FormTicks = 36, MeltTicks = 30;

        /// <summary>From its owner further than this, it goes back to them. Mine.</summary>
        public const float Leash = 75f * 16f;

        /// <summary>Where on the stand the bow turns, from the stand's middle.</summary>
        public static readonly Vector2 Pivot = new Vector2(0f, -10f);

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

        /// <summary>
        /// On the owner's machine: ticks to the next bolt (counts down), ticks without a target, and ticks before it may
        /// form again after going back to sand for want of anything to shoot (so a horde it cannot see does not raise
        /// and slump it over and over).
        /// </summary>
        private int reload, quiet, rest;

        /// <summary>This machine's look: the bow's angle, its kick after a shot, time out, and the loose sand's place.</summary>
        private float aim = -0.2f;
        private float kick;
        private int life;
        private Vector2 looseCentre;

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool? CanCutTiles() => false;

        public override bool MinionContactDamage() => false;

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            int buff = ModContent.BuffType<JellesSableHelperBuff>();

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
            Timer++;

            switch (Mode)
            {
                case Fluid:
                    Projectile.Center = owner.MountedCenter;
                    looseCentre = owner.MountedCenter;

                    if (mine && rest > 0)
                        rest--;
                    else if (mine && HordeNear(owner))
                        Form(owner, index);
                    break;

                case Forming:
                    looseCentre = owner.MountedCenter;
                    if (Timer >= FormTicks)
                        Go(Ballista);
                    break;

                case Ballista:
                    if (mine)
                        Shoot(owner);
                    break;

                default:
                    looseCentre = owner.MountedCenter;
                    if (Timer >= MeltTicks)
                    {
                        Go(Fluid);
                        Projectile.Center = owner.MountedCenter;
                    }
                    break;
            }

            Turn();

            if (!Main.dedServ)
                Effects(owner, index, count);
        }

        private void Go(int mode)
        {
            Mode = mode;
            Timer = 0f;

            if (Projectile.owner == Main.myPlayer)
                Projectile.netUpdate = true;
        }

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
        /// By request: three or more things it could shoot within its reach of its owner, or a boss; only what its owner
        /// can see (not what is behind the rock).
        /// </summary>
        private bool HordeNear(Player owner)
        {
            int many = 0;

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!npc.CanBeChasedBy(Projectile) || Vector2.Distance(npc.Center, owner.Center) > Range
                    || !Collision.CanHitLine(owner.position, owner.width, owner.height, npc.position, npc.width, npc.height))
                    continue;

                if (npc.boss)
                    return true;

                if (++many >= Horde)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Down onto the ground a little behind its owner, each ballista further back than the one before: where
        /// the ground is under that, within 15 tiles; in the air where they are if there is none.
        /// </summary>
        private void Form(Player owner, int index)
        {
            Vector2 over = owner.Bottom + new Vector2(-owner.direction * (36f + index * 44f), -20f);

            if (CourtOfHandsRite.FindGround(over, out Vector2 ground) && ground.Y - over.Y < 15f * 16f)
                Projectile.Bottom = ground;
            else
                Projectile.Center = owner.MountedCenter + new Vector2(-owner.direction * (36f + index * 44f), 0f);

            reload = Reload / 2;
            quiet = 0;
            Go(Forming);
        }

        /// <summary>The ballista at work: what to shoot, a bolt a second at it, and back to sand when there has long been nothing.</summary>
        private void Shoot(Player owner)
        {
            PickTarget(owner);
            NPC target = Target;

            if (Vector2.Distance(owner.Center, Projectile.Center) > Leash)
            {
                Go(Melting);
                return;
            }

            if (target == null)
            {
                if (++quiet >= Idle)
                {
                    rest = Idle;
                    Go(Melting);
                }

                return;
            }

            quiet = 0;

            if (--reload > 0)
                return;

            Vector2 from = Muzzle();
            Vector2 at = Lead(from, target);
            Vector2 velocity = (at - from).SafeNormalize(Vector2.UnitX) * BoltSpeed;

            // Shot as two half-steps a tick (SableBolt's extra update), sixteen pixels a tick in all.
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), from, velocity * 0.5f, ModContent.ProjectileType<SableBolt>(),
                Projectile.damage, Projectile.knockBack, Projectile.owner, Projectile.identity);

            reload = Reload;
            Projectile.netUpdate = true;
        }

        /// <summary>Where <paramref name="target"/> will be when a bolt from <paramref name="from"/> gets there, going as it goes now.</summary>
        private static Vector2 Lead(Vector2 from, NPC target)
        {
            Vector2 at = target.Center;

            for (int i = 0; i < 3; i++)
            {
                float ticks = Vector2.Distance(from, at) / BoltSpeed;
                at = target.Center + target.velocity * ticks;
            }

            return at;
        }

        /// <summary>The marked enemy; or the nearest in its reach and in its sight.</summary>
        private void PickTarget(Player owner)
        {
            int pick = -1;
            Vector2 from = Muzzle();
            NPC marked = Projectile.OwnerMinionAttackTargetNPC;

            if (marked != null && marked.CanBeChasedBy(Projectile) && Vector2.Distance(marked.Center, from) < Range)
                pick = marked.whoAmI;
            else
            {
                float nearest = Range;

                foreach (NPC npc in Main.ActiveNPCs)
                {
                    if (!npc.CanBeChasedBy(Projectile))
                        continue;

                    float distance = Vector2.Distance(npc.Center, from);

                    if (distance < nearest && Collision.CanHitLine(from - new Vector2(3f), 6, 6, npc.position, npc.width, npc.height))
                    {
                        nearest = distance;
                        pick = npc.whoAmI;
                    }
                }
            }

            if ((int)Projectile.ai[2] != pick + 1)
            {
                Projectile.ai[2] = pick + 1;
                Projectile.netUpdate = true;
            }
        }

        /// <summary>Where a bolt leaves: the end of the rail.</summary>
        private Vector2 Muzzle() => Projectile.Center + Pivot + aim.ToRotationVector2() * 18f;

        /// <summary>The bow turned toward what it shoots at, on every machine, a little at a time; and its kick dying away.</summary>
        private void Turn()
        {
            kick *= 0.82f;

            NPC target = Target;

            if (Mode != Ballista || target == null)
                return;

            Vector2 from = Projectile.Center + Pivot;
            float want = (Lead(from, target) - from).ToRotation();
            aim = aim.AngleLerp(want, 0.25f);
        }

        /// <summary>Called on every machine when a bolt leaves (SableBolt's first tick): the bow kicks back, sand blows off the rail.</summary>
        public void Loosed(Vector2 at, Vector2 velocity)
        {
            kick = 1f;
            reloadLook = 0;

            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.DD2_BallistaTowerShot with { Volume = 0.6f, Pitch = 0.25f, PitchVariance = 0.1f }, at);
            Harenaeum.Burst(at, velocity, 12, 4f, 0.5f, 1f);
        }

        /// <summary>Ticks since the last bolt left, on this machine, for the next one growing on the rail.</summary>
        private int reloadLook = Reload;

        private void Effects(Player owner, int index, int count)
        {
            reloadLook++;

            if (Mode == Fluid)
            {
                if (Main.rand.NextBool(5))
                {
                    Vector2 at = LoosePoint(owner, index, count, Main.rand.Next(0, 20));
                    PADParticleHandler.SpawnParticle(new SandGrain(at, Main.rand.NextVector2Circular(0.6f, 0.6f), Harenaeum.AnySand, Main.rand.Next(14, 24), Main.rand.NextFloat(0.8f, 1.4f)));
                }

                if (life % 40 == index * 7 % 40)
                    Harenaeum.Glint(LoosePoint(owner, index, count, 0), 0.5f);

                return;
            }

            Vector2 stand = Projectile.Center;

            if (Mode == Forming || Mode == Melting)
            {
                // The sand going down to it, or coming back up from it.
                for (int i = 0; i < 3; i++)
                {
                    Vector2 from = Mode == Forming ? owner.MountedCenter + Main.rand.NextVector2Circular(30f, 22f) : stand + Main.rand.NextVector2Circular(14f, 12f);
                    Vector2 to = Mode == Forming ? stand + Main.rand.NextVector2Circular(14f, 12f) : owner.MountedCenter;
                    Vector2 velocity = (to - from) * Main.rand.NextFloat(0.05f, 0.09f);
                    PADParticleHandler.SpawnParticle(new SandGrain(from, velocity, Harenaeum.AnySand, Main.rand.Next(16, 26), Main.rand.NextFloat(1f, 1.8f)));
                }

                if (Timer == 1f)
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = Mode == Forming ? -0.3f : 0.3f }, stand);

                return;
            }

            // A ballista of sand loses a little of itself as it stands.
            if (Main.rand.NextBool(8))
            {
                Vector2 at = stand + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-8f, 12f));
                PADParticleHandler.SpawnParticle(new SandGrain(at, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 0.4f), Harenaeum.AnySand, Main.rand.Next(18, 30), Main.rand.NextFloat(0.8f, 1.3f)));
            }
        }

        /// <summary>A grain of the loose ribbon, <paramref name="behind"/> grains back from its head.</summary>
        private Vector2 LoosePoint(Player owner, int index, int count, float behind)
        {
            float phase = MathHelper.TwoPi * index / Math.Max(1, count);
            float angle = life * 0.075f + phase - behind * 0.085f;
            float wobble = (float)Math.Sin(behind * 0.6f + life * 0.11f) * 4f;
            float radius = 32f + 4f * (float)Math.Sin(life * 0.02f + index) + wobble;
            return looseCentre + new Vector2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius * 0.62f - 4f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Group(out int index, out int count);
            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // The loose ribbon: while loose, and thinning while it pours into the ballista or out of it.
            float loose = Mode switch
            {
                Fluid => 1f,
                Forming => 1f - Timer / FormTicks,
                Melting => Timer / MeltTicks,
                _ => 0f,
            };

            if (loose > 0.01f)
            {
                const int Grains = 40;

                for (int j = Grains - 1; j >= 0; j--)
                {
                    float f = 1f - j / (float)Grains;
                    Vector2 at = LoosePoint(owner, index, count, j);
                    Vector2 next = LoosePoint(owner, index, count, j - 0.6f);
                    Color c = Harenaeum.Sand[(j + index) % Harenaeum.Sand.Length];
                    Color light = Lighting.GetColor(at.ToTileCoordinates());
                    float lit = Math.Max(0.6f, Math.Max(light.R, Math.Max(light.G, light.B)) / 255f);
                    Color drawn = new Color((int)(c.R * lit), (int)(c.G * lit), (int)(c.B * lit)) * (f * loose);
                    Vector2 d = next - at;

                    Main.EntitySpriteDraw(pixel, at - Main.screenPosition, new Rectangle(0, 0, 1, 1), drawn, d.ToRotation(), new Vector2(0f, 0.5f),
                        new Vector2(Math.Max(1.5f, d.Length() * 1.4f), j < 6 ? 2.2f : 1.4f), SpriteEffects.None, 0);
                }

                // The head, bright.
                Color head = Harenaeum.Shine * 0.6f * loose;
                head.A = 0;
                Main.EntitySpriteDraw(pixel, LoosePoint(owner, index, count, 0) - Main.screenPosition, new Rectangle(0, 0, 1, 1), head, 0f,
                    new Vector2(0.5f, 0.5f), 3f, SpriteEffects.None, 0);
            }

            // The ballista: risen from the bottom up as it forms, sunk from the top down as it slumps.
            float up = Mode switch
            {
                Forming => Timer / FormTicks,
                Ballista => 1f,
                Melting => 1f - Timer / MeltTicks,
                _ => 0f,
            };

            if (up > 0.01f)
                DrawBallista(up);

            return false;
        }

        private void DrawBallista(float up)
        {
            Texture2D stand = TextureAssets.Projectile[Type].Value;
            Texture2D bow = ModContent.Request<Texture2D>(Texture + "Bow").Value;
            Texture2D bolt = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Summoner/SableBolt").Value;

            Vector2 bottom = Projectile.Bottom;
            Color lit = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            lit = Color.Lerp(lit, Harenaeum.PaleGold, 0.12f);
            up = MathHelper.Clamp(up, 0f, 1f);

            // The stand, up from its foot: only the lowest part of the picture while it rises.
            int shown = Math.Max(1, (int)(stand.Height * up));
            Rectangle part = new Rectangle(0, stand.Height - shown, stand.Width, shown);
            Main.EntitySpriteDraw(stand, bottom - Main.screenPosition, part, lit, 0f, new Vector2(stand.Width * 0.5f, shown), 1f, SpriteEffects.None, 0);

            if (up < 0.55f)
                return;

            // The bow, turned to its aim, back a little after a shot; the bolt on its rail, grown back as it reloads.
            float bowUp = MathHelper.Clamp((up - 0.55f) / 0.45f, 0f, 1f);
            Vector2 pivot = Projectile.Center + Pivot + new Vector2(0f, (1f - bowUp) * 10f) - aim.ToRotationVector2() * kick * 4f;
            SpriteEffects flip = Math.Abs(MathHelper.WrapAngle(aim)) > MathHelper.PiOver2 ? SpriteEffects.FlipVertically : SpriteEffects.None;
            Color bowColour = lit * bowUp;

            float grown = MathHelper.Clamp(reloadLook / (float)Reload, 0f, 1f);

            if (Mode == Ballista && grown > 0.05f)
            {
                Vector2 rail = pivot + aim.ToRotationVector2() * (bolt.Width * 0.5f - 6f) * grown;
                Main.EntitySpriteDraw(bolt, rail - Main.screenPosition, null, lit * grown, aim, bolt.Size() * 0.5f, new Vector2(grown, 1f), flip, 0);
            }

            Main.EntitySpriteDraw(bow, pivot - Main.screenPosition, null, bowColour, aim, new Vector2(bow.Width * 0.4f, bow.Height * 0.5f), 1f, flip, 0);
        }
    }
}
