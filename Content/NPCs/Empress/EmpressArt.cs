using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// What the Empress is drawn with: her colours, the mod's light textures, what lies outside her
    /// arena (EmpressVoid, EmpressVoidStars, made by formula beside this file), and her own pieces,
    /// the game's, read at run time and never copied: her body (NPC 636), her wings and their glow
    /// (Extra 159, 157), her arms (158, 160), and in her second phase her streamers and skirt (187,
    /// 188). From those, made once on the main thread the first time they are wanted: each piece as
    /// white (the light she is drawn into), and the cracks that run through her when she dies.
    ///
    /// <see cref="DrawHer"/> draws her as the game does (Main.DrawNPCDirect_HallowBoss, 1.4.4), less
    /// the images of her that trail her moves, and with what her scenes need: a size, a white, and her
    /// arms where the scene puts them.
    /// </summary>
    public static class EmpressArt
    {
        // ---- her colours ---------------------------------------------------------------------------------

        /// <summary>Her name, by request (#ff87dd), and her title (#badfff).</summary>
        public static readonly Color Pink = new Color(255, 135, 221);
        public static readonly Color Sky = new Color(186, 223, 255);

        public static readonly Color Gold = new Color(255, 226, 140);
        public static readonly Color Lilac = new Color(206, 168, 255);
        public static readonly Color Mint = new Color(150, 250, 236);

        /// <summary>Round her colours in this order.</summary>
        private static readonly Color[] Wheel = { Pink, Gold, Mint, Sky, Lilac };

        /// <summary>What flies off her light: white, and her colours.</summary>
        public static readonly Color[] Sparks = { Color.White, Pink, Sky, Gold, Lilac, Mint };

        /// <summary>One of her colours, going round: a whole turn of them every 1 of <paramref name="t"/>.</summary>
        public static Color Prism(float t)
        {
            float u = (t % 1f + 1f) % 1f * Wheel.Length;
            int i = (int)u;

            return Color.Lerp(Wheel[i % Wheel.Length], Wheel[(i + 1) % Wheel.Length], u - i);
        }

        /// <summary>A light to add: the colour with no alpha, which the game's alpha blend adds.</summary>
        public static Color Additive(Color colour, float strength)
        {
            Color c = colour * strength;
            c.A = 0;
            return c;
        }

        // ---- the mod's textures ------------------------------------------------------------------------------

        public static Asset<Texture2D> Star;
        public static Asset<Texture2D> Soft;
        public static Asset<Texture2D> Hot;
        public static Asset<Texture2D> Line;
        public static Asset<Texture2D> Beam;
        public static Asset<Texture2D> Streak;
        public static Asset<Texture2D> Void;
        public static Asset<Texture2D> VoidStars;

        internal static void Load()
        {
            if (Main.dedServ)
                return;

            Star = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowStar");
            Soft = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowSoft");
            Hot = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/GlowHot");
            Line = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftLine");
            Beam = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/SoftBeam");
            Streak = ModContent.Request<Texture2D>("PisAndDag/Content/Graphics/Streak");
            Void = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Empress/EmpressVoid");
            VoidStars = ModContent.Request<Texture2D>("PisAndDag/Content/NPCs/Empress/EmpressVoidStars");
        }

        internal static void Unload()
        {
            Star = Soft = Hot = Line = Beam = Streak = Void = VoidStars = null;

            EmpressVeil.Unload();

            List<Texture2D> made = new List<Texture2D>(whites.Values);
            whites.Clear();
            refused.Clear();
            cracks = null;

            foreach (Texture2D texture in made)
            {
                if (texture != null)
                    Main.QueueMainThreadAction(texture.Dispose);
            }
        }

        public static bool Ready(Asset<Texture2D> asset) => asset != null && asset.IsLoaded;

        // ---- her pieces ----------------------------------------------------------------------------------------

        /// <summary>Her body's frame, and the middle of it, where the game puts her middle.</summary>
        public const int BodyWidth = 166;
        public const int BodyHeight = 176;
        public static readonly Vector2 BodyOrigin = new Vector2(83f, 88f);

        /// <summary>An arm's frame (seven of them down the sheet), and its middle, which is hers.</summary>
        public const int ArmFrames = 7;
        public static readonly Vector2 ArmOrigin = new Vector2(87f, 86f);

        /// <summary>
        /// Her eyes, from her middle, at her size: read off her body (NPC 636, the two dark arcs of her
        /// shut eyes at 76-79 and 86-89 across, 38-41 down, in a frame whose middle is at 83, 88).
        /// </summary>
        public static readonly Vector2 LeftEye = new Vector2(-5f, -48f);
        public static readonly Vector2 RightEye = new Vector2(5f, -48f);

        /// <summary>Her pieces, the game's own, loaded for drawing.</summary>
        public static Texture2D Body => TextureAssets.Npc[NPCID.HallowBoss].Value;
        public static Texture2D WingsBack => TextureAssets.Extra[ExtrasID.HallowBossWingsBack].Value;
        public static Texture2D WingsGlow => TextureAssets.Extra[ExtrasID.HallowBossWings].Value;
        public static Texture2D ArmLeft => TextureAssets.Extra[ExtrasID.HallowBossArmsLeft].Value;
        public static Texture2D ArmRight => TextureAssets.Extra[ExtrasID.HallowBossArmsRight].Value;
        public static Texture2D Tentacles => TextureAssets.Extra[ExtrasID.HallowBossTentacles].Value;
        public static Texture2D Skirt => TextureAssets.Extra[ExtrasID.HallowBossSkirt].Value;

        // ---- each piece as white ------------------------------------------------------------------------------

        private static readonly Dictionary<Texture2D, Texture2D> whites = new();
        private static readonly HashSet<Texture2D> refused = new();

        /// <summary>
        /// This piece of her as white, as bright as it is opaque (premultiplied: a colour with no alpha
        /// adds it as light, a colour with alpha lays it over her). Made from the piece the first time it
        /// is asked for; null if the graphics device will not read the piece back, and she is then
        /// drawn without it.
        /// </summary>
        public static Texture2D WhiteOf(Texture2D piece)
        {
            if (piece == null || refused.Contains(piece))
                return null;

            if (whites.TryGetValue(piece, out Texture2D white) && white != null && !white.IsDisposed)
                return white;

            try
            {
                Color[] data = new Color[piece.Width * piece.Height];
                piece.GetData(data);

                for (int i = 0; i < data.Length; i++)
                {
                    int a = data[i].A;
                    data[i] = new Color(a, a, a, a);
                }

                white = new Texture2D(Main.graphics.GraphicsDevice, piece.Width, piece.Height);
                white.SetData(data);
                whites[piece] = white;
                return white;
            }
            catch
            {
                refused.Add(piece);
                return null;
            }
        }

        // ---- drawing her --------------------------------------------------------------------------------------

        /// <summary>How a scene has her drawn.</summary>
        public struct Look
        {
            /// <summary>Where her middle is, in the world.</summary>
            public Vector2 At;

            /// <summary>Her size, times the game's (npc.scale).</summary>
            public float Size;

            /// <summary>How much of her is there, 0 to 1.</summary>
            public float Opacity;

            /// <summary>Light added over her, 0 to 1, as white; and white laid over her, 0 to 1, she gone to light.</summary>
            public float Bright;
            public float White;

            /// <summary>Her arms' frames (0 at rest; 1 as she comes; 2 and 3 raised to cast; 5 and 6 the dash).</summary>
            public int LeftArm;
            public int RightArm;

            /// <summary>Her wings' glow, drawn through the game's own shader for it.</summary>
            public bool Glow;

            /// <summary>Her arms left off, for an act that draws them held its own way after (DrawArmAt).</summary>
            public bool NoArms;

            /// <summary>
            /// In her second half in Legends: how far her veil has come (0 none, 1 whole: EmpressVeil), and its torn edges'
            /// light thrown up (0 to 1, as it is made).
            /// </summary>
            public float Veil;
            public float VeilFlare;

            public static Look Of(NPC npc) => new Look
            {
                At = npc.Center,
                Size = 1f,
                Opacity = npc.Opacity,
                LeftArm = 0,
                RightArm = 0,
                Glow = true,
            };
        }

        public static SpriteEffects Effects(NPC npc) => npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        /// <summary>
        /// Draws her as the game does, as the scene says. Expects the game's batch open (the NPCs'), and
        /// leaves it open the same way: her wings' glow is drawn through the game's own shader for it,
        /// in a batch of its own, as the game draws it.
        /// </summary>
        public static void DrawHer(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Look look)
        {
            if (look.Opacity <= 0.003f)
                return;

            Main.instance.LoadNPC(npc.type);

            Texture2D body = Body;
            Texture2D wings = WingsBack;
            Texture2D glow = WingsGlow;
            Texture2D left = ArmLeft;
            Texture2D right = ArmRight;

            Vector2 at = look.At - screenPos;
            float size = npc.scale * look.Size;
            float turn = npc.rotation;
            SpriteEffects effects = Effects(npc);
            bool second = npc.AI_120_HallowBoss_IsInPhase2();

            Rectangle wingFrame = wings.Frame(1, 11, 0, (int)(npc.localAI[0] / 4f) % 11);
            Rectangle leftFrame = left.Frame(1, ArmFrames, 0, Math.Clamp(look.LeftArm, 0, ArmFrames - 1));
            Rectangle rightFrame = right.Frame(1, ArmFrames, 0, Math.Clamp(look.RightArm, 0, ArmFrames - 1));
            Rectangle bodyFrame = body.Frame(1, 2, 0, second ? 1 : 0);
            Vector2 half = BodyOrigin;

            // Her colour: the game takes hers a quarter of the way from the light where she is to white
            // (NPC.GetAlpha); here she is at full light, so it is white, and no darkness of the sky or the
            // world reaches her, by request.
            Color colour = Color.White * look.Opacity;

            spriteBatch.Draw(wings, at, wingFrame, colour, turn, wingFrame.Size() / 2f, size * 2f, effects, 0f);

            if (look.Glow && !npc.IsABestiaryIconDummy)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.Transform);

                DrawData data = new DrawData(glow, at, wingFrame, colour, turn, wingFrame.Size() / 2f, size * 2f, effects);
                GameShaders.Misc["HallowBoss"].Apply(data);
                data.Draw(spriteBatch);
                Main.pixelShader.CurrentTechnique.Passes[0].Apply();

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            }
            else
            {
                spriteBatch.Draw(glow, at, wingFrame, colour, turn, wingFrame.Size() / 2f, size * 2f, effects, 0f);
            }

            // The pulse of light about her in her second half, as the game's.
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * MathHelper.TwoPi * 0.5f) * 0.5f + 0.5f;
            Color aura = Main.hslToRgb((pulse * 0.08f + 0.6f) % 1f, 1f, 0.5f);
            aura.A = 0;
            aura *= 0.6f;

            if (NPC.ShouldEmpressBeEnraged())
            {
                aura = Main.OurFavoriteColor;
                aura.A = 0;
                aura *= 0.3f;
            }

            aura *= look.Opacity;

            if (second)
            {
                Texture2D tentacles = Tentacles;
                Rectangle tentacleFrame = tentacles.Frame(1, 8, 0, (int)(npc.localAI[0] / 4f) % 8);

                spriteBatch.Draw(tentacles, at, tentacleFrame, colour, turn, half, size, effects, 0f);

                for (float k = 0f; k < 1f; k += 0.25f)
                {
                    Vector2 off = turn.ToRotationVector2().RotatedBy(k * MathHelper.TwoPi + MathHelper.PiOver4) * MathHelper.Lerp(2f, 8f, pulse);
                    spriteBatch.Draw(tentacles, at + off, tentacleFrame, aura, turn, half, size, effects, 0f);
                }
            }

            // Her body; in her second half in Legends, with her veil's streaks taken out of it (EmpressVeil), and their
            // torn edges lit.
            Texture2D veiled = null;
            Texture2D rim = null;
            Texture2D veiledWhite = null;
            Texture2D veiledSkirt = null;

            if (second && look.Veil > 0.003f && !npc.IsABestiaryIconDummy)
                veiled = EmpressVeil.BodyNow(npc, 1, look.Veil, out rim, out veiledWhite, out veiledSkirt);

            if (veiled != null)
            {
                spriteBatch.Draw(veiled, at, null, colour, turn, half, size, effects, 0f);
                EmpressVeil.DrawRim(spriteBatch, rim, at, turn, half, size, effects, look.Opacity, look.VeilFlare);
            }
            else
            {
                spriteBatch.Draw(body, at, bodyFrame, colour, turn, half, size, effects, 0f);
            }

            // The glow over her dress, in her pulsing light; with her veil, cut where its streaks are.
            if (second)
            {
                Texture2D skirt = veiled != null && veiledSkirt != null ? veiledSkirt : Skirt;

                for (float k = 0f; k < 1f; k += 0.25f)
                {
                    Vector2 off = turn.ToRotationVector2().RotatedBy(k * MathHelper.TwoPi + MathHelper.PiOver4) * MathHelper.Lerp(2f, 8f, pulse);
                    spriteBatch.Draw(skirt, at + off, null, aura, turn, half, size, effects, 0f);
                }
            }

            // Her arms: one held back behind the other in the dash, as the game draws them.
            int leftBehind = look.LeftArm == 5 ? 1 : 0;
            int rightBehind = look.RightArm == 5 ? 1 : 0;

            for (int k = 0; k < 2 && !look.NoArms; k++)
            {
                if (k == leftBehind)
                    spriteBatch.Draw(left, at, leftFrame, colour, turn, leftFrame.Size() / 2f, size, effects, 0f);

                if (k == rightBehind)
                    spriteBatch.Draw(right, at, rightFrame, colour, turn, rightFrame.Size() / 2f, size, effects, 0f);
            }

            // Light over her, and white laid over her.
            if (look.Bright > 0.003f || look.White > 0.003f)
            {
                DrawWhite(spriteBatch, wings, at, wingFrame, turn, wingFrame.Size() / 2f, size * 2f, effects, look);

                if (veiled != null)
                    DrawMadeWhite(spriteBatch, veiledWhite, at, turn, half, size, effects, look);
                else
                    DrawWhite(spriteBatch, body, at, bodyFrame, turn, half, size, effects, look);

                if (!look.NoArms)
                {
                    DrawWhite(spriteBatch, left, at, leftFrame, turn, leftFrame.Size() / 2f, size, effects, look);
                    DrawWhite(spriteBatch, right, at, rightFrame, turn, rightFrame.Size() / 2f, size, effects, look);
                }
            }
        }

        // ---- an arm held where an act wants it ----------------------------------------------------------------
        //
        // By leave ("You can cut up the arm textures and rotate them for better character animation"): each arm
        // as the game draws it held straight out (its seventh frame, the dash's reach), cut from its sheet and
        // turned about its shoulder to any angle. The screen's left arm is the left sheet's, the right the right
        // sheet's, whichever way she faces (the two sheets are each other's mirror), so no arm is drawn flipped.

        /// <summary>The arm held straight out: where on each sheet it is cut from (frame 6), and its shoulder in that cut.</summary>
        private static readonly Rectangle LeftCut = new Rectangle(0, ArmFrameHeight * 6 + 44, 82, 42);
        private static readonly Rectangle RightCut = new Rectangle(92, ArmFrameHeight * 6 + 44, 82, 42);
        private static readonly Vector2 LeftShoulderInCut = new Vector2(72f, 18f);
        private static readonly Vector2 RightShoulderInCut = new Vector2(10f, 18f);

        /// <summary>An arm frame's height on its sheet.</summary>
        private const int ArmFrameHeight = 172;

        /// <summary>The shoulders, from her middle (the cut's shoulder on the sheet, less the sheet frame's middle).</summary>
        public static readonly Vector2 LeftShoulder = new Vector2(72f - 87f, 62f - 86f);
        public static readonly Vector2 RightShoulder = new Vector2(102f - 87f, 62f - 86f);

        /// <summary>The way each held-out arm runs from its shoulder on the sheet: the left out left and a little down, the right its mirror.</summary>
        private static readonly float LeftRuns = (float)Math.Atan2(10.0, -64.0);
        private static readonly float RightRuns = (float)Math.Atan2(10.0, 64.0);

        /// <summary>From a shoulder to the palm of the hand, along the arm.</summary>
        public const float ArmReach = 58f;

        /// <summary>A palm, in the world, of an arm held at this angle (radians, 0 out to the right, down positive).</summary>
        public static Vector2 PalmAt(Vector2 middle, bool leftArm, float angle, float size)
        {
            Vector2 shoulder = middle + (leftArm ? LeftShoulder : RightShoulder) * size;
            return shoulder + angle.ToRotationVector2() * ArmReach * size;
        }

        /// <summary>
        /// One arm, held at this angle about its shoulder, at her middle <paramref name="middle"/> (on the
        /// screen). In white (as bright as it is opaque) when asked, for light laid over it.
        /// </summary>
        public static void DrawArmAt(SpriteBatch spriteBatch, Vector2 middle, bool leftArm, float angle, Color colour, float size, bool white = false)
        {
            Texture2D sheet = leftArm ? ArmLeft : ArmRight;

            if (white)
                sheet = WhiteOf(sheet);

            if (sheet == null)
                return;

            Rectangle cut = leftArm ? LeftCut : RightCut;
            Vector2 origin = leftArm ? LeftShoulderInCut : RightShoulderInCut;
            Vector2 shoulder = middle + (leftArm ? LeftShoulder : RightShoulder) * size;
            float turn = angle - (leftArm ? LeftRuns : RightRuns);

            spriteBatch.Draw(sheet, shoulder, cut, colour, turn, origin, size, SpriteEffects.None, 0f);
        }

        /// <summary>As DrawWhite, from a picture already made white (one frame, no source): the veiled body's.</summary>
        private static void DrawMadeWhite(SpriteBatch spriteBatch, Texture2D white, Vector2 at, float turn, Vector2 origin, float size, SpriteEffects effects, Look look)
        {
            if (white == null)
                return;

            if (look.Bright > 0.003f)
                spriteBatch.Draw(white, at, null, Additive(Color.White, look.Bright * look.Opacity), turn, origin, size, effects, 0f);

            if (look.White > 0.003f)
                spriteBatch.Draw(white, at, null, Color.White * (look.White * look.Opacity), turn, origin, size, effects, 0f);
        }

        /// <summary>
        /// One arm as the game draws it at one of its frames, the screen's left arm from the left sheet and the right from
        /// the right, never flipped (the sheets are each other's mirror), about her middle <paramref name="middle"/> (on
        /// the screen). For the arm her bow leaves free.
        /// </summary>
        public static void DrawArmFrame(SpriteBatch spriteBatch, Vector2 middle, bool leftArm, int frame, Color colour, float size, bool white = false)
        {
            Texture2D sheet = leftArm ? ArmLeft : ArmRight;

            if (white)
                sheet = WhiteOf(sheet);

            if (sheet == null)
                return;

            Rectangle cut = sheet.Frame(1, ArmFrames, 0, Math.Clamp(frame, 0, ArmFrames - 1));
            spriteBatch.Draw(sheet, middle, cut, colour, 0f, cut.Size() / 2f, size, SpriteEffects.None, 0f);
        }

        /// <summary>Where the hand is on each arm's first frame (bent up to her chest): from her middle, the screen's left arm's, at size 1.</summary>
        public static readonly Vector2 ChestHand = new Vector2(-21f, -22f);

        /// <summary>
        /// The game's own frames for her arms this tick (Main.DrawNPCDirect_GetHallowBossArmFrame, 1.4.4, ported): the left
        /// sheet's and the right sheet's, as the game draws them (flipped with her).
        /// </summary>
        public static void VanillaArms(NPC npc, out int left, out int right)
        {
            const int Rest = 0;
            const int Coming = 1;
            const int CastLow = 2;
            const int CastHigh = 3;
            const int Above = 4;
            const int Back = 5;
            const int Out = 6;

            left = Rest;
            right = Rest;
            float t = npc.ai[1];

            switch ((int)npc.ai[0])
            {
                case 6:
                    left = right = t < 6f ? CastLow : t < 174f ? CastHigh : t < 180f ? CastLow : Rest;
                    break;

                case 0:
                    left = right = t < 106f ? Coming : t < 110f ? CastLow : Rest;
                    break;

                case 2:
                case 11:
                    left = t < 5f ? CastLow : t < 65f ? CastHigh : CastLow;
                    break;

                case 5:
                    right = t < 6f ? CastLow : t < 54f ? CastHigh : CastLow;
                    break;

                case 4:
                case 10:
                    left = right = t < 6f ? CastLow : t < 54f ? CastHigh : CastLow;
                    break;

                case 8:
                case 9:
                    {
                        int dash = t < 10f ? CastLow : t < 20f ? CastHigh : t < 30f ? CastLow : Back;
                        int a = dash;
                        int b = dash;
                        int phase = (int)npc.ai[3];

                        if (t < 30f)
                        {
                            if (phase == 1)
                                b = Coming;

                            if (phase == -1)
                                a = Coming;
                        }

                        if (phase == -1 && b == Back)
                            b = Out;

                        if (phase == 1 && a == Back)
                            a = Out;

                        left = b;
                        right = a;
                        break;
                    }

                case 7:
                    {
                        int each = Main.expertMode ? 40 : 60;
                        const int Lift = 5;
                        int at = 0;

                        if (t < at + Lift) { left = CastLow; break; }
                        at += Lift;
                        if (t < at + each - Lift) { left = CastHigh; break; }
                        at += each - Lift;
                        if (t < at + Lift) { left = CastHigh; right = CastLow; break; }
                        at += Lift;
                        if (t < at + each - Lift) { left = CastHigh; right = CastHigh; break; }
                        at += each - Lift;
                        if (t < at + each) { left = Above; right = CastHigh; break; }
                        at += each;
                        if (t < at + each) { left = Above; right = Above; break; }
                        at += each;

                        if (Main.expertMode)
                        {
                            if (t < at + Lift) { left = CastHigh; right = Above; break; }
                            at += Lift;
                            if (t < at + each - Lift) { left = Coming; right = Above; break; }
                            at += each - Lift;
                            if (t < at + Lift) { left = Coming; right = CastHigh; break; }
                            at += Lift;
                            if (t < at + each - Lift) { left = Coming; right = Coming; break; }
                            at += each - Lift;
                        }

                        if (t >= at)
                            left = right = CastLow;

                        break;
                    }
            }
        }

        private static void DrawWhite(SpriteBatch spriteBatch, Texture2D piece, Vector2 at, Rectangle frame, float turn, Vector2 origin, float size, SpriteEffects effects, Look look)
        {
            Texture2D white = WhiteOf(piece);

            if (white == null)
                return;

            float opacity = look.Opacity;

            if (look.Bright > 0.003f)
                spriteBatch.Draw(white, at, frame, Additive(Color.White, look.Bright * opacity), turn, origin, size, effects, 0f);

            if (look.White > 0.003f)
                spriteBatch.Draw(white, at, frame, Color.White * (look.White * opacity), turn, origin, size, effects, 0f);
        }

        /// <summary>
        /// Her shape, all of it, in one colour: for the images of her left behind where she was, and
        /// for the light she leaves when she goes. Premultiplied, so a colour with no alpha adds it.
        /// No glow, no shader: an image, not her.
        /// </summary>
        public static void DrawShape(SpriteBatch spriteBatch, NPC npc, Vector2 world, Vector2 screenPos, float size, Color colour, int leftArm = 0, int rightArm = 0, bool withArms = true)
        {
            Main.instance.LoadNPC(npc.type);

            Texture2D body = WhiteOf(Body);
            Texture2D wings = WhiteOf(WingsBack);
            Texture2D left = WhiteOf(ArmLeft);
            Texture2D right = WhiteOf(ArmRight);

            if (body == null || wings == null || left == null || right == null)
                return;

            Vector2 at = world - screenPos;
            float scale = npc.scale * size;
            SpriteEffects effects = Effects(npc);
            bool second = npc.AI_120_HallowBoss_IsInPhase2();

            Rectangle wingFrame = wings.Frame(1, 11, 0, (int)(npc.localAI[0] / 4f) % 11);
            Rectangle leftFrame = left.Frame(1, ArmFrames, 0, Math.Clamp(leftArm, 0, ArmFrames - 1));
            Rectangle rightFrame = right.Frame(1, ArmFrames, 0, Math.Clamp(rightArm, 0, ArmFrames - 1));
            Rectangle bodyFrame = body.Frame(1, 2, 0, second ? 1 : 0);

            spriteBatch.Draw(wings, at, wingFrame, colour, npc.rotation, wingFrame.Size() / 2f, scale * 2f, effects, 0f);
            spriteBatch.Draw(body, at, bodyFrame, colour, npc.rotation, BodyOrigin, scale, effects, 0f);

            if (!withArms)
                return;

            spriteBatch.Draw(left, at, leftFrame, colour, npc.rotation, leftFrame.Size() / 2f, scale, effects, 0f);
            spriteBatch.Draw(right, at, rightFrame, colour, npc.rotation, rightFrame.Size() / 2f, scale, effects, 0f);
        }

        // ---- a line of light ----------------------------------------------------------------------------------

        /// <summary>A line of light from one point to another, this wide, in world pixels; soft across, even along.</summary>
        public static void DrawLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float width, Color colour)
        {
            if (!Ready(Line))
                return;

            Texture2D line = Line.Value;
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f)
                return;

            spriteBatch.Draw(line, from - Main.screenPosition, null, colour, way.ToRotation(), new Vector2(0f, line.Height * 0.5f),
                new Vector2(length / line.Width, width / line.Height), SpriteEffects.None, 0f);
        }

        /// <summary>SoftBeam's three parts: its rounded start, its body (the same all along), its rounded end.</summary>
        private static readonly Rectangle BeamStart = new Rectangle(0, 0, 16, 32);
        private static readonly Rectangle BeamBody = new Rectangle(16, 0, 64, 32);
        private static readonly Rectangle BeamEnd = new Rectangle(80, 0, 16, 32);

        /// <summary>
        /// A beam of light from one point to another, this wide, in world pixels: soft across as DrawLine's, but its two
        /// ends rounded off soft, as a round brush ends a stroke, inside the two points (never cut square). For a line
        /// standing alone; a run of lines joined end to end (a crack, a trail, an outline) keeps DrawLine, which does not
        /// fade where the lines meet.
        /// </summary>
        public static void DrawBeam(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float width, Color colour)
        {
            if (!Ready(Beam))
            {
                DrawLine(spriteBatch, from, to, width, colour);
                return;
            }

            Texture2D beam = Beam.Value;
            Vector2 way = to - from;
            float length = way.Length();

            if (length < 0.5f || width <= 0f)
                return;

            Vector2 u = way / length;
            float turn = (float)Math.Atan2(u.Y, u.X);
            float cap = Math.Min(width * 0.5f, length * 0.5f);
            float body = length - cap * 2f;
            float across = width / beam.Height;
            Vector2 origin = new Vector2(0f, beam.Height * 0.5f);
            Vector2 start = from - Main.screenPosition;

            spriteBatch.Draw(beam, start, BeamStart, colour, turn, origin, new Vector2(cap / BeamStart.Width, across), SpriteEffects.None, 0f);

            if (body > 0.01f)
                spriteBatch.Draw(beam, start + u * cap, BeamBody, colour, turn, origin, new Vector2(body / BeamBody.Width, across), SpriteEffects.None, 0f);

            spriteBatch.Draw(beam, start + u * (cap + body), BeamEnd, colour, turn, origin, new Vector2(cap / BeamEnd.Width, across), SpriteEffects.None, 0f);
        }

        /// <summary>A ray of light out of a point, this long and wide, brightest where it leaves the point and gone at its end.</summary>
        public static void DrawRay(SpriteBatch spriteBatch, Vector2 from, float angle, float length, float width, Color colour)
        {
            if (!Ready(Streak) || length < 1f)
                return;

            Texture2D streak = Streak.Value;

            // The streak is brightest at its right end: that end is put on the point, and turned to face back,
            // so the rest of it runs out the way the ray goes.
            spriteBatch.Draw(streak, from - Main.screenPosition, null, colour, angle + MathHelper.Pi, new Vector2(streak.Width - 4f, streak.Height * 0.5f),
                new Vector2(length / (streak.Width - 4f), width / streak.Height), SpriteEffects.None, 0f);
        }

        /// <summary>A four-pointed star of light, this many world pixels from point to point.</summary>
        public static void DrawStar(SpriteBatch spriteBatch, Vector2 at, float across, float turn, Color colour)
        {
            if (!Ready(Star))
                return;

            Texture2D star = Star.Value;

            // The star in GlowStar spans about half of its canvas.
            spriteBatch.Draw(star, at - Main.screenPosition, null, colour, turn, star.Size() * 0.5f, across / (star.Width * 0.5f), SpriteEffects.None, 0f);
        }

        /// <summary>A soft ball of light, this many world pixels across its bright middle.</summary>
        public static void DrawBloom(SpriteBatch spriteBatch, Vector2 at, float across, Color colour)
        {
            if (!Ready(Soft))
                return;

            Texture2D soft = Soft.Value;

            spriteBatch.Draw(soft, at - Main.screenPosition, null, colour, 0f, soft.Size() * 0.5f, across / (soft.Width * 0.5f), SpriteEffects.None, 0f);
        }

        // ---- the cracks ----------------------------------------------------------------------------------------

        /// <summary>A crack through one piece of her.</summary>
        public sealed class Crack
        {
            /// <summary>0 her body, 1 her left arm, 2 her right.</summary>
            public int Piece;

            /// <summary>Its line, in the piece's frame, from the piece's middle, at size 1; and how far along it each point is.</summary>
            public Vector2[] Points;
            public float[] Along;
            public float Length;

            /// <summary>When it opens, as a share of the cracking; and the share it takes to run its length.</summary>
            public float At;
            public float Run;

            /// <summary>Which way its light goes: out from her middle.</summary>
            public float Out;

            /// <summary>Its own colour of hers.</summary>
            public float Hue;
        }

        private static List<Crack>[] cracks;

        /// <summary>
        /// The cracks that run through her as she dies, her first half's and her second's: made once,
        /// the same every time (one seed), out of the shape of her own pieces. Null if her pieces cannot
        /// be read back, and she is then drawn without them.
        /// </summary>
        public static List<Crack> Cracks(bool second)
        {
            int half = second ? 1 : 0;
            cracks ??= new List<Crack>[2];

            if (cracks[half] != null)
                return cracks[half].Count > 0 ? cracks[half] : null;

            cracks[half] = new List<Crack>();

            try
            {
                Main.instance.LoadNPC(NPCID.HallowBoss);

                Mask body = Mask.Of(Body, body => body.Frame(1, 2, 0, half));
                Mask left = Mask.Of(ArmLeft, arm => arm.Frame(1, ArmFrames, 0, 0));
                Mask right = Mask.Of(ArmRight, arm => arm.Frame(1, ArmFrames, 0, 0));

                Random random = new Random(636 + half);
                List<Crack> made = cracks[half];

                // Her body: her crown and head (none across her face), her chest, the skirt either side,
                // and the crystal of her legs under it; then her arms. By request: "cracks randomly on
                // her body, arms and legs".
                Seed(made, body, 0, random, 2, (x, y) => y < 30 && Math.Abs(x - 83) > 6, 8, 16);
                Seed(made, body, 0, random, 4, (x, y) => y >= 54 && y < 92, 10, 22);
                Seed(made, body, 0, random, 8, (x, y) => y >= 92 && y < 150 && Math.Abs(x - 83) > 4, 12, 28);
                Seed(made, body, 0, random, 3, (x, y) => y >= 140 && Math.Abs(x - 83) <= 14, 8, 18);
                Seed(made, left, 1, random, 4, (x, y) => true, 8, 18);
                Seed(made, right, 2, random, 4, (x, y) => true, 8, 18);

                // Opened in no order of piece, one after another across the whole of the cracking.
                for (int i = made.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (made[i], made[j]) = (made[j], made[i]);
                }

                for (int i = 0; i < made.Count; i++)
                {
                    made[i].At = MathHelper.Clamp(i / (float)made.Count * 0.82f + (float)random.NextDouble() * 0.04f, 0f, 0.9f);
                    made[i].Run = 0.05f + 0.07f * (float)random.NextDouble();
                }
            }
            catch
            {
                cracks[half].Clear();
            }

            return cracks[half].Count > 0 ? cracks[half] : null;
        }

        /// <summary>Which pixels of a piece's frame are her, and where its middle is.</summary>
        private sealed class Mask
        {
            public bool[] Solid;
            public int Width;
            public int Height;
            public Vector2 Middle;

            public bool At(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && Solid[y * Width + x];

            public static Mask Of(Texture2D sheet, Func<Texture2D, Rectangle> frameOf)
            {
                Rectangle frame = frameOf(sheet);
                Color[] data = new Color[frame.Width * frame.Height];
                sheet.GetData(0, frame, data, 0, data.Length);

                Mask mask = new Mask
                {
                    Width = frame.Width,
                    Height = frame.Height,
                    Solid = new bool[data.Length],
                    Middle = frame.Size() / 2f,
                };

                for (int i = 0; i < data.Length; i++)
                    mask.Solid[i] = data[i].A > 110;

                // A crack runs in from her edge, not along it: the outermost pixel all round is not hers.
                bool[] inner = new bool[data.Length];

                for (int y = 0; y < mask.Height; y++)
                {
                    for (int x = 0; x < mask.Width; x++)
                    {
                        inner[y * mask.Width + x] = mask.At(x, y) && mask.At(x - 1, y) && mask.At(x + 1, y) && mask.At(x, y - 1) && mask.At(x, y + 1);
                    }
                }

                mask.Solid = inner;
                return mask;
            }
        }

        /// <summary>Lays <paramref name="count"/> cracks through a piece, from points of it the test lets them start at.</summary>
        private static void Seed(List<Crack> into, Mask mask, int piece, Random random, int count, Func<int, int, bool> where, int shortest, int longest)
        {
            int made = 0;

            for (int tries = 0; tries < count * 60 && made < count; tries++)
            {
                int x = random.Next(mask.Width);
                int y = random.Next(mask.Height);

                // Not across her face: her eyes are where the light comes out of her first.
                if (piece == 0 && Math.Abs(x - 83) <= 11 && y >= 29 && y <= 52)
                    continue;

                if (!mask.At(x, y) || !where(x, y))
                    continue;

                Crack crack = Walk(mask, piece, random, new Vector2(x + 0.5f, y + 0.5f), (float)(random.NextDouble() * MathHelper.TwoPi), random.Next(shortest, longest + 1));

                if (crack == null)
                    continue;

                into.Add(crack);
                made++;

                // Now and then it splits.
                if (random.NextDouble() < 0.45 && crack.Points.Length > 4)
                {
                    int from = random.Next(crack.Points.Length / 3, crack.Points.Length * 2 / 3);
                    Vector2 a = crack.Points[from] + mask.Middle;
                    Vector2 way = crack.Points[Math.Min(from + 1, crack.Points.Length - 1)] - crack.Points[from];
                    float angle = way.ToRotation() + (random.NextDouble() < 0.5 ? -1f : 1f) * (0.6f + 0.6f * (float)random.NextDouble());

                    Crack branch = Walk(mask, piece, random, a, angle, random.Next(4, Math.Max(6, longest / 2)));

                    if (branch != null)
                        into.Add(branch);
                }
            }
        }

        /// <summary>One crack: a wandering line through the piece from a point, a pixel a step, jagged, until it is as long as it is to be or leaves her.</summary>
        private static Crack Walk(Mask mask, int piece, Random random, Vector2 start, float angle, int steps)
        {
            List<Vector2> points = new List<Vector2> { start };
            Vector2 at = start;
            float heading = angle;

            for (int i = 1; i <= steps; i++)
            {
                heading += ((float)random.NextDouble() - 0.5f) * 0.45f;

                // A sharper turn every few pixels: a crack, not a scratch.
                if (i % 4 == 0)
                    heading += ((float)random.NextDouble() - 0.5f) * 1.3f;

                Vector2 next = at + heading.ToRotationVector2();

                if (!mask.At((int)next.X, (int)next.Y))
                    break;

                at = next;

                if (i % 2 == 0)
                    points.Add(at);
            }

            if (points.Count < 3)
                return null;

            Crack crack = new Crack
            {
                Piece = piece,
                Points = new Vector2[points.Count],
                Along = new float[points.Count],
                Hue = (float)random.NextDouble(),
            };

            for (int i = 0; i < points.Count; i++)
            {
                crack.Points[i] = points[i] - mask.Middle;

                if (i > 0)
                    crack.Length += Vector2.Distance(points[i], points[i - 1]);

                crack.Along[i] = crack.Length;
            }

            // Its light goes out from her middle: from the middle of her body, which an arm's frame shares.
            Vector2 middle = crack.Points[crack.Points.Length / 2];
            crack.Out = middle.LengthSquared() < 1f ? -MathHelper.PiOver2 : middle.ToRotation();

            return crack.Length >= 3f ? crack : null;
        }

        /// <summary>A point of a crack in the world: from her middle, flipped as she is drawn, at her size.</summary>
        public static Vector2 OnHer(Vector2 local, Vector2 middle, float size, SpriteEffects effects)
        {
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
                local.X = -local.X;

            return middle + local * size;
        }
    }
}
