using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- how her scenes look -------------------------------------------------------------------------
    //
    // Vanilla draws her through the fight. Through her scenes she is drawn here (PreDraw false), as the
    // game draws her (EmpressArt.DrawHer) with what the scene adds: the light behind her, the images of
    // her left as she flashes down, the cracks and the light out of them and out of her eyes as she
    // dies, the white she goes to, the size she shrinks to. What goes over the player (her cage, its
    // pieces when it breaks, her ten stars spinning out) is drawn after the players (DrawOverPlayers).
    //
    // The light is drawn sampled smoothly, since it is stretched far past its size; she is drawn as the
    // game draws her, pixel for pixel.

    public partial class EmpressOfLight
    {
        /// <summary>When the cage broke, by the game's clock: its sides fly off from then.</summary>
        private uint breakAt;

        /// <summary>The cage's sides fly for this long.</summary>
        private const int ShardTicks = 34;

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.IsABestiaryIconDummy || scene == Scene.None && !DrawsLegends)
                return true;

            Main.instance.LoadNPC(npc.type);

            try
            {
                if (scene == Scene.Arrival)
                    DrawArrival(npc, spriteBatch, screenPos);
                else if (scene == Scene.Death)
                    DrawDeath(npc, spriteBatch, screenPos);
                else
                    DrawLegends(npc, spriteBatch, screenPos);
            }
            catch (Exception e)
            {
                // A frame of her lost is not worth the game; the batch is put back as it was given. (An
                // exception out of here would have the game take her out of the world.)
                ModContent.GetInstance<EmpressOfLightSystem>()?.Mod.Logger.Warn("Empress draw: " + e.Message);
                Recover(spriteBatch);
            }

            return false;
        }

        /// <summary>The game's batch for the NPCs, begun again whatever state a failed frame left it in.</summary>
        private static void Recover(SpriteBatch spriteBatch)
        {
            try
            {
                spriteBatch.End();
            }
            catch (InvalidOperationException)
            {
                // It was not begun: the frame failed with it put down.
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        /// <summary>The game's batch for the NPCs begun again: sampled smoothly for light, or as the game samples, for her.</summary>
        private static void Restart(SpriteBatch spriteBatch, bool smooth)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, smooth ? SamplerState.LinearClamp : Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }

        private static float Time => (float)Main.timeForVisualEffects / 60f;

        // ---- her coming ------------------------------------------------------------------------------------

        private void DrawArrival(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (beat < ArriveSpawn || npc.Opacity <= 0.003f)
                return;

            int t = timer;
            EmpressArt.Look look = EmpressArt.Look.Of(npc);
            float glow = 0f;

            switch (beat)
            {
                case ArriveSpawn:
                    // As vanilla's arms come in her coming, at the length of this one.
                    look.LeftArm = look.RightArm = t < 70 ? 1 : t < 74 ? 2 : 0;
                    glow = 0.3f * npc.Opacity;
                    break;

                case ArriveFlash:
                    look.LeftArm = look.RightArm = 6;
                    look.Bright = 1f - Ease(t / (float)FlashTicks);
                    glow = 0.8f;
                    break;

                case ArriveCage:
                    look.LeftArm = look.RightArm = t < CageShuts + 10 ? 3 : 1;
                    glow = 0.55f;
                    break;

                case ArriveDrag:
                    look.LeftArm = look.RightArm = 1;
                    glow = 0.5f;
                    break;

                case ArriveStars:
                    look.LeftArm = look.RightArm = t < 30 ? 4 : 3;
                    look.Bright = 0.6f * (1f - Ease(t / 20f));
                    glow = 0.9f - 0.4f * Ease(t / (float)StarsTicks);
                    break;

                case ArriveForm:
                    look.LeftArm = look.RightArm = t < FormTicks / 2 ? 3 : 0;
                    glow = 0.45f;
                    break;

                case ArriveBreak:
                    look.LeftArm = look.RightArm = 3;
                    glow = 0.5f;
                    break;

                case ArriveScream:
                    // Her arms up through the scream, and down to how the fight has them as it ends.
                    look.LeftArm = look.RightArm = t < 60 ? 6 : t < 78 ? 3 : 0;
                    look.Bright = 0.35f * (1f - Ease(t / 40f));
                    glow = MathHelper.Lerp(0.7f, 0.35f, Ease((t - 60) / 30f));
                    break;

                default:
                    glow = 0.3f;
                    break;
            }

            Restart(spriteBatch, true);
            DrawBackGlow(spriteBatch, npc.Center, glow, 1f);

            if (beat == ArriveFlash)
                DrawFlashImages(npc, spriteBatch, screenPos, t);

            Restart(spriteBatch, false);
            EmpressArt.DrawHer(spriteBatch, npc, screenPos, look);
        }

        /// <summary>The light behind her, in her colours: a soft ball of it, white at its heart.</summary>
        private static void DrawBackGlow(SpriteBatch spriteBatch, Vector2 at, float strength, float size)
        {
            if (strength <= 0.003f)
                return;

            float time = Time;

            EmpressArt.DrawBloom(spriteBatch, at, 340f * size, EmpressArt.Additive(EmpressArt.Prism(time * 0.05f), 0.35f * strength));
            EmpressArt.DrawBloom(spriteBatch, at, 200f * size, EmpressArt.Additive(EmpressArt.Pink, 0.35f * strength));
            EmpressArt.DrawBloom(spriteBatch, at, 90f * size, EmpressArt.Additive(Color.White, 0.4f * strength));
        }

        /// <summary>Images of her, in her colours, all the way down from where she was to the player, going as the flash goes.</summary>
        private void DrawFlashImages(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, int t)
        {
            Vector2 from = spawnAt + new Vector2(0f, SpawnDrift);
            float fade = 1f - Ease(t / (float)FlashTicks);
            const int Images = 10;

            for (int k = 0; k < Images; k++)
            {
                float along = k / (float)(Images - 1);
                Vector2 at = Vector2.Lerp(from, cageFrom, along);
                Color colour = EmpressArt.Additive(Main.hslToRgb((along + Time * 0.3f) % 1f, 1f, 0.65f), 0.55f * fade * (0.35f + 0.65f * along));

                EmpressArt.DrawShape(spriteBatch, npc, at, screenPos, 1f, colour, 6, 6);
            }
        }

        // ---- over the players --------------------------------------------------------------------------------

        /// <summary>What goes over the player: her cage, its sides flying when it breaks, her ten stars spinning out of her.</summary>
        internal static void DrawOverPlayers()
        {
            if (Main.gameMenu || Main.dedServ)
                return;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                EmpressOfLight empress = Of(i);

                if (empress == null || empress.scene != Scene.Arrival && !empress.LegendsOver)
                    continue;

                try
                {
                    if (empress.scene == Scene.Arrival)
                        empress.DrawOver(Main.npc[i]);
                    else
                        empress.DrawLegendsOver(Main.npc[i]);
                }
                catch (Exception e)
                {
                    ModContent.GetInstance<EmpressOfLightSystem>()?.Mod.Logger.Warn("Empress cage draw: " + e.Message);
                }
            }
        }

        private void DrawOver(NPC npc)
        {
            bool caging = beat >= ArriveFlash && beat <= ArriveForm;
            bool shards = (beat == ArriveBreak || beat == ArriveFall || beat == ArriveCaught) && Main.GameUpdateCount - breakAt < ShardTicks;

            // The stars' flight: their spin (STARS) and their shot out to the corners (the start of FORM).
            float flight = beat == ArriveStars ? timer : beat == ArriveForm ? StarsTicks + timer : -1f;
            bool stars = flight >= 0f && flight < FlightTicks;

            // Come to their places, the stars are handed to the arena's drawing (EmpressVoidSystem), which
            // fades them up: these fade down over it, so there is no dip between.
            float handing = beat == ArriveForm && !stars ? 1f - Ease((timer - OutTicks) / (float)StarHandover) : 0f;

            if (!caging && !shards && !stars && handing <= 0f)
                return;

            SpriteBatch spriteBatch = Main.spriteBatch;

            // The stars' trails first: bands the batch cannot draw.
            if (stars)
                DrawStarTrails(flight);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            try
            {
                if (caging)
                    DrawCage(spriteBatch, npc.Center, CageGrown(), 1f);

                if (shards)
                    DrawShards(spriteBatch, arenaCentre, Main.GameUpdateCount - breakAt);

                if (stars)
                    DrawFlyingStars(spriteBatch, flight, 1f);
                else if (handing > 0f)
                    DrawFlyingStars(spriteBatch, FlightTicks, handing);
            }
            finally
            {
                spriteBatch.End();
            }
        }

        /// <summary>How much of the cage is drawn: its sides growing out of its corners as it shuts, all of it after.</summary>
        private float CageGrown()
        {
            if (beat == ArriveFlash)
                return 0f;

            if (beat == ArriveCage)
                return Ease(timer / (float)CageShuts);

            return 1f;
        }

        /// <summary>
        /// The cage: a hexagon of her light round the player, lying flat as her arena does, a smaller one
        /// turned inside it, a glint at each corner and a bead of light going round.
        /// </summary>
        private static void DrawCage(SpriteBatch spriteBatch, Vector2 centre, float grown, float strength)
        {
            if (grown <= 0.003f)
                return;

            float time = Time;
            float radius = CageRadius * (1f + 0.03f * (float)Math.Sin(time * 4f));

            // Faint light inside it.
            EmpressArt.DrawBloom(spriteBatch, centre, radius * 1.7f, EmpressArt.Additive(EmpressArt.Sky, 0.14f * strength * grown));

            for (int k = 0; k < 6; k++)
            {
                Vector2 a = Hexagon.Corner(centre, radius, k);
                Vector2 b = Vector2.Lerp(a, Hexagon.Corner(centre, radius, k + 1), grown);
                Color colour = EmpressArt.Prism(k / 6f + time * 0.2f);

                EmpressArt.DrawLine(spriteBatch, a, b, 12f, EmpressArt.Additive(colour, 0.55f * strength));
                EmpressArt.DrawLine(spriteBatch, a, b, 3f, EmpressArt.Additive(Color.White, 0.9f * strength));

                // The smaller one, turned, inside.
                Vector2 ia = centre + (Hexagon.Corner(Vector2.Zero, radius * 0.55f, k)).RotatedBy(MathHelper.Pi / 6f);
                Vector2 ib = Vector2.Lerp(ia, centre + Hexagon.Corner(Vector2.Zero, radius * 0.55f, k + 1).RotatedBy(MathHelper.Pi / 6f), grown);

                EmpressArt.DrawLine(spriteBatch, ia, ib, 5f, EmpressArt.Additive(colour, 0.25f * strength));

                float twinkle = 0.7f + 0.3f * (float)Math.Sin(time * 5f + k * 1.7f);
                EmpressArt.DrawStar(spriteBatch, a, 26f * twinkle, time * 1.5f + k, EmpressArt.Additive(Color.White, 0.85f * strength * Math.Min(1f, grown * 3f)));
            }

            // A bead of light going round it.
            if (grown >= 0.999f)
            {
                float u = time * 0.8f % 1f * 6f;
                int edge = (int)u;
                Vector2 bead = Vector2.Lerp(Hexagon.Corner(centre, radius, edge), Hexagon.Corner(centre, radius, edge + 1), u - edge);

                EmpressArt.DrawBloom(spriteBatch, bead, 30f, EmpressArt.Additive(EmpressArt.Pink, 0.7f * strength));
                EmpressArt.DrawStar(spriteBatch, bead, 20f, time * 3f, EmpressArt.Additive(Color.White, 0.9f * strength));
            }
        }

        /// <summary>The cage's six sides thrown off it when it breaks, turning as they go, and going out.</summary>
        private static void DrawShards(SpriteBatch spriteBatch, Vector2 centre, uint since)
        {
            float t = since;
            float fade = 1f - t / ShardTicks;

            if (fade <= 0f)
                return;

            for (int k = 0; k < 6; k++)
            {
                Vector2 a = Hexagon.Corner(centre, CageRadius, k);
                Vector2 b = Hexagon.Corner(centre, CageRadius, k + 1);
                Vector2 middle = (a + b) * 0.5f;
                Vector2 half = (b - a) * 0.5f * (1f - 0.4f * (1f - fade));

                // Out the way its side faces, slowing; turning, each its own way.
                float flown = 9f * (1f - (float)Math.Pow(0.9, t)) / 0.1f;
                Vector2 at = middle + Hexagon.Normal(k) * flown + new Vector2(0f, 0.04f * t * t);
                float turn = (k % 2 == 0 ? 1f : -1f) * 0.12f * t;
                Vector2 h = half.RotatedBy(turn);
                Color colour = EmpressArt.Prism(k / 6f);

                EmpressArt.DrawLine(spriteBatch, at - h, at + h, 10f, EmpressArt.Additive(colour, 0.5f * fade));
                EmpressArt.DrawLine(spriteBatch, at - h, at + h, 2.5f, EmpressArt.Additive(Color.White, 0.9f * fade));
            }
        }

        /// <summary>The ticks over which the stars, come to their places, are handed to the arena's drawing.</summary>
        private const int StarHandover = 24;

        /// <summary>Her ten stars, spinning out of her to their places: a star of light in a ball of her colour each.</summary>
        private void DrawFlyingStars(SpriteBatch spriteBatch, float t, float strength)
        {
            float time = Time;

            // Small as they leave her, all ten on top of one another, and grown as they spread.
            float grow = 0.3f + 0.7f * Ease(t / 24f);

            for (int i = 0; i < Decagon.Stars; i++)
            {
                Vector2 at = StarFlight(i, t);
                Color colour = EmpressArt.Prism(i / 10f + time * 0.1f);
                float across = 110f * grow;

                EmpressArt.DrawBloom(spriteBatch, at, across * 2.2f, EmpressArt.Additive(colour, 0.55f * strength * grow));
                EmpressArt.DrawStar(spriteBatch, at, across, time * 4f + i, EmpressArt.Additive(colour, strength));
                EmpressArt.DrawStar(spriteBatch, at, across * 0.55f, time * 4f + i + MathHelper.PiOver4, EmpressArt.Additive(Color.White, 0.9f * strength));
                EmpressArt.DrawBloom(spriteBatch, at, across * 0.3f, EmpressArt.Additive(Color.White, strength));
            }
        }

        /// <summary>The spiral each star has come along, as a band of her colours behind it, thinning and going.</summary>
        private void DrawStarTrails(float t)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            Texture2D line = EmpressArt.Line.Value;
            const int Steps = 16;
            const float Back = 0.9f;

            for (int i = 0; i < Decagon.Stars; i++)
            {
                Strip.Begin();

                for (int k = 0; k <= Steps; k++)
                {
                    float when = t - k * Back;

                    if (when < 0f)
                        break;

                    Vector2 at = StarFlight(i, when);
                    Vector2 ahead = StarFlight(i, when + 0.25f);
                    Vector2 way = ahead - at;

                    if (way.LengthSquared() < 0.0001f)
                        way = Vector2.UnitX;

                    way.Normalize();

                    Vector2 across = new Vector2(-way.Y, way.X);
                    float left = 1f - k / (float)Steps;
                    float width = 34f * left + 2f;
                    Color colour = EmpressArt.Additive(EmpressArt.Prism(i / 10f + k * 0.04f), 0.8f * left);

                    Strip.Pair(at + across * width * 0.5f, at - across * width * 0.5f, colour, colour, k / (float)Steps);
                }

                Strip.Draw(line);
            }
        }

        // ---- her death -----------------------------------------------------------------------------------------

        private void DrawDeath(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            // Gone into the butterfly, she still leaves her last light a moment.
            if (npc.Opacity <= 0.003f && beat != DeathShrink)
                return;

            int t = timer;
            EmpressArt.Look look = EmpressArt.Look.Of(npc);
            look.Size = DeathSize();

            // Her veil, if she had come to it, to the end.
            look.Veil = unveiled && PADWorld.Legends ? 1f : 0f;

            float cracks = 0f;
            float crackLight = 0f;
            float eyes = 0f;
            float rays = 0f;
            float glow = 0.3f;

            switch (beat)
            {
                case DeathSlow:
                    look.LeftArm = look.RightArm = 2;
                    look.Bright = 0.5f * (1f - Ease(t / 20f));
                    glow = 0.3f;
                    break;

                case DeathEyes:
                    look.LeftArm = look.RightArm = 0;
                    look.Bright = 0.1f * Ease(t / 30f);
                    eyes = Ease(t / 24f);
                    glow = 0.35f;
                    break;

                case DeathCracks:
                    {
                        float p = t / (float)CracksTicks;

                        look.LeftArm = look.RightArm = 0;
                        look.Bright = MathHelper.Lerp(0.1f, 0.35f, p);
                        eyes = 1f;
                        cracks = p;
                        crackLight = 0.5f + 0.5f * p;
                        rays = 0.2f * p;
                        glow = 0.35f + 0.35f * p;
                        break;
                    }

                case DeathClose:
                    {
                        float p = t / (float)CloseTicks;

                        look.LeftArm = look.RightArm = 0;
                        look.Bright = MathHelper.Lerp(0.35f, 0.75f, p);
                        look.White = 0.45f * p * p;
                        eyes = 1f + 0.6f * p;
                        cracks = 1f;
                        crackLight = 1f + 0.8f * p;
                        rays = 0.2f + 0.8f * p;
                        glow = 0.7f + 0.5f * p;
                        break;
                    }

                case DeathBurst:
                    {
                        float p = t / (float)BurstTicks;

                        look.LeftArm = look.RightArm = 6;
                        look.Bright = 1f;
                        look.White = MathHelper.Lerp(0.45f, 1f, Ease(p * 2f));
                        eyes = 1.6f * (1f - Ease(t / 12f));
                        rays = 1f - 0.5f * p;
                        glow = 1.4f - 0.4f * p;
                        break;
                    }

                case DeathShrink:
                    look.LeftArm = look.RightArm = 1;
                    look.Bright = 1f;
                    look.White = 1f;
                    rays = 0.5f * (1f - Ease(t / 30f));
                    glow = 1f;
                    break;
            }

            Restart(spriteBatch, true);
            DrawBackGlow(spriteBatch, npc.Center, glow, Math.Max(0.15f, look.Size));

            if (rays > 0.003f)
                DrawHerRays(spriteBatch, npc.Center, rays, look.Size);

            Restart(spriteBatch, false);
            EmpressArt.DrawHer(spriteBatch, npc, screenPos, look);
            Restart(spriteBatch, true);

            if (cracks > 0f)
                DrawCracks(npc, spriteBatch, cracks, crackLight, look.Size);

            // The light out of her eyes: bands the batch cannot draw, so with the batch put down.
            if (eyes > 0.003f)
            {
                spriteBatch.End();

                try
                {
                    DrawEyeBeams(npc, eyes);
                }
                finally
                {
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
                }

                DrawEyeFlares(npc, spriteBatch, eyes);
            }

            // Her last light, as she is all but gone into the butterfly.
            if (beat == DeathShrink)
            {
                float left = 1f - Ease((t - ButterflyAt + 8f) / 14f);
                EmpressArt.DrawBloom(spriteBatch, npc.Center, 70f * Math.Max(look.Size, 0.2f) + 30f, EmpressArt.Additive(Color.White, 0.9f * left));
                EmpressArt.DrawStar(spriteBatch, npc.Center, 60f * left + 10f, Time * 3f, EmpressArt.Additive(EmpressArt.Lilac, 0.8f * left));
            }

            Restart(spriteBatch, false);
        }

        /// <summary>Light out of her every way, behind her, turning slowly: as the border closes, and as it goes.</summary>
        private static void DrawHerRays(SpriteBatch spriteBatch, Vector2 at, float strength, float size)
        {
            float time = Time;
            const int Rays = 14;

            for (int k = 0; k < Rays; k++)
            {
                float angle = k / (float)Rays * MathHelper.TwoPi + time * 0.25f + 0.2f * (float)Math.Sin(time + k);
                float length = (320f + 380f * (0.5f + 0.5f * (float)Math.Sin(time * 1.3f + k * 2.1f))) * strength * Math.Max(0.3f, size);
                float width = 40f + 30f * (k % 3);

                EmpressArt.DrawRay(spriteBatch, at, angle, length, width, EmpressArt.Additive(EmpressArt.Prism(k / (float)Rays + time * 0.05f), 0.16f * Math.Min(1f, strength)));
            }
        }

        /// <summary>
        /// The cracks through her, as far open as <paramref name="p"/> of the cracking: each a line of white with
        /// her colour about it, run out from where it starts, and light shining out of it, away from her middle.
        /// </summary>
        private static void DrawCracks(NPC npc, SpriteBatch spriteBatch, float p, float light, float size)
        {
            List<EmpressArt.Crack> cracks = EmpressArt.Cracks(npc.AI_120_HallowBoss_IsInPhase2());

            if (cracks == null)
                return;

            SpriteEffects effects = EmpressArt.Effects(npc);
            Vector2 middle = npc.Center;
            float scale = npc.scale * size;
            float time = Time;

            foreach (EmpressArt.Crack crack in cracks)
            {
                float open = MathHelper.Clamp((p - crack.At) / crack.Run, 0f, 1f);

                if (open <= 0f)
                    continue;

                float cut = crack.Length * open;
                Color colour = EmpressArt.Prism(crack.Hue + time * 0.08f);
                float flicker = 0.85f + 0.15f * (float)Math.Sin(time * 9f + crack.Hue * 20f);

                for (int i = 1; i < crack.Points.Length; i++)
                {
                    if (crack.Along[i - 1] >= cut)
                        break;

                    Vector2 a = EmpressArt.OnHer(crack.Points[i - 1], middle, scale, effects);
                    Vector2 b = EmpressArt.OnHer(crack.Points[i], middle, scale, effects);

                    if (crack.Along[i] > cut)
                        b = Vector2.Lerp(a, b, (cut - crack.Along[i - 1]) / Math.Max(0.001f, crack.Along[i] - crack.Along[i - 1]));

                    EmpressArt.DrawLine(spriteBatch, a, b, 6f * scale, EmpressArt.Additive(colour, 0.55f * Math.Min(1f, light) * flicker));
                    EmpressArt.DrawLine(spriteBatch, a, b, 1.6f * scale, EmpressArt.Additive(Color.White, 0.95f * flicker));

                    // Light shining out of it, every other point of it, away from her middle.
                    if (i % 2 == 0)
                    {
                        float sway = 0.25f * (float)Math.Sin(time * 2f + i + crack.Hue * 10f);
                        float angle = crack.Out + sway;

                        // Its way out turned with her, if she is drawn the other way about.
                        if ((effects & SpriteEffects.FlipHorizontally) != 0)
                            angle = MathHelper.Pi - angle;

                        float length = (24f + 120f * Math.Min(2f, light) * (0.6f + 0.4f * (float)Math.Sin(time * 3f + i))) * open * scale;
                        EmpressArt.DrawRay(spriteBatch, b, angle, length, 5f * scale, EmpressArt.Additive(colour, 0.3f * Math.Min(1f, light)));
                    }
                }

                Vector2 mid = EmpressArt.OnHer(crack.Points[crack.Points.Length / 2], middle, scale, effects);
                EmpressArt.DrawBloom(spriteBatch, mid, 30f * scale, EmpressArt.Additive(colour, 0.35f * Math.Min(1.5f, light) * open));
            }
        }

        /// <summary>Where the light out of each eye goes: down and out either side of the player, searching, as she says.</summary>
        private static float BeamAngle(int side, float time) =>
            MathHelper.PiOver2 - side * MathHelper.ToRadians(32f) + MathHelper.ToRadians(9f) * (float)Math.Sin(time * 0.6f + side * 1.3f);

        /// <summary>
        /// The light out of her eyes: a cone out of each, narrow at the eye and wide at its end, her pink out
        /// of one and her blue out of the other, white down its middle, gone by its end.
        /// </summary>
        private static void DrawEyeBeams(NPC npc, float strength)
        {
            if (!EmpressArt.Ready(EmpressArt.Line))
                return;

            Texture2D line = EmpressArt.Line.Value;
            float time = Time;
            float length = 1400f * Math.Min(1f, strength);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = npc.Center + (side < 0 ? EmpressArt.LeftEye : EmpressArt.RightEye) * npc.scale;
                float angle = BeamAngle(side, time);
                Color tint = side < 0 ? EmpressArt.Pink : EmpressArt.Sky;

                Cone(line, eye, angle, length, 3f, 190f, EmpressArt.Additive(tint, 0.32f * Math.Min(1.4f, strength)));
                Cone(line, eye, angle, length * 0.85f, 1.5f, 34f, EmpressArt.Additive(Color.White, 0.5f * Math.Min(1.4f, strength)));
            }
        }

        /// <summary>One cone of light: from a point, this long, this wide where it leaves and where it ends, gone by its end.</summary>
        private static void Cone(Texture2D texture, Vector2 from, float angle, float length, float near, float far, Color colour)
        {
            const int Steps = 12;

            Vector2 way = angle.ToRotationVector2();
            Vector2 across = new Vector2(-way.Y, way.X);

            Strip.Begin();

            for (int k = 0; k <= Steps; k++)
            {
                float u = k / (float)Steps;
                Vector2 at = from + way * (length * u);
                float half = MathHelper.Lerp(near, far, u) * 0.5f;
                Color c = colour * (float)Math.Pow(1f - u, 1.3);

                Strip.Pair(at + across * half, at - across * half, c, c, u);
            }

            Strip.Draw(texture);
        }

        /// <summary>A star of light at each eye, turning, and the light round it.</summary>
        private static void DrawEyeFlares(NPC npc, SpriteBatch spriteBatch, float strength)
        {
            float time = Time;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = npc.Center + (side < 0 ? EmpressArt.LeftEye : EmpressArt.RightEye) * npc.scale;
                float pulse = 0.85f + 0.15f * (float)Math.Sin(time * 6f + side);

                EmpressArt.DrawBloom(spriteBatch, eye, 34f * pulse * Math.Min(1.5f, strength), EmpressArt.Additive(side < 0 ? EmpressArt.Pink : EmpressArt.Sky, 0.8f * Math.Min(1f, strength)));
                EmpressArt.DrawStar(spriteBatch, eye, 52f * pulse * Math.Min(1.5f, strength), time * 1.2f * side, EmpressArt.Additive(Color.White, 0.9f * Math.Min(1f, strength)));
            }
        }
    }
}
