using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- their coming -----------------------------------------------------------------------------------------------------
    //
    // By request: "player center pans just above player a little bit, They run from the back to over the screen like these
    // little scarabs (backdrop is 1/2 size and frontground is 2x size and darker moving fast) each from opposite side
    // mirroring each other, and drop down from above". After the beetles in the video it pointed at (Spirit Reforged's
    // Scarabeus, its first seconds): the far ones small and hazed with the sky, behind the ground; the near ones big, black,
    // over everything, and quick.
    //
    //     BACK    162   the camera eases to a little over the player, who is held; each crosses the sky far off from its own
    //                   side (the Retinazer from the left), half its size, hazed with the sky, behind the ground and walls,
    //                   its eye's colour showing; off the far side (12 of it out of sight)
    //     FRONT    64   back over the screen the other way, twice its size, dark and quick, over everything (16 out of sight)
    //     DROP     80   down from above to either side over the player (the Retinazer left), turning onto them
    //     HOLD     75   it hangs there, its eye flaring; their name comes up; the camera comes home and the fight begins
    //
    // Skipped (E held, single player): straight to the fight, both in their places.

    public partial class Twins
    {
        private const int EnBack = 0;
        private const int EnFront = 1;
        private const int EnDrop = 2;
        private const int EnHold = 3;

        private const int BackFly = 150;
        private const int BackTicks = 162;
        private const int FrontFly = 48;
        private const int FrontTicks = 64;
        private const int DropTicks = 80;

        /// <summary>
        /// They hang there as long as their name takes to come up under them and be read (it comes up on the hold's
        /// fourth tick; its plate has a line now, and is a brisk one), so the scene lets go as the name starts down to
        /// the bar. (75 before their plate had a line.)
        /// </summary>
        private const int HoldTicks = 4 + BossIntro.TitleRise + BossIntro.BriskRead;

        /// <summary>How big it is drawn far off, and near.</summary>
        private const float FarScale = 0.5f;
        private const float NearScale = 2f;

        /// <summary>Where the player was as they came: everything in their coming is laid out from here.</summary>
        private Vector2 anchor;

        /// <summary>The shot: a little over the player (by request: "just above player a little bit").</summary>
        private Vector2 Focus => anchor + new Vector2(0f, -6f * 16f);

        /// <summary>The side it first comes from: the Retinazer from the left.</summary>
        private static float FirstSide(NPC npc) => IsRet(npc) ? -1f : 1f;

        /// <summary>Where it hangs over the player once it has dropped in: the Retinazer to the left (as BossArrivalWait has them).</summary>
        private Vector2 HangAt(NPC npc) => anchor + new Vector2((IsRet(npc) ? -1f : 1f) * 190f, -270f);

        /// <summary>Its coming begins: with its twin's, if its twin's has begun; or none, if its twin is already fighting. The server's.</summary>
        private void BeginEnter(NPC npc, NPC other, Twins twin)
        {
            started = true;

            if (twin != null && twin.started)
            {
                if (twin.scene != Scene.Enter)
                    return;

                // In step with it (the NPCs run in order: if it has run this tick, it is a tick ahead).
                anchor = twin.anchor;
                Go(npc, Scene.Enter, twin.step);
                timer = Math.Max(0, twin.timer - (other.whoAmI < npc.whoAmI ? 1 : 0));
                return;
            }

            npc.TargetClosest(false);
            Player p = Target(npc);

            if (p == null)
                return;

            anchor = p.Center;
            seed = Main.rand.Next(int.MaxValue);
            Go(npc, Scene.Enter, EnBack);
        }

        private void RunEnter(NPC npc, bool entered, NPC other, Twins twin)
        {
            int t = timer;
            float side = FirstSide(npc);
            bool watching = Watching(anchor);

            if (watching)
            {
                float blend = step == EnBack ? Smooth(t / 50f) : 1f;
                Shot(Focus, blend, step >= EnDrop ? 1.06f : 1.02f, spare: true);
            }

            // E held in the corner (single player): straight to the fight.
            if (watching && step < EnHold)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    SkipEnter(npc, other, twin);
                    return;
                }
            }

            switch (step)
            {
                case EnBack:
                    {
                        // Far off, across the sky: an arc over the middle, a little flutter in it; the Retinazer a little lower.
                        float u = Math.Min(1f, t / (float)BackFly);
                        float x = MathHelper.Lerp(side * 1300f, -side * 1300f, u);
                        float y = -300f - 80f * (float)Math.Sin(u * MathHelper.Pi) + 14f * (float)Math.Sin(u * MathHelper.TwoPi * 2f) + (IsRet(npc) ? 22f : -22f);
                        Fly(npc, anchor + new Vector2(x, y));

                        if (t == 20)
                            Whoosh(npc, far: true);

                        if (Authority && t >= BackTicks)
                            Next(npc, EnFront);

                        break;
                    }

                case EnFront:
                    {
                        // Near, over everything, the other way: a dip through the middle of the screen; the Retinazer a little lower.
                        float u = Math.Min(1f, t / (float)FrontFly);
                        float x = MathHelper.Lerp(-side * 1500f, side * 1500f, u);
                        float y = -250f + 230f * (float)Math.Sin(u * MathHelper.Pi) + (IsRet(npc) ? 30f : -30f);
                        Fly(npc, anchor + new Vector2(x, y));

                        if (t == 6)
                            Whoosh(npc, far: false);

                        if (Authority && t >= FrontTicks)
                            Next(npc, EnDrop);

                        break;
                    }

                case EnDrop:
                    {
                        // From high over it, down to its place, a little past and back; turning onto the player as it comes.
                        Vector2 hang = HangAt(npc);
                        float u = Math.Min(1f, (t + 1f) / DropTicks);
                        Move(npc, Vector2.Lerp(hang + new Vector2(0f, -900f), hang, OutBack(u)));
                        Turn(npc, anchor - npc.Center, 0.18f);

                        if (entered)
                            DropFx(npc);

                        DropTrail(npc, u);

                        if (Authority && t >= DropTicks)
                            Next(npc, EnHold);

                        break;
                    }

                case EnHold:
                    {
                        Move(npc, HangAt(npc) + new Vector2(0f, 8f * (float)Math.Sin(t * 0.06f)));
                        Player p = Target(npc);
                        Turn(npc, (p?.Center ?? anchor) - npc.Center, 0.2f);

                        if (entered)
                            EyeFlare(npc);

                        if (t == 4)
                            ShowName(npc, other);

                        HoldGlow(npc, 1f - Smooth((t - HoldTicks + 30) / 30f));

                        if (t >= HoldTicks)
                        {
                            if (watching && !Main.dedServ && !sentHome)
                            {
                                sentHome = true;
                                PisPlayer.PanHome(Focus, 60);
                            }

                            if (Authority)
                                Fight(npc);
                        }

                        break;
                    }
            }
        }

        /// <summary>The camera sent home once (by this one of the pair, on this machine).</summary>
        private bool sentHome;

        /// <summary>Along its way, facing where it goes.</summary>
        private static void Fly(NPC npc, Vector2 to)
        {
            Move(npc, to);

            if (npc.velocity.LengthSquared() > 1f)
                npc.rotation = LookAlong(npc.velocity);
        }

        private static float OutBack(float u)
        {
            const float c1 = 1.2f;
            const float c3 = c1 + 1f;
            float v = u - 1f;
            return 1f + c3 * v * v * v + c1 * v * v;
        }

        /// <summary>Their name (once; the Retinazer names the pair, or whichever of them is here).</summary>
        private void ShowName(NPC npc, NPC other)
        {
            if (named)
                return;

            named = true;

            if (!IsRet(npc) && other != null && other.type == NPCID.Retinazer)
                return;

            if (!Main.dedServ)
                BossIntro.PlayNameOnly(npc.whoAmI, TwinsSystem.Plate);
        }

        /// <summary>The coming over: vanilla's AI from a fresh start, the fight on.</summary>
        private void Fight(NPC npc)
        {
            npc.velocity = Vector2.Zero;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            npc.hide = false;
            Go(npc, Scene.None, 0);
        }

        /// <summary>Skipped: both in their places, their name up, the camera home, the fight on.</summary>
        private void SkipEnter(NPC npc, NPC other, Twins twin)
        {
            foreach ((NPC n, Twins w) in new[] { (npc, this), (other, twin) })
            {
                if (n == null || w == null || w.scene != Scene.Enter)
                    continue;

                n.Center = w.HangAt(n);
                n.rotation = LookAlong(w.anchor - n.Center);
                w.ShowName(n, n == npc ? other : npc);
                w.Fight(n);
            }

            if (!Main.dedServ)
                PisPlayer.PanHome(Main.LocalPlayer.Center, 30);
        }

        // ---- drawn far off, and near -------------------------------------------------------------------------------------

        /// <summary>
        /// Its passes: far off (half its size, hazed with the sky: the sky's colour over a pale grey, its eye's colour still
        /// showing), or near (twice its size, nearly black, a smear behind it of where it was, its eye's colour a coal).
        /// </summary>
        private void DrawPass(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            bool near = step == EnFront;
            float scale = near ? NearScale : FarScale;
            Vector2 at = EyeCentre(npc) - screenPos;
            Vector2 eye = at + Facing(npc) * 52f * scale;
            Color element = ElementOf(npc);

            if (near)
            {
                Color dark = new(16, 12, 18);

                for (int k = 3; k >= 1; k--)
                    spriteBatch.Draw(texture, at - npc.velocity * 0.35f * k, npc.frame, dark * (0.34f - 0.09f * k), npc.rotation, Pivot, scale, SpriteEffects.None, 0f);

                spriteBatch.Draw(texture, at, npc.frame, dark, npc.rotation, Pivot, scale, SpriteEffects.None, 0f);
                DestroyerArt.DrawGlow(spriteBatch, eye + screenPos, 60f, DestroyerArt.Lit(element * 0.55f));
                DestroyerArt.DrawGlow(spriteBatch, eye + screenPos, 22f, DestroyerArt.Lit(Color.Lerp(element, Color.White, 0.35f) * 0.7f), hotCore: true);
            }
            else
            {
                Color haze = Color.Lerp(new Color(158, 156, 170), Main.ColorOfTheSkies, 0.5f);
                spriteBatch.Draw(texture, at, npc.frame, haze, npc.rotation, Pivot, scale, SpriteEffects.None, 0f);
                DestroyerArt.DrawGlow(spriteBatch, eye + screenPos, 26f, DestroyerArt.Lit(element * 0.6f));
                DestroyerArt.DrawGlow(spriteBatch, eye + screenPos, 9f, DestroyerArt.Lit(Color.Lerp(element, Color.White, 0.4f) * 0.8f), hotCore: true);
            }
        }
    }
}
