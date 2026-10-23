using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// One of her arrows of light, in her second half in Legends (by request: "Arrow projectiles have the textures and
    /// silhouette of Daybreak, with glow and shit make it like her other projectile just the shape like day break. It also
    /// have after image."): Daybreak's shape (Arrow.png) in her light, drawn as her lances are, images of it trailing it
    /// (EmpressBow.DrawArrow).
    ///
    ///     ai[0]  what it is (Kind)       ai[1]  its own number (see each)       ai[2]  hers (her index)
    ///
    ///   MOOR       flies to her arena's border and sticks in it; three seconds after, bursts into light thirty tiles
    ///              about (by request: "it is stuck onto the border and explode after 3 seconds of being stuck into a 30
    ///              tile radius explosion of light"). ai[1]: its colour of hers.
    ///   RICOCHET   very fast; off each wall it meets as a mirror throws light back (the way it comes, turned about the
    ///              wall's own line square to it), losing nothing of its speed, three times; at the next wall it breaks
    ///              into glass (by request: "use mirror thru construction line perpendicular to border, bounce on corner
    ///              doesnt loose magnitude of velocity ... Bounce 3 times before fading"). Drawn as the mirror's glass.
    ///              ai[1]: which of the volley it is.
    ///   REFRACT    one of the seven out of the prism: fanned out and slowing to a stop, held, then a line of light from
    ///              it to whoever it is for (a third of a second), and down that line, fast; the seven one after another.
    ///              ai[1]: which of the seven (its colour, and its turn).
    ///
    /// Its picture's nock, its middle and its tip: NockBack behind its middle, TipAhead ahead. Hers are for the players in
    /// her arena alone. Made by the server; every machine flies it, and the server says when one sticks, turns or dives.
    /// </summary>
    public class EmpressArrow : ModProjectile
    {
        public enum Kind
        {
            Moor,
            Ricochet,
            Refract,
        }

        /// <summary>From its middle back to its nock, and on to its tip, at size 1 (the arrow in its picture is 100 long, its nock at 6, its tip at 99, before the picture's margin: EmpressBow.ArrowNock).</summary>
        public const float NockBack = 46f;
        public const float TipAhead = 47f;

        /// <summary>From its middle to its sun of a head (the arrow's 72, before the picture's margin).</summary>
        private const float StarAhead = 20f;

        /// <summary>Moor: ticks it stays stuck before it bursts; how far the burst reaches (30 tiles); ticks the burst hurts; ticks it is seen.</summary>
        public const int StuckTicks = 180;
        public const float BurstRadius = 480f;
        public const int BurstHurts = 6;
        private const int BurstFade = 36;

        /// <summary>The burst hits harder than the arrow does.</summary>
        private const float BurstHarder = 1.4f;

        /// <summary>Ricochet: walls it bounces off before the next breaks it.</summary>
        public const int Bounces = 3;

        /// <summary>Refract: how long the seven hold before the first dives, and between each; the line to its mark; its speed down it.</summary>
        public const int RefractHold = 54;
        public const int RefractGap = 10;
        public const int AimTicks = 22;
        public const float DiveSpeed = 30f;

        /// <summary>The seven's colours, red to violet (as the prism's).</summary>
        private static readonly Color[] Spectrum =
        {
            new Color(255, 60, 70), new Color(255, 150, 40), new Color(255, 236, 80), new Color(90, 255, 120),
            new Color(70, 160, 255), new Color(110, 90, 255), new Color(205, 100, 255),
        };

        public override string Texture => "PisAndDag/Content/NPCs/Legends/Empress/Arrow";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.timeLeft = 900;
            Projectile.aiStyle = -1;
        }

        private Kind What => (Kind)(int)Projectile.ai[0];

        private EmpressOfLight Her => EmpressOfLight.Of((int)Projectile.ai[2]);

        /// <summary>Which of the seven, a refracted one is.</summary>
        private int Index => Math.Clamp((int)Projectile.ai[1], 0, 6);

        /// <summary>Ticks since it was loosed.</summary>
        private int Age
        {
            get => (int)Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        /// <summary>Moor: 0 flying, 1 stuck, 2 bursting. Refract: 0 fanning out, 1 held, 2 aiming, 3 diving. Ricochet: 0 flying, 1 breaking.</summary>
        private int stage;

        /// <summary>Ticks in this stage.</summary>
        private int inStage;

        /// <summary>Its way while it flies; kept as it sticks or holds (when it no longer moves).</summary>
        private float facing;

        /// <summary>A ricochet's bounces (the server's count, sent; each one heard as it is told).</summary>
        private int bouncesSeen;

        private static bool Authority => Main.netMode != NetmodeID.MultiplayerClient;

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
        {
            facing = Projectile.velocity.ToRotation();
        }

        public override bool ShouldUpdatePosition() => stage == 0 || What == Kind.Refract && stage == 3;

        public override void AI()
        {
            EmpressOfLight her = Her;

            if (her == null || !EmpressArena.Up)
            {
                Projectile.Kill();
                return;
            }

            // A client's first tick of it: its way from its speed.
            if (Age == 0 && Projectile.velocity.LengthSquared() > 0.01f)
                facing = Projectile.velocity.ToRotation();

            Age++;
            inStage++;

            if (Projectile.velocity.LengthSquared() > 0.01f)
                facing = Projectile.velocity.ToRotation();

            Projectile.rotation = facing;

            switch (What)
            {
                case Kind.Moor:
                    Moor();
                    break;

                case Kind.Ricochet:
                    Ricochet();
                    break;

                case Kind.Refract:
                    Refract();
                    break;
            }

            Lighting.AddLight(Tip, Colour.ToVector3() * 0.6f);
        }

        private Vector2 Way => facing.ToRotationVector2();

        /// <summary>Its tip, its star of a head, its nock, in the world.</summary>
        private Vector2 Tip => Projectile.Center + Way * TipAhead;

        private Vector2 Star => Projectile.Center + Way * StarAhead;

        private Vector2 Nock => Projectile.Center - Way * NockBack;

        /// <summary>Its colour of hers.</summary>
        private Color Colour => What switch
        {
            Kind.Refract => Spectrum[Index],
            Kind.Ricochet => Color.Lerp(Color.White, EmpressArt.Sky, 0.35f),
            _ => EmpressArt.Prism(Projectile.ai[1]),
        };

        /// <summary>How far past its arena's border a point is (below 0: inside).</summary>
        private static float Past(Vector2 point) => Decagon.Past(point, EmpressArena.Centre, EmpressArena.Size);

        /// <summary>Where a line out of a point, this way, meets the border.</summary>
        private static Vector2 WallFrom(Vector2 from, Vector2 way, out int edge) => from + way * Decagon.Reach(from, way, EmpressArena.Centre, EmpressArena.Size, out edge);

        // ---- moor ---------------------------------------------------------------------------------------------

        private void Moor()
        {
            switch (stage)
            {
                case 0:
                    // Into the border: its tip on the wall, there it stays (every machine sees it come there alike; the
                    // server's word follows).
                    if (Past(Tip) >= 0f)
                    {
                        Vector2 wall = WallFrom(Tip - Projectile.velocity, Way, out _);
                        Projectile.Center = wall - Way * TipAhead;
                        Projectile.velocity = Vector2.Zero;
                        Go(1);
                        Projectile.netUpdate = true;
                        Stuck();
                    }

                    break;

                case 1:
                    // Its light gathering, quicker and quicker, the ticks with it.
                    if (!Main.dedServ)
                    {
                        int left = StuckTicks - inStage;
                        int every = left > 120 ? 30 : left > 60 ? 15 : 8;

                        if (inStage % every == 0)
                            SoundEngine.PlaySound(EmpressSounds.Tick with { Pitch = MathHelper.Lerp(-0.2f, 0.6f, inStage / (float)StuckTicks), Volume = 0.45f, MaxInstances = 8 }, Projectile.Center);
                    }

                    if (Authority && inStage >= StuckTicks)
                    {
                        Go(2);
                        Projectile.netUpdate = true;
                        Burst();
                    }

                    break;

                case 2:
                    if (Authority && inStage >= BurstFade)
                        Projectile.Kill();

                    break;
            }
        }

        /// <summary>Stuck: the thunk, a flash, glass off the wall.</summary>
        private void Stuck()
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(EmpressSounds.Thunk with { Volume = 0.8f, MaxInstances = 8 }, Tip);
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.4f, Volume = 0.4f, MaxInstances = 8 }, Tip);
            SparkShard.Spray(Tip, Color.White, 8, 6f, 40f, 16, palette: EmpressArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(Tip, Vector2.Zero, Colour, 120f / 160f, 14));
        }

        /// <summary>The burst: thirty tiles of light about where it stuck, seen and heard, the world shaken through.</summary>
        private void Burst()
        {
            if (Main.dedServ)
                return;

            Vector2 at = Star;

            SoundEngine.PlaySound(EmpressSounds.Burst with { Volume = 1f, MaxInstances = 8 }, at);
            SoundEngine.PlaySound(EmpressSounds.Shock with { Pitch = -0.2f, Volume = 0.6f, MaxInstances = 4 }, at);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f, Volume = 0.6f, MaxInstances = 8 }, at);

            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White, 520f / 160f, 26));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Colour, 900f / 160f, 34));
            PADParticleHandler.SpawnParticle(new CrownGlint(at, 220f, 26, Color.White));
            SparkShard.Spray(at, Color.White, 30, 14f, 160f, 28, palette: EmpressArt.Sparks);
            EmpressWarp.Ripple(at, 24f, 70f, 18f, 30);

            float near = 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 600f) / 1800f, 0f, 1f);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(9f * near, 18);

            for (int i = 0; i < 46; i++)
            {
                Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 18f), 0,
                    EmpressArt.Prism(Main.rand.NextFloat()), Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }
        }

        // ---- ricochet ----------------------------------------------------------------------------------------

        private void Ricochet()
        {
            if (stage == 1)
            {
                if (Authority && inStage >= 18)
                    Projectile.Kill();

                return;
            }

            // Every machine bounces it as it comes to a wall (the server's word follows, and corrects).
            if (Past(Tip) < 0f)
                return;

            if (bouncesSeen >= Bounces)
            {
                // Its bounces done: the next wall breaks it into glass.
                Vector2 wall = WallFrom(Tip - Projectile.velocity, Way, out _);
                Projectile.Center = wall - Way * TipAhead;
                Projectile.velocity = Vector2.Zero;
                Go(1);
                Projectile.netUpdate = true;
                Break(wall);
                return;
            }

            // Off the wall it meets as off a mirror: the way it comes, turned about the wall's own line square to it; as fast
            // as it came. At a corner, the wall it meets first along its way.
            Vector2 from = Tip - Projectile.velocity;
            Vector2 hit = WallFrom(from, Way, out int edge);
            Vector2 n = Decagon.Normal(edge);
            Vector2 v = Projectile.velocity;

            if (Vector2.Dot(v, n) > 0f)
                Projectile.velocity = v - 2f * Vector2.Dot(v, n) * n;

            facing = Projectile.velocity.ToRotation();
            Projectile.Center = hit - Way * TipAhead + Way * 2f;
            bouncesSeen++;
            Projectile.netUpdate = true;
            Zing(hit);
        }

        private void Zing(Vector2 at)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(EmpressSounds.Ricochet with { Pitch = 0.1f * bouncesSeen, Volume = 0.8f, MaxInstances = 6 }, at);
            PADParticleHandler.SpawnParticle(new CrownGlint(at, 90f, 16, Color.White));
            SparkShard.Spray(at, Color.White, 6, 5f, 36f, 12, palette: EmpressArt.Sparks);
        }

        private void Break(Vector2 at)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(EmpressSounds.Crack with { Pitch = 0.3f, Volume = 0.7f, MaxInstances = 6 }, at);
            SparkShard.Spray(at, Color.White, 14, 8f, 60f, 18, palette: EmpressArt.Sparks);
        }

        // ---- refract ------------------------------------------------------------------------------------------

        private void Refract()
        {
            switch (stage)
            {
                case 0:
                    // Fanned out of the prism, slowing to a stop.
                    Projectile.velocity *= 0.9f;

                    if (Projectile.velocity.LengthSquared() < 0.25f)
                    {
                        Projectile.velocity = Vector2.Zero;
                        Go(1);
                    }

                    break;

                case 1:
                    // Held, turning a little; at its turn, a line of light to whoever it is for (the server's choice).
                    facing += 0.02f * (float)Math.Sin(Age * 0.1f + Index);

                    if (Authority && Age >= RefractHold + Index * RefractGap)
                    {
                        Player mark = Nearest();
                        facing = mark != null ? (mark.Center - Projectile.Center).ToRotation() : facing;
                        Go(2);
                        Projectile.netUpdate = true;
                    }

                    break;

                case 2:
                    if (inStage == 1 && !Main.dedServ)
                        SoundEngine.PlaySound(EmpressSounds.Lock with { Pitch = Index * 0.08f, Volume = 0.6f, MaxInstances = 8 }, Projectile.Center);

                    if (inStage >= AimTicks)
                    {
                        Projectile.velocity = Way * DiveSpeed;
                        Go(3);
                        Projectile.netUpdate = true;
                    }

                    break;

                case 3:
                    if (inStage == 1 && !Main.dedServ)
                    {
                        SoundEngine.PlaySound(EmpressSounds.Loose with { Pitch = 0.3f, Volume = 0.6f, MaxInstances = 8 }, Projectile.Center);
                        SoundEngine.PlaySound(SoundID.Item163 with { Pitch = 0.5f, Volume = 0.35f, MaxInstances = 8 }, Projectile.Center);
                    }

                    // Out of her arena: broken on its wall.
                    if (Past(Tip) >= 0f)
                    {
                        Break(Tip);

                        if (Authority)
                            Projectile.Kill();
                    }

                    break;
            }
        }

        /// <summary>The player in her arena nearest it, alive.</summary>
        private Player Nearest()
        {
            Player best = null;
            float nearest = float.MaxValue;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];

                if (!p.active || p.dead || !EmpressArena.Contains(p.Center, -64f))
                    continue;

                float d = Vector2.DistanceSquared(p.Center, Projectile.Center);

                if (d < nearest)
                {
                    nearest = d;
                    best = p;
                }
            }

            return best;
        }

        private void Go(int next)
        {
            stage = next;
            inStage = 0;
        }

        // ---- sent ---------------------------------------------------------------------------------------------

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)stage);
            writer.Write((short)Math.Clamp(inStage, 0, short.MaxValue));
            writer.Write(facing);
            writer.Write((byte)bouncesSeen);
            writer.Write((short)Math.Clamp(Age, 0, short.MaxValue));
            writer.Write(Projectile.timeLeft);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            int got = reader.ReadByte();
            int ticks = reader.ReadInt16();
            float way = reader.ReadSingle();
            int bounced = reader.ReadByte();
            int age = reader.ReadInt16();
            Projectile.timeLeft = reader.ReadInt32();

            // Told of what it has done that this machine has not seen: seen now.
            if (What == Kind.Moor && got > stage)
            {
                if (got >= 1 && stage == 0)
                    Stuck();

                if (got == 2)
                    Burst();
            }

            if (What == Kind.Ricochet)
            {
                if (bounced > bouncesSeen)
                {
                    bouncesSeen = bounced;
                    Zing(Projectile.Center + way.ToRotationVector2() * TipAhead);
                }

                if (got == 1 && stage == 0)
                    Break(Projectile.Center + way.ToRotationVector2() * TipAhead);
            }

            if (got != stage || Math.Abs(ticks - inStage) > 6)
            {
                stage = got;
                inStage = ticks;
            }

            if (Math.Abs(age - Age) > 6)
                Age = age;

            facing = way;
        }

        // ---- what it hurts ------------------------------------------------------------------------------------

        public override bool? CanDamage() => !(What == Kind.Moor && stage == 1) && !(What == Kind.Ricochet && stage == 1) && !(What == Kind.Refract && stage == 2);

        public override bool? CanHitNPC(NPC target) => false;

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            if (What == Kind.Moor && stage == 2)
                modifiers.SourceDamage *= BurstHarder;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            EmpressOfLight her = Her;

            if (her == null || !her.InArena(targetHitbox))
                return false;

            if (What == Kind.Moor && stage == 2)
                return inStage <= BurstHurts && EmpressHitbox.Near(targetHitbox, Star, BurstRadius);

            // Its length, and as far back as it has come this tick: nothing slips between ticks.
            return EmpressHitbox.OnLine(targetHitbox, Nock - Projectile.velocity, Tip, 16f);
        }

        // ---- drawn -------------------------------------------------------------------------------------------

        public override bool PreDraw(ref Color lightColor)
        {
            SpriteBatch spriteBatch = Main.spriteBatch;
            float time = (float)Main.timeForVisualEffects;
            float fade = 1f;

            if (What == Kind.Moor && stage == 2)
            {
                DrawBurst(spriteBatch, time);
                return false;
            }

            if (stage == 1 && What == Kind.Ricochet)
                fade = 1f - inStage / 18f;

            float trail = MathHelper.Clamp(Projectile.velocity.Length() / 20f, 0f, 1f);
            float hue = What == Kind.Ricochet ? -1f : Projectile.ai[1];
            Color? tint = What == Kind.Refract ? Spectrum[Index] : null;

            // A moor stuck in the wall: its light gathering, and the reach of its burst drawn round it, closing.
            if (What == Kind.Moor && stage == 1)
                DrawCountdown(spriteBatch, time);

            // A refract's line to its mark.
            if (What == Kind.Refract && stage == 2)
            {
                float p = inStage / (float)AimTicks;
                Vector2 to = WallFrom(Projectile.Center, Way, out _);
                float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 0.8f);
                EmpressArt.DrawBeam(spriteBatch, Projectile.Center, to, 10f, EmpressArt.Additive(Colour, 0.25f * p * flicker));
                EmpressArt.DrawBeam(spriteBatch, Projectile.Center, to, 2f, EmpressArt.Additive(Color.White, 0.75f * p));
            }

            // A refract held: a soft light of its colour about it, beating.
            if (What == Kind.Refract && stage <= 2)
                EmpressArt.DrawBloom(spriteBatch, Star, 70f + 20f * (float)Math.Sin(time * 0.15f + Index), EmpressArt.Additive(Colour, 0.35f));

            EmpressBow.DrawArrow(spriteBatch, Nock, facing, 1f, fade, hue, trail, tint);
            return false;
        }

        /// <summary>The stuck arrow's three seconds: its own light beating quicker; round it, the reach of its burst, and a ring closing in from it to the arrow.</summary>
        private void DrawCountdown(SpriteBatch spriteBatch, float time)
        {
            float p = inStage / (float)StuckTicks;
            float beat = 0.5f + 0.5f * (float)Math.Sin(time * (0.15f + 0.6f * p * p));
            Vector2 at = Star;

            EmpressArt.DrawBloom(spriteBatch, at, 90f + 120f * p, EmpressArt.Additive(Colour, (0.35f + 0.45f * beat) * (0.5f + 0.5f * p)));
            EmpressArt.DrawStar(spriteBatch, at, 70f + 90f * p * beat, time * 0.05f, EmpressArt.Additive(Color.White, 0.5f + 0.5f * p));

            // Where the burst will reach: faint, and brighter as it nears.
            EmpressOfLight.DrawRing(spriteBatch, at, BurstRadius, 4f, EmpressArt.Additive(Colour, 0.12f + 0.25f * p * beat));

            // A ring closing in from the reach to the arrow over the three seconds.
            float closing = BurstRadius * (1f - p);

            if (closing > 8f)
                EmpressOfLight.DrawRing(spriteBatch, at, closing, 3f, EmpressArt.Additive(Color.White, 0.18f + 0.3f * p));
        }

        /// <summary>The burst: a sphere of light thrown out to its reach and fading, white at its heart, her colour about it.</summary>
        private void DrawBurst(SpriteBatch spriteBatch, float time)
        {
            float p = inStage / (float)BurstFade;
            float grow = 1f - (float)Math.Pow(1f - Math.Min(1f, p * 3f), 3);
            float fade = 1f - p;
            Vector2 at = Star;

            EmpressArt.DrawBloom(spriteBatch, at, BurstRadius * 2.2f * grow, EmpressArt.Additive(Colour, 0.45f * fade));
            EmpressArt.DrawBloom(spriteBatch, at, BurstRadius * 1.3f * grow, EmpressArt.Additive(Color.White, 0.6f * fade * fade));
            EmpressOfLight.DrawRing(spriteBatch, at, BurstRadius * grow, 22f * fade + 2f, EmpressArt.Additive(Color.White, 0.75f * fade));
            EmpressOfLight.DrawRing(spriteBatch, at, BurstRadius * grow * 0.92f, 40f * fade + 4f, EmpressArt.Additive(Colour, 0.35f * fade));

            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * MathHelper.TwoPi + time * 0.01f;
                EmpressArt.DrawRay(spriteBatch, at, a, BurstRadius * 1.1f * grow, 60f, EmpressArt.Additive(EmpressArt.Prism(k / 8f), 0.3f * fade));
            }
        }
    }
}
