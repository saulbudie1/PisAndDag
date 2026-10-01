using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Legends
{
    // ---- the gel, alive --------------------------------------------------------------------------
    //
    // Vanilla draws her from a handful of frames through a shader that makes her gel. On a body
    // this big that reads as a picture of a slime being moved about, so every machine that draws
    // her also shapes her, from nothing but what it can see (on the ground or not, how fast and
    // which way, where vanilla's own tells are). Nothing here is sent, and nothing here moves her
    // or changes what she can hit. As the King's (LegendsSlimes), and more:
    //
    //     COIL     lower and wider as a tell runs out, trembling: the countdown to a hop, the
    //              wait before the slam, the gathering before the gel
    //     LAUNCH   tall and thin the moment she leaves the ground, gel thrown off her base
    //     AIR      drawn out by her speed, round at the top, leaning into her way; dripping
    //     LAND     flattened by how hard, then wobbling back; the crown hops off her head
    //     RIPPLE   a wave that runs up her body and dies (every landing, every blow she takes):
    //              she is drawn in slices and each slice swells and shifts in turn
    //     FLIGHT   her wings' beat through her (.Beat.cs): pressed by each push, thrown up by
    //              it, sinking between; a slow breath while she glides
    //
    // Her body is a fifth see-through (Opacity), over the core inside her.

    public partial class LegendsQueenSlime
    {
        /// <summary>How much of her body (and her wings) is there: the rest shows through.</summary>
        private const float Opacity = 0.8f;

        // Springs, as the King's.
        private const float JiggleStiffness = 0.2f;
        private const float JiggleDamping = 0.15f;
        private const float JiggleLimit = 0.42f;
        private const float Thinning = 0.7f;

        private const float LeanStiffness = 0.15f;
        private const float LeanDamping = 0.22f;
        private const float LeanPerSpeed = 0.03f;
        private const float LeanMost = 0.18f;

        private const float CrownStiffness = 0.1f;
        private const float CrownDamping = 0.09f;
        private const float CrownTiltMost = 0.4f;
        private const float CrownFall = 0.5f;
        private const float CrownBounce = 0.3f;

        /// <summary>Tall by the speed she leaves at, flat by the speed she lands at.</summary>
        private const float LaunchPerSpeed = 0.022f;
        private const float LandPerSpeed = 0.017f;
        private const float AirPerSpeed = 0.02f;
        private const float AirMost = 0.26f;

        /// <summary>The ripple: how fast it runs up her, how quickly it dies, and how much it swells her.</summary>
        private const float RippleSpeed = 0.33f;
        private const float RippleDecay = 0.94f;
        private const float RippleSwell = 0.55f;
        private const float RippleShift = 9f;

        /// <summary>At rest she is never quite still: this much ripple, always.</summary>
        private const float RippleRest = 0.018f;

        /// <summary>Her body in this many slices, bottom to top.</summary>
        private const int Slices = 14;

        /// <summary>A launch or landing this soon after the last is the collision putting her right, not her.</summary>
        private const int EventGap = 8;

        private bool standing;
        private bool primed;
        private float peakFall;
        private float floorY;
        private int sinceLaunch = EventGap;
        private int sinceLanding = EventGap;
        private float landingDrift;
        private int shapeClock;

        private float jiggle, jiggleSpeed;
        private float lean, leanSpeed;
        private float aloft;
        private float crownTilt, crownTiltSpeed;
        private float crownLift, crownLiftSpeed;
        private float quiver;

        /// <summary>The ripple: how big it is now, and where along it she is.</summary>
        private float ripple;
        private float ripplePhase;

        /// <summary>A stretch along her way that the scenes ask for (the dash), 0 none.</summary>
        private float dashStretch;

        /// <summary>A jerk of her whole body off true, radians, that the scenes throw in (her death): it springs back.</summary>
        private float twitch, twitchSpeed;

        /// <summary>She was coming down in vanilla's slam last tick: the landing it ends in is the slam's.</summary>
        private bool slamming;

        /// <summary>Added by the scenes for one tick: a kick to the shape, the lean, the ripple.</summary>
        private void Kick(float shape, float leanBy = 0f, float rippleBy = 0f)
        {
            jiggleSpeed += shape;
            leanSpeed += leanBy;
            ripple = Math.Min(1f, ripple + rippleBy);
        }

        /// <summary>A jerk off true, for the scenes: radians a tick, sprung back.</summary>
        private void Jerk(float by)
        {
            twitchSpeed += by;
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            // A blow runs through her.
            if (!Main.dedServ)
                ripple = Math.Min(0.6f, ripple + 0.06f + Math.Min(0.1f, hit.Damage / 800f));
        }

        /// <summary>One tick of her shape, after her AI and before she moves.</summary>
        private void Animate(NPC npc)
        {
            shapeClock++;
            sinceLaunch++;
            sinceLanding++;

            ripple = ripple * RippleDecay;
            ripplePhase += RippleSpeed;

            // Being carried by a scene: it shapes her itself.
            bool scripted = scene == Scene.Rite && beat < RiteFall;
            bool flying = npc.noGravity && !scripted;
            bool grounded = !npc.noGravity && npc.velocity.Y == 0f && !scripted;

            // Her wings' beat, where it is what moves them (the transition sets its own), and
            // the push of it through her.
            bool winged = Winged(npc);

            if (winged && BeatDrives() && AdvanceBeat(npc))
            {
                Kick(-0.05f * beatStrength, 0f, 0.04f);
                WingGust(npc, beatStrength);
            }

            beatBob += ((winged && flying ? BeatBobNow() : 0f) - beatBob) * 0.2f;

            if (winged)
            {
                WingLook wings = LookOfWings(npc);

                if (wings.Beat)
                    KeepTrail(wings.Frame);
                else
                    beatTrailCount = 0;
            }

            if (scripted || !primed)
            {
                primed = !scripted;
                standing = grounded;
            }
            else if (standing && !grounded)
            {
                standing = false;
                peakFall = 0f;

                if (!flying)
                    Launched(npc);
            }
            else if (!standing && grounded)
            {
                standing = true;
                Landed(npc);
            }

            if (standing)
                floorY = npc.position.Y + npc.height;
            else
                peakFall = Math.Max(peakFall, npc.velocity.Y);

            float coil = grounded ? Coil(npc) : 0f;
            float target = 0f;
            float leanTarget = 0f;

            if (grounded)
            {
                target = -0.2f * coil;
                leanTarget = -npc.direction * 0.05f * coil;
            }
            else if (flying)
            {
                // Her wings' beat through her (a slow breath without them), and leaning into her way.
                float breath = winged ? BeatGelNow() : 0.035f * (float)Math.Sin(shapeClock * 0.07f);

                target = breath + Math.Min(0.12f, Math.Abs(npc.velocity.Y) * 0.012f);
                leanTarget = MathHelper.Clamp(npc.velocity.X * LeanPerSpeed * 0.6f, -LeanMost, LeanMost);
            }
            else if (!scripted)
            {
                float speed = Math.Abs(npc.velocity.Y);
                target = Math.Min(AirMost, speed * AirPerSpeed) - 0.05f * Math.Max(0f, 1f - speed / 3f);
                leanTarget = MathHelper.Clamp(npc.velocity.X * LeanPerSpeed, -LeanMost, LeanMost);
            }

            // The slam: gathered while she hangs, stretched while she drops. Her dying slams too.
            bool slamNow = scene == Scene.None && npc.ai[0] == 4f && npc.ai[2] == 1f && !grounded;

            if (slamNow || (scene == Scene.Death && beat == DeathFrenzy && !grounded))
                target = npc.velocity.Y > 1f ? Math.Min(0.3f, 0.12f + npc.velocity.Y * 0.012f) : -0.08f;

            if (dashStretch > 0f)
            {
                target = -0.18f * dashStretch;
                leanTarget = MathHelper.Clamp(npc.velocity.X * 0.012f, -0.3f, 0.3f);
            }

            quiver = Math.Max(quiver * 0.9f, grounded ? coil : 0f);

            jiggleSpeed += (target - jiggle) * JiggleStiffness - jiggleSpeed * JiggleDamping;
            jiggle = MathHelper.Clamp(jiggle + jiggleSpeed, -JiggleLimit, JiggleLimit);

            // The turn up out of a landing's squash throws the crown.
            if (grounded && sinceLanding <= 24 && jiggleSpeed >= 0f && jiggleSpeed - (target - jiggle) * JiggleStiffness < 0f && jiggle < -0.08f)
                PopCrown(-jiggle);

            leanSpeed += (leanTarget - lean) * LeanStiffness - leanSpeed * LeanDamping;
            lean += leanSpeed;

            aloft += ((grounded ? 0f : 1f) - aloft) * 0.3f;

            crownTiltSpeed += -crownTilt * CrownStiffness - crownTiltSpeed * CrownDamping;
            crownTilt = MathHelper.Clamp(crownTilt + crownTiltSpeed, -CrownTiltMost, CrownTiltMost);

            if (crownLift > 0f || crownLiftSpeed > 0f)
            {
                crownLiftSpeed -= CrownFall;
                crownLift += crownLiftSpeed;

                if (crownLift <= 0f)
                {
                    crownLift = 0f;
                    crownLiftSpeed = crownLiftSpeed < -1.2f ? -crownLiftSpeed * CrownBounce : 0f;
                }
            }

            dashStretch *= 0.85f;

            twitchSpeed += -twitch * 0.25f - twitchSpeed * 0.2f;
            twitch = MathHelper.Clamp(twitch + twitchSpeed, -0.5f, 0.5f);

            // The light of her glow, on the world round her.
            float glow = Math.Max(BackLight(), OverLight());

            if (glow > 0.01f)
                Lighting.AddLight(npc.Center, QueenSlimeArt.Shade(shapeClock * 0.02f).ToVector3() * (1.2f * glow));

            slamming = slamNow && npc.velocity.Y > 1f;

            if (!grounded && !scripted && !flying)
                Drip(npc, 6);
            else if (flying && Main.rand.NextBool(10))
                Drip(npc, 1);
        }

        /// <summary>
        /// How far through one of vanilla's tells she is, 0 to 1, off its own clock: the countdown
        /// to a hop (state 3, ai[1] climbing to nothing), the wait before the slam (state 4, the
        /// first thirty ticks), the gathering before the gel (state 5).
        /// </summary>
        private float Coil(NPC npc)
        {
            if (scene == Scene.Boomerang && beat == BoomTell)
                return 0.35f + 0.4f * Math.Min(1f, timer / (float)BoomTellTicks);

            // Gathering herself for the wings; shuddering at the last.
            if (scene == Scene.Wings && beat == WingsCrouch)
                return Math.Min(1f, timer / (float)(CrouchTicks - 6));

            // Down into herself as her wings go up for the first beat.
            if (scene == Scene.Wings && beat == WingsTakeoff && timer < PushAt)
                return 0.5f * Ease(timer / (float)PushAt);

            if (scene == Scene.Death && beat == DeathShudder)
                return 0.5f + 0.5f * Math.Min(1f, timer / (float)ShudderTicks);

            if (scene != Scene.None)
                return 0f;

            switch ((int)npc.ai[0])
            {
                case 3:
                    return MathHelper.Clamp((npc.ai[1] + 40f) / 40f, 0f, 1f);

                case 4:
                    return npc.ai[2] == 0f ? MathHelper.Clamp(npc.ai[1] / 30f, 0f, 1f) : 0f;

                case 5:
                    return npc.ai[2] == 0f ? 0.6f * MathHelper.Clamp(npc.ai[1] / 50f, 0f, 1f) : 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>She has left the ground: tall at once, the crown tipped back, gel and dirt thrown off her base.</summary>
        private void Launched(NPC npc)
        {
            if (sinceLaunch < EventGap)
            {
                sinceLaunch = 0;
                return;
            }

            sinceLaunch = 0;

            float launch = MathHelper.Clamp(-npc.velocity.Y * LaunchPerSpeed, 0.1f, 0.3f);
            jiggle += (launch - jiggle) * 0.7f;
            jiggleSpeed = 0.04f;
            crownTiltSpeed -= npc.direction * 0.03f;
            ripple = Math.Min(1f, ripple + 0.08f);

            float strength = MathHelper.Clamp(-npc.velocity.Y / 10f, 0.4f, 1.4f);
            ThrowOff(npc, strength);
        }

        /// <summary>She has come down: thrown flat by how hard, the ripple sent up her, gel splashed off both sides.</summary>
        private void Landed(NPC npc)
        {
            if (sinceLanding < EventGap)
            {
                sinceLanding = 0;
                return;
            }

            sinceLanding = 0;
            landingDrift = npc.velocity.X;

            jiggleSpeed -= MathHelper.Clamp(peakFall * LandPerSpeed, 0.06f, 0.26f);
            ripple = Math.Min(1f, ripple + MathHelper.Clamp(peakFall * 0.018f, 0.05f, 0.3f));

            Splash(npc, MathHelper.Clamp(peakFall / 12f, 0.4f, 1.5f));

            // The end of her slam: the whole of her weight, and the ground heaved out from under
            // her. Vanilla has already made its sound, its dust and its shockwave.
            if (slamming)
            {
                slamming = false;
                ripple = Math.Min(1f, ripple + 0.25f);
                QueenLand(npc, 1f, heave: true, sound: false);
                return;
            }

            // A landing a scene plays its own way (her arrival, her death).
            if (LandedInScene(npc))
                return;

            // Every other landing is felt a little.
            if (peakFall > 6f)
                QueenLand(npc, MathHelper.Clamp(peakFall / 18f, 0.3f, 0.7f), heave: false);
        }

        /// <summary>A landing of one of her scenes: played by the scene, heavier. False if none has her.</summary>
        private bool LandedInScene(NPC npc)
        {
            if (scene == Scene.Rite && beat == RiteFall)
            {
                RiteLanded(npc);
                return true;
            }

            if (scene == Scene.Death && (beat == DeathFall || beat == DeathFrenzy || beat == DeathStill))
            {
                DeathLanded(npc);
                return true;
            }

            return false;
        }

        private void PopCrown(float depth)
        {
            crownLiftSpeed = Math.Max(crownLiftSpeed, Math.Min(4.5f, depth * 17f));

            float way = Math.Abs(landingDrift) > 0.5f ? Math.Sign(landingDrift) : Main.rand.NextBool() ? 1f : -1f;
            crownTiltSpeed += way * depth * 0.24f;
        }

        // ---- her gel ---------------------------------------------------------------------------------

        private static Color GelColour() => QueenSlimeArt.Gel[Main.rand.Next(QueenSlimeArt.Gel.Length)];

        /// <summary>Her launch: a wet sound, gel thrown out and up off both edges of her base, and the ground kicked up.</summary>
        private void ThrowOff(NPC npc, float strength)
        {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.3f - 0.2f * strength, Volume = 0.35f + 0.3f * strength, MaxInstances = 3 }, npc.Bottom);

            float half = npc.width * 0.5f;

            for (int i = 0; i < 3 + (int)(5f * strength); i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                Vector2 from = new Vector2(npc.Center.X + s * half * Main.rand.NextFloat(0.5f, 0.95f), floorY - Main.rand.NextFloat(2f, 12f));
                Vector2 velocity = new Vector2(s * Main.rand.NextFloat(1.5f, 4.5f), Main.rand.NextFloat(-4.5f, -1.5f)) * (0.6f + 0.4f * strength);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.6f, 1.05f), 80, GelColour()));
            }

            QueenDust(new Vector2(npc.position.X, floorY - 10f), npc.width, 10, 8 + (int)(10f * strength), 5f * (0.6f + 0.4f * strength));
            LegendsKingSlime.GroundDust(npc.position.X, npc.position.X + npc.width, floorY, 0.2f * strength);
        }

        /// <summary>A landing's spray: gel splashed off both sides of her, to fall and splat.</summary>
        private static void Splash(NPC npc, float weight)
        {
            float half = npc.width * 0.5f;
            float floor = npc.position.Y + npc.height;

            for (int i = 0; i < 3 + (int)(6f * weight); i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                Vector2 from = new Vector2(npc.Center.X + s * half * Main.rand.NextFloat(0.6f, 1f), floor - Main.rand.NextFloat(4f, 14f));
                Vector2 velocity = new Vector2(s * Main.rand.NextFloat(2.5f, 7f), Main.rand.NextFloat(-5.5f, -2f)) * (0.7f + 0.3f * weight);

                PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.7f, 1.2f), 90, GelColour()));
            }

            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f, Volume = 0.3f + 0.25f * weight, MaxInstances = 3 }, npc.Bottom);
        }

        /// <summary>Gel dripping off her in the air: one in <paramref name="every"/> ticks, on average.</summary>
        private static void Drip(NPC npc, int every)
        {
            if (!Main.rand.NextBool(Math.Max(1, every)))
                return;

            Vector2 from = new Vector2(npc.Center.X + Main.rand.NextFloat(-0.35f, 0.35f) * npc.width, npc.position.Y + npc.height - Main.rand.NextFloat(4f, 16f));
            Vector2 velocity = npc.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.5f, 1.5f));

            PADParticleHandler.SpawnParticle(new SlimeGlob(from, velocity, Main.rand.NextFloat(0.45f, 0.8f), 90, GelColour()));
        }

        /// <summary>Her gel as dust, in her colours: vanilla's slime dust tinted the way her own AI tints it.</summary>
        private static void QueenDust(Vector2 at, int width, int height, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Color tint = NPC.AI_121_QueenSlime_GetDustColor();
                tint.A = 150;

                Dust dust = Dust.NewDustDirect(at, width, height, DustID.TintableDust, 0f, 0f, 50, tint, Main.rand.NextFloat(1.1f, 1.7f));
                dust.velocity = new Vector2(Main.rand.NextFloat(-speed, speed), Main.rand.NextFloat(-speed * 0.6f, -0.5f));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }

        /// <summary>
        /// Her full landing: a low sheet of dust thrown out, gel shaken off, the ground kicked up,
        /// the sound of her own slam, and the screen shaken, less the further off it is.
        /// <paramref name="heave"/> also runs the ground out from under her in a wave
        /// (GroundWave), for the slams that deserve it; <paramref name="sound"/> false for a
        /// landing whose sound vanilla has already made.
        /// </summary>
        private static void QueenLand(NPC npc, float weight, bool heave, bool sound = true)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < (int)(20 * weight); i++)
            {
                Dust dust = Dust.NewDustDirect(npc.Bottom - new Vector2(npc.width / 2f, 30f), npc.width, 30, DustID.Smoke, 0f, 0f, 40, default, 1.5f * weight);
                dust.noGravity = true;
                dust.velocity.Y = -5f + Main.rand.NextFloat() * -3f;
                dust.velocity.X *= 7f;
            }

            QueenDust(new Vector2(npc.position.X, npc.position.Y + npc.height - 8f), npc.width, 12, (int)(26 * weight), 6f);
            LegendsKingSlime.GroundDust(npc.position.X, npc.position.X + npc.width, npc.position.Y + npc.height, weight);

            if (sound)
                SoundEngine.PlaySound(SoundID.Item167 with { Volume = MathHelper.Clamp(weight, 0.3f, 1f) }, npc.Center);

            float near = Near(npc.Center);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(10f * weight * near, (int)(24 * weight));

            if (heave)
                GroundWave.Slam(npc.Bottom, npc.width * 0.5f, (int)(22 + 16 * weight), 12f + 10f * weight, 34);
        }

        /// <summary>How much of something here this machine's player feels: all of it within 700, none past 2100.</summary>
        private static float Near(Vector2 at)
        {
            if (Main.dedServ)
                return 0f;

            return 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 700f) / 1400f, 0f, 1f);
        }

        // ---- her shape, drawn --------------------------------------------------------------------------

        /// <summary>
        /// Where and how she is drawn this frame: her feet (the bottom of her frame), the point she
        /// turns about, her stretch (x wider, y taller), her turn, her size, and her frame.
        /// </summary>
        private readonly struct Pose
        {
            public readonly Vector2 Feet;
            public readonly Vector2 Pivot;
            public readonly Vector2 Stretch;
            public readonly float Turn;
            public readonly float Size;
            public readonly int Frame;
            public readonly SpriteEffects Effects;

            public Pose(Vector2 feet, Vector2 pivot, Vector2 stretch, float turn, float size, int frame, SpriteEffects effects)
            {
                Feet = feet;
                Pivot = pivot;
                Stretch = stretch;
                Turn = turn;
                Size = size;
                Frame = frame;
                Effects = effects;
            }

            /// <summary>A point given from her feet, in her own unstretched pixels, to the world.</summary>
            public Vector2 At(Vector2 local)
            {
                Vector2 p = Feet + new Vector2(local.X * Stretch.X, local.Y * Stretch.Y) - Pivot;
                return Pivot + p.RotatedBy(Turn);
            }
        }

        /// <summary>Her frame on vanilla's sheet, 0 to 23, as vanilla's FindFrame left it.</summary>
        private static int FrameOf(NPC npc) => npc.frame.Height > 0 ? Math.Clamp(npc.frame.Y / npc.frame.Height, 0, 23) : 0;

        private Pose Posed(NPC npc, float grow = 1f)
        {
            float shape = jiggle;
            float wide = 1f;

            if (quiver > 0f)
            {
                shape += 0.022f * quiver * (float)Math.Sin(shapeClock * 1.9f);
                wide += 0.02f * quiver * (float)Math.Sin(shapeClock * 2.7f);
            }

            Vector2 stretch = new Vector2((float)Math.Pow(1f + shape, -Thinning) * wide, 1f + shape) * grow;

            if (dashStretch > 0f)
                stretch.X *= 1f + 0.3f * dashStretch;

            // Her rise and fall with her wings' beat.
            Vector2 feet = new Vector2(npc.Bottom.X, npc.Bottom.Y + 2f + beatBob * npc.scale);

            // Grown about her middle, where her core is, not about her feet: she comes out of it.
            if (grow != 1f)
            {
                Vector2 middle = feet - new Vector2(0f, npc.height * 0.5f);
                feet = middle + (feet - middle) * grow;
            }

            Vector2 pivot = feet - new Vector2(0f, npc.height * 0.5f * grow * aloft);

            // Vanilla's own mirror of her, then its flip on top (Main.DrawNPCDirect_Inner).
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            return new Pose(feet, pivot, stretch, npc.rotation + lean + twitch, npc.scale, FrameOf(npc), effects);
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.IsABestiaryIconDummy)
                return true;

            Main.instance.LoadNPC(npc.type);

            if (scene == Scene.Rite && beat < RiteGrow)
            {
                DrawRiteLight(npc, spriteBatch, screenPos);
                return false;
            }

            if (scene == Scene.Death && beat == DeathBurst)
                return false;

            Color lit = Color.Lerp(Color.White, drawColor, 0.5f);
            Pose pose = Posed(npc, RiteGrowth());

            DrawWings(npc, spriteBatch, screenPos, lit, pose);
            DrawBackLight(npc, spriteBatch, screenPos, pose);
            DrawTrail(npc, spriteBatch, screenPos, lit, pose);

            if (!coreOut)
                DrawCore(npc, spriteBatch, screenPos, lit, pose);

            // Whoever she holds is drawn inside her: her body goes on over them, after the
            // players (DrawHolding).
            if (!HoldingAnyone(npc))
            {
                DrawBody(npc, spriteBatch, screenPos, lit, pose);
                DrawCrown(npc, spriteBatch, screenPos, lit, pose);
                DrawOverLight(npc, spriteBatch, screenPos, pose);
            }

            return false;
        }

        /// <summary>Her core, where vanilla puts it, riding her shape; red while she means to throw it.</summary>
        private void DrawCore(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lit, Pose pose)
        {
            Texture2D core = TextureAssets.Extra[ExtrasID.QueenSlimeCrystalCore].Value;
            Vector2 at = CoreWorld(npc, pose) - screenPos;
            float size = CoreSize();

            spriteBatch.Draw(core, at, null, lit, pose.Turn, core.Size() * 0.5f, size, pose.Effects, 0f);

            // Still the light she came out of, for a moment (her arrival).
            float white = CoreWhite();

            if (white > 0f)
                DrawWhiteCore(spriteBatch, at, pose.Turn, size, white);

            float red = RedGlow();

            if (red > 0f)
                DrawRed(spriteBatch, at, pose.Turn, size, red);
        }

        /// <summary>Where her core is in the world: her middle, raised or lowered by her frame, riding her shape.</summary>
        private Vector2 CoreWorld(NPC npc, Pose pose)
        {
            float lift = scene == Scene.Boomerang && beat == BoomTell ? -14f * Math.Min(1f, timer / 20f) : 0f;
            Vector2 local = new Vector2(0f, -npc.height * 0.5f + QueenSlimeArt.CoreRise[pose.Frame] + lift);
            Vector2 at = pose.At(local);

            // Coming out of the light it hung in, as she grows round it.
            if (scene == Scene.Rite && beat == RiteGrow)
                at = Vector2.Lerp(riteFocus + new Vector2(0f, Floating()), at, Ease(timer / (float)GrowTicks));

            return at;
        }

        /// <summary>Smoothstep, clamped.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>0 below <paramref name="from"/>, 1 above <paramref name="to"/>, smooth between.</summary>
        private static float Smooth(float from, float to, float t) => Ease((t - from) / (to - from));

        /// <summary>
        /// Her body, in slices so a ripple can run up it, through vanilla's own shader for her gel
        /// (GameShaders.Misc["QueenSlime"]), a fifth see-through. Expects the game's own batch
        /// open, and leaves it open the same way.
        /// </summary>
        private void DrawBody(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lit, Pose pose)
        {
            Texture2D sheet = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = QueenSlimeArt.BodyFrame(pose.Frame);
            Color colour = npc.GetAlpha(lit) * Opacity;

            // Exactly as vanilla draws her body (Main.DrawNPCDirect_Inner): an immediate batch, the
            // shader applied with no draw data, her drawn, and the game's batch and its pixel
            // shader put back. The slices are drawn from the same sheet the same way, so the
            // shader sees each of her pixels as it would have.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.Transform);

            GameShaders.Misc["QueenSlime"].Apply();

            DrawSlices(spriteBatch, sheet, frame, colour, pose, screenPos);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>
        /// Her frame in slices, bottom to top, each swollen and shifted by the ripple where it is
        /// on its way up her; the lowest slice is held to the ground. All of them turn about the
        /// same point, so at rest they are her frame exactly.
        /// </summary>
        private void DrawSlices(SpriteBatch spriteBatch, Texture2D sheet, Rectangle frame, Color colour, Pose pose, Vector2 screenPos)
        {
            float size = pose.Size;
            Vector2 scale = pose.Stretch * size;

            // The pivot, in the frame's own pixels: her feet, raised to where she turns about.
            float pivotUp = (pose.Feet.Y - pose.Pivot.Y) / Math.Max(0.01f, scale.Y);
            Vector2 pivotInFrame = new Vector2(frame.Width * 0.5f, frame.Height - pivotUp);

            float amount = ripple + RippleRest;
            bool flipped = (pose.Effects & SpriteEffects.FlipHorizontally) != 0;

            for (int i = 0; i < Slices; i++)
            {
                int top = frame.Height * i / Slices;
                int bottom = frame.Height * (i + 1) / Slices;

                if (bottom <= top)
                    continue;

                // Up from her feet, 0 at the bottom of her frame, 1 at the top.
                float up = 1f - (top + bottom) * 0.5f / frame.Height;
                float wave = amount * (float)Math.Pow(up, 1.2) * (float)Math.Sin(ripplePhase - up * 3.2f);

                Rectangle slice = new Rectangle(frame.X, frame.Y + top, frame.Width, bottom - top);
                Vector2 origin = new Vector2(pivotInFrame.X, pivotInFrame.Y - top);
                Vector2 sliceScale = new Vector2(scale.X * (1f + RippleSwell * wave), scale.Y);
                Vector2 shift = new Vector2(RippleShift * wave * (flipped ? -1f : 1f) * size, 0f).RotatedBy(pose.Turn);

                spriteBatch.Draw(sheet, pose.Pivot + shift - screenPos, slice, colour, pose.Turn, origin, sliceScale, pose.Effects, 0f);
            }
        }

        /// <summary>Her crown, where vanilla puts it, riding her shape, tipped and hopping on its own springs.</summary>
        private void DrawCrown(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lit, Pose pose)
        {
            if (!CrownOn())
                return;

            Texture2D crown = TextureAssets.Extra[ExtrasID.QueenSlimeCrown].Value;
            Vector2 local = new Vector2(0f, -npc.height - 12f + QueenSlimeArt.CrownRise[pose.Frame]);
            Vector2 at = pose.At(local) - new Vector2(0f, crownLift + CrownDrop()).RotatedBy(pose.Turn) - screenPos;

            spriteBatch.Draw(crown, at, null, lit, pose.Turn + crownTilt, crown.Size() * 0.5f, 1f, pose.Effects, 0f);
        }

        /// <summary>Her afterimages: dark and fading behind the slam's drop and the dash, as vanilla draws the drop.</summary>
        private void DrawTrail(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lit, Pose pose)
        {
            bool dropping = npc.ai[0] == 4f && npc.velocity.Y > 2f && scene == Scene.None;
            bool dashing = scene == Scene.Dash && beat == DashCharge;

            if (!dropping && !dashing)
                return;

            Texture2D sheet = TextureAssets.Npc[npc.type].Value;
            Rectangle frame = QueenSlimeArt.BodyFrame(pose.Frame);
            int count = Math.Min(8, npc.oldPos.Length);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.Transform);

            GameShaders.Misc["QueenSlime"].Apply();

            for (int i = count - 1; i >= 1; i--)
            {
                Vector2 old = npc.oldPos[i] + new Vector2(npc.width * 0.5f, npc.height + 2f);

                if (old == new Vector2(npc.width * 0.5f, npc.height + 2f))
                    continue;

                float strength = (1f - i / (float)count) * 0.45f * Opacity;

                spriteBatch.Draw(sheet, old - screenPos, frame, lit * strength, pose.Turn, new Vector2(frame.Width * 0.5f, frame.Height),
                    pose.Stretch * pose.Size, pose.Effects, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.Transform);

            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        // ---- her wings -----------------------------------------------------------------------------------

        /// <summary>
        /// How her wings are drawn: from the beat's sheet (QueenSlimeWingbeat) or the unfolding's
        /// (QueenSlimeWings), which frame, how big, how far raised (radians, up), how much there,
        /// how lit (1 as the world lights them), and how much of her colours is in them.
        /// </summary>
        private readonly struct WingLook
        {
            public readonly bool Beat;
            public readonly int Frame;
            public readonly float Size;
            public readonly float Raise;
            public readonly float Alpha;
            public readonly float Lit;
            public readonly float Sheen;

            public WingLook(bool beat, int frame, float size, float raise = 0f, float alpha = 1f, float lit = 1f, float sheen = 0.1f)
            {
                Beat = beat;
                Frame = frame;
                Size = size;
                Raise = raise;
                Alpha = alpha;
                Lit = lit;
                Sheen = sheen;
            }
        }

        /// <summary>Whether her beat is what moves her wings now (flying, or anything but the dash's tell and charge in it).</summary>
        private bool BeatDrives() =>
            scene == Scene.None || (scene == Scene.Dash && beat != DashTell && beat != DashCharge);

        /// <summary>How her wings are, this frame.</summary>
        private WingLook LookOfWings(NPC npc)
        {
            if (scene == Scene.Wings)
                return ScriptedWings();

            if (scene == Scene.Dash && beat == DashCharge)
            {
                // Swept in for the dash.
                return new WingLook(false, 2, FlightWingSize, -0.35f);
            }

            if (scene == Scene.Dash && beat == DashTell)
            {
                // Raised high in the breath before it.
                return new WingLook(true, QueenSlimeArt.BeatTop, FlightWingSize, 0.08f);
            }

            if (scene == Scene.Death && beat == DeathSpasm)
            {
                // Beating at nothing: fast, and out of time with itself.
                int frame = shapeClock * 2 + (int)(Math.Sin(shapeClock * 0.7f) * 4f);
                return new WingLook(true, frame, FlightWingSize, 0.25f * (float)Math.Sin(shapeClock * 1.3f));
            }

            float pose = PoseAt(Gliding ? GlideAt : beatPhase);

            return new WingLook(true, BeatFrameOf(pose), FlightWingSize, 0f, 1f, Keyed(BeatLit, beatPhase), Keyed(BeatSheen, beatPhase));
        }

        /// <summary>
        /// How big her wings are in flight: each reaches about 235 pixels out from its root, so
        /// with her they span about three times her own width.
        /// </summary>
        private const float FlightWingSize = 0.8f;

        private bool Winged(NPC npc)
        {
            if (wingsBroken)
                return false;

            if (scene == Scene.Wings)
                return beat >= WingSprout;

            if (scene == Scene.Rite)
                return false;

            return transitioned || (Halved(npc) && scene != Scene.None);
        }

        /// <summary>
        /// Her wings, behind her: the right one from its root on her upper back, the left the
        /// same mirrored, both riding her shape and tipping with her rise and fall; while a beat
        /// sweeps fast, faint shadows of where it was three and six ticks before.
        /// </summary>
        private void DrawWings(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color lit, Pose pose)
        {
            if (!Winged(npc) || QueenSlimeArt.Wings == null || QueenSlimeArt.Wingbeat == null)
                return;

            WingLook look = LookOfWings(npc);

            if (look.Size <= 0.01f || look.Alpha <= 0.01f)
                return;

            Texture2D wings = look.Beat ? QueenSlimeArt.Wingbeat.Value : QueenSlimeArt.Wings.Value;
            Rectangle frame = look.Beat ? QueenSlimeArt.BeatFrame(look.Frame) : QueenSlimeArt.WingFrame(look.Frame);
            Vector2 rootInFrame = look.Beat ? QueenSlimeArt.BeatRoot : QueenSlimeArt.WingRoot;
            int width = look.Beat ? QueenSlimeArt.BeatWidth : QueenSlimeArt.WingWidth;
            float tip = MathHelper.Clamp(npc.velocity.Y, -6f, 6f) * 0.03f;

            // The underside dark as it rises: the colour darkened, not thinned.
            Color colour = new Color((int)(lit.R * look.Lit), (int)(lit.G * look.Lit), (int)(lit.B * look.Lit), lit.A) * (Opacity * look.Alpha);
            float shadows = look.Beat ? TrailStrength() : 0f;

            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 root = pose.At(new Vector2(s * 30f, -npc.height * 0.64f + WingSink())) - screenPos;
                bool left = s < 0;
                Vector2 origin = left ? new Vector2(width - rootInFrame.X, rootInFrame.Y) : rootInFrame;
                float turn = pose.Turn - s * look.Raise + s * tip;
                SpriteEffects effects = left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                if (shadows > 0f)
                {
                    spriteBatch.Draw(wings, root, QueenSlimeArt.BeatFrame(TrailFrame(6)), colour * (0.08f * shadows), turn, origin, look.Size, effects, 0f);
                    spriteBatch.Draw(wings, root, QueenSlimeArt.BeatFrame(TrailFrame(3)), colour * (0.16f * shadows), turn, origin, look.Size, effects, 0f);
                }

                spriteBatch.Draw(wings, root, frame, colour, turn, origin, look.Size, effects, 0f);

                // A little of her colours in them, going round.
                Color sheen = QueenSlimeArt.Additive(QueenSlimeArt.Shade(shapeClock * 0.012f + s), look.Sheen * look.Alpha);
                spriteBatch.Draw(wings, root, frame, sheen, turn, origin, look.Size, effects, 0f);
            }
        }

        /// <summary>Where a wing's root is along her back, in her own pixels from her feet: for the shards when they break.</summary>
        private static Vector2 WingRootLocal(NPC npc, int s) => new Vector2(s * 30f, -npc.height * 0.64f);

        // ---- her light ------------------------------------------------------------------------------------
        //
        // The light she comes out of and dies in, both ways vanilla has none of: a glow in her
        // colours behind her (her standing frame, blurred), and her own shape in white over her.

        /// <summary>How strong the glow behind her is now, 0 none.</summary>
        private float BackLight()
        {
            switch (scene)
            {
                case Scene.Rite:
                    return RiteBackLight();

                case Scene.Wings:
                    return WingsBackLight();

                case Scene.Death:
                    return DeathBackLight();

                default:
                    return 0f;
            }
        }

        /// <summary>How white she is now, 0 not at all, 1 all light.</summary>
        private float OverLight()
        {
            switch (scene)
            {
                case Scene.Rite:
                    return RiteOverLight();

                case Scene.Death:
                    return DeathOverLight();

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// A glow in her colours round something made of light: three tints of her colours going
        /// round, the widest the faintest, and a little light added over the nearest. Tints, not
        /// light alone: light added to the bright day sky of the Hallow is only white, where a tint
        /// shows on that as it does in the dark. <paramref name="inner"/>, <paramref name="middle"/>
        /// and <paramref name="outer"/> are the three sizes, times <paramref name="scale"/>.
        /// </summary>
        private void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Rectangle? frame, Vector2 at, Vector2 origin, float turn, Vector2 scale,
            SpriteEffects effects, float strength, float inner, float middle, float outer)
        {
            float t = shapeClock * 0.02f;

            spriteBatch.Draw(texture, at, frame, QueenSlimeArt.Shade(t + 2f) * Math.Min(1f, 0.3f * strength), turn, origin, scale * outer, effects, 0f);
            spriteBatch.Draw(texture, at, frame, QueenSlimeArt.Shade(t + 1f) * Math.Min(1f, 0.5f * strength), turn, origin, scale * middle, effects, 0f);
            spriteBatch.Draw(texture, at, frame, QueenSlimeArt.Shade(t) * Math.Min(1f, 0.8f * strength), turn, origin, scale * inner, effects, 0f);
            spriteBatch.Draw(texture, at, frame, QueenSlimeArt.Additive(QueenSlimeArt.Shade(t), 0.25f * strength), turn, origin, scale * (inner * 0.95f), effects, 0f);
        }

        /// <summary>The glow behind her (DrawGlow), in her own shape blurred.</summary>
        private void DrawBackLight(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Pose pose)
        {
            float strength = BackLight();

            if (strength <= 0.01f || QueenSlimeArt.Halo == null)
                return;

            // Her feet in the halo: the pad, then where vanilla's origin sits in her unshaved frame.
            Vector2 origin = new Vector2(QueenSlimeArt.HaloPad + QueenSlimeArt.FrameWidth * 0.5f, QueenSlimeArt.HaloPad + QueenSlimeArt.FrameHeight - 2f);

            DrawGlow(spriteBatch, QueenSlimeArt.Halo.Value, null, pose.Feet - screenPos, origin, pose.Turn, pose.Stretch * pose.Size, pose.Effects,
                strength, 1.04f, 1.14f, 1.3f);
        }

        /// <summary>Her shape in white over her, sliced as her body is so it ripples with her.</summary>
        private void DrawOverLight(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Pose pose)
        {
            float white = OverLight();

            if (white <= 0.01f || QueenSlimeArt.White == null)
                return;

            Texture2D sheet = QueenSlimeArt.White.Value;
            Rectangle frame = QueenSlimeArt.BodyFrame(pose.Frame);

            DrawSlices(spriteBatch, sheet, frame, Color.White * Math.Min(1f, white), pose, screenPos);
        }

        /// <summary>The white of the core's shape over the core, and its glow: the light it still is as she grows round it.</summary>
        private void DrawWhiteCore(SpriteBatch spriteBatch, Vector2 at, float turn, float size, float white)
        {
            if (QueenSlimeArt.Morph == null || QueenSlimeArt.MorphHalo == null)
                return;

            Rectangle frame = QueenSlimeArt.MorphFrame(QueenSlimeArt.MorphFrames - 1);
            Rectangle haloFrame = QueenSlimeArt.MorphHaloFrame(QueenSlimeArt.MorphFrames - 1);

            DrawGlow(spriteBatch, QueenSlimeArt.MorphHalo.Value, haloFrame, at, haloFrame.Size() * 0.5f, turn, new Vector2(size), SpriteEffects.None,
                white, 1.2f, 1.8f, 2.8f);

            spriteBatch.Draw(QueenSlimeArt.Morph.Value, at, frame, Color.White * white, turn, frame.Size() * 0.5f, size, SpriteEffects.None, 0f);
        }
    }
}
