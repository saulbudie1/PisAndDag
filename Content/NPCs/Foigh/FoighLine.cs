using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// A straight cut of his where nothing is. By request: "the straight slashes stuff ...
    /// like this plunge", "do damage", and "slashes aoe must always be straight cuts". Two
    /// of his moves let them out, one after another: his sword driven deeper, in his third
    /// phase (Foigh.Driven: fourteen across the ten tiles about the blade); and the ball, in
    /// any phase (Foigh.Burst: sixteen across the eighteen tiles about him in his first
    /// phase, twenty-three in his second, twenty-seven in his third). A straight line at any
    /// slant through the ground and the air: shown first as a hair of dark red that
    /// brightens, and then, all along its length at once, open: a cut of his burning sword,
    /// red with its black twin, hanging a moment. The ball's in his first two phases are
    /// white, by request ("its white, slashes not red black blood"): a hair of steel that
    /// brightens, and open, a cut of white steel with nothing black about it, as his sword's
    /// cuts are before it burns.
    ///
    /// It does not move. Open, for a few ticks, it cuts whoever it lies through: half his
    /// sword's blow, through armour, varied as vanilla varies any hit, once, against each
    /// machine's own player; and in his third phase the wound his sword leaves (Ultra
    /// Bleeding). While it shows it can be stepped out of, and jumped where it lies low;
    /// and it can be parried, whichever way they face (it lies through them, as the late
    /// cut does: it is the moment that has to be right): a parry already begun when it
    /// opens turns it aside, its wind-up and all (ParryPlayer.TakesLine). Turned aside it
    /// is through with whoever turned it, and opens for everybody else as it would.
    ///
    /// The ball's cut down what is called up to fight for a player (Foigh.Called), by
    /// request ("4/9 chance", and "killing 1 stardust dragon means killing the whole
    /// dragon"): open, each cuts down whatever of that it goes through, a minion or a sentry
    /// (not what those shoot), each tried once; and any part of the Stardust Dragon with the
    /// whole of it. Four times in nine in his first phase; by request half as often again in
    /// his second ("1.5x phase 1": six in nine), and three times as often in his third ("3x
    /// phase 1": twelve in nine is more than every time, so every time). As the ball was in
    /// when it let the cut out, sent with it. On the machine whose it is, as the game kills
    /// anything of a player's and tells every other machine (Projectile.Kill).
    ///
    /// No two alike, by request ("they dont start and end at the same time, they have
    /// variations ranges and different duration, start and ends", and "should not go thru a
    /// same point"): each is let out (Let) with a length and a place of its own, through a
    /// point of its own, and its own ticks shown before it opens and hanging after; and the
    /// moves that let them out do so at uneven ticks.
    ///
    /// No picture of its own: it is drawn in bands along its length, as the late cut's line
    /// is, with the picture those are drawn with.
    /// </summary>
    public class FoighLine : ModProjectile
    {
        /// <summary>None of its own (and it is never drawn as a sprite): its bands'.</summary>
        public override string Texture => "PisAndDag/Content/Graphics/SoftLine";

        /// <summary>
        /// Ticks one shows before it opens, the fewest and the most (each its own, Let);
        /// ticks it cuts for, open, every one alike; and ticks it hangs after, the fewest
        /// and the most.
        /// </summary>
        public const int ShowsLeast = 12;
        public const int ShowsMost = 22;
        public const int Opens = 3;
        public const int HangsLeast = 6;
        public const int HangsMost = 28;

        /// <summary>How thick it is where it cuts, in pixels.</summary>
        public const float Width = 14f;

        /// <summary>Of his sword's blow: half, as his slashes.</summary>
        public const float Share = FoighSlash.Share;
        private const float Knockback = 4f;

        /// <summary>
        /// The ball's cut down what fights for a player that they go through this many times
        /// in nine (CutDown): in his first phase, his second and his third.
        /// </summary>
        public const int CutsOf = 9;
        public const int CutsDownFirst = 4;
        public const int CutsDownSecond = 6;
        public const int CutsDownThird = 9;

        /// <summary>Of nine, how many times the ball's cuts cut down what they go through, in this phase of his.</summary>
        public static int CutsDown(int phase) => phase >= 3 ? CutsDownThird : phase == 2 ? CutsDownSecond : CutsDownFirst;

        private static Asset<Texture2D> softLine;

        // ai[0] his whoAmI · ai[1] the way it lies, in radians · ai[2] how far it runs each side of its middle
        // localAI[0] 1 once it is through with this machine's player · localAI[1] the ticks this machine has seen of it

        /// <summary>It is through with the Everclear Mirror's copy, a second player on this machine: as localAI[0] is for this machine's player.</summary>
        private bool throughCopy;

        /// <summary>Whether it is through with whoever is this machine's player for the moment (the copy, under MimicSystem.AlsoOnCopy or while it parries).</summary>
        private static bool Through(Projectile projectile) =>
            MimicSystem.Is(Main.LocalPlayer) ? (projectile.ModProjectile as FoighLine)?.throughCopy ?? false : projectile.localAI[0] != 0f;

        /// <summary>It is through with this player: this machine's, or the copy.</summary>
        private static void ThroughWith(Projectile projectile, Player player)
        {
            if (MimicSystem.Is(player))
            {
                if (projectile.ModProjectile is FoighLine line)
                    line.throughCopy = true;
            }
            else
            {
                projectile.localAI[0] = 1f;
            }
        }

        /// <summary>Ticks this one shows before it opens, and hangs after it has cut: as it was let out (Let), and sent with it to every machine.</summary>
        private int shows = (ShowsLeast + ShowsMost) / 2;
        private int hangs = (HangsLeast + HangsMost) / 2;

        /// <summary>The ball's: it cuts down what fights for a player (CutDown). As it was let out, and sent with it.</summary>
        private bool ball;

        /// <summary>White (the ball's in his first two phases), not red and black. As it was let out, and sent with it.</summary>
        private bool pale;

        /// <summary>The ball's: of nine, how many times it cuts down what it goes through. As it was let out, and sent with it.</summary>
        private int downs;

        /// <summary>What of this machine's player's that fights for them it has tried to cut down, by their identity: each once.</summary>
        private HashSet<int> tried;

        /// <summary>The ticks this machine has seen of it.</summary>
        public static int Age(Projectile projectile) => (int)projectile.localAI[1];

        /// <summary>Ticks it shows before it opens.</summary>
        public static int ShowsOf(Projectile projectile) => projectile.ModProjectile is FoighLine line ? line.shows : ShowsLeast;

        /// <summary>Ticks it hangs after it has cut.</summary>
        public static int HangsOf(Projectile projectile) => projectile.ModProjectile is FoighLine line ? line.hangs : HangsLeast;

        /// <summary>Whether it cuts as it is: open.</summary>
        public static bool Open(Projectile projectile)
        {
            int age = Age(projectile);
            int shows = ShowsOf(projectile);

            return age > shows && age <= shows + Opens;
        }

        /// <summary>The way it lies.</summary>
        public static Vector2 Along(Projectile projectile) => projectile.ai[1].ToRotationVector2();

        /// <summary>Its two ends.</summary>
        public static void Ends(Projectile projectile, out Vector2 from, out Vector2 to)
        {
            Vector2 half = Along(projectile) * projectile.ai[2];

            from = projectile.Center - half;
            to = projectile.Center + half;
        }

        /// <summary>Whether it lies through this player, as thick as it cuts.</summary>
        public static bool LiesThrough(Projectile projectile, Player player)
        {
            if (player == null || !player.active || player.dead)
                return false;

            Ends(projectile, out Vector2 from, out Vector2 to);

            float point = 0f;
            return Collision.CheckAABBvLineCollision(player.position, player.Size, from, to, Width, ref point);
        }

        /// <summary>What a line is let out by: him, its own ticks, whether it is the ball's, whether it is white, and how often it cuts down, as far as OnSpawn.</summary>
        private sealed class Timed : EntitySource_Parent
        {
            public readonly int Shows;
            public readonly int Hangs;
            public readonly bool Ball;
            public readonly bool Pale;
            public readonly int Downs;

            public Timed(Entity by, int shows, int hangs, bool ball, bool pale, int downs) : base(by, null)
            {
                Shows = shows;
                Hangs = hangs;
                Ball = ball;
                Pale = pale;
                Downs = downs;
            }
        }

        /// <summary>
        /// Lets one out, where the world is decided: lying <paramref name="turn"/> (radians)
        /// through <paramref name="middle"/>, <paramref name="half"/> either side of it; shown
        /// <paramref name="shows"/> ticks before it opens, and hanging <paramref name="hangs"/>
        /// after it has cut (each between the fewest and the most: his moves draw them so);
        /// the ball's (<paramref name="ball"/>) cutting down what fights for a player, this
        /// many times in nine (<paramref name="downs"/>); white, not red and black
        /// (<paramref name="pale"/>).
        /// </summary>
        public static void Let(NPC by, Vector2 middle, float turn, float half, int shows, int hangs, bool ball, bool pale = false, int downs = 0)
        {
            Projectile.NewProjectile(new Timed(by, shows, hangs, ball, pale, downs), middle, Vector2.Zero, ModContent.ProjectileType<FoighLine>(), 0, 0f, Main.myPlayer,
                by.whoAmI, turn, half);
        }

        /// <summary>Its own ticks, whether it is the ball's, whether it is white, and how often it cuts down, where it is let out: before the game sends it to anybody (Projectile.NewProjectile), so that they go with it.</summary>
        public override void OnSpawn(IEntitySource source)
        {
            if (source is Timed timed)
            {
                shows = timed.Shows;
                hangs = timed.Hangs;
                ball = timed.Ball;
                pale = timed.Pale;
                downs = timed.Downs;
            }
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)shows);
            writer.Write((byte)hangs);
            writer.Write(ball);
            writer.Write(pale);
            writer.Write((byte)downs);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            shows = reader.ReadByte();
            hangs = reader.ReadByte();
            ball = reader.ReadBoolean();
            pale = reader.ReadBoolean();
            downs = reader.ReadByte();
        }

        public override void Load()
        {
            if (Main.dedServ)
                return;

            softLine = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
        }

        public override void Unload()
        {
            softLine = null;
        }

        public override void SetStaticDefaults()
        {
            // Open, not already through with this machine's player, and lying through them.
            // Turned aside, it is through with them (and opens for everybody else).
            Parry.AddProjectile(Type, projectile => !Through(projectile) && Open(projectile) && LiesThrough(projectile, Main.LocalPlayer),
                (projectile, by) => ThroughWith(projectile, by));
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = ShowsMost + Opens + HangsMost + 20;
        }

        private Foigh Owner()
        {
            int i = (int)Projectile.ai[0];

            if (i < 0 || i >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[i];
            return npc.active ? npc.ModNPC as Foigh : null;
        }

        public override void AI()
        {
            int age = Age(Projectile) + 1;

            Projectile.localAI[1] = age;
            Projectile.velocity = Vector2.Zero;

            if (age > shows + Opens + hangs)
            {
                Projectile.Kill();
                return;
            }

            Foigh foigh = Owner();

            // He is gone, or down, before it has opened: it does not.
            if ((foigh == null || !foigh.Fighting) && age <= shows)
            {
                Projectile.Kill();
                return;
            }

            if (age == shows + 1)
                Opened();

            if (age > shows)
            {
                float left = 1f - (age - shows - 1) / (float)(Opens + hangs);
                Vector2 along = Along(Projectile);

                // Its light: red, or, white, the light of steel.
                Vector3 light = pale ? new Vector3(0.42f, 0.45f, 0.5f) : new Vector3(0.6f, 0.06f, 0.05f);

                for (int i = -2; i <= 2; i++)
                    Lighting.AddLight(Projectile.Center + along * (Projectile.ai[2] * i * 0.4f), light * left);
            }

            if (!Main.dedServ && foigh != null && foigh.Fighting && Open(Projectile))
            {
                Strike(foigh);

                // And the Everclear Mirror's copy, as a second player: it cuts each of them once.
                MimicSystem.AlsoOnCopy(() => Strike(foigh));

                // The ball's cut down what fights for either of them (the copy's, on this machine, as its own).
                if (ball)
                {
                    CutDown();
                    MimicSystem.AlsoOnCopy(CutDown);
                }
            }
        }

        /// <summary>The tick it opens, on a machine with a screen: seen and heard all along it. White: a cut of steel and its sparks; else his burning sword's, and its embers.</summary>
        private void Opened()
        {
            if (Main.dedServ)
                return;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            Vector2 along = Along(Projectile);
            float half = Projectile.ai[2];
            int knots = Math.Max(2, (int)(half * 2f / 64f));

            if (pale)
                PADParticleHandler.SpawnParticle(new SlashStreak(Projectile.Center, along, half + 8f, 16f, 22));
            else
                BloodSlash.Pair(Projectile.Center, along, half + 8f, 12f, 22);

            for (int i = 0; i <= knots; i++)
            {
                Vector2 at = Vector2.Lerp(from, to, i / (float)knots);

                if (pale)
                {
                    SparkShard.Spray(at, Foigh.SteelColour, 2, 4f, 16f, 12, falls: true, palette: Foigh.SparkColours);
                    continue;
                }

                PADParticleHandler.SpawnParticle(new Ember(at, new Vector2(-along.Y, along.X) * Main.rand.NextFloat(-1.6f, 1.6f) + along * Main.rand.NextFloat(-1f, 1f),
                    Foigh.Cinder, Main.rand.NextFloat(0.45f, 0.8f), Main.rand.Next(14, 26)));
            }

            SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaivePierce with { Pitch = pale ? -0.2f : -0.45f, Volume = 0.55f, PitchVariance = 0.12f, MaxInstances = 3 }, Projectile.Center);
        }

        /// <summary>Against this machine's own player (or the copy, under MimicSystem.AlsoOnCopy), lying through them: the parry first; then half his sword's blow through armour, and in his third phase its wound. Once each.</summary>
        private void Strike(Foigh foigh)
        {
            if (Through(Projectile))
                return;

            Player me = Main.LocalPlayer;

            if (!LiesThrough(Projectile, me))
                return;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            // Turned aside, whichever way they face: through with them (Parry.AddProjectile's turned).
            if (me.GetModPlayer<ParryPlayer>().TakesLine(Projectile, from, to, true))
                return;

            // Thrown off it: away from it, whichever side of it they are on.
            Vector2 along = Along(Projectile);
            Vector2 across = new Vector2(-along.Y, along.X);
            float side = Vector2.Dot(me.Center - Projectile.Center, across);
            float away = across.X * Math.Sign(side);
            int direction = Math.Abs(away) > 0.2f ? Math.Sign(away) : me.Center.X >= Projectile.Center.X ? 1 : -1;

            int damage = Main.DamageVar(foigh.SwordDamage() * Share, -me.luck);

            // (Not while the game's grace after a hit lasts: Player.Hurt takes nothing then.)
            if (!Foigh.Pierce(me, "FoighLine", damage, direction, Knockback))
                return;

            ThroughWith(Projectile, me);

            // His sword's cut, and in his third phase it leaves what his sword leaves.
            if (foigh.Phase >= 3)
                UltraBleeding.Wound(me);

            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.6f, Volume = 1f }, me.Center);
            SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.3f, Volume = 0.6f }, me.Center);
            me.PIS().BumpScreenShake(5f, 10);

            // Seen on them along the line it cut: white, or his burning sword's.
            if (pale)
                PADParticleHandler.SpawnParticle(new SlashStreak(me.Center, along, 44f, 9f, 16));
            else
                BloodSlash.Pair(me.Center, along, 44f, 6f, 18);

            BloodDroplet.SpawnBurst(me.Center, across * (side >= 0f ? 1f : -1f), 10, 46f);
        }

        /// <summary>
        /// The ball's, open, on a machine with a player: whatever of this machine's player's
        /// it goes through that was called up to fight for them (a minion, a sentry) is cut
        /// down as often in nine as it was let out with (<see cref="downs"/>: four, six, or
        /// all nine), each tried once; a part of the Stardust Dragon with the whole of it.
        /// </summary>
        private void CutDown()
        {
            Ends(Projectile, out Vector2 from, out Vector2 to);

            List<Projectile> down = null;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner != Main.myPlayer || (!p.minion && !p.sentry))
                    continue;

                float point = 0f;

                if (!Collision.CheckAABBvLineCollision(p.position, p.Size, from, to, Width, ref point))
                    continue;

                tried ??= new HashSet<int>();

                if (tried.Add(p.identity) && Main.rand.Next(CutsOf) < downs)
                    (down ??= new List<Projectile>()).Add(p);
            }

            if (down == null)
                return;

            Vector2 along = Along(Projectile);

            foreach (Projectile p in down)
            {
                // (A part of a dragon already cut down with the rest of it.)
                if (!p.active)
                    continue;

                if (!Foigh.Dragon(p))
                {
                    Down(p, along, pale);
                    continue;
                }

                List<Projectile> whole = new List<Projectile>();

                foreach (Projectile part in Main.ActiveProjectiles)
                {
                    if (part.owner == p.owner && Foigh.Dragon(part))
                        whole.Add(part);
                }

                foreach (Projectile part in whole)
                    Down(part, along, pale);
            }
        }

        /// <summary>One thing cut down: the cut seen across it (white, or his burning sword's), and it is gone (its owner's, so every machine is told).</summary>
        private static void Down(Projectile projectile, Vector2 along, bool pale)
        {
            float reach = 16f + Math.Max(projectile.width, projectile.height) * 0.5f;

            if (pale)
                PADParticleHandler.SpawnParticle(new SlashStreak(projectile.Center, along, reach, 8f, 14));
            else
                BloodSlash.Pair(projectile.Center, along, reach, 5f, 16);

            projectile.Kill();
        }

        // ============================================================
        // DRAWING
        // ============================================================

        public override bool PreDraw(ref Color lightColor)
        {
            if (softLine == null || !softLine.IsLoaded)
                return false;

            int age = Age(Projectile);

            if (age <= 0)
                return false;

            Ends(Projectile, out Vector2 from, out Vector2 to);

            Main.spriteBatch.End();

            if (age <= shows && pale)
            {
                // White, showing: a hair of dusk under it, so that it is seen against a bright
                // wall; and a hair of steel along it, brighter by the tick until it opens.
                float shown = MathHelper.Clamp(age / 4f, 0f, 1f);
                float lit = shown * (0.35f + 0.65f * age / shows);

                Band(from, to, 2.6f, Dark(Foigh.DuskColour, 0.5f * shown));
                Band(from, to, 7f, Light(Foigh.SteelColour, 0.2f * lit));
                Band(from, to, 1.1f, Light(Color.White, 0.9f * lit));
            }
            else if (age <= shows)
            {
                // Showing: a hair of dark, so that it is seen against a bright wall; and a
                // hair of red along it, brighter by the tick until it opens.
                float shown = MathHelper.Clamp(age / 4f, 0f, 1f);
                float lit = shown * (0.35f + 0.65f * age / shows);

                Band(from, to, 2.6f, Dark(Foigh.Soot, 0.6f * shown));
                Band(from, to, 7f, Light(Foigh.BurnRed, 0.22f * lit));
                Band(from, to, 1.1f, Light(Foigh.TrailRed, 0.9f * lit));
            }
            else if (pale)
            {
                // White, open: wide and bright at once, and thinner and dimmer to nothing.
                // Nothing black beside it.
                float t = MathHelper.Clamp((age - shows - 1) / (float)(Opens + hangs), 0f, 1f);
                float left = 1f - Easing.Smoother(t);

                Band(from, to, 16f * left, Light(Foigh.SteelColour, 0.45f * left));
                Band(from, to, 2f + 4f * left, Light(Color.White, 0.95f * left));
                Band(from, to, 1.4f, Light(Color.White, left));
            }
            else
            {
                // Open: wide and bright at once, and thinner and dimmer to nothing; its black
                // twin beside it, the last of it to go.
                float t = MathHelper.Clamp((age - shows - 1) / (float)(Opens + hangs), 0f, 1f);
                float left = 1f - Easing.Smoother(t);
                Vector2 along = to - from;
                Vector2 aside = new Vector2(-along.Y, along.X).SafeNormalize(Vector2.UnitY) * 7f;

                Band(from + aside, to + aside, 1.5f + 3f * left, Dark(Foigh.Soot, 0.85f * (1f - t)));
                Band(from, to, 16f * left, Light(Foigh.BurnRed, 0.5f * left));
                Band(from, to, 2f + 4f * left, Light(Foigh.TrailRed, 0.95f * left));
                Band(from, to, 1.4f, Light(Foigh.BurnHot, left));
            }

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, Main.Transform);

            return false;
        }

        /// <summary>One band along it, this wide to either side, this colour at its strongest: thinning to nothing at both ends.</summary>
        private static void Band(Vector2 from, Vector2 to, float half, Color colour)
        {
            const int steps = 24;

            Vector2 along = to - from;
            float length = along.Length();

            if (length < 2f || half <= 0f)
                return;

            along /= length;

            Vector2 across = new Vector2(-along.Y, along.X);

            Strip.Begin();

            for (int i = 0; i <= steps; i++)
            {
                float k = i / (float)steps;
                Color here = colour * MathHelper.Clamp(Math.Min(k, 1f - k) / 0.1f, 0f, 1f);
                Vector2 at = from + along * (length * k);

                Strip.Pair(at - across * half, at + across * half, here, here, 0.5f);
            }

            Strip.Draw(softLine.Value);
        }

        /// <summary>A colour as light: added to what is there. And as soot: over it.</summary>
        private static Color Light(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 0) * strength;

        private static Color Dark(Color colour, float strength) => new Color(colour.R, colour.G, colour.B, 255) * strength;
    }
}
