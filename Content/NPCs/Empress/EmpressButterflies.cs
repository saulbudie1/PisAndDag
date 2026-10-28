using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PisAndDag.Content.NPCs.Empress
{
    /// <summary>
    /// The butterflies she bursts into at her death (by request: "a clutter of 20 ish, flying in different directions,
    /// updown left right, sorta like malenia", and then "butterfly critters not butterfly particles"): the game's own,
    /// real ones, that a net catches and a jar keeps. Twenty, round her colours in her order as they go out round the
    /// ring: Purple Emperors for her pink, Sulphurs for her gold, Ulysses for her mint, Tree Nymphs for her sky, Purple
    /// Emperors again for her lilac.
    ///
    /// For their first seconds they are hers. Thrown out every way from where she was, fast, each a little off its share
    /// of the ring and none as fast as another, they slow to a flutter and still go out, wandering off their lines and
    /// curling as they go, bobbing as they beat their wings; what stops them turns them, and like the game's own they turn
    /// up from ground or water close under them. A little of her light is on them: enough on the dark round each to see
    /// it by, a faint glow behind it in its colour, a mote off it now and then. Nothing more: they are to look like what
    /// they are. Then they are butterflies and fly as the game flies them, on from the way they were going (one gone far
    /// comes back once toward whoever is near, as a new one does), and the light goes off them.
    ///
    /// The server makes them and sends each with its flight, so every machine flies it the same.
    /// </summary>
    public class EmpressButterflies : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Butterfly;

        /// <summary>How many she bursts into: a ring of them thrown far, and (by request: "add 20 more butterflies but they
        /// have smaller velocity/radius smaller") as many again thrown softly, a nearer cloud inside it.</summary>
        public const int Count = 20;
        public const int Near = 20;

        // The game's butterflies by their ai[2]: one more than their row on its sheet.
        private const int PurpleEmperor = 2;
        private const int Ulysses = 4;
        private const int Sulphur = 5;
        private const int TreeNymph = 6;

        /// <summary>The butterfly for each of her colours, in the order they go round (EmpressArt: Pink, Gold, Mint, Sky, Lilac).</summary>
        private static readonly int[] Kinds = { PurpleEmperor, Sulphur, Ulysses, TreeNymph, PurpleEmperor };

        /// <summary>
        /// Thrown out this fast (some much faster than others, so they go out a cloud and not a ring), slowing to a flutter
        /// this fast; and how quickly the one becomes the other (of the difference, a tick).
        /// </summary>
        private const float ThrownLeast = 3.5f;
        private const float ThrownMost = 9f;
        private const float FlutterLeast = 1.3f;
        private const float FlutterMost = 2.5f;
        private const float Settle = 0.035f;

        /// <summary>The nearer cloud: thrown this fast, slowing to this (each about a third of the ring's).</summary>
        private const float NearThrownLeast = 1.2f;
        private const float NearThrownMost = 3.2f;
        private const float NearFlutterLeast = 0.45f;
        private const float NearFlutterMost = 1f;

        /// <summary>Ticks of her flight, the least and the most; then the game's own.</summary>
        private const int FlightLeast = 150;
        private const int FlightMost = 220;

        /// <summary>Ticks her light is on them: all of it for the first of these, then going, gone by the second.</summary>
        private const int LightWhole = 100;
        private const int LightTicks = 300;

        /// <summary>One of hers: her flight, then her light, on it still.</summary>
        private bool hers;

        /// <summary>Ticks since she burst.</summary>
        private int age;

        /// <summary>Ticks of her flight it has; then its own.</summary>
        private int flight;

        /// <summary>Its place round the ring, of <see cref="Count"/>: its colour.</summary>
        private byte place;

        /// <summary>The way it goes, a little turn a tick off it, the speed it slows to, its own beat.</summary>
        private float way;
        private float curl;
        private float flutter;
        private float sway;

        /// <summary>Its flight, before the bob of its wings.</summary>
        private Vector2 glide;

        /// <summary>The server's: <see cref="Count"/> of them out of <paramref name="at"/>, every way, the first straight up and the rest round from it; and <see cref="Near"/> more, softly.</summary>
        public static void Burst(IEntitySource source, Vector2 at)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            for (int n = 0; n < Count + Near; n++)
            {
                // The ring first, then the nearer cloud, each round her colours; the cloud's ways between the ring's.
                bool near = n >= Count;
                int i = near ? n - Count : n;
                float way = (i + (near ? 0.5f : 0f) + Main.rand.NextFloat(-0.42f, 0.42f)) / Count * MathHelper.TwoPi - MathHelper.PiOver2;
                int kind = Kinds[Math.Min(Kinds.Length - 1, i * Kinds.Length / Count)];
                Vector2 from = at + way.ToRotationVector2() * 6f;

                // The game's own butterfly, of this kind and this size (it stands with its foot where it is put).
                int index = NPC.NewNPC(source, (int)from.X, (int)from.Y + 5, NPCID.Butterfly, 0, 0f, 0f, kind, Main.rand.NextFloat(0.85f, 1.1f));

                if (index < 0 || index >= Main.maxNPCs || !Main.npc[index].TryGetGlobalNPC(out EmpressButterflies flier))
                    continue;

                NPC butterfly = Main.npc[index];

                flier.hers = true;
                flier.age = 0;
                flier.flight = Main.rand.Next(FlightLeast, FlightMost + 1);
                flier.place = (byte)i;
                flier.way = way;
                flier.curl = Main.rand.NextFloat(0.0015f, 0.004f) * (Main.rand.NextBool() ? 1f : -1f);
                flier.flutter = near ? Main.rand.NextFloat(NearFlutterLeast, NearFlutterMost) : Main.rand.NextFloat(FlutterLeast, FlutterMost);
                flier.sway = Main.rand.NextFloat(MathHelper.TwoPi);
                flier.glide = way.ToRotationVector2() * (near ? Main.rand.NextFloat(NearThrownLeast, NearThrownMost) : Main.rand.NextFloat(ThrownLeast, ThrownMost));

                butterfly.velocity = flier.glide;
                Own(butterfly);

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);
            }
        }

        /// <summary>What the game's flight would have made it, which it is not flying yet: its size, what a net catches of it, the way it faces.</summary>
        private static void Own(NPC npc)
        {
            npc.scale = npc.ai[3] > 0f ? npc.ai[3] : 1f;
            npc.catchItem = (short)(ItemID.MonarchButterfly + Math.Max(0, (int)npc.ai[2] - 1));

            if (npc.velocity.X != 0f)
                npc.direction = npc.velocity.X < 0f ? -1 : 1;
        }

        public override bool PreAI(NPC npc)
        {
            if (!hers)
                return true;

            age++;

            if (age >= LightTicks)
            {
                hers = false;
                return true;
            }

            Shine(npc);

            // Its own flight now; only her light left on it.
            if (age > flight)
                return true;

            // What stops it turns it; and, as the game's own do, it turns up from ground or water close under it
            // rather than meet them.
            if (npc.collideX)
            {
                way = MathHelper.Pi - way;
                glide.X *= -0.5f;
            }

            if (npc.collideY)
            {
                way = -way;
                glide.Y *= -0.5f;
            }

            if (glide.Y > 0f && Under(npc))
            {
                if (Math.Sin(way) > 0.0)
                    way = -way;

                glide.Y *= 0.9f;
            }

            // On its way, curling off it and wandering, slowing from its throw to a flutter.
            way += curl;

            float wander = 0.36f * (float)Math.Sin(age * 0.055f + sway) + 0.15f * (float)Math.Sin(age * 0.13f + sway * 1.7f);
            glide = Vector2.Lerp(glide, (way + wander).ToRotationVector2() * flutter, Settle);

            // The bob of its wings, coming in as it slows.
            float beat = Math.Min(1f, age / 25f);
            npc.velocity = glide + new Vector2(0f, (float)Math.Cos(age * 0.38f + sway) * 0.85f * beat);

            Own(npc);

            if (age == flight)
                Release(npc);

            return false;
        }

        /// <summary>Ground or water in its own tile or the two under it (the game's own butterflies look as far).</summary>
        private static bool Under(NPC npc)
        {
            int x = (int)(npc.Center.X / 16f);
            int y = (int)(npc.Center.Y / 16f);

            for (int j = y; j < y + 3; j++)
            {
                if (!WorldGen.InWorld(x, j))
                    continue;

                Tile tile = Main.tile[x, j];

                if ((tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) || tile.LiquidAmount > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Its own now: the game's flight takes it on the way it was going, a little slower, until the game turns it (the
        /// server says when; a new one's first turn, which takes one gone far back toward whoever is near).
        /// </summary>
        private void Release(NPC npc)
        {
            Vector2 heading = glide.SafeNormalize(new Vector2(npc.direction, 0f));

            npc.ai[0] = heading.X * flutter * 0.8f;
            npc.ai[1] = heading.Y * flutter * 0.8f;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.localAI[0] = Main.rand.Next(40, 121);
                npc.netUpdate = true;
            }
        }

        /// <summary>Leaning into its way a little, as the game would lean it at a flutter (thrown, it would lie on its side).</summary>
        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (hers && age <= flight)
                npc.rotation = MathHelper.Clamp(npc.velocity.X * 0.08f, -0.35f, 0.35f);
        }

        // ---- her light on it ----------------------------------------------------------------------------------

        /// <summary>How much of her light is on it: all, then going smoothly to none.</summary>
        private float Light
        {
            get
            {
                if (!hers)
                    return 0f;

                float p = MathHelper.Clamp((age - LightWhole) / (float)(LightTicks - LightWhole), 0f, 1f);
                return 1f - p * p * (3f - 2f * p);
            }
        }

        /// <summary>Its colour of hers: the one its place round the ring falls in.</summary>
        private Color Colour => EmpressArt.Prism(place * Kinds.Length / Count / (float)Kinds.Length);

        /// <summary>Light thrown round it, enough to see it by in the dark, and a mote off it now and then. Only seen.</summary>
        private void Shine(NPC npc)
        {
            if (Main.dedServ)
                return;

            float light = Light;

            if (light <= 0f)
                return;

            Color colour = Colour;

            // Lit nearly white, so its own colours show.
            Lighting.AddLight(npc.Center, Color.Lerp(colour, Color.White, 0.5f).ToVector3() * (0.55f * light));

            if (Main.rand.NextFloat() < 0.05f * light)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(4f, 4f), DustID.RainbowMk2,
                    -npc.velocity * 0.12f + Main.rand.NextVector2Circular(0.4f, 0.4f), 0, colour, 0.6f);
                dust.noGravity = true;
                dust.fadeIn = 0.75f;
            }
        }

        /// <summary>Where the game draws a butterfly (Main.DrawNPCDirect_Inner): its middle across, a little up from its foot.</summary>
        private static Vector2 DrawnAt(NPC npc, Vector2 screenPos)
        {
            float frameHeight = TextureAssets.Npc[npc.type].Height() / (float)Math.Max(1, Main.npcFrameCount[npc.type]);
            return new Vector2(npc.Center.X, npc.Bottom.Y - frameHeight * npc.scale * 0.5f + 4f + Main.NPCAddHeight(npc) + npc.gfxOffY) - screenPos;
        }

        /// <summary>A faint glow behind it, in its colour; the butterfly itself the game's, drawn as the game draws it.</summary>
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            float light = Light;
            Texture2D soft = EmpressArt.Soft?.Value;

            if (light > 0f && soft != null)
                spriteBatch.Draw(soft, DrawnAt(npc, screenPos), null, EmpressArt.Additive(Colour, 0.3f * light), 0f, soft.Size() * 0.5f,
                    22f * npc.scale / (soft.Width * 0.5f), SpriteEffects.None, 0f);

            return true;
        }

        // ---- sent ---------------------------------------------------------------------------------------------

        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
        {
            bitWriter.WriteBit(hers);

            if (!hers)
                return;

            binaryWriter.Write((ushort)age);
            binaryWriter.Write((ushort)flight);
            binaryWriter.Write(place);
            binaryWriter.Write(way);
            binaryWriter.Write(curl);
            binaryWriter.Write(flutter);
            binaryWriter.Write(sway);
            binaryWriter.WriteVector2(glide);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
        {
            hers = bitReader.ReadBit();

            if (!hers)
                return;

            age = binaryReader.ReadUInt16();
            flight = binaryReader.ReadUInt16();
            place = binaryReader.ReadByte();
            way = binaryReader.ReadSingle();
            curl = binaryReader.ReadSingle();
            flutter = binaryReader.ReadSingle();
            sway = binaryReader.ReadSingle();
            glide = binaryReader.ReadVector2();
        }
    }
}
