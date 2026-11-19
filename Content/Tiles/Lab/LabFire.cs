using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.Tiles.Lab
{
    /// <summary>
    /// A burner in the floor of the incineration pit: a block of iron grate over a glow, and over it, while there is room
    /// over it, its part of the pit's fire (by request: "incineration pit should have a big fire that anything item
    /// dropped down should disappear"). Gives a great deal of light. Laid by the lab (LabPlan), never by hand; not broken.
    /// </summary>
    public class LabBurner : ModTile
    {
        /// <summary>How high its flames go at most (pixels), and the fire's colours, hottest first.</summary>
        private const int Highest = 88;
        private static readonly Color[] Flame = { new(255, 244, 196), new(255, 206, 92), new(250, 140, 44), new(214, 70, 30), new(130, 30, 22) };

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileLighted[Type] = true;
            Main.tileMergeDirt[Type] = false;

            LabFurnishing.TypeOf[LabPlan.LabDecor.Burner] = Type;

            DustType = DustID.Torch;
            HitSound = SoundID.Tink;
            AddMapEntry(new Color(230, 110, 40), CreateMapEntryName());
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            yield break;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            float flicker = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 1.7f);
            r = 1.1f * flicker;
            g = 0.55f * flicker;
            b = 0.16f * flicker;
        }

        /// <summary>
        /// Its flames, if the tile over it is open: a column of fire every two pixels across it, each its own height in its
        /// own time, white-yellow at its foot to deep red at its tip; drawn as light (adding to what is behind); and the
        /// light of them in the tiles over it.
        /// </summary>
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (WorldGen.SolidTile(i, j - 1))
                return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 offScreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            int left = i * 16 - (int)Main.screenPosition.X + (int)offScreen.X;
            int bottom = j * 16 - (int)Main.screenPosition.Y + (int)offScreen.Y;
            float t = Main.GlobalTimeWrappedHourly;

            for (int k = 0; k < 8; k++)
            {
                float x = i * 8 + k;
                float lick = 0.5f + 0.25f * MathF.Sin(t * 7.3f + x * 0.9f) + 0.25f * MathF.Sin(t * 4.1f - x * 0.37f + 2f);
                int high = (int)(Highest * (0.35f + 0.65f * lick)) / 2 * 2;

                for (int band = 0; band < Flame.Length; band++)
                {
                    int from = high * band / Flame.Length / 2 * 2, to = high * (band + 1) / Flame.Length / 2 * 2;

                    if (to <= from)
                        continue;

                    Color colour = Flame[band];
                    Color add = new Color(colour.R, colour.G, colour.B, 0) * (0.9f - band * 0.12f);
                    spriteBatch.Draw(pixel, new Rectangle(left + k * 2, bottom - to, 2, to - from), add);
                }
            }

            for (int up = 1; up <= 5; up++)
                if (WorldGen.InWorld(i, j - up))
                    Lighting.AddLight(i, j - up, 0.9f / up, 0.45f / up, 0.12f / up);

            if (!Main.gamePaused && Main.rand.NextBool(40))
            {
                Dust ember = Dust.NewDustDirect(new Vector2(i * 16, j * 16 - 40), 16, 30, DustID.Torch, 0f, -2.5f, 0, default, 1.3f);
                ember.noGravity = true;
            }
        }
    }

    /// <summary>
    /// What the pit's fire does (LabPlan.Fire): any item in it burns away; anything alive in it burns, a player badly (on
    /// fire, and hurt every third of a second), a Culture to death (by request: "Burns both").
    /// </summary>
    public class LabFireSystem : ModSystem
    {
        /// <summary>How hard it burns a player, how often; how hard an NPC.</summary>
        public const int PlayerBurn = 40, Every = 20, NpcBurn = 70;

        private static readonly int[] lastBurnt = new int[Main.maxNPCs];

        public static bool InLab => SubworldKeeper.Current is AbandonedLab;

        public static Rectangle Zone
        {
            get
            {
                var f = LabPlan.Fire;
                return new Rectangle(f.X0 * 16, f.Y0 * 16, (f.X1 - f.X0 + 1) * 16, (f.Y1 - f.Y0 + 1) * 16);
            }
        }

        public override void PostUpdateItems()
        {
            if (!InLab || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Rectangle zone = Zone;

            for (int k = 0; k < Main.maxItems; k++)
            {
                Item item = Main.item[k];

                if (!item.active || item.IsAir || !item.Hitbox.Intersects(zone))
                    continue;

                if (!Main.dedServ)
                {
                    SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = -0.2f }, item.Center);

                    for (int d = 0; d < 8; d++)
                        Dust.NewDust(item.position, item.width, item.height, d % 2 == 0 ? DustID.Torch : DustID.Smoke, 0f, -2f);
                }

                item.TurnToAir();
                item.active = false;

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, k);
            }
        }

        public override void PostUpdateNPCs()
        {
            if (!InLab || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Rectangle zone = Zone;
            int car = ModContent.NPCType<LabElevator>();

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (npc.friendly || npc.type == car || npc.dontTakeDamage || !npc.Hitbox.Intersects(zone))
                    continue;

                if (Main.GameUpdateCount - lastBurnt[npc.whoAmI] < 10)
                    continue;

                lastBurnt[npc.whoAmI] = (int)Main.GameUpdateCount;
                npc.AddBuff(BuffID.OnFire3, 180);
                npc.SimpleStrikeNPC(NpcBurn, 0, false, 0f, null, false, 0f, true);

                for (int d = 0; d < 6; d++)
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Torch, 0f, -2f, 0, default, 1.4f);
            }
        }
    }

    /// <summary>
    /// A player in the lab: where they wake after dying there, if they have lain in a bunk (LabBunk); no fall in the
    /// elevator's shaft hurts them; in the pit's fire, they burn.
    /// </summary>
    public class LabPlayer : ModPlayer
    {
        /// <summary>The bunk they lay in: its top left tile; none, nothing.</summary>
        public Point Bunk;

        private int burnt;

        public override void OnRespawn()
        {
            if (!LabFireSystem.InLab || Bunk == Point.Zero || !WorldGen.InWorld(Bunk.X, Bunk.Y))
                return;

            Tile tile = Main.tile[Bunk.X, Bunk.Y];

            if (!tile.HasTile || tile.TileType != ModContent.TileType<LabBunk>())
                return;

            int wide = LabPlan.LabDecor.Size[LabPlan.LabDecor.Bunk].Wide, tall = LabPlan.LabDecor.Size[LabPlan.LabDecor.Bunk].Tall;
            Player.position = new Vector2((Bunk.X + wide * 0.5f) * 16f - Player.width * 0.5f, (Bunk.Y + tall) * 16f - Player.height);
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        public override void PostUpdateEquips()
        {
            if (!LabFireSystem.InLab)
                return;

            // The elevator's shaft: a fall down it hurts nobody.
            float x = Player.Center.X / 16f, y = Player.Center.Y / 16f;

            if (x >= LabPlan.ElevatorLeft - 1 && x <= LabPlan.ElevatorLeft + LabPlan.ElevatorWide + 1 && y >= LabPlan.ElevatorTop - 12 && y <= LabPlan.ElevatorBottom + 2)
                Player.noFallDmg = true;
        }

        public override void PostUpdate()
        {
            if (!LabFireSystem.InLab || Player.dead || !Player.Hitbox.Intersects(LabFireSystem.Zone))
                return;

            Player.AddBuff(BuffID.OnFire3, 240);

            if (++burnt < LabFireSystem.Every)
                return;

            burnt = 0;

            if (Player.whoAmI == Main.myPlayer)
                Player.Hurt(PlayerDeathReason.ByCustomReason(NetworkText.FromKey("Mods.PisAndDag.Subworlds.Incinerated", Player.name)), LabFireSystem.PlayerBurn, 0);
        }
    }
}
