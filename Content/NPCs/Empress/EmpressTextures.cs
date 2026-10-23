using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// Her pictures, the mod's own copies of them (by request: "Port all the textures of empress into a folder so i will
    /// edit it later at PisAndDag\Content\NPCs\Legends\Empress"), put in the game's place while the mod is loaded, so
    /// whatever is painted over them is what is seen, wherever she is drawn: by the game, by her scenes, on the map, in
    /// the bestiary. Given back to the game when the mod goes.
    ///
    ///     Body            her body, both halves (NPC 636)              Streamers   her second half's streamers (Extra 187)
    ///     WingsBack       her wings (Extra 159)                        Skirt       her second half's skirt of light (Extra 188)
    ///     WingsGlow       their glow, through the game's shader (157)  Head        her head on the map (NPC_Head_Boss_37)
    ///     ArmLeft         her left arm, seven frames (Extra 158)       Lacewing    the Prismatic Lacewing (NPC 661)
    ///     ArmRight        her right (Extra 160)                        LanceLine   the line before a lance (Extra 178)
    ///     Lance, RainbowStreak, LastingRainbow, SplitShotCore, DeathAurora, SunDance: her shots (Projectile 919, 873, 872,
    ///     871, 874, 923)
    ///
    /// And three of the mod's own for her second half in Legends: Bow (her bow of light, the game's Eventide enlarged two
    /// and a half times, drawn at 0.6 of that in the fight, its string taken off: the string is drawn as light), Arrow (her arrows of light, Daybreak's
    /// shape, in greys: tinted as they fly), and Veil (where on her body the veil's moving streaks may take her away, painted
    /// over her body's own two frames: all of her below her face, as it comes; EmpressVeil).
    ///
    /// Each is made premultiplied as it loads (each colour times its own alpha, as the game keeps its own pictures and
    /// as its alpha blend takes them): a mod's pictures are packed as painted, and without this anything soft painted
    /// here (a glow, a fading edge, the game's own soft shots copied in) would be drawn too bright, its gradient lost.
    /// So paint them as any picture is painted.
    /// </summary>
    public class EmpressTextures : ModSystem
    {
        private const string Folder = "PisAndDag/Content/NPCs/Legends/Empress/";

        public static Asset<Texture2D> Bow;
        public static Asset<Texture2D> Arrow;
        public static Asset<Texture2D> Veil;

        /// <summary>The game's own, put back as they were.</summary>
        private static readonly List<(Asset<Texture2D>[] Array, int Index, Asset<Texture2D> Game)> taken = new();

        /// <summary>The mod's own already made premultiplied (Premultiply), each once.</summary>
        private static readonly HashSet<Texture2D> premultiplied = new();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            Bow = Mine("Bow");
            Arrow = Mine("Arrow");
            Veil = Mine("Veil");

            Put(TextureAssets.Npc, NPCID.HallowBoss, "Body");
            Put(TextureAssets.Npc, NPCID.EmpressButterfly, "Lacewing");
            Put(TextureAssets.Extra, ExtrasID.HallowBossWings, "WingsGlow");
            Put(TextureAssets.Extra, ExtrasID.HallowBossArmsLeft, "ArmLeft");
            Put(TextureAssets.Extra, ExtrasID.HallowBossWingsBack, "WingsBack");
            Put(TextureAssets.Extra, ExtrasID.HallowBossArmsRight, "ArmRight");
            Put(TextureAssets.Extra, 178, "LanceLine");
            Put(TextureAssets.Extra, ExtrasID.HallowBossTentacles, "Streamers");
            Put(TextureAssets.Extra, ExtrasID.HallowBossSkirt, "Skirt");
            Put(TextureAssets.NpcHeadBoss, 37, "Head");
            Put(TextureAssets.Projectile, ProjectileID.HallowBossSplitShotCore, "SplitShotCore");
            Put(TextureAssets.Projectile, ProjectileID.HallowBossLastingRainbow, "LastingRainbow");
            Put(TextureAssets.Projectile, ProjectileID.HallowBossRainbowStreak, "RainbowStreak");
            Put(TextureAssets.Projectile, ProjectileID.HallowBossDeathAurora, "DeathAurora");
            Put(TextureAssets.Projectile, ProjectileID.FairyQueenLance, "Lance");
            Put(TextureAssets.Projectile, ProjectileID.FairyQueenSunDance, "SunDance");
        }

        public override void Unload()
        {
            foreach ((Asset<Texture2D>[] array, int index, Asset<Texture2D> game) in taken)
            {
                if (array != null && index >= 0 && index < array.Length)
                    array[index] = game;
            }

            taken.Clear();
            premultiplied.Clear();
            Bow = Arrow = Veil = null;
        }

        /// <summary>
        /// One of the mod's own pictures of her, loaded now (the game asks for a picture by name only when it is not), and
        /// made premultiplied on the main thread straight after.
        /// </summary>
        private static Asset<Texture2D> Mine(string name)
        {
            Asset<Texture2D> asset = ModContent.Request<Texture2D>(Folder + name, AssetRequestMode.ImmediateLoad);
            Main.QueueMainThreadAction(() => Premultiply(asset));
            return asset;
        }

        /// <summary>
        /// The picture as the game keeps its own: each colour times its own alpha. The game's reader does this to its
        /// pictures as it reads them; a mod's are packed and read as painted. Only the soft pixels change (wholly opaque
        /// and wholly clear ones are the same either way), once a picture.
        /// </summary>
        private static void Premultiply(Asset<Texture2D> asset)
        {
            if (asset == null || !asset.IsLoaded)
                return;

            Texture2D texture = asset.Value;

            if (texture == null || texture.IsDisposed || !premultiplied.Add(texture))
                return;

            try
            {
                Color[] data = new Color[texture.Width * texture.Height];
                texture.GetData(data);
                bool changed = false;

                for (int i = 0; i < data.Length; i++)
                {
                    Color c = data[i];

                    if (c.A == 255)
                        continue;

                    Color made = new Color(c.R * c.A / 255, c.G * c.A / 255, c.B * c.A / 255, (int)c.A);

                    if (made != c)
                    {
                        data[i] = made;
                        changed = true;
                    }
                }

                if (changed)
                    texture.SetData(data);
            }
            catch
            {
                // A device that will not read a picture back: drawn as it was loaded.
            }
        }

        /// <summary>The mod's copy in the game's place, the game's kept to be given back.</summary>
        private static void Put(Asset<Texture2D>[] array, int index, string name)
        {
            if (array == null || index < 0 || index >= array.Length)
                return;

            taken.Add((array, index, array[index]));
            array[index] = Mine(name);
        }
    }
}
