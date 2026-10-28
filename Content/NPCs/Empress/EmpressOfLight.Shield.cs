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
    //     RAISE   30   she stops, her free hand out: in front of her, between her and the player, the world cracks as glass
    //                  struck: a small star of a crack, a glint
    //     HOLD  ≤600   she is still; every blow on her strikes the glass instead, none of it reaching her, and cracks it
    //                  where it comes through it (the world's own numbers not shown: the glass shows it). The crack grows
    //                  outward with what it takes (and a little with time), a spine across the way to the player and
    //                  branches off it, forked; its panes are shards, the world in them moved, and again smaller inside each
    //                  (EmpressBarrier, EmpressMirror). The sphere it guards is still there about her, unseen. At two
    //                  thousand five hundred of it, or ten seconds whichever first,
    //     PUSH  ≤260   it comes off her at the player, whole now, at the front of the sphere: faster and faster, pushing
    //                  whoever it meets before it, until it meets the border
    //     BURST   40   and bursts there: its shards flying every way, turning, a blast of light (100 in a normal world, 65
    //                  in an expert one, doubled as the game doubles any hostile shot)
    //
    // By request, later: "its size the circle is still there but invisible but its freeform and it expands outwards ...
    // Make it pretty and make the glass actually have the fracture and repeating effect".
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
        private const int ShieldHoldMost = 600;
        private const int ShieldBurstTicks = 40;

        /// <summary>
        /// The sphere: middle to its edge; what it takes before it goes (by request, "about 2000", then "after 2500
        /// acumulative damage, it pushed towards the player at an increasingly fast speed, and breaks upon reaching the
        /// arena border"); its blast's reach.
        /// </summary>
        private const float ShieldRadius = 160f;
        private const int ShieldBreaks = 2500;
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

        /// <summary>Where a blow came through the glass, each machine's own: how far across it (a share of its whole reach), how much it has taken there, its own seed.</summary>
        private struct GlassCrack
        {
            public float At;
            public float Size;
            public int Seed;
        }

        private readonly List<GlassCrack> glass = new();

        /// <summary>The last blow seen: when, and where across the glass; where the blow on its way came from.</summary>
        private uint struckAt;
        private float struckOn;
        private Vector2 blowFrom;
        private bool blowKnown;

        /// <summary>
        /// The crack as this machine has it: how far it has grown (0 to 1 of its reach), the way it faces (to the player),
        /// how far in front of her middle it stands.
        /// </summary>
        private float barrierGrow;
        private float barrierTurn;
        private float barrierOut;

        /// <summary>The crack's whole reach either way from its middle, at its full growth; how far in front of her it stands as she holds it.</summary>
        private const float BarrierReach = 190f;
        private const float BarrierHeld = 104f;

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
            GrowBarrier(npc, who, entered);

            switch (beat)
            {
                case ShieldRaise:
                    npc.velocity *= 0.8f;
                    FreeArmAt((who.Center - npc.Center).ToRotation());

                    if (entered)
                    {
                        glass.Clear();
                        struckAt = 0;
                        blowKnown = false;

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
            // The game's numbers not shown for blows the glass takes (the glass shows them), nor for blows in the
            // negative (they heal her: her own green numbers instead).
            if (Shielding || Inverted(npc))
                modifiers.HideCombatText();
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if (Inverted(npc) && !Shielding)
            {
                NegativeHeals(npc, hit.Damage);
                return;
            }

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

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (Shielding)
            {
                blowFrom = projectile.Center - projectile.velocity * 2f;
                blowKnown = true;
            }
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (Shielding)
            {
                blowFrom = player.Center;
                blowKnown = true;
            }
        }

        /// <summary>
        /// The crack's growth, its way and its place, this tick, on every machine: a small star of it as she raises it; then
        /// growing outward with what it takes, a little with time too; whole as it goes. Turned to the player as she holds it,
        /// to its way as it goes; in front of her, then at the sphere's front as it goes, where it pushes.
        /// </summary>
        private void GrowBarrier(NPC npc, Player who, bool entered)
        {
            float want = (who.Center - npc.Center).ToRotation();

            if (beat == ShieldRaise && entered)
            {
                barrierGrow = 0f;
                barrierTurn = want;
                barrierOut = BarrierHeld;
            }

            switch (beat)
            {
                case ShieldRaise:
                    barrierGrow = 0.14f * Ease(timer / (float)ShieldRaiseTicks);
                    barrierTurn = LerpAngle(barrierTurn, want, 0.08f);
                    break;

                case ShieldHold:
                    {
                        float target = Math.Max(0.14f + 0.86f * (float)Math.Pow(ShieldStrain, 0.7), 0.14f + 0.45f * timer / ShieldHoldMost);
                        barrierGrow += (target - barrierGrow) * 0.1f;
                        barrierTurn = LerpAngle(barrierTurn, want, 0.05f);
                        barrierOut = BarrierHeld;
                        break;
                    }

                case ShieldPush:
                    barrierGrow += (1f - barrierGrow) * 0.15f;
                    barrierTurn = LerpAngle(barrierTurn, actTurn, 0.25f);
                    barrierOut += (ShieldRadius - 8f - barrierOut) * 0.2f;
                    break;
            }
        }

        /// <summary>A blow on the glass, seen: a crack where it came through (or the one there grown), a ripple in the glass, a flash, its sound.</summary>
        private void GlassStruck(NPC npc, int damage)
        {
            if (Main.dedServ)
                return;

            Player by = npc.lastInteraction >= 0 && npc.lastInteraction < Main.maxPlayers && Main.player[npc.lastInteraction].active ? Main.player[npc.lastInteraction] : Main.LocalPlayer;
            Vector2 from = blowKnown ? blowFrom : by.Center;
            blowKnown = false;

            // Where the line from the blow to her meets the glass's plane, across it.
            Vector2 face = barrierTurn.ToRotationVector2();
            Vector2 across = new Vector2(-face.Y, face.X);
            Vector2 off = from - npc.Center;
            float forward = Vector2.Dot(off, face);
            float side = Vector2.Dot(off, across);
            float y = forward > barrierOut + 1f ? side * barrierOut / forward : side;
            float on = MathHelper.Clamp(y / BarrierReach, -0.95f, 0.95f);
            float grow = MathHelper.Clamp(damage / 160f, 0.2f, 1.2f);

            int nearest = -1;
            float closest = 0.07f;

            for (int i = 0; i < glass.Count; i++)
            {
                float d = Math.Abs(glass[i].At - on);

                if (d < closest)
                {
                    closest = d;
                    nearest = i;
                }
            }

            if (nearest >= 0 || glass.Count >= 24)
            {
                if (nearest < 0)
                    nearest = Main.rand.Next(glass.Count);

                GlassCrack crack = glass[nearest];
                crack.Size = Math.Min(2.2f, crack.Size + grow * 0.4f);
                glass[nearest] = crack;
            }
            else
            {
                glass.Add(new GlassCrack { At = on, Size = 0.4f + grow * 0.5f, Seed = Main.rand.Next(1 << 20) });
            }

            struckAt = Main.GameUpdateCount;
            struckOn = on;

            Vector2 at = BarrierPoint(npc, on);
            Sound(EmpressSounds.Crack, at, MathHelper.Lerp(0.4f, -0.3f, grow / 1.2f), 0.55f + 0.3f * grow, 4);

            if (Main.rand.NextBool(2))
                Sound(SoundID.Item27, at, Main.rand.NextFloat(0.3f, 0.9f), 0.3f, 3);

            EmpressWarp.Ripple(at, 7f, 22f, 5f + 4f * grow, 14);
            SparkShard.Spray(at, Color.White, 3 + (int)(grow * 5f), 4f, 30f, 12, palette: EmpressArt.Sparks);
            CombatText.NewText(new Rectangle((int)at.X - 8, (int)at.Y - 8, 16, 16), EmpressArt.Sky, damage, false, true);
        }

        // ---- drawing ----------------------------------------------------------------------------------------------

        private void DrawShieldHer(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float bright = beat == ShieldBurst ? 0f : 0.15f * ShieldStrain;
            DrawSecondHer(npc, spriteBatch, screenPos, bright, 0.35f + 0.25f * ShieldStrain);
        }

        // ---- the crack, as it is this frame --------------------------------------------------------------------

        /// <summary>One shard of the crack: its corners (in the shared list), its middle, how the world is moved in it, its flight.</summary>
        internal struct Shard
        {
            public int Start;
            public int Count;
            public Vector2 Middle;
            public Vector2 Shift;
            public float Zoom;
            public float Hue;
            public Vector2 Flown;
            public float Spin;
        }

        /// <summary>A line of the crack: its ends, how wide, how bright, its colour's place round her colours.</summary>
        internal struct CrackLine
        {
            public Vector2 A;
            public Vector2 B;
            public float Width;
            public float Light;
            public float Hue;
        }

        internal static readonly List<Vector2> BarrierCorners = new();
        internal static readonly List<Shard> BarrierShards = new();
        internal static readonly List<CrackLine> BarrierLines = new();

        /// <summary>The crack is up (EmpressMirror draws its shards).</summary>
        internal bool Barrier => scene == Scene.Act && act == Act.Shield && !dying;

        /// <summary>How much of it is seen: coming as she raises it, going as it bursts.</summary>
        internal float BarrierShown => beat == ShieldRaise ? Ease(timer / 12f) : beat == ShieldBurst ? 1f - Ease(timer / (float)ShieldBurstTicks) : 1f;

        /// <summary>How far its shards have flown as it bursts (0 to 1).</summary>
        internal float BarrierFlight => beat == ShieldBurst ? (float)Math.Pow(Ease(timer / (float)ShieldBurstTicks), 0.6) : 0f;

        /// <summary>A point of the crack: its share x toward the player, y across, of its reach now.</summary>
        private Vector2 BarrierAt(NPC npc, Vector2 q)
        {
            Vector2 face = barrierTurn.ToRotationVector2();
            Vector2 across = new Vector2(-face.Y, face.X);
            float reach = BarrierReach * Math.Max(0.02f, barrierGrow);
            return ShieldAt(npc) + face * (barrierOut + q.X * reach) + across * (q.Y * reach);
        }

        /// <summary>The point on the spine this share of its whole reach across (where a blow came through).</summary>
        private Vector2 BarrierPoint(NPC npc, float on)
        {
            BarrierNet net = BarrierNet.Of(actSeed);
            float y = MathHelper.Clamp(on / Math.Max(0.05f, barrierGrow), -0.92f, 0.92f);
            float u = (y + 1f) * 0.5f * BarrierNet.Steps;
            int i = Math.Clamp((int)u, 0, BarrierNet.Steps - 1);
            return BarrierAt(npc, Vector2.Lerp(net.Spine[i], net.Spine[i + 1], u - i));
        }

        /// <summary>
        /// The crack worked out for this frame, in the world: its lines (the spine, the branches as far as they have come,
        /// their twigs), its shards (the panes between the spine and each two branches on a side, and beyond them between
        /// the branches' first and second lengths), each with the world moved in it and, as it bursts, its flight.
        /// </summary>
        internal void BuildBarrier(NPC npc)
        {
            BarrierCorners.Clear();
            BarrierShards.Clear();
            BarrierLines.Clear();

            if (!Barrier || barrierGrow <= 0.003f)
                return;

            BarrierNet net = BarrierNet.Of(actSeed);
            float grow = barrierGrow;
            float time = (float)Main.timeForVisualEffects;
            Vector2 face = barrierTurn.ToRotationVector2();
            Vector2 across = new Vector2(-face.Y, face.X);
            Vector2 heart = BarrierAt(npc, Vector2.Zero);
            float flight = BarrierFlight;

            Span<Vector2> spine = stackalloc Vector2[BarrierNet.Steps + 1];

            for (int i = 0; i <= BarrierNet.Steps; i++)
                spine[i] = BarrierAt(npc, net.Spine[i]);

            for (int i = 0; i < BarrierNet.Steps; i++)
                BarrierLines.Add(new CrackLine { A = spine[i], B = spine[i + 1], Width = 1f, Light = 1f, Hue = i * 0.06f });

            // The branches as far as they have come; and their ends, for the shards.
            int count = net.Branches.Length;
            Span<Vector2> first = stackalloc Vector2[count];
            Span<Vector2> second = stackalloc Vector2[count];
            Span<float> grown = stackalloc float[count];

            for (int k = 0; k < count; k++)
            {
                BarrierNet.Branch branch = net.Branches[k];
                float g = BarrierNet.Grown(branch, grow);
                grown[k] = g;
                Vector2 root = net.Spine[branch.Root];
                Vector2 previous = spine[branch.Root];

                for (int s = 0; s < branch.Points.Length; s++)
                {
                    Vector2 next = BarrierAt(npc, root + branch.Points[s] * g);

                    if (s == 0)
                        first[k] = next;
                    else if (s == 1)
                        second[k] = next;

                    if (g > 0.01f)
                        BarrierLines.Add(new CrackLine { A = previous, B = next, Width = 0.75f - 0.12f * s, Light = g, Hue = branch.Own + s * 0.1f });

                    previous = next;
                }

                if (branch.Twigs != null && g > 0.7f)
                {
                    Vector2 end = root + branch.Points[^1];
                    float t = (g - 0.7f) / 0.3f;

                    foreach (Vector2 twig in branch.Twigs)
                        BarrierLines.Add(new CrackLine { A = previous, B = BarrierAt(npc, end + twig * t), Width = 0.4f, Light = t, Hue = branch.Own + 0.4f });
                }
            }

            // The shards, each side of the spine: a pane between each two branches on it that have come (the spine's ends
            // counted as branches too, short ones), bounded by the spine, the two branches out to their second points, and
            // the line between those; beyond, between their second and last points, once both have come far.
            Span<Vector2> third = stackalloc Vector2[count];

            for (int k = 0; k < count; k++)
            {
                BarrierNet.Branch branch = net.Branches[k];
                third[k] = BarrierAt(npc, net.Spine[branch.Root] + branch.Points[^1] * grown[k]);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                int previousK = -1;
                int previousRoot = 0;
                Vector2 previousFirst = BarrierAt(npc, net.Spine[0] + new Vector2(side * 0.16f, -0.05f));
                Vector2 previousSecond = BarrierAt(npc, net.Spine[0] + new Vector2(side * 0.3f, -0.1f));

                for (int k = 0; k <= count; k++)
                {
                    bool end = k == count;

                    if (!end && (net.Branches[k].Side != side || grown[k] < 0.2f))
                        continue;

                    int root = end ? BarrierNet.Steps : net.Branches[k].Root;
                    Vector2 hereFirst = end ? BarrierAt(npc, net.Spine[BarrierNet.Steps] + new Vector2(side * 0.16f, 0.05f)) : first[k];
                    Vector2 hereSecond = end ? BarrierAt(npc, net.Spine[BarrierNet.Steps] + new Vector2(side * 0.3f, 0.1f)) : second[k];

                    if (root > previousRoot || end)
                    {
                        int start = BarrierCorners.Count;

                        for (int i = previousRoot; i <= root; i++)
                            BarrierCorners.Add(spine[i]);

                        BarrierCorners.Add(hereFirst);
                        BarrierCorners.Add(hereSecond);
                        BarrierCorners.Add(previousSecond);
                        BarrierCorners.Add(previousFirst);
                        AddShard(start, heart, face, across, grow, flight, time, side * 31 + k);
                    }

                    if (!end && previousK >= 0 && grown[k] > 0.75f && grown[previousK] > 0.75f)
                    {
                        int start = BarrierCorners.Count;
                        BarrierCorners.Add(second[previousK]);
                        BarrierCorners.Add(third[previousK]);
                        BarrierCorners.Add(third[k]);
                        BarrierCorners.Add(second[k]);
                        AddShard(start, heart, face, across, grow, flight, time, side * 53 + k + 400);
                    }

                    previousRoot = root;
                    previousFirst = hereFirst;
                    previousSecond = hereSecond;
                    previousK = end ? previousK : k;
                }
            }
        }

        /// <summary>
        /// A shard of the corners from <paramref name="start"/> on: its middle, the world in it moved (toward her, a little
        /// across, a little nearer), its colour; as it bursts, where it has flown and how it has turned.
        /// </summary>
        private void AddShard(int start, Vector2 heart, Vector2 face, Vector2 across, float grow, float flight, float time, int own)
        {
            int n = BarrierCorners.Count - start;

            if (n < 3)
            {
                BarrierCorners.RemoveRange(start, n);
                return;
            }

            Vector2 middle = Vector2.Zero;

            for (int i = start; i < BarrierCorners.Count; i++)
                middle += BarrierCorners[i];

            middle /= n;

            UnifiedRandomish dice = new UnifiedRandomish(actSeed * 7 + own * 131);
            float r1 = dice.Next();
            float r2 = dice.Next();
            float r3 = dice.Next();
            float sway = (float)Math.Sin(time * 0.02f + own);

            Vector2 shift = -face * (16f + 30f * r1) * (0.4f + 0.6f * grow) + across * (r2 - 0.5f) * 26f + across * sway * 3f;
            Vector2 outward = (middle - heart).SafeNormalize(face) + face * 0.4f;
            Vector2 flown = outward.SafeNormalize(face) * (180f + 220f * r3) * flight + new Vector2(0f, 110f * flight * flight);

            BarrierShards.Add(new Shard
            {
                Start = start,
                Count = n,
                Middle = middle,
                Shift = shift,
                Zoom = 1.05f + 0.08f * r2,
                Hue = r1 + r3 * 0.5f,
                Flown = flown,
                Spin = (r3 - 0.5f) * 5f * flight,
            });
        }

        /// <summary>A shard's corner where the shard is now (flown and turned as it bursts).</summary>
        internal static Vector2 ShardCorner(in Shard shard, Vector2 corner) => shard.Middle + shard.Flown + (corner - shard.Middle).RotatedBy(shard.Spin);

        /// <summary>
        /// The crack's light, over its shards (or alone, with no shards to draw): each line its colour wide, white down its
        /// middle; the shards' edges and their smaller selves' (the mirror in the mirror); where blows came through, a star
        /// of small cracks and a ring; glints running down the spine; the shards' edges flying as it bursts.
        /// </summary>
        internal void DrawBarrierLines(NPC npc, SpriteBatch spriteBatch, bool shards)
        {
            float shown = BarrierShown;
            float time = (float)Main.timeForVisualEffects;
            float flight = BarrierFlight;

            if (shown <= 0.003f)
                return;

            if (flight <= 0.001f)
            {
                foreach (CrackLine l in BarrierLines)
                {
                    float glint = 0.75f + 0.25f * (float)Math.Sin(time * 0.1f + l.Hue * 9f);
                    Color colour = EmpressArt.Prism(l.Hue + time / 240f);
                    EmpressArt.DrawLine(spriteBatch, l.A, l.B, 7f * l.Width, EmpressArt.Additive(colour, 0.35f * l.Light * shown * glint));
                    EmpressArt.DrawLine(spriteBatch, l.A, l.B, 1.9f * l.Width + 0.3f, EmpressArt.Additive(Color.White, 0.95f * l.Light * shown));
                }
            }

            // The shards' own edges, and their smaller selves': thin, faint, catching the light in turn.
            foreach (Shard shard in BarrierShards)
            {
                for (int depth = 0; depth < (shards ? 3 : 1); depth++)
                {
                    float scale = 1f - 0.27f * depth;
                    float light = (depth == 0 ? (flight > 0f ? 0.9f : 0.25f) : 0.3f) * shown * (0.6f + 0.4f * (float)Math.Sin(time * 0.06f + shard.Hue * 11f + depth));
                    Color colour = depth == 0 ? Color.White : EmpressArt.Prism(shard.Hue + depth * 0.2f + time / 300f);

                    for (int c = 0; c < shard.Count; c++)
                    {
                        Vector2 a = BarrierCorners[shard.Start + c];
                        Vector2 b = BarrierCorners[shard.Start + (c + 1) % shard.Count];
                        a = ShardCorner(shard, shard.Middle + (a - shard.Middle) * scale);
                        b = ShardCorner(shard, shard.Middle + (b - shard.Middle) * scale);
                        EmpressArt.DrawLine(spriteBatch, a, b, depth == 0 ? 1.4f : 1f, EmpressArt.Additive(colour, light));
                    }
                }
            }

            if (flight > 0.001f)
                return;

            // Where blows came through: a star of small cracks, a ring or two, a glint.
            foreach (GlassCrack crack in glass)
            {
                Vector2 at = BarrierPoint(npc, crack.At);
                UnifiedRandomish dice = new UnifiedRandomish(crack.Seed);
                int rays = 5 + (int)(dice.Next() * 3f);
                float reach = 10f + 16f * crack.Size;

                for (int r = 0; r < rays; r++)
                {
                    float a = (r + dice.Next() * 0.6f) / rays * MathHelper.TwoPi;
                    Vector2 mid = at + a.ToRotationVector2() * reach * 0.5f;
                    Vector2 tip = mid + (a + (dice.Next() - 0.5f) * 0.6f).ToRotationVector2() * reach * (0.4f + 0.5f * dice.Next());
                    EmpressArt.DrawLine(spriteBatch, at, mid, 1.3f, EmpressArt.Additive(Color.White, 0.8f * shown));
                    EmpressArt.DrawLine(spriteBatch, mid, tip, 1f, EmpressArt.Additive(Color.White, 0.6f * shown));
                }

                for (int k = 1; k <= (crack.Size > 1f ? 2 : 1); k++)
                    DrawRing(spriteBatch, at, reach * 0.35f * k, 1.1f, EmpressArt.Additive(Color.White, 0.5f * shown));

                EmpressArt.DrawStar(spriteBatch, at, 22f + 18f * crack.Size, time * 0.03f + crack.Seed, EmpressArt.Additive(Color.White, 0.7f * shown));
            }

            // Glints running down the spine, out from its middle.
            BarrierNet net = BarrierNet.Of(actSeed);

            for (int k = 0; k < 4; k++)
            {
                float run = (time * 0.012f + k * 0.25f) % 1f;
                float y = (k % 2 == 0 ? 1f : -1f) * run;
                float u = (y + 1f) * 0.5f * BarrierNet.Steps;
                int i = Math.Clamp((int)u, 0, BarrierNet.Steps - 1);
                Vector2 at = BarrierAt(npc, Vector2.Lerp(net.Spine[i], net.Spine[i + 1], u - i));
                float twinkle = (float)Math.Sin(run * MathHelper.Pi);
                EmpressArt.DrawStar(spriteBatch, at, 34f * twinkle + 6f, time * 0.05f + k, EmpressArt.Additive(Color.White, 0.8f * twinkle * shown));
            }
        }

        /// <summary>
        /// Over the players: the crack (its lines here when the world cannot be drawn into its shards; else EmpressMirror
        /// draws both), the air along it shimmering, a blow's flash; going, images of its spine behind it; bursting, the light.
        /// </summary>
        private void DrawShieldOver(NPC npc, SpriteBatch spriteBatch)
        {
            float time = (float)Main.timeForVisualEffects;
            Vector2 at = ShieldAt(npc);

            BuildBarrier(npc);

            if (!EmpressMirror.WillDraw)
                DrawBarrierLines(npc, spriteBatch, false);

            if (beat == ShieldBurst)
            {
                DrawGlassBurst(spriteBatch, at, timer / (float)ShieldBurstTicks);
                return;
            }

            float shown = BarrierShown;
            BarrierNet net = BarrierNet.Of(actSeed);
            Vector2 top = BarrierAt(npc, net.Spine[0]);
            Vector2 bottom = BarrierAt(npc, net.Spine[BarrierNet.Steps]);
            EmpressWarp.Haze(top, bottom, 46f, (1.2f + 1.8f * ShieldStrain) * shown, 80f, 0.7f);

            // Its heart: a soft light where it began.
            Vector2 heart = BarrierAt(npc, Vector2.Zero);
            EmpressArt.DrawBloom(spriteBatch, heart, 70f + 90f * barrierGrow, EmpressArt.Additive(EmpressArt.Prism(time / 200f), 0.25f * shown));

            if (beat == ShieldRaise)
                EmpressArt.DrawStar(spriteBatch, heart, 120f * Ease(timer / 10f) * (1f - Ease((timer - 10) / 20f)) + 1f, time * 0.04f, EmpressArt.Additive(Color.White, 0.9f));

            // A blow: a flash where it came through.
            uint since = Main.GameUpdateCount - struckAt;

            if (struckAt != 0 && since < 14 && beat == ShieldHold)
            {
                float fade = 1f - since / 14f;
                Vector2 hit = BarrierPoint(npc, struckOn);
                EmpressArt.DrawBloom(spriteBatch, hit, 90f, EmpressArt.Additive(Color.White, 0.7f * fade));
                EmpressArt.DrawStar(spriteBatch, hit, 70f * fade, time * 0.1f, EmpressArt.Additive(EmpressArt.Sky, 0.7f * fade));
            }

            // Going: images of its spine behind it.
            if (beat == ShieldPush)
            {
                Vector2 back = -actTurn.ToRotationVector2();
                float speed = Math.Min(ShieldMost, ShieldStart + ShieldGather * timer);

                for (int k = 1; k <= 4; k++)
                {
                    Vector2 by = back * speed * k * 1.6f;
                    Color colour = EmpressArt.Additive(EmpressArt.Prism(k / 5f + time / 100f), 0.3f * (1f - k / 5f));

                    for (int i = 0; i < BarrierNet.Steps; i++)
                        EmpressArt.DrawLine(spriteBatch, BarrierAt(npc, net.Spine[i]) + by, BarrierAt(npc, net.Spine[i + 1]) + by, 5f, colour);
                }
            }
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
