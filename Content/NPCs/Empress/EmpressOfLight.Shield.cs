using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- the mirror barrier (Legends act, her second half) -------------------------------------------------------
    //
    // By request: "Standstill but if create an invisible barrier around her body, player attacks and its like doctor
    // strange's mirror shield move ... the more damage the more it cracks. After taking about 2000 of culmilative damage,
    // it pushes at player until it reachs borders and explode."
    //
    //     RAISE   30   she stops, her free hand out: about her a sphere of glass is made, unseen but for how the world
    //                  bends through it, and a glint going round its edge as it closes
    //     HOLD  ≤480   she is still; every blow on her strikes the glass instead, none of it reaching her, and cracks it
    //                  where it lands, more the harder (the world's own numbers not shown: the glass shows it); at two
    //                  thousand of it, or eight seconds whichever first,
    //     PUSH  ≤260   it comes off her at the player, faster and faster, pushing whoever it meets before it, until it
    //                  meets the border
    //     BURST   40   and bursts there: glass every way, a blast of light (100 in a normal world, 65 in an expert one,
    //                  doubled as the game doubles any hostile shot)
    //
    // What it takes is counted by the server (its word sent); every machine puts back what a blow took of her, as each
    // machine takes it. Each machine cracks the glass where it sees a blow land.

    public partial class EmpressOfLight
    {
        private const int ShieldRaise = 0;
        private const int ShieldHold = 1;
        private const int ShieldPush = 2;
        private const int ShieldBurst = 3;

        private const int ShieldRaiseTicks = 30;
        private const int ShieldHoldMost = 480;
        private const int ShieldBurstTicks = 40;

        /// <summary>The sphere: middle to its edge; what it takes before it goes (by request, "about 2000"); its blast's reach.</summary>
        private const float ShieldRadius = 160f;
        private const int ShieldBreaks = 2000;
        private const float ShieldBlast = 380f;

        /// <summary>How fast it goes once it comes off her: at first, the most, and how fast it gathers.</summary>
        private const float ShieldStart = 4f;
        private const float ShieldMost = 24f;
        private const float ShieldGather = 0.36f;

        private const int ShieldNormal = 100;
        private const int ShieldExpert = 65;

        /// <summary>What the glass has taken (the server's count, sent), and whether the server owes the world a word of it.</summary>
        private int shieldTaken;
        private bool shieldOwed;

        /// <summary>A crack in the glass, each machine's own: where on the sphere it struck (its way from her, how far out), how big it has grown, its own seed.</summary>
        private struct GlassCrack
        {
            public float Angle;
            public float Out;
            public float Size;
            public int Seed;
        }

        private readonly List<GlassCrack> glass = new();

        /// <summary>The last blow seen: when, and where on the sphere.</summary>
        private uint struckAt;
        private float struckWay;

        private void PlanShield(NPC npc, Player target)
        {
            shieldTaken = 0;
            actTurn = (target.Center - npc.Center).ToRotation();
        }

        /// <summary>Does the glass take her blows this tick?</summary>
        private bool Shielding => scene == Scene.Act && act == Act.Shield && (beat == ShieldRaise && timer >= ShieldRaiseTicks / 2 || beat == ShieldHold);

        /// <summary>How far the sphere has gone off her, so many ticks into its push.</summary>
        private static float ShieldFlown(float t)
        {
            float gathered = (ShieldMost - ShieldStart) / ShieldGather;

            if (t <= gathered)
                return ShieldStart * t + 0.5f * ShieldGather * t * t;

            return ShieldStart * gathered + 0.5f * ShieldGather * gathered * gathered + ShieldMost * (t - gathered);
        }

        /// <summary>How far it goes before its edge meets the border (the shape is convex: its middle meets a smaller one).</summary>
        private float ShieldRoom() => Decagon.Reach(actAt, actTurn.ToRotationVector2(), arenaCentre, EmpressArena.Radius - ShieldRadius / Decagon.Apothem, out _);

        /// <summary>Where the sphere is: about her, then flying off her.</summary>
        private Vector2 ShieldAt(NPC npc)
        {
            if (beat == ShieldPush)
                return actAt + actTurn.ToRotationVector2() * Math.Min(ShieldFlown(timer), ShieldRoom());

            if (beat == ShieldBurst)
                return actAt + actTurn.ToRotationVector2() * ShieldRoom();

            return npc.Center;
        }

        private void RunShield(NPC npc, Player who, bool entered)
        {
            int t = timer;

            switch (beat)
            {
                case ShieldRaise:
                    npc.velocity *= 0.8f;
                    FreeArmAt((who.Center - npc.Center).ToRotation());

                    if (entered)
                    {
                        glass.Clear();
                        struckAt = 0;

                        if (!Main.dedServ)
                        {
                            Sound(EmpressSounds.Barrier, npc.Center, 0f, 0.9f, 2);
                            Sound(SoundID.Item29, npc.Center, 0.3f, 0.5f);
                        }
                    }

                    if (Authority && t >= ShieldRaiseTicks)
                        Next(npc, ShieldHold);

                    break;

                case ShieldHold:
                    // Still: hanging where she is.
                    npc.velocity = new Vector2(0f, (float)Math.Sin(t * 0.05f) * 0.3f);
                    FreeArmAt((who.Center - npc.Center).ToRotation());

                    if (!Main.dedServ && t % 80 == 40)
                        Sound(EmpressSounds.Barrier, npc.Center, 0.1f + 0.3f * ShieldStrain, 0.35f, 2);

                    // What it has taken, told now and then rather than every blow.
                    if (Authority && shieldOwed && t % 10 == 0)
                    {
                        shieldOwed = false;
                        npc.netUpdate = true;
                    }

                    if (Authority && (shieldTaken >= ShieldBreaks || t >= ShieldHoldMost))
                    {
                        // Off her, at the player, from where she is: fixed now, and sent.
                        actAt = npc.Center;
                        actTurn = (who.Center - npc.Center).ToRotation();
                        Next(npc, ShieldPush);
                    }

                    break;

                case ShieldPush:
                    {
                        npc.velocity *= 0.85f;
                        FreeArmAt(actTurn);

                        if (entered && !Main.dedServ)
                        {
                            Sound(EmpressSounds.Crinkle, npc.Center, 0.2f, 0.9f, 2);
                            Sound(EmpressSounds.WhooshUp, npc.Center, -0.4f, 0.9f, 2);
                            Scream(npc, -0.1f, 0.9f);
                        }

                        Vector2 at = ShieldAt(npc);
                        float speed = Math.Min(ShieldMost, ShieldStart + ShieldGather * t);

                        // Whoever it meets, before it: this machine's own player, pushed out of its way.
                        if (!Main.dedServ)
                            ShieldShoves(at, actTurn.ToRotationVector2() * speed);

                        if (!Main.dedServ && t % 6 == 0)
                            Sound(EmpressSounds.Crack, at, 0.3f + 0.05f * (t / 6 % 4), 0.35f, 3);

                        if (Authority && (ShieldFlown(t) >= ShieldRoom() || t >= 260))
                            Next(npc, ShieldBurst);

                        break;
                    }

                case ShieldBurst:
                    npc.velocity *= 0.85f;

                    if (entered)
                        ShieldBursts(npc);

                    if (Authority && t >= ShieldBurstTicks)
                        EndAct(npc);

                    break;
            }
        }

        /// <summary>How near the glass is to going: what it has taken, as a share of what it takes.</summary>
        private float ShieldStrain => MathHelper.Clamp(shieldTaken / (float)ShieldBreaks, 0f, 1f);

        /// <summary>This machine's player, if the sphere meets them: put out of its way, and carried before it.</summary>
        private static void ShieldShoves(Vector2 at, Vector2 going)
        {
            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            const float Body = 22f;
            Vector2 away = me.Center - at;
            float distance = away.Length();

            if (distance >= ShieldRadius + Body)
                return;

            Vector2 n = distance < 0.01f ? going.SafeNormalize(Vector2.UnitY) : away / distance;
            me.Center = at + n * (ShieldRadius + Body);

            float along = Vector2.Dot(me.velocity, n);
            float want = Vector2.Dot(going, n) + 2f;

            if (along < want)
                me.velocity += n * (want - along);

            me.fallStart = (int)(me.position.Y / 16f);
        }

        /// <summary>The burst where it meets the border: the server's blow, glass and light everywhere.</summary>
        private void ShieldBursts(NPC npc)
        {
            Vector2 at = ShieldAt(npc);
            Hitbox(npc, EmpressHitbox.Shape.Circle, at, ShieldBlast, Hurt(npc, ShieldNormal, ShieldExpert), 5);

            if (Main.dedServ)
                return;

            float near = Near(at);

            Sound(EmpressSounds.Shatter, at, 0f, 1f, 2);
            Sound(SoundID.Shatter, at, -0.2f, 0.8f);
            Sound(SoundID.Item14, at, -0.6f, 0.8f);
            Sound(EmpressSounds.Burst, at, 0.2f, 0.6f, 2);

            Cinematic.Flash(Color.White, 0.3f * near);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(12f * near, 26);

            EmpressWarp.Ripple(at, 26f, 90f, 22f, 36);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, Color.White, 600f / 160f, 24));
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, EmpressArt.Sky, 900f / 160f, 34));
            SparkShard.Spray(at, Color.White, 40, 16f, 160f, 30, palette: EmpressArt.Sparks);
            glass.Clear();
        }

        // ---- the glass takes her blows ------------------------------------------------------------------------------

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            // The game's numbers not shown for blows the glass takes: the glass shows them.
            if (Shielding)
                modifiers.HideCombatText();
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if (!Shielding)
                return;

            // Every machine that takes the blow puts back what it took of her.
            npc.life = Math.Min(npc.lifeMax, npc.life + hit.Damage);

            if (Authority)
            {
                shieldTaken += hit.Damage;
                shieldOwed = true;
            }

            GlassStruck(npc, hit.Damage);
        }

        /// <summary>A blow on the glass, seen: a crack where it lands (toward whoever it came from), or the one there grown; a flash, its sound.</summary>
        private void GlassStruck(NPC npc, int damage)
        {
            if (Main.dedServ)
                return;

            Player by = npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers && Main.player[npc.lastInteraction].active ? Main.player[npc.lastInteraction] : Main.LocalPlayer;
            float way = (by.Center - npc.Center).ToRotation() + Main.rand.NextFloat(-0.4f, 0.4f);
            float grow = MathHelper.Clamp(damage / 160f, 0.2f, 1.2f);

            int nearest = -1;
            float closest = 0.3f;

            for (int i = 0; i < glass.Count; i++)
            {
                float off = Math.Abs(MathHelper.WrapAngle(glass[i].Angle - way));

                if (off < closest)
                {
                    closest = off;
                    nearest = i;
                }
            }

            if (nearest >= 0 || glass.Count >= 32)
            {
                if (nearest < 0)
                    nearest = Main.rand.Next(glass.Count);

                GlassCrack crack = glass[nearest];
                crack.Size = Math.Min(2.2f, crack.Size + grow * 0.5f);
                glass[nearest] = crack;
            }
            else
            {
                glass.Add(new GlassCrack { Angle = way, Out = Main.rand.NextFloat(0.55f, 0.95f), Size = 0.3f + grow * 0.6f, Seed = Main.rand.Next(1 << 20) });
            }

            struckAt = Main.GameUpdateCount;
            struckWay = way;

            Vector2 at = npc.Center + way.ToRotationVector2() * ShieldRadius * 0.92f;
            Sound(EmpressSounds.Crack, at, MathHelper.Lerp(0.4f, -0.3f, grow / 1.2f), 0.55f + 0.3f * grow, 4);

            if (Main.rand.NextBool(2))
                Sound(SoundID.Item27, at, Main.rand.NextFloat(0.3f, 0.9f), 0.3f, 3);

            SparkShard.Spray(at, Color.White, 3 + (int)(grow * 5f), 4f, 30f, 12, palette: EmpressArt.Sparks);
            CombatText.NewText(new Rectangle((int)at.X - 8, (int)at.Y - 8, 16, 16), EmpressArt.Sky, damage, false, true);
        }

        // ---- drawing ----------------------------------------------------------------------------------------------

        private void DrawShieldHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float bright = beat == ShieldBurst ? 0f : 0.15f * ShieldStrain;
            DrawSecondHer(npc, spriteBatch, screenPos, bright, 0.35f + 0.25f * ShieldStrain);
        }

        /// <summary>The glass: the world bent through it; its edge, more seen the more it has taken; its cracks; a blow's flash.</summary>
        private void DrawShieldOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 at = ShieldAt(npc);

            if (beat == ShieldBurst)
            {
                DrawGlassBurst(spriteBatch, at, timer / (float)ShieldBurstTicks);
                return;
            }

            float made = beat == ShieldRaise ? Ease(timer / (float)ShieldRaiseTicks) : 1f;
            float strain = ShieldStrain;
            float radius = ShieldRadius * (beat == ShieldRaise ? 0.4f + 0.6f * made : 1f);

            // Unseen but for the world bent through it.
            EmpressWarp.Lens(at, radius * 1.08f, (0.07f + 0.08f * strain) * made, 0.05f * (float)Math.Sin(time * 0.02f));

            // Its edge: barely there, more seen as it takes; a glint going round it; a pale shine on its upper side.
            DrawRing(spriteBatch, at, radius, 2f, EmpressArt.Additive(Color.White, (0.08f + 0.45f * strain) * made));
            DrawRing(spriteBatch, at, radius * 1.01f, 8f, EmpressArt.Additive(EmpressArt.Prism(time / 200f), (0.04f + 0.18f * strain) * made));

            float glint = time * 0.04f;
            EmpressArt.DrawStar(spriteBatch, at + glint.ToRotationVector2() * radius, 34f, glint, EmpressArt.Additive(Color.White, 0.6f * made));

            for (int k = 0; k < 5; k++)
            {
                float a = -2.2f + k * 0.12f;
                Vector2 p1 = at + a.ToRotationVector2() * radius * 0.82f;
                Vector2 p2 = at + (a + 0.12f).ToRotationVector2() * radius * 0.82f;
                EmpressArt.DrawLine(spriteBatch, p1, p2, 4f, EmpressArt.Additive(Color.White, (0.08f + 0.15f * strain) * made));
            }

            // A blow: a flash where it landed, a ring of it going round.
            uint since = Main.GameUpdateCount - struckAt;

            if (struckAt != 0 && since < 14 && beat == ShieldHold)
            {
                float fade = 1f - since / 14f;
                Vector2 hit = at + struckWay.ToRotationVector2() * radius * 0.92f;
                EmpressArt.DrawBloom(spriteBatch, hit, 90f, EmpressArt.Additive(Color.White, 0.7f * fade));
                DrawRing(spriteBatch, at, radius, 6f * fade, EmpressArt.Additive(EmpressArt.Sky, 0.45f * fade));
            }

            // The cracks.
            foreach (GlassCrack crack in glass)
                DrawGlassCrack(spriteBatch, at, radius, crack, time);

            // Going: images of its edge behind it.
            if (beat == ShieldPush)
            {
                Vector2 back = -actTurn.ToRotationVector2();
                float speed = Math.Min(ShieldMost, ShieldStart + ShieldGather * timer);

                for (int k = 1; k <= 4; k++)
                    DrawRing(spriteBatch, at + back * speed * k * 1.6f, radius, 3f, EmpressArt.Additive(EmpressArt.Prism(k / 5f + time / 100f), 0.18f * (1f - k / 5f)));
            }
        }

        /// <summary>One crack, struck glass's: lines out every way from where it struck, jagged, and rings round it, kept inside the sphere.</summary>
        private static void DrawGlassCrack(SpriteBatch spriteBatch, Vector2 centre, float radius, GlassCrack crack, float time)
        {
            UnifiedRandomish dice = new UnifiedRandomish(crack.Seed);
            Vector2 struck = centre + crack.Angle.ToRotationVector2() * radius * crack.Out;
            int rays = 5 + (int)(dice.Next() * 4f);
            float reach = radius * (0.25f + 0.35f * crack.Size);

            for (int r = 0; r < rays; r++)
            {
                float a = (r + dice.Next() * 0.6f) / rays * MathHelper.TwoPi;
                float length = reach * (0.5f + 0.6f * dice.Next());
                Vector2 previous = struck;
                float hue = dice.Next();

                for (int s = 1; s <= 3; s++)
                {
                    a += (dice.Next() - 0.5f) * 0.6f;
                    Vector2 next = previous + a.ToRotationVector2() * length / 3f;

                    // Kept inside the glass.
                    if (Vector2.Distance(next, centre) > radius)
                        next = centre + (next - centre).SafeNormalize(Vector2.UnitX) * radius;

                    float glint = 0.6f + 0.4f * (float)Math.Sin(time * 0.12f + r + s);
                    EmpressArt.DrawLine(spriteBatch, previous, next, 5f, EmpressArt.Additive(EmpressArt.Prism(hue + s * 0.1f + time / 240f), 0.4f * glint));
                    EmpressArt.DrawLine(spriteBatch, previous, next, 1.4f, EmpressArt.Additive(Color.White, 0.85f * glint));
                    previous = next;
                }
            }

            // Rings round where it struck, as a struck mirror breaks.
            int rings = crack.Size > 1f ? 2 : 1;

            for (int k = 1; k <= rings; k++)
            {
                float r = reach * 0.28f * k;
                int steps = 10 + 4 * k;
                Vector2 previous = struck + new Vector2(r, 0f);

                for (int i = 1; i <= steps; i++)
                {
                    float a = i / (float)steps * MathHelper.TwoPi;
                    float wobble = 1f + 0.18f * (dice.Next() - 0.5f);
                    Vector2 next = struck + a.ToRotationVector2() * r * wobble;

                    if (Vector2.Distance(next, centre) <= radius && Vector2.Distance(previous, centre) <= radius)
                        EmpressArt.DrawLine(spriteBatch, previous, next, 1.2f, EmpressArt.Additive(Color.White, 0.6f));

                    previous = next;
                }
            }

            EmpressArt.DrawStar(spriteBatch, struck, 26f + 20f * crack.Size, time * 0.03f + crack.Seed, EmpressArt.Additive(Color.White, 0.7f));
        }

        /// <summary>The glass gone: its pieces flying out, turning, edged in her colours; a flash, rings.</summary>
        private static void DrawGlassBurst(SpriteBatch spriteBatch, Vector2 at, float p)
        {
            float fade = 1f - p;
            UnifiedRandomish dice = new UnifiedRandomish((int)(at.X * 3f + at.Y * 5f));

            EmpressArt.DrawBloom(spriteBatch, at, ShieldBlast * 2f * (0.6f + p), EmpressArt.Additive(Color.White, 0.6f * fade * fade));

            for (int k = 0; k < 3; k++)
            {
                float r = ShieldBlast * (0.3f + 0.35f * k) * (float)Math.Pow(p, 0.5) + 20f;
                DrawRing(spriteBatch, at, r, 12f * fade + 2f, EmpressArt.Additive(EmpressArt.Prism(k / 3f + p), 0.7f * fade));
            }

            for (int i = 0; i < 26; i++)
            {
                float a = dice.Next() * MathHelper.TwoPi;
                float flight = (ShieldRadius + 260f * dice.Next()) * (float)Math.Pow(p, 0.6);
                float size = 12f + 22f * dice.Next();
                float spin = (dice.Next() - 0.5f) * 14f * p + dice.Next() * MathHelper.TwoPi;
                Vector2 middle = at + a.ToRotationVector2() * flight + new Vector2(0f, 70f * p * p);
                Color edge = EmpressArt.Prism(dice.Next());

                for (int c = 0; c < 3; c++)
                {
                    Vector2 from = middle + (spin + c * MathHelper.TwoPi / 3f).ToRotationVector2() * size;
                    Vector2 to = middle + (spin + (c + 1) * MathHelper.TwoPi / 3f + 0.4f).ToRotationVector2() * size * 0.8f;
                    EmpressArt.DrawLine(spriteBatch, from, to, 5f, EmpressArt.Additive(edge, 0.5f * fade));
                    EmpressArt.DrawLine(spriteBatch, from, to, 1.6f, EmpressArt.Additive(Color.White, 0.9f * fade));
                }
            }
        }
    }
}
