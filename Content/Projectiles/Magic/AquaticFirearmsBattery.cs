using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Mage;
using PisAndDag.Content.Items.Mage.Spell;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Aquatic Firearms, the guns themselves: a Minishark, a Megashark and an S.D.M.G. stacked
    /// over the caster's head, the strongest on top, each turning to the cursor and firing
    /// musket balls at its own rate for as long as the staff is held.
    ///
    /// What each one does is read off the real gun (<see cref="Stats"/>): its use time, its
    /// speed, its knockback, its crit. So they fire as fast as that gun does in this mod
    /// (PSItem's rates), with vanilla's own scatter for it (<see cref="Scatter"/>), and each
    /// shot is two thirds of what that gun does with a musket ball, (gun + ball) x 2/3,
    /// through the caster's magic damage. No ammunition; magic, not ranged.
    ///
    /// The way in: three waves out of the caster's back, bottom gun first, each curling up
    /// to where its gun will hang; as a wave lands its gun grows out of it, small to full
    /// and a little past, faded to seen. The way out is the same run backwards, top gun
    /// first: each swells, shrinks away, and its wave pours back into the caster.
    ///
    /// Held: the cast's own mana pays for the first <see cref="FreeTicks"/>, then it costs
    /// <see cref="DrainPerSecond"/> a second (less with mana cost reductions), paid off the
    /// bar a point at a time as Court of Hands pays. Let go, run dry, put the staff away or
    /// change the spell and they leave; let go before all three are in and they finish
    /// coming first, so the way back always starts from a full stack.
    ///
    /// ai[0] is the age in ticks. ai[1] is the age it was let go at, 0 while held. The
    /// velocity is the aim, cursor minus the caster's centre, sent by the owner when it
    /// moves. Everything else follows from those three on every client: the waves, the
    /// guns' turning, and the look of each shot (sound, flash, kick). Only the owner fires.
    /// </summary>
    public class AquaticFirearmsBattery : ModProjectile
    {
        public override string Texture => "PisAndDag/Content/Particles/BloomGeneric";

        // ---- the three ---------------------------------------------------------------

        public const int Guns = 3;

        /// <summary>Bottom to top, weakest to strongest.</summary>
        public static readonly int[] GunTypes = { ItemID.Minishark, ItemID.Megashark, ItemID.SDMG };

        /// <summary>Where each hangs over the caster's centre, in pixels, the right way up.</summary>
        private static readonly Vector2[] Hover = { new(0f, -38f), new(0f, -63f), new(0f, -92f) };

        /// <summary>
        /// Vanilla's scatter for each (Player.ItemCheck_Shoot): a whole number from -40 to 40
        /// times this, added to each axis of the velocity. The two sharks share one, the
        /// S.D.M.G. is steadier.
        /// </summary>
        private static readonly float[] Scatter = { 0.01f, 0.01f, 0.005f };

        /// <summary>
        /// The two colours each glows between: the two its own sprite is mostly made of,
        /// lit enough to glow. The Minishark's amber and gunmetal, the Megashark's rose and
        /// cream, the S.D.M.G.'s aqua and indigo.
        /// </summary>
        private static readonly Color[] GlowA = { new(0xd9, 0x8a, 0x2b), new(0xc9, 0x8d, 0x95), new(0x93, 0xe3, 0xec) };
        private static readonly Color[] GlowB = { new(0x9f, 0xb0, 0xb0), new(0xf7, 0xf6, 0xcf), new(0x6b, 0x78, 0xc8) };

        /// <summary>Where the barrels end if the skin has not been made (measured off the vanilla sprites).</summary>
        private static readonly Vector2[] FallbackMuzzle = { new(26f, 3f), new(35f, 3f), new(33f, 2f) };

        /// <summary>The guns' own sounds, this loud: three at once at full volume is a lot.</summary>
        private const float ShotVolume = 0.45f;

        // ---- time ---------------------------------------------------------------------

        /// <summary>The cast's own mana pays for this long. Three seconds.</summary>
        public const int FreeTicks = 180;

        /// <summary>Mana a second after that, before mana cost reductions.</summary>
        public const float DrainPerSecond = 30f;

        /// <summary>One wave leaves the back this long after the one before, bottom gun first.</summary>
        private const int WaveGap = 5;

        /// <summary>Ticks a wave takes from the back to its gun, and back again.</summary>
        private const int WaveTravel = 16;
        private const int ReturnTravel = 16;

        /// <summary>How far along its path a wave's tail trails its head, and how long the tail takes to run in once the head is there.</summary>
        private const float TailLag = 0.5f;
        private const int TailCatch = 8;

        /// <summary>A gun grows in over this long once its wave lands, and is seen fully a little sooner.</summary>
        private const int GrowTicks = 14;
        private const int FadeInTicks = 10;

        /// <summary>Ticks after its wave lands that a gun starts firing.</summary>
        private const int ReadyAfter = 10;

        /// <summary>Going: top gun first, the next this long after; each shrinks over <see cref="ShrinkTicks"/>, and its wave leaves it after <see cref="ReturnDelay"/>.</summary>
        private const int LeaveGap = 6;
        private const int ShrinkTicks = 14;
        private const int ReturnDelay = 4;

        /// <summary>All three are in by now. A let-go before this waits for it.</summary>
        private const int AllIn = (Guns - 1) * WaveGap + WaveTravel + GrowTicks;

        /// <summary>From the start of the way back to the last wave home.</summary>
        private const int LeaveTicks = (Guns - 1) * LeaveGap + ReturnDelay + ReturnTravel + TailCatch;

        // ---- the water ----------------------------------------------------------------

        private static readonly Color Deep = new(0x1b, 0x4f, 0x8a);
        private static readonly Color Sea = new(0x2e, 0x8b, 0xc0);
        private static readonly Color Shallow = new(0x7f, 0xe3, 0xf0);
        private static readonly Color Foam = new(0xea, 0xfc, 0xff);
        private static readonly Color WaterEdge = new(0x12, 0x31, 0x5c);

        private const int RibbonPoints = 26;
        private static readonly Vector2[] ribbon = new Vector2[RibbonPoints];
        private static readonly Vector2[] ribbonSlope = new Vector2[RibbonPoints];

        // ---- state ----------------------------------------------------------------------

        private int Age => (int)Projectile.ai[0];

        private bool Leaving => Projectile.ai[1] > 0f;

        /// <summary>When the way back starts: when it was let go, but not before all three are in.</summary>
        private int LeaveStart => Math.Max((int)Projectile.ai[1], AllIn);

        /// <summary>Each gun's facing, and how far its last shot kicked it back. Every client keeps its own.</summary>
        private float[] turn;
        private float[] kick;
        private bool[] turned;

        /// <summary>
        /// Which way the caster faced when the waves went out, and when they came back. The
        /// waves keep to the side they started on even if the caster turns round.
        /// </summary>
        private int outDir;
        private int backDir;

        /// <summary>
        /// The one-off sounds and bursts this client has had, a bit each (<see cref="Once"/>),
        /// so a tick skipped or repeated by a sync cannot lose one or play it twice.
        /// </summary>
        private int played;

        private const int CastBit = 0;
        private const int PopBit = 1;
        private const int HomeBit = 1 + Guns;
        private const int FarewellBit = 1 + Guns * 2;

        /// <summary>How late a one-off may still happen: a client that joins mid-cast does not get the whole entrance at once.</summary>
        private const int LateWindow = 6;

        /// <summary>Owner only.</summary>
        private float manaDue;
        private Vector2 sentAim;

        // ---- what the guns do, read off the guns -----------------------------------------

        public readonly struct GunStats
        {
            /// <summary>Ticks between shots.</summary>
            public readonly int UseTime;

            /// <summary>One shot's damage before the caster's bonuses: (gun + musket ball) x 2/3, rounded.</summary>
            public readonly int ShotBase;

            public readonly float Speed;
            public readonly float Knockback;
            public readonly int Crit;
            public readonly SoundStyle? Sound;

            public GunStats(Item gun, Item ball)
            {
                UseTime = Math.Max(1, gun.useTime);
                ShotBase = (int)MathF.Round((gun.damage + ball.damage));
                Speed = gun.shootSpeed + ball.shootSpeed;
                Knockback = gun.knockBack + ball.knockBack;
                Crit = gun.crit;
                Sound = gun.UseSound;
            }
        }

        private static GunStats[] stats;

        /// <summary>
        /// Each gun's numbers, bottom to top, from a fresh one of each: so this mod's own
        /// changes to them (PSItem) are in. Made the first time they are asked for, in game.
        /// </summary>
        public static GunStats[] Stats
        {
            get
            {
                if (stats == null)
                {
                    Item ball = new(ItemID.MusketBall);
                    GunStats[] made = new GunStats[Guns];

                    for (int i = 0; i < Guns; i++)
                        made[i] = new GunStats(new Item(GunTypes[i]), ball);

                    stats = made;
                }

                return stats;
            }
        }

        public override void Unload()
        {
            stats = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 4;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
            Projectile.netImportant = true;

            turn = new float[Guns];
            kick = new float[Guns];
            turned = new bool[Guns];
            outDir = 0;
            backDir = 0;
            played = 0;
        }

        /// <summary>True once, the first tick at or after <paramref name="at"/> (and not long after it), for this bit.</summary>
        private bool Once(int bit, int age, int at)
        {
            if (age < at || age >= at + LateWindow || (played & (1 << bit)) != 0)
                return false;

            played |= 1 << bit;
            return true;
        }

        public override bool? CanDamage() => false;

        // The velocity is the aim, not a motion.
        public override bool ShouldUpdatePosition() => false;

        // ---- the timeline ------------------------------------------------------------------

        private static int LaunchAt(int i) => i * WaveGap;

        private static int ArriveAt(int i) => LaunchAt(i) + WaveTravel;

        private int LeaveAt(int i) => LeaveStart + (Guns - 1 - i) * LeaveGap;

        private int ReturnStart(int i) => LeaveAt(i) + ReturnDelay;

        private static float Clamp01(float x) => Math.Clamp(x, 0f, 1f);

        private static float EaseOutCubic(float p) => 1f - (1f - p) * (1f - p) * (1f - p);

        private static float EaseInCubic(float p) => p * p * p;

        /// <summary>Past full and back: the little overshoot that makes it grow like it means it.</summary>
        private static float EaseOutBack(float p)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = p - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>A small swell before it goes.</summary>
        private static float EaseInBack(float p)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * p * p * p - c1 * p * p;
        }

        /// <summary>How big and how seen gun <paramref name="i"/> is at this age: nothing before its wave lands.</summary>
        private void Look(int i, int age, out float scale, out float alpha)
        {
            int arrive = ArriveAt(i);

            if (age < arrive)
            {
                scale = 0f;
                alpha = 0f;
                return;
            }

            scale = 0.25f + 0.75f * EaseOutBack(Clamp01((age - arrive) / (float)GrowTicks));
            alpha = Clamp01((age - arrive) / (float)FadeInTicks);

            if (Leaving)
            {
                float q = Clamp01((age - LeaveAt(i)) / (float)ShrinkTicks);
                scale *= 1f - 0.85f * EaseInBack(q);
                alpha *= 1f - MathF.Pow(q, 1.5f);
            }
        }

        private static Color Glow(int i, int age) =>
            Color.Lerp(GlowA[i], GlowB[i], 0.5f + 0.5f * MathF.Sin(age * 0.035f + i * 2.1f));

        /// <summary>Where gun <paramref name="i"/> hangs, bobbing a little. For drawing, with the caster's step smoothing.</summary>
        private static Vector2 GunCentre(Player player, int i, int age, bool forDrawing)
        {
            Vector2 centre = player.MountedCenter;

            if (forDrawing)
                centre.Y += player.gfxOffY;

            float bob = 1.5f * MathF.Sin(age * 0.06f + i * 2.1f);
            return centre + new Vector2(Hover[i].X, (Hover[i].Y + bob) * player.gravDir);
        }

        /// <summary>
        /// Gun <paramref name="i"/>'s wave, from the caster's back to where the gun hangs,
        /// relative to the caster's centre: out behind them and curling over.
        /// </summary>
        private static void WaveControls(int i, int dir, float grav, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d)
        {
            Vector2 back = new(-7f * dir, -5f * grav);
            Vector2 hover = new(Hover[i].X, Hover[i].Y * grav);
            a = back;
            b = back + new Vector2(-dir * (28f + 8f * i), (-4f - 4f * i) * grav);
            c = hover + new Vector2(-dir * (24f + 8f * i), 12f * grav);
            d = hover;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
        {
            float v = 1f - u;
            return a * (v * v * v) + b * (3f * v * v * u) + c * (3f * v * u * u) + d * (u * u * u);
        }

        private static Vector2 BezierSlope(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u)
        {
            float v = 1f - u;
            return (b - a) * (3f * v * v) + (c - b) * (6f * v * u) + (d - c) * (3f * u * u);
        }

        // ---- every tick ---------------------------------------------------------------------

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            int age = Age;
            Projectile.ai[0]++;
            Projectile.timeLeft = 2;
            Projectile.Center = player.MountedCenter;

            if (Projectile.owner == Main.myPlayer)
                Drive(player, age);

            if (Leaving && age >= LeaveStart + LeaveTicks)
            {
                Projectile.Kill();
                return;
            }

            if (outDir == 0)
                outDir = player.direction;

            if (Leaving && backDir == 0)
                backDir = player.direction;

            if (!Leaving)
                Point(player);

            for (int i = 0; i < Guns; i++)
                Tend(player, i, age);

            if (!Main.dedServ)
                Effects(player, age);
        }

        /// <summary>Owner only: the hold, the aim, the drain, and letting go.</summary>
        private void Drive(Player player, int age)
        {
            if (Leaving)
                return;

            // Only while THIS spell is the one up on the staff in the hand.
            bool armed = player.HeldItem?.ModItem is Catalyst staff && staff.Selected(player) is AquaticFirearms;

            if (!armed || !player.channel)
            {
                LetGo(player, age, armed);
                return;
            }

            // The staff stays up for as long as the button is down.
            player.itemTime = 2;
            player.itemAnimation = 2;

            Vector2 aim = Main.MouseWorld - player.MountedCenter;
            Projectile.velocity = aim;

            if (Vector2.DistanceSquared(aim, sentAim) > 36f)
            {
                sentAim = aim;
                Projectile.netUpdate = true;
            }

            if (age < FreeTicks)
                return;

            // Paid straight off the bar a point at a time, the reduction on the rate, as
            // Court of Hands pays: CheckMana on one point rounds a reduced cost to nothing.
            manaDue += DrainPerSecond / 60f * Math.Max(0f, player.manaCost);

            while (manaDue >= 1f)
            {
                manaDue -= 1f;

                if (player.statMana < 1)
                {
                    LetGo(player, age, armed);
                    return;
                }

                player.statMana--;
            }
        }

        private void LetGo(Player player, int age, bool armed)
        {
            Projectile.ai[1] = Math.Max(1, age);
            Projectile.netUpdate = true;

            // The staff comes down over a whole use rather than at once, as Court of Hands'
            // seal does. Only if it is still the staff in the hand.
            Item item = player.HeldItem;

            if (armed && item?.ModItem is Catalyst)
            {
                player.itemTime = item.useTime;
                player.itemAnimation = item.useAnimation;
            }
        }

        /// <summary>The caster faces the aim and holds the staff along it, the way a held gun is.</summary>
        private void Point(Player player)
        {
            Vector2 aim = Projectile.velocity;

            if (aim == Vector2.Zero)
                return;

            player.ChangeDir(aim.X >= 0f ? 1 : -1);
            player.itemRotation = MathF.Atan2(aim.Y * player.direction, aim.X * player.direction);
        }

        /// <summary>One gun: turn it toward the aim, let its kick settle, and fire it when its turn comes.</summary>
        private void Tend(Player player, int i, int age)
        {
            int arrive = ArriveAt(i);

            if (age < arrive)
                return;

            Vector2 centre = GunCentre(player, i, age, false);
            float want = (player.MountedCenter + Projectile.velocity - centre).ToRotation();

            if (!turned[i])
            {
                // Each grows in pointing up and swings round to the aim.
                turn[i] = -MathHelper.PiOver2 * player.gravDir;
                turned[i] = true;
            }

            turn[i] += MathHelper.WrapAngle(want - turn[i]) * 0.3f;
            kick[i] *= 0.6f;

            int ready = arrive + ReadyAfter;

            if (Leaving || age < ready)
                return;

            GunStats gun = Stats[i];

            if ((age - ready) % gun.UseTime == 0)
                Fire(player, i, centre, gun);
        }

        /// <summary>A shot: the bullet on the owner, the sound, flash and kick everywhere.</summary>
        private void Fire(Player player, int i, Vector2 centre, GunStats gun)
        {
            float rotation = turn[i];
            Vector2 direction = rotation.ToRotationVector2();
            Vector2 muzzle = centre + Muzzle(i, rotation);
            kick[i] = 2f;

            if (Projectile.owner == Main.myPlayer)
            {
                Item staff = player.HeldItem;
                Vector2 velocity = direction * gun.Speed
                    + new Vector2(Main.rand.Next(-40, 41), Main.rand.Next(-40, 41)) * Scatter[i];

                int shot = Projectile.NewProjectile(Projectile.GetSource_FromThis(), muzzle, velocity, ProjectileID.Bullet,
                    ShotDamage(player, staff, gun.ShotBase), player.GetWeaponKnockback(staff, gun.Knockback), Projectile.owner);

                // A vanilla musket ball, but a spell's: magic, so magic's effects answer to it,
                // and with its gun's own crit on top of the staff's (the S.D.M.G.'s +10).
                // Hits are worked out by the owner, so here is where it has to be set.
                if (shot >= 0 && shot < Main.maxProjectiles)
                {
                    Main.projectile[shot].DamageType = DamageClass.Magic;
                    Main.projectile[shot].CritChance += gun.Crit;
                }
            }

            if (Main.dedServ)
                return;

            if (gun.Sound is SoundStyle sound)
                SoundEngine.PlaySound(sound with { Volume = sound.Volume * ShotVolume }, muzzle);

            MuzzleFlash.Spawn(muzzle, direction);
        }

        /// <summary>
        /// What the staff would do with this base, which is what vanilla does for a weapon
        /// (Player.GetWeaponDamage) with the gun's number in place of the staff's.
        /// </summary>
        private static int ShotDamage(Player player, Item staff, int shotBase)
        {
            StatModifier damage = player.GetTotalDamage(staff.DamageType);
            CombinedHooks.ModifyWeaponDamage(player, staff, ref damage);
            return Math.Max(0, (int)(damage.ApplyTo(shotBase) + 5E-06f));
        }

        /// <summary>The end of gun <paramref name="i"/>'s barrel from its centre, turned as it is drawn: flipped when it faces left.</summary>
        private static Vector2 Muzzle(int i, float rotation)
        {
            Vector2 local = AquaticFirearmsSkin.Get(i)?.Muzzle ?? FallbackMuzzle[i];

            if (MathF.Cos(rotation) < 0f)
                local.Y = -local.Y;

            return local.RotatedBy(rotation);
        }

        /// <summary>Sound, spray and light, on each client.</summary>
        private void Effects(Player player, int age)
        {
            float grav = player.gravDir;

            if (Once(CastBit, age, 0))
            {
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.7f, Pitch = 0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.6f, Pitch = 0.15f }, Projectile.Center);
            }

            if (Leaving && Once(FarewellBit, age, LeaveStart))
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);

            for (int i = 0; i < Guns; i++)
            {
                WaveControls(i, outDir, grav, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d);
                int launch = LaunchAt(i);
                int arrive = ArriveAt(i);

                // Spray off the head of each wave as it climbs.
                if (age >= launch && age < arrive)
                {
                    Vector2 head = player.MountedCenter + Bezier(a, b, c, d, EaseOutCubic((age - launch) / (float)WaveTravel));
                    Spray(head, outDir, 2);
                    Lighting.AddLight(head, Shallow.ToVector3() * 0.3f);
                }

                if (Once(PopBit + i, age, arrive))
                    Pop(player, i, age);

                if (Leaving && Once(HomeBit + i, age, ReturnStart(i) + ReturnTravel))
                    Home(player, i, backDir, grav);

                Look(i, age, out _, out float alpha);

                if (alpha > 0f)
                    Lighting.AddLight(GunCentre(player, i, age, false), Glow(i, age).ToVector3() * (0.35f * alpha));
            }
        }

        private static void Spray(Vector2 at, int dir, int count)
        {
            for (int k = 0; k < count; k++)
            {
                Vector2 velocity = new(Main.rand.NextFloat(-1.4f, 1.4f) - dir * 0.8f, Main.rand.NextFloat(-2f, 0.4f));
                Dust.NewDustPerfect(at, DustID.Water, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.3f));
            }
        }

        /// <summary>A wave lands and its gun comes out of it: a burst of water, a flash of the gun's colour, a bubbling note that rises gun by gun.</summary>
        private static void Pop(Player player, int i, int age)
        {
            Vector2 at = GunCentre(player, i, age, false);

            SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.5f, Pitch = 0.2f + 0.12f * i }, at);

            for (int k = 0; k < 12; k++)
            {
                Vector2 velocity = (MathHelper.TwoPi * k / 12f).ToRotationVector2() * 2.2f + new Vector2(0f, -1f);
                Dust.NewDustPerfect(at, DustID.Water, velocity, 0, default, Main.rand.NextFloat(1f, 1.4f));
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Glow(i, age), 0.22f, 16));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Foam, 0.1f, 10, lights: false));
        }

        /// <summary>A wave pours back into the caster's back.</summary>
        private static void Home(Player player, int i, int dir, float grav)
        {
            WaveControls(i, dir, grav, out Vector2 back, out _, out _, out _);
            Vector2 at = player.MountedCenter + back;

            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.6f, Pitch = -0.1f - 0.1f * i }, at);

            for (int k = 0; k < 6; k++)
            {
                Vector2 velocity = new(Main.rand.NextFloat(-1.5f, 1.5f) - dir, Main.rand.NextFloat(-2f, -0.5f) * grav);
                Dust.NewDustPerfect(at, DustID.Water, velocity, 0, default, Main.rand.NextFloat(0.9f, 1.2f));
            }
        }

        // ---- drawing -------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active)
                return false;

            AquaticFirearmsSkin.Refresh();

            SpriteBatch batch = Main.spriteBatch;

            // The AI has already counted this tick.
            int age = Math.Max(0, Age - 1);
            float grav = player.gravDir;
            Vector2 origin = player.MountedCenter + new Vector2(0f, player.gfxOffY);

            // The waves behind, then the guns, bottom first, so the stronger sits over the weaker.
            for (int i = 0; i < Guns; i++)
                DrawWaves(batch, i, age, origin, grav);

            for (int i = 0; i < Guns; i++)
                DrawGun(batch, player, i, age);

            return false;
        }

        private void DrawWaves(SpriteBatch batch, int i, int age, Vector2 origin, float grav)
        {
            // Out, from the back to the gun, on the side the caster's back was on then.
            int launch = LaunchAt(i);
            int arrive = ArriveAt(i);

            if (outDir != 0 && age >= launch && age < arrive + TailCatch)
            {
                WaveControls(i, outDir, grav, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d);
                float head = EaseOutCubic(Clamp01((age - launch) / (float)WaveTravel));
                float tail = age < arrive
                    ? Math.Max(0f, head - TailLag)
                    : 1f - TailLag + TailLag * Clamp01((age - arrive) / (float)TailCatch);

                Ribbon(batch, origin, a, b, c, d, tail, head, age, outDir);
            }

            if (!Leaving || backDir == 0)
                return;

            // Back, from the gun to the back: the same path the other way.
            int start = ReturnStart(i);

            if (age >= start && age < start + ReturnTravel + TailCatch)
            {
                WaveControls(i, backDir, grav, out Vector2 a, out Vector2 b, out Vector2 c, out Vector2 d);
                float head = EaseInCubic(Clamp01((age - start) / (float)ReturnTravel));
                float tail = age < start + ReturnTravel
                    ? Math.Max(0f, head - TailLag)
                    : 1f - TailLag + TailLag * Clamp01((age - start - ReturnTravel) / (float)TailCatch);

                Ribbon(batch, origin, d, c, b, a, tail, head, age, backDir);
            }
        }

        /// <summary>
        /// A wave between <paramref name="tail"/> and <paramref name="head"/> along its path:
        /// thin and deep at the tail, broad and pale at the head, rippling along its length,
        /// with foam on its outer edge and a cap of it on the head.
        /// </summary>
        private static void Ribbon(SpriteBatch batch, Vector2 origin, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            float tail, float head, int age, int dir)
        {
            if (head - tail < 0.005f)
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            for (int k = 0; k < RibbonPoints; k++)
            {
                float s = k / (float)(RibbonPoints - 1);
                float u = MathHelper.Lerp(tail, head, s);
                Vector2 slope = BezierSlope(a, b, c, d, u).SafeNormalize(Vector2.UnitY);
                Vector2 normal = new(-slope.Y, slope.X);
                float ripple = 2f * MathF.Sin(u * 18f - age * 0.45f) * Math.Min(1f, 4f * s * (1f - s));

                ribbon[k] = origin + Bezier(a, b, c, d, u) + normal * ripple;
                ribbonSlope[k] = slope;
            }

            // A darker edge under it, then the water.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 0; k < RibbonPoints - 1; k++)
                {
                    float s = k / (float)(RibbonPoints - 1);
                    float width = Width(s);
                    float fade = Math.Min(1f, s * 3f);

                    if (pass == 0)
                        Segment(batch, pixel, ribbon[k], ribbon[k + 1], WaterEdge * (0.7f * fade), width + 1.6f);
                    else
                        Segment(batch, pixel, ribbon[k], ribbon[k + 1], Water(s) * (0.9f * fade), width);
                }
            }

            // Foam along the outside of the front half: the side away from the way the caster faces.
            for (int k = RibbonPoints / 2; k < RibbonPoints - 1; k++)
            {
                float s = k / (float)(RibbonPoints - 1);
                Vector2 normal = new(-ribbonSlope[k].Y, ribbonSlope[k].X);

                if (normal.X * -dir < 0f)
                    normal = -normal;

                Vector2 off = normal * (Width(s) * 0.35f);
                Segment(batch, pixel, ribbon[k] + off, ribbon[k + 1] + off, Foam * (0.85f * Clamp01((s - 0.5f) * 4f)), 1.4f);
            }

            // The head: a cap of foam, and a little light.
            Vector2 tip = ribbon[RibbonPoints - 1];
            Segment(batch, pixel, tip, tip + ribbonSlope[RibbonPoints - 1] * 1.5f, Foam * 0.95f, Width(1f) * 0.8f);

            Texture2D dot = AquaticFirearmsSkin.Dot;

            if (dot != null)
            {
                batch.Draw(dot, tip - Main.screenPosition, null, (Shallow * 0.35f) with { A = 0 }, 0f, dot.Size() * 0.5f,
                    13f / dot.Width, SpriteEffects.None, 0f);
            }
        }

        private static float Width(float s) => 1.5f + 6f * MathF.Pow(s, 1.3f);

        /// <summary>The water's colour from tail (0) to head (1).</summary>
        private static Color Water(float s)
        {
            if (s < 0.45f)
                return Color.Lerp(Deep, Sea, s / 0.45f);

            if (s < 0.8f)
                return Color.Lerp(Sea, Shallow, (s - 0.45f) / 0.35f);

            return Color.Lerp(Shallow, Foam, (s - 0.8f) / 0.2f);
        }

        /// <summary>A straight run of solid colour, <paramref name="width"/> across whatever its length.</summary>
        private static void Segment(SpriteBatch batch, Texture2D pixel, Vector2 from, Vector2 to, Color colour, float width)
        {
            Vector2 run = to - from;
            float length = run.Length();

            if (length < 0.01f)
                return;

            batch.Draw(pixel, from - Main.screenPosition, new Rectangle(0, 0, 1, 1), colour, run.ToRotation(),
                new Vector2(0f, 0.5f), new Vector2(length + 0.6f, width), SpriteEffects.None, 0f);
        }

        private void DrawGun(SpriteBatch batch, Player player, int i, int age)
        {
            Look(i, age, out float scale, out float alpha);

            if (alpha <= 0f || scale <= 0.02f)
                return;

            float rotation = turn[i];
            Vector2 at = GunCentre(player, i, age, true) - rotation.ToRotationVector2() * kick[i] - Main.screenPosition;

            // Facing left it is flipped top to bottom, so it is never upside down.
            SpriteEffects flip = MathF.Cos(rotation) < 0f ? SpriteEffects.FlipVertically : SpriteEffects.None;
            AquaticFirearmsSkin.Gun skin = AquaticFirearmsSkin.Get(i);

            if (skin == null)
            {
                // No skin could be made: the gun as it is, so the spell still reads.
                Main.instance.LoadItem(GunTypes[i]);
                Texture2D plain = TextureAssets.Item[GunTypes[i]].Value;
                batch.Draw(plain, at, null, Color.White * alpha, rotation, plain.Size() * 0.5f, scale, flip, 0f);
                return;
            }

            Color glow = Glow(i, age);
            float pulse = 0.85f + 0.15f * MathF.Sin(age * 0.05f + i * 2.1f);

            batch.Draw(skin.Halo, at, null, (glow * (0.7f * pulse * alpha)) with { A = 0 }, rotation, skin.Halo.Size() * 0.5f, scale, flip, 0f);
            batch.Draw(skin.Body, at, null, Color.White * alpha, rotation, skin.Body.Size() * 0.5f, scale, flip, 0f);
            batch.Draw(skin.Rim, at, null, glow * alpha, rotation, skin.Rim.Size() * 0.5f, scale, flip, 0f);
        }
    }
}
