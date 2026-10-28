using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using PisAndDag.Content.UI;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace PisAndDag.Content.NPCs.Empress
{
    // ---- her coming ------------------------------------------------------------------------------
    //
    // By request ("Spawn animation once hit the butterfly: Cinematic"), every world, every time:
    //
    //     HUSH      36   everything stops: the player held, the bars in, the music and the world's
    //                    sounds gone
    //     FLICKER  150   the light flickers: the sky pitch and back, the moon dark and thrown bright,
    //                    twice all of it gone to black; and at the end it goes out and stays out
    //     DARK      60   it comes back, but darker: her sky
    //     SPAWN    120   she comes as she always has, over the player, her colours raining down onto
    //                    her as she fades up; in Legends she speaks (IntroWords, by request "as soon as
    //                    she appears, before she drags you up in her cage"), and it lasts until she has
    //                    said all of it
    //     FLASH     30   she flashes down onto them, her middle on theirs, images of her left all the
    //                    way down
    //     CAGE      44   a hexagon of light shuts round them both
    //     DRAG     ...   and drags them to halfway between the ground and the top of the sky
    //     STARS     60   ten stars spin out of her and round her, a second of it (by request, "in a
    //                    spin for 1 second"), slowing as they spread into a ring
    //     FORM     195   they shoot out to the ten corners of a decagon (by request: ten, a corner
    //                    each), the camera with the one going to the right corner; there it sees the
    //                    border drawn between them out of the stars, and what lies outside come; and
    //                    goes back
    //     BREAK     20   the cage breaks
    //     FALL      28   the player falls
    //     CAUGHT    24   and her favour catches them: they hang in the air
    //     RISE      50   she rises to five hundred pixels over them
    //     SCREAM    90   she screams, and her name comes up with it: Elin, Empress Of Light; and as it
    //                    goes down to its place the scene lets go (by request, "cinematic should end as
    //                    boss name plate roll in"): the fight, the music back, the camera walking home
    //
    // The server decides where (BeginArrival, and at the flash where the cage goes and where the arena
    // stands) and when each step starts; every machine plays it.

    public partial class EmpressOfLight
    {
        private const int ArriveHush = 0;
        private const int ArriveFlicker = 1;
        private const int ArriveDark = 2;
        private const int ArriveSpawn = 3;
        private const int ArriveFlash = 4;
        private const int ArriveCage = 5;
        private const int ArriveDrag = 6;
        private const int ArriveStars = 7;
        private const int ArriveForm = 8;
        private const int ArriveBreak = 9;
        private const int ArriveFall = 10;
        private const int ArriveCaught = 11;
        private const int ArriveRise = 12;
        private const int ArriveScream = 13;

        private const int HushTicks = 36;
        private const int FlickerTicks = 150;
        private const int DarkTicks = 60;
        private const int SpawnTicks = 120;

        /// <summary>
        /// In Legends her speech (IntroWords) starts this many ticks into SPAWN, as she fades up; and SPAWN lasts until its
        /// last line has been read (the flash down onto them as it goes), however long that is.
        /// </summary>
        private const int IntroFrom = 40;
        private static int introSpawnTicks;

        private static int SpawnFor => !PADWorld.Legends ? SpawnTicks
            : introSpawnTicks > 0 ? introSpawnTicks
            : introSpawnTicks = Math.Max(SpawnTicks, IntroFrom + Taunt.TicksFor(IntroWords) - Taunt.FadeOut - 8);
        private const int FlashTicks = 30;
        private const int CageTicks = 44;
        private const int StarsTicks = 60;
        private const int FormTicks = 195;
        private const int BreakTicks = 20;
        private const int FallTicks = 28;
        private const int CaughtTicks = 24;
        private const int RiseTicks = 50;

        /// <summary>
        /// Her scream, and the scene's last beat: as long as her name takes to come up under her and be
        /// read (her plate is a brisk one), so the scene lets go as the name starts down to the bar.
        /// </summary>
        private const int ScreamTicks = BossIntro.TitleRise + BossIntro.BriskRead;

        /// <summary>The drag, at the least and the most, and how far it goes a tick at most before it takes longer.</summary>
        private const int DragLeast = 50;
        private const int DragMost = 150;
        private const float DragPace = 18f;

        /// <summary>Where she comes, over the summoner; and how far she drifts down as she comes.</summary>
        private const float SpawnAbove = 380f;
        private const float SpawnDrift = 60f;

        /// <summary>By request: "She still 500 px above player and hovering."</summary>
        private const float HoverAbove = 500f;

        /// <summary>The cage, from its middle to a corner; and how long it takes to shut.</summary>
        internal const float CageRadius = 96f;
        private const int CageShuts = 20;

        /// <summary>A player this near her is in the scene: camera, bars, held still.</summary>
        private const float SceneRange = 3600f;

        /// <summary>The camera catches up with the star it goes out with over the first of these, and comes back from the corner over the last.</summary>
        private const int RideOut = 20;
        private const int RideBack = 55;

        /// <summary>
        /// The ten stars' spin out of her: by request, "in a spin for 1 second": a turn and a quarter of
        /// it, out to a ring this far round her (all of it in the shot).
        /// </summary>
        private const float StarSpin = 1.25f * MathHelper.TwoPi;
        private const float SpinReach = 320f;

        /// <summary>
        /// FORM, in ticks: the stars' flight out from the ring to the corners; the line drawn out of them
        /// from their landing; what lies outside coming; the border thrown white as the line is whole.
        /// </summary>
        private const int OutTicks = 45;
        private const int LineFrom = OutTicks;
        private const int LineGrows = 60;
        private const int VeilFrom = 90;
        private const int VeilTicks = 75;
        private const int FlareFrom = LineFrom + LineGrows - 2;
        private const int FlareTicks = 30;

        /// <summary>
        /// Her name's plate: the one with a line, a small lean in, and brisk (by request: "waiting too long
        /// between the scream and the nameplate"): up with her scream, read through the last of it, and
        /// down to the bar as the scene lets go, the lean let go over the flight.
        /// </summary>
        private static readonly BossIntroProfile Plate = new()
        {
            ZoomIn = 1.12f,
            Brisk = true,
        };

        /// <summary>The camera's walk home at the end.</summary>
        private const int HomeTicks = 60;

        /// <summary>Where she comes, where the cage starts its drag (the summoner's middle, at the flash), and how long the drag is.</summary>
        private Vector2 spawnAt;
        private Vector2 cageFrom;
        private int dragTicks = DragLeast;

        /// <summary>This machine's player was in the shot on the last tick, and is in her cage.</summary>
        private bool watching;
        private bool caged;
        private bool cageDecided;
        private bool plated;

        // ---- the flicker ---------------------------------------------------------------------------------
        //
        // Each holds until the next one's tick: the sky's light, the moon, how much brighter than itself the
        // moon is thrown, and how much of the whole picture goes to black. By request: "The light flickers,
        // moon becomes dark and bright, sky become pitch and normal".

        private static readonly float[,] Flicker =
        {
            //  from  light  moon   flare  black
            {   0f,   1f,    1f,    0f,    0f    },
            {  24f,   0.03f, 0.04f, 0f,    0f    },
            {  28f,   1f,    1f,    0.7f,  0f    },
            {  46f,   0.03f, 0.04f, 0f,    0f    },
            {  52f,   1f,    1f,    0f,    0f    },
            {  58f,   0.03f, 0.04f, 0f,    0f    },
            {  61f,   1f,    1f,    1f,    0f    },
            {  64f,   0.02f, 0.02f, 0f,    0.6f  },
            {  74f,   1f,    1f,    0.85f, 0f    },
            {  77f,   0.03f, 0.04f, 0f,    0f    },
            {  80f,   1f,    1f,    0f,    0f    },
            {  82f,   0.02f, 0.02f, 0f,    0.85f },
            {  90f,   1f,    1f,    1f,    0f    },
            {  93f,   0.03f, 0.04f, 0f,    0f    },
            {  96f,   1f,    1f,    0.45f, 0f    },
            {  98f,   0.03f, 0.04f, 0f,    0f    },
            {  99f,   1f,    1f,    0f,    0f    },
            { 101f,   0.02f, 0.02f, 0f,    0f    },
        };

        /// <summary>The flicker's step at this tick, and the tick that step began.</summary>
        private static int FlickerStep(int t, out int from)
        {
            int step = 0;

            for (int i = 0; i < Flicker.GetLength(0); i++)
            {
                if (t >= Flicker[i, 0])
                    step = i;
            }

            from = (int)Flicker[step, 0];
            return step;
        }

        // ---- begun --------------------------------------------------------------------------------------

        /// <summary>The server's: whom she came for, and her hidden where she will come, until she does.</summary>
        private void BeginArrival(NPC npc, Player who)
        {
            summoner = who.whoAmI;
            spawnAt = who.Center + new Vector2(0f, -SpawnAbove);

            npc.Center = spawnAt;
            npc.velocity = Vector2.Zero;
            npc.Opacity = 0f;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.dontTakeDamage = true;
            npc.chaseable = false;
            npc.damage = 0;

            Go(npc, Scene.Arrival, ArriveHush);
        }

        /// <summary>The player she came for, or the nearest if they are gone.</summary>
        private Player Summoner(NPC npc)
        {
            if (summoner >= 0 && summoner < Main.maxPlayers && Main.player[summoner].active && !Main.player[summoner].dead)
                return Main.player[summoner];

            if (Authority)
            {
                npc.TargetClosest(false);
                summoner = npc.target;
                npc.netUpdate = true;
            }

            return Main.player[Math.Clamp(npc.target, 0, Main.maxPlayers - 1)];
        }

        // ---- run ------------------------------------------------------------------------------------

        private void RunArrival(NPC npc, bool entered)
        {
            // Asked afresh every tick (ArrivalShot), so it says whether the player was in the shot on the
            // last tick of it.
            watching = false;

            npc.dontTakeDamage = true;
            npc.damage = 0;
            npc.chaseable = false;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.velocity = Vector2.Zero;

            Player who = Summoner(npc);
            int t = timer;

            // A player in her scene: the world's sounds, its light and its sky are hers for them (and only
            // for them: anyone further off plays on as they were).
            bool here = InScene(npc);

            // E held in the corner: straight to the fight (single player; CinematicSkip).
            if (here)
            {
                CinematicSkip.Offer();

                if (Authority && CinematicSkip.Take())
                {
                    SkipArrival(npc, who);
                    return;
                }
            }

            // A player whose cage it is: decided once, the first tick this machine sees the cage (a machine
            // that falls behind and first sees it being dragged is put in it then).
            if (!cageDecided && beat >= ArriveFlash && beat <= ArriveForm)
            {
                Cage(who);
                cageDecided = true;
            }

            // Nothing to hear but her, from the first tick until she has screamed.
            if (here && beat <= ArriveScream)
                EmpressSky.Hush();

            // Her sky, from when it comes.
            if ((here || NearArena()) && beat >= ArriveDark)
                EmpressSky.Darken(1f);

            switch (beat)
            {
                case ArriveHush:
                    npc.Center = spawnAt;
                    npc.Opacity = 0f;
                    ArrivalShot(npc, who.Center, 0f, 1.06f);

                    if (entered && !Main.dedServ)
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.95f, Volume = 0.35f }, who.Center);

                    if (Authority && t >= HushTicks)
                        Next(npc, ArriveFlicker);

                    break;

                case ArriveFlicker:
                    npc.Center = spawnAt;
                    npc.Opacity = 0f;
                    ArrivalShot(npc, who.Center, 0f, 1.06f);

                    if (here)
                        Flickering(who, t);

                    if (Authority && t >= FlickerTicks)
                        Next(npc, ArriveDark);

                    break;

                case ArriveDark:
                    {
                        npc.Center = spawnAt;
                        npc.Opacity = 0f;
                        ArrivalShot(npc, who.Center, 0f, 1.06f);

                        // Back, but darker: the light comes up under her sky.
                        float up = Ease(t / (float)DarkTicks);

                        if (here)
                        {
                            EmpressSky.Light(MathHelper.Lerp(0.02f, 1f, up));
                            EmpressSky.Moon(MathHelper.Lerp(0.02f, 1f, up));
                        }

                        if (entered && here)
                        {
                            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.7f, Volume = 0.6f }, who.Center);
                            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = 0.2f, Volume = 0.45f }, who.Center);
                        }

                        if (Authority && t >= DarkTicks)
                        {
                            // She comes where the summoner is now.
                            spawnAt = who.Center + new Vector2(0f, -SpawnAbove);
                            Next(npc, ArriveSpawn);
                        }

                        break;
                    }

                case ArriveSpawn:
                    {
                        float p = MathHelper.Clamp(t / (float)SpawnTicks, 0f, 1f);

                        // Come to rest, she hangs there breathing while she speaks.
                        float bob = Bob() * Ease((t - SpawnTicks) / 60f);
                        npc.Center = spawnAt + new Vector2(0f, SpawnDrift * (1f - (1f - p) * (1f - p)) + bob);
                        npc.Opacity = Ease(p);

                        // Between the two of them, out to take both in.
                        Vector2 between = Vector2.Lerp(who.Center, npc.Center, 0.5f);
                        ArrivalShot(npc, between, Ease(t / 40f), 1f);

                        Comes(npc, t);

                        // Her aurora, as she has always come with it (vanilla's state 0): harmless, and gone in a few seconds.
                        if (entered && Authority)
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0f, -80f), Vector2.Zero, ProjectileID.HallowBossDeathAurora, 0, 0f, Main.myPlayer);

                        // Her speech, in Legends, for whoever is in the scene.
                        if (Crossed(IntroFrom) && PADWorld.Legends && InScene(npc))
                            Taunt.Speech(npc, IntroWords, EmpressArt.Pink, EmpressArt.Sky, reach: 0f);

                        if (Authority && t >= SpawnFor)
                        {
                            // Down onto them: the cage starts where they are, and the arena stands over the ground there.
                            cageFrom = who.Center;
                            arenaCentre = ArenaCentre(cageFrom);
                            arenaSet = true;
                            dragTicks = (int)MathHelper.Clamp(Vector2.Distance(cageFrom, arenaCentre) / DragPace, DragLeast, DragMost);
                            Next(npc, ArriveFlash);
                        }

                        break;
                    }

                case ArriveFlash:
                    npc.Center = cageFrom;
                    npc.Opacity = 1f;

                    if (entered)
                        Flashes(npc);

                    HoldInCage(cageFrom, who);
                    ArrivalShot(npc, cageFrom, 1f, 1.18f, 0.2f);

                    if (Authority && t >= FlashTicks)
                        Next(npc, ArriveCage);

                    break;

                case ArriveCage:
                    npc.Center = cageFrom;
                    npc.Opacity = 1f;

                    HoldInCage(cageFrom, who);
                    ArrivalShot(npc, cageFrom, 1f, 1.25f);

                    if (entered && !Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.35f, Volume = 0.8f }, cageFrom);
                        SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.5f }, cageFrom);
                    }

                    if (Crossed(CageShuts) && !Main.dedServ)
                    {
                        SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f, Volume = 0.9f }, cageFrom);
                        Shake(cageFrom, 3f, 10);
                        PADParticleHandler.SpawnParticle(new BloomGeneric(cageFrom, Vector2.Zero, EmpressArt.Sky, CageRadius * 2.4f / 160f, 18));
                    }

                    if (Authority && t >= CageTicks)
                        Next(npc, ArriveDrag);

                    break;

                case ArriveDrag:
                    {
                        float p = Smoother(t / (float)Math.Max(1, dragTicks));
                        Vector2 cage = Vector2.Lerp(cageFrom, arenaCentre, p);

                        npc.Center = cage;
                        npc.Opacity = 1f;

                        HoldInCage(cage, who);
                        ArrivalShot(npc, cage, 1f, 1.12f);
                        Dragged(cage, t);

                        if (Authority && t >= dragTicks)
                            Next(npc, ArriveStars);

                        break;
                    }

                case ArriveStars:
                    npc.Center = arenaCentre;
                    npc.Opacity = 1f;

                    HoldInCage(arenaCentre, who);
                    ArrivalShot(npc, arenaCentre, 1f, MathHelper.Lerp(1.12f, 1f, Ease(t / 30f)));

                    if (entered)
                        StarsBurst(npc);

                    StarsFlying(npc, t);

                    if (Authority && t >= StarsTicks)
                        Next(npc, ArriveForm);

                    break;

                case ArriveForm:
                    {
                        npc.Center = arenaCentre;
                        npc.Opacity = 1f;

                        HoldInCage(arenaCentre, who);

                        // Out with the first star as it shoots to the arena's right corner, to watch the
                        // border drawn there; and back.
                        Vector2 star = StarFlight(0, StarsTicks + Math.Min(t, OutTicks));
                        Vector2 shot = t < FormTicks - RideBack
                            ? Vector2.Lerp(arenaCentre, star, Ease(t / (float)RideOut))
                            : Vector2.Lerp(star, arenaCentre, Ease((t - (FormTicks - RideBack)) / (float)RideBack));

                        ArrivalShot(npc, shot, 1f, 1f);

                        Forming(npc, t);

                        if (Authority && t >= FormTicks)
                            Next(npc, ArriveBreak);

                        break;
                    }

                case ArriveBreak:
                    npc.Center = arenaCentre;
                    npc.Opacity = 1f;
                    ArrivalShot(npc, Main.LocalPlayer.Center, 1f, 1.15f);

                    if (entered)
                        CageBreaks();

                    if (Authority && t >= BreakTicks)
                        Next(npc, ArriveFall);

                    break;

                case ArriveFall:
                    npc.Center = arenaCentre;
                    npc.Opacity = 1f;
                    ArrivalShot(npc, Main.LocalPlayer.Center, 1f, 1.15f);

                    if (Authority && t >= FallTicks)
                        Next(npc, ArriveCaught);

                    break;

                case ArriveCaught:
                    npc.Center = arenaCentre;
                    npc.Opacity = 1f;

                    if (InScene(npc))
                        EmpressArenaPlayer.Hover();

                    ArrivalShot(npc, Main.LocalPlayer.Center, 1f, 1.15f);

                    if (entered)
                        Caught();

                    if (Authority && t >= CaughtTicks)
                        Next(npc, ArriveRise);

                    break;

                case ArriveRise:
                    {
                        Vector2 over = who.Center + new Vector2(0f, -HoverAbove);
                        float p = Ease(t / (float)RiseTicks);

                        npc.Center = Vector2.Lerp(arenaCentre, over, p);
                        npc.Opacity = 1f;

                        if (InScene(npc))
                            EmpressArenaPlayer.Hover();

                        ArrivalShot(npc, Vector2.Lerp(Main.LocalPlayer.Center, npc.Center, 0.5f + 0.5f * p), 1f, MathHelper.Lerp(1.15f, 1.05f, p));
                        Rising(npc, t);

                        if (Authority && t >= RiseTicks)
                            Next(npc, ArriveScream);

                        break;
                    }

                case ArriveScream:
                    npc.Center = who.Center + new Vector2(0f, -HoverAbove + Bob());
                    npc.Opacity = 1f;

                    if (InScene(npc))
                        EmpressArenaPlayer.Hover();

                    ArrivalShot(npc, npc.Center, 1f, 0f);

                    if (entered)
                        Screams(npc);

                    Announce(npc);

                    // Her name is read; as it goes down to the bar, the fight.
                    if (Authority && t >= ScreamTicks)
                        Release(npc);

                    break;
            }
        }

        /// <summary>The arena as her coming has it: none until it forms; then forming, walls up when the cage breaks, her favour when it catches them.</summary>
        private void ArrivalArena(NPC npc)
        {
            // The arena is there once the stars are at its corners (its own drawing of them takes over then).
            if (beat < ArriveForm || beat == ArriveForm && timer < OutTicks)
                return;

            bool holds = beat >= ArriveBreak;
            bool grants = beat >= ArriveCaught;

            EmpressArena.Keep(arenaCentre, EmpressArena.Radius, holds, grants);

            if (beat == ArriveForm)
            {
                int t = timer;
                float line = Ease((t - LineFrom) / (float)LineGrows);
                float veil = Ease((t - VeilFrom) / (float)VeilTicks);
                float flare = t >= FlareFrom && t <= FlareFrom + FlareTicks ? 0.8f * (float)Math.Sin((t - FlareFrom) / (float)FlareTicks * MathHelper.Pi) : 0f;

                EmpressArena.Appear(line, veil, flare);
            }
        }

        /// <summary>Her coming is over, on this machine (Restore): the camera home, her sky staying, the music let back.</summary>
        private void EndArrival(NPC npc)
        {
            if (watching && !Main.dedServ)
                PisPlayer.PanHome(npc.Center, HomeTicks);

            watching = false;
            caged = false;
            cageDecided = false;

            npc.Opacity = 1f;
            npc.noGravity = true;
            npc.noTileCollide = true;

            // Whoever's name was not put up by the scene (a late join) still has it.
            if (!plated && !Main.dedServ)
                DialoguePortrait.Introduce(NPCID.HallowBoss);

            // In Legends, she has said her piece (her speech, as she came): a while before she remarks on anything.
            if (PADWorld.Legends && !Main.dedServ)
                talkRest = System.Math.Max(talkRest, TalkRest);
        }

        /// <summary>
        /// Her coming, skipped (CinematicSkip; single player, so this machine decides): her arena where it would have stood,
        /// the player where the drag would have left them, in the middle of it, and her over them; her speech cut; her name
        /// up; the fight.
        /// </summary>
        private void SkipArrival(NPC npc, Player who)
        {
            if (!arenaSet)
            {
                cageFrom = who.Center;
                arenaCentre = ArenaCentre(cageFrom);
                arenaSet = true;
            }

            // Not yet let go in her arena: put there, as the drag would have.
            if (beat < ArriveBreak && !Main.dedServ)
            {
                Player me = Main.LocalPlayer;
                me.Center = arenaCentre;
                me.velocity = Vector2.Zero;
                me.fallStart = (int)(me.position.Y / 16f);
            }

            npc.Center = who.Center + new Vector2(0f, -HoverAbove);
            npc.velocity = Vector2.Zero;

            Taunt.Hush(npc);
            Announce(npc);
            Release(npc);
        }

        /// <summary>This machine's player is near enough to be in her scene.</summary>
        private bool InScene(NPC npc)
        {
            if (Main.dedServ)
                return false;

            Player me = Main.LocalPlayer;

            return me.active && !me.dead && (caged || Vector2.Distance(me.Center, npc.Center) <= SceneRange);
        }

        /// <summary>
        /// The shot, for a player near enough to be in it: the camera <paramref name="blend"/> of the way to
        /// the focus, the bars, the interface away, the player held, and the lean, as a framing that counts
        /// the player's own zoom. A lean of 0 leaves the zoom to ease back (and to the name's plate).
        /// </summary>
        private void ArrivalShot(NPC npc, Vector2 focus, float blend, float lean, float rate = Cinematic.LeanRate)
        {
            if (!InScene(npc))
                return;

            watching = true;

            if (blend > 0f)
                PisPlayer.BlendCamera(focus, blend);

            PisPlayer.HideUI();
            Cinematic.Hold();
            Cinematic.Frame();

            if (lean > 0f)
                Cinematic.Lean(Math.Max(1f, lean / Math.Max(1f, Main.GameZoomTarget)), rate);
        }

        // ---- the flicker -------------------------------------------------------------------------------

        /// <summary>The light going and coming, this tick: the sky's light, the moon, the black, and the sound of each going.</summary>
        private void Flickering(Player who, int t)
        {
            if (Main.dedServ)
                return;

            int step = FlickerStep(t, out int from);
            float light = Flicker[step, 1];
            float moon = Flicker[step, 2];
            float flare = Flicker[step, 3];
            float black = Flicker[step, 4];

            int next = step + 1 < Flicker.GetLength(0) ? (int)Flicker[step + 1, 0] : FlickerTicks;
            float into = MathHelper.Clamp((t - from) / (float)Math.Max(1, next - from), 0f, 1f);

            EmpressSky.Light(light);
            EmpressSky.Moon(moon, flare * (1f - 0.7f * into));

            if (black > 0f)
                Cinematic.Fade(black);

            // The sound of each going out, deeper for the black; a glint for the moon thrown bright.
            if (t == from && t > 0)
            {
                if (black > 0f)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -1f, Volume = 0.5f }, who.Center);
                    SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.8f, Volume = 0.35f }, who.Center);
                    Shake(who.Center, 2.5f, 8);
                }
                else if (light < 0.5f)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.9f, Volume = 0.22f, MaxInstances = 3 }, who.Center);
                }
                else if (flare > 0.5f)
                {
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.4f, Volume = 0.3f, MaxInstances = 3 }, who.Center);
                }
            }
        }

        // ---- she comes ----------------------------------------------------------------------------------

        /// <summary>As she has always come (vanilla's state 0): her sound, and her colours raining down onto her as she fades up.</summary>
        private void Comes(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            if (t == 10)
                SoundEngine.PlaySound(SoundID.Item161, npc.Center);

            if (t <= 10)
                return;

            for (int i = 0; i < 2; i++)
            {
                float fade = MathHelper.Lerp(1.3f, 0.7f, npc.Opacity) * Utils.GetLerpValue(0f, 120f, t, clamped: true);
                Color colour = Main.hslToRgb(t / 180f % 1f, 1f, 0.5f);

                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 0, colour);
                dust.position = npc.Center + Main.rand.NextVector2Circular(npc.width * 3f, npc.height * 3f) + new Vector2(0f, -150f);
                dust.velocity *= Main.rand.NextFloat() * 0.8f;
                dust.velocity += Vector2.UnitY * 3f;
                dust.noGravity = true;
                dust.fadeIn = 0.6f + Main.rand.NextFloat() * 0.7f * fade;
                dust.scale = 0.35f;

                Dust white = Dust.CloneDust(dust);
                white.scale /= 2f;
                white.fadeIn *= 0.85f;
                white.color = Color.White;
            }
        }

        /// <summary>Down onto them: a flash, her sound turned bright, and a burst of her colours where she lands.</summary>
        private void Flashes(NPC npc)
        {
            if (Main.dedServ)
                return;

            float near = Near(cageFrom);

            if (near > 0f)
            {
                Cinematic.Flash(Color.White, 0.75f * near);
                Shake(cageFrom, 5f, 16);
            }

            SoundEngine.PlaySound(SoundID.Item161 with { Pitch = 0.3f, Volume = 1f }, cageFrom);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.6f }, cageFrom);

            SparkShard.Spray(cageFrom, Color.White, 18, 8f, 90f, 22, palette: EmpressArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(cageFrom, Vector2.Zero, EmpressArt.Pink, 360f / 160f, 24));
            PADParticleHandler.SpawnParticle(new BloomGeneric(cageFrom, Vector2.Zero, Color.White, 160f / 160f, 14));

            // Her colours left all the way down from where she was.
            Vector2 from = spawnAt + new Vector2(0f, SpawnDrift);

            for (int i = 0; i < 40; i++)
            {
                Vector2 at = Vector2.Lerp(from, cageFrom, Main.rand.NextFloat());
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(30f, 30f), DustID.RainbowMk2, Main.rand.NextVector2Circular(1.5f, 1.5f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f), Main.rand.NextFloat(0.8f, 1.4f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }
        }

        // ---- the cage --------------------------------------------------------------------------------------

        /// <summary>This machine's player goes in her cage if they are near enough: the summoner, and anyone with them.</summary>
        private void Cage(Player who)
        {
            if (Main.dedServ)
                return;

            Player me = Main.LocalPlayer;
            caged = me.active && !me.dead && Vector2.Distance(me.Center, cageFrom) <= SceneRange;
        }

        /// <summary>This machine's player, held in the cage with its middle here: the summoner at the middle of it, anyone else beside them.</summary>
        private void HoldInCage(Vector2 cage, Player who)
        {
            if (!caged || Main.dedServ)
                return;

            Vector2 beside = Vector2.Zero;

            if (Main.myPlayer != who.whoAmI)
                beside = new Vector2((Main.myPlayer % 2 == 0 ? -1f : 1f) * (30f + 16f * (Main.myPlayer / 2 % 3)), 6f);

            EmpressArenaPlayer.Pin(cage + beside);
        }

        /// <summary>The drag: wind past it, light left behind it, the sound of it going and of it stopping.</summary>
        private void Dragged(Vector2 cage, int t)
        {
            if (Main.dedServ)
                return;

            if (t == 0)
            {
                SoundEngine.PlaySound(SoundID.Item165 with { Pitch = -0.3f, Volume = 0.8f }, cage);
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.3f, Volume = 0.6f }, cage);
            }

            if (t == dragTicks - 1)
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 0.8f }, cage);
                Shake(cage, 4f, 12);
                PADParticleHandler.SpawnParticle(new BloomGeneric(cage, Vector2.Zero, EmpressArt.Lilac, 300f / 160f, 20));
            }

            Vector2 way = arenaCentre - cageFrom;

            if (way.LengthSquared() < 1f)
                return;

            way.Normalize();

            // Streaks of her colours left behind the cage, and sparks off its corners.
            for (int i = 0; i < 2; i++)
            {
                Vector2 at = cage + Main.rand.NextVector2Circular(CageRadius * 1.4f, CageRadius * 1.4f);
                PADParticleHandler.SpawnParticle(new SparkShard(at, -way * Main.rand.NextFloat(6f, 12f), EmpressArt.Prism(Main.rand.NextFloat()),
                    20f, 18f, 60f, 16));
            }

            if (t % 3 == 0)
            {
                int k = Main.rand.Next(6);
                Dust dust = Dust.NewDustPerfect(Hexagon.Corner(cage, CageRadius, k), DustID.RainbowMk2, -way * 2f + Main.rand.NextVector2Circular(1f, 1f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.65f), 1.1f);
                dust.noGravity = true;
            }
        }

        // ---- the stars --------------------------------------------------------------------------------------

        /// <summary>
        /// Where star <paramref name="i"/> is, <paramref name="t"/> ticks into its flight (STARS, then on into
        /// FORM): spinning out of her and round her, slowing as it spreads, into a ring with the others,
        /// each over its own corner; then shooting straight out to it.
        /// </summary>
        internal Vector2 StarFlight(int i, float t)
        {
            float home = MathHelper.TwoPi * i / Decagon.Stars;

            if (t <= StarsTicks)
            {
                float p = MathHelper.Clamp(t / StarsTicks, 0f, 1f);
                float spread = SpinReach * (1f - (float)Math.Pow(1f - p, 2.2));
                float angle = home - StarSpin * (float)Math.Pow(1f - p, 1.6);

                return arenaCentre + angle.ToRotationVector2() * spread;
            }

            float q = MathHelper.Clamp((t - StarsTicks) / OutTicks, 0f, 1f);
            return arenaCentre + home.ToRotationVector2() * MathHelper.Lerp(SpinReach, EmpressArena.Radius, Ease(q));
        }

        /// <summary>The whole of the stars' flight, ring and corners: past it, they are the arena's.</summary>
        internal const int FlightTicks = StarsTicks + OutTicks;

        /// <summary>The ten stars leave her: a flash of her colours, her sound for it, her light thrown out every way.</summary>
        private void StarsBurst(NPC npc)
        {
            if (Main.dedServ)
                return;

            float near = Near(npc.Center);

            if (near > 0f)
            {
                Cinematic.Flash(EmpressArt.Pink, 0.35f * near);
                Shake(npc.Center, 7f, 24);
            }

            SoundEngine.PlaySound(SoundID.Item163 with { Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f, Volume = 0.9f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.1f, Volume = 0.7f }, npc.Center);

            SparkShard.Spray(npc.Center, Color.White, 26, 11f, 140f, 26, palette: EmpressArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Pink, 520f / 160f, 30));
            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Sky, 300f / 160f, 22));

            for (int i = 0; i < 50; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center, DustID.RainbowMk2, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 13f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f), Main.rand.NextFloat(1f, 1.8f));
                dust.noGravity = true;
                dust.fadeIn = 1.4f;
            }
        }

        /// <summary>Their light along the way: sparks off each star as it goes, while it is near enough to see.</summary>
        private void StarsFlying(NPC npc, int t)
        {
            if (Main.dedServ || t >= StarsTicks)
                return;

            for (int i = 0; i < Decagon.Stars; i++)
            {
                Vector2 at = StarFlight(i, t);

                if (Vector2.Distance(at, Main.LocalPlayer.Center) > 1800f)
                    continue;

                if (Main.rand.NextBool(2))
                {
                    Dust dust = Dust.NewDustPerfect(at, DustID.RainbowMk2, Main.rand.NextVector2Circular(2f, 2f), 0, Main.hslToRgb((i / 10f + t * 0.01f) % 1f, 1f, 0.65f), 1.3f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.1f;
                }
            }

            if (t % 10 == 0)
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.5f + t * 0.01f, Volume = 0.35f, MaxInstances = 4 }, npc.Center);
        }

        /// <summary>The border drawn out of the stars, and what lies outside coming: the sounds of it, the light of it.</summary>
        private void Forming(NPC npc, int t)
        {
            // The sounds and the light of it for whoever is in the scene: the camera is out at the corner.
            if (!InScene(npc))
                return;

            Vector2 corner = Decagon.Star(arenaCentre, EmpressArena.Radius, 0);

            // The stars shoot out of the ring.
            if (Crossed(1))
            {
                SoundEngine.PlaySound(SoundID.Item165 with { Pitch = 0.4f, Volume = 0.7f }, arenaCentre);
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.6f, Volume = 0.6f }, arenaCentre);
            }

            // They land on their corners: a chime, and a burst of light off the one in the shot.
            if (Crossed(OutTicks - 2))
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 0.8f }, corner);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.6f, Volume = 0.6f }, corner);
                Main.LocalPlayer.PIS().BumpScreenShake(3f, 10);

                PADParticleHandler.SpawnParticle(new BloomGeneric(corner, Vector2.Zero, EmpressArt.Pink, 320f / 160f, 22));
                SparkShard.Spray(corner, Color.White, 12, 8f, 90f, 20, palette: EmpressArt.Sparks);
            }

            // The line goes out of the stars.
            if (Crossed(LineFrom))
            {
                SoundEngine.PlaySound(SoundID.Item25 with { Pitch = -0.2f, Volume = 0.9f }, corner);
                SoundEngine.PlaySound(SoundID.Item165 with { Pitch = 0.2f, Volume = 0.6f }, corner);
            }

            // What lies outside comes.
            if (Crossed(VeilFrom))
            {
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Pitch = -0.4f, Volume = 0.8f }, corner);
            }

            // The line is whole: the border thrown white, and rung.
            if (Crossed(LineFrom + LineGrows))
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.5f, Volume = 1f }, corner);
                SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.3f, Volume = 0.8f }, corner);
                Cinematic.Flash(EmpressArt.Sky, 0.22f);
                Main.LocalPlayer.PIS().BumpScreenShake(5f, 18);

                PADParticleHandler.SpawnParticle(new BloomGeneric(corner, Vector2.Zero, EmpressArt.Sky, 600f / 160f, 30));
                SparkShard.Spray(corner, Color.White, 16, 9f, 120f, 24, palette: EmpressArt.Sparks);
            }

            // Sparks off the line as it runs out of the star in front of the camera.
            if (t > LineFrom && t < LineFrom + LineGrows + 2 && Main.rand.NextBool(2))
            {
                float grown = Ease((t - LineFrom) / (float)LineGrows);

                foreach (int neighbour in new[] { 1, 9 })
                {
                    Vector2 toward = Decagon.Star(arenaCentre, EmpressArena.Radius, neighbour);
                    Vector2 tip = Vector2.Lerp(corner, toward, 0.5f * grown);

                    Dust dust = Dust.NewDustPerfect(tip, DustID.RainbowMk2, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f), 1.5f);
                    dust.noGravity = true;
                    dust.fadeIn = 1.3f;
                }
            }
        }

        // ---- out of the cage ------------------------------------------------------------------------------

        /// <summary>The cage breaks: the sound of it, its sides thrown off (drawn), splinters, sparks.</summary>
        private void CageBreaks()
        {
            breakAt = Main.GameUpdateCount;

            if (Main.dedServ)
                return;

            Vector2 at = arenaCentre;
            float near = Near(at);

            if (near > 0f)
            {
                Cinematic.Flash(Color.White, 0.3f * near);
                Shake(at, 7f, 20);
            }

            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.1f, Volume = 1f }, at);
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.3f, Volume = 0.9f }, at);
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Pitch = 0.2f, Volume = 0.7f }, at);

            for (int i = 0; i < 40; i++)
            {
                int type = (i % 3) switch
                {
                    0 => DustID.PinkCrystalShard,
                    1 => DustID.BlueCrystalShard,
                    _ => DustID.PurpleCrystalShard,
                };

                Vector2 from = Hexagon.Corner(at, CageRadius, i % 6) + Main.rand.NextVector2Circular(16f, 16f);
                Dust dust = Dust.NewDustPerfect(from, type, (from - at).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(3f, 8f), 0, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = Main.rand.NextBool();
            }

            SparkShard.Spray(at, Color.White, 22, 9f, 100f, 22, palette: EmpressArt.Sparks);
            PADParticleHandler.SpawnParticle(new BloomGeneric(at, Vector2.Zero, EmpressArt.Sky, 320f / 160f, 20));
        }

        /// <summary>Her favour catches them: a ring of her colours round them, and a clear sound.</summary>
        private void Caught()
        {
            if (Main.dedServ || !InSceneLocal())
                return;

            Player me = Main.LocalPlayer;

            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.6f, Volume = 0.7f }, me.Center);
            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.4f, Volume = 0.6f }, me.Center);

            for (int i = 0; i < 36; i++)
            {
                Vector2 way = (i / 36f * MathHelper.TwoPi).ToRotationVector2();
                Dust dust = Dust.NewDustPerfect(me.Center + way * 20f, DustID.RainbowMk2, way * 4f, 0, Main.hslToRgb(i / 36f, 1f, 0.65f), 1.3f);
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            PADParticleHandler.SpawnParticle(new CrownGlint(me.Center, 60f, 30, EmpressArt.Pink));
            PADParticleHandler.SpawnParticle(new BloomGeneric(me.Center, Vector2.Zero, EmpressArt.Pink, 140f / 160f, 20));
        }

        private bool InSceneLocal() => caged || Vector2.Distance(Main.LocalPlayer.Center, arenaCentre) <= SceneRange;

        /// <summary>Her light trailing her as she rises.</summary>
        private void Rising(NPC npc, int t)
        {
            if (Main.dedServ)
                return;

            if (t == 0)
                SoundEngine.PlaySound(SoundID.Item165 with { Pitch = 0.3f, Volume = 0.5f }, npc.Center);

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(60f, 50f), DustID.RainbowMk2, new Vector2(0f, 2f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f), 1.1f);
                dust.noGravity = true;
                dust.fadeIn = 1f;
            }
        }

        /// <summary>
        /// She screams (her dash's own shriek, the game's, and a roar's rings about her rimmed in her pink),
        /// her colours thrown off her, the screen shaken; and the name is hers on the panel from now.
        /// </summary>
        private void Screams(NPC npc)
        {
            if (Main.dedServ)
                return;

            Cinematic.Roar(npc, ScreamTicks, 1.15f, quiet: true, rim: EmpressArt.Pink);
            SoundEngine.PlaySound(SoundID.Item160 with { Volume = 1f }, npc.Center);
            SoundEngine.PlaySound(SoundID.Item161 with { Pitch = -0.3f, Volume = 0.7f }, npc.Center);

            for (int i = 0; i < 60; i++)
            {
                Dust dust = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.RainbowMk2, Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(5f, 14f), 0,
                    Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f), Main.rand.NextFloat(1.2f, 2f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            PADParticleHandler.SpawnParticle(new BloomGeneric(npc.Center, Vector2.Zero, EmpressArt.Pink, 420f / 160f, 26));
            DialoguePortrait.Introduce(NPCID.HallowBoss);
        }

        /// <summary>Her name, once, on this machine: the long plate, from her scream.</summary>
        private void Announce(NPC npc)
        {
            if (plated || Main.dedServ)
                return;

            plated = true;
            BossIntro.PlayNameOnly(npc.whoAmI, Plate);
        }

        // ---- small things ------------------------------------------------------------------------------------

        /// <summary>A slow bob, for her hanging in the air.</summary>
        private float Bob() => 6f * (float)Math.Sin(Main.GameUpdateCount * 0.05f);

        /// <summary>How much a thing happening here is felt on this machine's screen: in full near the player, none far off.</summary>
        private static float Near(Vector2 at)
        {
            if (Main.dedServ)
                return 0f;

            return 1f - MathHelper.Clamp((Vector2.Distance(Main.LocalPlayer.Center, at) - 900f) / 1600f, 0f, 1f);
        }

        /// <summary>The screen shaken here, as much as it is felt.</summary>
        private static void Shake(Vector2 at, float power, int ticks)
        {
            float near = Near(at);

            if (near > 0f)
                Main.LocalPlayer.PIS().BumpScreenShake(power * near, ticks);
        }

        /// <summary>Smoothstep. Zero slope at both ends.</summary>
        private static float Ease(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smootherstep: zero slope and zero bend at both ends, for a drag that starts and stops with no jolt.</summary>
        private static float Smoother(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
