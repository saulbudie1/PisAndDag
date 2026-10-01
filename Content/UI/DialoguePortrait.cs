using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// The face of whoever is speaking, in a small frame on the top-left corner of the box
    /// they speak from: vanilla's chat box for a townsperson, the Dialogue panel for the
    /// others.
    ///
    /// A townsperson's face is their map head (TownNPCProfiles, so a shimmered or
    /// variant one is the one shown). A speaker with no map head is given one with
    /// <see cref="Register(int, string, Rectangle?)"/>, or several layers of one canvas with
    /// <see cref="Register(int, string[], Rectangle?)"/> (a face and the hair over it). A face
    /// nobody has put a name to yet is pitch black, a silhouette: a townsperson is always
    /// named, and anyone else is named once their own name has been spoken in a line to the
    /// player (<see cref="Hear"/>), after which the face comes up out of the black and stays
    /// up in that world (DialogueIntroductions).
    ///
    /// The Dialogue panel also writes who is speaking on a tab over its top edge
    /// (<see cref="LabelName"/>, <see cref="LabelTitle"/>): "???" until they are named, then
    /// their name, and their title after it if they have one (<see cref="Name"/>). Someone the
    /// player has a word for but never a name goes by that word instead of "???"
    /// (<see cref="KnownAs"/>): Mors is the Tall Elder, and his face stays black. Someone who
    /// says their name not quite as it is written is named by the way they say it
    /// (<see cref="SpokenAs"/>).
    /// </summary>
    public static class DialoguePortrait
    {
        /// <summary>The frame, square, and the most the face inside it is drawn to.</summary>
        private const int Size = 44;
        private const int Inner = 34;

        /// <summary>
        /// How far the frame stands out past the box's top-left corner, left and up. The
        /// rest of it lies over the corner, short of where the box's text begins (the
        /// Dialogue panel's at 24 in and 18 down, vanilla's at 20 and 20).
        /// </summary>
        private const int PanelOutX = 24;
        private const int PanelOutY = 28;
        private const int ChatOutX = 26;
        private const int ChatOutY = 28;

        /// <summary>vanilla's chat box: its top edge, from Main.GUIChatDrawInner (1.4.4).</summary>
        private const int ChatTop = 100;

        /// <summary>Ticks a face takes to come up out of the black once it is named.</summary>
        private const int RevealTicks = 40;

        /// <summary>The Dialogue panel's slices, TalkPanel.png's 12px ends.</summary>
        private const int SliceEnd = 12;

        /// <summary>Behind the face in the Dialogue panel's frame, so a black one still reads against it: the lore colour, faint.</summary>
        private static readonly Color Window = new Color(196, 180, 152) * 0.22f;

        /// <summary>Vanilla's inventory blue, which its chat box is drawn in too.</summary>
        private static readonly Color ChatBlue = new Color(63, 65, 151, 255) * 0.785f;

        private sealed class Face
        {
            /// <summary>Back to front, all on one canvas: the same frame is cut from each.</summary>
            public string[] Paths;
            public Rectangle? Frame;
            public Asset<Texture2D>[] Textures;
        }

        /// <summary>What the panel's tab says for one speaker. Every part optional.</summary>
        private sealed class Label
        {
            /// <summary>Once named. Null means the NPC's own name.</summary>
            public string Name;

            /// <summary>After the name, once named. Null means the name goes alone.</summary>
            public string Title;

            /// <summary>Before they are named. Null means "???".</summary>
            public string KnownAs;

            /// <summary>Their name as they say it in a line, if it is not quite the NPC's own. Null means the NPC's own.</summary>
            public string Spoken;
        }

        /// <summary>The tab for someone the player cannot put a name to.</summary>
        public const string Unknown = "???";

        private static readonly Dictionary<int, Face> faces = new();
        private static readonly Dictionary<int, Label> labels = new();

        /// <summary>When each type was named this session, for the rise out of the black.</summary>
        private static readonly Dictionary<int, uint> namedAt = new();

        /// <summary>
        /// Gives a speaker with no map head a face: <paramref name="texture"/>, or the part
        /// of it in <paramref name="frame"/>.
        /// </summary>
        public static void Register(int npcType, string texture, Rectangle? frame = null)
        {
            Register(npcType, new[] { texture }, frame);
        }

        /// <summary>
        /// A face drawn from several layers of one canvas, back to front, the same
        /// <paramref name="frame"/> cut from each: a face and the hair over it.
        /// </summary>
        public static void Register(int npcType, string[] layers, Rectangle? frame = null)
        {
            faces[npcType] = new Face { Paths = layers, Frame = frame };
        }

        /// <summary>
        /// What the tab says once this speaker is named: <paramref name="name"/> (null for the
        /// NPC's own), and <paramref name="title"/> after it if given.
        /// </summary>
        public static void Name(int npcType, string name = null, string title = null)
        {
            Label label = LabelOf(npcType);
            label.Name = name;
            label.Title = title;
        }

        /// <summary>What the tab says before this speaker is named, in place of "???".</summary>
        public static void KnownAs(int npcType, string epithet)
        {
            LabelOf(npcType).KnownAs = epithet;
        }

        /// <summary>
        /// Their name as they say it, where that is not the NPC's own name as written: 'Dame' Quilt
        /// says "Dame Quilt". A line with this in it names them (<see cref="Hear"/>).
        /// </summary>
        public static void SpokenAs(int npcType, string spoken)
        {
            LabelOf(npcType).Spoken = spoken;
        }

        private static Label LabelOf(int npcType)
        {
            if (!labels.TryGetValue(npcType, out Label label))
                labels[npcType] = label = new Label();

            return label;
        }

        /// <summary>The first part of the tab: the name, the word the player has for them, or "???".</summary>
        public static string LabelName(NPC npc)
        {
            if (npc == null)
                return Unknown;

            labels.TryGetValue(npc.type, out Label label);

            if (!Named(npc))
                return label?.KnownAs ?? Unknown;

            return label?.Name ?? npc.TypeName;
        }

        /// <summary>The second part of the tab, after the name: their title once named, or null.</summary>
        public static string LabelTitle(NPC npc)
        {
            if (npc == null || !Named(npc) || !labels.TryGetValue(npc.type, out Label label))
                return null;

            return label.Title;
        }

        internal static void Clear()
        {
            faces.Clear();
            labels.Clear();
            namedAt.Clear();
        }

        /// <summary>Whether the player has a name for this one.</summary>
        public static bool Named(NPC npc) => npc.townNPC || DialogueIntroductions.Known.Contains(DialogueIntroductions.Key(npc.type));

        /// <summary>
        /// What the player has been shown of a line so far. If it has the speaker's own
        /// name in it, they have introduced themselves.
        /// </summary>
        internal static void Hear(NPC npc, string shown)
        {
            if (npc == null || Named(npc) || string.IsNullOrEmpty(shown))
                return;

            labels.TryGetValue(npc.type, out Label label);
            string name = label?.Spoken ?? npc.TypeName;

            if (string.IsNullOrEmpty(name) || !shown.Contains(name, StringComparison.Ordinal))
                return;

            DialogueIntroductions.Known.Add(DialogueIntroductions.Key(npc.type));
            namedAt[npc.type] = Main.GameUpdateCount;
        }

        /// <summary>On the Dialogue panel, whose box is <paramref name="box"/>, drawn in its own <paramref name="panel"/>.</summary>
        internal static void DrawOnPanel(SpriteBatch spriteBatch, NPC npc, Rectangle box, Texture2D panel, float alpha)
        {
            if (npc == null || !TryFace(npc, out Texture2D[] layers, out Rectangle frame))
                return;

            Rectangle rect = new Rectangle(box.X - PanelOutX, box.Y - PanelOutY, Size, Size);

            Utils.DrawSplicedPanel(spriteBatch, panel, rect.X, rect.Y, rect.Width, rect.Height,
                SliceEnd, SliceEnd, SliceEnd, SliceEnd, Color.White * alpha);

            Rectangle window = rect;
            window.Inflate(-4, -4);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, window, new Rectangle(0, 0, 1, 1), Window * alpha);

            DrawFace(spriteBatch, npc, layers, frame, rect, alpha);
        }

        /// <summary>
        /// The right edge of the portrait's frame on the Dialogue panel whose box is
        /// <paramref name="box"/>: the name tab starts a little past it.
        /// </summary>
        internal static int PanelFrameRight(Rectangle box) => box.X - PanelOutX + Size;

        /// <summary>On vanilla's chat box, for the townsperson the player is talking to.</summary>
        internal static void DrawOnChat(SpriteBatch spriteBatch, NPC npc)
        {
            if (npc == null || !TryFace(npc, out Texture2D[] layers, out Rectangle frame))
                return;

            int left = Main.screenWidth / 2 - TextureAssets.ChatBack.Width() / 2;
            Rectangle rect = new Rectangle(left - ChatOutX, ChatTop - ChatOutY, Size, Size);

            Utils.DrawInvBG(spriteBatch, rect, ChatBlue);

            DrawFace(spriteBatch, npc, layers, frame, rect, 1f);
        }

        private static void DrawFace(SpriteBatch spriteBatch, NPC npc, Texture2D[] layers, Rectangle frame, Rectangle rect, float alpha)
        {
            // Pixel art: never drawn up, only down if it would not fit.
            float scale = Math.Min(1f, Inner / (float)Math.Max(frame.Width, frame.Height));
            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 at = new Vector2(rect.X + rect.Width * 0.5f, rect.Y + rect.Height * 0.5f);

            // Round to whole pixels at scale 1 so it does not shimmer.
            at = new Vector2((float)Math.Floor(at.X), (float)Math.Floor(at.Y));

            Color tint = Tint(npc) * alpha;

            foreach (Texture2D layer in layers)
                spriteBatch.Draw(layer, at, frame, tint, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>Pitch black until named; then up out of the black, and itself.</summary>
        private static Color Tint(NPC npc)
        {
            if (!Named(npc))
                return Color.Black;

            if (!namedAt.TryGetValue(npc.type, out uint when))
                return Color.White;

            float up = MathHelper.Clamp((Main.GameUpdateCount - when) / (float)RevealTicks, 0f, 1f);

            return Color.Lerp(Color.Black, Color.White, up * up * (3f - 2f * up));
        }

        private static bool TryFace(NPC npc, out Texture2D[] layers, out Rectangle frame)
        {
            layers = null;
            frame = Rectangle.Empty;

            if (faces.TryGetValue(npc.type, out Face face))
            {
                if (face.Textures == null)
                {
                    face.Textures = new Asset<Texture2D>[face.Paths.Length];

                    for (int i = 0; i < face.Paths.Length; i++)
                        face.Textures[i] = ModContent.Request<Texture2D>(face.Paths[i]);
                }

                foreach (Asset<Texture2D> asset in face.Textures)
                {
                    if (!asset.IsLoaded)
                        return false;
                }

                layers = new Texture2D[face.Textures.Length];

                for (int i = 0; i < layers.Length; i++)
                    layers[i] = face.Textures[i].Value;

                frame = face.Frame ?? layers[0].Bounds;
                return true;
            }

            int index = TownNPCProfiles.GetHeadIndexSafe(npc);

            if (index <= 0 || index >= TextureAssets.NpcHead.Length)
                return false;

            Asset<Texture2D> head = TextureAssets.NpcHead[index];

            if (head == null || !head.IsLoaded)
                return false;

            layers = new[] { head.Value };
            frame = head.Value.Bounds;
            return true;
        }
    }

    /// <summary>Who has told the player their name, in this world: kept with it.</summary>
    public class DialogueIntroductions : ModSystem
    {
        private const string SaveKey = "PADIntroduced";

        public static readonly HashSet<string> Known = new();

        /// <summary>A name for an NPC type that survives a reload: its number for vanilla's, its full name for a mod's.</summary>
        public static string Key(int npcType)
        {
            if (npcType < NPCID.Count)
                return npcType.ToString();

            ModNPC modNPC = NPCLoader.GetNPC(npcType);

            return modNPC != null ? modNPC.FullName : npcType.ToString();
        }

        public override void ClearWorld() => Known.Clear();

        public override void SaveWorldData(TagCompound tag)
        {
            if (Known.Count > 0)
                tag[SaveKey] = new List<string>(Known);
        }

        public override void LoadWorldData(TagCompound tag)
        {
            Known.Clear();

            if (tag.ContainsKey(SaveKey))
                Known.UnionWith(tag.GetList<string>(SaveKey));
        }
    }
}
