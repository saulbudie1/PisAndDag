using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.PADplayer;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.UI
{
    /// <summary>
    /// What one boss's bar says and does. Every field is optional — a boss with no entry
    /// at all still gets a bar, named from its type.
    /// </summary>
    public sealed class BossPlateEntry
    {
        /// <summary>Null means the NPC's own type name.</summary>
        public string Name;

        /// <summary>The line under the name during the arrival. Null means the name goes alone.</summary>
        public Func<string> Title;

        /// <summary>
        /// True for a boss that animates its own arrival, so the camera side of BossIntro
        /// stays out — the name still rises and lands, once that arrival has the boss
        /// centred. Takes the NPC so it can depend on world state (King Slime only in
        /// Legends).
        /// </summary>
        public Func<NPC, bool> OwnIntro;

        /// <summary>The arrival's timing, or null for BossIntroProfile.Default.</summary>
        public BossIntroProfile Intro;

        /// <summary>
        /// False for a boss that must keep moving through its arrival (the Wall). True
        /// otherwise: BossArrivalWait holds the boss still until the camera is home.
        /// </summary>
        public bool Waits = true;

        /// <summary>
        /// True for something vanilla gives a boss bar to that is not a boss — an event's
        /// big enemy. It keeps the bar and the name at rest; it gets no arrival at all.
        /// </summary>
        public bool Plain;
    }


    /// <summary>
    /// The boss health bar, replacing vanilla's for EVERY boss, and the name on it.
    ///
    /// A long thin line across the bottom of the screen with the name above its LEFT end,
    /// the way Elden Ring does it — no frame, no icon, no plate. The fill is a window
    /// onto a drifting nebula and star field (see THE GALAXY below) rather than a colour.
    ///
    /// Universal by construction: vanilla still decides WHO is being fought and HOW MUCH
    /// life that is — the Twins added together, every Eater segment, Golem's head and
    /// body, a pillar's shield — through its own BigProgressBarSystem. PADBossBar sits
    /// on the GlobalBossBar.PreDraw hook that every vanilla bar and every ModBossBar
    /// funnels through, copies those numbers out, and returns false so vanilla draws
    /// nothing. This class then draws the same fight its way. No registration is
    /// needed for any boss, vanilla or modded; Register only overrides what is shown.
    ///
    /// Two things make this different from an ordinary custom bar:
    ///
    /// 1. It survives a sequence's Main.hideUI. Every other piece of interface goes
    ///    away during a boss animation; this does not, because the bar coming up IS
    ///    part of the animation. Its layer sits ahead of "Vanilla: Hide UI Toggle" (see
    ///    BossPlateSystem), it calls vanilla's bar Draw itself so the numbers keep
    ///    coming, and it still obeys the player's own F11 (PisPlayer.HidingUI).
    ///
    /// 2. The name is not always at rest. BossIntro takes it over for the arrival — it
    ///    rises below the boss mid-screen and flies down to the bar — so the position
    ///    below is only where the name ends up.
    ///
    /// The arrival is universal too. A boss with no animation of its own gets the full
    /// BossIntro: pan, hold, name, home. A boss that animates its own arrival (OwnIntro)
    /// keeps its camera work, and the name flight fires on its own the moment that
    /// camera work puts the boss in the middle of the screen — the same rise and fly
    /// every boss gets, under the boss, while the shot is on it. Nothing has to be
    /// called. (Becoming damageable is the backstop for an arrival with no camera.)
    ///
    /// The NAME is never withheld. What is optional is understanding it — that lives in
    /// the TITLE, seen only in flight and gone when the name touches the bar.
    ///
    /// A mod boss, in its own SetDefaults:
    ///
    ///     NPC.PAD().BossName  = "Megashark";                    // optional; type name otherwise
    ///     NPC.PAD().BossTitle = "Made because it could be.";     // optional
    ///     NPC.PAD().OwnIntro  = true;                            // only if it animates its own arrival
    ///
    /// A vanilla boss, from any PostSetupContent — also all optional:
    ///
    ///     BossPlate.Register(NPCID.WallofFlesh, title: "Three, holding each other down.", intro: wallProfile);
    ///     BossPlate.HasOwnIntro(NPCID.KingSlime, npc =&gt; PADWorld.Legends);
    ///     BossPlate.NoWait(NPCID.WallofFlesh);                  // keeps moving through its arrival
    ///
    /// While the universal arrival runs, the boss does not fight — BossArrivalWait
    /// holds it (or hovers it, for a flyer) for ArrivalTicks, on server and client
    /// alike. OwnIntro bosses run their own; NoWait opts out.
    /// </summary>
    public static class BossPlate
    {
        // ============================================================
        // THE BAR
        // ============================================================

        /// <summary>Bar width as a fraction of the screen. Long and thin is the whole idea.</summary>
        private const float BarWidthFraction = 0.54f;

        /// <summary>Height of the filled part. The empty track is thinner (see TrackHeight).</summary>
        private const int BarHeight = 8;

        /// <summary>The unfilled remainder is thinner than the fill, so loss reads as erosion.</summary>
        private const int TrackHeight = 2;

        /// <summary>A faint one-pixel line around the whole bar, so it reads on any background.</summary>
        private const float OutlineAlpha = 0.45f;
        private static readonly Color OutlineColour = new Color(18, 14, 20);

        /// <summary>Pixels from the screen's bottom edge to the bar's top.</summary>
        private const int BarBottom = 64;

        /// <summary>Bright cap on the leading edge of the fill.</summary>
        private const int TipWidth = 2;

        /// <summary>
        /// Ruggedness. Every Nth column of the fill drops a shade — a fixed pattern from
        /// the column index rather than rand, because a per-frame roll would crawl.
        /// </summary>
        private const int GrainEvery = 7;

        /// <summary>How fast the bar chases a health change. Per tick, as a fraction of the gap.</summary>
        private const float DrainRate = 0.08f;

        /// <summary>A shielded boss (the pillars) shows the shield as a second, paler line above the fill.</summary>
        private const int ShieldHeight = 2;

        // ============================================================
        // THE GALAXY — what the fill is made of
        // ============================================================
        //
        // The fill is not a colour, it is a WINDOW: two tileable strips — a nebula and
        // a star field — drift behind the bar, and the fill shows however much of them
        // its width uncovers. The pattern is pinned to the screen and to the clock,
        // not to the bar, so when health drops the window shrinks over a sky that
        // keeps moving rather than the picture squashing with it (the unmoving-plaid
        // trick, which is all the effect is). Two strips at two speeds is what makes
        // it read as depth. Plain SpriteBatch with source rectangles; no shader.

        /// <summary>false draws the flat fill with the grain instead. Textures still load.</summary>
        private static readonly bool GalaxyEnabled = true;

        /// <summary>Drift, in pixels per second. Opposite signs on the two strips is the parallax.</summary>
        private const float NebulaScrollX = -22f;
        private const float NebulaScrollY = 3f;
        private const float StarScrollX = 41f;
        private const float StarScrollY = -5f;

        /// <summary>The stars ride on top at this alpha, so the nebula stays the body of it.</summary>
        private const float StarAlpha = 0.85f;

        /// <summary>The strips. 2048×64 each, tileable in both axes; PisAndDag/Content/UI/.</summary>
        private static Asset<Texture2D> nebula;
        private static Asset<Texture2D> stars;

        internal static void LoadTextures()
        {
            if (Main.dedServ)
                return;

            nebula = ModContent.Request<Texture2D>("PisAndDag/Content/UI/BossBarNebula", AssetRequestMode.ImmediateLoad);
            stars = ModContent.Request<Texture2D>("PisAndDag/Content/UI/BossBarStars", AssetRequestMode.ImmediateLoad);
        }

        internal static void UnloadTextures()
        {
            nebula = null;
            stars = null;
        }

        // ============================================================
        // THE NAME
        // ============================================================

        /// <summary>Gap between the bottom of the name's box and the bar's top, at rest.</summary>
        private const int NameGap = 9;

        /// <summary>In flight only: gap between the name's box and the title under it.</summary>
        internal const int TitleBelowName = 2;

        /// <summary>DeathText is enormous at 1. Internal so BossIntro measures with the same number.</summary>
        internal const float NameScale = 0.39f;
        internal const float TitleScale = 0.59f;

        private static readonly Color NameColour = new Color(233, 227, 212);

        /// <summary>PADGlobalItem's LoreColour. The title is lore, so it wears the lore colour.</summary>
        private static readonly Color TitleColour = new Color(196, 180, 152);

        private static readonly Color FillColour = new Color(196, 66, 58);
        private static readonly Color FillGrain = new Color(140, 44, 40);
        private static readonly Color TipColour = new Color(240, 178, 150);
        private static readonly Color TrackColour = new Color(58, 50, 52);
        private static readonly Color ShieldColour = new Color(150, 170, 196);

        /// <summary>The ghost of health just lost, draining behind the fill.</summary>
        private static readonly Color DrainColour = new Color(126, 104, 96);

        // ============================================================
        // TIMING
        // ============================================================

        private const int FadeInTicks = 40;
        private const int FadeOutTicks = 26;

        /// <summary>
        /// How long the fight is presumed to continue after vanilla stops reporting it.
        /// Covers the frame or two vanilla takes to move from a dead Eater segment or a
        /// dead twin to the next one, so the bar does not blink between parts.
        /// </summary>
        private const int FightGapTicks = 12;

        /// <summary>
        /// How stale the numbers from PreDraw may be before the NPC is read directly.
        /// Draws and updates are not in lockstep; this covers a frame-rate collapse.
        /// </summary>
        private const uint StashTolerance = 30;

        /// <summary>
        /// For a boss with its own arrival: the name rises the moment that arrival has
        /// the boss in the middle of the screen — within this many pixels of the view's
        /// centre, for this many ticks running, so a pan merely passing through does
        /// not count. Becoming damageable is the backstop for an arrival with no camera.
        /// </summary>
        private const float CentreRadius = 48f;
        private const int CentreTicks = 3;

        // The look before the name is the plate's own LOOK — the same the universal
        // arrival gives, so an OwnIntro boss's plate is timed identically. Its own camera
        // hold has to cover CentreTicks + BossIntro.PlateTicks(hasTitle) after it lands
        // (Megashark.RiseHold). A cutscene that knows its own moment calls
        // BossIntro.PlayNameOnly itself instead (the King, on the burst).

        // ============================================================
        // THE GLOW
        // ============================================================
        //
        // A boss that arrives at night, or underground, or in the Corruption, can be a
        // silhouette through its own plate. While the arrival owns the shot — and while
        // an OwnIntro boss is still waiting for its name — it is lit a little from
        // within: not a spotlight, enough to read. The light lingers through the
        // settle after the name lands, then lets go.

        private const float GlowStrength = 0.55f;
        private static readonly Vector3 GlowColour = new Vector3(1f, 0.93f, 0.82f);

        /// <summary>How far out from the centre the body is also lit, as a fraction of its size.</summary>
        private const float GlowSpread = 0.35f;

        /// <summary>Ticks of full glow after the arrival lets go of the shot — the settle, for a name-only run.</summary>
        private const int GlowLinger = BossIntro.TitleSettle;

        private const int GlowFadeTicks = 40;

        // ============================================================
        // NAMES FOR BOSSES IN PIECES
        // ============================================================
        //
        // Vanilla tracks one NPC of a multi-part boss and that NPC's type name is the
        // part's, not the boss's. These are the ones where that reads wrong.

        private static readonly Dictionary<int, string> PartNames = new()
        {
            { 125, "The Twins" }, { 126, "The Twins" },
            { 245, "Golem" }, { 246, "Golem" },
            { 396, "Moon Lord" }, { 397, "Moon Lord" }, { 398, "Moon Lord" },
            { 393, "Martian Saucer" }, { 394, "Martian Saucer" }, { 395, "Martian Saucer" },
            { 492, "The Flying Dutchman" },
        };

        // ============================================================
        // WHAT IS NOT A BOSS
        // ============================================================
        //
        // Vanilla hands a boss bar to anything with a boss head icon, which sweeps in the
        // big enemies of the events — the Dutchman, the saucer, the moons' visitors, the
        // Old One's Army's champions. They are not bosses and an arrival makes them look
        // like ones: the world should not stop for a Mourning Wood. These keep the bar
        // and the name at rest, and nothing else — no pan, no title, no hold, no glow.
        //
        // Never add a summoned true boss here (the Cultist, Deerclops); they earn it.

        private static readonly HashSet<int> NotBosses = new()
        {
            NPCID.MourningWood, NPCID.Pumpking,                        // Pumpkin Moon
            NPCID.Everscream, NPCID.IceQueen, NPCID.SantaNK1,          // Frost Moon
            NPCID.MartianSaucer, NPCID.MartianSaucerTurret,            // Martian Madness
            NPCID.MartianSaucerCannon, NPCID.MartianSaucerCore,
            NPCID.PirateShip, NPCID.PirateShipCannon,                  // Pirate Invasion
            NPCID.Mothron,                                             // Solar Eclipse
            NPCID.DD2Betsy, NPCID.DD2DarkMageT1, NPCID.DD2DarkMageT3,  // Old One's Army
            NPCID.DD2OgreT2, NPCID.DD2OgreT3,
        };

        // ============================================================
        // STATE
        // ============================================================

        private static readonly Dictionary<int, BossPlateEntry> registry = new();

        // What PADBossBar.PreDraw last saw. Vanilla's numbers, vanilla's choice of NPC.
        // The type rides along so a freed slot reused by something else — a Red Devil
        // spawning into the Wall's slot the moment it dies — is never mistaken for a
        // report about the newcomer.
        private static int stashIndex = -1;
        private static int stashType = -1;
        private static float stashLife;
        private static float stashLifeMax = 1f;
        private static float stashShield;
        private static float stashShieldMax;
        private static uint stashTick;

        /// <summary>Index of the NPC whose bar this is, or -1 between fights.</summary>
        private static int fightIndex = -1;

        /// <summary>Its type, so a reused slot is not mistaken for the same boss.</summary>
        private static int fightType = -1;

        /// <summary>Resolved when the fight starts or the tracked part changes.</summary>
        private static BossPlateEntry current;

        private static float fade;
        private static float life;
        private static float lifeMax = 1f;
        private static float shield;
        private static float shieldMax;

        /// <summary>Trails behind `life` so a big hit leaves a receding ghost.</summary>
        private static float drain;

        /// <summary>Ticks since vanilla last reported the fight. See FightGapTicks.</summary>
        private static int gap;

        /// <summary>An OwnIntro boss is waiting for its cutscene to centre it before its name rises.</summary>
        private static bool nameFlightPending;

        /// <summary>Consecutive ticks the pending boss has sat in the middle of the screen.</summary>
        private static int centredTicks;

        /// <summary>The glow, 0..1, and the ticks of full glow left once the arrival is over.</summary>
        private static float glow;
        private static int glowHold;


        // ============================================================
        // REGISTRATION — all optional
        // ============================================================

        /// <summary>Override what a boss type shows. Every argument may be left null.</summary>
        public static void Register(int npcType, string name = null, string title = null, BossIntroProfile intro = null)
        {
            Func<string> fixedTitle = title == null ? null : () => title;

            Register(npcType, name, fixedTitle, intro);
        }

        public static void Register(int npcType, string name, Func<string> title, BossIntroProfile intro = null)
        {
            BossPlateEntry entry = Entry(npcType);

            if (name != null) entry.Name = name;
            if (title != null) entry.Title = title;
            if (intro != null) entry.Intro = intro;
        }

        /// <summary>
        /// This type animates its own arrival; the camera side of BossIntro stays out.
        /// The condition is checked when the fight starts — null means always.
        /// </summary>
        public static void HasOwnIntro(int npcType, Func<NPC, bool> when = null)
        {
            Entry(npcType).OwnIntro = when ?? (_ => true);
        }

        /// <summary>This type keeps moving through its arrival; BossArrivalWait leaves it alone.</summary>
        public static void NoWait(int npcType)
        {
            Entry(npcType).Waits = false;
        }

        /// <summary>
        /// This type carries a boss bar but is not a boss — an event's big enemy. Bar and
        /// name only: no arrival, no title, no hold. See NotBosses for the vanilla ones,
        /// which need no call.
        /// </summary>
        public static void NotABoss(int npcType)
        {
            Entry(npcType).Plain = true;
        }

        private static BossPlateEntry Entry(int npcType)
        {
            if (!registry.TryGetValue(npcType, out BossPlateEntry entry))
                registry[npcType] = entry = new BossPlateEntry();

            return entry;
        }

        /// <summary>The entry being shown, or null. BossIntro measures the name from it.</summary>
        internal static BossPlateEntry Current => fightIndex >= 0 ? current : null;

        /// <summary>
        /// Everything the bar will say about this NPC: its own fields first, the registry
        /// second, then the part-name table, then the type name. Titles are delegates so
        /// a boss that reassigns BossTitle mid-fight is read on the next frame.
        /// </summary>
        private static BossPlateEntry Resolve(NPC npc)
        {
            PADGlobalNPC own = npc.PAD();
            registry.TryGetValue(npc.type, out BossPlateEntry reg);

            string name = !string.IsNullOrEmpty(own.BossName) ? own.BossName
                : reg?.Name
                ?? (PartNames.TryGetValue(npc.type, out string part) ? part : npc.TypeName);

            Func<string> regTitle = reg?.Title;
            Func<string> title = () => !string.IsNullOrEmpty(npc.PAD().BossTitle) ? npc.PAD().BossTitle : regTitle?.Invoke();

            bool ownIntro = own.OwnIntro || (reg?.OwnIntro?.Invoke(npc) ?? false);
            bool plain = NotBosses.Contains(npc.type) || (reg?.Plain ?? false);

            return new BossPlateEntry
            {
                Name = name,
                Title = plain ? null : title,
                OwnIntro = _ => ownIntro,
                Intro = reg?.Intro,
                Waits = reg?.Waits ?? true,
                Plain = plain,
            };
        }

        /// <summary>
        /// How long this NPC's universal arrival runs, spawn to camera home — what
        /// BossArrivalWait holds it still for. 0 for a boss with its own arrival or one
        /// registered NoWait. Reads only the NPC and the registry, so it is the same
        /// answer on the server as on every client.
        /// </summary>
        public static int ArrivalTicks(NPC npc)
        {
            BossPlateEntry entry = Resolve(npc);

            if (entry.Plain || !entry.Waits || entry.OwnIntro(npc))
                return 0;

            bool titled = !string.IsNullOrWhiteSpace(entry.Title?.Invoke());

            return BossIntro.Length(entry.Intro ?? BossIntroProfile.Default, titled);
        }

        /// <summary>Mod unload. Statics do not clear themselves.</summary>
        public static void Clear()
        {
            registry.Clear();
            current = null;
            stashIndex = -1;
            fightIndex = -1;
            fightType = -1;
            fade = 0f;
            drain = 0f;
            gap = 0;
            nameFlightPending = false;
            centredTicks = 0;
            glow = 0f;
            glowHold = 0;
        }


        // ============================================================
        // WHAT VANILLA TELLS US
        // ============================================================

        /// <summary>From PADBossBar.PreDraw: the NPC vanilla chose and the life it summed.</summary>
        internal static void Report(NPC npc, float lifeNow, float lifeTop, float shieldNow, float shieldTop)
        {
            stashIndex = npc.whoAmI;
            stashType = npc.type;
            stashLife = lifeNow;
            stashLifeMax = lifeTop;
            stashShield = shieldNow;
            stashShieldMax = shieldTop;
            stashTick = Main.GameUpdateCount;
        }


        // ============================================================
        // TRACKING
        // ============================================================

        /// <summary>
        /// Keeps the bar's idea of the fight honest, and starts the arrival when a new
        /// one begins. Vanilla's own tracking (its Update runs in the game loop, so it
        /// carries on while the UI is hidden) says who; this only has to notice changes.
        /// </summary>
        internal static void Track()
        {
            if (Main.dedServ)
                return;

            bool fresh = stashIndex >= 0 && Main.GameUpdateCount - stashTick <= StashTolerance;
            NPC npc = stashIndex >= 0 ? Main.npc[stashIndex] : null;
            bool alive = npc != null && npc.active && npc.life > 0 && npc.type == stashType;

            if (fresh && alive)
            {
                if (fightIndex < 0)
                {
                    Begin(npc);
                }
                else if (stashIndex != fightIndex)
                {
                    // Vanilla moved to another part of the same fight — the other twin,
                    // the next segment. Same bar, same fade, no second arrival.
                    fightIndex = stashIndex;
                    fightType = npc.type;
                    current = Resolve(npc);
                }

                gap = 0;
                life = stashLife;
                lifeMax = Math.Max(stashLifeMax, 1f);
                shield = stashShield;
                shieldMax = stashShieldMax;
                drain = MathHelper.Lerp(drain, life, DrainRate);
                fade = Math.Min(fade + 1f, FadeInTicks);

                PendingNameFlight(npc);
                Light(npc);
                return;
            }

            if (fightIndex >= 0)
            {
                NPC mine = Main.npc[fightIndex];

                // Vanilla's report is stale but the boss is plainly still here — a
                // frame-rate collapse, or it drifted out of vanilla's range. Read it
                // directly rather than dropping the bar.
                if (mine.active && mine.life > 0 && mine.type == fightType)
                {
                    gap = 0;
                    life = mine.life;
                    lifeMax = Math.Max(mine.lifeMax, 1);
                    drain = MathHelper.Lerp(drain, life, DrainRate);
                    fade = Math.Min(fade + 1f, FadeInTicks);

                    PendingNameFlight(mine);
                    Light(mine);
                    return;
                }

                // Gone. Give vanilla a moment to pick up the next part before calling it.
                if (++gap <= FightGapTicks)
                    return;

                // Over. The stash goes too, so nothing that lands in the freed slot can
                // be taken for a late report about this fight.
                fightIndex = -1;
                fightType = -1;
                stashIndex = -1;
                stashType = -1;
                current = null;
                nameFlightPending = false;
                glow = 0f;
                glowHold = 0;
            }

            fade -= (float)FadeInTicks / FadeOutTicks;

            if (fade <= 0f)
                fade = 0f;
        }

        /// <summary>A new fight. Resolve what to show, then choose which arrival it gets.</summary>
        private static void Begin(NPC npc)
        {
            fightIndex = npc.whoAmI;
            fightType = npc.type;
            current = Resolve(npc);
            fade = 0f;
            life = stashLife;
            lifeMax = Math.Max(stashLifeMax, 1f);
            drain = life;
            gap = 0;

            if (current.Plain)
            {
                // Not a boss. The bar fades up and the name is simply there; nothing
                // takes the camera, nothing holds it still, nothing lights it.
                nameFlightPending = false;
            }
            else if (current.OwnIntro(npc))
            {
                // Its own camera work. The name still has to land — it rises the moment
                // that camera work puts the boss in the middle of the screen, which is
                // the Megashark's RiseHold and the King's Rite.Rise.
                nameFlightPending = true;
                centredTicks = 0;
            }
            else
            {
                nameFlightPending = false;
                BossIntro.Play(npc.whoAmI, current.Intro);
            }
        }

        /// <summary>
        /// For an OwnIntro boss: fire the name-only arrival once its own cutscene has
        /// held the boss centred on screen for a beat — or, failing that, when the boss
        /// becomes damageable — unless the boss's own code already started one.
        ///
        /// Centre is measured in world space against the view's centre, which is the
        /// same point whatever the zoom is doing (vanilla zooms about the centre), so
        /// this needs nothing from the camera code and works for any cutscene that
        /// ends up looking at the boss, however it got there.
        /// </summary>
        private static void PendingNameFlight(NPC npc)
        {
            if (!nameFlightPending)
                return;

            if (BossIntro.Running)
            {
                nameFlightPending = false;
                return;
            }

            Vector2 viewCentre = Main.screenPosition + new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;
            bool centred = Vector2.Distance(npc.Center, viewCentre) <= CentreRadius;

            centredTicks = centred ? centredTicks + 1 : 0;

            bool titled = !string.IsNullOrWhiteSpace(current.Title?.Invoke());
            int look = titled ? BossIntro.TitleLook : BossIntro.PlainLook;

            if (centredTicks >= CentreTicks + look || !npc.dontTakeDamage)
            {
                nameFlightPending = false;
                BossIntro.PlayNameOnly(npc.whoAmI, current.Intro);
            }
        }

        /// <summary>
        /// Lights the boss from within while its arrival is on. Full while the intro
        /// runs or the name is still owed, held for GlowLinger after, then faded.
        /// Light is added over the body, not at one point, so a big boss reads across.
        /// </summary>
        private static void Light(NPC npc)
        {
            bool arriving = !(current?.Plain ?? false) && (BossIntro.Running || nameFlightPending);

            if (arriving)
            {
                glow = 1f;
                glowHold = GlowLinger;
            }
            else if (glowHold > 0)
            {
                glowHold--;
            }
            else
            {
                glow = Math.Max(0f, glow - 1f / GlowFadeTicks);
            }

            if (glow <= 0f)
                return;

            Vector3 light = GlowColour * (GlowStrength * glow);
            Vector2 spread = new Vector2(npc.width, npc.height) * GlowSpread;

            Lighting.AddLight(npc.Center, light);
            Lighting.AddLight(npc.Center + new Vector2(-spread.X, -spread.Y), light * 0.6f);
            Lighting.AddLight(npc.Center + new Vector2(spread.X, -spread.Y), light * 0.6f);
            Lighting.AddLight(npc.Center + new Vector2(-spread.X, spread.Y), light * 0.6f);
            Lighting.AddLight(npc.Center + new Vector2(spread.X, spread.Y), light * 0.6f);
        }


        // ============================================================
        // GEOMETRY
        // ============================================================

        private static Rectangle BarRect()
        {
            int w = (int)(Main.screenWidth * BarWidthFraction);

            return new Rectangle((Main.screenWidth - w) / 2, Main.screenHeight - BarBottom, w, BarHeight);
        }

        /// <summary>Where the name comes to rest: the bar's left end, above it.</summary>
        internal static Vector2 NameRest()
        {
            Rectangle bar = BarRect();
            DynamicSpriteFont big = FontAssets.DeathText.Value;

            return new Vector2(bar.X, bar.Y - NameGap - big.MeasureString("M").Y * NameScale);
        }


        // ============================================================
        // DRAW
        // ============================================================

        public static void Draw(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ)
                return;
             
            if (Main.hideUI && !PisPlayer.HidingUI)
                return;
             
            Main.BigBossProgressBar.Draw(spriteBatch);

            DrawBar(spriteBatch);
            DrawName(spriteBatch);
        }

        private static float Alpha()
        {
            float a = MathHelper.Clamp(fade / FadeInTicks, 0f, 1f);

            // Smoothstep, so it arrives rather than ramps.
            return a * a * (3f - 2f * a);
        }

        private static void DrawBar(SpriteBatch spriteBatch)
        {
            if (fade <= 0f)
                return;

            float alpha = Alpha();
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle bar = BarRect();

            float pct = MathHelper.Clamp(life / lifeMax, 0f, 1f);
            float ghost = MathHelper.Clamp(drain / lifeMax, 0f, 1f);

            int fill = (int)(bar.Width * pct);
            int ghostW = (int)(bar.Width * ghost);

            // The outline: one pixel outside the bar on all four sides, faint, under
            // everything else.
            Color line = OutlineColour * (alpha * OutlineAlpha);

            spriteBatch.Draw(pixel, new Rectangle(bar.X - 1, bar.Y - 1, bar.Width + 2, 1), line);
            spriteBatch.Draw(pixel, new Rectangle(bar.X - 1, bar.Y + BarHeight, bar.Width + 2, 1), line);
            spriteBatch.Draw(pixel, new Rectangle(bar.X - 1, bar.Y, 1, BarHeight), line);
            spriteBatch.Draw(pixel, new Rectangle(bar.X + bar.Width, bar.Y, 1, BarHeight), line);

            // The empty remainder: thinner than the fill, on the bar's centre line, so a
            // spent bar is a scratch across the screen rather than an empty box.
            int trackY = bar.Y + (BarHeight - TrackHeight) / 2;

            spriteBatch.Draw(pixel, new Rectangle(bar.X, trackY, bar.Width, TrackHeight),
                TrackColour * alpha);

            // What was just lost, receding behind the fill.
            if (ghostW > fill)
            {
                spriteBatch.Draw(pixel, new Rectangle(bar.X + fill, bar.Y, ghostW - fill, BarHeight),
                    DrainColour * (alpha * 0.75f));
            }

            if (fill > 0)
            {
                Rectangle window = new Rectangle(bar.X, bar.Y, fill, BarHeight);

                if (GalaxyEnabled && nebula != null && stars != null)
                {
                    DrawDrifting(spriteBatch, nebula.Value, window, NebulaScrollX, NebulaScrollY, Color.White * alpha);
                    DrawDrifting(spriteBatch, stars.Value, window, StarScrollX, StarScrollY, Color.White * (alpha * StarAlpha));
                }
                else
                {
                    spriteBatch.Draw(pixel, window, FillColour * alpha);

                    // Grain: a fixed pattern of darker columns, so the line reads as
                    // beaten metal rather than a progress bar. Deterministic in x.
                    for (int x = GrainEvery / 2; x < fill; x += GrainEvery)
                    {
                        int h = (x / GrainEvery) % 3 == 0 ? BarHeight : BarHeight - 2;

                        spriteBatch.Draw(pixel, new Rectangle(bar.X + x, bar.Y, 1, h), FillGrain * alpha);
                    }
                }

                spriteBatch.Draw(pixel,
                    new Rectangle(bar.X + Math.Max(fill - TipWidth, 0), bar.Y, Math.Min(TipWidth, fill), BarHeight),
                    TipColour * alpha);
            }

            // A shield (the pillars) as a paler line just above the fill.
            if (shieldMax > 0f && shield > 0f)
            {
                int sw = (int)(bar.Width * MathHelper.Clamp(shield / shieldMax, 0f, 1f));

                spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y - ShieldHeight - 1, sw, ShieldHeight),
                    ShieldColour * alpha);
            }
        }

        /// <summary>
        /// Fills <paramref name="window"/> with <paramref name="strip"/> as seen through
        /// it: the strip's origin sits at screen (0,0) plus the drift, so every window
        /// anywhere on screen shows the same continuous sky, and the sky moves on its
        /// own clock. Split into as many source rectangles as the wrap needs — the
        /// sprite batch is clamped, so seams are done by hand rather than by sampler.
        /// </summary>
        private static void DrawDrifting(SpriteBatch spriteBatch, Texture2D strip, Rectangle window,
            float driftX, float driftY, Color colour)
        {
            int tw = strip.Width;
            int th = strip.Height;

            // GameUpdateCount rather than the hourly clock: it never jumps, and it
            // stops when the game does, which is the right thing for a pause.
            float seconds = Main.GameUpdateCount / 60f;

            int ox = Wrap(window.X + (int)(driftX * seconds), tw);
            int oy = Wrap(window.Y + (int)(driftY * seconds), th);

            for (int y = 0; y < window.Height;)
            {
                int sy = (oy + y) % th;
                int h = Math.Min(th - sy, window.Height - y);

                for (int x = 0; x < window.Width;)
                {
                    int sx = (ox + x) % tw;
                    int w = Math.Min(tw - sx, window.Width - x);

                    spriteBatch.Draw(strip,
                        new Rectangle(window.X + x, window.Y + y, w, h),
                        new Rectangle(sx, sy, w, h),
                        colour);

                    x += w;
                }

                y += h;
            }
        }

        /// <summary>Modulo that lands in [0, m) for negative inputs too.</summary>
        private static int Wrap(int v, int m) => ((v % m) + m) % m;

        private static void DrawName(SpriteBatch spriteBatch)
        {
            BossPlateEntry entry = Current;

            if (entry == null)
                return;

            // Not yet. An OwnIntro boss's name arrives by its flight, not before it —
            // otherwise it sits on the bar through the cutscene and then jumps up to
            // rise from under the boss.
            if (nameFlightPending)
                return;

            Vector2 rest = NameRest();

            // The intro owns the name while it is flying it down from the boss.
            Vector2 pos = rest;
            float alpha = Alpha();
            float titleAlpha = 0f;
            float scale = 1f;
            bool centred = false;

            bool flying = !entry.Plain
                && BossIntro.NamePlacement(rest, out pos, out alpha, out titleAlpha, out scale, out centred);

            if (!flying)
            {
                pos = rest;
                alpha = Alpha();
                titleAlpha = 0f;
                scale = 1f;
                centred = false;
            }

            if (alpha <= 0f)
                return;

            float anchorX = centred ? 0.5f : 0f;

            Utils.DrawBorderStringBig(spriteBatch, entry.Name, pos,
                NameColour * alpha, NameScale * scale, anchorX, 0f);

            // The title exists only in flight: under the name, moving with it, and gone
            // when the name touches the bar. It is never drawn at rest.
            if (titleAlpha <= 0f)
                return;

            string title = entry.Title?.Invoke();

            if (string.IsNullOrWhiteSpace(title))
                return;

            float nameH = FontAssets.DeathText.Value.MeasureString(entry.Name).Y * NameScale * scale;

            Utils.DrawBorderString(spriteBatch, title,
                new Vector2(pos.X, pos.Y + nameH + TitleBelowName * scale),
                TitleColour * titleAlpha, TitleScale * scale, anchorX, 0f);
        }
    }


    /// <summary>
    /// Where vanilla's numbers are taken and vanilla's drawing is refused.
    ///
    /// GlobalBossBar.PreDraw is called from inside BigProgressBarHelper.DrawFancyBar,
    /// which every vanilla bar — common, Twins, Eater, Golem, Moon Lord, pillars — and
    /// every ModBossBar calls with its combined life already worked out. Returning
    /// false skips the draw, and unlike ModBossBarStyle it needs no selection in the
    /// settings menu: it applies the moment the mod is enabled.
    /// </summary>
    public class PADBossBar : GlobalBossBar
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            BossPlate.Report(npc, drawParams.Life, drawParams.LifeMax, drawParams.Shield, drawParams.ShieldMax);

            return false;
        }
    }


    /// <summary>
    /// Hangs the bar off the interface — AHEAD of "Vanilla: Hide UI Toggle".
    ///
    /// That layer is how F11 works: when Main.hideUI is set it returns false, and the
    /// interface loop stops there, so nothing after it draws at all. Every ordinary
    /// layer, ours included if it sat with the resource bars, is after it. Going in
    /// front is what lets the bar survive a cutscene; Draw() then decides for itself
    /// whether the hide is a sequence's (stay) or the player's (go).
    /// </summary>
    public class BossPlateSystem : ModSystem
    {
        public override void Load()
        {
            BossPlate.LoadTextures();
        }

        public override void Unload()
        {
            BossPlate.UnloadTextures();
            BossPlate.Clear();
            BossIntro.Clear();
        }

        public override void PostUpdateEverything()
        {
            // Order matters: the intro may be started by Track, and Tick advances it on
            // the same frame it was started so there is no idle tick at the front.
            BossPlate.Track();
            BossIntro.Tick();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Hide UI Toggle");

            // Mouse Text is always present. Falling back to it keeps the bar working if
            // the toggle is ever renamed — it just would not survive hideUI.
            if (index == -1)
                index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Boss Bar",
                () =>
                {
                    BossPlate.Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
