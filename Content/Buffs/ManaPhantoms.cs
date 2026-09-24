using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Buffs
{
    /// <summary>
    /// What else the trip shows (ManaNauseaScreen.Trip): critters and townsfolk that are not
    /// there, at the sides of the screen, over everything, going about their business.
    ///
    /// They come in from the edges or out of nothing, a few seconds each, and do one thing
    /// while they are there: walk, hop, turn slowly round, swell until they fill half the
    /// screen, float up and away, or dance on the spot facing one way and then the other.
    /// Sometimes three come at once, in step. They are the world's own townsfolk when it has
    /// any, and critters, in colours the trip gives them.
    ///
    /// Drawn after the screen filters, in screen pixels, so the slices do not cut them up:
    /// they are over the picture, not in it. How many come, and how clearly, follows the
    /// trip up and down. Client only; nobody else sees them.
    /// </summary>
    public class ManaPhantoms : ModSystem
    {
        private enum Act { Walk, Hop, Spin, Swell, Float, Dance }

        private sealed class Phantom
        {
            public int Type;
            public bool Town;
            public Act Act;
            public Vector2 Start, Pos;
            public float Speed, Scale, Rotation, Hue, Seed, MaxAlpha;
            public int Dir, Side, Age, Life;
        }

        /// <summary>The most on screen at once, at the height of the trip.</summary>
        private const int Most = 10;

        /// <summary>Below this much trip nothing new comes.</summary>
        private const float Threshold = 0.35f;

        /// <summary>Ticks to come in and to go.</summary>
        private const int FadeIn = 40, FadeOut = 60;

        /// <summary>How far in from each side they keep, as a share of the screen's width.</summary>
        private const float SideBand = 0.24f;

        private static readonly int[] Critters =
        {
            NPCID.Bunny, NPCID.GoldBunny, NPCID.Bird, NPCID.BirdBlue, NPCID.BirdRed, NPCID.Squirrel,
            NPCID.SquirrelRed, NPCID.Butterfly, NPCID.Frog, NPCID.Mouse, NPCID.Rat, NPCID.Duck,
            NPCID.Grebe, NPCID.Seagull, NPCID.Penguin, NPCID.Snail, NPCID.Owl, NPCID.Turtle, NPCID.Goldfish,
        };

        private static readonly int[] Townsfolk =
        {
            NPCID.Guide, NPCID.Merchant, NPCID.Nurse, NPCID.ArmsDealer, NPCID.Dryad, NPCID.Demolitionist,
            NPCID.Clothier, NPCID.Painter, NPCID.Angler, NPCID.Stylist, NPCID.DyeTrader, NPCID.PartyGirl,
            NPCID.GoblinTinkerer, NPCID.Mechanic, NPCID.Wizard, NPCID.TownCat, NPCID.TownDog, NPCID.TownBunny,
        };

        private static readonly List<Phantom> phantoms = new List<Phantom>();
        private static readonly List<int> living = new List<int>();
        private static int wait;

        public override void Load()
        {
            if (!Main.dedServ)
                Main.OnPostDraw += DrawOverEverything;
        }

        public override void Unload()
        {
            if (!Main.dedServ)
                Main.OnPostDraw -= DrawOverEverything;

            phantoms.Clear();
        }

        public override void OnWorldUnload()
        {
            phantoms.Clear();
            wait = 0;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            float trip = ManaNauseaScreen.Trip;

            if (trip > Threshold && phantoms.Count < (int)Math.Ceiling(Most * trip) && --wait <= 0)
            {
                Come(trip);
                wait = (int)(Main.rand.Next(25, 75) / Math.Max(0.4f, trip));
            }

            for (int i = phantoms.Count - 1; i >= 0; i--)
            {
                Phantom p = phantoms[i];
                if (++p.Age >= p.Life)
                {
                    phantoms.RemoveAt(i);
                    continue;
                }

                Move(p);
            }
        }

        /// <summary>One more, or three in step.</summary>
        private static void Come(float trip)
        {
            int type = Pick(out bool town);
            bool left = Main.rand.NextBool();
            float w = Main.screenWidth, h = Main.screenHeight;

            var p = new Phantom
            {
                Type = type,
                Town = town,
                Act = (Act)Main.rand.Next(6),
                Dir = left ? 1 : -1,
                Side = left ? -1 : 1,
                Speed = Main.rand.NextFloat(0.4f, 1.3f),
                Scale = Main.rand.NextFloat(1.6f, 3.2f),
                Hue = Main.rand.NextFloat(),
                Seed = Main.rand.NextFloat(100f),
                MaxAlpha = Main.rand.NextFloat(0.55f, 0.9f),
                Life = Main.rand.Next(240, 600),
            };

            // Walkers and hoppers come in from the edge; the rest appear somewhere in the band.
            float x = p.Act == Act.Walk || p.Act == Act.Hop
                ? (left ? -0.04f : 1.04f) * w
                : (left ? Main.rand.NextFloat(0.02f, SideBand) : Main.rand.NextFloat(1f - SideBand, 0.98f)) * w;
            p.Start = p.Pos = new Vector2(x, Main.rand.NextFloat(0.15f, 0.9f) * h);

            if (p.Act == Act.Swell)
            {
                p.MaxAlpha *= 0.6f;
                p.Life = Main.rand.Next(200, 360);
            }

            phantoms.Add(p);

            // Now and then the same one twice more, in step behind it.
            if (p.Act == Act.Walk && Main.rand.NextBool(3))
            {
                for (int k = 1; k <= 2; k++)
                {
                    phantoms.Add(new Phantom
                    {
                        Type = p.Type, Town = p.Town, Act = p.Act, Dir = p.Dir, Side = p.Side, Speed = p.Speed, Scale = p.Scale,
                        Hue = (p.Hue + 0.2f * k) % 1f, Seed = p.Seed, MaxAlpha = p.MaxAlpha, Life = p.Life,
                        Start = p.Start - new Vector2(p.Dir * 60f * k * p.Scale, 0f),
                        Pos = p.Start - new Vector2(p.Dir * 60f * k * p.Scale, 0f),
                    });
                }
            }
        }

        /// <summary>A townsperson or a critter. The world's own townsfolk first, when it has any.</summary>
        private static int Pick(out bool town)
        {
            town = Main.rand.NextBool();
            if (!town)
                return Critters[Main.rand.Next(Critters.Length)];

            living.Clear();
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.townNPC && !living.Contains(npc.type))
                    living.Add(npc.type);
            }

            if (living.Count > 0 && Main.rand.Next(10) < 6)
                return living[Main.rand.Next(living.Count)];

            return Townsfolk[Main.rand.Next(Townsfolk.Length)];
        }

        private static void Move(Phantom p)
        {
            float a = p.Age;

            // Walkers and hoppers keep to their side: at the inner edge of the band they turn back.
            float inner = p.Side < 0 ? SideBand * Main.screenWidth : (1f - SideBand) * Main.screenWidth;
            if ((p.Act == Act.Walk || p.Act == Act.Hop) && (p.Side < 0 ? p.Pos.X > inner && p.Dir > 0 : p.Pos.X < inner && p.Dir < 0))
                p.Dir = -p.Dir;

            switch (p.Act)
            {
                case Act.Walk:
                    p.Pos.X += p.Dir * p.Speed;
                    p.Pos.Y = p.Start.Y - Math.Abs((float)Math.Sin(a * 0.25f)) * 2f;
                    break;

                case Act.Hop:
                    p.Pos.X += p.Dir * p.Speed * 1.4f;
                    p.Pos.Y = p.Start.Y - Math.Abs((float)Math.Sin(a * 0.09f + p.Seed)) * 36f * p.Scale * 0.5f;
                    break;

                case Act.Spin:
                    p.Pos = p.Start + new Vector2((float)Math.Sin(a * 0.02f + p.Seed) * 30f, (float)Math.Cos(a * 0.017f) * 20f);
                    p.Rotation += 0.02f * p.Dir;
                    break;

                case Act.Swell:
                    p.Pos = Vector2.Lerp(p.Start, new Vector2(Main.screenWidth * 0.5f, p.Start.Y), a / p.Life * 0.25f);
                    break;

                case Act.Float:
                    p.Pos = p.Start + new Vector2((float)Math.Sin(a * 0.05f + p.Seed) * 26f, -a * 0.45f);
                    p.Rotation = (float)Math.Sin(a * 0.04f + p.Seed) * 0.35f;
                    break;

                case Act.Dance:
                    p.Pos = p.Start + new Vector2(0f, -Math.Abs((float)Math.Sin(a * 0.2f)) * 8f);
                    if (p.Age % 24 == 0)
                        p.Dir = -p.Dir;
                    break;
            }
        }

        private static float Alpha(Phantom p)
        {
            float fadeIn = Math.Min(1f, p.Age / (float)FadeIn);
            float fadeOut = Math.Min(1f, (p.Life - p.Age) / (float)FadeOut);
            return p.MaxAlpha * fadeIn * fadeOut * Math.Min(1f, ManaNauseaScreen.Trip * 1.5f);
        }

        /// <summary>The frame this one is on: a townsperson's walk, or a critter's whole strip in turn.</summary>
        private static int Frame(Phantom p, int frames)
        {
            if (p.Act == Act.Dance || p.Act == Act.Spin || p.Act == Act.Swell)
                return p.Town ? 0 : p.Age / 7 % frames;

            if (p.Town)
            {
                int end = frames - NPCID.Sets.ExtraFramesCount[p.Type];
                if (end > 3)
                    return 2 + p.Age / 5 % (end - 2);
            }

            return p.Age / 6 % frames;
        }

        private static void Draw(SpriteBatch batch)
        {
            if (phantoms.Count == 0)
                return;

            float time = Main.GlobalTimeWrappedHourly;

            foreach (Phantom p in phantoms)
            {
                float alpha = Alpha(p);
                if (alpha <= 0.01f)
                    continue;

                Main.instance.LoadNPC(p.Type);
                Texture2D texture = TextureAssets.Npc[p.Type].Value;
                int frames = Math.Max(1, Main.npcFrameCount[p.Type]);
                int height = texture.Height / frames;
                var source = new Rectangle(0, Frame(p, frames) * height, texture.Width, height);

                float scale = p.Scale;
                if (p.Act == Act.Swell)
                    scale *= 1f + 2.2f * (p.Age / (float)p.Life);
                else if (p.Act == Act.Dance)
                    scale *= 1f + 0.15f * (float)Math.Sin(p.Age * 0.15f);

                Color tint = Color.Lerp(Color.White, Main.hslToRgb((p.Hue + time * 0.12f) % 1f, 0.9f, 0.65f, 255), 0.55f) * alpha;

                // The sheets face left; a phantom going right is turned.
                SpriteEffects facing = p.Dir > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                batch.Draw(texture, p.Pos, source, tint, p.Rotation, new Vector2(texture.Width / 2f, height / 2f),
                    scale, facing, 0f);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (phantoms.Count == 0)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("PisAndDag: Mana Phantoms", () =>
            {
                // The layer's batch samples smoothly (GameInterfaceLayer.Draw begins it with the
                // defaults); these are sprites blown up, so sharp, then the batch as it was.
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                Draw(Main.spriteBatch);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Matrix.Identity);
                return true;
            }, InterfaceScaleType.None));
        }

        /// <summary>With the interface hidden its layers are not drawn; the phantoms still are.</summary>
        private static void DrawOverEverything(GameTime gameTime)
        {
            if (!Main.hideUI || Main.gameMenu || phantoms.Count == 0)
                return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            Draw(Main.spriteBatch);

            Main.spriteBatch.End();
        }
    }
}
