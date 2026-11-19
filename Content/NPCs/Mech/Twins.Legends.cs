using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- the Twins in Legends -----------------------------------------------------------------------------------------------
    //
    // By request ("Legends ... retinazer ... spazmatism ... Twin attacks"). A third again the life, a seventh again the
    // damage (as the Destroyer). Each fights on its own: its first attack is vanilla's own, kept going (PinBase: its
    // lasers and flames reworked, TwinsShots), and between stretches of that, one of its two others, turn about:
    //
    //   THE RETINAZER   first form    flaming lasers (vanilla's)   Roll: a fireball, three charges   Column: a column of fire
    //                   second form   a barrage of them (vanilla's)   Streaks: three columns from afar   SpinColumn: ten
    //                                                                                                  seconds of one, turning
    //   THE SPAZMATISM  first form    cursed flames (vanilla's)    Whirl: circles in, pulling    Sweep: across, seven marks
    //                   second form   its flamethrower, half as far again   Spiral: the golden spiral, gas   Inhale: then spears
    //
    // (Twins.Ret, Twins.Spaz.) The one that changed first, waiting on its twin (untouchable, behind its shield), keeps to
    // its first form's attacks; both go to their second together.
    //
    // More rarely (every forty to fifty-five seconds) both drop what they are doing for an attack together, untouchable
    // while it lasts (Twins.Duo): the Pincer and the Binary before both have changed, the Gas and the Clash after. The
    // Retinazer calls it and keeps its clock; the Spazmatism follows that.
    //
    // The server (or a single player) picks every attack and every mark; every machine plays them from that (SendLegends).

    public partial class Twins
    {
        public const float LifeScale = 1.3f;
        public const float DamageScale = 1.15f;

        private enum Act : byte
        {
            None,
            Roll, Column, Streaks, SpinColumn,
            Whirl, Sweep, Spiral, Inhale,
            Pincer, Binary, GasLeak, Clash,
        }

        private Act act, lastAct, lastDuo;

        /// <summary>Ticks of its own first attack left before its next; ticks to the next attack together (the Retinazer's count).</summary>
        private int restFor = 300, duoIn = 1800;

        /// <summary>Two places an attack is laid out from, a direction, a length, a count, a side: what each means is the attack's own.</summary>
        private Vector2 markA, markB;
        private float angle, span;
        private int count, side = 1;

        /// <summary>In an attack together: the step of the pair's clock this one has seen, its own ticks in it, and ticks without its twin.</summary>
        private int duoSeen = -1, duoT, lonely;

        /// <summary>How far it is turned to fire, 0 to 1 (the Retinazer's roll): only seen, this machine's.</summary>
        private float heat;

        /// <summary>How brightly its outline glows in its colour (their clash): only seen.</summary>
        private float glow;

        /// <summary>In its second form for good (both have changed).</summary>
        internal bool Second => released;

        /// <summary>Firing a column of fire, or about to: what a column lives by (TwinsFireColumn).</summary>
        internal bool ColumnOn => (scene == Scene.Move || scene == Scene.Duo) && (act == Act.Column || act == Act.Streaks || act == Act.SpinColumn || act == Act.Pincer);

        /// <summary>Rolled into a fireball: what it touches burns.</summary>
        internal bool Rolling => heat > 0.5f && (act == Act.Roll || act == Act.GasLeak);

        public override void SetDefaults(NPC entity)
        {
            if (!PADWorld.Legends)
                return;

            entity.lifeMax = (int)(entity.lifeMax * LifeScale);
            entity.damage = (int)(entity.damage * DamageScale);
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (PADWorld.Legends && !NPC.IsMechQueenUp && Rolling)
                target.AddBuff(ModContent.BuffType<IndustrialFlames>(), IndustrialFlames.Ticks);
        }

        // ---- what comes next ----------------------------------------------------------------------------------------------

        /// <summary>Free to be called into an attack together: fighting, not changing, not fallen.</summary>
        private bool FreeFor(NPC npc) => (scene == Scene.None || scene == Scene.Move) && heldFor <= 0 && npc.ai[0] != 1f && npc.ai[0] != 2f;

        /// <summary>
        /// The Retinazer's count to their next attack together, kept through its own attacks: when it is up and both are
        /// free, both drop what they are doing (by request: "both pauses whatever they are doing to do this").
        /// </summary>
        private void LegendsDuoClock(NPC npc, NPC other, Twins twin)
        {
            if (!Authority || !IsRet(npc) || other == null || twin == null || !FreeFor(npc) || !twin.FreeFor(other) || Target(npc) == null || Main.IsItDay())
                return;

            if (--duoIn > 0)
                return;

            bool both = Second && twin.Second;
            Act what = both
                ? (lastDuo == Act.GasLeak ? Act.Clash : lastDuo == Act.Clash ? Act.GasLeak : Main.rand.NextBool() ? Act.GasLeak : Act.Clash)
                : (lastDuo == Act.Pincer ? Act.Binary : lastDuo == Act.Binary ? Act.Pincer : Main.rand.NextBool() ? Act.Pincer : Act.Binary);

            lastDuo = what;
            duoIn = Main.rand.Next(2400, 3301);
            seed = Main.rand.Next(int.MaxValue);
            side = Main.rand.NextBool() ? 1 : -1;
            count = 0;

            foreach ((NPC n, Twins w) in new[] { (npc, this), (other, twin) })
            {
                // What it was doing is broken off.
                TwinsShots.EndColumns(n);
                w.act = what;
                w.duoSeen = -1;
                w.duoT = 0;
                w.lonely = 0;
                w.heat = 0f;
                w.Go(n, Scene.Duo, 0);
            }
        }

        /// <summary>Between its attacks: vanilla's AI kept on its first attack; and, when that has run its stretch, its next.</summary>
        private void LegendsBetween(NPC npc)
        {
            PinBase(npc);
            heat = Math.Max(0f, heat - 0.05f);
            glow = Math.Max(0f, glow - 0.05f);

            if (!Authority || npc.ai[0] == 1f || npc.ai[0] == 2f || Target(npc) == null || Main.IsItDay())
                return;

            if (--restFor > 0)
                return;

            Act a, b;

            if (IsRet(npc))
            {
                a = Second ? Act.Streaks : Act.Roll;
                b = Second ? Act.SpinColumn : Act.Column;
            }
            else
            {
                a = Second ? Act.Spiral : Act.Whirl;
                b = Second ? Act.Inhale : Act.Sweep;
            }

            act = lastAct == a ? b : lastAct == b ? a : Main.rand.NextBool() ? a : b;
            lastAct = act;
            seed = Main.rand.Next(int.MaxValue);
            side = Main.rand.NextBool() ? 1 : -1;
            count = 0;
            Go(npc, Scene.Move, 0);
        }

        /// <summary>
        /// Vanilla's AI held on the one attack of each form that is kept (its counts to its own charges never run out): the
        /// Retinazer's lasers from over the player, its barrage from beside them in its second form; the Spazmatism's cursed
        /// flames, its flamethrower in its second.
        /// </summary>
        private static void PinBase(NPC npc)
        {
            if (npc.ai[0] != 0f && npc.ai[0] != 3f)
                return;

            float want = IsRet(npc) && npc.ai[0] == 3f ? 1f : 0f;

            if (npc.ai[1] != want)
            {
                npc.ai[1] = want;
                npc.ai[2] = 0f;
                npc.ai[3] = 0f;
            }

            if (npc.ai[2] > 120f)
                npc.ai[2] = 0f;
        }

        /// <summary>An attack over: back to vanilla's, for a stretch.</summary>
        private void EndMove(NPC npc)
        {
            act = Act.None;
            restFor = Main.rand.Next(240, 361);
            BackToVanilla(npc);
            Go(npc, Scene.None, 0);
        }

        private static void BackToVanilla(NPC npc)
        {
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;

            // Its turn as vanilla keeps it: once round, from nothing.
            npc.rotation = MathHelper.WrapAngle(npc.rotation);

            if (npc.rotation < 0f)
                npc.rotation += MathHelper.TwoPi;

            float speed = npc.velocity.Length();

            if (speed > 12f)
                npc.velocity *= 12f / speed;
        }

        private void RunMove(NPC npc, bool entered, NPC other)
        {
            Player p = Target(npc);

            if (p == null)
            {
                npc.velocity *= 0.9f;

                if (Authority)
                    EndMove(npc);

                return;
            }

            // Where it is, told again now and then: its way depends on a player every machine sees a little differently.
            if (Authority && timer % 20 == 10)
                npc.netUpdate = true;

            switch (act)
            {
                case Act.Roll: RunRoll(npc, entered, p); break;
                case Act.Column: RunColumn(npc, entered, p); break;
                case Act.Streaks: RunStreaks(npc, entered, p); break;
                case Act.SpinColumn: RunSpinColumn(npc, entered, p); break;
                case Act.Whirl: RunWhirl(npc, entered, p); break;
                case Act.Sweep: RunSweep(npc, entered, p); break;
                case Act.Spiral: RunSpiral(npc, entered, p, other); break;
                case Act.Inhale: RunInhale(npc, entered, p); break;

                default:
                    if (Authority)
                        EndMove(npc);

                    break;
            }
        }

        /// <summary>
        /// An attack together. The Retinazer's step and marks are the pair's; each counts its own ticks in the step it has
        /// seen (so neither misses a beat by which of them the game runs first).
        /// </summary>
        private void RunDuo(NPC npc, NPC other, Twins twin)
        {
            bool lead = IsRet(npc);

            if (other == null || twin == null || twin.scene != Scene.Duo || twin.act != act)
            {
                // Its twin not with it (gone, or word of it not here yet): it waits; the server ends it if it lasts.
                npc.velocity *= 0.9f;

                if (Authority && ++lonely > 40)
                {
                    act = Act.None;
                    restFor = 120;
                    BackToVanilla(npc);
                    Go(npc, Scene.None, 0);
                }

                return;
            }

            lonely = 0;

            // What a broken-off attack of its own left on it (a machine that only watches clears nothing else).
            if (act != Act.GasLeak)
                heat = Math.Max(0f, heat - 0.05f);

            if (act != Act.Clash)
                glow = Math.Max(0f, glow - 0.05f);

            Twins clock = lead ? this : twin;
            bool fresh = clock.step != duoSeen;

            if (fresh)
            {
                duoSeen = clock.step;
                duoT = 0;
            }
            else
            {
                duoT++;
            }

            Player p = Target(lead ? npc : other);

            if (p == null)
            {
                npc.velocity *= 0.9f;

                if (Authority && lead)
                    EndDuo(npc, other, twin);

                return;
            }

            if (Authority && duoT % 20 == 10)
                npc.netUpdate = true;

            switch (act)
            {
                case Act.Pincer: RunPincer(npc, lead, fresh, clock, p, other, twin); break;
                case Act.Binary: RunBinary(npc, lead, fresh, clock, p, other, twin); break;
                case Act.GasLeak: RunGasLeak(npc, lead, fresh, clock, p, other, twin); break;
                case Act.Clash: RunClash(npc, lead, fresh, clock, p, other, twin); break;

                default:
                    if (Authority && lead)
                        EndDuo(npc, other, twin);

                    break;
            }
        }

        /// <summary>Their attack together over (the Retinazer's to say): both back to their own.</summary>
        private void EndDuo(NPC npc, NPC other, Twins twin)
        {
            foreach ((NPC n, Twins w) in new[] { (npc, this), (other, twin) })
            {
                if (n == null || w == null || w.scene != Scene.Duo)
                    continue;

                w.act = Act.None;
                w.restFor = Main.rand.Next(150, 261);
                BackToVanilla(n);
                w.Go(n, Scene.None, 0);
            }
        }

        // ---- how they move, and what they do to the player -------------------------------------------------------------------

        /// <summary>Toward a place, easing in: its velocity brought toward what would take it there, by this share a tick.</summary>
        private static void Hover(NPC npc, Vector2 to, float speed, float share)
        {
            Vector2 want = to - npc.Center;
            float far = want.Length();
            want = far > 1f ? want / far * Math.Min(speed, far * 0.14f + 0.4f) : Vector2.Zero;
            npc.velocity = Vector2.Lerp(npc.velocity, want, share);
        }

        /// <summary>This machine's player drawn toward a place, if within reach of it: the pull a tick, in pixels a tick.</summary>
        private static void Pull(Vector2 toward, float strength, float reach)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;

            if (!me.active || me.dead)
                return;

            Vector2 way = toward - me.Center;
            float far = way.Length();

            if (far > 12f && far < reach)
                me.velocity += way / far * strength;
        }

        /// <summary>Numbers for this attack that every machine draws alike (from what the server drew for it).</summary>
        private Random Dice(int salt) => new(unchecked(seed * 31 + salt * 7919));

        // ---- drawn ------------------------------------------------------------------------------------------------------------

        /// <summary>The bubble's size about its eye, and how often its pulse goes out (ticks).</summary>
        private const float ShieldWide = 200f;
        private const int PulseEvery = 50;

        /// <summary>
        /// Its round shield in its colour (by request: "whichever is transitioned first has a round shield (like supreme
        /// calamitas) but of their respective color", "pulse shield"): a bubble about its eye, breathing, and a pulse of
        /// it going out and fading every fifty ticks. On the half-size canvas, as light.
        /// </summary>
        private static void DrawShield(NPC npc, float strength)
        {
            Texture2D bubble = TwinsArt.Shield;

            if (bubble == null || strength <= 0.01f)
                return;

            Vector2 at = EyeCentre(npc);
            Color c = TwinsArt.Bright(npc);
            float time = Main.GlobalTimeWrappedHourly;
            float breathe = 0.74f + 0.2f * MathF.Sin(time * 3.6f + npc.whoAmI);
            float beat = (Main.GameUpdateCount + (uint)npc.whoAmI * 17u) % PulseEvery / (float)PulseEvery;
            float wide = ShieldWide * npc.scale / bubble.Width;
            float body = strength * breathe;
            float ring = strength * 0.8f * (1f - beat) * (1f - beat);
            Color pale = Color.Lerp(c, Color.White, 0.35f);

            PADPixelCanvas.Queue(batch =>
            {
                Vector2 on = at - Main.screenPosition;
                Vector2 middle = bubble.Size() * 0.5f;
                batch.Draw(bubble, on, null, new Color((int)(c.R * body), (int)(c.G * body), (int)(c.B * body), 255), 0f, middle, wide * (1f + 0.025f * MathF.Sin(time * 3.6f)), SpriteEffects.None, 0f);
                batch.Draw(bubble, on, null, new Color((int)(pale.R * ring), (int)(pale.G * ring), (int)(pale.B * ring), 255), 0f, middle, wide * (1f + 0.45f * beat), SpriteEffects.None, 0f);
            }, additive: true);

            Lighting.AddLight(at, c.R / 255f * 0.5f * strength, c.G / 255f * 0.5f * strength, c.B / 255f * 0.5f * strength);
        }

        /// <summary>Its picture behind itself, a little out every way, in its colour: an outline of light (their clash).</summary>
        private static void DrawOutline(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, float strength)
        {
            if (strength <= 0.01f)
                return;

            Texture2D texture = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
            Color c = DestroyerArt.Lit(TwinsArt.Bright(npc) * (0.55f * strength));
            Vector2 at = EyeCentre(npc) - screenPos;
            float out_ = 4f + 4f * strength;

            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int k = 0; k < 8; k++)
                spriteBatch.Draw(texture, at + (k * MathHelper.PiOver4).ToRotationVector2() * out_, npc.frame, c, npc.rotation, Pivot, npc.scale, flip, 0f);
        }

        /// <summary>Before its own picture: its outline of light; and, rolled into a fireball, the fire behind it.</summary>
        private void DrawUnder(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            DrawOutline(npc, spriteBatch, screenPos, glow);

            if (heat <= 0.02f)
                return;

            // The fire behind it: a comet of light back along its way (the flames themselves are PADFire's, over it).
            Vector2 eye = EyeCentre(npc);
            Vector2 back = npc.velocity.LengthSquared() > 4f ? -npc.velocity.SafeNormalize(Vector2.Zero) : Vector2.Zero;
            float reach = Math.Min(170f, npc.velocity.Length() * 7f);

            for (int k = 5; k >= 0; k--)
            {
                float f = k / 5f;
                Color c = Color.Lerp(new Color(255, 214, 120), new Color(206, 52, 14), f) * (heat * (0.5f - 0.3f * f));
                DestroyerArt.DrawGlow(spriteBatch, eye + back * reach * f, (190f - 70f * f) * npc.scale, DestroyerArt.Lit(c));
            }
        }

        /// <summary>
        /// Over its own picture: rolled into a fireball, its picture again turned to fire and lit (by request: "modify
        /// texture to make it look bright"); its shield; and what its attacks warn with.
        /// </summary>
        private void DrawLegends(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (heat > 0.02f)
            {
                Texture2D own = Terraria.GameContent.TextureAssets.Npc[npc.type].Value;
                Texture2D fire = TwinsArt.FireOf(own);
                Vector2 at = EyeCentre(npc) - screenPos;

                if (fire != null)
                    spriteBatch.Draw(fire, at, npc.frame, Color.White * heat, npc.rotation, Pivot, npc.scale, npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

                float flick = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 31f);
                Tint(npc, spriteBatch, screenPos, new Color(255, 170, 70) * (0.5f * heat * flick));
                DestroyerArt.DrawGlow(spriteBatch, EyeCentre(npc), 150f * npc.scale, DestroyerArt.Lit(new Color(255, 200, 110) * (0.45f * heat * flick)), hotCore: true);
            }

            // Only on the one that changed first, while it waits on its twin: not in their attacks together (by request).
            if (waiting)
                DrawShield(npc, 1f);

            if (scene == Scene.Move)
            {
                if (IsRet(npc))
                    DrawRetMarks(npc, spriteBatch);
                else
                    DrawSpazMarks(npc, spriteBatch);
            }
            else if (scene == Scene.Duo && Partner(npc, out NPC other, out Twins twin))
            {
                DrawDuoMarks(npc, spriteBatch, IsRet(npc) ? this : twin);
            }
        }

        /// <summary>
        /// A warning down a line, in a colour: flashing (on and off, quicker as <paramref name="ready"/> goes to one; solid
        /// once <paramref name="locked"/>), as wide as what it warns of if a width is given (a faint band under it).
        /// </summary>
        private static void DrawWarn(SpriteBatch batch, Vector2 from, float along, float length, float ready, bool locked, Color tint, float band = 0f)
        {
            float gap = MathHelper.Lerp(9f, 4f, ready);
            bool on = locked || Main.GameUpdateCount % (uint)(gap * 2f) < gap;
            float alpha = on ? 1f : 0.25f;

            if (band > 0f)
            {
                Vector2 dir = along.ToRotationVector2();
                DestroyerArt.DrawLine(batch, from, from + dir * length * 0.9f, band, DestroyerArt.Lit(tint * (0.05f + 0.09f * ready) * alpha));
            }

            DrawMark(batch, from, along, length, locked ? 1f : ready * 0.7f, alpha, tint);
        }

        // ---- told across the network ----------------------------------------------------------------------------------------

        private void SendLegends(BinaryWriter writer)
        {
            writer.Write((byte)act);
            writer.Write((byte)Math.Clamp(count, 0, 255));
            writer.Write((sbyte)side);
            writer.Write(angle);
            writer.Write(span);
            writer.Write(markA.X);
            writer.Write(markA.Y);
            writer.Write(markB.X);
            writer.Write(markB.Y);
        }

        private void ReceiveLegends(BinaryReader reader)
        {
            act = (Act)reader.ReadByte();
            count = reader.ReadByte();
            side = reader.ReadSByte();
            angle = reader.ReadSingle();
            span = reader.ReadSingle();
            markA = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            markB = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
    }
}
