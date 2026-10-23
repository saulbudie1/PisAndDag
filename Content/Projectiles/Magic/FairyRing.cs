using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Magic
{
    /// <summary>
    /// Mycelium, Finish Them: the ring itself. It sits on the ground point the cast found (its
    /// middle) and lays a ring of glowing caps along the ground <see cref="HalfWidth"/> either side,
    /// as a fairy ring is seen from the side: <see cref="Caps"/> caps that burst out of the floor one
    /// after another from the middle out, with threads of light running between them under the
    /// ground. Spores rise between them, and everything in the ring, up to <see cref="Height"/> over
    /// the ground, is hurt every <see cref="SporeTick"/> ticks.
    ///
    /// It lasts <see cref="Life"/> ticks and withers, the caps going back into the ground; another cast
    /// withers it at once (ai[1] 1, <see cref="WitherAll"/>). While it stands (<see cref="Taking"/>),
    /// whatever dies in it feeds it (FairyRingDeaths: a cap comes up where it lay, FairyRingFruit) and
    /// whatever is left lying in it goes down into the ground (FairyRingSoil).
    ///
    /// Every machine lays its caps the same: their places are rolled from the ground point.
    /// </summary>
    public class FairyRing : ModProjectile
    {
        public const string CapTexture = "PisAndDag/Content/Projectiles/Magic/FairyRingCap";
        public const string CapGlowTexture = "PisAndDag/Content/Projectiles/Magic/FairyRingCapGlow";

        /// <summary>One frame of the caps' sheet, three down it: tall, round, flat.</summary>
        public const int CapWide = 18, CapHigh = 22, CapFrames = 3;

        public const int Life = 600;
        public const int WitherTicks = 36;

        /// <summary>How far the ring reaches either side of its middle, and how high over the ground it hurts.</summary>
        public const float HalfWidth = 7f * 16f;
        public const float Height = 6f * 16f;

        public const int Caps = 9;

        /// <summary>Ticks a cap takes to come up, and between one cap and the next out from the middle.</summary>
        public const int RiseTicks = 16;
        private const int RiseStagger = 3;

        /// <summary>Ticks between one hurt of the spores and the next, for one thing in the ring.</summary>
        public const int SporeTick = 30;

        /// <summary>How far up or down from the ground point a cap looks for its own ground, in tiles.</summary>
        private const int CapSearch = 4;

        public static readonly Color Deep = new Color(14, 26, 92);
        public static readonly Color Mid = new Color(44, 96, 226);
        public static readonly Color Bright = new Color(116, 186, 255);
        public static readonly Color Pale = new Color(206, 238, 255);

        private static Asset<Texture2D> capGlow;
        private static Asset<Texture2D> softLine;
        private static Asset<Texture2D> bloom;

        /// <summary>The caps' glow and the soft line the threads are drawn with, for the caps that come up where something died (FairyRingFruit).</summary>
        public static Texture2D Glow => capGlow?.Value;

        /// <summary>The soft glow made by formula (Content/Graphics/GlowSoft.png): it fades to nothing at its edge.</summary>
        public static Texture2D Soft => bloom?.Value;
        public static Texture2D Line => softLine?.Value;

        private bool laid;
        private Vector2 ground;

        /// <summary>Where each cap stands (the middle of its foot), whether it has ground at all, and how it looks.</summary>
        private readonly Vector2[] capAt = new Vector2[Caps];
        private readonly bool[] capThere = new bool[Caps];
        private readonly int[] capFrame = new int[Caps];
        private readonly float[] capScale = new float[Caps];
        private readonly int[] capDelay = new int[Caps];
        private readonly float[] capPhase = new float[Caps];
        private readonly bool[] capFlip = new bool[Caps];

        /// <summary>The threads under the ground: runs of points, each from one cap to the next, and short roots off each cap.</summary>
        private readonly List<Vector2[]> threads = new();

        private ref float Age => ref Projectile.localAI[0];

        private bool Withering => Projectile.ai[1] == 1f || Projectile.timeLeft <= WitherTicks;

        /// <summary>Up and not withering: what dies in it feeds it, and what lies in it goes down.</summary>
        public bool Taking => laid && Age >= RiseTicks && !Withering;

        /// <summary>The ring, from the ground up to <see cref="Height"/> over it.</summary>
        public Rectangle Zone => new Rectangle((int)(ground.X - HalfWidth), (int)(ground.Y - Height), (int)(HalfWidth * 2f), (int)Height + 12);

        public override string Texture => CapTexture;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            capGlow = ModContent.Request<Texture2D>(CapGlowTexture);
            softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
            bloom = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft");
        }

        public override void Unload()
        {
            capGlow = null;
            softLine = null;
            bloom = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = SporeTick;
            Projectile.timeLeft = Life + WitherTicks;
            Projectile.netImportant = true;
            Projectile.hide = false;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>Withers every ring of this player's: another is being called.</summary>
        public static void WitherAll(int owner)
        {
            int type = ModContent.ProjectileType<FairyRing>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type != type || p.owner != owner || p.ai[1] == 1f)
                    continue;

                p.ai[1] = 1f;
                p.timeLeft = Math.Min(p.timeLeft, WitherTicks);
                p.netUpdate = true;
            }
        }

        /// <summary>The ring of this player's that is standing, if there is one.</summary>
        public static FairyRing StandingOf(int owner)
        {
            int type = ModContent.ProjectileType<FairyRing>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type == type && p.owner == owner && p.ModProjectile is FairyRing ring && !ring.Withering)
                    return ring;
            }

            return null;
        }

        // ---- laying it out -------------------------------------------------------------------------

        /// <summary>The caps' places along the ground, and the threads between them, rolled from where it stands so every machine rolls them alike.</summary>
        private void Lay()
        {
            laid = true;
            ground = Projectile.Center;

            var roll = new Random((int)(ground.X * 7f) ^ (int)(ground.Y * 131f));
            int middle = Caps / 2;

            for (int i = 0; i < Caps; i++)
            {
                float along = MathHelper.Lerp(-HalfWidth, HalfWidth, i / (float)(Caps - 1)) + (float)(roll.NextDouble() * 10.0 - 5.0);
                capThere[i] = Foot(ground.X + along, ground.Y, out capAt[i]);
                capFrame[i] = roll.Next(CapFrames);
                capScale[i] = 0.8f + (float)roll.NextDouble() * 0.4f;
                capDelay[i] = Math.Abs(i - middle) * RiseStagger;
                capPhase[i] = (float)(roll.NextDouble() * MathHelper.TwoPi);
                capFlip[i] = roll.Next(2) == 0;
            }

            // The middle one is the biggest: the ring grows out from it.
            capScale[middle] = 1.25f;
            capFrame[middle] = 0;

            for (int i = 0; i < Caps - 1; i++)
            {
                if (!capThere[i] || !capThere[i + 1])
                    continue;

                threads.Add(Thread(capAt[i], capAt[i + 1], roll));
            }

            for (int i = 0; i < Caps; i++)
            {
                if (!capThere[i])
                    continue;

                for (int k = 0; k < 2; k++)
                {
                    float side = k == 0 ? -1f : 1f;
                    Vector2 end = capAt[i] + new Vector2(side * (6f + (float)roll.NextDouble() * 10f), 10f + (float)roll.NextDouble() * 12f);
                    threads.Add(Thread(capAt[i] + new Vector2(0f, 3f), end, roll, 3));
                }
            }
        }

        /// <summary>A run of points from one place to another under the ground, a few pixels down and wandering a little.</summary>
        private static Vector2[] Thread(Vector2 from, Vector2 to, Random roll, int steps = 6)
        {
            Vector2[] points = new Vector2[steps + 1];

            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                Vector2 p = Vector2.Lerp(from, to, t);

                // Down into the ground, more in the middle of the run, and a little this way and that.
                float sag = (float)Math.Sin(t * MathHelper.Pi) * 4f;
                p += new Vector2(0f, 4f + sag) + new Vector2((float)(roll.NextDouble() - 0.5) * 3f, (float)(roll.NextDouble() - 0.5) * 3f) * (s == 0 || s == steps ? 0f : 1f);
                points[s] = p;
            }

            return points;
        }

        /// <summary>The ground under a column near this height: the top of the first tile with a top to stand on and room over it.</summary>
        public static bool Foot(float x, float y, out Vector2 foot)
        {
            int tx = (int)(x / 16f);
            int ty = (int)(y / 16f);
            foot = new Vector2(x, y);

            if (tx < 1 || tx >= Main.maxTilesX - 1)
                return false;

            for (int row = Math.Max(2, ty - CapSearch); row <= Math.Min(Main.maxTilesY - 2, ty + CapSearch); row++)
            {
                if (Standable(tx, row) && !Solid(tx, row - 1))
                {
                    foot = new Vector2(x, row * 16f);
                    return true;
                }
            }

            return false;
        }

        private static bool Solid(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType];
        }

        private static bool Standable(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }

        // ---- standing --------------------------------------------------------------------------------

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active)
            {
                Projectile.Kill();
                return;
            }

            if (!laid)
            {
                Lay();

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch with { Pitch = -0.3f, Volume = 0.55f }, ground);
                    SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.5f, Volume = 0.8f }, ground);
                }
            }

            if (Projectile.ai[1] == 1f && Projectile.timeLeft > WitherTicks)
                Projectile.timeLeft = WitherTicks;

            Age++;
            Projectile.velocity = Vector2.Zero;
            Projectile.friendly = Age >= RiseTicks * 0.75f && !Withering;

            float strength = Strength();

            for (int i = 0; i < Caps; i++)
            {
                if (!capThere[i])
                    continue;

                float g = Grown(i);

                if (g > 0.05f)
                    Lighting.AddLight(capAt[i] - new Vector2(0f, CapHigh * 0.5f * capScale[i]), 0.07f * g, 0.2f * g, 0.55f * g);
            }

            Lighting.AddLight(ground - new Vector2(0f, 8f), 0.04f * strength, 0.1f * strength, 0.3f * strength);

            if (!Main.dedServ)
                Breathe(strength);
        }

        /// <summary>How far up the whole ring is, 0 to 1: up over its first ticks, down as it withers.</summary>
        private float Strength()
        {
            float up = MathHelper.Clamp(Age / (RiseTicks + RiseStagger * 4f), 0f, 1f);
            float down = Withering ? MathHelper.Clamp(Projectile.timeLeft / (float)WitherTicks, 0f, 1f) : 1f;
            return up * down;
        }

        /// <summary>How far up cap <paramref name="i"/> is: out of the ground with a little too much, settling; back down as the ring withers.</summary>
        private float Grown(int i)
        {
            float t = MathHelper.Clamp((Age - capDelay[i]) / RiseTicks, 0f, 1f);

            // Out past its size and back: ease out, overshooting.
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            float g = t <= 0f ? 0f : 1f + c3 * u * u * u + c1 * u * u;

            if (Withering)
            {
                float w = MathHelper.Clamp(Projectile.timeLeft / (float)WitherTicks, 0f, 1f);
                g *= w * w;
            }

            return Math.Max(0f, g);
        }

        /// <summary>The ground under the ring at this column, between the two caps either side of it.</summary>
        private float SurfaceAt(float x)
        {
            float t = (x - (ground.X - HalfWidth)) / (HalfWidth * 2f) * (Caps - 1);
            int a = (int)MathHelper.Clamp((float)Math.Floor(t), 0, Caps - 1);
            int b = Math.Min(Caps - 1, a + 1);
            float ya = capThere[a] ? capAt[a].Y : ground.Y;
            float yb = capThere[b] ? capAt[b].Y : ground.Y;
            return MathHelper.Lerp(ya, yb, t - a);
        }

        /// <summary>Spores up out of the ground, and puffs off the caps now and then.</summary>
        private void Breathe(float strength)
        {
            if (strength <= 0.05f)
                return;

            if (Main.rand.NextFloat() < 0.9f * strength)
            {
                float x = ground.X + Main.rand.NextFloat(-HalfWidth, HalfWidth);
                Vector2 at = new Vector2(x, SurfaceAt(x) - Main.rand.NextFloat(0f, 6f));
                Color colour = Main.rand.NextBool(3) ? Pale : Bright;

                PADParticleHandler.SpawnParticle(new SporeMote(at, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.3f, -0.4f)),
                    colour * 0.8f, Main.rand.NextFloat(0.55f, 0.95f), Main.rand.Next(50, 90)));
            }

            for (int i = 0; i < Caps; i++)
            {
                if (!capThere[i] || !Main.rand.NextBool(90))
                    continue;

                float g = Grown(i);
                Vector2 top = capAt[i] - new Vector2(0f, CapHigh * 0.7f * capScale[i] * g);

                for (int k = 0; k < 4; k++)
                {
                    PADParticleHandler.SpawnParticle(new SporeMote(top + Main.rand.NextVector2Circular(4f, 2f),
                        new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(-1.1f, -0.3f)), Pale * 0.85f,
                        Main.rand.NextFloat(0.5f, 0.8f), Main.rand.Next(40, 70)));
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (!laid || !Projectile.friendly)
                return false;

            return Zone.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
        {
            modifiers.Knockback *= 0f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.dedServ)
                return;

            for (int k = 0; k < 4; k++)
            {
                PADParticleHandler.SpawnParticle(new SporeMote(target.Center + Main.rand.NextVector2Circular(target.width * 0.4f, target.height * 0.4f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-1f, -0.2f)), Bright * 0.9f, Main.rand.NextFloat(0.5f, 0.85f), Main.rand.Next(30, 50)));
            }
        }

        // ---- drawing -----------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            if (!laid)
                return false;

            float strength = Strength();
            Texture2D caps = TextureAssets.Projectile[Type].Value;
            Texture2D glow = capGlow?.Value;

            // The ground holds a glow under the ring: a long soft one along it, a brighter one at its middle.
            if (bloom != null && strength > 0f)
            {
                Texture2D b = bloom.Value;
                Color wide = Mid * (0.2f * strength);
                wide.A = 0;
                Color heart = Bright * (0.14f * strength);
                heart.A = 0;
                Main.EntitySpriteDraw(b, ground - Main.screenPosition, null, wide, 0f, b.Size() * 0.5f,
                    new Vector2(HalfWidth * 2.7f / b.Width, 46f / b.Height), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(b, ground - Main.screenPosition, null, heart, 0f, b.Size() * 0.5f,
                    new Vector2(HalfWidth * 1.1f / b.Width, 28f / b.Height), SpriteEffects.None, 0);
            }

            DrawThreads(strength);

            for (int i = 0; i < Caps; i++)
            {
                if (!capThere[i])
                    continue;

                DrawCap(caps, glow, capAt[i], capFrame[i], new Vector2(capScale[i] * Grown(i)), capPhase[i], capFlip[i], Withering ? 1f - Grown(i) : 0f);
            }

            return false;
        }

        /// <summary>The threads under the ground: a faint wide band and a bright thin line, a pulse running out along them from the middle.</summary>
        private void DrawThreads(float strength)
        {
            if (softLine == null || strength <= 0f)
                return;

            Texture2D line = softLine.Value;

            foreach (Vector2[] run in threads)
            {
                for (int s = 0; s < run.Length - 1; s++)
                {
                    Vector2 a = run[s], b = run[s + 1];
                    float out_ = Math.Abs((a.X + b.X) * 0.5f - ground.X);
                    float pulse = (float)Math.Pow(Math.Max(0.0, Math.Cos(Age * 0.11f - out_ * 0.045f)), 6.0);
                    float k = strength * (0.35f + 0.65f * pulse);

                    Segment(line, a, b, Mid * (0.45f * k), 7f);
                    Segment(line, a, b, Pale * (0.8f * k), 2.5f);
                }
            }
        }

        /// <summary>One straight piece of thread, added light, this wide in world pixels.</summary>
        public static void Segment(Texture2D line, Vector2 a, Vector2 b, Color colour, float width)
        {
            Vector2 d = b - a;
            float length = d.Length();

            if (length < 0.5f)
                return;

            colour.A = 0;
            Main.EntitySpriteDraw(line, a - Main.screenPosition, null, colour, d.ToRotation(), new Vector2(0f, line.Height * 0.5f),
                new Vector2(length / line.Width, width / line.Height), SpriteEffects.None, 0);
        }

        /// <summary>
        /// One cap, its foot at <paramref name="foot"/>, this big: the sprite lit (never darker than its
        /// own glow keeps it), then its glow over it, breathing. <paramref name="sunk"/> lowers it into
        /// the ground as it withers.
        /// </summary>
        public static void DrawCap(Texture2D caps, Texture2D glow, Vector2 foot, int frameIndex, Vector2 scale, float phase, bool flip, float sunk)
        {
            if (scale.X <= 0.02f || scale.Y <= 0.02f)
                return;

            Rectangle frame = caps.Frame(1, CapFrames, 0, frameIndex);
            Vector2 origin = new Vector2(CapWide * 0.5f, CapHigh);
            Vector2 at = (foot + new Vector2(0f, 2f + sunk * CapHigh * 0.5f) - Main.screenPosition).Floor();
            SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Point cell = foot.ToTileCoordinates();
            Color lit = Lighting.GetColor(cell.X, cell.Y - 1);
            Color floor = new Color(150, 170, 220);
            Color colour = new Color(Math.Max(lit.R, floor.R), Math.Max(lit.G, floor.G), Math.Max(lit.B, floor.B));

            float breath = 0.55f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.07f + phase);

            // Its light in the air about the cap, soft, breathing with it.
            Texture2D halo = bloom?.Value;

            if (halo != null)
            {
                Color air = Mid * (0.32f * breath * Math.Min(1f, scale.Y));
                air.A = 0;
                Vector2 middle = at - new Vector2(0f, CapHigh * 0.62f * scale.Y);
                Main.EntitySpriteDraw(halo, middle, null, air, 0f, halo.Size() * 0.5f, CapWide * 2.8f * scale.X / halo.Width, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(caps, at, frame, colour, 0f, origin, scale, effects, 0);

            if (glow != null)
            {
                Color shine = Color.White * breath;
                shine.A = 0;
                Main.EntitySpriteDraw(glow, at, frame, shine, 0f, origin, scale, effects, 0);
            }
        }
    }

    /// <summary>
    /// Whatever dies in a standing ring (FairyRing.Taking) feeds it: a cap comes up under where it
    /// fell (FairyRingFruit), <see cref="FairyRingFruit.Most"/> to a player at once. Decided where
    /// deaths are decided (the server, or the one machine in a game alone); the cap is sent like any
    /// projectile.
    /// </summary>
    public class FairyRingDeaths : GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || NPCID.Sets.ProjectileNPC[npc.type])
                return;

            int ringType = ModContent.ProjectileType<FairyRing>();
            int fruitType = ModContent.ProjectileType<FairyRingFruit>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type != ringType || p.ModProjectile is not FairyRing ring || !ring.Taking || !ring.Zone.Intersects(npc.Hitbox))
                    continue;

                if (Main.player[p.owner].ownedProjectileCounts[fruitType] >= FairyRingFruit.Most)
                    return;

                float x = MathHelper.Clamp(npc.Center.X, ring.Zone.Left + 8f, ring.Zone.Right - 8f);

                if (!FairyRing.Foot(x, Math.Max(npc.Bottom.Y, ring.Zone.Bottom - 20f), out Vector2 foot))
                    return;

                Projectile.NewProjectile(p.GetSource_FromThis(), foot, Vector2.Zero, fruitType, p.damage, 0f, p.owner);
                return;
            }
        }
    }

    /// <summary>
    /// What is left lying in a standing ring goes down into the ground: a piece of something dead
    /// (a gore) that has come to rest on the floor inside it is drawn behind the tiles from then on
    /// and sinks, a third of a pixel a tick, darkening, spores rising off it, until it is under. Only
    /// what the game draws, on each machine for itself (gores are nobody else's).
    /// </summary>
    public class FairyRingSoil : ModSystem
    {
        private const float SinkSpeed = 0.35f;

        /// <summary>How far each gore has gone down, by its place in Main.gore, and what it was when it started (a place is used again).</summary>
        private static readonly float[] sunk = new float[Main.maxGore];
        private static readonly int[] sinking = new int[Main.maxGore];

        private static readonly List<Rectangle> zones = new();

        public override void PostUpdateGores()
        {
            if (Main.dedServ)
                return;

            zones.Clear();
            int ringType = ModContent.ProjectileType<FairyRing>();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.type == ringType && p.ModProjectile is FairyRing ring && ring.Taking)
                    zones.Add(ring.Zone);
            }

            for (int g = 0; g < Main.maxGore; g++)
            {
                Gore gore = Main.gore[g];

                if (gore == null || !gore.active)
                {
                    sunk[g] = 0f;
                    continue;
                }

                if (sunk[g] > 0f && sinking[g] != gore.type)
                    sunk[g] = 0f;

                Vector2 size = Size(gore);

                if (sunk[g] <= 0f)
                {
                    if (zones.Count == 0 || gore.velocity.LengthSquared() > 0.5f)
                        continue;

                    Vector2 middle = gore.position + size * 0.5f;

                    if (!Inside(middle) || !Resting(gore, size))
                        continue;

                    sinking[g] = gore.type;
                    sunk[g] = 0.01f;
                }

                sunk[g] += SinkSpeed;
                gore.behindTiles = true;
                gore.velocity = Vector2.Zero;
                gore.position.Y += SinkSpeed;
                gore.timeLeft = Math.Max(gore.timeLeft, 30);

                float share = sunk[g] / Math.Max(8f, size.Y);
                gore.alpha = (int)MathHelper.Clamp(share * 170f, 0f, 255f);

                if (Main.rand.NextBool(12))
                {
                    Vector2 from = gore.position + new Vector2(Main.rand.NextFloat(size.X), Main.rand.NextFloat(size.Y * 0.4f));
                    PADParticleHandler.SpawnParticle(new SporeMote(from, new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-1f, -0.4f)),
                        FairyRing.Bright * 0.8f, Main.rand.NextFloat(0.4f, 0.7f), Main.rand.Next(30, 55)));
                }

                if (share >= 1f)
                {
                    gore.active = false;
                    sunk[g] = 0f;
                }
            }
        }

        private static bool Inside(Vector2 point)
        {
            foreach (Rectangle zone in zones)
            {
                if (zone.Contains((int)point.X, (int)point.Y))
                    return true;
            }

            return false;
        }

        /// <summary>The gore's drawn size: its picture's frame, or a guess before the picture is loaded.</summary>
        private static Vector2 Size(Gore gore)
        {
            if (gore.type >= 0 && gore.type < TextureAssets.Gore.Length)
            {
                Asset<Texture2D> texture = TextureAssets.Gore[gore.type];

                if (texture != null && texture.IsLoaded)
                    return gore.Frame.GetSourceRectangle(texture.Value).Size() * gore.scale;
            }

            return new Vector2(20f, 20f);
        }

        /// <summary>Something with a top under the middle of its foot.</summary>
        private static bool Resting(Gore gore, Vector2 size)
        {
            Point under = (gore.position + new Vector2(size.X * 0.5f, size.Y + 2f)).ToTileCoordinates();

            if (under.X < 1 || under.X >= Main.maxTilesX - 1 || under.Y < 1 || under.Y >= Main.maxTilesY - 1)
                return false;

            Tile tile = Main.tile[under.X, under.Y];
            return tile.HasTile && !tile.IsActuated && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType]);
        }
    }
}
