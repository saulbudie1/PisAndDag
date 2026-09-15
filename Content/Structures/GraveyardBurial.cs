using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Structures
{
    /// <summary>
    /// Counts every death in the world. Player.KillMe is what the death packet
    /// calls on the server and on every client, and tModLoader's Kill hook
    /// sits inside it — so each copy of the count moves together with no
    /// packet of its own. Saved with the world by GraveyardZone.
    ///
    /// Also says one line, once: after the world's first death, the player
    /// who came back is told that something should have noticed (Lore Bible
    /// §V — respawning is the symptom, not the gift). It means nothing on
    /// day one and everything after the wanderer.
    /// </summary>
    public class GraveyardDeathCount : ModPlayer
    {
        private const string FirstRespawnLine = "Something should have remembered that.";

        /// <summary>PADGlobalItem's LoreColour: the mod's voice, not a system message.</summary>
        private static readonly Color LoreColour = new Color(196, 180, 152);

        /// <summary>Ticks between standing up again and the line. Lets the screen settle first.</summary>
        private const int LineDelay = 90;

        private int lineIn;

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            GraveyardZone.Deaths++;
        }

        /// <summary>
        /// OnRespawn fires inside Player.Spawn, only when the player was dead —
        /// on the owner's client, on the server, and on every other client
        /// that hears the spawn packet. Only the one who died gets the line.
        /// </summary>
        public override void OnRespawn()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer)
                return;

            if (GraveyardZone.Deaths == 1)
                lineIn = LineDelay;
        }

        public override void PostUpdate()
        {
            if (lineIn <= 0)
                return;

            if (--lineIn == 0)
                Main.NewText(FirstRespawnLine, LoreColour);
        }
    }


    /// <summary>
    /// Your dead are buried here.
    ///
    /// Vanilla drops a tombstone where you fall. The first death in the world
    /// is buried in the graveyard instead — the open grave is filled and the
    /// stone set on it. Every death after that is vanilla, and the count
    /// keeps going.
    ///
    /// Player.DropTombstone runs in singleplayer and on the server, never on a
    /// client — so this runs where the count is authoritative. The stone is a
    /// projectile (aiStyle 17) that plants itself where it comes to rest, and
    /// only its owner's client does the planting; moving the projectile at
    /// spawn is all that is needed, the rest is vanilla.
    /// </summary>
    public class GraveyardBurial : GlobalProjectile
    {
        /// <summary>Deaths that get a plot. The counter is not capped; this is.</summary>
        public const int Buried = 1;

        /// <summary>
        /// Vanilla's tombstone projectiles: Tombstone, the five markers, the
        /// five golden ones. Listed rather than read from a set because the
        /// set that names them was added after 1.4.4.
        /// </summary>
        private static readonly bool[] Gravestones = ProjectileID.Sets.Factory.CreateBoolSet(
            43, 201, 202, 203, 204, 205, 527, 528, 529, 530, 531);

        /// <summary>How many tiles above its plot the stone is dropped from.</summary>
        private const int DropHeight = 4;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type >= 0 && entity.type < Gravestones.Length && Gravestones[entity.type];
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (!GraveyardZone.HasGraves || GraveyardZone.Deaths > Buried)
                return;

            // A stone that is not a death's. Kill has already run for this
            // death (it sits before DropTombstone in KillMe), so the count
            // above includes it, and the owner is already dead.
            if (!Main.player.IndexInRange(projectile.owner) || !Main.player[projectile.owner].dead)
                return;

            if (!FindPlot(out int x, out int y))
                return;

            // Centred on the column, a few tiles up, no sideways travel: it
            // falls straight onto its plot and plants there.
            projectile.position = new Vector2(x * 16 + 8 - projectile.width / 2f, (y - DropHeight) * 16 - projectile.height);
            projectile.velocity = Vector2.Zero;
            projectile.netUpdate = true;
        }

        /// <summary>
        /// The open grave while it is still open; if someone filled it by
        /// hand, any level pair of columns on the crown with two clear
        /// tiles above it.
        /// </summary>
        private static bool FindPlot(out int x, out int y)
        {
            x = -1;
            y = -1;

            if (GraveyardZone.OpenGraveX >= 0)
            {
                int ox = GraveyardZone.OpenGraveX;
                int oy = GraveyardZone.OpenGraveY;

                // Still a hole? Someone may have filled it by hand.
                if (!Main.tile[ox, oy].HasTile && !Main.tile[ox + 1, oy].HasTile)
                {
                    FillOpenGrave(ox, oy);
                    x = ox;
                    y = oy;
                }

                // Either way it is spent.
                GraveyardZone.OpenGraveX = -1;
                GraveyardZone.OpenGraveY = -1;

                if (x >= 0)
                    return true;
            }

            List<int> spots = new List<int>();

            for (int column = GraveyardZone.GraveLeft; column < GraveyardZone.GraveRight; column++)
            {
                if (LevelPair(column, out _))
                    spots.Add(column);
            }

            if (spots.Count == 0)
                return false;

            x = spots[Main.rand.Next(spots.Count)];
            LevelPair(x, out y);

            return true;
        }

        /// <summary>
        /// Two columns whose ground is the same height, square (a sloped
        /// riser will not anchor a stone), with nothing in the two tiles
        /// above either — which also keeps it off cobwebs, statues and the
        /// stones already there.
        /// </summary>
        private static bool LevelPair(int x, out int y)
        {
            y = Ground(x);

            if (y < 0 || Ground(x + 1) != y)
                return false;

            for (int dx = 0; dx < 2; dx++)
            {
                Tile ground = Main.tile[x + dx, y];

                if (ground.Slope != SlopeType.Solid || ground.IsHalfBlock)
                    return false;

                for (int dy = 1; dy <= 2; dy++)
                {
                    if (Main.tile[x + dx, y - dy].HasTile)
                        return false;
                }
            }

            return true;
        }

        /// <summary>First solid tile down from just above the crown, or -1.</summary>
        private static int Ground(int x)
        {
            if (x < 1 || x >= Main.maxTilesX - 1)
                return -1;

            int from = System.Math.Max(1, GraveyardZone.CrownY - 6);
            int to = System.Math.Min(Main.maxTilesY - 2, GraveyardZone.CrownY + 40);

            for (int y = from; y < to; y++)
            {
                Tile tile = Main.tile[x, y];

                if (tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated)
                    return y;
            }

            return -1;
        }

        /// <summary>
        /// Closes the hole: the three rows of the pit packed with whatever the
        /// hill is made of under it, skinned with whatever the lip beside it
        /// wears, so the fresh grave matches the ground either side. The
        /// dirt wall the pit was dug with stays behind the fill.
        /// </summary>
        private static void FillOpenGrave(int x, int top)
        {
            Tile under = Main.tile[x, top + 3];
            ushort fill = under.HasTile && Main.tileSolid[under.TileType] ? under.TileType : TileID.Dirt;

            Tile lip = Main.tile[x - 1, top];
            ushort cap = lip.HasTile && Main.tileSolid[lip.TileType] ? lip.TileType : fill;

            for (int dx = 0; dx < 2; dx++)
            {
                for (int dy = 0; dy < 3; dy++)
                {
                    Tile tile = Main.tile[x + dx, top + dy];

                    tile.HasTile = true;
                    tile.TileType = dy == 0 ? cap : fill;
                    tile.Slope = SlopeType.Solid;
                    tile.IsHalfBlock = false;
                    tile.LiquidAmount = 0;
                }
            }

            for (int dx = -1; dx <= 2; dx++)
                for (int dy = -1; dy <= 3; dy++)
                    WorldGen.SquareTileFrame(x + dx, top + dy);

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, x - 1, top - 1, 4, 5);
        }
    }


    /// <summary>
    /// The quiet, for the ears. Inside the zone the ambient volume — wind and
    /// rain, the only things vanilla routes through it — is held at zero, and
    /// given back the moment the player steps out.
    ///
    /// ambientVolume is a SETTING, which is the hazard: it is written to the
    /// config file by Main.SaveSettings, and a zero saved there would follow
    /// the player into every world they own. So it is put back around every
    /// save (the hook below), on unload, on save-and-quit, and whenever the
    /// options window is open so the slider shows the truth and any change
    /// the player makes there is the value kept.
    /// </summary>
    public class GraveyardSilence : ModSystem
    {
        private static bool muted;
        private static float saved;

        public override void Load()
        {
            if (!Main.dedServ)
                On_Main.SaveSettings += RestoreAroundSave;
        }

        private static bool RestoreAroundSave(On_Main.orig_SaveSettings orig)
        {
            bool was = muted;

            Restore();
            bool result = orig();

            if (was)
                Mute();

            return result;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            bool inside = !Main.gameMenu
                && !Main.ingameOptionsWindow
                && GraveyardZone.Contains(Main.LocalPlayer);

            if (inside)
                Mute();
            else
                Restore();
        }

        private static void Mute()
        {
            if (!muted)
            {
                saved = Main.ambientVolume;
                muted = true;
            }

            Main.ambientVolume = 0f;
        }

        private static void Restore()
        {
            if (!muted)
                return;

            Main.ambientVolume = saved;
            muted = false;
        }

        public override void OnWorldUnload() => Restore();

        public override void PreSaveAndQuit() => Restore();

        public override void Unload() => Restore();
    }
}
