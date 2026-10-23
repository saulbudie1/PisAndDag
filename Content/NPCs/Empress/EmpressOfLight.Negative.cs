using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the negative (Legends act, her second half) -------------------------------------------------------------
    //
    // By request: "Inverts the color of everything and makes it lerp hue with time, while her normal vanilla attack play
    // for 3 attacks".
    //
    //     TURN    48   she holds her bow up over her; out of her the world turns inside out: a circle of it, every colour
    //                  in it its own opposite, swelling out past the edges of the screen, a ring of her light at its edge
    //                  (a chord swelling, and turned over)
    //     then vanilla fights on, three of its moves, in the world turned inside out, its colours going round her own (a
    //     wash of them over it, turning); and it comes back in to her, its ring closing, and is gone.
    //
    // Only seen: none of it hurts. Drawn over the world and under the interface (Cinematic.Cover) with the game's own
    // blending, no shader: white drawn so that it takes from each colour its own amount (what is there turned to one
    // less it), then her colour laid over that by multiplying. A colour cannot be turned round the wheel without a shader;
    // the wash going round her colours is how it goes round here.

    public partial class EmpressOfLight
    {
        /// <summary>The world is turned inside out (vanilla fighting on in it), and how many of vanilla's moves it has had. Sent.</summary>
        private bool negativeOn;
        private int negativeDone;

        /// <summary>Each machine's own: ticks it has been turned (for the wash's going round), ticks of it coming back into her, and whether it was turned last tick.</summary>
        private int negativeTicks;
        private int negativeFade;
        private bool negativeWas;

        private const int NegativeMoves = 3;
        private const int NegTurnTicks = 48;
        private const int NegativeFadeTicks = 40;

        /// <summary>Longest it lasts, whatever vanilla does (the server's).</summary>
        private const int NegativeMost = 60 * 24;

        /// <summary>A wash of her colours over the turned world: how strong, and how long it takes to go round them all.</summary>
        private const float WashStrength = 0.42f;
        private const float WashTurn = 300f;

        // ---- what the cover draws (one at a time: the last asked) ------------------------------------------------

        /// <summary>The turned circle this frame: its middle on the screen, its radius (screen pixels; past the corners: all), its wash's colour.</summary>
        private static Vector2 negCentre;
        private static float negRadius;
        private static Color negWash;
        private static Texture2D disc;

        /// <summary>The colour subtracted from its own opposite: one less what is there, where white is drawn (dst x (1 - src) + src x (1 - dst)).</summary>
        private static readonly BlendState Inverting = new BlendState
        {
            ColorSourceBlend = Blend.InverseDestinationColor,
            ColorDestinationBlend = Blend.InverseSourceColor,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
        };

        /// <summary>A colour multiplied into what is there.</summary>
        private static readonly BlendState Washing = new BlendState
        {
            ColorSourceBlend = Blend.DestinationColor,
            ColorDestinationBlend = Blend.Zero,
            ColorBlendFunction = BlendFunction.Add,
            AlphaSourceBlend = Blend.Zero,
            AlphaDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Add,
        };

        private void PlanNegative(NPC npc, Player target)
        {
            negativeDone = 0;
        }

        /// <summary>TURN: the bow held up; the world turned out of her. Then vanilla's, the world left turned.</summary>
        private void RunNegative(NPC npc, Player who, bool entered)
        {
            int t = timer;

            npc.velocity *= 0.9f;
            HoldBowUp(BowLeft(npc) ? -2.2f : -0.94f, BowLeft(npc) ? MathHelper.Pi + 0.25f : -0.25f);
            FreeArmFrame(4);

            if (entered && !Main.dedServ)
            {
                Sound(EmpressSounds.Invert, npc.Center, 0f, 1f, 1);
                Sound(SoundID.Item29, npc.Center, -0.8f, 0.7f);
                Sound(EmpressSounds.WhooshUp, npc.Center, -0.3f, 0.6f, 2);
            }

            // At the turn of it, glass, and the world shaken through.
            if (Crossed(20) && !Main.dedServ)
            {
                Sound(EmpressSounds.Shock, npc.Center, 0.3f, 0.6f, 2);
                EmpressWarp.Ripple(npc.Center, 34f, 90f, 14f, 40);
                PADParticleHandler.SpawnParticle(new CrownGlint(npc.Center, 180f, 30, Color.White));
            }

            if (!Main.dedServ && NearArena())
            {
                float grown = Smoother(t / (float)NegTurnTicks);
                AskNegative(npc.Center, grown, 0);
            }

            if (Authority && t >= NegTurnTicks)
            {
                EndAct(npc);
                negativeOn = true;
                negativeDone = 0;
                negativeTicks = 0;
                npc.netUpdate = true;
            }
        }

        /// <summary>After vanilla (SecondAfter): its moves counted; three, and it goes back into her. The server's.</summary>
        private void NegativeAfter(NPC npc, bool done)
        {
            if (!Authority)
                return;

            if (done)
                negativeDone++;

            if (negativeDone >= NegativeMoves || negativeTicks >= NegativeMost)
                EndNegative(npc);
        }

        private void EndNegative(NPC npc)
        {
            negativeOn = false;
            npc.netUpdate = true;
        }

        /// <summary>Every tick, every machine (PostAI): the turned world kept, or going back into her.</summary>
        private void NegativeTick(NPC npc)
        {
            // Her death, or her leaving the second half, takes it at once (the server says; every machine stops asking).
            bool cut = dying || !LegendsSecond(npc);

            if (negativeOn && cut && Authority)
            {
                negativeOn = false;
                npc.netUpdate = true;
            }

            // Over (on this machine, as the server's word comes): back into her, the chord turned back.
            if (negativeWas && !negativeOn)
            {
                negativeFade = cut ? 0 : NegativeFadeTicks;

                if (!cut)
                    Sound(EmpressSounds.Invert, npc.Center, 0.25f, 0.7f, 1);
            }

            // Begun (on this machine, as the server's word comes): its wash starts from nothing.
            if (!negativeWas && negativeOn)
                negativeTicks = 0;

            negativeWas = negativeOn;

            if (negativeOn && !cut)
            {
                negativeTicks++;

                if (!Main.dedServ && NearArena())
                    AskNegative(npc.Center, 1f, negativeTicks);

                return;
            }

            if (negativeFade > 0)
            {
                negativeFade--;

                if (!Main.dedServ && NearArena())
                    AskNegative(npc.Center, Smoother(negativeFade / (float)NegativeFadeTicks), negativeTicks + NegativeFadeTicks - negativeFade);
            }
        }

        /// <summary>How far a point on the screen is from the screen's farthest corner.</summary>
        private static float FarthestCorner(Vector2 at)
        {
            float x = Math.Max(at.X, Main.screenWidth - at.X);
            float y = Math.Max(at.Y, Main.screenHeight - at.Y);
            return (float)Math.Sqrt(x * x + y * y);
        }

        /// <summary>The cover's drawing, made once (it is handed over every tick it is up).</summary>
        private static readonly Action<SpriteBatch> NegativeCover = DrawNegativeCover;

        /// <summary>The cover asked for this tick: the circle out of <paramref name="from"/>, grown so far (1: past every corner), the wash at its turn.</summary>
        private static void AskNegative(Vector2 from, float grown, int turned)
        {
            if (grown <= 0.003f)
                return;

            Vector2 centre = Vector2.Transform(from - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
            float corners = 0f;

            corners = FarthestCorner(centre);

            negCentre = centre;
            negRadius = grown * (corners + 60f);

            // The wash: in as it turns, then going round her colours.
            float wash = WashStrength * Math.Min(1f, turned / 40f) * Math.Min(1f, grown * 1.5f);
            negWash = Color.Lerp(Color.White, EmpressArt.Prism(turned / WashTurn), wash);

            Cinematic.Cover(NegativeCover);
        }

        /// <summary>
        /// The turned world, in raw screen pixels (the cover's batch open, no transform, as given back): the circle of it
        /// turned, then washed in her colour going round; the batch begun again as it was.
        /// </summary>
        private static void DrawNegativeCover(SpriteBatch spriteBatch)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle screen = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            float far = Vector2.Distance(Vector2.Zero, new Vector2(Main.screenWidth, Main.screenHeight)) * 2f;
            bool whole = negRadius >= far * 0.5f;
            Texture2D round = whole ? null : Disc();

            spriteBatch.End();
            bool open = false;

            try
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, Inverting, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                open = true;

                if (round == null)
                    spriteBatch.Draw(pixel, screen, Color.White);
                else
                    spriteBatch.Draw(round, negCentre, null, Color.White, 0f, round.Size() * 0.5f, negRadius * 2f / round.Width, SpriteEffects.None, 0f);

                spriteBatch.End();
                open = false;

                if (negWash != Color.White)
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, Washing, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                    open = true;

                    if (round == null)
                        spriteBatch.Draw(pixel, screen, negWash);
                    else
                        spriteBatch.Draw(round, negCentre, null, negWash, 0f, round.Size() * 0.5f, negRadius * 2f / round.Width, SpriteEffects.None, 0f);

                    spriteBatch.End();
                    open = false;
                }
            }
            finally
            {
                // Should a draw have thrown with a batch of ours still open, it is closed first, so the batch given back
                // begins cleanly (and the first fault is the one seen).
                if (open)
                    spriteBatch.End();

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
            }
        }

        /// <summary>
        /// A disc of white, its edge a little soft: wherever it is drawn the world is turned in full, but for a rim a few
        /// pixels wide (a turn half made is grey, so the rim is kept thin). Made once, the first time it is drawn.
        /// </summary>
        private static Texture2D Disc()
        {
            if (disc != null && !disc.IsDisposed)
                return disc;

            const int Size = 512;
            Color[] data = new Color[Size * Size];
            float half = Size * 0.5f;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half)) / half;
                    float a = MathHelper.Clamp((1f - r) / 0.02f, 0f, 1f);
                    byte v = (byte)(a * 255f);
                    data[y * Size + x] = new Color(v, v, v, v);
                }
            }

            disc = new Texture2D(Main.graphics.GraphicsDevice, Size, Size);
            disc.SetData(data);
            return disc;
        }

        /// <summary>Let go of with the mod (EmpressOfLightSystem.Unload).</summary>
        internal static void UnloadNegative()
        {
            Texture2D old = disc;
            disc = null;

            if (old != null)
                Main.QueueMainThreadAction(old.Dispose);
        }

        // ---- drawn over the players ---------------------------------------------------------------------------

        /// <summary>The turn: her colours at the edge of the circle as it swells.</summary>
        private void DrawNegativeOver(NPC npc, SpriteBatch spriteBatch)
        {
            float grown = Smoother(timer / (float)NegTurnTicks);
            DrawNegativeRing(npc, spriteBatch, grown, 1f - grown * 0.6f);

            // Her raised bow's light.
            float time = (float)Main.timeForVisualEffects;
            BowNow(npc, npc.Center, out Vector2 grip, out _, out _, out _);
            EmpressArt.DrawBloom(spriteBatch, grip, 200f + 120f * (float)Math.Sin(time * 0.1f), EmpressArt.Additive(Color.White, 0.35f));
        }

        /// <summary>Coming back into her: the ring closing.</summary>
        private void DrawNegativeEdge(NPC npc, SpriteBatch spriteBatch)
        {
            float grown = Smoother(negativeFade / (float)NegativeFadeTicks);
            DrawNegativeRing(npc, spriteBatch, grown, 1f - grown * 0.5f);
        }

        /// <summary>The ring at the circle's edge, in world pixels: her colours going round it, white at its middle.</summary>
        private static void DrawNegativeRing(NPC npc, SpriteBatch spriteBatch, float grown, float strength)
        {
            if (grown <= 0.003f || grown >= 0.999f || strength <= 0.003f)
                return;

            float zoom = Math.Max(0.25f, Main.GameViewMatrix.Zoom.X);
            Vector2 centre = npc.Center;
            float corners = 0f;
            Vector2 onScreen = Vector2.Transform(centre - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

            corners = FarthestCorner(onScreen);

            float radius = grown * (corners + 60f) / zoom;
            float time = (float)Main.timeForVisualEffects;

            DrawRing(spriteBatch, centre, radius, 30f, EmpressArt.Additive(EmpressArt.Prism(time / 120f), 0.35f * strength));
            DrawRing(spriteBatch, centre, radius, 4f, EmpressArt.Additive(Color.White, 0.9f * strength));
        }
    }
}
