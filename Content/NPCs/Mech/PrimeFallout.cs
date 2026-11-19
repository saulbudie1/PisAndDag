using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Mech
{
    /// <summary>
    /// The ground Skeletron Prime went up over, left radioactive (by request: "an area that is radio active for 5 mins"):
    /// everything within thirty tiles of where its mouth was (as far as its blast hurt), for five minutes. Whatever
    /// stands in it is Radioactive for as long as it does and a second after: players (each on their own machine), and
    /// enemies and creatures (the server's). Not the townsfolk, nor anything else friendly, nor Skeletron Prime itself
    /// and its arms, should it be called again.
    ///
    /// Seen: its green lighting the ground and the air of it, faintly, pulsing; motes of it rising everywhere in it; a
    /// thin haze; and heard, by a player in or near it, as a counter's clicks, quicker the nearer its middle
    /// (PrimeGeiger, made in code). All of it thinning over its last ten seconds.
    ///
    /// The world's, not a player's: kept with the world (so it is still there, with what is left of its five minutes,
    /// if the world is left and come back to), and told to every machine.
    ///
    /// Legends (by request): smaller patches of it, for thirty seconds (asked after), where one of its cannon's nukes
    /// went up ("leaving a radioactive area radius 10 tiles") and along the way its head launched itself ("causing a
    /// path of toxic radioactivity"): each its own reach and its own time, seen and felt the same.
    /// </summary>
    public class PrimeFallout : ModSystem
    {
        /// <summary>How long it lasts (by request: five minutes), and how far from its middle it reaches.</summary>
        public const int Lasts = 5 * 60 * 60;
        public const float Reach = Prime.BlastReach;

        /// <summary>How long one of its fight's own lasts (asked after: thirty seconds).</summary>
        public const int ShortLasts = 30 * 60;

        /// <summary>Its last ticks, over which it thins away (a short one's: its last third).</summary>
        private const int Thins = 600;

        /// <summary>The most there can be at once (the oldest goes).</summary>
        private const int Most = 48;

        public static readonly SoundStyle GeigerSound = new("PisAndDag/Content/Sounds/Mech/PrimeGeiger")
        {
            Volume = 0.45f,
            PitchVariance = 0.25f,
            MaxInstances = 8,
        };

        private struct Zone
        {
            public Vector2 At;
            public int Left;

            /// <summary>How far from its middle it reaches, and the ticks it thins away over.</summary>
            public float Reach;
            public int Thin;
        }

        private static readonly List<Zone> zones = new();

        /// <summary>A place is left radioactive from now, as its end leaves it. The server's (or a single player's).</summary>
        public static void Add(Vector2 at) => Add(at, Reach, Lasts);

        /// <summary>
        /// A place is left radioactive from now, this far from its middle, for this long. The server's (or a single
        /// player's). <paramref name="tell"/>: every machine is told at once (not, for the first of several laid in one
        /// tick: the last of them tells).
        /// </summary>
        public static void Add(Vector2 at, float reach, int lasts, bool tell = true)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            zones.Add(new Zone { At = at, Left = lasts, Reach = Math.Max(16f, reach), Thin = Math.Max(1, Math.Min(Thins, lasts / 3)) });

            while (zones.Count > Most)
                zones.RemoveAt(0);

            if (tell && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>Whether a box is in radioactive ground, and how deep (0 at its edge to 1 at its middle).</summary>
        public static bool Inside(Rectangle box, out float depth)
        {
            depth = 0f;
            bool inside = false;

            foreach (Zone zone in zones)
            {
                Vector2 near = Vector2.Clamp(zone.At, box.TopLeft(), box.BottomRight());
                float d = Vector2.Distance(near, zone.At);

                if (d > zone.Reach)
                    continue;

                inside = true;
                depth = Math.Max(depth, 1f - d / zone.Reach);
            }

            return inside;
        }

        public override void ClearWorld() => zones.Clear();

        public override void SaveWorldData(TagCompound tag)
        {
            if (zones.Count == 0)
                return;

            List<TagCompound> kept = new();

            foreach (Zone zone in zones)
                kept.Add(new TagCompound { ["x"] = zone.At.X, ["y"] = zone.At.Y, ["left"] = zone.Left, ["reach"] = zone.Reach, ["thin"] = zone.Thin });

            tag["primeFallout"] = kept;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            zones.Clear();

            if (!tag.ContainsKey("primeFallout"))
                return;

            foreach (TagCompound kept in tag.GetList<TagCompound>("primeFallout"))
            {
                int left = kept.GetInt("left");

                // One kept before they had a reach of their own is its end's.
                float reach = kept.ContainsKey("reach") ? kept.GetFloat("reach") : Reach;
                int thin = kept.ContainsKey("thin") ? kept.GetInt("thin") : Thins;

                if (left > 0 && zones.Count < Most)
                    zones.Add(new Zone { At = new Vector2(kept.GetFloat("x"), kept.GetFloat("y")), Left = Math.Min(left, Lasts), Reach = Math.Max(16f, reach), Thin = Math.Max(1, thin) });
            }
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write((byte)zones.Count);

            foreach (Zone zone in zones)
            {
                writer.Write(zone.At.X);
                writer.Write(zone.At.Y);
                writer.Write(zone.Left);
                writer.Write(zone.Reach);
                writer.Write((short)Math.Min(zone.Thin, short.MaxValue));
            }
        }

        public override void NetReceive(BinaryReader reader)
        {
            zones.Clear();
            int many = reader.ReadByte();

            for (int i = 0; i < many; i++)
            {
                Vector2 at = new(reader.ReadSingle(), reader.ReadSingle());
                int left = reader.ReadInt32();
                float reach = reader.ReadSingle();
                int thin = reader.ReadInt16();

                if (left > 0 && zones.Count < Most)
                    zones.Add(new Zone { At = at, Left = left, Reach = Math.Max(16f, reach), Thin = Math.Max(1, thin) });
            }
        }

        /// <summary>Every tick, on every machine: each counts down; what stands in one is made Radioactive; and it is seen and heard.</summary>
        public override void PostUpdateEverything()
        {
            if (zones.Count == 0)
                return;

            for (int i = zones.Count - 1; i >= 0; i--)
            {
                Zone zone = zones[i];
                zone.Left--;

                if (zone.Left <= 0)
                    zones.RemoveAt(i);
                else
                    zones[i] = zone;
            }

            if (zones.Count == 0)
                return;

            // Enemies and creatures in it: the server's (or a single player's), a few times a second.
            if (Main.netMode != NetmodeID.MultiplayerClient && Main.GameUpdateCount % 10 == 0)
                Sicken();

            if (Main.dedServ)
                return;

            // This machine's player.
            Player me = Main.LocalPlayer;
            float depth = 0f;
            bool inside = me.active && !me.dead && Inside(me.Hitbox, out depth);

            if (inside)
                me.AddBuff(ModContent.BuffType<Radioactive>(), Radioactive.Lingers);

            if (Main.gamePaused)
                return;

            foreach (Zone zone in zones)
                Show(zone);

            Click(me, inside, depth);
        }

        private static void Sicken()
        {
            int buff = ModContent.BuffType<Radioactive>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                // Not the friendly, not Skeletron Prime and its arms, and nothing that cannot be hurt just now (a boss in a
                // scene of its own: it would die of it, be refused, and die again).
                if (!npc.active || npc.friendly || npc.townNPC || npc.dontTakeDamage || npc.immortal
                    || npc.type == NPCID.SkeletronPrime || Prime.IsArm(npc.type) || npc.type == NPCID.TargetDummy)
                    continue;

                if (Inside(npc.Hitbox, out _))
                    npc.AddBuff(buff, Radioactive.Lingers);
            }
        }

        /// <summary>One of them, seen: its green on the ground and air of it where the screen is, motes of it rising, a thin haze.</summary>
        private static void Show(Zone zone)
        {
            float there = MathHelper.Clamp(zone.Left / (float)zone.Thin, 0f, 1f);
            float Reach = zone.Reach;

            // A small one's share of its end's ground: as much less of it rises off it (never none).
            float share = MathHelper.Clamp(Reach * Reach / (PrimeFallout.Reach * PrimeFallout.Reach), 0f, 1f);
            Vector2 screen = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float span = Reach + new Vector2(Main.screenWidth, Main.screenHeight).Length() * 0.5f + 100f;

            if (Vector2.DistanceSquared(screen, zone.At) > span * span)
                return;

            // Its light: a lamp every four tiles of it that is on screen, all pulsing together, slowly.
            float pulse = (0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f)) * there;
            int step = Reach < 200f ? 2 : 4;
            int reach = Math.Max(1, (int)(Reach / 16f));
            int cx = (int)(zone.At.X / 16f), cy = (int)(zone.At.Y / 16f);
            int left = Math.Max(cx - reach, (int)(Main.screenPosition.X / 16f) - step);
            int right = Math.Min(cx + reach, (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + step);
            int top = Math.Max(cy - reach, (int)(Main.screenPosition.Y / 16f) - step);
            int bottom = Math.Min(cy + reach, (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + step);

            // On a grid that does not move with the screen.
            left -= ((left % step) + step) % step;
            top -= ((top % step) + step) % step;

            for (int x = left; x <= right; x += step)
            {
                for (int y = top; y <= bottom; y += step)
                {
                    float dx = x - cx, dy = y - cy;
                    float far = (dx * dx + dy * dy) / (reach * (float)reach);

                    if (far > 1f || !WorldGen.InWorld(x, y, 2))
                        continue;

                    // A little brighter toward its middle.
                    float k = pulse * (0.2f + 0.14f * (1f - far));
                    Lighting.AddLight(x, y, 0.73f * k, k, 0.02f * k);
                }
            }

            // Motes of it rising, and a thin haze, wherever in it the screen is.
            float room = MathHelper.Clamp(PADParticleHandler.FreeSlots / 400f, 0.2f, 1f);

            float motes = Math.Max(0.4f, 3f * share);

            for (int n = 0; n < 3; n++)
            {
                if (Main.rand.NextFloat() > there * room * motes / 3f)
                    continue;

                Vector2 at = zone.At + Main.rand.NextVector2Circular(Reach, Reach);

                if (!Prime.Seen(at, 40f))
                    continue;

                Prime.Add(new HoneyMote(at, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(0.2f, 0.9f)), Main.rand.NextBool(4) ? PrimeArt.NuclearHot : PrimeArt.Nuclear,
                    Main.rand.NextFloat(0.3f, 0.55f), Main.rand.Next(50, 100), 0.008f));
            }

            if (Main.rand.NextFloat() < Math.Max(0.07f, 0.35f * share) * there * room)
            {
                Vector2 at = zone.At + Main.rand.NextVector2Circular(Reach, Reach);
                float big = MathHelper.Clamp(Reach / 240f, 0.5f, 1f);

                if (Prime.Seen(at, 80f))
                    Prime.Add(new SteamPuff(at, new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.05f, 0.3f)), new Color(150, 204, 40),
                        Main.rand.Next(110, 190), Main.rand.NextFloat(30f, 50f) * big, Main.rand.NextFloat(80f, 130f) * big, Main.rand.NextFloat(0.14f, 0.24f)));
            }
        }

        /// <summary>A counter's clicks for this machine's player: in it, quicker the nearer its middle; a few just outside it.</summary>
        private static void Click(Player me, bool inside, float depth)
        {
            if (!me.active || me.dead)
                return;

            float rate;

            if (inside)
            {
                rate = 0.07f + 0.4f * depth;
            }
            else
            {
                // Near its edge: a click now and then, a warning.
                float least = float.MaxValue;

                foreach (Zone zone in zones)
                    least = Math.Min(least, Vector2.Distance(me.Center, zone.At) - zone.Reach);

                if (least > 400f)
                    return;

                rate = 0.05f * (1f - least / 400f);
            }

            if (Main.rand.NextFloat() < rate)
                SoundEngine.PlaySound(GeigerSound with { Volume = inside ? 0.45f : 0.3f }, me.Center + Main.rand.NextVector2Circular(40f, 40f));
        }
    }
}
