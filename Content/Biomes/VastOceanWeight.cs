using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Buffs;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Subworlds;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Biomes
{
    /// <summary>
    /// THE WATER'S WEIGHT on whoever is in the Vast Ocean, by request ("Create depth pressure
    /// system: start at section 6 lose 10HP a sec; up to section 10 where you lose 140Hp a sec
    /// without adequate gear. 11 lose 200HP a sec. and 12 just crush you", "put much effort
    /// into the depth pressure system", and since: "I want you to make anythinig below section
    /// 8 implode, except if they had the right gear"). By the player's middle (VastOcean.RowOf),
    /// as the depths' biomes go:
    ///
    ///   - from the top of the 6th section, life taken every second: 10 there, more with every
    ///     row down, evenly, on toward 140 at the top of the 10th (<see cref="LifePerSecond"/>),
    ///     some 107 at the foot of the 8th. Taken as the game takes life to a poison
    ///     (Player.lifeRegen, which counts half points a second): none of the player's own
    ///     coming back while it lasts, and the numbers rising off them as it goes;
    ///   - under the 8th, from the top of the 9th (<see cref="ImplodeFrom"/>), imploded at once
    ///     (Player.KillMe): the sea closing on them from all round
    ///     (<see cref="VastOceanImplosion"/>), a deep crunch, the screen shaken. The 140 and the
    ///     200 of the 10th and the 11th are now only where the slope of the life taken is aimed:
    ///     nobody without the gear for that depth is alive there to feel them;
    ///   - none of it down to the deepest section what the player has on holds against
    ///     (<see cref="HoldTo"/>): the 5th with nothing, the 8th under Water! Resuscitate Me!
    ///     (Resuscitated), all twelve in the HEATR (<see cref="Deepest"/>). Under that, the first
    ///     section that is the 9th or deeper implodes them, and any of the 6th to the 8th takes
    ///     life, as if nothing were held.
    ///
    /// Seen and heard, on the player's own machine (<see cref="VastOceanWeightPlayer"/>,
    /// <see cref="VastOceanWeightSystem"/>):
    ///   - coming down to where it begins for them, the last <see cref="WarnRows"/> rows over
    ///     it: "The water grows heavy" over them, once each way down; the dark coming in at the
    ///     screen's corners; a slow heart; the Depth Meter amber;
    ///   - while it takes life: The Water's Weight among their debuffs, saying how much a
    ///     second; the dark closing in on the screen the more it takes, from deep blue to a
    ///     dark red, and pressing in a little on every beat; the heart faster and louder, 62 to
    ///     150 beats a minute; the Depth Meter red (VastOceanDepthMeter);
    ///   - coming down to where it implodes them, the last <see cref="BrinkRows"/> rows over it:
    ///     "Any deeper and the sea closes in" over them, once each way down; the dark closing all
    ///     the way in and going red, the heart at its fastest by the line; the Depth Meter
    ///     flashing red and white;
    ///   - dying of it: the game's death, told as the sea's doing (DeathText: Pressed1 to 3, and
    ///     Crushed1 to 3 for the implosion, seen and heard by everyone there, as the death is).
    /// The heart and the crunch are sounds of the deep (Content/Sounds/Deep), heard through the
    /// silence from the 8th section down (VastOceanSound). None of it under Journey's godmode.
    ///
    /// ON THE SEA'S CREATURES TOO, by request ("Crushing depth also applies to NPCS too (except
    /// they are immune to it)", and the implosion: "anything"): the same life a second by their
    /// middle, taken as the game takes it to a poison (NPC.lifeRegen, the numbers rising off
    /// them, the last of it killing them as a poison does, on the machine the world is decided
    /// on), and from the 9th imploded at once (NPC.StrikeInstantKill, with the crunch and the
    /// sea closing on them). Each is safe as deep as it lives (<see cref="SafeTo"/>): the 5th,
    /// as a player with nothing on; the 6th for the game's creatures of deep dark water, the
    /// deepest they are put down in (VastOceanSpawns: the caves' jellyfish, the angler fish,
    /// the squid); its own for one of the deep (<see cref="IDeepNative"/>). Never any of it
    /// (<see cref="NeverPressed"/>): a boss, a townsperson, a dummy, an attack that is a
    /// creature only in name, what cannot be hurt, and the body of a worm (what the head takes
    /// is the worm's, once).
    /// </summary>
    public static class VastOceanWeight
    {
        /// <summary>The section it begins in.</summary>
        public const int FirstSection = 6;

        /// <summary>The first section in which it implodes what it reaches, not taking life: the 9th, the first under the 8th.</summary>
        public const int ImplodeFrom = 9;

        /// <summary>The deepest section, the floor's and the trench's: one safe down to its end is safe everywhere.</summary>
        public const int Deepest = VastOcean.Sections;

        /// <summary>The deepest section that is safe with nothing held against it: the 5th.</summary>
        public const int Unheld = FirstSection - 1;

        /// <summary>Life taken a second at the top of the 6th, of the 10th, and of the 11th (and through it).</summary>
        public const float AtFirst = 10f;
        public const float AtTenth = 140f;
        public const float AtEleventh = 200f;

        /// <summary>The rows over where it begins for a player in which they are warned: 40, 80 feet as the Depth Meter reads.</summary>
        public const int WarnRows = 40;

        /// <summary>The rows over where it implodes them in which they are warned again: 80, 160 feet as the Depth Meter reads.</summary>
        public const int BrinkRows = 80;

        /// <summary>The lines for dying of the implosion: DeathText.Crushed1 to 3.</summary>
        public const int CrushedLines = 3;

        /// <summary>Feet to a row, as the Depth Meter counts them (VastOceanDepthMeter).</summary>
        public const int FeetPerRow = 2;

        /// <summary>The heart, one beat (lub, dub); and the crush. Sounds of the deep: heard in the silence (VastOceanSound).</summary>
        public static readonly SoundStyle Heartbeat = new SoundStyle("PisAndDag/Content/Sounds/Deep/Heartbeat", SoundType.Sound)
        {
            MaxInstances = 3,
            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest,
            PauseBehavior = PauseBehavior.PauseWithGame,
        };

        public static readonly SoundStyle Crunch = new SoundStyle("PisAndDag/Content/Sounds/Deep/Crushed", SoundType.Sound)
        {
            MaxInstances = 1,
        };

        /// <summary>Life taken a second at this row by the water's weight, nothing held against it: none over the top of the 6th.</summary>
        public static float LifePerSecond(float row)
        {
            float first = VastOcean.SectionTop(FirstSection);
            float tenth = VastOcean.SectionTop(10);
            float eleventh = VastOcean.SectionTop(11);

            if (row < first)
                return 0f;

            if (row < tenth)
                return MathHelper.Lerp(AtFirst, AtTenth, (row - first) / (tenth - first));

            if (row < eleventh)
                return MathHelper.Lerp(AtTenth, AtEleventh, (row - tenth) / (eleventh - tenth));

            return AtEleventh;
        }

        /// <summary>The first row it reaches for one safe down to the end of this section.</summary>
        public static int FirstRowFor(int safeTo) => VastOcean.SectionTop(Math.Max(FirstSection, safeTo + 1));

        /// <summary>The first row it implodes one safe down to the end of this section in: the top of the first section under it that is the 9th or deeper; for one safe in the deepest, none.</summary>
        public static int ImplodeRowFor(int safeTo) => safeTo >= Deepest ? int.MaxValue : VastOcean.SectionTop(Math.Max(ImplodeFrom, safeTo + 1));

        /// <summary>Imploded at this row, safe down to the end of this section: in a section under it that is the 9th or deeper.</summary>
        public static bool Implodes(int safeTo, float row)
        {
            int section = VastOcean.SectionOf(row);
            return section >= ImplodeFrom && section > safeTo;
        }

        /// <summary>The depth a section begins at, in feet as the Depth Meter reads them: the 6th at 2240, the 9th at 3584.</summary>
        public static int FeetAt(int section) => (VastOcean.SectionTop(section) - VastOcean.Surface) * FeetPerRow;

        /// <summary>
        /// For what is worn or put on (an accessory's UpdateAccessory, a buff's Update): safe from
        /// the water's weight down to the end of this section, this tick. The deepest of all said
        /// is the one that counts.
        /// </summary>
        public static void HoldTo(Player player, int section)
        {
            VastOceanWeightPlayer weight = player.GetModPlayer<VastOceanWeightPlayer>();
            weight.SafeTo = Math.Max(weight.SafeTo, section);
        }

        /// <summary>The deepest section the game's creatures of deep dark water are put down in, and safe to (VastOceanSpawns).</summary>
        public const int DarkWater = 6;

        /// <summary>
        /// Never pressed, at any depth: a boss, or what counts as one; a townsperson, or one that
        /// acts as one; a dummy; an attack that is a creature only in name (the game's own list of
        /// those, NPCID.Sets.ProjectileNPC); what cannot be hurt now; and a piece of something
        /// whose life is another's (a worm's body: the game takes what a piece suffers from the
        /// head's life, so what the head takes is taken once).
        /// </summary>
        public static bool NeverPressed(NPC npc)
        {
            return npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
                || npc.townNPC || NPCID.Sets.ActsLikeTownNPC[npc.type]
                || npc.type == NPCID.TargetDummy || NPCID.Sets.ProjectileNPC[npc.type]
                || npc.immortal || npc.dontTakeDamage
                || npc.realLife >= 0 && npc.realLife != npc.whoAmI;
        }

        /// <summary>
        /// The deepest section a creature is safe in: its own, if it is of the deep
        /// (<see cref="IDeepNative"/>); the 6th for the game's creatures of deep dark water;
        /// else the 5th, as a player with nothing on.
        /// </summary>
        public static int SafeTo(NPC npc)
        {
            if (npc.ModNPC is IDeepNative native)
                return native.SafeTo;

            return npc.type switch
            {
                NPCID.BlueJellyfish or NPCID.GreenJellyfish or NPCID.AnglerFish or NPCID.Squid => DarkWater,
                _ => Unheld,
            };
        }
    }

    /// <summary>
    /// A creature of the deep: safe from the water's weight down to the end of this section of
    /// its own (<see cref="VastOceanWeight.Deepest"/>: it never feels it at all).
    /// </summary>
    public interface IDeepNative
    {
        int SafeTo { get; }
    }

    /// <summary>The water's weight on one of the sea's creatures (<see cref="VastOceanWeight"/>): what it takes, the implosion from the 9th, and how it shows.</summary>
    public class VastOceanWeightNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Life it takes from this one a second, as of this tick.</summary>
        public float Taking { get; private set; }

        /// <summary>Its implosion seen and heard, once.</summary>
        private bool crunched;

        /// <summary>Pressed at all, here and now: in the Vast Ocean, not one that never is, and not safe at every depth.</summary>
        private static bool Feels(NPC npc, out int safeTo)
        {
            safeTo = VastOceanWeight.Unheld;

            if (SubworldKeeper.Current is not VastOcean || VastOceanWeight.NeverPressed(npc))
                return false;

            safeTo = VastOceanWeight.SafeTo(npc);
            return safeTo < VastOceanWeight.Deepest;
        }

        public override void ResetEffects(NPC npc)
        {
            Taking = 0f;
        }

        /// <summary>
        /// What it takes, as the game takes life to a poison: lifeRegen counts half points a
        /// second, and the life goes in pieces a quarter of a second's worth or so, each shown
        /// rising off it (NPC.UpdateNPC_BuffApplyDOTs), not a number for every point.
        /// </summary>
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            Taking = 0f;

            if (!Feels(npc, out int safeTo))
                return;

            float row = npc.Center.Y / 16f;

            if (row < VastOceanWeight.FirstRowFor(safeTo))
                return;

            Taking = VastOceanWeight.LifePerSecond(row);

            if (Taking <= 0f)
                return;

            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= (int)Math.Round(Taking * 2f);

            int piece = (int)Math.Ceiling(Taking / 4f);

            if (damage < piece)
                damage = piece;
        }

        /// <summary>From the 9th, under what it is safe to: imploded, whatever life is left; seen and heard where it can be, and done where the world is decided.</summary>
        public override void PostAI(NPC npc)
        {
            if (!npc.active || !Feels(npc, out int safeTo) || !VastOceanWeight.Implodes(safeTo, npc.Center.Y / 16f))
                return;

            if (!crunched && !Main.dedServ)
            {
                crunched = true;
                SoundEngine.PlaySound(VastOceanWeight.Crunch with { Volume = 0.7f }, npc.Center);
                VastOceanImplosion.Begin(npc.Center, (npc.width + npc.height) / 48f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
                npc.StrikeInstantKill();
        }

        /// <summary>Pressed, it darkens toward red: a little at the first of it, a good deal at its worst.</summary>
        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (Taking <= 0f)
                return;

            float heavy = MathHelper.Clamp(Taking / VastOceanWeight.AtEleventh, 0f, 1f);
            Color pressed = new Color((int)(drawColor.R * 0.9f), (int)(drawColor.G * 0.45f), (int)(drawColor.B * 0.4f), drawColor.A);

            drawColor = Color.Lerp(drawColor, pressed, 0.25f + 0.5f * heavy);
        }
    }

    /// <summary>The water's weight on one player: how deep they are safe, what it takes from them, and how it is told to them.</summary>
    public class VastOceanWeightPlayer : ModPlayer
    {
        /// <summary>The deepest section this player is safe in this tick: the 5th, or deeper by what they have on (<see cref="VastOceanWeight.HoldTo"/>).</summary>
        public int SafeTo = VastOceanWeight.Unheld;

        /// <summary>Life the water's weight takes from them a second, as of this tick.</summary>
        public float Taking { get; private set; }

        /// <summary>How near they are to where it begins for them, coming down: 0 above the last <see cref="VastOceanWeight.WarnRows"/> rows over it, 1 at the line. Naught where nothing is taken before the implosion (held to the 8th).</summary>
        public float Nearing { get; private set; }

        /// <summary>How near they are to where it implodes them, coming down: 0 above the last <see cref="VastOceanWeight.BrinkRows"/> rows over it, 1 at the line.</summary>
        public float Brink { get; private set; }

        /// <summary>Rows over where it begins for them (under it, below naught).</summary>
        private float over = float.MaxValue;

        /// <summary>Rows over where it implodes them.</summary>
        private float overBrink = float.MaxValue;

        // ---- on the player's own machine only: what is seen and heard -------------------------

        /// <summary>How hard it is shown pressing, 0 to 1, eased toward what it is.</summary>
        internal float Shown;

        /// <summary>How far to red it is shown, 0 to 1.</summary>
        internal float ShownRed;

        /// <summary>The beat: 1 as the heart beats, dying away.</summary>
        internal float Beat;

        private int nextBeat;
        private bool warned;
        private bool warnedBrink;

        private static readonly Color WarnColour = new Color(150, 200, 225);
        private static readonly Color BrinkColour = new Color(255, 120, 110);

        public override void ResetEffects()
        {
            SafeTo = VastOceanWeight.Unheld;
        }

        /// <summary>Dead (PostUpdate is not run then): nothing taken, and the dark let go of over a second or so.</summary>
        public override void UpdateDead()
        {
            Taking = 0f;
            Nearing = 0f;
            Brink = 0f;
            over = float.MaxValue;
            overBrink = float.MaxValue;
            Shown *= 0.92f;
            ShownRed *= 0.92f;
            Beat = 0f;
        }

        /// <summary>
        /// Where it begins for them, and what it takes: after the player's buffs and what they
        /// wear have said how deep they are safe, as both are done before this in a tick
        /// (Player.Update: UpdateBuffs, UpdateEquips, then this, then their life).
        /// </summary>
        public override void PostUpdateMiscEffects()
        {
            Taking = 0f;
            Nearing = 0f;
            Brink = 0f;
            over = float.MaxValue;
            overBrink = float.MaxValue;

            if (SubworldKeeper.Current is not VastOcean || Player.dead || Player.ghost || Player.creativeGodMode || SafeTo >= VastOceanWeight.Deepest)
                return;

            float row = VastOcean.RowOf(Player);
            int first = VastOceanWeight.FirstRowFor(SafeTo);
            int implode = VastOceanWeight.ImplodeRowFor(SafeTo);

            overBrink = implode - row;
            Brink = Smooth(1f - overBrink / VastOceanWeight.BrinkRows);

            // Held to the 8th: nothing is taken before the implosion.
            if (first >= implode)
                return;

            over = first - row;

            if (over > 0f)
            {
                Nearing = Smooth(1f - over / VastOceanWeight.WarnRows);
                return;
            }

            Taking = VastOceanWeight.LifePerSecond(row);
        }

        /// <summary>0 to 1, eased at both ends.</summary>
        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>What it takes, as the game takes life to a poison: lifeRegen counts half points a second.</summary>
        public override void UpdateBadLifeRegen()
        {
            if (Taking <= 0f)
                return;

            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= (int)Math.Round(Taking * 2f);
        }

        /// <summary>
        /// Life run out with nothing more particular to blame (the game's reasons 8, 9 and 10:
        /// burning, poison, electrified; Player.UpdateLifeRegen) while the water's weight was
        /// taking it: it was the water. Drowning is still drowning.
        /// </summary>
        public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
        {
            if (Taking > 0f && damageSource.SourceOtherIndex is 8 or 9 or 10)
            {
                damageSource = PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey("Mods.PisAndDag.DeathText.Pressed" + Main.rand.Next(1, 4), Player.name));
            }

            return true;
        }

        /// <summary>
        /// Imploded (Crushed1 to 3, as <see cref="PostUpdate"/> says it): the sea closing on them,
        /// and the crunch, on every machine that has them, each told of the death with its words
        /// (Player.KillMe there too, from the death sent on); the screen shaken on their own.
        /// </summary>
        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Main.dedServ || SubworldKeeper.Current is not VastOcean || !Imploded(damageSource))
                return;

            SoundEngine.PlaySound(VastOceanWeight.Crunch, Player.Center);
            VastOceanImplosion.Begin(Player.Center, 1.25f);

            if (Player.whoAmI == Main.myPlayer)
                Player.GetModPlayer<PisPlayer>().BumpScreenShake(14f, 24);
        }

        /// <summary>A death told in one of the implosion's lines: what was sent is the line's key and the name, so the words are the same on every machine.</summary>
        private bool Imploded(PlayerDeathReason reason)
        {
            if (reason == null)
                return false;

            NetworkText said = reason.CustomReason;

            if (said == null)
                return false;

            string text = said.ToString();

            for (int i = 1; i <= VastOceanWeight.CrushedLines; i++)
            {
                if (text == Language.GetTextValue("Mods.PisAndDag.DeathText.Crushed" + i, Player.name))
                    return true;
            }

            return false;
        }

        public override void OnRespawn()
        {
            Shown = ShownRed = Beat = 0f;
            warned = warnedBrink = false;
            nextBeat = 0;
        }

        /// <summary>On the player's own machine: the implosion, the debuff shown, the warnings, the heart, and how hard the dark is shown pressing.</summary>
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            // From the 9th, under what they have on holds against: imploded, whatever life is left.
            if (SubworldKeeper.Current is VastOcean && !Player.dead && !Player.ghost && !Player.creativeGodMode
                && VastOceanWeight.Implodes(SafeTo, VastOcean.RowOf(Player)))
            {
                Player.KillMe(PlayerDeathReason.ByCustomReason(
                    NetworkText.FromKey("Mods.PisAndDag.DeathText.Crushed" + Main.rand.Next(1, VastOceanWeight.CrushedLines + 1), Player.name)), 10.0, 0);
                return;
            }

            if (Taking > 0f)
                Player.AddBuff(ModContent.BuffType<WatersWeight>(), 2);

            // Told once each way down: as the warning begins, or at once if put straight under it;
            // again only after coming back up past the warning and twenty rows more.
            if (!warned && (Nearing > 0f || Taking > 0f) && !Player.dead)
            {
                warned = true;

                if (!Main.dedServ)
                    CombatText.NewText(Player.Hitbox, WarnColour, Language.GetTextValue("Mods.PisAndDag.Subworlds.Weight.Heavy"));
            }
            else if (warned && over > VastOceanWeight.WarnRows + 20f)
            {
                warned = false;
            }

            // And so over where it implodes them.
            if (!warnedBrink && Brink > 0f && !Player.dead)
            {
                warnedBrink = true;

                if (!Main.dedServ)
                    CombatText.NewText(Player.Hitbox, BrinkColour, Language.GetTextValue("Mods.PisAndDag.Subworlds.Weight.Brink"), true);
            }
            else if (warnedBrink && overBrink > VastOceanWeight.BrinkRows + 20f)
            {
                warnedBrink = false;
            }

            if (Main.dedServ)
                return;

            // How hard it is to be shown: a little over the line, more the more it takes; all the
            // way, and red, at the line where it implodes them.
            float heavy = MathHelper.Clamp(Taking / VastOceanWeight.AtEleventh, 0f, 1f);
            float pressing = Player.dead ? 0f : Math.Max(Taking > 0f ? 0.3f + 0.7f * heavy : 0.3f * Nearing, Brink);
            float red = Player.dead ? 0f : Math.Max(heavy, Brink);

            Shown += (pressing - Shown) * 0.06f;
            ShownRed += (red - ShownRed) * 0.04f;
            Beat *= 0.88f;

            // The heart, while it presses or is about to: slow and soft at the warning, fast and loud at its worst.
            if (pressing > 0.01f)
            {
                if (--nextBeat <= 0)
                {
                    float beats = MathHelper.Lerp(62f, 150f, MathHelper.Clamp((pressing - 0.05f) / 0.95f, 0f, 1f));
                    nextBeat = Math.Max(1, (int)(3600f / beats));

                    SoundEngine.PlaySound(VastOceanWeight.Heartbeat with
                    {
                        Volume = MathHelper.Lerp(0.3f, 0.95f, pressing),
                        Pitch = MathHelper.Lerp(-0.12f, 0.1f, pressing),
                    });

                    Beat = 1f;
                }
            }
            else
            {
                // The first beat at once, the next time.
                nextBeat = 0;
            }
        }
    }

    /// <summary>
    /// The dark closing in at the edges of the screen as the water presses: under everything of
    /// the interface, over the world, in screen pixels. The picture is made once, by formula, on
    /// the game's thread the first time it is drawn: clear in the middle, darkening to the edges
    /// and darkest in the corners; drawn bigger than the screen when it presses a little (only
    /// its corners showing), down to the screen's size when it presses hardest.
    /// </summary>
    public class VastOceanWeightSystem : ModSystem
    {
        private const int EdgeSize = 128;

        private static readonly Color DeepBlue = new Color(0, 6, 18);
        private static readonly Color DarkRed = new Color(46, 0, 6);

        private static Texture2D edge;

        public override void Unload()
        {
            Texture2D made = edge;
            edge = null;

            if (made != null)
                Main.QueueMainThreadAction(() => made.Dispose());
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (Main.gameMenu || Main.LocalPlayer.GetModPlayer<VastOceanWeightPlayer>().Shown < 0.004f)
                return;

            layers.Insert(0, new LegacyGameInterfaceLayer("PisAndDag: The Water's Weight", () =>
            {
                Draw(Main.spriteBatch);
                return true;
            }, InterfaceScaleType.None));
        }

        private static void Draw(SpriteBatch batch)
        {
            VastOceanWeightPlayer weight = Main.LocalPlayer.GetModPlayer<VastOceanWeightPlayer>();

            if (weight.Shown < 0.004f)
                return;

            edge ??= MakeEdge();

            float squeeze = MathHelper.Clamp(weight.Shown * (1f + 0.07f * weight.Beat), 0f, 1f);
            float scale = MathHelper.Lerp(2.1f, 1f, squeeze);
            int wide = (int)(Main.screenWidth * scale);
            int high = (int)(Main.screenHeight * scale);
            Rectangle to = new Rectangle((Main.screenWidth - wide) / 2, (Main.screenHeight - high) / 2, wide, high);
            Color tint = Color.Lerp(DeepBlue, DarkRed, weight.ShownRed) * MathHelper.Lerp(0.55f, 0.95f, squeeze);

            batch.Draw(edge, to, tint);
        }

        /// <summary>White, premultiplied, its alpha rising from nothing inside 0.42 of the way to an edge's middle to all at 1.18 (past the corners' 1.41, all).</summary>
        private static Texture2D MakeEdge()
        {
            Color[] data = new Color[EdgeSize * EdgeSize];

            for (int y = 0; y < EdgeSize; y++)
            {
                for (int x = 0; x < EdgeSize; x++)
                {
                    float dx = (x + 0.5f) / EdgeSize * 2f - 1f;
                    float dy = (y + 0.5f) / EdgeSize * 2f - 1f;
                    float t = MathHelper.Clamp(((float)Math.Sqrt(dx * dx + dy * dy) - 0.42f) / (1.18f - 0.42f), 0f, 1f);
                    float a = t * t * (3f - 2f * t);
                    byte v = (byte)Math.Round(a * a * 255f);

                    data[y * EdgeSize + x] = new Color(v, v, v, v);
                }
            }

            Texture2D made = new Texture2D(Main.instance.GraphicsDevice, EdgeSize, EdgeSize);
            made.SetData(data);
            return made;
        }
    }

    /// <summary>
    /// What an implosion looks like (<see cref="VastOceanWeight"/>): the water rushing in on the
    /// spot from all round, slow and then fast as it closes, a flash where it meets, and what air
    /// there was going up in a burst of bubbles. The game's own dust (sapphire and diamond motes,
    /// drawn at full brightness, so it shows in the dark of the deep; the breathing bubbles), on
    /// each machine that sees it; none on a server.
    /// </summary>
    public class VastOceanImplosion : ModSystem
    {
        /// <summary>Ticks the water takes to close.</summary>
        private const int Closing = 16;

        /// <summary>How far round the spot it starts to close from, for a size of 1: six tiles.</summary>
        private const float Reach = 96f;

        private struct Implosion
        {
            public Vector2 At;
            public float Size;
            public int Tick;
        }

        private static readonly List<Implosion> going = new();

        /// <summary>The sea closing on this spot, about something of this size (1 for something some 24 pixels across, a player 1.25).</summary>
        public static void Begin(Vector2 at, float size)
        {
            if (Main.dedServ)
                return;

            going.Add(new Implosion { At = at, Size = MathHelper.Clamp(size, 0.5f, 3f), Tick = 0 });
        }

        public override void ClearWorld() => going.Clear();

        public override void Unload() => going.Clear();

        public override void PostUpdateDusts()
        {
            for (int i = going.Count - 1; i >= 0; i--)
            {
                Implosion one = going[i];

                Step(one);

                if (++one.Tick > Closing)
                    going.RemoveAt(i);
                else
                    going[i] = one;
            }
        }

        private static void Step(Implosion one)
        {
            float size = one.Size;

            if (one.Tick < Closing)
            {
                // The ring closing on a curve, 1 - t squared: slow at first, then fast.
                float t = one.Tick / (float)Closing;
                float next = (one.Tick + 1) / (float)Closing;
                float radius = Reach * size * (1f - t * t);
                float closes = Reach * size * (next * next - t * t);
                int motes = (int)(6f + 6f * size);

                for (int k = 0; k < motes; k++)
                {
                    Vector2 way = Main.rand.NextVector2Unit(0f, MathHelper.TwoPi);
                    float from = radius * Main.rand.NextFloat(0.85f, 1.15f);
                    float speed = Math.Min(closes * 1.3f + 0.8f, from / 3f + 0.5f);

                    Dust mote = Dust.NewDustPerfect(one.At + way * from, Main.rand.NextBool(3) ? DustID.GemDiamond : DustID.GemSapphire,
                        -way * speed, 0, default, MathHelper.Lerp(1.3f, 0.8f, t) * Math.Min(1.4f, size));
                    mote.noGravity = true;
                }

                Lighting.AddLight(one.At, new Vector3(0.15f, 0.3f, 0.45f) * (t * size));
                return;
            }

            // Met: a flash, motes thrown back out, and the bubbles going up.
            for (int k = 0; k < (int)(14f * size); k++)
            {
                Dust flash = Dust.NewDustPerfect(one.At, DustID.GemDiamond, Main.rand.NextVector2Circular(6f, 6f) * size, 0, default, 1.4f);
                flash.noGravity = true;
            }

            for (int k = 0; k < (int)(16f * size); k++)
                Dust.NewDustPerfect(one.At + Main.rand.NextVector2Circular(10f, 10f) * size, DustID.BreatheBubble, Vector2.Zero, 0, default, Main.rand.NextFloat(0.9f, 1.5f));

            Lighting.AddLight(one.At, new Vector3(0.6f, 0.9f, 1.2f) * size);
        }
    }
}
