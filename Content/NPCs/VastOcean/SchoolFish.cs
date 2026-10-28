using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs.Pseudo;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

// Not "PisAndDag.Content.NPCs.VastOcean", as the folder is: a namespace of that name would be found
// before the place itself (Subworlds.VastOcean) by everything under PisAndDag.Content.NPCs that names it.
// The pictures are named here by their paths (Texture).
namespace PisAndDag.Content.NPCs.Ocean
{
    /// <summary>
    /// A fish of a school in the Vast Ocean (VastOceanSchools), a pseudo-NPC (ModPseudoNPC): by request
    /// ("Added 3 schoolers fishes ... school of 35-60, tries to dodge player use 2d gaussian ... it curls
    /// and moves in masses where the invisible point shifts within the school size ... make it beautiful
    /// and magical ... think of them as little dots ... size 0.5x with +- 0.05", and "they can also have
    /// color variations and yeah make it magical and sparkly").
    ///
    /// HOW ONE SWIMS (each tick, <see cref="AI"/>): to a place of its own in the crowd, by request ("they
    /// gonna be in a amorphous crowd not rotating in a circle"): somewhere in an oval about the point its
    /// school gathers on (half as long again as the school is wide along the way it goes, three quarters
    /// as wide across it), that place drifting about by itself, slowly, so the crowd churns and changes
    /// shape; the gathering point shifting about inside the school, the crowd bulging after it. One school
    /// in three is a vortex instead (VastOceanSchools.VortexChance): its fish each at a distance of their
    /// own from the gathering point, milling round it one way. Along with the school as it goes; apart from the fish next to it; away from anything big
    /// coming through (a player, a creature, a shot), by a bell curve of its distance from the thing's
    /// body (a 2D gaussian): hard right by it, nothing a few body widths off, so the school opens round
    /// it and closes again behind; faster when frightened, and slower to calm. It wiggles as it goes. It
    /// keeps out of the rock and under the surface, turning off what is ahead of it.
    ///
    /// HOW ONE LOOKS (<see cref="Draw"/>, <see cref="DrawGlow"/>): its picture at its own size, a tenth
    /// either way (by request: "keep their original size; make it +- 0.1x"), in one of its kind's colours (<see cref="SchoolFishLooks"/>), most of a school in one or
    /// two of them; tilted up or down as it swims, wiggling. Turning round it is seen edge on, narrowing
    /// through nothing to the other way, and its flank catches the light as it goes through: a flash of
    /// silver, and a glint (VastOceanSchools) now and then; in the sun's light a scale glitters now and then.
    /// In the dark (by night, and in the deep at any hour) it gives its own light, in its own colours,
    /// a wave of it going through the school, with a soft glow about it and a trail of small lights behind.
    /// </summary>
    public abstract class SchoolFish : ModPseudoNPC
    {
        /// <summary>Every kind of school fish, as loaded.</summary>
        public static readonly List<SchoolFish> All = new List<SchoolFish>();

        // ---- the kind ----------------------------------------------------------------------------------

        /// <summary>The kind's picture: its file under Content/NPCs/VastOcean.</summary>
        protected abstract string File { get; }

        public override string Texture => "PisAndDag/Content/NPCs/VastOcean/" + File;

        /// <summary>Pictures in its file, one under another (the Anthias's two).</summary>
        public virtual int Frames => 1;

        /// <summary>Its file's pixels to each of the picture's own (drawn two to one).</summary>
        public virtual int ArtPixel => 2;

        /// <summary>Its colours: which picture, and what is done to its colours (<see cref="Recolour"/>); the first of each picture as it is drawn.</summary>
        public abstract Variant[] Variants { get; }

        /// <summary>How fast its school goes along (pixels a tick), from and to.</summary>
        public abstract float CruiseSlow { get; }
        public abstract float CruiseFast { get; }

        /// <summary>How hard it swims about its school's point; its top speed; its top speed frightened; how quickly it turns to where it would go (the part of the way in a tick).</summary>
        public abstract float Swim { get; }
        public abstract float Top { get; }
        public abstract float Flee { get; }
        public virtual float Steer => 0.065f;

        /// <summary>How likely a school of it is in each of the sections schools are in (VastOceanSchools.Sections: the Vast Ocean, the Schooler Zone); and times what near the rock (within twenty tiles).</summary>
        public abstract float[] Depths { get; }
        public virtual float NearRock => 1f;

        public override int Most => VastOceanSchools.MostSchools * VastOceanSchools.Most;

        public override bool Smooth => true;

        public override float DespawnDistance => 4200f;

        public override void SetStaticDefaults()
        {
            All.Add(this);
        }

        public override void Unload()
        {
            All.Clear();
        }

        // ---- its colours ------------------------------------------------------------------------------

        /// <summary>A school fish's colour: the picture it is drawn from and what is done to its colours.</summary>
        public readonly struct Variant
        {
            public readonly int Frame;
            public readonly Recolour[] Rules;

            public Variant(int frame, params Recolour[] rules)
            {
                Frame = frame;
                Rules = rules;
            }
        }

        /// <summary>Which of a fish's pixels are changed: all, its body, or its fins (its picture's top and bottom rows and the last quarter and more of it, the tail).</summary>
        public enum Part : byte
        {
            All,
            Body,
            Fins,
        }

        /// <summary>
        /// A change to the colours of a fish's picture: its pixels of hues from <see cref="From"/> to
        /// <see cref="To"/> (degrees, going round past 360 if From is the greater) in the <see cref="Part"/>
        /// given, turned so many degrees round the wheel, and their colour and brightness so many times. Its
        /// pale and dark pixels (the eye, the jaw) are never changed. The first rule a pixel fits is the one
        /// done to it.
        /// </summary>
        public readonly struct Recolour
        {
            public readonly float From, To, Hue, Saturation, Value;
            public readonly Part Part;

            public Recolour(float from, float to, Part part, float hue, float saturation, float value)
            {
                From = from;
                To = to;
                Part = part;
                Hue = hue;
                Saturation = saturation;
                Value = value;
            }

            public bool Fits(float hue) => From <= To ? hue >= From && hue <= To : hue >= From || hue <= To;
        }

        // ---- one fish's slots --------------------------------------------------------------------------

        /// <summary>Its place in the crowd from its school's gathering point (pixels): along the way the school goes, and across it.</summary>
        private static ref float Along(ref PseudoNPC npc) => ref npc.Ai0;
        private static ref float Across(ref PseudoNPC npc) => ref npc.LocalAi3;

        /// <summary>How hard it swims, its own (its kind's, a little more or less).</summary>
        private static ref float Strength(ref PseudoNPC npc) => ref npc.Ai1;

        /// <summary>Where it is in its wiggle.</summary>
        private static ref float Phase(ref PseudoNPC npc) => ref npc.Ai2;

        /// <summary>Which way it faces, -1 left to 1 right; edge on between, turning.</summary>
        private static ref float Face(ref PseudoNPC npc) => ref npc.Ai3;

        /// <summary>How frightened, 0 to 1.</summary>
        private static ref float Fright(ref PseudoNPC npc) => ref npc.LocalAi0;

        /// <summary>Ticks before it can glint again turning.</summary>
        private static ref float Cool(ref PseudoNPC npc) => ref npc.LocalAi1;

        /// <summary>A little brighter or darker than the next one.</summary>
        private static ref float Shade(ref PseudoNPC npc) => ref npc.LocalAi2;

        /// <summary>How far past a threat's body its bell curve reaches (its spread, pixels).</summary>
        public const float Spread = 34f;

        /// <summary>
        /// The room a fish keeps about itself (pixels, middle to middle: about its length, so they touch and overlap
        /// a little, never crammed), and how hard it is pushed off one right on top of it (pixels a tick, each tick),
        /// by request ("dont have too much overlapping within the fishes tho so the school can be big").
        /// </summary>
        public const float Room = 24f, KeepApart = 0.22f;

        public override void OnSpawn(ref PseudoNPC npc)
        {
            ref VastOceanSchools.School school = ref VastOceanSchools.Schools[npc.Group];

            // Anywhere in the oval, as evenly as not.
            Vector2 place = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * ((float)Math.Sqrt(Main.rand.NextFloat()) * school.Radius);
            Along(ref npc) = place.X * 1.3f;
            Across(ref npc) = place.Y * 0.75f;
            Strength(ref npc) = Swim * Main.rand.NextFloat(0.85f, 1.15f);
            Phase(ref npc) = Main.rand.NextFloat(MathHelper.TwoPi);
            Face(ref npc) = npc.Velocity.X < 0f ? -1f : 1f;
            Shade(ref npc) = Main.rand.NextFloat(0.94f, 1.06f);
            npc.Scale = Main.rand.NextFloat(0.9f, 1.1f);
            npc.Opacity = 0f;

            // Mostly one or two of its kind's colours in a school, a few of the others.
            npc.Variant = Main.rand.NextFloat() < 0.85f ? (Main.rand.NextBool() ? school.VariantA : school.VariantB) : Main.rand.Next(Variants.Length);
        }

        public override bool CheckActive(ref PseudoNPC npc) =>
            npc.Group >= 0 && npc.Group < VastOceanSchools.MostSchools && VastOceanSchools.Schools[npc.Group].Alive;

        public override void AI(ref PseudoNPC npc)
        {
            if (!CheckActive(ref npc))
                return;

            int g = npc.Group;
            ref VastOceanSchools.School school = ref VastOceanSchools.Schools[g];
            Vector2 v = npc.Velocity;

            npc.Opacity = Math.Min(1f, npc.Age / 40f);

            // To its place in the crowd (drifting about by itself), along with the school.
            float phase = Phase(ref npc);
            float along = Along(ref npc) + (float)Math.Sin(npc.Age * 0.011f + phase) * 0.3f * school.Radius;
            float across = Across(ref npc) + (float)Math.Cos(npc.Age * 0.014f + phase * 1.7f) * 0.25f * school.Radius;
            Vector2 heading = school.Way;
            Vector2 place = school.Centre + school.Offset + heading * along + new Vector2(-heading.Y, heading.X) * across;
            Vector2 pull = (place - npc.Center) * 0.035f;
            float strength = Strength(ref npc);

            if (pull.LengthSquared() > strength * strength)
                pull = Vector2.Normalize(pull) * strength;

            Vector2 wants = school.Cruise * 1.2f + pull;

            if (school.Vortex)
            {
                // A vortex: to a distance of its own from the gathering point (its place's, from the middle of
                // the oval), and round it; the crowd's place left aside.
                Vector2 focus = school.Centre + school.Offset;
                Vector2 to = focus - npc.Center;
                float distance = to.Length() + 0.001f;
                Vector2 inward = to / distance;
                float orbit = new Vector2(Along(ref npc) / 1.3f, Across(ref npc) / 0.75f).Length();
                float radial = MathHelper.Clamp((distance - orbit) / school.Radius, -1f, 2f) * 1.2f;

                wants = school.Cruise * 1.3f + (inward * radial + new Vector2(-inward.Y, inward.X) * (school.Spin * school.Curl)) * strength;
            }

            v += (wants - v) * Steer;

            // Apart from the fish next to it: room of its own (<see cref="Room"/>), pushed off harder the closer,
            // so a school is big and loose, touching and a little over one another but never crammed.
            for (int k = 0; k < school.Count; k++)
            {
                int other = VastOceanSchools.Members[g, k];

                if (other == npc.WhoAmI || other < 0 || !PseudoNPCs.Pool[other].Active || PseudoNPCs.Pool[other].Group != g)
                    continue;

                Vector2 apart = npc.Center - PseudoNPCs.Pool[other].Center;

                if (apart.X > Room || apart.X < -Room || apart.Y > Room || apart.Y < -Room)
                    continue;

                float d2 = apart.LengthSquared();

                if (d2 < Room * Room)
                {
                    float d = (float)Math.Sqrt(d2);
                    v += apart / (d + 0.5f) * ((Room - d) / Room * KeepApart);
                }
            }

            // Away from what comes through: a bell curve of the distance from its body.
            ref float fright = ref Fright(ref npc);
            fright *= 0.97f;

            for (int k = 0; k < school.NearCount; k++)
            {
                ref VastOceanSchools.Threat threat = ref VastOceanSchools.Threats[VastOceanSchools.Near[g, k]];
                Vector2 away = npc.Center - threat.At;
                float d = away.Length() + 0.001f;
                float past = Math.Max(d - threat.Body, 0f);

                if (past > Spread * 3.5f)
                    continue;

                float bell = (float)Math.Exp(-past * past / (2f * Spread * Spread));

                v += away / d * (threat.Push * bell);
                fright = Math.Max(fright, bell);
            }

            // A wiggle, side to side of its way.
            float speed = v.Length() + 0.001f;
            v += new Vector2(-v.Y, v.X) / speed * ((float)Math.Sin(npc.Age * 0.23f + Phase(ref npc)) * 0.05f);

            // Its speed: never stopped, to its top, more frightened.
            float top = Top + fright * (Flee - Top);
            speed = v.Length() + 0.001f;
            v *= MathHelper.Clamp(speed, 0.45f, top) / speed;

            // Out of the rock and under the surface: what is ahead of it, turned off.
            if (!VastOceanSchools.Open(npc.Center + v * 8f))
            {
                Vector2 open = Vector2.Zero;

                for (int a = 0; a < 8; a++)
                {
                    Vector2 way = VastOceanSchools.Ways[a];

                    if (VastOceanSchools.Open(npc.Center + way * 20f))
                        open += way;
                }

                v += open != Vector2.Zero ? Vector2.Normalize(open) * 0.35f : -v * 0.35f;
            }

            if (!VastOceanSchools.Open(npc.Center + v))
            {
                // Somewhere it cannot be (the ground put down about it, or out of the water): back to its school.
                v = VastOceanSchools.Open(npc.Center) ? v * -0.3f : Vector2.Normalize(school.Centre - npc.Center + new Vector2(0.01f)) * 1.5f;
            }

            npc.Velocity = v;

            // Facing: turning round, edge on between, and its flank flashing as it goes through.
            ref float face = ref Face(ref npc);
            float was = face;
            float facing = Math.Abs(v.X) > 0.12f ? (v.X < 0f ? -1f : 1f) : (face < 0f ? -1f : 1f);

            face += MathHelper.Clamp(facing - face, -0.13f, 0.13f);

            ref float cool = ref Cool(ref npc);

            if (cool > 0f)
                cool--;

            bool seen = VastOceanSchools.Seen.Contains((int)npc.Center.X, (int)npc.Center.Y);

            if (seen && (was < 0f) != (face < 0f) && was != 0f && cool <= 0f && Main.rand.NextFloat() < 0.6f)
            {
                cool = 50f;
                VastOceanSchools.Flash(npc.Center, ref school, SchoolFishLooks.Glow(Type, npc.Variant));
            }

            npc.Direction = face > 0f ? 1 : -1;

            float pitch = MathHelper.Clamp((float)Math.Atan2(v.Y, Math.Abs(v.X) + 0.3f), -0.8f, 0.8f);
            float wiggle = (float)Math.Sin(npc.Age * 0.35f + Phase(ref npc)) * 0.07f;

            npc.Rotation = (face > 0f ? pitch : -pitch) + wiggle;

            // Glitter in the sun; a trail of light in the dark (only where it can be seen).
            if (!seen)
                return;

            if (school.Sun > 0.05f && Main.rand.NextFloat() < (0.0025f + 0.01f * fright) * school.Sun)
                VastOceanSchools.Twinkle(npc.Center + Main.rand.NextVector2Circular(2f, 2f), school.Sun);

            if (school.Dark > 0.05f && Main.rand.NextFloat() < 0.006f * speed * school.Dark)
                VastOceanSchools.Mote(npc.Center, v, SchoolFishLooks.Glow(Type, npc.Variant), school.Dark);
        }

        // ---- drawing -----------------------------------------------------------------------------------

        public override void Draw(SpriteBatch batch, ref PseudoNPC npc, Vector2 screenPosition)
        {
            if (!SchoolFishLooks.Ready(out Texture2D atlas) || !CheckActive(ref npc))
                return;

            ref VastOceanSchools.School school = ref VastOceanSchools.Schools[npc.Group];
            Rectangle frame = SchoolFishLooks.Frame(Type, npc.Variant);
            float face = Face(ref npc);

            // In the sea's own light where its school is, or whatever else lights it, whichever is brighter.
            Vector3 light = Vector3.Max(school.Sea, Lighting.GetColor((int)(npc.Center.X / 16f), (int)(npc.Center.Y / 16f)).ToVector3()) * Shade(ref npc);
            Color colour = new Color(Math.Min(light.X, 1f), Math.Min(light.Y, 1f), Math.Min(light.Z, 1f)) * npc.Opacity;
            Vector2 scale = new Vector2(npc.Scale * Math.Max(Math.Abs(face), 0.18f), npc.Scale);

            batch.Draw(atlas, npc.Center - screenPosition, frame, colour, npc.Rotation, frame.Size() * 0.5f, scale,
                face > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
        }

        /// <summary>Two: their bodies' own light, all of them in one go (the one picture); then the glows about them (the soft dot).</summary>
        public override int GlowPasses => 2;

        public override void DrawGlow(SpriteBatch batch, ref PseudoNPC npc, Vector2 screenPosition, int pass)
        {
            if (!SchoolFishLooks.Ready(out Texture2D atlas) || !CheckActive(ref npc))
                return;

            ref VastOceanSchools.School school = ref VastOceanSchools.Schools[npc.Group];
            float face = Face(ref npc);

            // Its flank catching the light, turning; and its own light in the dark, a wave of it through the school.
            float flash = (1f - Math.Abs(face)) * 0.45f * Math.Max(school.Sun, 0.25f);
            float wave = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.4f - npc.Center.X * 0.04f + npc.Center.Y * 0.02f);
            float own = school.Dark * 0.34f * wave;
            float add = (flash + own) * npc.Opacity;
            Vector2 at = npc.Center - screenPosition;

            if (pass == 0)
            {
                if (add <= 0.02f)
                    return;

                Rectangle frame = SchoolFishLooks.Frame(Type, npc.Variant);
                Vector2 scale = new Vector2(npc.Scale * Math.Max(Math.Abs(face), 0.18f), npc.Scale);

                batch.Draw(atlas, at, frame, new Color(add, add, add, 0f), npc.Rotation, frame.Size() * 0.5f, scale,
                    face > 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                return;
            }

            float halo = school.Dark * 0.11f * wave * npc.Opacity;

            if (halo > 0.01f && VastOceanSchools.Dot(out Texture2D dot))
                batch.Draw(dot, at, null, VastOceanSchools.Lit(SchoolFishLooks.Glow(Type, npc.Variant), halo), 0f, dot.Size() * 0.5f, 22f / dot.Width, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// The school fish's pictures in all their colours, in one picture (so a whole sea of them is drawn
    /// in one go): made from their own files the first time one is drawn, each kind's colours one under
    /// another, with a row of nothing between; and each colour's own light (<see cref="Glow"/>), the
    /// picture's colours averaged and brightened, what it gives in the dark.
    /// </summary>
    public static class SchoolFishLooks
    {
        private static Texture2D atlas;
        private static bool failed;
        private static Rectangle[][] frames;
        private static Color[][] glows;

        public static void Unload()
        {
            if (atlas != null)
            {
                Texture2D made = atlas;
                Main.QueueMainThreadAction(() => made.Dispose());
            }

            atlas = null;
            frames = null;
            glows = null;
            failed = false;
        }

        public static Rectangle Frame(int type, int variant)
        {
            Rectangle[] own = frames?[type];
            return own == null || own.Length == 0 ? Rectangle.Empty : own[Math.Clamp(variant, 0, own.Length - 1)];
        }

        /// <summary>A colour's own light; till the pictures are made, a pale blue.</summary>
        public static Color Glow(int type, int variant)
        {
            Color[] own = glows?[type];
            return own == null || own.Length == 0 ? new Color(140, 220, 255) : own[Math.Clamp(variant, 0, own.Length - 1)];
        }

        /// <summary>The pictures, made now if not yet (on the thread that draws).</summary>
        public static bool Ready(out Texture2D picture)
        {
            if (atlas == null && !failed)
                Make();

            picture = atlas;
            return atlas != null;
        }

        private static void Make()
        {
            failed = true;
            int types = 0;

            foreach (SchoolFish kind in SchoolFish.All)
                types = Math.Max(types, kind.Type + 1);

            var made = new List<(int type, int variant, Color[] texels, int w, int h)>();
            int wide = 1, high = 0;

            foreach (SchoolFish kind in SchoolFish.All)
            {
                Texture2D own = ModContent.Request<Texture2D>(kind.Texture, AssetRequestMode.ImmediateLoad).Value;
                Color[] texels = new Color[own.Width * own.Height];
                own.GetData(texels);

                int w = own.Width, h = own.Height / Math.Max(1, kind.Frames);
                SchoolFish.Variant[] variants = kind.Variants;

                for (int v = 0; v < variants.Length; v++)
                {
                    Color[] picture = new Color[w * h];
                    Array.Copy(texels, Math.Clamp(variants[v].Frame, 0, kind.Frames - 1) * w * h, picture, 0, w * h);
                    Apply(picture, w, h, kind.ArtPixel, variants[v].Rules);
                    made.Add((kind.Type, v, picture, w, h));
                    wide = Math.Max(wide, w + 2);
                    high += h + 2;
                }
            }

            if (made.Count == 0)
                return;

            Color[] all = new Color[wide * high];
            frames = new Rectangle[types][];
            glows = new Color[types][];

            foreach (SchoolFish kind in SchoolFish.All)
            {
                frames[kind.Type] = new Rectangle[kind.Variants.Length];
                glows[kind.Type] = new Color[kind.Variants.Length];
            }

            int y = 0;

            foreach ((int type, int variant, Color[] texels, int w, int h) in made)
            {
                for (int j = 0; j < h; j++)
                    Array.Copy(texels, j * w, all, (y + j) * wide, w);

                frames[type][variant] = new Rectangle(0, y, w, h);
                glows[type][variant] = GlowOf(texels);
                y += h + 2;
            }

            atlas = new Texture2D(Main.instance.GraphicsDevice, wide, high);
            atlas.SetData(all);
            failed = false;
        }

        /// <summary>The rules done to a picture's pixels (<see cref="Recolour"/>).</summary>
        private static void Apply(Color[] picture, int w, int h, int art, SchoolFish.Recolour[] rules)
        {
            if (rules == null || rules.Length == 0)
                return;

            // Where its fins are: the top and bottom rows of its own pixels, and its tail.
            int top = int.MaxValue, bottom = -1, left = int.MaxValue, right = -1;

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    if (picture[j * w + i].A == 0)
                        continue;

                    top = Math.Min(top, j / art);
                    bottom = Math.Max(bottom, j / art);
                    left = Math.Min(left, i / art);
                    right = Math.Max(right, i / art);
                }
            }

            float tail = left + 0.72f * (right - left + 1);

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    Color c = picture[j * w + i];

                    if (c.A == 0)
                        continue;

                    ToHsv(c, out float hue, out float saturation, out float value);

                    if (saturation < 0.12f || value < 0.4f)
                        continue;

                    bool fin = j / art == top || j / art == bottom || i / art >= tail;

                    foreach (SchoolFish.Recolour rule in rules)
                    {
                        if (rule.Part == SchoolFish.Part.Body && fin || rule.Part == SchoolFish.Part.Fins && !fin || !rule.Fits(hue))
                            continue;

                        picture[j * w + i] = FromHsv(hue + rule.Hue, saturation * rule.Saturation, value * rule.Value, c.A);
                        break;
                    }
                }
            }
        }

        /// <summary>A picture's colours averaged (but its dark ones), and brought up to full brightness, a little paler.</summary>
        private static Color GlowOf(Color[] texels)
        {
            Vector3 sum = Vector3.Zero;
            int count = 0;

            foreach (Color c in texels)
            {
                if (c.A == 0 || c.R + c.G + c.B < 150)
                    continue;

                sum += c.ToVector3();
                count++;
            }

            if (count == 0)
                return new Color(140, 220, 255);

            Vector3 mean = sum / count;
            float most = Math.Max(mean.X, Math.Max(mean.Y, mean.Z));
            mean = Vector3.Lerp(mean / Math.Max(most, 0.01f), Vector3.One, 0.18f);

            return new Color(mean);
        }

        private static void ToHsv(Color c, out float hue, out float saturation, out float value)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), span = max - min;

            value = max;
            saturation = max <= 0f ? 0f : span / max;

            if (span <= 0f)
                hue = 0f;
            else if (max == r)
                hue = 60f * (((g - b) / span) % 6f);
            else if (max == g)
                hue = 60f * ((b - r) / span + 2f);
            else
                hue = 60f * ((r - g) / span + 4f);

            if (hue < 0f)
                hue += 360f;
        }

        private static Color FromHsv(float hue, float saturation, float value, byte alpha)
        {
            hue = ((hue % 360f) + 360f) % 360f;
            saturation = MathHelper.Clamp(saturation, 0f, 1f);
            value = MathHelper.Clamp(value, 0f, 1f);

            float c = value * saturation;
            float x = c * (1f - Math.Abs(hue / 60f % 2f - 1f));
            float m = value - c;
            float r, g, b;

            if (hue < 60f) { r = c; g = x; b = 0f; }
            else if (hue < 120f) { r = x; g = c; b = 0f; }
            else if (hue < 180f) { r = 0f; g = c; b = x; }
            else if (hue < 240f) { r = 0f; g = x; b = c; }
            else if (hue < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }

            return new Color((byte)Math.Round((r + m) * 255f), (byte)Math.Round((g + m) * 255f), (byte)Math.Round((b + m) * 255f), alpha);
        }
    }

    // ---- the kinds -------------------------------------------------------------------------------------

    /// <summary>
    /// Anthias (Pseudanthias): bright orange and pink, gathered in shimmering clouds over the rock. Slow,
    /// fluttering; most in the upper sea, and three times as likely near the rock. Its two pictures (the
    /// orange and the pink, AnthiasVariants) and four more: golden, coral red, magenta and violet.
    /// </summary>
    public class Anthias : SchoolFish
    {
        protected override string File => "AnthiasVariants";

        public override int Frames => 2;

        public override Variant[] Variants => variants;

        private static readonly Variant[] variants =
        {
            new Variant(0),
            new Variant(1),
            new Variant(0, new Recolour(0f, 40f, Part.All, 13f, 1.05f, 1.06f)),
            new Variant(0, new Recolour(0f, 40f, Part.All, -9f, 1.12f, 0.98f)),
            new Variant(1, new Recolour(275f, 325f, Part.All, 22f, 1.25f, 0.96f)),
            new Variant(1, new Recolour(275f, 325f, Part.All, -38f, 1.1f, 0.95f)),
        };

        public override float CruiseSlow => 0.3f;
        public override float CruiseFast => 0.6f;
        public override float Swim => 1.3f;
        public override float Top => 1.9f;
        public override float Flee => 4.2f;
        public override float Steer => 0.06f;
        public override float[] Depths => depths;
        private static readonly float[] depths = { 3f, 2f };
        public override float NearRock => 3f;
    }

    /// <summary>
    /// Fusiliers: quick, silver-blue with a yellow tail, in dense schools moving all as one. The most
    /// common in open water. Its picture (Fusiliers) and four more: red-tailed,
    /// indigo, lagoon green, and silver.
    /// </summary>
    public class Fusilier : SchoolFish
    {
        protected override string File => "Fusiliers";

        public override Variant[] Variants => variants;

        private static readonly Variant[] variants =
        {
            new Variant(0),
            new Variant(0, new Recolour(40f, 130f, Part.All, -68f, 1.05f, 1f)),
            new Variant(0, new Recolour(165f, 215f, Part.All, 30f, 1.1f, 0.9f)),
            new Variant(0, new Recolour(165f, 215f, Part.All, -24f, 1f, 1f), new Recolour(40f, 130f, Part.All, -8f, 1.1f, 1.05f)),
            new Variant(0, new Recolour(165f, 215f, Part.All, 8f, 0.38f, 1.05f)),
        };

        public override float CruiseSlow => 0.55f;
        public override float CruiseFast => 0.9f;
        public override float Swim => 1.9f;
        public override float Top => 2.6f;
        public override float Flee => 5.2f;
        public override float Steer => 0.07f;
        public override float[] Depths => depths;
        private static readonly float[] depths = { 4f, 4f };
    }

    /// <summary>
    /// Pacific Creolefish (Paranthias colonus): sleek, in walls feeding on what drifts by, deeper than
    /// the others. Its picture (PacificCreolefish) and four more: silver-blue with its fins still red (as
    /// they are by day), salmon, wine, and dusk.
    /// </summary>
    public class PacificCreolefish : SchoolFish
    {
        protected override string File => "PacificCreolefish";

        public override Variant[] Variants => variants;

        private static readonly Variant[] variants =
        {
            new Variant(0),
            new Variant(0, new Recolour(300f, 30f, Part.Body, -145f, 0.42f, 1.02f)),
            new Variant(0, new Recolour(300f, 30f, Part.All, 14f, 0.92f, 1.05f)),
            new Variant(0, new Recolour(300f, 30f, Part.All, -10f, 1.05f, 0.8f)),
            new Variant(0, new Recolour(300f, 30f, Part.All, -32f, 0.8f, 0.98f)),
        };

        public override float CruiseSlow => 0.4f;
        public override float CruiseFast => 0.7f;
        public override float Swim => 1.6f;
        public override float Top => 2.2f;
        public override float Flee => 4.6f;
        public override float[] Depths => depths;
        private static readonly float[] depths = { 1f, 3f };
    }
}
