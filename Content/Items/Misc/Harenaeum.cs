using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Items.Misc
{
    /// <summary>
    /// Harenaeum, the desert's metal, by request ("Add an ore named Harenaeum Ore, generated on harden sandstone
    /// tiles in underground desert, that is used to make Harenaeum Bars in pre hardmode", "can be picked up with 65
    /// pick power"): sun-gold, flecked with turquoise, in the underground desert's hardened sand and sandstone. Its
    /// colours, and the sand everything made of it throws about, kept here so the whole set looks the same.
    ///
    /// What is made of it (PADWorkFlow §17.20): the yoyo Pocket Sandstorm and the fist Corosion; the summons Scorpius
    /// Servus and Jelle's Sable Helper; the bow Sidewinder and the revolver The Last Grain; the seal Clay Turbah and
    /// the incantation Mini Sandstorm.
    /// </summary>
    public static class Harenaeum
    {
        /// <summary>The metal, darkest to brightest.</summary>
        public static readonly Color Bronze = new Color(112, 68, 30);
        public static readonly Color Gold = new Color(214, 158, 62);
        public static readonly Color PaleGold = new Color(248, 212, 124);
        public static readonly Color Shine = new Color(255, 246, 206);

        /// <summary>Its flecks.</summary>
        public static readonly Color Turquoise = new Color(64, 190, 178);
        public static readonly Color PaleTurquoise = new Color(150, 236, 220);

        /// <summary>Sand, darkest to palest: what its weapons throw.</summary>
        public static readonly Color[] Sand =
        {
            new Color(158, 112, 58), new Color(204, 160, 92), new Color(226, 192, 124), new Color(242, 218, 160),
        };

        public static Color AnySand => Sand[Main.rand.Next(Sand.Length)];

        /// <summary>
        /// A burst of sand: grains thrown out from <paramref name="at"/> round <paramref name="way"/> (a full circle if
        /// it is zero), a puff of dust hanging after them, and a little of the game's own sand. Seen only.
        /// </summary>
        public static void Burst(Vector2 at, Vector2 way, int grains, float speed, float spread = MathHelper.Pi, float dust = 1f)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < grains; i++)
            {
                Vector2 velocity = way == Vector2.Zero
                    ? Main.rand.NextVector2Unit() * speed * Main.rand.NextFloat(0.4f, 1f)
                    : way.SafeNormalize(Vector2.UnitX).RotatedByRandom(spread) * speed * Main.rand.NextFloat(0.4f, 1f);

                PADParticleHandler.SpawnParticle(new SandGrain(at + Main.rand.NextVector2Circular(4f, 4f), velocity, AnySand,
                    Main.rand.Next(16, 30), Main.rand.NextFloat(1f, 2f)));
            }

            for (int i = 0; i < (int)(3 * dust); i++)
            {
                PADParticleHandler.SpawnParticle(new DustFog(at + Main.rand.NextVector2Circular(8f, 8f),
                    (way == Vector2.Zero ? Main.rand.NextVector2Unit() : way.SafeNormalize(Vector2.UnitX)) * Main.rand.NextFloat(0.5f, 1.6f),
                    Sand[2], Main.rand.Next(30, 50), Main.rand.NextFloat(0.5f, 0.9f), 0.35f));
            }

            for (int i = 0; i < (int)(4 * dust); i++)
            {
                Dust d = Dust.NewDustPerfect(at, DustID.Sand, Main.rand.NextVector2Circular(speed * 0.4f, speed * 0.4f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>
        /// Throws an NPC: its speed set to <paramref name="velocity"/> where the world is run (the server, or a game of
        /// one) and told to everyone; from a client it is asked of the server (PADPacket.Launch). Weight is the
        /// caller's to judge: this moves what it is given.
        /// </summary>
        public static void Launch(NPC npc, Vector2 velocity)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ModPacket packet = ModContent.GetInstance<global::PisAndDag.PisAndDag>().GetPacket();
                packet.Write((byte)PADPacket.Launch);
                packet.Write((byte)npc.whoAmI);
                packet.WriteVector2(velocity);
                packet.Send();
                return;
            }

            Throw(npc, velocity);
        }

        /// <summary>The server's half of <see cref="Launch"/>. A client may ask for a throw and no more: 24 pixels a tick at most.</summary>
        internal static void HandleLaunch(BinaryReader reader, int from)
        {
            int who = reader.ReadByte();
            Vector2 velocity = reader.ReadVector2();

            if (Main.netMode != NetmodeID.Server || who >= Main.maxNPCs || !Main.npc[who].active)
                return;

            float speed = velocity.Length();

            if (float.IsNaN(speed))
                return;

            if (speed > 24f)
                velocity *= 24f / speed;

            Throw(Main.npc[who], velocity);
        }

        private static void Throw(NPC npc, Vector2 velocity)
        {
            npc.velocity = velocity;
            npc.netUpdate = true;
        }

        /// <summary>A glint off the metal: a small gold star, added as light.</summary>
        public static void Glint(Vector2 at, float scale = 1f)
        {
            if (Main.dedServ)
                return;

            PADParticleHandler.SpawnParticle(new CrownGlint(at, 26f * scale, 16, Shine));
        }
    }
}
