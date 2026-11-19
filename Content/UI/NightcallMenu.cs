using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Melee;
using PisAndDag.Content.NPCs.Mech;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// Nightcall's menu (by request: "reload will open a UI where there is 2 circles next to player, click on one opens 4
    /// options"; asked after: "its just circles like Andromeda UI", Calamity's, taken as a look only): on the reload key,
    /// with Nightcall in hand, two dark circles in steel rings beside the player, the left hand's on the left and the
    /// right's on the right, each with its mark (vice, cannon, chainsaw, laser) lit in Skeletron Prime's green. Clicking
    /// one fans the four out from it, on its own side; clicking one of those sets that hand (NightcallPlayer); the one it
    /// is now is ringed. Hovered, a circle is ringed in green and its name is shown. The key again closes it, as does
    /// putting Nightcall away.
    ///
    /// In the world, beside the player, as the Andromeda's is: drawn at the game's zoom, the mouse taken in the world.
    /// Only for the player whose menu it is. While the mouse is on it, the fists do not punch.
    /// </summary>
    public class NightcallMenu : ModSystem
    {
        /// <summary>How far beside the player each hand's circle is, how far up; how far out the four are from it, and their angles (on the right; mirrored on the left).</summary>
        private const float Apart = 48f, Up = 10f, Out = 44f;
        private static readonly float[] Fan = { -66f, -22f, 22f, 66f };

        /// <summary>What reaches a circle: its radius, as drawn.</summary>
        private const float BigRadius = 17f, SmallRadius = 12f;

        /// <summary>Ticks it takes to come in, and the four to fan out.</summary>
        private const int FadeTicks = 10, FanTicks = 8;

        public static bool Open { get; private set; }

        /// <summary>The hand whose four are out (0 left, 1 right), or none.</summary>
        private static int picking = -1;
        private static int openTicks, fanTicks;

        private static Asset<Texture2D> big, bigHover, small, smallHover, marks;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            big = ModContent.Request<Texture2D>("PisAndDag/Content/UI/NightcallUI_Big");
            bigHover = ModContent.Request<Texture2D>("PisAndDag/Content/UI/NightcallUI_BigHover");
            small = ModContent.Request<Texture2D>("PisAndDag/Content/UI/NightcallUI_Small");
            smallHover = ModContent.Request<Texture2D>("PisAndDag/Content/UI/NightcallUI_SmallHover");
            marks = ModContent.Request<Texture2D>("PisAndDag/Content/UI/NightcallIcons");
        }

        public override void Unload()
        {
            big = bigHover = small = smallHover = marks = null;
            Open = false;
        }

        public override void OnWorldUnload() => Close();

        public static void Toggle()
        {
            if (Open)
            {
                Close();
                SoundEngine.PlaySound(SoundID.MenuClose);
                return;
            }

            Open = true;
            picking = -1;
            openTicks = 0;
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public static void Close()
        {
            Open = false;
            picking = -1;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (!Open)
                return;

            Player me = Main.LocalPlayer;

            if (Main.gameMenu || !me.active || me.dead || me.HeldItem?.ModItem is not Nightcall)
            {
                Close();
                return;
            }

            if (openTicks < FadeTicks)
                openTicks++;

            if (picking >= 0 && fanTicks < FanTicks)
                fanTicks++;
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer("PisAndDag: Nightcall Menu", () =>
            {
                if (Open)
                    Draw(Main.spriteBatch);

                return true;
            }, InterfaceScaleType.Game));
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Where a hand's circle is, in the world.</summary>
        private static Vector2 HandAt(Player me, int hand) => me.MountedCenter + new Vector2(hand == 0 ? -Apart : Apart, -Up);

        /// <summary>Where one of a hand's four is, in the world, <paramref name="grown"/> of the way out.</summary>
        private static Vector2 ChoiceAt(Player me, int hand, int k, float grown)
        {
            float angle = MathHelper.ToRadians(Fan[k]);
            Vector2 way = angle.ToRotationVector2();

            if (hand == 0)
                way.X = -way.X;

            return HandAt(me, hand) + way * Out * grown;
        }

        private static bool Over(Vector2 at, float radius) => !PlayerInput.IgnoreMouseInterface && Vector2.Distance(Main.MouseWorld, at) <= radius;

        private static bool Clicked => Main.mouseLeft && Main.mouseLeftRelease;

        private static void Take()
        {
            Main.mouseLeftRelease = false;
            Main.LocalPlayer.mouseInterface = true;
        }

        private static void Draw(SpriteBatch batch)
        {
            if (big == null || !big.IsLoaded || !marks.IsLoaded)
                return;

            Player me = Main.LocalPlayer;
            NightcallPlayer hands = me.GetModPlayer<NightcallPlayer>();
            float fade = Smooth(openTicks / (float)FadeTicks);
            bool used = false;

            // The four of the hand being set, under its circle.
            if (picking >= 0)
            {
                float grown = Smooth(fanTicks / (float)FanTicks);
                NightcallFist now = hands.Of(picking);

                for (int k = 0; k < 4; k++)
                {
                    Vector2 at = ChoiceAt(me, picking, k, grown);
                    bool hover = grown > 0.9f && Over(at, SmallRadius);
                    NightcallFist fist = (NightcallFist)k;

                    Plate(batch, hover || fist == now ? smallHover : small, at, fade * grown, hover ? 1.12f : 1f);
                    Mark(batch, fist, at, fade * grown, hover ? 1.12f : 1f, hover || fist == now);

                    if (!hover)
                        continue;

                    me.mouseInterface = true;
                    Label(batch, fist, at - new Vector2(0f, SmallRadius + 6f));

                    if (Clicked && !used)
                    {
                        used = true;
                        Take();
                        hands.Set(picking, fist);
                        picking = -1;
                        SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.5f, Pitch = 0.3f });
                        SoundEngine.PlaySound(SoundID.MenuTick);
                    }
                }
            }

            // The two hands.
            for (int hand = 0; hand < 2; hand++)
            {
                Vector2 at = HandAt(me, hand);
                bool hover = fade > 0.9f && Over(at, BigRadius);
                bool lit = hover || picking == hand;

                Plate(batch, lit ? bigHover : big, at, fade, 1f);
                Mark(batch, hands.Of(hand), at, fade, 1.25f, true);

                if (!hover)
                    continue;

                me.mouseInterface = true;
                Label(batch, hands.Of(hand), at - new Vector2(0f, BigRadius + 6f));

                if (Clicked && !used)
                {
                    used = true;
                    Take();
                    picking = picking == hand ? -1 : hand;
                    fanTicks = 0;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            // A click anywhere else, with the four out, puts them away (and is not a punch).
            if (picking >= 0 && Clicked && !used)
            {
                Take();
                picking = -1;
            }
        }

        private static void Plate(SpriteBatch batch, Asset<Texture2D> plate, Vector2 at, float alpha, float scale)
        {
            Texture2D texture = plate.Value;
            batch.Draw(texture, at - Main.screenPosition, null, Color.White * alpha, 0f, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>A mark: its green, unlit by the place; brighter when it is the one or is hovered.</summary>
        private static void Mark(SpriteBatch batch, NightcallFist fist, Vector2 at, float alpha, float scale, bool bright)
        {
            Texture2D texture = marks.Value;
            Rectangle frame = new Rectangle((int)fist * 20, 0, 16, 16);
            Color colour = (bright ? Color.White : new Color(170, 176, 160)) * alpha;
            batch.Draw(texture, at - Main.screenPosition, frame, colour, 0f, new Vector2(8f, 8f), scale, SpriteEffects.None, 0f);

            if (bright)
                batch.Draw(texture, at - Main.screenPosition, frame, PrimeArt.Lit(PrimeArt.Nuclear) * (0.25f * alpha), 0f, new Vector2(8f, 8f), scale * 1.15f, SpriteEffects.None, 0f);
        }

        private static void Label(SpriteBatch batch, NightcallFist fist, Vector2 at)
        {
            string text = Language.GetTextValue("Mods.PisAndDag.Nightcall." + fist);
            Utils.DrawBorderString(batch, text, at - Main.screenPosition, Color.Lerp(PrimeArt.NuclearHot, Color.White, 0.4f), 0.8f, 0.5f, 1f);
        }
    }
}
