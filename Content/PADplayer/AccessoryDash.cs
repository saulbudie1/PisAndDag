using System;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Items.Accessories.Tentochi;
using PisAndDag.Content.Mimic;
using PisAndDag.Content.NPCs.Empress;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.PADplayer
{
    /// <summary>
    /// The dashes the mod's accessories give (by request): Exo Air I, 13 tiles; Exo Air II, 18; the Empress' Blessing, 20,
    /// with nothing able to hurt its wearer while it lasts ("have invincibility during that dash"). Begun as the game's
    /// are: a direction tapped twice within a quarter of a second.
    ///
    /// One at a time: the longest of those worn is the one used (<see cref="Worn"/>), and while one of them is worn the
    /// game's own (Tabi's, the Shield of Cthulhu's, the Solar Flare's, the Crystal Assassin's) is not: Player.dashType is
    /// put to 0 once everything worn has had its say.
    ///
    /// A dash covers its distance in its ticks unless something solid stops it: at one speed for the first half of them,
    /// then easing to the wearer's run, held level as the game's dashes are here (LevelDash.Hold). At its end the wearer is
    /// left running the way they went at their top speed. None begins on a mount, on a hook or a rope, frozen, webbed or
    /// stoned; one under way ends if any of those comes about, or if the wearer is stopped against something.
    ///
    /// Its way along is set before the game's own movement, as the game's own dashes set theirs (PostUpdateRunSpeeds), so
    /// whatever moves the player after it has the last word: the mod's holds (the Crevice Eel's jaws, the Meat Hook's
    /// chain, the Mantrap, the Empress's grab and her arena's walls, the Legends bosses' grabs). Seeing its way changed
    /// before the move (PreUpdateMovement), the dash gives way and ends; one begun into a hold never goes at all.
    ///
    /// Only the player at the keyboard begins their own. The server and the others in the world are told where it began
    /// and how it goes (PADPacket.Dash) and move the dasher along it as the dasher's machine does, and are told where it
    /// ended and how the dasher was left moving. Nothing about being hurt is decided anywhere but on the dasher's own
    /// machine, as the game decides it. The Everclear Mirror's copy wears what its owner wears and never dashes.
    ///
    /// Seen: the exoskeleton's in Tentochi's red, its jets burning back from the feet, the air behind it torn, the wearer
    /// split into red and cyan copies a little either side of them and the game's three after-images in those colours
    /// (TentochiFX); the Blessing's in the Empress's light, a ribbon of her colours laid behind, the wearer pale with it.
    /// </summary>
    public class AccessoryDash : ModPlayer
    {
        public enum Kind : byte { None, ExoAirI, ExoAirII, Empress }

        /// <summary>How far each carries, in tiles, by request.</summary>
        public static int Tiles(Kind kind) => kind switch { Kind.ExoAirI => 13, Kind.ExoAirII => 18, Kind.Empress => 20, _ => 0 };

        /// <summary>How many ticks each takes.</summary>
        public static int Ticks(Kind kind) => kind switch { Kind.ExoAirI => 14, Kind.ExoAirII => 16, Kind.Empress => 18, _ => 1 };

        /// <summary>The wait after each before another can begin, in ticks.</summary>
        public static int Rest(Kind kind) => kind switch { Kind.ExoAirI => 30, Kind.ExoAirII => 30, Kind.Empress => 75, _ => 0 };

        /// <summary>The Empress's dash: past its end, the ticks it still keeps its wearer from harm.</summary>
        public const int Grace = 6;

        /// <summary>The game's own window for the second tap, in ticks (Player.DoCommonDashHandle).</summary>
        public const int TapWindow = 15;

        /// <summary>The fastest any of them runs at its start, in pixels a tick: faster, and it outruns its own pictures.</summary>
        public const float TopSpeed = 26f;

        /// <summary>The best dash worn this tick, set by the accessories (<see cref="Wear"/>).</summary>
        public Kind Worn;

        /// <summary>The dash under way, None between them.</summary>
        public Kind Doing { get; private set; }

        /// <summary>Which way it goes: 1 right, -1 left.</summary>
        public int Direction { get; private set; } = 1;

        /// <summary>Ticks into it.</summary>
        public int Tick { get; private set; }

        /// <summary>Ticks until another can begin.</summary>
        public int Cooldown { get; private set; }

        /// <summary>Ticks since the last ended, for what it leaves behind to fade by.</summary>
        public int Since { get; private set; } = 999;

        /// <summary>The kind the last was.</summary>
        public Kind Last { get; private set; }

        public bool Dashing => Doing != Kind.None;

        /// <summary>Nothing can hurt them: the Empress's dash under way, or just ended.</summary>
        public bool Untouchable => Doing == Kind.Empress || safe > 0;

        private int safe;
        private float cruise;
        private float exit;
        private float lastX;

        /// <summary>The first tap of a double tap: which way (0 for none), and on which of the game's ticks.</summary>
        private int tapDir;
        private uint tapAt;

        /// <summary>The way along the dash gives them this tick, set before the game's own movement (PostUpdateRunSpeeds).</summary>
        private float set;

        /// <summary>Begun this tick: whether it goes is settled just before the move (PreUpdateMovement).</summary>
        private bool fresh;

        /// <summary>
        /// How far their way along may be from the dash's, by the move, before the dash takes it that something else is
        /// moving them (pixels a tick): more than the wind gives (Player.HorizontalMovement: a tenth at most), far less
        /// than any hold.
        /// </summary>
        private const float Overruled = 0.5f;

        /// <summary>For the accessories: this dash is worn. The longest worn wins.</summary>
        public void Wear(Kind kind)
        {
            if (kind > Worn)
                Worn = kind;
        }

        public override void ResetEffects()
        {
            Worn = Kind.None;
        }

        public override void PostUpdateEquips()
        {
            if (Worn != Kind.None)
                Player.dashType = 0;

            Guard();
        }

        public override void PreUpdate()
        {
            Guard();
        }

        /// <summary>While the Empress's dash keeps them, they are kept, before anything can reach them this tick.</summary>
        private void Guard()
        {
            if (!Untouchable)
                return;

            Player.immune = true;
            Player.immuneNoBlink = true;

            if (Player.immuneTime < 2)
                Player.immuneTime = 2;
        }

        public override bool FreeDodge(Player.HurtInfo info) => Untouchable;

        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !Untouchable;

        public override bool CanBeHitByProjectile(Projectile proj) => !Untouchable;

        /// <summary>Whether this player may begin a dash now: not on a mount, hook or rope, not held fast, not shimmering, alive.</summary>
        private bool Free => !Player.dead && !Player.mount.Active && Player.grappling[0] == -1 && !Player.pulley && !Player.frozen
            && !Player.webbed && !Player.stoned && !Player.shimmering && !Player.tongued && !Player.CCed;

        /// <summary>
        /// The taps (the dasher's own machine), looked for where the game looks for its own: after the run's speeds are set
        /// and before the keys pressed this tick are marked as held (Player.Update), so a tap still reads as one here. Then,
        /// on every machine, the dash's way along for this tick, set before the game's own movement (HorizontalMovement,
        /// its quickening and slowing held off this tick) as the game's own dashes set theirs.
        /// </summary>
        public override void PostUpdateRunSpeeds()
        {
            if (MimicSystem.Is(Player))
                return;

            if (Player.whoAmI == Main.myPlayer)
                Taps();

            if (!Dashing)
                return;

            set = Direction * (Tick < Ticks(Doing) ? SpeedAt(Tick) : exit);
            Player.velocity.X = set;
            Player.runAcceleration = 0f;
            Player.runSlowdown = 0f;
        }

        private void Taps()
        {
            if (Worn == Kind.None || Dashing || Cooldown > 0 || !Free)
                return;

            int way = Player.controlRight && Player.releaseRight ? 1 : Player.controlLeft && Player.releaseLeft ? -1 : 0;

            if (way == 0)
                return;

            // Counted on the game's own clock, which runs on while they are on a rope or a hook and this hook is not run:
            // a first tap there is old by the time they are off it.
            uint now = Main.GameUpdateCount;

            if (way == tapDir && now - tapAt < TapWindow)
            {
                tapDir = 0;
                Begin(way);
            }
            else
            {
                tapDir = way;
                tapAt = now;
            }
        }

        private void Begin(int direction)
        {
            Doing = Worn;
            Direction = direction;
            Tick = 0;
            fresh = true;
            lastX = Player.position.X;

            // Left running at their top speed as it will be this tick (the TopSpeed class's sum), once what slows them has
            // had its say (TopSpeedPlayer, after this).
            Plan(Doing, Math.Max(Player.maxRunSpeed, Player.accRunSpeed) * Player.GetModPlayer<TopSpeedPlayer>().Kept);
        }

        /// <summary>
        /// The dash's two speeds: <see cref="cruise"/> for the first half of its ticks, then easing in a straight line to
        /// <see cref="exit"/> (the wearer's run) by its last. Together exactly its distance, unless the run alone would
        /// cover it (then all at one speed).
        /// </summary>
        private void Plan(Kind kind, float run)
        {
            float distance = Tiles(kind) * 16f;
            int ticks = Ticks(kind);

            exit = MathHelper.Clamp(run, 0f, distance / ticks);
            cruise = distance / ticks;

            for (int pass = 0; pass < 4; pass++)
            {
                float sum = 0f;

                for (int k = 0; k < ticks; k++)
                    sum += Shape(k, ticks, exit / cruise);

                cruise = Math.Min(TopSpeed, distance / sum);
            }
        }

        /// <summary>The share of the cruising speed at tick k of n, ending at r.</summary>
        private static float Shape(int k, int n, float r)
        {
            float half = n * 0.5f;

            if (k < half)
                return 1f;

            float t = (k - half + 1f) / (n - half);
            return MathHelper.Lerp(1f, MathHelper.Clamp(r, 0f, 1f), t);
        }

        private float SpeedAt(int k) => cruise * Shape(k, Ticks(Doing), exit / cruise);

        /// <summary>
        /// Just before the move, after the game and everything else have had their say: whether the dash goes on (anything
        /// that has moved the player otherwise since its way was set has the last word, and it ends), and if it does, its
        /// way along held to the pixel and the player held level.
        /// </summary>
        public override void PreUpdateMovement()
        {
            if (!Dashing || MimicSystem.Is(Player))
                return;

            bool overruled = Math.Abs(Player.velocity.X - set) > Overruled;

            // Elsewhere it is followed, not decided: kept to its way unless something there moves them otherwise. Its end
            // there is the dasher's word (HandlePacket), or its time running out (PostUpdate).
            if (Player.whoAmI != Main.myPlayer)
            {
                if (!overruled && Tick < Ticks(Doing) && Free)
                {
                    Player.velocity.X = set;
                    Player.GetModPlayer<LevelDash>().Hold();
                }

                return;
            }

            if (fresh)
            {
                fresh = false;

                // Begun into a hold, or held fast since: it never went. Nothing seen, nothing to wait for.
                if (overruled || !Free)
                {
                    Doing = Kind.None;
                    return;
                }

                Seen(true);

                if (Main.netMode == NetmodeID.MultiplayerClient)
                    Send(Player.whoAmI, Word.Begin, Doing, Direction, Player.position, new Vector2(cruise, exit), -1, -1);
            }
            else
            {
                bool stopped = Tick > 1 && Math.Abs(Player.position.X - lastX) < 1f;

                if (Tick >= Ticks(Doing) || stopped || overruled || !Free)
                {
                    End(!stopped && !overruled && Free);
                    return;
                }
            }

            Player.velocity.X = set;
            Player.GetModPlayer<LevelDash>().Hold();
            lastX = Player.position.X;
        }

        /// <summary>The dasher's own end of it: left running the way they went (unless stopped or taken), and the others told where.</summary>
        private void End(bool running)
        {
            if (running)
                Player.velocity.X = Direction * exit;

            Kind kind = Doing;
            Finish();

            if (Main.netMode == NetmodeID.MultiplayerClient)
                Send(Player.whoAmI, Word.End, kind, Direction, Player.position, Player.velocity, -1, -1);
        }

        /// <summary>The dash over, on any machine: what it leaves behind (the Empress's grace, the wait before another) and how it looks ending.</summary>
        private void Finish()
        {
            if (Doing == Kind.Empress)
                safe = Grace;

            Cooldown = Rest(Doing);
            Seen(false);
            Last = Doing;
            Doing = Kind.None;
            Since = 0;
        }

        public override void PostUpdate()
        {
            if (Dashing)
            {
                Tick++;

                // On the others' machines, and on the Everclear Mirror's copy, it ends when it would, if the dasher's word
                // of its end has not come first.
                if ((Player.whoAmI != Main.myPlayer || MimicSystem.Is(Player)) && Tick > Ticks(Doing))
                    Finish();
                else if (!Main.dedServ)
                {
                    if (Doing == Kind.Empress)
                        TentochiFX.BlessingTick(Player, Direction, Tick);
                    else
                        TentochiFX.DashTick(Player, Direction, Tick, Doing == Kind.ExoAirII);
                }
            }
            else
            {
                if (Cooldown > 0)
                    Cooldown--;

                if (Since < 999)
                    Since++;
            }

            if (safe > 0 && !Dashing)
                safe--;

            // The game's three after-images, while the exoskeleton's dash goes and a little after (tinted in DrawEffects).
            if (Doing is Kind.ExoAirI or Kind.ExoAirII || (Last is Kind.ExoAirI or Kind.ExoAirII && Since < 10))
                Player.armorEffectDrawShadow = true;
        }

        public override void OnRespawn()
        {
            Doing = Kind.None;
            fresh = false;
            tapDir = 0;
            safe = 0;
            Cooldown = 0;
        }

        /// <summary>What it looks like and sounds like, beginning or ending, on every machine that sees it.</summary>
        private void Seen(bool beginning)
        {
            if (Main.dedServ)
                return;

            if (Doing == Kind.Empress)
            {
                if (beginning)
                    TentochiFX.BlessingStart(Player, Direction);
                else
                    TentochiFX.BlessingStop(Player, Direction);
            }
            else if (beginning)
                TentochiFX.DashStart(Player, Direction, Doing == Kind.ExoAirII);
            else
                TentochiFX.DashStop(Player, Direction);
        }

        // ---- the look ------------------------------------------------------------------------------------------------

        /// <summary>Which copy of them is being drawn now: 0 the player, 1 the red split, 2 the cyan.</summary>
        private static int drawingSplit;

        /// <summary>How far apart the split copies are now, in world pixels: wide as a dash begins, closing after it.</summary>
        private float SplitWidth()
        {
            if (Doing is Kind.ExoAirI or Kind.ExoAirII)
                return 3f + 3f * (1f - Tick / (float)Ticks(Doing));

            if (Last is Kind.ExoAirI or Kind.ExoAirII && Since < 10)
                return 3f * (1f - Since / 10f);

            return 0f;
        }

        /// <summary>
        /// The split copies, drawn before the game draws the player (inside its own batch, LegacyPlayerRenderer.DrawPlayerFull),
        /// so they lie under the player: one a few pixels behind them in red, one ahead in cyan.
        /// </summary>
        public override void DrawPlayer(Camera camera)
        {
            float width = SplitWidth();

            if (width <= 0.5f || Player.invis || Player.dead || Player.stoned || drawingSplit != 0)
                return;

            Vector2 at = Player.position + new Vector2(0f, Player.gfxOffY);
            int dir = Doing != Kind.None ? Direction : Player.direction;

            try
            {
                drawingSplit = 1;
                Main.PlayerRenderer.DrawPlayer(camera, Player, at - new Vector2(dir * width, 0f), Player.fullRotation, Player.fullRotationOrigin, 0.45f);
                drawingSplit = 2;
                Main.PlayerRenderer.DrawPlayer(camera, Player, at + new Vector2(dir * width * 0.6f, 0f), Player.fullRotation, Player.fullRotationOrigin, 0.55f);
            }
            finally
            {
                drawingSplit = 0;
            }
        }

        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (drawingSplit == 1)
            {
                r = TentochiFX.Red.R / 255f;
                g = TentochiFX.Red.G / 255f;
                b = TentochiFX.Red.B / 255f;
                fullBright = true;
                return;
            }

            if (drawingSplit == 2)
            {
                r = TentochiFX.Cyan.R / 255f;
                g = TentochiFX.Cyan.G / 255f;
                b = TentochiFX.Cyan.B / 255f;
                fullBright = true;
                return;
            }

            // The game's after-images (shadow 0.5, 0.7, 0.9), red, cyan, red, while the exoskeleton's dash shows them.
            if (drawInfo.shadow > 0f && (Doing is Kind.ExoAirI or Kind.ExoAirII || (Last is Kind.ExoAirI or Kind.ExoAirII && Since < 10)))
            {
                Color tint = drawInfo.shadow < 0.6f ? TentochiFX.Red : drawInfo.shadow < 0.8f ? TentochiFX.Cyan : TentochiFX.Red;
                r = tint.R / 255f;
                g = tint.G / 255f;
                b = tint.B / 255f;
                fullBright = true;
                return;
            }

            // In her dash the wearer is light: pale, lit through, in her colours.
            if (Doing == Kind.Empress && drawInfo.shadow == 0f)
            {
                Color tint = Color.Lerp(Color.White, EmpressArt.Prism(Tick / 18f), 0.45f);
                r = tint.R / 255f;
                g = tint.G / 255f;
                b = tint.B / 255f;
                a = 0.75f;
                fullBright = true;
            }
        }

        /// <summary>
        /// The exoskeleton's dash tears the wearer's picture as it begins: for a few ticks, some of its pieces drawn a
        /// pixel or two to one side, as a picture slips where its signal drops.
        /// </summary>
        public override void TransformDrawData(ref PlayerDrawSet drawInfo)
        {
            if (drawingSplit != 0 || drawInfo.shadow != 0f || !(Doing is Kind.ExoAirI or Kind.ExoAirII) || Tick > 6)
                return;

            int seed = (int)(Main.GameUpdateCount / 2) * 7919 + Player.whoAmI;

            for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
            {
                seed = seed * 1103515245 + 12345;

                if (((seed >> 16) & 3) != 0)
                    continue;

                DrawData data = drawInfo.DrawDataCache[i];
                data.position.X += ((seed >> 20) & 1) == 0 ? -2f : 2f;
                drawInfo.DrawDataCache[i] = data;
            }
        }

        // ---- the others' machines ------------------------------------------------------------------------------------

        /// <summary>The dasher's two words: where it began and how it goes (its two speeds), and where it ended and how they were left moving.</summary>
        private enum Word : byte { Begin, End }

        /// <summary>Sent by the dasher to the server, and on by the server to the rest: who, which word, what kind, which way, where, and the two numbers.</summary>
        private static void Send(int who, Word word, Kind kind, int direction, Vector2 at, Vector2 numbers, int to, int except)
        {
            ModPacket packet = ModContent.GetInstance<PisAndDag>().GetPacket();
            packet.Write((byte)PADPacket.Dash);
            packet.Write((byte)who);
            packet.Write((byte)word);
            packet.Write((byte)kind);
            packet.Write((sbyte)direction);
            packet.WriteVector2(at);
            packet.WriteVector2(numbers);
            packet.Send(to, except);
        }

        internal static void HandlePacket(BinaryReader reader, int whoAmI)
        {
            int who = reader.ReadByte();
            Word word = (Word)reader.ReadByte();
            Kind kind = (Kind)reader.ReadByte();
            int direction = reader.ReadSByte() < 0 ? -1 : 1;
            Vector2 at = reader.ReadVector2();
            Vector2 numbers = reader.ReadVector2();

            if (Main.netMode == NetmodeID.Server)
                who = whoAmI;

            if (who < 0 || who >= Main.maxPlayers || word > Word.End || kind == Kind.None || kind > Kind.Empress)
                return;

            if (!float.IsFinite(at.X) || !float.IsFinite(at.Y) || !float.IsFinite(numbers.X) || !float.IsFinite(numbers.Y))
                return;

            if (word == Word.Begin && !(numbers.X > 0f && numbers.X <= TopSpeed && numbers.Y >= 0f && numbers.Y <= numbers.X))
                return;

            Player player = Main.player[who];

            if (!player.active || who == Main.myPlayer)
                return;

            AccessoryDash dash = player.GetModPlayer<AccessoryDash>();

            if (word == Word.Begin)
            {
                if (dash.Dashing)
                    dash.Finish();

                dash.Doing = kind;
                dash.Direction = direction;
                dash.Tick = 0;
                dash.fresh = false;
                dash.cruise = numbers.X;
                dash.exit = numbers.Y;
                player.position = at;
                dash.lastX = at.X;
                dash.Seen(true);
            }
            else
            {
                if (dash.Dashing)
                    dash.Finish();

                player.position = at;
                player.velocity = numbers;
            }

            if (Main.netMode == NetmodeID.Server)
                Send(who, word, kind, direction, at, numbers, -1, whoAmI);
        }
    }
}
