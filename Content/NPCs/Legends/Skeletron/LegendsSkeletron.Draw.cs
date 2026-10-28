using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- drawn -----------------------------------------------------------------------------------------------------
    //
    // His whole body, in one order, whatever slots the game gave its parts: what a move lays under him (a path, a line it
    // will take), his arms, his skull, his hands, and what a move lays over him. In his coming the old man is drawn too,
    // over the skull still hidden in him and under the arms coming out of him.

    public partial class LegendsSkeletron
    {
        private void DrawBody(NPC npc, SpriteBatch spriteBatch, Color drawColor)
        {
            bool red = redHat;
            Color colour = npc.GetAlpha(npc.GetNPCColorTintedByBuffs(drawColor));

            DrawMoveUnder(npc, spriteBatch);

            if (scene == Scene.Arrival)
            {
                if (beat == ArriveGather || beat == ArriveBurst && !victim)
                    DrawGatherGlow(spriteBatch);

                if (!hidden)
                    DrawSkull(npc, spriteBatch, colour, red);

                if (victim && (beat <= ArriveShred || beat == ArriveRise && timer < CastAt))
                    DrawOldMan(spriteBatch);

                DrawArms(npc, spriteBatch, red);
                DrawHands(npc, spriteBatch, red);
                DrawMoveOver(npc, spriteBatch);
                return;
            }

            DrawArms(npc, spriteBatch, red);

            if (!hidden)
                DrawSkull(npc, spriteBatch, colour, red);

            DrawHands(npc, spriteBatch, red);
            DrawMoveOver(npc, spriteBatch);
        }

        /// <summary>His skull: afterimages when he goes fast, his ward, the skull itself, his eyes lit.</summary>
        private void DrawSkull(NPC npc, SpriteBatch spriteBatch, Color colour, bool red)
        {
            float scale = npc.scale * popScale;
            Vector2 centre = npc.Center;

            // Fast, or spinning: images of him where he has just been.
            float speed = npc.velocity.Length();
            float spin = trailCount > 1 ? Math.Abs(MathHelper.WrapAngle(trailTurn[0] - trailTurn[1])) : 0f;

            if ((speed > 12f || spin > 0.2f) && trailCount > 2)
            {
                for (int i = trailCount - 1; i >= 1; i--)
                {
                    float fade = (1f - i / (float)trailCount) * 0.35f;
                    SkeletronRig.DrawHead(spriteBatch, red, trail[i], trailTurn[i], scale, jawShown, liftShown, colour * fade, !jawAway, hatShown, cracked);
                }
            }

            if (wardShown > 0.02f)
            {
                float pulse = 0.55f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f);
                SkeletronRig.DrawHeadRim(spriteBatch, red, centre, npc.rotation, scale, jawShown, liftShown, WardColour * (pulse * wardShown), 3f, !jawAway, hatShown);
            }

            SkeletronRig.DrawHead(spriteBatch, red, centre, npc.rotation, scale, jawShown, liftShown, colour, !jawAway, hatShown, cracked);

            if (eyesShown > 0.02f || flameShown > 0.02f)
            {
                for (int k = 0; k < 2; k++)
                {
                    Vector2 eye = SkeletronRig.Eye(k == 0, centre, npc.rotation, scale, liftShown);
                    float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 23f + k * 2f);

                    if (eyesShown > 0.02f)
                    {
                        SkeletronRig.Glow(spriteBatch, eye, eyeColour * (0.55f * eyesShown), 22f * scale);
                        SkeletronRig.Hot(spriteBatch, eye, eyeColour * (eyesShown * flicker), 7f * scale);
                    }

                    if (flameShown > 0.02f)
                    {
                        SkeletronRig.Glow(spriteBatch, eye + new Vector2(0f, -6f), BlazePurple * (0.6f * flameShown), 34f * scale);
                        SkeletronRig.Hot(spriteBatch, eye, Color.Lerp(BlazePurple, BlazeRed, 0.5f + 0.5f * flicker) * flameShown, 12f * scale);
                    }
                }
            }
        }

        /// <summary>His arms: from his shoulders (or wherever a move says they start) to his wrists, as the move says they go.</summary>
        private void DrawArms(NPC npc, SpriteBatch spriteBatch, bool red)
        {
            for (int i = 0; i < 2; i++)
            {
                if (hand[i] < 0)
                    continue;

                NPC h = Main.npc[hand[i]];

                if (!h.active || h.type != NPCID.SkeletronHand || !h.TryGetGlobalNPC(out LegendsSkeletronHand look) || !look.shown || look.arm == ArmStyle.None)
                    continue;

                float side = SideOf(i);
                float scale = h.scale * look.scale;
                Vector2 wrist = SkeletronRig.WristFromMiddle(h.Center + h.netOffset, look.angle, side > 0f, scale);
                Vector2 shoulder = look.hasRoot ? look.root : look.armKnown ? look.armTop : SkeletronRig.Shoulder(npc.Center, side, npc.scale);
                Color tint = h.GetAlpha(Color.White);

                switch (look.arm)
                {
                    case ArmStyle.Whip:
                        {
                            // Torn off and swung: still its two bones, the elbow thrown out the way the swing bends.
                            Vector2 middle = (shoulder + wrist) * 0.5f;
                            Vector2 prefer = (look.bend - middle).SafeNormalize(new Vector2(side, 0.7f));
                            SkeletronRig.DrawArm(spriteBatch, red, shoulder, wrist, prefer, npc.scale, tint);
                            break;
                        }

                    default:
                        // Hanging off him, or straight out from where a move roots it: two bones, upper arm and forearm.
                        SkeletronRig.DrawArm(spriteBatch, red, shoulder, wrist, side, look.arm == ArmStyle.Straight ? scale : npc.scale, tint);
                        break;
                }
            }
        }

        /// <summary>His hands: a rim of light first if a move lights them (or the yellow of a blow nothing stops), then the hand.</summary>
        private void DrawHands(NPC npc, SpriteBatch spriteBatch, bool red)
        {
            for (int i = 0; i < 2; i++)
            {
                if (hand[i] < 0)
                    continue;

                NPC h = Main.npc[hand[i]];

                if (!h.active || h.type != NPCID.SkeletronHand || !h.TryGetGlobalNPC(out LegendsSkeletronHand look) || !look.shown)
                    continue;

                float side = SideOf(i);
                bool mirror = side > 0f;
                float scale = h.scale * look.scale;
                Vector2 middle = h.Center + h.netOffset;
                Vector2 wrist = SkeletronRig.WristFromMiddle(middle, look.angle, mirror, scale);
                Color light = Lighting.GetColor((int)(middle.X / 16f), (int)(middle.Y / 16f));
                Color colour = h.GetAlpha(h.GetNPCColorTintedByBuffs(light));

                if (look.bypass)
                    SkeletronRig.DrawHandRim(spriteBatch, red, wrist, look.angle, mirror, scale, look.pose, CCBypass.Warning * CCBypass.OutlineGlow, CCBypass.OutlineReach);

                if (look.rimReach > 0f)
                    SkeletronRig.DrawHandRim(spriteBatch, red, wrist, look.angle, mirror, scale, look.pose, look.rim, look.rimReach);

                SkeletronRig.DrawHand(spriteBatch, red, wrist, look.angle, mirror, scale, look.pose, colour);
            }
        }

        // ---- the old man, in his coming --------------------------------------------------------------------------------

        /// <summary>
        /// Into the shred and a little after, where each half of him is from where it was cut: pulled out with the hand that
        /// holds it, swung back as that hand winds up to cast it.
        /// </summary>
        private Vector2 HalfOffset(float side)
        {
            if (beat == ArriveShred)
            {
                float p = EaseOut(timer / 8f);
                return new Vector2(side * 78f * p, -14f * p);
            }

            if (beat == ArriveRise)
            {
                float p = EaseIn(timer / (float)CastAt);
                return new Vector2(side * (78f - 20f * p), -14f - 20f * p);
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// The old man as his coming has him: standing, coughing (doubled over, his way), shaking, crushed; then torn in two
        /// down the middle, a half with each hand.
        /// </summary>
        private void DrawOldMan(SpriteBatch spriteBatch)
        {
            Texture2D texture = SkeletronRig.OldMan;

            if (texture == null)
                return;

            int frameHeight = texture.Height / SkeletronRig.OldManFrames;
            int width = texture.Width;
            bool flip = facing == 1;
            SpriteEffects effects = flip ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 middle = anchor + new Vector2(0f, -20f);
            Color light = Lighting.GetColor((int)(middle.X / 16f), (int)(middle.Y / 16f));

            if (beat < ArriveShred)
            {
                // Whole: doubled over at each cough, about his feet; shaking more and more once his hand has gone.
                float bend = 0f;
                float shake = 0f;

                if (beat == ArriveCough)
                {
                    bend = CoughBend(timer - FirstCough) + CoughBend(timer - SecondCough) + (timer > SecondCough ? 0.08f : 0f);
                    shake = timer > SecondCough ? 0.6f : 0f;
                }
                else if (beat == ArriveBurst)
                {
                    bend = 0.08f - 0.2f * EaseOut(timer / 10f);
                    shake = 1.2f;
                }
                else if (beat == ArriveGrip)
                {
                    bend = -0.12f;
                    shake = 1f + 2f * Math.Min(1f, timer / (float)GripTicks);
                }

                Vector2 jitter = shake > 0f ? Main.rand.NextVector2Circular(shake, shake) : Vector2.Zero;
                Vector2 origin = new Vector2(width * 0.5f, frameHeight - 2f);
                Rectangle frame = new Rectangle(0, 0, width, frameHeight);

                // Crushed: a little narrower as they squeeze.
                float squeeze = beat == ArriveGrip ? 1f - 0.12f * Math.Min(1f, timer / (float)GripTicks) : 1f;

                spriteBatch.Draw(texture, anchor + jitter - Main.screenPosition, frame, light, facing * bend, origin, new Vector2(squeeze, 1f), effects, 0f);
                return;
            }

            // In two: each half about the cut, with its hand.
            int half = width / 2;

            for (int k = -1; k <= 1; k += 2)
            {
                bool screenRight = k > 0;
                bool pictureRight = screenRight != flip;
                Rectangle frame = new Rectangle(pictureRight ? half : 0, 0, half, frameHeight);
                Vector2 origin = new Vector2(pictureRight ? 0f : half, frameHeight * 0.62f);

                if (flip)
                    origin.X = half - origin.X;

                Vector2 at = middle + HalfOffset(k);
                float tilt = k * 0.35f * EaseOut(timer / 8f);

                spriteBatch.Draw(texture, at - Main.screenPosition, frame, light, tilt, origin, 1f, effects, 0f);
            }
        }

        /// <summary>How far a cough doubles him over, this many ticks after it: quickly down, slowly back.</summary>
        private static float CoughBend(int since)
        {
            if (since < 0)
                return 0f;

            if (since < 6)
                return 0.24f * EaseOut(since / 6f);

            return 0.24f * (1f - Ease((since - 6) / 20f));
        }

        /// <summary>A red glow under the ground where he will come up (no old man): growing as the bone gathers.</summary>
        private void DrawGatherGlow(SpriteBatch spriteBatch)
        {
            float p = MathHelper.Clamp(beat == ArriveGather ? timer / (float)GatherTicks : 1f - timer / (float)BurstTicks, 0f, 1f);
            Vector2 at = anchor + new Vector2(0f, -16f);

            SkeletronRig.Glow(spriteBatch, at, new Color(200, 30, 20) * (0.5f * p), 60f + 70f * p);
            SkeletronRig.Hot(spriteBatch, at, new Color(255, 120, 80) * (0.7f * p), 14f + 16f * p);
        }

        // ---- shed, on each machine ---------------------------------------------------------------------------------------

        /// <summary>The old man's blood, still running from where his arms join him: a drip from each shoulder now and then.</summary>
        private void Bleed(NPC npc)
        {
            if (handCount == 0 || Main.rand.Next(5) != 0)
                return;

            float side = Main.rand.NextBool() ? -1f : 1f;

            if (HandOn(side) == null)
                return;

            Vector2 shoulder = SkeletronRig.Shoulder(npc.Center, side, npc.scale);
            Dust dust = Dust.NewDustPerfect(shoulder + Main.rand.NextVector2Circular(8f, 4f), DustID.Blood, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.5f, 2f)), 0, default, 1.4f);
            dust.noGravity = false;
        }

        /// <summary>His eyes on fire: flames off them, purple at the heart going red.</summary>
        private void EyeFlames(NPC npc)
        {
            for (int k = 0; k < 2; k++)
            {
                Vector2 eye = SkeletronRig.Eye(k == 0, npc.Center, npc.rotation, npc.scale, liftShown);
                Vector2 speed = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.5f, 3.5f)) + npc.velocity * 0.4f;
                Color tint = Color.Lerp(BlazePurple, BlazeRed, Main.rand.NextFloat());

                PADParticleHandler.SpawnParticle(new FlameSmoke(eye + Main.rand.NextVector2Circular(4f, 3f), speed, tint, Main.rand.Next(18, 30), 6f, 16f, 0.5f));

                if (Main.rand.NextBool(3))
                    PADParticleHandler.SpawnParticle(new Ember(eye, speed * 1.4f, tint, Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(20, 40)));
            }
        }

        /// <summary>His fire's two colours, mouth to end: the purple and the red (by request, "purple to red").</summary>
        internal static readonly Color BlazePurple = new Color(170, 70, 255);
        internal static readonly Color BlazeRed = new Color(255, 60, 30);
    }
}
