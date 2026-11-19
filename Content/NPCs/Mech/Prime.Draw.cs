using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- drawn with it, and over it ---------------------------------------------------------------------------------------
    //
    //     its eyes' trails   behind each eye, smooth, in its eyes' colour: short while they are red (by request: "a trail
    //                        of red in the iris"), long once they are its green ("a long trail of that green")
    //     over it            a flash of its colour dying off its own picture; in Legends, what its attacks warn with
    //                        (Prime.Group, Prime.Moves); in its end, its mouth's light (a glow that
    //                        grows, streaks of its green drawn in to it), and its arms, once they fly at it: drawn here,
    //                        over it, so their hands lie on its mouth
    //     in the dark        (its end, DARK) over the dark itself (Cinematic.Light): its rays, out between its arms and
    //                        up out of its eyes, reaching further and further; its arms' shapes black against them
    //     its impact frames  (its end, IMPACT) three still frames over everything, three ticks each, and white after:
    //                        white, with black lines in at it from every edge, it and its arms black; black, a white
    //                        burst out of it, it and its arms black rimmed in its green; its green, the burst bigger

    public partial class Prime
    {
        // ---- its eyes' trails -------------------------------------------------------------------------------------------

        private void DrawEyeTrails(NPC npc, SpriteBatch batch, bool lit, bool green)
        {
            if (!lit || trailKept < 2)
                return;

            Color c = green ? PrimeArt.Nuclear : PrimeArt.Red;
            int keep = Math.Min(trailKept, green ? TrailLong : TrailShort);
            float wide = green ? 8f : 5f;
            float bright = green ? 0.7f : 0.6f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2[] points = side < 0 ? trailLeft : trailRight;
                Vector2 from = Eye(npc, side);

                for (int k = 0; k < keep; k++)
                {
                    float f = 1f - k / (float)keep;
                    DestroyerArt.DrawLine(batch, from, points[k], wide * f + 1.5f, PrimeArt.Lit(c * (bright * f)));
                    from = points[k];
                }
            }
        }

        // ---- over it ------------------------------------------------------------------------------------------------------

        private void DrawOver(NPC npc, SpriteBatch batch, Vector2 screenPos)
        {
            if (flash > 0.01f)
                Tint(npc, batch, screenPos, (flashGreen ? PrimeArt.Nuclear : PrimeArt.Red) * flash);

            // Legends: what its attacks warn with (its arms' together, and its own).
            if (scene == Scene.Group)
                DrawGroupMarks(npc, batch);
            else if (scene == Scene.Move)
                DrawMoveMarks(npc, batch);

            if (scene != Scene.Finale)
                return;

            if (step >= FinOpen)
                DrawAura(npc, batch);

            // Its arms that are flying at it, or on it: over it.
            int n = FindArms(npc);

            for (int k = 0; k < n; k++)
            {
                NPC limb = Main.npc[armList[k]];

                if (limb.TryGetGlobalNPC(out PrimeArm arm) && arm.Front)
                    arm.DrawLimb(limb, batch, screenPos, false);
            }
        }

        /// <summary>
        /// Its mouth's light: a glow of its green, a white heart in it, both growing with it; and, till its arms' hands
        /// close on it, short streaks of its green drawn in to it from all about, each quicker as it nears.
        /// </summary>
        private void DrawAura(NPC npc, SpriteBatch batch)
        {
            float aura = Aura;

            if (aura <= 0.001f)
                return;

            Vector2 mouth = Mouth(npc);
            float beat = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 21f);

            DestroyerArt.DrawGlow(batch, mouth, (40f + 250f * aura) * beat, PrimeArt.Lit(PrimeArt.Nuclear * (0.3f + 0.4f * aura)));
            DestroyerArt.DrawGlow(batch, mouth, (18f + 86f * aura) * beat, PrimeArt.Lit(PrimeArt.NuclearHot * (0.55f + 0.4f * aura)), hotCore: true);

            if (step >= FinCover)
                return;

            const int many = 11;
            const int period = 28;

            for (int k = 0; k < many; k++)
            {
                int tick = (int)(Main.GameUpdateCount % 100000) + k * 5;
                int round = tick / period;
                float phase = tick % period / (float)period;
                float angle = Chance(round * 131 + k * 17 + seed) * MathHelper.TwoPi;
                float far = 70f + 150f * aura + 60f * Chance(round * 7 + k * 53);
                float near = far * (1f - phase * phase);
                float length = 8f + 30f * phase;
                Vector2 way = angle.ToRotationVector2();
                float alpha = MathF.Sin(phase * MathHelper.Pi) * (0.2f + 0.6f * aura);

                DestroyerArt.DrawLine(batch, mouth + way * (near + length), mouth + way * near, 2.5f, PrimeArt.Lit(PrimeArt.Nuclear * alpha));
            }
        }

        // ---- in the dark --------------------------------------------------------------------------------------------------

        /// <summary>A ray of its light: the way it goes (degrees, clockwise from right as seen), how far of its full reach, how wide of its full width, and where from (0 its mouth, -1 and 1 its eyes).</summary>
        private static readonly (float Angle, float Far, float Wide, int From)[] Rays =
        {
            // Down, between its lower pair.
            (72f, 0.82f, 0.8f, 0), (84f, 1f, 1f, 0), (96f, 0.9f, 1f, 0), (108f, 0.74f, 0.8f, 0),

            // Out to its right, between its right pair; and to its left.
            (6f, 0.78f, 0.7f, 0), (22f, 1f, 1f, 0), (38f, 0.7f, 0.8f, 0),
            (142f, 0.7f, 0.8f, 0), (158f, 1f, 1f, 0), (174f, 0.78f, 0.7f, 0),

            // Up, out of its eyes.
            (-64f, 0.62f, 0.5f, 1), (-80f, 0.8f, 0.55f, 1), (-100f, 0.8f, 0.55f, -1), (-116f, 0.62f, 0.5f, -1),
        };

        /// <summary>What is drawn over the dark is kept as one thing a head (not made anew every tick).</summary>
        private Action<SpriteBatch> darkDraw, impactDraw;
        private int drawnFor = -1;

        private Action<SpriteBatch> DarkDraw(NPC npc)
        {
            drawnFor = npc.whoAmI;
            return darkDraw ??= DrawDark;
        }

        private Action<SpriteBatch> ImpactDraw(NPC npc)
        {
            drawnFor = npc.whoAmI;
            return impactDraw ??= DrawImpact;
        }

        /// <summary>The head this was asked for, if it is still it.</summary>
        private bool Drawn(out NPC npc)
        {
            npc = null;

            if (drawnFor < 0 || drawnFor >= Main.maxNPCs)
                return false;

            NPC found = Main.npc[drawnFor];

            if (!found.active || found.type != NPCID.SkeletronPrime || scene != Scene.Finale)
                return false;

            npc = found;
            return true;
        }

        /// <summary>
        /// Over the dark (Cinematic.Light: a batch open on the world's own view): its rays, each a soft shaft from its
        /// mouth (or an eye), flickering, swaying a little, as long as its light has reached; its mouth's own light; its
        /// arms' shapes black over that, so its light comes out between them; its eyes.
        /// </summary>
        private void DrawDark(SpriteBatch batch)
        {
            if (!Drawn(out NPC npc) || step != FinDark)
                return;

            int t = timer;
            float u = MathHelper.Clamp(t / (float)FinDarkTicks, 0f, 1f);
            float reach = MathHelper.Lerp(80f, BlastReach, MathF.Pow(u, 1.7f));
            float width = 34f + 96f * u;
            Vector2 mouth = Mouth(npc);
            Texture2D ray = PrimeArt.Ray;
            Vector2 root = new(0f, ray.Height * 0.5f);
            Color light = Color.Lerp(PrimeArt.Nuclear, PrimeArt.NuclearHot, 0.25f);
            int arms = FindArms(npc);

            for (int k = 0; k < Rays.Length; k++)
            {
                (float degrees, float far, float wide, int from) = Rays[k];

                float flick = 0.72f + 0.28f * MathF.Sin(t * (0.31f + 0.07f * k) + k * 1.7f);
                float angle = MathHelper.ToRadians(degrees + 2.5f * MathF.Sin(t * 0.05f + k)) + npc.rotation;
                Vector2 at = from == 0 ? mouth : Eye(npc, from);
                float length = reach * far * flick;
                // Fainter than its glow, so where they cross they stay shafts and do not fill in flat.
                Color c = PrimeArt.Lit(light * ((0.2f + 0.45f * u) * flick));

                batch.Draw(ray, at - Main.screenPosition, null, c, angle, root, new Vector2(length / ray.Width, width * wide / ray.Height), SpriteEffects.None, 0f);
            }

            DestroyerArt.DrawGlow(batch, mouth, 170f + 130f * u, PrimeArt.Lit(PrimeArt.Nuclear * (0.45f + 0.3f * u)));
            DestroyerArt.DrawGlow(batch, mouth, 76f + 56f * u, PrimeArt.Lit(PrimeArt.NuclearHot * 0.9f), hotCore: true);

            for (int k = 0; k < arms; k++)
            {
                NPC limb = Main.npc[armList[k]];

                if (!limb.TryGetGlobalNPC(out PrimeArm arm) || !arm.Front)
                    continue;

                arm.DrawShape(limb, batch, Main.screenPosition, Color.Black * 0.95f, false, Vector2.Zero);
                arm.DrawLamp(limb, batch, Main.screenPosition);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 eye = Eye(npc, side);
                DestroyerArt.DrawGlow(batch, eye, 34f + 22f * u, PrimeArt.Lit(PrimeArt.Nuclear * 0.8f));
                DestroyerArt.DrawGlow(batch, eye, 13f, PrimeArt.Lit(PrimeArt.NuclearHot), hotCore: true);
            }
        }

        // ---- its impact frames ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Over everything (Cinematic.Light), a still frame every three ticks: white, black, its green; then only white,
        /// which its blast's own flash takes up. Flat colours and hard edges, nothing soft: a frame struck, not lit.
        /// </summary>
        private void DrawImpact(SpriteBatch batch)
        {
            if (!Drawn(out NPC npc) || step != FinImpact)
                return;

            int frame = Math.Min(3, timer / 3);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle one = new(0, 0, 1, 1);
            Rectangle all = new(-600, -600, Main.screenWidth + 1200, Main.screenHeight + 1200);
            Color ground = frame switch
            {
                0 => Color.White,
                1 => Color.Black,
                2 => PrimeArt.Nuclear,
                _ => Color.White,
            };

            batch.Draw(pixel, all, one, ground);

            if (frame >= 3)
                return;

            Vector2 mouth = Mouth(npc);
            Vector2 centre = mouth - Main.screenPosition;
            Texture2D wedge = PrimeArt.Wedge;
            Vector2 foot = new(wedge.Width, wedge.Height * 0.5f);
            int pick = seed + frame * 977;

            if (frame == 0)
            {
                // Black lines in at it from every edge, each a spike with its point toward it.
                const int lines = 28;
                float far = (Main.screenWidth + Main.screenHeight) * 0.75f;

                for (int k = 0; k < lines; k++)
                {
                    float angle = (k + Chance(pick + k)) / lines * MathHelper.TwoPi;
                    float near = 130f + 280f * Chance(pick + k * 7 + 1);
                    float wide = 12f + 52f * Chance(pick + k * 13 + 2);
                    batch.Draw(wedge, centre + angle.ToRotationVector2() * far, null, Color.Black, angle, foot,
                        new Vector2((far - near) / wedge.Width, wide / wedge.Height), SpriteEffects.None, 0f);
                }
            }
            else
            {
                // A burst out of it: white spikes, their feet in it, bigger on the second of them.
                const int spikes = 24;
                float least = frame == 1 ? 240f : 400f, more = frame == 1 ? 460f : 620f;

                for (int k = 0; k < spikes; k++)
                {
                    float angle = (k + Chance(pick + k)) / spikes * MathHelper.TwoPi;
                    float length = least + more * Chance(pick + k * 7 + 1);
                    float wide = 30f + 80f * Chance(pick + k * 13 + 2);
                    batch.Draw(wedge, centre, null, Color.White, angle + MathHelper.Pi, foot,
                        new Vector2(length / wedge.Width, wide / wedge.Height), SpriteEffects.None, 0f);
                }

                // Its heart: white, hard-edged enough.
                float heart = frame == 1 ? 150f : 230f;
                DestroyerArt.DrawGlow(batch, mouth, heart * 2f, Color.White, hotCore: true);
                DestroyerArt.DrawGlow(batch, mouth, heart * 1.3f, Color.White, hotCore: true);
            }

            // It and its arms: black; on the black frame, rimmed in its green so they show.
            Texture2D sheet = TextureAssets.Npc[npc.type].Value;
            bool spiked = Spiked(npc), open = MouthOpen(npc);
            Vector2 middle = Middle(npc) - Main.screenPosition;
            int arms = FindArms(npc);

            if (frame == 1)
            {
                Texture2D shape = PrimeArt.WhiteOf(sheet);

                for (int k = 0; k < 8; k++)
                {
                    Vector2 off = (k * MathHelper.PiOver4).ToRotationVector2() * 4f;
                    DrawSheet(batch, shape, false, spiked, open, middle + off, PrimeArt.Nuclear, npc.rotation, npc.scale);

                    for (int a = 0; a < arms; a++)
                    {
                        NPC limb = Main.npc[armList[a]];

                        if (limb.TryGetGlobalNPC(out PrimeArm arm))
                            arm.DrawShape(limb, batch, Main.screenPosition, PrimeArt.Nuclear, true, off);
                    }
                }
            }

            DrawSheet(batch, sheet, false, spiked, open, middle, Color.Black, npc.rotation, npc.scale);

            for (int a = 0; a < arms; a++)
            {
                NPC limb = Main.npc[armList[a]];

                if (limb.TryGetGlobalNPC(out PrimeArm arm))
                    arm.DrawShape(limb, batch, Main.screenPosition, Color.Black, false, Vector2.Zero);
            }

            // Its eyes, the one thing lit in it.
            if (frame != 0)
            {
                for (int side = -1; side <= 1; side += 2)
                    batch.Draw(pixel, Eye(npc, side) - Main.screenPosition, one, Color.White, npc.rotation, new Vector2(0.5f), new Vector2(14f, 10f) * npc.scale, SpriteEffects.None, 0f);
            }
        }
    }
}
