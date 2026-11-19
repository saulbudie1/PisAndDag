using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Mech
{
    // ---- its coming -------------------------------------------------------------------------------------------------------
    //
    // By request: "Spawn: like normal but only head comes in on top, robotic screech, then a pair of arms flies out of
    // behind the head, then another pair, they are activated and have a red led on each, the eye also sparks red and have
    // a trail of red in the iris. Every arms points up and shoots at the sky".
    //
    //     DROP      90   the camera goes up over the player, who is held; its head alone comes down from over the top of
    //                    the screen to hang there, its eyes dark, no arm to be seen
    //     SCREECH  100   it shakes and screeches (PrimeScreech: a machine's, not a beast's), its jaw open, steam out of it
    //     PAIR A    40   its upper arms (cannon, laser) come out from behind it, up and out to either side, and lock
    //     PAIR B    40   its lower arms (vice, saw) after them, down and out
    //     ON        70   a red lamp comes on on each arm in turn, a beep each; then its eyes spark and light, red, and
    //                    trail red from then on
    //     AIM       36   every arm swings up to point at the sky, two close by its head and two wide of it
    //     FIRE      94   they fire at the sky, three volleys; its name comes up; the camera comes home, the fight begins
    //
    // Skipped (E held, single player): straight to the fight, its head in its place.
    //
    // Its arms are moved by this on every machine (PrimeArm.FollowEnter), from its step and clock.

    public partial class Prime
    {
        internal const int EnDrop = 0;
        internal const int EnScreech = 1;
        internal const int EnPairA = 2;
        internal const int EnPairB = 3;
        internal const int EnOn = 4;
        internal const int EnAim = 5;
        internal const int EnFire = 6;

        internal const int EnDropTicks = 90;
        internal const int EnScreechTicks = 100;
        internal const int EnPairTicks = 40;
        internal const int EnOnTicks = 70;
        internal const int EnAimTicks = 36;

        /// <summary>
        /// They fire for as long as its name takes to come up under it and be read (it comes up on the fourth tick; its
        /// plate has a line, and is a brisk one), so the scene lets go as the name starts down to the bar.
        /// </summary>
        internal const int EnFireTicks = 4 + BossIntro.TitleRise + BossIntro.BriskRead;

        /// <summary>Ticks into SCREECH its screech begins, and how long its jaw stays open for it.</summary>
        internal const int ScreechAt = 8;
        internal const int ScreechLong = 74;

        /// <summary>Ticks an arm takes to come out from behind it; ticks into ON each lamp comes on (by its turn), and its eyes.</summary>
        internal const int PairOut = 30;
        internal const int LampFirst = 6;
        internal const int LampEvery = 9;
        internal const int EyesAt = 46;

        /// <summary>Ticks into FIRE each volley goes.</summary>
        internal static readonly int[] Volleys = { 12, 40, 68 };

        /// <summary>How high over where the player was it hangs, and how far over that it comes down from.</summary>
        private const float HangHigh = 230f;
        private const float DropFrom = 760f;

        /// <summary>Where the player was as it came: everything in its coming is laid out from here.</summary>
        private Vector2 anchor;

        /// <summary>The camera sent home once (on this machine).</summary>
        private bool sentHome;

        private Vector2 Hang => anchor + new Vector2(0f, -HangHigh);

        /// <summary>The shot: over the player, toward where it hangs.</summary>
        private Vector2 Focus => anchor + new Vector2(0f, -HangHigh * 0.7f);

        /// <summary>Ticks since its coming began, at this step and clock (its steps' lengths are set).</summary>
        private static int EnterClock(int step, int t)
        {
            int before = step switch
            {
                EnDrop => 0,
                EnScreech => EnDropTicks,
                EnPairA => EnDropTicks + EnScreechTicks,
                EnPairB => EnDropTicks + EnScreechTicks + EnPairTicks,
                EnOn => EnDropTicks + EnScreechTicks + EnPairTicks * 2,
                EnAim => EnDropTicks + EnScreechTicks + EnPairTicks * 2 + EnOnTicks,
                _ => EnDropTicks + EnScreechTicks + EnPairTicks * 2 + EnOnTicks + EnAimTicks,
            };

            return before + t;
        }

        // ---- where its arms go (PrimeArm.FollowEnter) -------------------------------------------------------------------

        /// <summary>Its upper pair: the cannon and the laser (vanilla keeps them over it); the vice and the saw are its lower.</summary>
        internal static bool Upper(int type) => type == NPCID.PrimeCannon || type == NPCID.PrimeLaser;

        /// <summary>The side an arm is kept on (as vanilla keeps them: the cannon and the vice to its right as it is seen).</summary>
        internal static float SideOf(int type) => type == NPCID.PrimeCannon || type == NPCID.PrimeVice ? 1f : -1f;

        /// <summary>The turn an arm's lamp comes on in: cannon, laser, vice, saw.</summary>
        internal static int LampTurn(int type) => type switch
        {
            NPCID.PrimeCannon => 0,
            NPCID.PrimeLaser => 1,
            NPCID.PrimeVice => 2,
            _ => 3,
        };

        /// <summary>Where an arm's hand is once it is out, from its head's middle: its upper pair up and out, its lower down and out (a great X).</summary>
        internal static Vector2 OutSpot(int type) =>
            Upper(type) ? new Vector2(SideOf(type) * 170f, -165f) : new Vector2(SideOf(type) * 152f, 150f);

        /// <summary>Where an arm's hand is as it points at the sky: its upper pair close by its head and high, its lower wide of it.</summary>
        internal static Vector2 UpSpot(int type) =>
            Upper(type) ? new Vector2(SideOf(type) * 148f, -172f) : new Vector2(SideOf(type) * 252f, -44f);

        /// <summary>This tick's clock for one of its arms: an arm after it in the game's order runs after it has counted on.</summary>
        internal int ArmClock(NPC head, NPC arm) => Math.Max(0, arm.whoAmI > head.whoAmI ? timer - 1 : timer);

        // ---- its own ------------------------------------------------------------------------------------------------------

        /// <summary>Its coming begins: put over the top of where it will hang. The server's.</summary>
        private void BeginEnter(NPC npc)
        {
            npc.TargetClosest(false);
            Player p = Target(npc);

            // Nobody to come for: vanilla's, which leaves.
            if (p == null)
                return;

            anchor = p.Center;
            seed = Main.rand.Next(int.MaxValue);
            npc.Center = Hang + new Vector2(0f, -DropFrom);
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            Go(npc, Scene.Enter, EnDrop);
        }

        private static float OutBack(float u)
        {
            const float c1 = 1.1f;
            const float c3 = c1 + 1f;
            float v = MathHelper.Clamp(u, 0f, 1f) - 1f;
            return 1f + c3 * v * v * v + c1 * v * v;
        }

        private void RunEnter(NPC npc, bool entered)
        {
            int t = timer;
            bool watching = Watching(anchor);

            if (watching)
            {
                float blend = step == EnDrop ? Smooth(t / 50f) : 1f;
                Shot(Focus, blend, step >= EnOn ? 1.07f : 1.03f);
            }

            // E held in the corner (single player): straight to the fight.
            if (watching && step < EnFire)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    SkipEnter(npc);
                    return;
                }
            }

            npc.rotation = npc.rotation.AngleLerp(0f, 0.25f);
            Vector2 hang = Hang + new Vector2(0f, 5f * (float)Math.Sin(EnterClock(step, t) * 0.05f));

            switch (step)
            {
                case EnDrop:
                    {
                        // Down from over the top of the screen, a little past its place and back.
                        float u = Math.Min(1f, (t + 1f) / EnDropTicks);
                        Move(npc, Vector2.Lerp(Hang + new Vector2(0f, -DropFrom), hang, OutBack(u)));

                        if (entered)
                            DropFx(npc);

                        if (Authority && t >= EnDropTicks)
                            Next(npc, EnScreech);

                        break;
                    }

                case EnScreech:
                    {
                        float into = (t - ScreechAt) / (float)ScreechLong;
                        float voice = into < 0f || into > 1f ? 0f : Smooth(into * 12f) * (1f - Smooth((into - 0.8f) / 0.2f));
                        Move(npc, hang);
                        shake = 3.5f * voice;

                        if (At(ScreechAt))
                            Screech(npc, 0f);

                        ScreechTick(npc, voice);

                        if (Authority && t >= EnScreechTicks)
                            Next(npc, EnPairA);

                        break;
                    }

                case EnPairA:
                case EnPairB:
                    {
                        // A pair thrown out from behind it: it is kicked back a little by it.
                        Move(npc, hang + new Vector2(0f, (step == EnPairA ? 7f : -7f) * (float)Math.Exp(-t / 6f)));

                        if (entered)
                            Deploy(npc);

                        if (At(PairOut))
                            Locked(npc, step == EnPairA);

                        if (Authority && t >= EnPairTicks)
                            Next(npc, step + 1);

                        break;
                    }

                case EnOn:
                    {
                        Move(npc, hang);

                        if (At(EyesAt))
                            EyesOn(npc, green: false);

                        if (t >= EyesAt)
                            shake = 2f * (1f - Smooth((t - EyesAt) / 14f));

                        if (Authority && t >= EnOnTicks)
                            Next(npc, EnAim);

                        break;
                    }

                case EnAim:
                    {
                        Move(npc, hang);

                        if (entered)
                            Whir(npc, 1f);

                        if (Authority && t >= EnAimTicks)
                            Next(npc, EnFire);

                        break;
                    }

                case EnFire:
                    {
                        Move(npc, hang);

                        if (At(4))
                            ShowName(npc);

                        foreach (int volley in Volleys)
                        {
                            if (t >= volley)
                                shake = Math.Max(shake, 3f * (float)Math.Exp(-(t - volley) / 5f));
                        }

                        if (t >= EnFireTicks)
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

        /// <summary>Its name (once).</summary>
        private void ShowName(NPC npc)
        {
            if (named)
                return;

            named = true;

            if (!Main.dedServ)
                BossIntro.PlayNameOnly(npc.whoAmI, PrimeSystem.Plate);
        }

        /// <summary>The scene over: vanilla's AI from a fresh start (its arms' too: they are let go the tick after), the fight on.</summary>
        private void Fight(NPC npc)
        {
            npc.velocity = Vector2.Zero;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.dontTakeDamage = false;
            npc.chaseable = true;
            shielded = false;
            Go(npc, Scene.None, 0);
        }

        /// <summary>Skipped: in its place, its name up, the camera home, the fight on.</summary>
        private void SkipEnter(NPC npc)
        {
            npc.Center = Hang;
            npc.rotation = 0f;
            ShowName(npc);
            Fight(npc);

            if (!Main.dedServ)
                PisPlayer.PanHome(Main.LocalPlayer.Center, 30);
        }
    }
}
