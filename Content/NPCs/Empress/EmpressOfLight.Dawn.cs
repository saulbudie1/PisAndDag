using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the dawnbreak (Legends act, her second half; offered, chosen) -------------------------------------------
    //
    //     AIM     24   she turns her bow down to the horizon (the arena's floor, the far side of her from the player)
    //     LOOSE   12   one arrow, into the border: where it strikes, a false sun begins to rise
    //     RISE   180   three seconds: the sun coming up over the edge, everything going to gold and then white, a choir of
    //                  light swelling; her shadow thrown long across the arena, away from it: the one place it will not
    //                  reach. She holds still in the light.
    //     SEAR    24   everything not in her shadow burned white (120 in a normal world, 80 in an expert one, doubled as the
    //                  game doubles any hostile shot)
    //     FADE    40   the sun gone back under, the light let down
    //
    // The shadow is hers, cast from where the sun is through her: a band as wide as she is, from her out to the border.
    // The false day's light and her shadow are laid behind her and everyone (the world's ground and sky lit and
    // shaded; she and the players drawn over it as they are); the sear's flash over everything.

    public partial class EmpressOfLight
    {
        private const int DawnAim = 0;
        private const int DawnLoose = 1;
        private const int DawnRise = 2;
        private const int DawnSear = 3;
        private const int DawnFade = 4;

        private const int DawnAimTicks = 24;
        private const int DawnLooseTicks = 12;
        private const int DawnRiseTicks = 180;
        private const int DawnSearTicks = 24;
        private const int DawnFadeTicks = 40;

        /// <summary>Her shadow: half its width at her, and at the border; how far behind her (toward the sun) it starts.</summary>
        private const float ShadowNear = 150f;
        private const float ShadowFar = 190f;
        private const float ShadowBehind = 0f;

        /// <summary>
        /// Drawn only (the safe place is the above): how far behind her its dark comes in from nothing; how far past its
        /// edges, at her and at the border, its dark is let go.
        /// </summary>
        private const float ShadowFadeIn = 50f;
        private const float ShadowSoftNear = 18f;
        private const float ShadowSoftFar = 26f;

        /// <summary>How long the sear hurts.</summary>
        private const int SearHurts = 6;

        private const int SearNormal = 120;
        private const int SearExpert = 80;

        private static readonly Color Dawn = new Color(255, 214, 150);

        private void PlanDawn(NPC npc, Player target)
        {
            // The sun on the arena's floor, the far side of her from the player (down and away), where the arrow goes.
            float down = MathHelper.PiOver2 + actSide * 0.55f;
            actAt = npc.Center + down.ToRotationVector2() * Decagon.Reach(npc.Center, down.ToRotationVector2(), arenaCentre, EmpressArena.Radius, out _);
            actTurn = (actAt - npc.Center).ToRotation();
        }

        /// <summary>The sun: on the border where the arrow struck, rising up out of it as it comes.</summary>
        private Vector2 SunAt()
        {
            float rise = beat == DawnRise ? Ease(timer / (float)DawnRiseTicks) : beat >= DawnSear ? 1f : 0f;
            Vector2 inward = (arenaCentre - actAt).SafeNormalize(-Vector2.UnitY);
            return actAt + inward * 140f * rise;
        }

        /// <summary>Her shadow's way: from the sun, through her, out.</summary>
        private Vector2 ShadowWay(NPC npc) => (npc.Center - SunAt()).SafeNormalize(-Vector2.UnitY);

        /// <summary>Is this point in her shadow (by a margin in from its edge)?</summary>
        private bool InShadow(NPC npc, Vector2 point, float margin)
        {
            Vector2 way = ShadowWay(npc);
            Vector2 off = point - npc.Center;
            float along = Vector2.Dot(off, way);

            if (along < -ShadowBehind)
                return false;

            float across = Math.Abs(off.X * way.Y - off.Y * way.X);
            float reach = Decagon.Reach(npc.Center, way, arenaCentre, EmpressArena.Radius, out _);
            float half = MathHelper.Lerp(ShadowNear, ShadowFar, MathHelper.Clamp(along / Math.Max(1f, reach), 0f, 1f));

            return across < half - margin;
        }

        private void RunDawn(NPC npc, Player who, bool entered)
        {
            int t = timer;

            // Still, so her shadow is; turned to the horizon her arrow goes to.
            npc.velocity *= beat == DawnAim ? 0.85f : 0f;
            facingHeld = true;
            npc.spriteDirection = npc.direction = actAt.X >= npc.Center.X ? 1 : -1;

            switch (beat)
            {
                case DawnAim:
                    Aim(actTurn, Ease(t / 18f), 1, 0f, 0.1f);

                    if (entered)
                        Sound(EmpressSounds.Draw, npc.Center, -0.3f, 0.8f, 2);

                    if (Authority && t >= DawnAimTicks)
                        Next(npc, DawnLoose);

                    break;

                case DawnLoose:
                    Aim(actTurn);

                    if (entered)
                    {
                        Loosed();

                        if (!Main.dedServ)
                        {
                            Sound(EmpressSounds.Loose, npc.Center, -0.2f, 1f, 2);
                            Sound(SoundID.Item5, npc.Center, -0.5f, 0.8f);
                        }
                    }

                    if (Crossed(DawnLooseTicks - 4) && !Main.dedServ)
                    {
                        Sound(EmpressSounds.Thunk, actAt, -0.3f, 0.9f, 2);
                        PADParticleHandler.SpawnParticle(new BloomGeneric(actAt, Vector2.Zero, Dawn, 260f / 160f, 20));
                    }

                    if (Authority && t >= DawnLooseTicks)
                        Next(npc, DawnRise);

                    break;

                case DawnRise:
                    HoldBowUp(BowLeft(npc) ? -2.1f : -1.04f, BowLeft(npc) ? MathHelper.Pi : 0f);

                    if (entered)
                    {
                        Sound(EmpressSounds.Sunrise, actAt, 0f, 1f, 1);
                        Hitbox(npc, EmpressHitbox.Shape.Sear, npc.Center, 0f, Hurt(npc, SearNormal, SearExpert), DawnRiseTicks + DawnSearTicks + 10);
                    }

                    // Heat shimmering up off the arena as it comes.
                    if (!Main.dedServ && t % 30 == 15)
                        Sound(SoundID.Item34, SunAt(), -0.6f + t / 300f, 0.25f, 2);

                    if (Authority && t >= DawnRiseTicks)
                        Next(npc, DawnSear);

                    break;

                case DawnSear:
                    HoldBowUp(BowLeft(npc) ? -2.1f : -1.04f, BowLeft(npc) ? MathHelper.Pi : 0f);

                    if (entered)
                        Sears(npc);

                    if (Authority && t >= DawnSearTicks)
                        Next(npc, DawnFade);

                    break;

                case DawnFade:
                    if (Authority && t >= DawnFadeTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>The sear: white, a sizzle, the world shaken; her shadow left dark.</summary>
        private void Sears(NPC npc)
        {
            if (Main.dedServ)
                return;

            float near = Near(npc.Center);
            bool safe = InShadow(npc, Main.LocalPlayer.Center, 0f);

            Sound(EmpressSounds.Sear, Main.LocalPlayer.Center, 0f, 1f, 2);
            Sound(SoundID.Item74, SunAt(), -0.3f, 0.8f);
            Cinematic.Flash(Dawn, (safe ? 0.25f : 0.6f) * near);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(9f * near, 22);

            EmpressWarp.Ripple(SunAt(), 40f, 140f, 18f, 40);
        }

        /// <summary>The sear's hitbox asks: is this body out of her shadow, as it burns?</summary>
        internal bool SearHits(Rectangle box)
        {
            if (scene != Scene.Act || act != Act.Dawn || beat != DawnSear || timer > SearHurts)
                return false;

            NPC npc = NpcOfThis();
            return npc != null && !InShadow(npc, box.Center.ToVector2(), 6f);
        }

        /// <summary>How bright the false day is: rising over the rise, white at the sear, let down after.</summary>
        private float DawnLight() => beat switch
        {
            DawnRise => Ease(timer / (float)DawnRiseTicks),
            DawnSear => 1f,
            DawnFade => 1f - Ease(timer / (float)DawnFadeTicks),
            _ => 0f,
        };

        /// <summary>
        /// The false day (Strip: no batch open): the arena washed in its light, gold going to white, and her shadow laid
        /// over it, dark; the light thrown at the sear.
        /// </summary>
        private void DrawDawnBands(NPC npc)
        {
            float light = DawnLight();

            if (light <= 0.003f || !EmpressArt.Ready(EmpressArt.Line))
                return;

            Texture2D line = EmpressArt.Line.Value;
            float sear = beat == DawnSear ? 1f - Ease(timer / (float)DawnSearTicks) : 0f;
            View(out float left, out float right, out float top, out float bottom);

            // The wash: over all that is seen, its colour going from dawn's gold to white as it rises.
            Color wash = EmpressArt.Additive(Color.Lerp(Dawn, Color.White, light), 0.08f + 0.22f * light + 0.55f * sear);
            Strip.Begin();
            Strip.Pair(new Vector2(left, top), new Vector2(left, bottom), wash, wash, 0f, FlatRow, FlatRow);
            Strip.Pair(new Vector2(right, top), new Vector2(right, bottom), wash, wash, 1f, FlatRow, FlatRow);
            Strip.Draw(line, SamplerState.LinearClamp);

            // Her shadow: dark, from just behind her out to the border, a little wider as it goes; darkest down its
            // middle, lighter to its edges, and past its edges let go softly over a few pixels (never a cut line, but
            // the dark ends where the safety does: the lilac lines over it mark the very edge).
            Vector2 way = ShadowWay(npc);
            Vector2 across = new Vector2(-way.Y, way.X);
            float reach = Decagon.Reach(npc.Center, way, arenaCentre, EmpressArena.Radius, out _);
            Vector2 near = npc.Center - way * ShadowBehind;
            Vector2 far = npc.Center + way * reach;
            Vector2 behind = near - way * ShadowFadeIn;
            Color dark = Color.Black * (0.3f + 0.35f * light);
            Color rim = dark * 0.7f;
            Color none = Color.Black * 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 to = across * side;

                // Middle to edge, from the moment behind her where it comes in.
                Strip.Begin();
                Strip.Pair(behind, behind + to * ShadowNear, none, none, 0f, FlatRow, FlatRow);
                Strip.Pair(near, near + to * ShadowNear, dark, rim, 0.05f, FlatRow, FlatRow);
                Strip.Pair(far, far + to * ShadowFar, dark, rim, 1f, FlatRow, FlatRow);
                Strip.Draw(line, SamplerState.LinearClamp);

                // Past the edge, let go.
                Strip.Begin();
                Strip.Pair(behind + to * ShadowNear, behind + to * (ShadowNear + ShadowSoftNear), none, none, 0f, FlatRow, FlatRow);
                Strip.Pair(near + to * ShadowNear, near + to * (ShadowNear + ShadowSoftNear), rim, none, 0.05f, FlatRow, FlatRow);
                Strip.Pair(far + to * ShadowFar, far + to * (ShadowFar + ShadowSoftFar), rim, none, 1f, FlatRow, FlatRow);
                Strip.Draw(line, SamplerState.LinearClamp);
            }
        }

        private void DrawDawnOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            float light = DawnLight();
            Vector2 sun = SunAt();

            // The arrow going down to the horizon.
            if (beat == DawnLoose)
            {
                Vector2 nock = NockFor(npc, actTurn);
                float p = Math.Min(1f, timer / (float)(DawnLooseTicks - 4));
                EmpressBow.DrawArrow(spriteBatch, Vector2.Lerp(nock, actAt, p) - actTurn.ToRotationVector2() * 90f, actTurn, 1f, 1f, 0.1f, 1f);
            }

            if (light <= 0.003f)
                return;

            // The sun, rising: layers of light, rays fanning out of it across the arena, turning slowly.
            float size = 120f + 260f * light;
            Vector2 inward = (arenaCentre - actAt).SafeNormalize(-Vector2.UnitY);
            float up = inward.ToRotation();

            for (int k = 0; k < 14; k++)
            {
                float a = up + (k / 13f - 0.5f) * 2.6f + 0.06f * (float)Math.Sin(time * 0.01f + k);
                float length = (900f + 1100f * light) * (0.75f + 0.25f * (float)Math.Sin(time * 0.02f + k * 2.1f));
                EmpressArt.DrawRay(spriteBatch, sun, a, length, 90f + 50f * (k % 3), EmpressArt.Additive(Color.Lerp(Dawn, Color.White, light), 0.12f * light));
            }

            EmpressArt.DrawBloom(spriteBatch, sun, size * 4f, EmpressArt.Additive(Dawn, 0.35f * light));
            EmpressArt.DrawBloom(spriteBatch, sun, size * 2f, EmpressArt.Additive(Color.Lerp(Dawn, Color.White, 0.5f), 0.6f * light));
            EmpressArt.DrawBloom(spriteBatch, sun, size, EmpressArt.Additive(Color.White, 0.95f * light));
            EmpressArt.DrawStar(spriteBatch, sun, size * 1.6f, time * 0.004f, EmpressArt.Additive(Color.White, 0.5f * light));
            EmpressWarp.Lens(sun, size * 1.2f, 0.12f * light, 0f);

            // Her shadow's edges, beating as the sear nears: the one place to be.
            if (beat == DawnRise)
            {
                Vector2 way = ShadowWay(npc);
                Vector2 across = new Vector2(-way.Y, way.X);
                float reach = Decagon.Reach(npc.Center, way, arenaCentre, EmpressArena.Radius, out _);
                Vector2 near = npc.Center - way * ShadowBehind;
                Vector2 far = npc.Center + way * reach;
                float beatOf = 0.5f + 0.5f * (float)Math.Sin(time * (0.1f + 0.4f * light));

                for (int side = -1; side <= 1; side += 2)
                {
                    EmpressArt.DrawBeam(spriteBatch, near + across * side * ShadowNear, far + across * side * ShadowFar, 3f, EmpressArt.Additive(EmpressArt.Lilac, (0.2f + 0.5f * light) * beatOf));
                }
            }

            // The sear: everything outside it thrown white a moment.
            if (beat == DawnSear && timer < 10)
                EmpressArt.DrawBloom(spriteBatch, sun, 4000f, EmpressArt.Additive(Color.White, 0.5f * (1f - timer / 10f)));
        }
    }
}
