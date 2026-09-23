using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Pugream
{
    /// <summary>
    /// Pugream's body, as it is drawn, wherever he is standing (Lore Bible §XXXI).
    ///
    /// Three layers on one 42x60 canvas, drawn back to front: the great sword that is
    /// not his, the loose end of his scarf, and him. The sword and the body never move;
    /// he stands the way a man stands who is about to start walking again, and the
    /// book forbids him an idle fidget. The scarf is the one thing on him that moves,
    /// and only because the weather moves it.
    ///
    /// All three PNGs share the canvas: the same top-left lands every layer in place.
    /// The art faces right. Facing left mirrors all three about the canvas, the way
    /// MorsIdle mirrors his.
    ///
    /// THE SCARF. The end hangs from the back of his neck and, left alone, sits almost
    /// wholly behind him; what shows is whatever the wind carries out past his outline.
    /// It is drawn one texture row at a time down a bent spine, each row turned to the
    /// angle the cloth has at that point, so it curves and ripples rather than swinging
    /// as a plank. The angle at a row is three things added together:
    ///
    ///   the lean     one angle for the whole end, pushed downwind by the wind and held
    ///                on a loose spring so a gust makes it swing and settle, not snap.
    ///                The top of the end takes a share of it and the tip all of it,
    ///                which is the curl a hanging cloth has
    ///   the wave     a ripple running from the knot to the tip, small and slow in a
    ///                calm and large and fast in a gale, growing toward the tip
    ///   the flutter  in a gale only, a quicker shiver on the last third
    ///
    /// Wind that blows toward his back streams it out behind him; wind from behind
    /// him lays it against his back, where it hides. Underground there is no wind and
    /// it hangs, with only the breath of a sway a hanging cloth has anyway.
    /// </summary>
    public abstract class PugreamBody : ModNPC
    {
        public override string Texture => "PisAndDag/Content/NPCs/Pugream/Pugream";

        // ============================================================
        // THE CANVAS
        // ============================================================

        public const int CanvasWidth = 42;
        public const int CanvasHeight = 60;

        /// <summary>The scarf end's first and only rows on the canvas, and the column its knot sits on.</summary>
        private const int ScarfTop = 32;
        private const int ScarfRows = 24;
        private const float ScarfRootX = 19f;

        // ============================================================
        // THE WEATHER
        // ============================================================
        //
        // Main.windSpeedCurrent runs to about ±0.35 on an ordinary day and ±0.85 on a
        // windy one. The scarf reads it against WindFull, so a windy day is already a
        // full gale to a strip of wool, and anything past that is clamped.

        private const float WindFull = 0.85f;

        /// <summary>The lean with no wind at all: the end hangs a little away from his back.</summary>
        private const float Rest = 0.1f;

        /// <summary>Lean per unit of wind, in radians. A full gale lays it out nearly flat.</summary>
        private const float Lean = 1.3f;

        /// <summary>How far the lean may go either way: a little past flat behind him, less in front, where he is in the way.</summary>
        private const float LeanForward = -1.2f;
        private const float LeanBack = 1.62f;

        /// <summary>How much a gust adds to or takes from the lean, as a share of it.</summary>
        private const float GustShare = 0.25f;

        /// <summary>The lean's spring. Loose and under-damped: a gust swings it past and it comes back.</summary>
        private const float Stiffness = 34f;
        private const float Damping = 0.3f;

        /// <summary>Share of the lean the knot end takes; the tip takes all of it.</summary>
        private const float Curl = 0.45f;

        /// <summary>The ripple's size at the tip, calm to gale, radians, and its rate, radians a second.</summary>
        private const float WaveCalm = 0.05f;
        private const float WaveGale = 0.45f;
        private const float WaveRateCalm = 2.5f;
        private const float WaveRateGale = 16.5f;

        /// <summary>How far behind the row above each row's ripple runs, radians: about one wave along the whole end.</summary>
        private const float WaveLag = 0.28f;

        /// <summary>The gale's shiver on the end third: its size, and where along the end it starts.</summary>
        private const float Flutter = 0.14f;
        private const float FlutterFrom = 0.7f;

        /// <summary>The breath of a sway a hanging cloth has even in still air, radians.</summary>
        private const float Breath = 0.06f;

        /// <summary>How far the knot itself is dragged downwind in a gale, in pixels.</summary>
        private const float Drag = 1f;

        /// <summary>What the one shot that needs him seen lights him to: cold, like the moon on him.</summary>
        private static readonly Color Moonlight = new Color(186, 184, 206);

        /// <summary>
        /// In that same shot the moon catches his edges: a one-pixel rim of cold light
        /// round the whole of him, sword and scarf included, so a dark figure reads
        /// against a dark night at the size the camera leaves him. How bright, at full.
        /// </summary>
        private static readonly Color Rim = new Color(196, 212, 255);
        private const float RimStrength = 0.35f;

        private static Asset<Texture2D> swordTexture;
        private static Asset<Texture2D> scarfTexture;

        /// <summary>White copies of the three layers, same alpha, for the rim. Made on the first frame that needs them.</summary>
        private static Texture2D bodyWhite;
        private static Texture2D swordWhite;
        private static Texture2D scarfWhite;

        // ============================================================
        // STATE — visual only, never sent
        // ============================================================

        /// <summary>Which way he faces: 1 right, the way the art is drawn; -1 mirrored.</summary>
        protected int Facing = 1;

        /// <summary>How far toward moonlight he is lit, 0 to 1. Only the Eye's scene sets it.</summary>
        public float Lit;

        private float lean = Rest;
        private float leanSpeed;
        private float clock;

        /// <summary>This tick's wind against the way he faces, -1 to 1: negative blows toward his back.</summary>
        private float along;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;

            // He is not a thing to catalogue.
            NPCID.Sets.NPCBestiaryDrawOffset[Type] = new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true };

            if (!Main.dedServ)
            {
                swordTexture ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Pugream/PugreamBackGreatSword");
                scarfTexture ??= ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Pugream/PugreamFlyingScarfEnd");
            }
        }

        public override void Unload()
        {
            swordTexture = null;
            scarfTexture = null;

            Texture2D body = bodyWhite;
            Texture2D sword = swordWhite;
            Texture2D scarf = scarfWhite;

            bodyWhite = null;
            swordWhite = null;
            scarfWhite = null;

            if (body != null || sword != null || scarf != null)
            {
                Main.QueueMainThreadAction(() =>
                {
                    body?.Dispose();
                    sword?.Dispose();
                    scarf?.Dispose();
                });
            }
        }

        /// <summary>A white copy of a texture with its alpha kept. Draw-time only: the device is the main thread's.</summary>
        private static Texture2D White(Texture2D source, ref Texture2D cache)
        {
            if (cache != null && !cache.IsDisposed)
                return cache;

            Color[] data = new Color[source.Width * source.Height];
            source.GetData(data);

            // Premultiplied: white at this alpha is the alpha in every channel.
            for (int i = 0; i < data.Length; i++)
            {
                byte a = data[i].A;
                data[i] = new Color(a, a, a, a);
            }

            cache = new Texture2D(Main.graphics.GraphicsDevice, source.Width, source.Height);
            cache.SetData(data);

            return cache;
        }

        public override void SetDefaults()
        {
            // The bubble does the pointing; the name he gives himself.
            NPC.ShowNameOnHover = false;

            // Exactly player-sized. The sword and the hat hang outside the box.
            NPC.width = 20;
            NPC.height = 42;

            NPC.aiStyle = -1;
            NPC.noGravity = false;
            NPC.noTileCollide = false;
            NPC.knockBackResist = 0f;

            NPC.friendly = true;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 250;
            NPC.dontTakeDamage = true;
            NPC.dontTakeDamageFromHostiles = true;
            NPC.immortal = true;
            NPC.dontCountMe = true;
            NPC.npcSlots = 0f;
            NPC.value = 0f;
            NPC.chaseable = false;

            NPC.HitSound = null;
            NPC.DeathSound = null;
        }

        public override bool? CanBeHitByItem(Player player, Item item) => false;

        public override bool? CanBeHitByProjectile(Projectile projectile) => false;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        public override bool CanChat() => false;

        /// <summary>The wind where he stands. None underground, where the weather does not reach.</summary>
        protected virtual float Wind => NPC.Center.Y > Main.worldSurface * 16f ? 0f : Main.windSpeedCurrent;

        // ============================================================
        // THE SCARF, MOVING
        // ============================================================

        /// <summary>Runs on the drawing client every update, whatever the network is doing.</summary>
        public override void FindFrame(int frameHeight)
        {
            if (Main.dedServ)
                return;

            const float dt = 1f / 60f;

            clock += dt;
            along = MathHelper.Clamp(Wind / WindFull, -1f, 1f) * Facing;

            float strength = Math.Abs(along);
            float t = clock;

            // Three sines that never line up, so the gusting never repeats in a way
            // anyone would catch.
            float gust = 0.55f * (float)Math.Sin(1.7f * t) + 0.3f * (float)Math.Sin(4.3f * t + 1f)
                + 0.15f * (float)Math.Sin(9.1f * t + 2f);

            float want = Rest - along * Lean * (1f + GustShare * gust * strength)
                + Breath * (float)Math.Sin(0.8f * t) * (1f - strength);

            want = MathHelper.Clamp(want, LeanForward, LeanBack);

            float damping = 2f * (float)Math.Sqrt(Stiffness) * Damping;

            leanSpeed += (Stiffness * (want - lean) - damping * leanSpeed) * dt;
            lean += leanSpeed * dt;
        }

        /// <summary>The angle of one row of the scarf, from hanging straight down; positive swings it away from his back.</summary>
        private float RowAngle(int row)
        {
            float strength = Math.Abs(along);
            float s = (row + 0.5f) / ScarfRows;

            float wave = MathHelper.Lerp(WaveCalm, WaveGale, (float)Math.Pow(strength, 1.3));
            float rate = MathHelper.Lerp(WaveRateCalm, WaveRateGale, strength);

            float angle = lean * (Curl + (1f - Curl) * s)
                + wave * (float)Math.Pow(s, 1.4) * (float)Math.Sin(rate * clock - WaveLag * row);

            if (s > FlutterFrom)
            {
                angle += Flutter * strength * strength * (float)Math.Sin(2.6f * rate * clock - 1.1f * row)
                    * (s - FlutterFrom) / (1f - FlutterFrom);
            }

            return angle;
        }

        // ============================================================
        // THE DRAW
        // ============================================================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (swordTexture == null || scarfTexture == null)
                return true;

            Texture2D body = TextureAssets.Npc[Type].Value;

            // Whole pixels, or the layers shimmer against each other.
            Vector2 topLeft = new Vector2(
                (int)(NPC.Center.X - CanvasWidth * 0.5f - screenPos.X),
                (int)(NPC.Bottom.Y - CanvasHeight - screenPos.Y + NPC.gfxOffY));

            bool flip = Facing < 0;
            SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color colour = NPC.GetAlpha(drawColor);
            float lit = MathHelper.Clamp(Lit, 0f, 1f);

            if (lit > 0f)
            {
                colour = Color.Lerp(colour, Moonlight * (colour.A / 255f), lit);

                // 0. The rim, under everything, a pixel out all round: the layers drawn
                // over it cover the middle and leave the edge.
                DrawRim(spriteBatch, body, topLeft, flip, effects, lit * (colour.A / 255f));
            }

            // 1. The sword, at the back.
            spriteBatch.Draw(swordTexture.Value, topLeft, null, colour, 0f, Vector2.Zero, 1f, effects, 0f);

            // 2. The scarf, over the sword and under him.
            DrawScarf(spriteBatch, scarfTexture.Value, topLeft, flip, effects, colour);

            // 3. Him.
            spriteBatch.Draw(body, topLeft, null, colour, 0f, Vector2.Zero, 1f, effects, 0f);

            return false;
        }

        /// <summary>
        /// All three layers in white, added rather than laid over (no alpha), at the eight
        /// one-pixel steps round where he stands.
        /// </summary>
        private void DrawRim(SpriteBatch spriteBatch, Texture2D body, Vector2 topLeft, bool flip, SpriteEffects effects, float strength)
        {
            Texture2D sword = White(swordTexture.Value, ref swordWhite);
            Texture2D scarf = White(scarfTexture.Value, ref scarfWhite);
            Texture2D him = White(body, ref bodyWhite);

            Color glow = Rim * (RimStrength * strength);
            glow.A = 0;

            for (int i = 0; i < 8; i++)
            {
                Vector2 step = new Vector2(
                    (float)Math.Round(Math.Cos(MathHelper.TwoPi * i / 8f)),
                    (float)Math.Round(Math.Sin(MathHelper.TwoPi * i / 8f)));

                Vector2 at = topLeft + step;

                spriteBatch.Draw(sword, at, null, glow, 0f, Vector2.Zero, 1f, effects, 0f);
                DrawScarf(spriteBatch, scarf, at, flip, effects, glow);
                spriteBatch.Draw(him, at, null, glow, 0f, Vector2.Zero, 1f, effects, 0f);
            }
        }

        /// <summary>
        /// The scarf end, a row at a time down its bent spine. Each row is laid two
        /// pixels tall so it overlaps the next and no light gets through the bend. Worked
        /// in the art's own frame (facing right) and mirrored at the last moment: the
        /// point, the pivot and the turn all flip together, so a mirrored scarf is the
        /// same scarf.
        /// </summary>
        private void DrawScarf(SpriteBatch spriteBatch, Texture2D scarf, Vector2 topLeft, bool flip,
            SpriteEffects effects, Color colour)
        {
            // The knot is dragged a pixel downwind in a gale.
            float x = ScarfRootX + along * Drag;
            float y = ScarfTop;

            Vector2 pivot = new Vector2(flip ? CanvasWidth - ScarfRootX : ScarfRootX, 0f);

            for (int row = 0; row < ScarfRows; row++)
            {
                float angle = RowAngle(row);
                int tall = row == ScarfRows - 1 ? 1 : 2;

                Rectangle source = new Rectangle(0, ScarfTop + row, CanvasWidth, tall);
                Vector2 at = new Vector2(flip ? CanvasWidth - x : x, y);

                spriteBatch.Draw(scarf, topLeft + at, source, colour, flip ? -angle : angle, pivot, 1f, effects, 0f);

                // Down the spine to the next row, one pixel along the cloth.
                x -= (float)Math.Sin(angle);
                y += (float)Math.Cos(angle);
            }
        }
    }
}
