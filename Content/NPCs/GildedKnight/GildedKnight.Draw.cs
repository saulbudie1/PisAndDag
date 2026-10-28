using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.GildedKnight
{
    /// <summary>
    /// How he is drawn. The whole of him is painted once a frame into a picture of his own before
    /// anything goes on the screen (KnightCanvas): every piece in its order, his cloth between
    /// them, the near pieces laying a shadow on what is under them, and a second picture of only
    /// the light his blades give off, the pieces in front of a blade hiding its light as they
    /// hide the blade. Here the two are laid on the world: the body in the light where he stands,
    /// narrowed as he turns, cut off at the ground a driven blade goes into; the light over it,
    /// as light; and, round them, what his blades leave in the air behind them, the shapes he
    /// leaves when he moves fast, the long blade, the lancet's line, the ward of gold, the Sun
    /// lying where it fell, and at his end the body coming apart into light, the highest of him first.
    /// </summary>
    public partial class GildedKnight
    {
        // ============================================================
        // THIS MACHINE'S OWN, FOR THE DRAWING
        // ============================================================

        /// <summary>How much light each blade gives off now: what it has from his second half, from light drawn into it, from a flare.</summary>
        internal float sunLit, graceLit;

        /// <summary>The move draws light into this blade before it lets it go.</summary>
        private bool gatherSun, gatherGrace;

        /// <summary>A light running up a blade from its guard to its point: how far along (ticks), over how many; -1 none.</summary>
        private int sunGleam = -1, graceGleam = -1;
        private int sunGleamLength = 1, graceGleamLength = 1;

        /// <summary>The lancet's line: how much of it shows, whether it has stopped following them, a flash.</summary>
        private float lineShown;
        private bool lineLocked;
        private float lineFlash;

        /// <summary>Afterimages: ticks still to leave them, and how strongly they show.</summary>
        private int ghostTicks;
        private float ghostShown;

        /// <summary>Where the Sun went into the ground for the procession; where the long blade lay.</summary>
        private Vector2 plunge;
        private Vector2 longLanding;

        /// <summary>How much of the long blade's light is left, apart from the move's own clock (it goes if the move is broken off).</summary>
        private float longKept;

        /// <summary>The ward after a parry, as it shows.</summary>
        private float wardShown;

        /// <summary>The pose a tick ago (the Sun let fall leaves from where it was in his hand).</summary>
        private KnightRig.Pose before = KnightMoves.Rest;

        /// <summary>The reel's movement, made once when the reel begins.</summary>
        private Move reelMove;
        private int reelMadeFor = -1;

        private struct Snap
        {
            public KnightRig.Pose Pose;
            public Vector2 Feet;
            public int Facing;
            public float SunSwing, GraceSwing;
        }

        /// <summary>The last ticks of him, for the afterimages.</summary>
        private const int SnapCount = 14;
        private readonly Snap[] snaps = new Snap[SnapCount];
        private int snapNext;
        private int snapHave;

        /// <summary>The long blade, at its full length; the Grace's light reaches this far.</summary>
        public const float LongReach = 560f;

        /// <summary>A light run up a blade: from its guard to its point over so many ticks.</summary>
        public void Gleam(int blade, int ticks)
        {
            if (Main.dedServ)
                return;

            if (blade == KnightRig.Sun)
            {
                sunGleam = 0;
                sunGleamLength = Math.Max(4, ticks);
            }
            else
            {
                graceGleam = 0;
                graceGleamLength = Math.Max(4, ticks);
            }
        }

        /// <summary>A new state or move on this machine: what the last one was drawing lets go.</summary>
        private void Restarted()
        {
            gatherSun = false;
            gatherGrace = false;

            if (State != Act.Move || MoveId != Moves.Lancet)
                lineShown = 0f;
        }

        /// <summary>The colour each blade's light is: the Sun warm gold and then fire, the Grace pale gold and then white.</summary>
        internal Color SunShine => KnightFx.BladeLight(KnightRig.Sun, second) * MathHelper.Clamp(sunLit, 0f, 1.6f);
        internal Color GraceShine => KnightFx.BladeLight(KnightRig.Grace, second) * MathHelper.Clamp(graceLit, 0f, 1.6f);

        // ============================================================
        // EVERY TICK, ON A MACHINE THAT DRAWS HIM
        // ============================================================

        /// <summary>
        /// What his look does from tick to tick: the light coming into and going out of his blades,
        /// the gleams running up them, the lancet's line, the afterimages kept, the long blade, the
        /// ward, and the fire and light coming off the blades in his second half.
        /// </summary>
        private void Looks()
        {
            if (Main.dedServ)
                return;

            KnightRig.Frame f = KnightRig.Build(Pose, Feet, facing, null, SunSwing, GraceSwing);

            // ---- the blades' light
            float shine = 0f;

            if (State == Act.Scene)
            {
                int d = Timer - DrawFrom;

                if (d >= KnightMoves.DrawCrossed && d < KnightMoves.DrawParted + 30)
                    shine = 0.55f;
            }

            bool fighting = State != Act.Post && State != Act.Leave && State != Act.End;
            float sunWant = (second && fighting ? 0.75f : 0f) + (gatherSun && State == Act.Move ? 1f : 0f) + shine;
            float graceWant = (second && fighting ? 0.75f : 0f) + (gatherGrace && State == Act.Move ? 1f : 0f) + shine;

            if (State == Act.End)
            {
                sunWant = 0f;
                graceWant = second ? 0.5f * (1f - Dissolve) : 0f;
            }

            sunLit = sunLit < sunWant ? Math.Min(sunWant, sunLit + 0.06f) : Math.Max(sunWant, sunLit - 0.035f);
            graceLit = graceLit < graceWant ? Math.Min(graceWant, graceLit + 0.06f) : Math.Max(graceWant, graceLit - 0.035f);

            if (sunGleam >= 0 && ++sunGleam > sunGleamLength)
                sunGleam = -1;

            if (graceGleam >= 0 && ++graceGleam > graceGleamLength)
                graceGleam = -1;

            // ---- the lancet's line, while the thrust is coming
            if (State == Act.Move && MoveId == Moves.Lancet && lineShown > 0f && Timer < KnightMoves.LancetThrust)
                lineShown = Math.Min(1f, lineShown + 0.08f);
            else
                lineShown = Math.Max(0f, lineShown - 0.12f);

            lineFlash *= 0.86f;

            // ---- the afterimages
            if (ghostTicks > 0)
            {
                ghostTicks--;
                ghostShown = Math.Min(1f, ghostShown + 0.25f);
            }
            else
            {
                ghostShown = Math.Max(0f, ghostShown - 0.07f);
            }

            snaps[snapNext] = new Snap { Pose = Pose, Feet = Feet, Facing = facing, SunSwing = SunSwing, GraceSwing = GraceSwing };
            snapNext = (snapNext + 1) % SnapCount;
            snapHave = Math.Min(SnapCount, snapHave + 1);

            // ---- the long blade's light, kept while the move it belongs to is on
            longKept = State == Act.Move && MoveId == Moves.Long ? 1f : Math.Max(0f, longKept - 0.08f);

            // ---- the ward
            wardShown = State == Act.Reel ? 1f - Timer / (float)KnightMoves.ReelTicks : Math.Max(0f, wardShown - 0.1f);

            if (State == Act.Reel && Main.rand.NextBool(3))
                KnightFx.Motes(Chest + Main.rand.NextVector2Circular(30f, 46f), 1, 4f, KnightFx.Gold);

            // ---- fire and light off the blades in his second half
            if (second && fighting && NPC.alpha < 200)
            {
                if (f.SunOut && Main.rand.NextBool(2))
                {
                    Vector2 at = Vector2.Lerp(f.SunGuard, f.SunTip, Main.rand.NextFloat(0.15f, 1f));
                    PADParticleHandler.SpawnParticle(new Particles.Ember(at, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.6f, 1.6f)),
                        Main.rand.NextBool(3) ? KnightFx.Fire : KnightFx.Ember, Main.rand.NextFloat(0.4f, 0.8f), Main.rand.Next(20, 40), 0.06f));
                }

                if (f.GraceOut && Main.rand.NextBool(5))
                {
                    Vector2 at = Vector2.Lerp(f.GraceGuard, f.GraceTip, Main.rand.NextFloat(0.15f, 1f));
                    PADParticleHandler.SpawnParticle(new GraceMote(at, KnightFx.White, Main.rand.Next(30, 60)));
                }
            }

            // ---- the Sun where it fell, going into light with him
            if (dropped && State == Act.End && Dissolve > 0.5f && Dissolve < 0.98f && Main.rand.NextBool(2))
            {
                Vector2 along = droppedAngle.ToRotationVector2();
                Vector2 at = droppedGrip + along * Main.rand.NextFloat(0f, KnightRig.GripToTip);
                PADParticleHandler.SpawnParticle(new GraceMote(at, KnightFx.Pale, Main.rand.Next(60, 110)));
            }

            // ---- the procession: fire welling up where the Sun is in the ground
            if (State == Act.Move && MoveId == Moves.Procession && Timer > KnightMoves.ProcessionPlunge && Timer < KnightMoves.ProcessionHold && Main.rand.NextBool(2))
                KnightFx.Embers(plunge + new Vector2(0f, -6f), 1, 8f, 2.2f);

            // ---- the light of it, on the world
            if (f.SunOut && sunLit > 0.05f)
                Lighting.AddLight(Vector2.Lerp(f.SunGuard, f.SunTip, 0.6f), KnightFx.BladeLight(KnightRig.Sun, second).ToVector3() * 0.6f * Math.Min(1f, sunLit));

            if (f.GraceOut && graceLit > 0.05f)
                Lighting.AddLight(Vector2.Lerp(f.GraceGuard, f.GraceTip, 0.6f), KnightFx.BladeLight(KnightRig.Grace, second).ToVector3() * 0.6f * Math.Min(1f, graceLit));

            float longLight = LongShown();

            if (longLight > 0.05f)
            {
                Vector2 end = LongEnd(f, LongLength(), out _);

                for (float s = 0.1f; s <= 1f; s += 0.15f)
                    Lighting.AddLight(Vector2.Lerp(f.GraceGuard, end, s), new Vector3(1f, 0.95f, 0.8f) * longLight);
            }
        }

        // ============================================================
        // THE LONG BLADE
        // ============================================================

        /// <summary>How long the long blade is at this moment of its move: grown out of the Grace while it is raised, then all of it.</summary>
        private float LongLength(float clock = -1f)
        {
            if (clock < 0f)
                clock = Timer - 1f;

            if (State != Act.Move || MoveId != Moves.Long || clock < KnightMoves.LongGrow)
                return 0f;

            float grow = (clock - KnightMoves.LongGrow) / (KnightMoves.LongRaised - KnightMoves.LongGrow);
            grow = MathHelper.Clamp(grow, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);

            return LongReach * grow;
        }

        /// <summary>
        /// How much of the long blade's light shows: all of it from its growing to the sweep; going as he
        /// lifts it off the ground to wind the sweep (it is not swung round behind him), and the sweep's
        /// own light (KnightSweep) runs out along the ground in its place.
        /// </summary>
        private float LongShown()
        {
            if (State != Act.Move || MoveId != Moves.Long)
                return longKept * 0.5f;

            int t = Timer - 1;

            if (t < KnightMoves.LongGrow)
                return 0f;

            int goneFrom = KnightMoves.LongSweepFrom;

            if (t < goneFrom)
                return longKept;

            return longKept * MathHelper.Clamp(1f - (t - goneFrom) / 5f, 0f, 1f);
        }

        /// <summary>
        /// Where the long blade ends: from the Grace's guard along the Grace, so long; and, where
        /// that would go into the ground, lying along the ground ahead instead, its point on it.
        /// </summary>
        private Vector2 LongEnd(KnightRig.Frame f, float length, out float angle)
        {
            Vector2 from = f.GraceGuard;
            Vector2 way = f.GraceBlade.ToRotationVector2();
            float ground = Feet.Y - 2f;

            if (length > 1f && from.Y + way.Y * length > ground)
            {
                float drop = Math.Max(0f, ground - from.Y);
                float s = MathHelper.Clamp(drop / length, 0f, 1f);
                float c = (float)Math.Sqrt(1f - s * s);
                int side = Math.Abs(way.X) < 0.05f ? facing : Math.Sign(way.X);

                way = new Vector2(side * c, s);
            }

            angle = way.ToRotation();
            return from + way * length;
        }

        /// <summary>Where the long blade's point is on the ground when it is down (the pillar's place).</summary>
        private Vector2 LongLanding(KnightRig.Frame f)
        {
            Vector2 end = LongEnd(f, LongReach, out _);
            float ground = GroundUnder(new Vector2(end.X, end.Y - 40f), out bool found);

            return new Vector2(end.X, found ? ground : Feet.Y);
        }

        // ============================================================
        // DRAWING
        // ============================================================

        /// <summary>The light added: a colour with its alpha zeroed draws as light over what is there.</summary>
        private static Color Lit(Color c, float strength) => new Color(c.R, c.G, c.B, 0) * strength;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (NPC.IsABestiaryIconDummy || gone)
                return false;

            // Up a step: drawn where he was, settling onto it over a few ticks, as the game draws any
            // NPC; everything of his drawn with him (Lift), so nothing of it comes away from him.
            screenPos.Y -= NPC.gfxOffY;

            // Called back by the spur, he is there from under the black of his scene's fade; not before.
            if (called && State == Act.Scene && Timer < FadeOut - 2)
                return false;

            float shown = 1f - NPC.alpha / 255f;

            if (shown <= 0.01f)
                return false;

            KnightRig.Frame f = KnightRig.Build(Pose, Feet + Lift, facing, null, SunSwing, GraceSwing);
            Color light = drawColor * shown;

            if (lineShown > 0.01f)
                DrawLancetLine(spriteBatch, f, shown);

            if (ghostShown > 0.01f)
                DrawGhosts(spriteBatch, screenPos, shown);

            if (dropped)
                DrawDropped(spriteBatch, screenPos, shown);

            DrawBody(spriteBatch, screenPos, f, light, shown);

            // What the blades leave behind them, the long blade, the ward: light, over him.
            spriteBatch.End();

            if (Dissolve < 0.5f)
            {
                DrawTrail(KnightRig.Sun, shown);
                DrawTrail(KnightRig.Grace, shown);
            }

            Open(spriteBatch);

            DrawLong(spriteBatch, f, shown);
            DrawGleams(spriteBatch, f, shown);
            DrawEye(spriteBatch, f, shown);
            DrawWard(spriteBatch, shown);

            return false;
        }

        /// <summary>The sprite batch opened again as the game had it for NPCs (Main.DoDraw_DrawNPCsOverTiles, 1.4.4).</summary>
        private static void Open(SpriteBatch spriteBatch, Matrix? view = null)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null, view ?? Main.Transform);
        }

        // ---- the body ----

        /// <summary>
        /// His picture laid on the world: the body in the light where he stands, its light over it
        /// as light; narrowed about the line through his feet as he turns; cut off at the ground
        /// under him. At his end, in squares that lift off as light, the highest first.
        /// </summary>
        private void DrawBody(SpriteBatch spriteBatch, Vector2 screenPos, KnightRig.Frame f, Color light, float shown)
        {
            Texture2D body = KnightCanvas.Body(NPC.whoAmI);
            Texture2D glow = KnightCanvas.Light(NPC.whoAmI);

            if (body == null)
            {
                DrawPlain(spriteBatch, screenPos, light);
                return;
            }

            // Down to the ground under him and two rows into it; in the air, all of it.
            float ground = GroundUnder(new Vector2(Feet.X, Feet.Y - 8f), out bool found);
            int below = found ? (int)Math.Round(ground - Feet.Y) + 2 : KnightCanvas.Below;
            below = Math.Clamp(below, 0, KnightCanvas.Below);

            Rectangle source = new Rectangle(0, 0, KnightCanvas.Width, (int)KnightCanvas.Origin.Y + below);
            Vector2 at = Feet - screenPos;
            Vector2 scale = new Vector2(Math.Max(KnightRig.Thin, f.Turn), 1f);

            float dissolve = Dissolve;

            if (dissolve <= 0f)
            {
                spriteBatch.Draw(body, at, source, light, 0f, KnightCanvas.Origin, scale, SpriteEffects.None, 0f);

                if (glow != null)
                    spriteBatch.Draw(glow, at, source, new Color(255, 255, 255, 0) * shown, 0f, KnightCanvas.Origin, scale, SpriteEffects.None, 0f);

                // The ward: a sheen of gold over all of him while it holds.
                if (wardShown > 0.01f)
                    spriteBatch.Draw(body, at, source, Lit(KnightFx.Gold, 0.45f * wardShown * shown), 0f, KnightCanvas.Origin, scale, SpriteEffects.None, 0f);

                return;
            }

            // From the top of him (his plume over his crown) down: his kneeling height, not the canvas's.
            float top = KnightCanvas.Origin.Y + (f.CrownAt.Y - (Feet.Y + Lift.Y)) - 24f;

            DrawDissolving(spriteBatch, body, glow, at, source, scale, light, shown, dissolve, top);
        }

        /// <summary>How far under where he stands he is drawn, settling up a step he has just walked up (the game's gfxOffY).</summary>
        private Vector2 Lift => new Vector2(0f, NPC.gfxOffY);

        /// <summary>The size of the squares he comes apart in, in pixels of his picture.</summary>
        private const int Bit = 4;

        /// <summary>
        /// When each square goes, on the dissolve's way from 0 to 1: the top of him at once, the
        /// soles last, with chance in it; each lifting off over Lifts. The latest is gone at
        /// DissolveFirst + DissolveDown + DissolveChance + Lifts, which is under 1.
        /// </summary>
        private const float DissolveFirst = 0.03f;
        private const float DissolveDown = 0.5f;
        private const float DissolveChance = 0.22f;
        private const float Lifts = 0.22f;

        /// <summary>
        /// Coming apart into light: his picture in small squares; each, at its moment (the higher
        /// on him the sooner, with chance in it), first brightens to gold, then lifts off drifting
        /// up and sideways as light and fades; what is still on him glows more and more, gold
        /// going to white.
        /// </summary>
        private void DrawDissolving(SpriteBatch spriteBatch, Texture2D body, Texture2D glow, Vector2 at, Rectangle source, Vector2 scale, Color light, float shown, float dissolve, float top)
        {
            // Only the squares about him: from over his head to his feet, a body's width either side.
            // (Anything of the picture over the top of him goes with the top of him.)
            int x0 = (int)KnightCanvas.Origin.X - 90, x1 = (int)KnightCanvas.Origin.X + 90;
            int y1 = source.Bottom;
            int y0 = (int)MathHelper.Clamp(top, 0f, y1 - 24f);
            int yStart = Math.Max(0, y0 - 40);
            float span = Math.Max(1f, y1 - y0);

            for (int y = yStart; y < y1; y += Bit)
            {
                for (int x = x0; x < x1; x += Bit)
                {
                    // The highest first: the share of the way up him, and a little chance. The last
                    // square has lifted off (when + Lifts) just as the dissolve comes to its end.
                    float high = MathHelper.Clamp(1f - (y - y0) / span, 0f, 1f);
                    float rnd = Hash(x, y);
                    float when = DissolveFirst + DissolveDown * (1f - high) + DissolveChance * rnd;

                    Rectangle bit = new Rectangle(x, y, Bit, Math.Min(Bit, y1 - y));
                    Vector2 local = new Vector2(x - KnightCanvas.Origin.X, y - KnightCanvas.Origin.Y) * scale;
                    Vector2 place = at + local;

                    float into = (dissolve - when) / Lifts;

                    if (into <= 0f)
                    {
                        // Still on him, warming to gold as his end comes.
                        float warm = MathHelper.Clamp(dissolve * 1.6f, 0f, 1f);
                        spriteBatch.Draw(body, place, bit, light, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                        spriteBatch.Draw(body, place, bit, Lit(Color.Lerp(KnightFx.Gold, KnightFx.White, warm), 0.55f * warm * shown), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                        if (glow != null)
                            spriteBatch.Draw(glow, place, bit, new Color(255, 255, 255, 0) * shown, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                        continue;
                    }

                    if (into >= 1f)
                        continue;

                    // Lifting off: up, drifting, light only, fading.
                    float lift = into * into * 46f + into * 10f;
                    float drift = (rnd - 0.5f) * 30f * into + (float)Math.Sin(rnd * 40f + into * 6f) * 3f;
                    Color c = Lit(Color.Lerp(KnightFx.Gold, KnightFx.White, into), (1f - into) * 1.2f * shown);

                    spriteBatch.Draw(body, place + new Vector2(drift, -lift), bit, c, 0f, Vector2.Zero, scale * (1f - 0.4f * into), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>A number from 0 to 1 for a square of his picture, the same every frame.</summary>
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>His pieces drawn straight on the world, in order, with nothing more: for a frame his own picture is not ready for (his first).</summary>
        private void DrawPlain(SpriteBatch spriteBatch, Vector2 screenPos, Color light)
        {
            var parts = new System.Collections.Generic.List<KnightRig.Part>(KnightRig.Pieces);
            KnightRig.Frame f = KnightRig.Build(Pose, Feet, facing, parts, SunSwing, GraceSwing);

            DrawParts(spriteBatch, parts, f, screenPos, light);
        }

        /// <summary>Pieces laid on the world in a colour, narrowed with him as he turns (through a view of their own if he is).</summary>
        private static void DrawParts(SpriteBatch spriteBatch, System.Collections.Generic.List<KnightRig.Part> parts, KnightRig.Frame f, Vector2 screenPos, Color colour)
        {
            bool turned = Math.Abs(f.Turn - 1f) > 0.001f;

            if (turned)
            {
                spriteBatch.End();
                Open(spriteBatch, Narrowed(f.Turn, f.TurnAbout - screenPos.X));
            }

            foreach (KnightRig.Part p in parts)
            {
                Texture2D tex = KnightCanvas.Piece(p.Piece);

                if (tex == null)
                    continue;

                spriteBatch.Draw(tex, p.At - screenPos, null, colour, p.Rotation, p.Origin, p.Scale,
                    p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }

            if (turned)
            {
                spriteBatch.End();
                Open(spriteBatch);
            }
        }

        /// <summary>The game's view, with what is drawn through it narrowed about an upright line (screen x) first.</summary>
        private static Matrix Narrowed(float wide, float aboutX)
        {
            return Matrix.CreateTranslation(-aboutX, 0f, 0f)
                * Matrix.CreateScale(Math.Max(KnightRig.Thin, wide), 1f, 1f)
                * Matrix.CreateTranslation(aboutX, 0f, 0f)
                * Main.Transform;
        }

        // ---- the afterimages ----

        /// <summary>The shapes he leaves when he moves fast (the dash, the leap, the wheel): him a few ticks ago, as gold light, fading.</summary>
        private static readonly System.Collections.Generic.List<KnightRig.Part> ghostParts = new System.Collections.Generic.List<KnightRig.Part>(KnightRig.Pieces);

        private void DrawGhosts(SpriteBatch spriteBatch, Vector2 screenPos, float shown)
        {
            var parts = ghostParts;
            Color tint = second ? new Color(255, 170, 90) : new Color(255, 214, 140);

            for (int k = 4; k >= 1; k--)
            {
                int back = k * 2;

                if (back >= snapHave)
                    continue;

                Snap s = snaps[(snapNext - 1 - back + SnapCount * 2) % SnapCount];

                // Only where he has gone from: nothing left standing in him.
                if (Vector2.Distance(s.Feet, Feet) < 10f && Math.Abs(s.Pose.Turn - Pose.Turn) < 0.2f)
                    continue;

                parts.Clear();
                KnightRig.Frame f = KnightRig.Build(s.Pose, s.Feet, s.Facing, parts, s.SunSwing, s.GraceSwing);

                float strength = ghostShown * shown * 0.3f * (1f - (k - 1) / 4.5f);
                DrawParts(spriteBatch, parts, f, screenPos, Lit(tint, strength));
            }
        }

        // ---- the Sun, let fall ----

        private void DrawDropped(SpriteBatch spriteBatch, Vector2 screenPos, float shown)
        {
            Texture2D tex = KnightCanvas.Piece(KnightRig.SunBlade);
            Texture2D glow = KnightCanvas.Tex(KnightRig.Folder + KnightRig.SunGlow);

            if (tex == null)
                return;

            KnightRig.Part p = KnightRig.LooseSword(KnightRig.Sun, droppedGrip, droppedAngle);

            // It goes into light with him, a little after the rest of him.
            float left = 1f - MathHelper.Clamp((Dissolve - 0.55f) / 0.4f, 0f, 1f);
            shown *= left;

            if (shown <= 0.01f)
                return;

            Color light = Lighting.GetColor(droppedGrip.ToTileCoordinates()) * shown;

            // Down, its point in the turf: cut off at the ground as he is.
            spriteBatch.Draw(tex, p.At - screenPos, null, light, p.Rotation, p.Origin, p.Scale, p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            float ember = second ? 0.5f * (1f - Dissolve) : 0f;

            if (glow != null && ember > 0.01f)
                spriteBatch.Draw(glow, p.At - screenPos, null, Lit(KnightFx.Fire, ember * shown), p.Rotation, p.Origin, p.Scale, p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            // Going: white light over it as it goes.
            float going = 1f - left;

            if (glow != null && going > 0.01f)
            {
                spriteBatch.Draw(tex, p.At - screenPos, null, Lit(KnightFx.Pale, going * 1.4f * shown), p.Rotation, p.Origin, p.Scale, p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, p.At - screenPos, null, Lit(KnightFx.White, going * shown), p.Rotation, p.Origin, p.Scale, p.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }

        // ---- the lancet's line ----

        /// <summary>
        /// The way the lancet will go: a hair of pale gold from the Grace's point along the line
        /// the fight aims (to the first wall), flickering while it follows them, steady and
        /// brighter once it is still, a flash as it stops.
        /// </summary>
        private void DrawLancetLine(SpriteBatch spriteBatch, KnightRig.Frame f, float shown)
        {
            Texture2D line = KnightCanvas.Tex("PisAndDag/Content/Graphics/SoftLine");

            if (line == null)
                return;

            float world = facing > 0 ? arg : MathHelper.Pi - arg;
            Vector2 way = world.ToRotationVector2();
            Vector2 from = f.GraceTip;
            float length = 1600f;

            for (float d = 16f; d < 1600f; d += 8f)
            {
                Point tile = (from + way * d).ToTileCoordinates();

                if (!WorldGen.InWorld(tile.X, tile.Y, 2) || WorldGen.SolidTile(tile.X, tile.Y))
                {
                    length = d;
                    break;
                }
            }

            float flicker = lineLocked ? 1f : 0.55f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f);
            float a = lineShown * flicker * shown;
            Vector2 to = from + way * length;

            DrawStretched(spriteBatch, line, from, to, (lineLocked ? 5f : 3f) + 6f * lineFlash, Lit(KnightFx.Gold, (0.4f + 0.6f * lineFlash) * a));
            DrawStretched(spriteBatch, line, from, to, 1.6f, Lit(KnightFx.White, 0.9f * a));
        }

        /// <summary>A texture laid from one point to another, so thick.</summary>
        private static void DrawStretched(SpriteBatch spriteBatch, Texture2D tex, Vector2 from, Vector2 to, float thickness, Color colour)
        {
            Vector2 d = to - from;
            float length = d.Length();

            if (length < 0.5f)
                return;

            spriteBatch.Draw(tex, from - Main.screenPosition, null, colour, d.ToRotation(), new Vector2(0f, tex.Height * 0.5f),
                new Vector2(length / tex.Width, thickness / tex.Height), SpriteEffects.None, 0f);
        }

        private static void DrawSoft(SpriteBatch spriteBatch, Vector2 at, float across, Color colour)
        {
            Texture2D tex = KnightCanvas.Tex("PisAndDag/Content/Graphics/GlowSoft");

            if (tex != null)
                spriteBatch.Draw(tex, at - Main.screenPosition, null, colour, 0f, tex.Size() * 0.5f, across / tex.Width, SpriteEffects.None, 0f);
        }

        private static void DrawStar(SpriteBatch spriteBatch, Vector2 at, float across, float rotation, Color colour)
        {
            Texture2D tex = KnightCanvas.Tex("PisAndDag/Content/Graphics/GlowStar");

            if (tex != null)
                spriteBatch.Draw(tex, at - Main.screenPosition, null, colour, rotation, tex.Size() * 0.5f, across / tex.Width, SpriteEffects.None, 0f);
        }

        // ---- the long blade ----

        /// <summary>The long blade: a blade of light out of the Grace, as long as the sky is high, gold about a white core, brightest at its point.</summary>
        private void DrawLong(SpriteBatch spriteBatch, KnightRig.Frame f, float shown)
        {
            float a = LongShown() * shown;
            float length = LongLength();

            if (a <= 0.01f || length < 4f)
                return;

            Texture2D beam = KnightCanvas.Tex("PisAndDag/Content/Graphics/SoftBeam");
            Texture2D line = KnightCanvas.Tex("PisAndDag/Content/Graphics/SoftLine");

            if (beam == null || line == null)
                return;

            Vector2 from = f.GraceGuard;
            Vector2 end = LongEnd(f, length, out _);
            float pulse = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);

            DrawStretched(spriteBatch, beam, from, end, 46f * pulse, Lit(KnightFx.Gold, 0.45f * a));
            DrawStretched(spriteBatch, beam, from, end, 20f, Lit(KnightFx.Pale, 0.85f * a));
            DrawStretched(spriteBatch, line, from, end, 6f, Lit(KnightFx.White, a));
            DrawSoft(spriteBatch, end, 120f, Lit(KnightFx.White, 0.6f * a));
            DrawSoft(spriteBatch, from, 90f, Lit(KnightFx.Gold, 0.5f * a));
            DrawStar(spriteBatch, end, 60f * pulse, Main.GlobalTimeWrappedHourly * 2f, Lit(KnightFx.White, 0.8f * a));
        }

        // ---- the gleams, the eye, the ward ----

        /// <summary>A light running up each blade that has one: a soft spot and a star where it is.</summary>
        private void DrawGleams(SpriteBatch spriteBatch, KnightRig.Frame f, float shown)
        {
            if (sunGleam >= 0 && f.SunOut)
            {
                float s = sunGleam / (float)sunGleamLength;
                Vector2 at = Vector2.Lerp(f.SunGuard, f.SunTip, s);
                float a = (float)Math.Sin(s * MathHelper.Pi) * shown;

                DrawSoft(spriteBatch, at, 36f, Lit(KnightFx.BladeLight(KnightRig.Sun, second), 0.8f * a));
                DrawStar(spriteBatch, at, 26f, f.SunBlade, Lit(KnightFx.White, a));
            }

            if (graceGleam >= 0 && f.GraceOut)
            {
                float s = graceGleam / (float)graceGleamLength;
                Vector2 at = Vector2.Lerp(f.GraceGuard, f.GraceTip, s);
                float a = (float)Math.Sin(s * MathHelper.Pi) * shown;

                DrawSoft(spriteBatch, at, 36f, Lit(KnightFx.BladeLight(KnightRig.Grace, second), 0.8f * a));
                DrawStar(spriteBatch, at, 26f, f.GraceBlade, Lit(KnightFx.White, a));
            }
        }

        /// <summary>The light in the slit of his visor: gold, brighter and paler in his second half; going out at his end.</summary>
        private void DrawEye(SpriteBatch spriteBatch, KnightRig.Frame f, float shown)
        {
            float a = (second ? 0.95f : 0.6f) * shown * (State == Act.End ? Math.Max(0f, 1f - (Timer - KnightMoves.EndKnees) / 60f) : 1f);
            a *= 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + NPC.whoAmI);

            if (a <= 0.01f)
                return;

            Color c = second ? new Color(255, 236, 190) : KnightFx.Gold;
            DrawSoft(spriteBatch, f.EyeAt, 16f, Lit(c, 0.6f * a));
            DrawStar(spriteBatch, f.EyeAt, 9f, 0f, Lit(KnightFx.White, 0.7f * a));
        }

        /// <summary>The ward of gold about him after a parry: a ring turning, a glow, while it holds.</summary>
        private void DrawWard(SpriteBatch spriteBatch, float shown)
        {
            if (wardShown <= 0.01f)
                return;

            Texture2D ring = KnightCanvas.Tex(KnightRig.Folder + "KnightRing");

            if (ring == null)
                return;

            float a = wardShown * shown;
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 chest = Chest + Lift;
            Vector2 at = chest - Main.screenPosition;

            for (int k = 0; k < 2; k++)
            {
                float across = (118f + 10f * k) * (1f + 0.06f * (float)Math.Sin(t * 6f + k));
                spriteBatch.Draw(ring, at, null, Lit(k == 0 ? KnightFx.Gold : KnightFx.Pale, (0.5f - 0.2f * k) * a), t * (k == 0 ? 1.4f : -1f), ring.Size() * 0.5f,
                    new Vector2(across / ring.Width, across * 1.25f / ring.Height), SpriteEffects.None, 0f);
            }

            DrawSoft(spriteBatch, chest, 180f, Lit(KnightFx.Gold, 0.25f * a));
        }

        // ============================================================
        // WHAT THE BLADES LEAVE BEHIND THEM
        // ============================================================

        private const int TrailSteps = 12;

        /// <summary>How far back a trail reaches, in ticks of his move's clock.</summary>
        private const float TrailBack = 7f;

        // Laid out again each frame it is drawn, only on the main thread.
        private static readonly Vector2[] TrailIn = new Vector2[TrailSteps + 1];
        private static readonly Vector2[] TrailOut = new Vector2[TrailSteps + 1];
        private static readonly Vector2[] TrailEdge = new Vector2[TrailSteps + 1];
        private static readonly float[] TrailStrength = new float[TrailSteps + 1];

        /// <summary>
        /// Where a blade swept these last ticks, as a band from a fifth of the way up it to its
        /// point, strongest where it is now and only where it went fast: a breath of shade under
        /// it so it shows against the bright sky, the blade's light over that, and a bright line
        /// along the way its point went. The Sun's gold (then fire), the Grace's pale gold (then white).
        /// </summary>
        private void DrawTrail(int blade, float shown)
        {
            if (!TrailPoints(blade, shown))
                return;

            Texture2D smear = KnightCanvas.Tex("PisAndDag/Content/Graphics/Smear");
            Texture2D line = KnightCanvas.Tex("PisAndDag/Content/Graphics/SoftLine");

            if (smear == null || line == null)
                return;

            Color colour = blade == KnightRig.Sun ? (second ? new Color(255, 140, 50) : new Color(255, 200, 110)) : (second ? new Color(255, 252, 236) : new Color(255, 236, 190));
            Color shade = blade == KnightRig.Sun ? new Color(110, 50, 16) : new Color(96, 76, 40);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color dark = new Color(shade.R, shade.G, shade.B, 255) * (0.22f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], dark, dark, i / (float)TrailSteps);
            }
            Strip.Draw(smear);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color bright = Lit(colour, 0.7f * TrailStrength[i]);
                Strip.Pair(TrailIn[i], TrailOut[i], bright, bright, i / (float)TrailSteps);
            }
            Strip.Draw(smear);

            Strip.Begin();
            for (int i = 0; i <= TrailSteps; i++)
            {
                Color pale = Lit(Color.White, 0.9f * TrailStrength[i]);
                Strip.Pair(TrailEdge[i], TrailOut[i], pale, pale, i / (float)TrailSteps);
            }
            Strip.Draw(line);
        }

        /// <summary>The trail's points for a blade, newest first; false when there is nothing of it to draw.</summary>
        private bool TrailPoints(int blade, float shown)
        {
            float clock = Timer - 1;
            bool any = false;
            Vector2 lastTip = Vector2.Zero;

            for (int i = 0; i <= TrailSteps; i++)
            {
                float ago = TrailBack * i / TrailSteps;
                TrailStrength[i] = 0f;

                if (!PoseBack(clock - ago, out KnightRig.Pose pose, out Vector2 feet))
                {
                    // Nothing further back: the rest of the band lies where the last of it was.
                    for (int j = i; j <= TrailSteps; j++)
                    {
                        TrailIn[j] = j > 0 ? TrailIn[j - 1] : Vector2.Zero;
                        TrailOut[j] = j > 0 ? TrailOut[j - 1] : Vector2.Zero;
                        TrailEdge[j] = j > 0 ? TrailEdge[j - 1] : Vector2.Zero;
                        TrailStrength[j] = 0f;
                    }

                    break;
                }

                KnightRig.Frame f = KnightRig.Build(pose, feet + Lift, facing, null, SunSwing, GraceSwing);
                bool held = blade == KnightRig.Sun ? f.SunOut : f.GraceOut;
                Vector2 guard = blade == KnightRig.Sun ? f.SunGuard : f.GraceGuard;
                Vector2 tip = blade == KnightRig.Sun ? f.SunTip : f.GraceTip;

                // The long blade's whole length sweeps: its trail is as long as its light.
                if (blade == KnightRig.Grace && State == Act.Move && MoveId == Moves.Long && LongShown() > 0.05f)
                {
                    float reach = LongLength(clock - ago);

                    if (reach > KnightRig.GripToTip)
                        tip = LongEnd(f, reach, out _);
                }

                TrailIn[i] = Vector2.Lerp(guard, tip, 0.2f);
                TrailOut[i] = tip;
                TrailEdge[i] = Vector2.Lerp(guard, tip, 0.9f);

                if (!held)
                {
                    lastTip = tip;
                    continue;
                }

                if (i > 0)
                {
                    // Only where the blade went fast: its point's speed over this step, in pixels a tick.
                    float speed = Vector2.Distance(tip, lastTip) / (TrailBack / TrailSteps);
                    float fast = MathHelper.Clamp((speed - 4f) / 10f, 0f, 1f);
                    float k = i / (float)TrailSteps;

                    TrailStrength[i] = (float)Math.Pow(1f - k, 1.2f) * fast * shown;
                    any |= TrailStrength[i] > 0.02f;
                }

                lastTip = tip;
            }

            // The newest end as strong as the step after it.
            TrailStrength[0] = TrailStrength[1];
            return any;
        }

        /// <summary>
        /// Him at an earlier moment of what he is doing now (its clock): the pose and where his
        /// feet were. False where that moment is not his present movement's (before it began, or
        /// in his guard, where he is never quick enough to leave a trail).
        /// </summary>
        private bool PoseBack(float clock, out KnightRig.Pose pose, out Vector2 feet)
        {
            pose = Pose;
            feet = Feet;

            if (clock < 0f)
                return false;

            float now = Timer - 1;

            switch (State)
            {
                case Act.Move:
                {
                    Move m = Current();
                    pose = PoseAt(clock);
                    feet = FeetAt(m, clock, now);
                    return true;
                }

                case Act.Reel:
                {
                    Move m = Reeling();
                    KnightRig.Pose p = m.Track.Sample(clock);
                    pose = clock < 3f ? KnightTrack.Between(from, p, clock / 3f) : p;
                    feet = Feet + new Vector2((m.Track.Travel(clock).X - m.Track.Travel(now).X) * facing, 0f);
                    return true;
                }

                case Act.Change:
                case Act.End:
                {
                    KnightTrack track = State == Act.Change ? KnightMoves.Change : KnightMoves.End;
                    KnightRig.Pose p = track.Sample(clock);
                    pose = clock < (State == Act.End ? 4f : 6f) ? KnightTrack.Between(from, p, clock / (State == Act.End ? 4f : 6f)) : p;
                    feet = Feet + new Vector2((track.Travel(clock).X - track.Travel(now).X) * facing, 0f);
                    return true;
                }

                case Act.Scene:
                {
                    if (clock >= DrawFrom && clock < PointFrom)
                    {
                        pose = KnightMoves.Draw.Sample(clock - DrawFrom);
                        return now < PointFrom;
                    }

                    if (clock >= PointFrom && pointTrack != null)
                    {
                        float d = clock - PointFrom;
                        KnightRig.Pose p = pointTrack.Sample(d);
                        pose = d < 12f ? KnightTrack.Between(KnightMoves.Draw.Last, p, d / 12f) : p;
                        return true;
                    }

                    return false;
                }

                default:
                    return false;
            }
        }

        /// <summary>Where his feet were at an earlier moment of a move: along the leap's arc while he was in the air, else back along the ground by what the move carried him.</summary>
        private Vector2 FeetAt(Move m, float clock, float now)
        {
            if (MoveId == Moves.Leap)
            {
                int up = KnightMoves.LeapCrouch;
                int air = (int)Math.Max(20f, arg);

                if (clock < up)
                    return leapFrom;

                if (clock <= up + air)
                {
                    float s = (clock - up) / air;
                    return Vector2.Lerp(leapFrom, leapTo, s) + new Vector2(0f, -4f * leapHeight * s * (1f - s));
                }

                return now <= up + air ? leapTo : Feet;
            }

            return Feet + new Vector2((m.Track.Travel(clock).X - m.Track.Travel(now).X) * facing, 0f);
        }

        /// <summary>The reel's movement, for the blade that was turned aside: made once each reel.</summary>
        private Move Reeling()
        {
            if (reelMove == null || reelMadeFor != reelBlade)
            {
                reelMove = KnightMoves.Reel(reelBlade);
                reelMadeFor = reelBlade;
            }

            return reelMove;
        }
    }
}
