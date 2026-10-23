using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Utilities;

namespace PisAndDag.Content.Items
{
    /// <summary>
    /// The game's Inner Tube in five colours, by request ("make inner tube have color variations and is
    /// saved upon generation"): pink (the game's own), cyan, neon green, maroon and solar orange, one in
    /// five each. Each tube's colour is chosen when the tube is made, however it is made (found in a
    /// chest as the world is made, fished, bought, given), and kept with it: saved, sent to the other
    /// players, kept through a reforge.
    ///
    /// The pictures are the game's own, read at run time and recoloured shade for shade (both the item
    /// and the tube worn round the waist in water: six shades of pink, each given the same step of the
    /// new colour's ramp), never copied into the mod. A dye on the tube dyes the new colour as it did
    /// the pink.
    /// </summary>
    public class InnerTubeColours : GlobalItem
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Item entity, bool lateInstantiation) => entity.type == ItemID.FloatingTube;

        /// <summary>The colours, in order: 0 is the game's own pink.</summary>
        public const int Pink = 0, Cyan = 1, NeonGreen = 2, Maroon = 3, SolarOrange = 4;
        public const int Count = 5;

        /// <summary>This tube's colour.</summary>
        public byte Colour;

        /// <summary>A reforge makes the item afresh: its colour is carried across it.</summary>
        private static int reforging = -1;

        public override void SetDefaults(Item entity)
        {
            // The moment it is made. While the world is made, from the world's own dice (the game's
            // everyday dice belong to the main thread, and the world is made on another).
            UnifiedRandom dice = WorldGen.generatingWorld ? WorldGen.genRand : Main.rand;
            Colour = (byte)(dice?.Next(Count) ?? 0);
        }

        public override void PreReforge(Item item) => reforging = Colour;

        public override void PostReforge(Item item)
        {
            if (reforging >= 0)
                Colour = (byte)reforging;

            reforging = -1;
        }

        public override void SaveData(Item item, TagCompound tag) => tag["colour"] = Colour;

        public override void LoadData(Item item, TagCompound tag)
        {
            // A tube from before there were colours keeps the one it was given as it was loaded.
            if (tag.ContainsKey("colour"))
                Colour = (byte)Math.Clamp((int)tag.GetByte("colour"), 0, Count - 1);
        }

        public override void NetSend(Item item, BinaryWriter writer) => writer.Write(Colour);

        public override void NetReceive(Item item, BinaryReader reader) => Colour = (byte)Math.Clamp((int)reader.ReadByte(), 0, Count - 1);

        /// <summary>Its name says its colour (the pink, the game's own, is just the Inner Tube).</summary>
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (Colour == Pink)
                return;

            TooltipLine name = tooltips.Find(line => line.Mod == "Terraria" && line.Name == "ItemName");

            if (name != null)
                name.Text = Language.GetTextValue("Mods.PisAndDag.InnerTube.Named", Language.GetTextValue("Mods.PisAndDag.InnerTube.Colour" + Colour), name.Text);
        }

        /// <summary>Worn where it shows: the player is drawn wearing this one's colour.</summary>
        public override void UpdateVisibleAccessory(Item item, Player player, bool hideVisual)
        {
            if (!hideVisual)
                player.GetModPlayer<InnerTubeWorn>().Shown = Colour;
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D icon = Colour == Pink ? null : InnerTubeArt.Icon(Colour);

            if (icon == null)
                return true;

            spriteBatch.Draw(icon, position, frame, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D icon = Colour == Pink ? null : InnerTubeArt.Icon(Colour);

            if (icon == null)
                return true;

            // Where the game puts an item lying in the world: its middle over the bottom of its box.
            Vector2 at = new Vector2(item.position.X + item.width * 0.5f, item.position.Y + item.height - icon.Height * 0.5f + 2f) - Main.screenPosition;

            spriteBatch.Draw(icon, at, null, alphaColor, rotation, icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>Which colour of tube this player is drawn wearing this tick (asked again every tick).</summary>
    public class InnerTubeWorn : ModPlayer
    {
        public int Shown = -1;

        public override void ResetEffects() => Shown = -1;
    }

    /// <summary>
    /// The tube round the waist, in its colour: the game draws it in two parts (behind the body and in
    /// front of it) with its own pink picture; this comes after both and gives each the recoloured one,
    /// leaving where and how the game drew it, its light and its dye, as they were.
    /// </summary>
    public class InnerTubeLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new Between(PlayerDrawLayers.Torso, PlayerDrawLayers.OffhandAcc);

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            int shown = drawInfo.drawPlayer.GetModPlayer<InnerTubeWorn>().Shown;
            return drawInfo.drawFloatingTube && shown > InnerTubeColours.Pink && shown < InnerTubeColours.Count;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Texture2D pink = InnerTubeArt.WornPink();
            Texture2D worn = InnerTubeArt.Worn(drawInfo.drawPlayer.GetModPlayer<InnerTubeWorn>().Shown);

            if (pink == null || worn == null)
                return;

            List<DrawData> cache = drawInfo.DrawDataCache;

            for (int i = 0; i < cache.Count; i++)
            {
                if (cache[i].texture != pink)
                    continue;

                DrawData data = cache[i];
                data.texture = worn;
                cache[i] = data;
            }
        }
    }

    /// <summary>The recoloured pictures, made from the game's own the first time each is drawn.</summary>
    public static class InnerTubeArt
    {
        /// <summary>The game's six shades of the tube, darkest first.</summary>
        private static readonly Color[] Original =
        {
            new Color(85, 0, 39), new Color(166, 0, 75), new Color(229, 12, 114),
            new Color(255, 66, 152), new Color(255, 132, 184), new Color(255, 217, 234),
        };

        /// <summary>
        /// Each colour's six, darkest first, step for step with the pink's: the shadow ends cool, the
        /// light ends warm, as the game's own ramps go.
        /// </summary>
        private static readonly Color[][] Ramps =
        {
            Original,
            new[] { new Color(10, 38, 72), new Color(0, 98, 140), new Color(0, 168, 205), new Color(40, 220, 245), new Color(140, 245, 255), new Color(225, 255, 252) },
            new[] { new Color(16, 54, 40), new Color(20, 120, 44), new Color(40, 200, 40), new Color(110, 255, 70), new Color(185, 255, 140), new Color(238, 255, 214) },
            new[] { new Color(40, 6, 22), new Color(84, 10, 30), new Color(128, 18, 40), new Color(168, 44, 60), new Color(204, 98, 104), new Color(240, 196, 190) },
            new[] { new Color(72, 18, 18), new Color(150, 44, 8), new Color(225, 98, 10), new Color(255, 150, 30), new Color(255, 202, 90), new Color(255, 240, 196) },
        };

        private static readonly Texture2D[] icons = new Texture2D[InnerTubeColours.Count];
        private static readonly Texture2D[] worn = new Texture2D[InnerTubeColours.Count];
        private static Asset<Texture2D> wornPink;

        /// <summary>The game's tube worn round the waist (both parts on one picture).</summary>
        public static Texture2D WornPink()
        {
            if (Main.dedServ)
                return null;

            wornPink ??= Main.Assets.Request<Texture2D>("Images/Extra_" + ExtrasID.FloatingTube, AssetRequestMode.ImmediateLoad);
            return wornPink.Value;
        }

        public static Texture2D Icon(int colour)
        {
            if (Main.dedServ || colour <= InnerTubeColours.Pink || colour >= InnerTubeColours.Count)
                return null;

            if (icons[colour] == null || icons[colour].IsDisposed)
            {
                Main.instance.LoadItem(ItemID.FloatingTube);
                icons[colour] = Recolour(TextureAssets.Item[ItemID.FloatingTube].Value, colour);
            }

            return icons[colour];
        }

        public static Texture2D Worn(int colour)
        {
            if (Main.dedServ || colour <= InnerTubeColours.Pink || colour >= InnerTubeColours.Count)
                return null;

            if (worn[colour] == null || worn[colour].IsDisposed)
                worn[colour] = Recolour(WornPink(), colour);

            return worn[colour];
        }

        /// <summary>
        /// The picture with each shade of pink given its step of the colour's ramp: the nearest of the six
        /// (so a resource pack's tube still comes out in the colour), its opacity kept. Null if the
        /// graphics device will not give the picture back.
        /// </summary>
        private static Texture2D Recolour(Texture2D source, int colour)
        {
            if (source == null)
                return null;

            try
            {
                Color[] data = new Color[source.Width * source.Height];
                source.GetData(data);
                Color[] ramp = Ramps[colour];

                for (int i = 0; i < data.Length; i++)
                {
                    Color c = data[i];

                    if (c.A == 0)
                        continue;

                    // The game's pictures are kept premultiplied: the shade is the colour over its opacity.
                    float a = c.A / 255f;
                    Vector3 shade = new Vector3(c.R, c.G, c.B) / (255f * a);
                    int best = 0;
                    float bestDistance = float.MaxValue;

                    for (int k = 0; k < Original.Length; k++)
                    {
                        float d = Vector3.DistanceSquared(shade, Original[k].ToVector3());

                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = k;
                        }
                    }

                    Color to = ramp[best];
                    data[i] = new Color((byte)(to.R * a), (byte)(to.G * a), (byte)(to.B * a), c.A);
                }

                Texture2D made = new Texture2D(Main.graphics.GraphicsDevice, source.Width, source.Height);
                made.SetData(data);
                return made;
            }
            catch
            {
                return null;
            }
        }

        internal static void Unload()
        {
            List<Texture2D> made = new List<Texture2D>();

            for (int i = 0; i < InnerTubeColours.Count; i++)
            {
                if (icons[i] != null)
                    made.Add(icons[i]);

                if (worn[i] != null)
                    made.Add(worn[i]);

                icons[i] = null;
                worn[i] = null;
            }

            wornPink = null;

            if (made.Count > 0)
                Main.QueueMainThreadAction(() => made.ForEach(texture => texture.Dispose()));
        }
    }

    public class InnerTubeSystem : ModSystem
    {
        public override void Unload() => InnerTubeArt.Unload();
    }
}
