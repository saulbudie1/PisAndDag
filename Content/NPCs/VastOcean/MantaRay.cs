using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Biomes;
using PisAndDag.Content.NPCs.Pseudo;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.ModLoader;

// (As the school fish: not "...VastOcean", which would be found before the place itself. SchoolFish.)
namespace PisAndDag.Content.NPCs.Ocean
{
    /// <summary>
    /// A manta ray of the Vast Ocean, a pseudo-NPC as the school fish are (for the looks only, on each player's own
    /// machine), by request ("Manta ray NPCs create: 4 frame, flat and swimming slowly in whatever zone you figure out in
    /// school of 2-3"). In twos and threes (VastOceanRays), in the open water of the Vast Ocean's first section, away from
    /// the rock: the first ahead, the others behind it and to either side, each drifting about its place.
    ///
    /// HOW ONE SWIMS: slowly and heavily, turning wide; its wings beating a slow four-beat stroke (MantaRay.png, four
    /// pictures: level, up, level, down), quicker when something big comes close, which it keeps from by a few body
    /// lengths without hurry. It rises and sinks a little with each stroke, tilted with the way it goes. Turning round it
    /// is seen edge on, narrowing through nothing to the other way, as the fish are. It keeps off the rock and under the
    /// surface. The school fish part round it as they do round anything big (VastOceanSchools.GatherThreats).
    ///
    /// HOW ONE LOOKS: its picture (dark above, pale beneath, white marks at the shoulders) a little bigger or smaller
    /// than the next, in the sea's own light where it is or whatever else lights it, whichever is brighter.
    /// </summary>
    public class MantaRay : ModPseudoNPC
    {
        public override string Texture => "PisAndDag/Content/NPCs/VastOcean/MantaRay";

        /// <summary>Its pictures, one under another (a beat of its wings), and the ticks each is shown for.</summary>
        public const int Frames = 4;
        public const float FrameTicks = 12f;

        public override int Most => VastOceanRays.MostGroups * VastOceanRays.Most;

        public override bool Smooth => true;

        public override float DespawnDistance => 4200f;

        /// <summary>How fast it goes, at its calmest and most hurried (pixels a tick); how quickly it turns to where it would go.</summary>
        private const float Slowest = 0.35f, Calm = 1.3f, Hurried = 2.4f, Steer = 0.025f;

        /// <summary>How near something big may come before it is minded to keep away (pixels past its body), and how hard.</summary>
        private const float Wary = 110f, KeepFrom = 0.05f;

        /// <summary>The room it keeps from the others of its school, middle to middle.</summary>
        private const float Room = 70f;

        /// <summary>Its places behind the first, along the way they go and across it (pixels): one behind and above, one behind and below.</summary>
        internal static readonly Vector2[] Places = { Vector2.Zero, new Vector2(-95f, -40f), new Vector2(-80f, 46f) };

        // ---- one ray's slots -----------------------------------------------------------------------------

        /// <summary>Where it is in its stroke (ticks of its own, quicker when hurried); its drift's phase.</summary>
        private static ref float Beat(ref PseudoNPC npc) => ref npc.Ai0;
        private static ref float Phase(ref PseudoNPC npc) => ref npc.Ai1;

        /// <summary>Which way it faces, -1 left to 1 right; edge on between, turning.</summary>
        private static ref float Face(ref PseudoNPC npc) => ref npc.Ai2;

        /// <summary>How hurried, 0 to 1.</summary>
        private static ref float Fright(ref PseudoNPC npc) => ref npc.LocalAi0;

        public override void OnSpawn(ref PseudoNPC npc)
        {
            Beat(ref npc) = Main.rand.NextFloat(Frames * FrameTicks);
            Phase(ref npc) = Main.rand.NextFloat(MathHelper.TwoPi);
            Face(ref npc) = npc.Velocity.X < 0f ? -1f : 1f;
            npc.Scale = Main.rand.NextFloat(1.0f, 1.3f);
            npc.Opacity = 0f;
        }

        public override bool CheckActive(ref PseudoNPC npc) =>
            npc.Group >= 0 && npc.Group < VastOceanRays.MostGroups && VastOceanRays.Groups[npc.Group].Alive;

        public override void AI(ref PseudoNPC npc)
        {
            if (!CheckActive(ref npc))
                return;

            ref VastOceanRays.Group group = ref VastOceanRays.Groups[npc.Group];
            Vector2 v = npc.Velocity;

            npc.Opacity = Math.Min(1f, npc.Age / 60f);

            // To its place in the school, drifting about it a little.
            float phase = Phase(ref npc);
            Vector2 slot = Places[Math.Clamp(npc.Slot, 0, Places.Length - 1)];
            slot += new Vector2((float)Math.Sin(npc.Age * 0.008f + phase) * 18f, (float)Math.Cos(npc.Age * 0.011f + phase * 1.3f) * 14f);

            Vector2 place = VastOceanRays.At(group.Centre, group.Way, slot);
            Vector2 pull = (place - npc.Center) * 0.012f;

            if (pull.LengthSquared() > 1f)
                pull = Vector2.Normalize(pull);

            Vector2 wants = group.Cruise + pull;

            // Apart from the others of its school.
            for (int k = 0; k < group.Count; k++)
            {
                int other = group.Members[k];

                if (other == npc.WhoAmI || other < 0 || !PseudoNPCs.Pool[other].Active || PseudoNPCs.Pool[other].Group != npc.Group)
                    continue;

                Vector2 apart = npc.Center - PseudoNPCs.Pool[other].Center;
                float d = apart.Length() + 0.001f;

                if (d < Room)
                    wants += apart / d * ((Room - d) / Room * 0.6f);
            }

            // Away from anything big near it, without hurry.
            ref float fright = ref Fright(ref npc);
            fright *= 0.985f;

            float body = 40f * npc.Scale;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player player = Main.player[p];

                if (!player.active || player.dead)
                    continue;

                Vector2 away = npc.Center - player.Center;
                float d = away.Length() + 0.001f;
                float past = Math.Max(d - body - 20f, 0f);

                if (past > Wary * 2.5f)
                    continue;

                float bell = (float)Math.Exp(-past * past / (2f * Wary * Wary));

                wants += away / d * (KeepFrom * 20f * bell);
                fright = Math.Max(fright, bell);
            }

            v += (wants - v) * Steer;

            // A rise and a sink with each stroke.
            v.Y += (float)Math.Cos(Beat(ref npc) / (Frames * FrameTicks) * MathHelper.TwoPi) * 0.012f;

            // Its speed: never quite stopped, calm, quicker when hurried.
            float top = Calm + fright * (Hurried - Calm);
            float speed = v.Length() + 0.001f;
            v *= MathHelper.Clamp(speed, Slowest, top) / speed;

            // Off the rock and under the surface: what is ahead of it, turned off.
            if (!VastOceanSchools.Open(npc.Center + v * 30f))
            {
                Vector2 open = Vector2.Zero;

                for (int a = 0; a < 8; a++)
                {
                    Vector2 look = VastOceanSchools.Ways[a];

                    if (VastOceanSchools.Open(npc.Center + look * 60f))
                        open += look;
                }

                v += open != Vector2.Zero ? Vector2.Normalize(open) * 0.12f : -v * 0.2f;
            }

            if (!VastOceanSchools.Open(npc.Center + v))
                v = VastOceanSchools.Open(npc.Center) ? v * -0.3f : Vector2.Normalize(group.Centre - npc.Center + new Vector2(0.01f)) * 1f;

            npc.Velocity = v;

            // Its stroke: quicker when hurried.
            Beat(ref npc) = (Beat(ref npc) + 1f + fright * 1.2f) % (Frames * FrameTicks);

            // Facing: turning round slowly, edge on between.
            ref float face = ref Face(ref npc);
            float facing = Math.Abs(v.X) > 0.08f ? (v.X < 0f ? -1f : 1f) : (face < 0f ? -1f : 1f);

            face += MathHelper.Clamp(facing - face, -0.05f, 0.05f);
            npc.Direction = face > 0f ? 1 : -1;

            float pitch = MathHelper.Clamp((float)Math.Atan2(v.Y, Math.Abs(v.X) + 0.4f), -0.35f, 0.35f);
            npc.Rotation = face > 0f ? pitch : -pitch;
        }

        public override void Draw(SpriteBatch batch, ref PseudoNPC npc, Vector2 screenPosition)
        {
            if (TextureAsset == null || !TextureAsset.IsLoaded || !CheckActive(ref npc))
                return;

            Texture2D picture = TextureAsset.Value;
            int high = picture.Height / Frames;
            int frame = (int)(Beat(ref npc) / FrameTicks) % Frames;
            Rectangle source = new Rectangle(0, frame * high, picture.Width, high);

            // In the sea's own light where it is, or whatever else lights it, whichever is brighter.
            float rows = npc.Center.Y / 16f - VastOcean.Surface;
            Vector3 sea = Vector3.Min(VastOceanSky.SeaGlow(rows) * 2.2f, Vector3.One);
            Vector3 light = Vector3.Max(sea, Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f)).ToVector3());
            Color colour = new Color(light) * npc.Opacity;

            float face = Face(ref npc);
            Vector2 scale = new Vector2(npc.Scale * Math.Max(Math.Abs(face), 0.12f), npc.Scale);

            // Its picture faces left: turned about to go right.
            batch.Draw(picture, npc.Center - screenPosition, source, colour, npc.Rotation, source.Size() * 0.5f, scale,
                face > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// The manta rays' schools (MantaRay): two at a time about the player while the screen's middle is in the Vast
    /// Ocean's first section, in open water from twelve rows under the surface to a little above the section's end; each
    /// of two or three. Each comes in out of sight, from past the edge of the screen, where there is open water all about
    /// it (no rock within ten tiles), and is let go out of sight, far behind. A school's middle goes its way slowly,
    /// mostly along, turning wide: off the rock ahead, under the surface and over the section's end, apart from the
    /// other school.
    /// </summary>
    public class VastOceanRays : ModSystem
    {
        public const int MostGroups = 2;

        /// <summary>How many rays a school is: 2 to 3.</summary>
        public const int Fewest = 2, Most = 3;

        /// <summary>The rows they keep to: from this many under the surface, to this many over the first section's end.</summary>
        private const int HighestRows = 12, AboveSectionEnd = 10;

        /// <summary>How fast a school goes along (pixels a tick), from and to.</summary>
        private const float CruiseSlow = 0.5f, CruiseFast = 0.8f;

        /// <summary>A school comes in this far past the screen's half diagonal, at most; and is let go past this.</summary>
        private const float ComeFrom = 700f, LetGoPast = 1200f;

        /// <summary>How much open water a school wants about it to come (pixels), and how far it keeps from the other school.</summary>
        private const float OpenAround = 160f, Apart = 900f;

        internal struct Group
        {
            public bool Alive;
            public int Count;
            public Vector2 Centre, Cruise, Way;
            public float Heading, Speed, Spin;
            public int[] Members;
        }

        internal static readonly Group[] Groups = new Group[MostGroups];

        private static int nextSpawn;
        private static bool fresh = true;

        public override void OnWorldUnload() => Clear();

        private static void Clear()
        {
            for (int g = 0; g < MostGroups; g++)
                Let(g);

            nextSpawn = 0;
            fresh = true;
        }

        private static void Let(int g)
        {
            if (!Groups[g].Alive)
                return;

            for (int k = 0; k < Groups[g].Count; k++)
            {
                int ray = Groups[g].Members[k];

                if (ray >= 0 && PseudoNPCs.Pool[ray].Active && PseudoNPCs.Pool[ray].Group == g && PseudoNPCs.Pool[ray].Type == ModContent.GetInstance<MantaRay>().Type)
                    PseudoNPCs.Kill(ray);
            }

            Groups[g] = default;
        }

        /// <summary>A place in a school (along the way it goes, and across it: above is above whichever way it goes) from its middle.</summary>
        internal static Vector2 At(Vector2 centre, Vector2 way, Vector2 slot) =>
            centre + way * slot.X + new Vector2(-way.Y, way.X) * (slot.Y * (way.X < 0f ? -1f : 1f));

        /// <summary>The rays as things the school fish keep from (VastOceanSchools.GatherThreats): where each is, half its size, how hard.</summary>
        internal static void Threaten(Action<Vector2, float, float> add)
        {
            for (int g = 0; g < MostGroups; g++)
            {
                if (!Groups[g].Alive)
                    continue;

                for (int k = 0; k < Groups[g].Count; k++)
                {
                    int ray = Groups[g].Members[k];

                    if (ray >= 0 && PseudoNPCs.Pool[ray].Active && PseudoNPCs.Pool[ray].Group == g)
                        add(PseudoNPCs.Pool[ray].Center, 36f * PseudoNPCs.Pool[ray].Scale, 1.2f);
                }
            }
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu)
                return;

            if (SubworldKeeper.Current is not VastOcean)
            {
                if (!fresh)
                    Clear();

                return;
            }

            Vector2 middle = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            float outside = new Vector2(Main.screenWidth, Main.screenHeight).Length() * 0.5f;
            float far = outside + LetGoPast;

            for (int g = 0; g < MostGroups; g++)
            {
                if (Groups[g].Alive && Vector2.DistanceSquared(Groups[g].Centre, middle) > far * far)
                    Let(g);
            }

            for (int g = 0; g < MostGroups; g++)
            {
                if (Groups[g].Alive)
                    Move(g);
            }

            Spawn(middle, outside);
            fresh = false;
        }

        private static void Move(int g)
        {
            ref Group s = ref Groups[g];
            float rows = s.Centre.Y / 16f - VastOcean.Surface;

            // Its way: wandering a little; off the rock ahead, turning wide.
            s.Heading += Main.rand.NextFloat(-0.006f, 0.006f);

            float look = 260f;

            if (!VastOceanSchools.Open(s.Centre + s.Heading.ToRotationVector2() * look))
            {
                bool left = VastOceanSchools.Open(s.Centre + (s.Heading - 0.6f).ToRotationVector2() * look);
                bool right = VastOceanSchools.Open(s.Centre + (s.Heading + 0.6f).ToRotationVector2() * look);

                s.Heading += left && !right ? -0.025f : !left && right ? 0.025f : 0.03f * s.Spin;
            }

            // Mostly along: its heading eased back toward level.
            float level = s.Heading.ToRotationVector2().X >= 0f ? 0f : MathHelper.Pi;
            s.Heading += MathHelper.WrapAngle(level - s.Heading) * 0.002f;

            Vector2 room = Vector2.Zero;

            for (int a = 0; a < 8; a++)
            {
                if (!VastOceanSchools.Open(s.Centre + VastOceanSchools.Ways[a] * OpenAround))
                    room -= VastOceanSchools.Ways[a];
            }

            if (rows < HighestRows)
                room.Y += 1f;
            else if (rows > VastOcean.SectionRows - AboveSectionEnd)
                room.Y -= 1f;

            for (int o = 0; o < MostGroups; o++)
            {
                if (o == g || !Groups[o].Alive)
                    continue;

                Vector2 apart = s.Centre - Groups[o].Centre;
                float d = apart.Length() + 0.001f;

                if (d < Apart)
                    room += apart / d * 0.5f;
            }

            Vector2 wants = s.Heading.ToRotationVector2() * s.Speed + room * 0.15f;

            s.Cruise += (wants - s.Cruise) * 0.01f;
            s.Centre += s.Cruise;

            if (s.Cruise.LengthSquared() > 0.01f)
                s.Way = Vector2.Normalize(Vector2.Lerp(s.Way, Vector2.Normalize(s.Cruise), 0.01f) + new Vector2(0.0001f, 0f));
        }

        private static void Spawn(Vector2 middle, float outside)
        {
            if (--nextSpawn > 0 && !fresh)
                return;

            nextSpawn = 120;

            // Only while the screen's middle is in the first section.
            float rows = middle.Y / 16f - VastOcean.Surface;

            if (rows < 0f || rows > VastOcean.SectionRows)
                return;

            int alive = 0;

            for (int g = 0; g < MostGroups; g++)
            {
                if (Groups[g].Alive)
                    alive++;
            }

            int tries = fresh ? 24 : 4;

            for (int k = 0; k < tries && alive < MostGroups; k++)
            {
                Vector2 at = middle + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * (outside + Main.rand.NextFloat(260f, ComeFrom));

                if (TryStart(at, middle))
                    alive++;
            }
        }

        private static bool TryStart(Vector2 at, Vector2 middle)
        {
            float rows = at.Y / 16f - VastOcean.Surface;

            if (rows < HighestRows + 2 || rows > VastOcean.SectionRows - AboveSectionEnd)
                return false;

            int g = Array.FindIndex(Groups, s => !s.Alive);

            if (g < 0 || !VastOceanSchools.Open(at))
                return false;

            for (int o = 0; o < MostGroups; o++)
            {
                if (Groups[o].Alive && Vector2.DistanceSquared(Groups[o].Centre, at) < Apart * Apart)
                    return false;
            }

            // Open water all about it, its width and more: the open sea, not by the rock.
            for (int a = 0; a < 8; a++)
            {
                if (!VastOceanSchools.Open(at + VastOceanSchools.Ways[a] * OpenAround))
                    return false;
            }

            // Across the screen, mostly along.
            float heading = (middle.X - at.X >= 0f ? 0f : MathHelper.Pi) + Main.rand.NextFloat(-0.35f, 0.35f);
            float speed = Main.rand.NextFloat(CruiseSlow, CruiseFast);
            int count = Main.rand.Next(Fewest, Most + 1);
            int type = ModContent.GetInstance<MantaRay>().Type;

            Groups[g] = new Group
            {
                Alive = true,
                Centre = at,
                Heading = heading,
                Speed = speed,
                Cruise = heading.ToRotationVector2() * speed,
                Way = heading.ToRotationVector2(),
                Spin = Main.rand.NextBool() ? 1f : -1f,
                Members = new int[Most],
            };

            ref Group s = ref Groups[g];

            for (int k = 0; k < count; k++)
            {
                Vector2 place = At(at, s.Way, MantaRay.Places[k]);

                if (!VastOceanSchools.Open(place))
                    place = at;

                int made = PseudoNPCs.New(type, place, s.Cruise, g, k);

                if (made < 0)
                    break;

                s.Members[k] = made;
                s.Count = k + 1;
            }

            if (s.Count < Fewest)
            {
                Let(g);
                return false;
            }

            return true;
        }
    }
}
