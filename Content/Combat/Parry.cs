using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using PisAndDag.Content.NPCs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// The parry. A player who starts one (ParryPlayer.Begin: a weapon does, on its right
    /// click) winds for a few ticks, and then for a few more whatever can be parried and comes
    /// at their front is turned aside instead of hitting them; after that they are open for a
    /// while. Too early or too late and it does nothing (ParryTiming has the ticks).
    ///
    /// WHAT CAN BE PARRIED. Most things, by what they are (ParryRules has the rules).
    /// Shots: solid things shot or thrown (arrows, javelins, bones, knives, stingers, spikes,
    /// darts, bullets), hostile and with damage. Enemies: anything that hurts by touch and is
    /// not a boss or a part of one, a shot in an enemy's place, a bomb, a ghost, beyond being
    /// hurt, or a giant. Nothing that wears the yellow star (CCBypass). And what the mod, the
    /// config or another mod says: <see cref="AddProjectile"/>, <see cref="AddNPC"/> (which
    /// can say when, and what being parried does: a boss's one move), ParryConfig,
    /// <see cref="Call"/>.
    ///
    /// WHAT IT DOES. A shot is destroyed. An enemy's hit is turned aside, and it takes the
    /// blow of the parry: the damage of the weapon that parried, never a critical hit, done
    /// on the parrier's machine as any hit of theirs is. It is pushed back (every one, whether
    /// or not the game lets it be knocked back). And every so many parries of it, it is
    /// staggered for <see cref="StaggerTicks"/>: every <see cref="FlyingParries"/>nd of
    /// something that flies, every <see cref="GroundParries"/>rd of anything else (the count
    /// does not run down, as Elden Ring's does not, and starts over at each stagger); never
    /// if its AI cannot be stopped. Staggered, the first hit on it
    /// does <see cref="RiposteDamage"/> times its damage and <see cref="RiposteKnockback"/>
    /// times its knockback, without a critical hit, and wakes it (PADStaggerNPC). A giant
    /// is not turned aside: its hit lands, and so does the parry's blow on it. A flying
    /// weapon's stagger is its own: thrown back the way it came and left spinning, its
    /// state for having been hit, open to the riposte while it spins.
    ///
    /// WHO DECIDES. The machine of the player who parried, as it decides everything else that
    /// hits them. In multiplayer it tells the server what it turned aside (PADPacket.Parry);
    /// the server does it to the thing and has everyone else see the spark.
    /// </summary>
    public static class Parry
    {
        /// <summary>How fast a parried flying weapon leaves, in pixels a tick. It launches itself at 9.</summary>
        public const float ThrowSpeed = 8f;

        /// <summary>
        /// Ticks a parried enemy is staggered: 2 seconds, by request. (No guide to Elden Ring
        /// gives its own figure; they say "a significant amount of time".)
        /// </summary>
        public const int StaggerTicks = 120;

        /// <summary>How far every parried enemy is pushed back, in pixels: six tiles.</summary>
        public const float PushPixels = 96f;

        /// <summary>
        /// How many parries of an enemy it takes to stagger it, by request: every second one of
        /// something that flies (no weight: NPC.noGravity, as it is at that parry), every third
        /// of anything else.
        /// </summary>
        public const int FlyingParries = 2;
        public const int GroundParries = 3;

        /// <summary>The first hit on a staggered enemy: this many times its damage, this many times its knockback, no critical hit.</summary>
        public const float RiposteDamage = 3f;
        public const float RiposteKnockback = 4f;

        /// <summary>
        /// How far from a player the server will take their word for a parry, in pixels. The
        /// arc is 48 from their middle; the rest is for what moved while the message did.
        /// </summary>
        private const float Believable = 480f;

        private const byte OfProjectile = 0;
        private const byte OfNPC = 1;

        // ---- what the mod itself says ------------------------------------------------------------

        private sealed class Answer
        {
            public Func<NPC, bool> When;
            public Action<NPC, Player, Vector2, bool> Thrown;
            public bool? Flies;
            public bool Reels = true;
            public string Told;
        }

        private sealed class Shot
        {
            public Func<Projectile, bool> When;
            public Action<Projectile, Player> Turned;
        }

        private static readonly Dictionary<int, Shot> projectiles = new Dictionary<int, Shot>();
        private static readonly Dictionary<int, Answer> npcs = new Dictionary<int, Answer>();

        /// <summary>
        /// This type of projectile can be parried: while it is hostile and has damage, or,
        /// given <paramref name="when"/>, exactly while that says so (for one whose hit is the
        /// mod's own work and not the game's). <paramref name="turned"/> is what being parried
        /// does to it, run on the machine of whoever parried it and where the world is decided;
        /// without one it is destroyed. For SetStaticDefaults.
        /// </summary>
        public static void AddProjectile(int type, Func<Projectile, bool> when = null, Action<Projectile, Player> turned = null)
        {
            projectiles[type] = new Shot { When = when, Turned = turned };
        }

        /// <summary>
        /// This type of NPC can be parried whatever it is: always, or only while
        /// <paramref name="when"/> says so (a boss in the moves of its that can be).
        /// <paramref name="thrown"/> is what being parried does to it, run where it is
        /// simulated (the server, or the one machine there is), given who parried it, the way
        /// from them to it, and whether this parry staggers it; without one it is pushed back,
        /// and staggered if it is to be. Either way it takes the parry's blow.
        /// <paramref name="flies"/> says which count it is staggered by, for one whose own
        /// weight does not say (a rider carried with no weight of her own); left out, its
        /// weight says. <paramref name="reels"/> false: no parry ever staggers it, and no
        /// count is kept. <paramref name="told"/>: what a parry does to it, in words, for
        /// /parry, when the words for every other enemy would be wrong of it. For
        /// SetStaticDefaults.
        /// </summary>
        public static void AddNPC(int type, Func<NPC, bool> when = null, Action<NPC, Player, Vector2, bool> thrown = null, bool? flies = null,
            bool reels = true, string told = null)
        {
            npcs[type] = new Answer { When = when, Thrown = thrown, Flies = flies, Reels = reels, Told = told };
        }

        // ---- what the config and other mods say ---------------------------------------------------

        /// <summary>
        /// Five lists of types: enemies never parried; parried whatever they are (their size and
        /// their rank set aside: light or heavy by their weight alone); never staggered; shots
        /// that are solid; and shots that never are.
        /// </summary>
        internal sealed class Lists
        {
            public readonly HashSet<int> Never = new HashSet<int>();
            public readonly HashSet<int> Anyway = new HashSet<int>();
            public readonly HashSet<int> NoStagger = new HashSet<int>();
            public readonly HashSet<int> Solid = new HashSet<int>();
            public readonly HashSet<int> NoShot = new HashSet<int>();

            public void Clear()
            {
                Never.Clear();
                Anyway.Clear();
                NoStagger.Clear();
                Solid.Clear();
                NoShot.Clear();
            }
        }

        /// <summary>The config's (ParryConfig fills it, and again whenever it changes), and other mods' (<see cref="Call"/>).</summary>
        internal static readonly Lists Chosen = new Lists();
        private static readonly Lists Called = new Lists();

        /// <summary>Whether the config or another mod says this type of enemy is never to be staggered by a parry.</summary>
        internal static bool NeverStaggers(int type)
        {
            return Chosen.NoStagger.Contains(type) || Called.NoStagger.Contains(type) || (npcs.TryGetValue(type, out Answer answer) && !answer.Reels);
        }

        /// <summary>
        /// Mod.Call, with the type as the second thing: ("ParryNPC", type) parries that enemy
        /// whatever its size or rank; ("ParryNoNPC", type) never parries it;
        /// ("ParryNoStagger", type) never staggers it; ("ParryProjectile", type) that shot is
        /// solid; ("ParryNoProjectile", type) it never is. True when taken, false when the type
        /// is no number, null when the word is none of these.
        /// </summary>
        public static object Call(object[] args)
        {
            if (args == null || args.Length < 2 || args[0] is not string what)
                return null;

            HashSet<int> list;

            switch (what.ToLowerInvariant())
            {
                case "parrynpc": list = Called.Anyway; break;
                case "parrynonpc": list = Called.Never; break;
                case "parrynostagger": list = Called.NoStagger; break;
                case "parryprojectile": list = Called.Solid; break;
                case "parrynoprojectile": list = Called.NoShot; break;
                default: return null;
            }

            try
            {
                list.Add(Convert.ToInt32(args[1]));
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        internal static void Forget()
        {
            projectiles.Clear();
            npcs.Clear();
            Chosen.Clear();
            Called.Clear();
            ParryRules.Forget();
        }

        // ---- what can be parried ---------------------------------------------------------------------

        public static bool Can(Projectile projectile)
        {
            if (!projectile.active)
                return false;

            int type = projectile.type;

            if (projectiles.TryGetValue(type, out Shot shot) && shot.When != null)
                return shot.When(projectile);

            if (!projectile.hostile || projectile.damage <= 0 || projectile.PAD().CCBypass)
                return false;

            if (Chosen.NoShot.Contains(type) || Called.NoShot.Contains(type))
                return false;

            return shot != null || Chosen.Solid.Contains(type) || Called.Solid.Contains(type) || ParryRules.Solid(projectile);
        }

        /// <summary>Whether a parry turns this enemy's hit aside as it is now. Not a giant's: see <see cref="Of(NPC)"/>.</summary>
        public static bool Can(NPC npc) => Of(npc) >= ParryClass.Heavy;

        /// <summary>What a parry does to this enemy as it is now.</summary>
        public static ParryClass Of(NPC npc) => Of(npc, out _);

        /// <summary>What a parry does to this enemy as it is now, and why.</summary>
        public static ParryClass Of(NPC npc, out ParryWhy why)
        {
            if (!npc.active || npc.life <= 0)
            {
                why = ParryWhy.NoThreat;
                return ParryClass.Never;
            }

            // Staggered already: a parry does nothing more to it. What comes next is the riposte,
            // and a parry is not one.
            if (npc.GetGlobalNPC<PADStaggerNPC>().Reeling)
            {
                why = ParryWhy.Reeling;
                return ParryClass.Never;
            }

            int type = npc.type;

            // The mod speaks for it: nothing else is asked but the star.
            if (npcs.TryGetValue(type, out Answer answer))
            {
                if (npc.PAD().CCBypass)
                {
                    why = ParryWhy.Star;
                    return ParryClass.Never;
                }

                if (answer.When != null && !answer.When(npc))
                {
                    why = ParryWhy.NotNow;
                    return ParryClass.Never;
                }

                return ParryRules.Weighed(npc, out why);
            }

            if (Chosen.Never.Contains(type) || Called.Never.Contains(type))
            {
                why = ParryWhy.Listed;
                return ParryClass.Never;
            }

            ParryClass found;

            if (Chosen.Anyway.Contains(type) || Called.Anyway.Contains(type))
            {
                if (npc.friendly || npc.damage <= 0)
                {
                    why = ParryWhy.NoThreat;
                    return ParryClass.Never;
                }

                found = ParryRules.Weighed(npc, out why);
            }
            else
            {
                found = ParryRules.Of(npc, out why);
            }

            if (found != ParryClass.Never && npc.PAD().CCBypass)
            {
                why = ParryWhy.Star;
                return ParryClass.Never;
            }

            return found;
        }

        /// <summary>
        /// What a parry does to this kind of enemy, by the game's own sample of it
        /// (ContentSamples): for the report, not for a parry. One the mod speaks for is light
        /// or heavy by its weight; when is for the moment.
        /// </summary>
        public static ParryClass OfKind(NPC sample, out ParryWhy why)
        {
            int type = sample.type;

            if (npcs.ContainsKey(type))
                return ParryRules.Weighed(sample, out why);

            if (Chosen.Never.Contains(type) || Called.Never.Contains(type))
            {
                why = ParryWhy.Listed;
                return ParryClass.Never;
            }

            if (Chosen.Anyway.Contains(type) || Called.Anyway.Contains(type))
            {
                if (sample.friendly || sample.damage <= 0)
                {
                    why = ParryWhy.NoThreat;
                    return ParryClass.Never;
                }

                return ParryRules.Weighed(sample, out why);
            }

            return ParryRules.Kind(sample, out why);
        }

        /// <summary>
        /// How many parries of this enemy stagger it, as it is now: <see cref="FlyingParries"/>
        /// if it flies (what the mod says of it, or else its having no weight),
        /// <see cref="GroundParries"/> if not.
        /// </summary>
        public static int StaggersEvery(NPC npc)
        {
            bool flies = npcs.TryGetValue(npc.type, out Answer answer) && answer.Flies.HasValue ? answer.Flies.Value : npc.noGravity;
            return flies ? FlyingParries : GroundParries;
        }

        /// <summary>Whether the mod itself speaks for this type of enemy (<see cref="AddNPC"/>).</summary>
        public static bool Spoken(int type) => npcs.ContainsKey(type);

        /// <summary>What a parry does to this type of enemy, in the mod's own words for it (<see cref="AddNPC"/>); nothing, for one the usual words are right of.</summary>
        public static string Told(int type) => npcs.TryGetValue(type, out Answer answer) ? answer.Told : null;

        /// <summary>One of vanilla's flying weapons, or a mod's that flies as they do, with nothing in the mod speaking for it.</summary>
        internal static bool FlyingWeapon(NPC npc) => npc.aiStyle == NPCAIStyleID.EnchantedSword && !npcs.ContainsKey(npc.type);

        // ---- on the machine of the player who parried ----------------------------------------

        /// <summary>This machine's player has parried a projectile, at this point.</summary>
        internal static void Turn(Projectile projectile, Player by, Vector2 at)
        {
            ParryEffects.Spark(at);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                Send(OfProjectile, projectile.identity, projectile.owner, at, -1, -1);

            // Done here at once, whoever's it is: on this machine it is this player's alone to
            // be hit by. The server does it for everyone else.
            End(projectile, by);
        }

        /// <summary>What being parried does to a projectile: its own answer, or the end of it.</summary>
        private static void End(Projectile projectile, Player by)
        {
            if (projectiles.TryGetValue(projectile.type, out Shot shot) && shot.Turned != null)
                shot.Turned(projectile, by);
            else
                projectile.Kill();
        }

        /// <summary>This machine's player has parried an NPC, at this point.</summary>
        internal static void Turn(NPC npc, Player by, Vector2 at)
        {
            ParryEffects.Clash(at, Away(npc, by));

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Throw(npc, by);
                return;
            }

            // What becomes of it is the server's to say: only it has the count of the parries
            // the thing has taken. It cannot touch this player meanwhile (ParryPlayer.Spare).
            Send(OfNPC, npc.whoAmI, 0, at, -1, -1);
        }

        /// <summary>The way from the player to the thing; the way they face, if it is right on top of them or straight over or under them.</summary>
        private static Vector2 Away(NPC npc, Player by)
        {
            Vector2 away = (npc.Center - by.Center).SafeNormalize(new Vector2(by.direction, 0f));

            if (Math.Abs(away.X) < 0.1f)
                away.X = 0.1f * by.direction;

            return away;
        }

        // ---- where the thing is simulated ------------------------------------------------------

        /// <summary>What being parried does to an NPC.</summary>
        private static void Throw(NPC npc, Player by)
        {
            if (!npc.active || npc.life <= 0)
                return;

            Vector2 away = Away(npc, by);
            PADStaggerNPC state = npc.GetGlobalNPC<PADStaggerNPC>();

            npcs.TryGetValue(npc.type, out Answer answer);

            // Every second parry of something that flies, every third of anything else.
            bool staggers = PADStaggerNPC.CanReel(npc) && state.TakeParry(StaggersEvery(npc));

            if (answer == null && npc.aiStyle == NPCAIStyleID.EnchantedSword && staggers)
            {
                // A flying weapon's stagger is its AI's own third state: 120 ticks of spinning
                // as it slows, then it launches itself again. What a hit does to it in flight,
                // with the count started over. Open to the riposte while it spins; its own AI
                // is what holds it.
                npc.ai[0] = 2f;
                npc.ai[1] = 0f;
                npc.velocity = away * ThrowSpeed;
                state.Reel(npc, StaggerTicks, false);
                npc.netUpdate = true;
                return;
            }

            if (answer != null && answer.Thrown != null)
            {
                answer.Thrown(npc, by, away, staggers);
                npc.netUpdate = true;
                return;
            }

            state.Push(npc, away, PushPixels);

            if (staggers)
                state.Reel(npc, StaggerTicks, true);

            npc.netUpdate = true;
        }

        /// <summary>On the server: a player says they parried something. Everywhere else: the spark of somebody's parry.</summary>
        internal static void HandlePacket(BinaryReader reader, int from)
        {
            byte kind = reader.ReadByte();
            int index = reader.ReadInt16();
            int owner = reader.ReadByte();
            Vector2 at = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            if (Main.netMode != NetmodeID.Server)
            {
                if (kind == OfNPC && index >= 0 && index < Main.maxNPCs && Main.npc[index].active)
                    ParryEffects.Clash(at, Main.npc[index].Center - at);
                else
                    ParryEffects.Spark(at);

                return;
            }

            if (from < 0 || from >= Main.maxPlayers)
                return;

            Player by = Main.player[from];

            if (!by.active || by.dead || !Near(at, by))
                return;

            if (kind == OfProjectile)
            {
                foreach (Projectile projectile in Main.ActiveProjectiles)
                {
                    if (projectile.identity != index || projectile.owner != owner)
                        continue;

                    if (Can(projectile) && Near(projectile.Center, by))
                    {
                        bool own = projectiles.TryGetValue(projectile.type, out Shot shot) && shot.Turned != null;

                        End(projectile, by);

                        // The server only tells the others of the end of its own.
                        if (!own && projectile.owner != Main.myPlayer)
                            NetMessage.SendData(MessageID.KillProjectile, -1, -1, null, index, owner);
                    }

                    break;
                }
            }
            else if (kind == OfNPC && index >= 0 && index < Main.maxNPCs)
            {
                NPC npc = Main.npc[index];

                if (Can(npc) && Near(npc.Center, by))
                    Throw(npc, by);
            }

            // The spark, for everyone but the one who made it. Whatever became of the thing
            // here: a parry seen on one screen is one that happened.
            Send(kind, index, owner, at, -1, from);
        }

        /// <summary>Within what the server will believe of this player. A point that is no number is not.</summary>
        private static bool Near(Vector2 point, Player player)
        {
            return Vector2.DistanceSquared(point, player.Center) <= Believable * Believable;
        }

        private static void Send(byte kind, int index, int owner, Vector2 at, int to, int ignore)
        {
            ModPacket packet = ModContent.GetInstance<ParrySystem>().Mod.GetPacket();
            packet.Write((byte)PADPacket.Parry);
            packet.Write(kind);
            packet.Write((short)index);
            packet.Write((byte)owner);
            packet.Write(at.X);
            packet.Write(at.Y);
            packet.Send(to, ignore);
        }

        // ---- the spark -------------------------------------------------------------------------

        /// <summary>
        /// What a parry looks and sounds like, at the point it happened: steel on steel, a star
        /// of light, shards thrown off it. Nothing on a server. (ParryEffects has all of it.)
        /// </summary>
        public static void Spark(Vector2 at) => ParryEffects.Spark(at);
    }

    /// <summary>
    /// The parry's lists: what ammunition shoots is found once everything is loaded, the
    /// config's are read then (they name other mods' things, which are not there before), and
    /// all of it is let go of when the mod unloads.
    /// </summary>
    public class ParrySystem : ModSystem
    {
        public override void PostSetupContent()
        {
            ParryRules.Learn();
            ModContent.GetInstance<ParryConfig>()?.Read();

            int light = 0, heavy = 0, giant = 0, never = 0;

            foreach (KeyValuePair<int, NPC> sample in ContentSamples.NpcsByNetId)
            {
                if (sample.Key <= 0 || sample.Value == null || sample.Value.friendly || (sample.Value.damage <= 0 && !Parry.Spoken(sample.Key)))
                    continue;

                switch (Parry.OfKind(sample.Value, out _))
                {
                    case ParryClass.Light: light++; break;
                    case ParryClass.Heavy: heavy++; break;
                    case ParryClass.Giant: giant++; break;
                    default: never++; break;
                }
            }

            Mod.Logger.Info($"Parry: of {light + heavy + giant + never} kinds of enemy that hurt by touch, {light + heavy} are turned aside by a parry " +
                $"({heavy} of them the game does not let be knocked back), {giant} are too big to turn aside, {never} cannot be parried. /parry list writes them all here.");
        }

        public override void Unload()
        {
            Parry.Forget();
        }
    }

    /// <summary>
    /// A player's side of the parry: the count of its ticks, and for the ticks it is active,
    /// the looking for what it turns aside. Only ever started, and only ever acted on, on that
    /// player's own machine.
    ///
    /// While it is active there is an arc in front of them: the part of a circle
    /// <see cref="Reach"/> pixels from their middle that is ahead of a line
    /// <see cref="Behind"/> pixels behind it, which is from 10 degrees past straight
    /// overhead, round the front, to 10 degrees past straight below. Once a tick, after they
    /// have moved, everything that can be parried, is ahead of that line, and is within the
    /// arc or will have come within it by the end of the tick, is turned aside: at the arc,
    /// if that is where it was met, or where it is, if it was already inside. And whatever
    /// can be parried and reaches the player themselves from ahead of that line is turned
    /// aside in place of the hit (vanilla asks: <see cref="CanBeHitByProjectile"/>,
    /// <see cref="CanBeHitByNPC"/>).
    ///
    /// An enemy has to be attacking: able to hit this player now (what vanilla itself asks:
    /// its own and every mod's CanHitPlayer, and the player not being one it leaves alone),
    /// and coming at them, its own speed toward them, when it is met at the arc; moving at
    /// all, vanilla's own rule for its shield parry (Player.CanParryAgainst), when it is on
    /// them already. One walked into is not parried. A flying weapon is the exception, as
    /// it was before there was a rule: it is parried wherever in the arc it is.
    ///
    /// What comes from behind is not turned aside, as with vanilla's own shield parry.
    /// Ahead is judged by where the thing's middle is before it moves, or was before the
    /// step that brought it (<see cref="Faces"/>), so that something fast from behind
    /// cannot step over the player's back and be found at their front.
    ///
    /// A hit the mod works out itself, which vanilla never asks about (a lance, a body run
    /// at them), asks here before it lands: <see cref="MeetsLine"/>, <see cref="MeetsBox"/>,
    /// <see cref="Takes"/>, and <see cref="Spares"/> for the ticks after.
    /// </summary>
    public class ParryPlayer : ModPlayer
    {
        /// <summary>How far from the player's middle the arc is, in pixels.</summary>
        public const float Reach = 48f;

        /// <summary>How far behind their middle the line is that the arc is ahead of.</summary>
        public const float Behind = 8f;

        /// <summary>Ticks an NPC that was parried cannot touch the player who parried it.</summary>
        public const int Spare = 45;

        /// <summary>The riposte, for the one who landed it: ticks the camera leans in, how far, how fast, the shake, the flash.</summary>
        private const int PunchTicks = 7;
        private const float PunchLean = 1.05f;
        private const float PunchRate = 0.45f;
        private const float PunchShake = 6f;
        private const float PunchFlash = 0.06f;

        private ParryTiming timing;

        /// <summary>Ticks since it began; less than none when there is none.</summary>
        private int elapsed = -1;

        private int facing = 1;

        /// <summary>What is parrying: the parry's blow is its damage. None, and the parry only turns things aside.</summary>
        private Item weapon;

        /// <summary>
        /// The giants this parry's blow has already landed on: once each. One for the machine,
        /// like the spared below: only its own player's parry ever lands a blow.
        /// </summary>
        private static readonly HashSet<int> struck = new HashSet<int>();

        private int punch;

        /// <summary>
        /// The NPCs this machine's player has parried: which they were, and the update they may
        /// touch them again on. One for the machine: only its own player is ever asked.
        /// </summary>
        private static readonly Dictionary<int, (int Type, uint Until)> spared = new Dictionary<int, (int Type, uint Until)>();

        /// <summary>A parry is under way: winding, active or recovering.</summary>
        public bool Busy => elapsed >= 0;

        /// <summary>It turns things aside this tick.</summary>
        public bool Active => elapsed >= timing.Startup && elapsed < timing.Startup + timing.Active;

        /// <summary>How many things this parry has turned aside.</summary>
        public int Landed { get; private set; }

        /// <summary>
        /// Starts a parry, facing this way, with this weapon: what it turns aside takes that
        /// weapon's damage. For the player's own machine.
        /// </summary>
        public void Begin(ParryTiming timing, int facing, Item weapon = null)
        {
            this.timing = timing;
            this.facing = facing < 0 ? -1 : 1;
            this.weapon = weapon;
            elapsed = 0;
            Landed = 0;
            struck.Clear();
        }

        /// <summary>Ends it where it is: whatever started it is gone.</summary>
        public void Cancel()
        {
            elapsed = -1;
        }

        /// <summary>A riposte of this player's has landed: the camera leans in, the screen shakes, a breath of light. Their own machine's.</summary>
        public void Punch()
        {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer)
                return;

            punch = PunchTicks;
            Player.PIS().BumpScreenShake(PunchShake, 10);
            Cinematic.Flash(ParryEffects.Steel, PunchFlash);
        }

        public override void OnEnterWorld()
        {
            spared.Clear();
        }

        public override void PreUpdate()
        {
            if (elapsed < 0)
                return;

            if (Player.dead || Player.CCed)
            {
                elapsed = -1;
                return;
            }

            elapsed++;

            if (elapsed >= timing.Total)
                elapsed = -1;
        }

        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (punch > 0)
            {
                punch--;

                // Not over a scene: whatever has the screen narrowed has the camera.
                if (!Cinematic.Framed)
                    Cinematic.Lean(PunchLean, PunchRate);
            }

            if (!Active)
                return;

            Vector2 middle = Player.Center;

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (!Parry.Can(projectile) || !Faces(projectile.Center))
                    continue;

                float size = Math.Max(projectile.width, projectile.height) * 0.5f;

                if (Nearest(middle, projectile.Center, projectile.velocity * (projectile.extraUpdates + 1), out Vector2 met) > Reach + size)
                    continue;

                // At the arc: only of what is there is it asked whether it could hit them.
                if (Threatens(projectile))
                    Turn(projectile, OnArc(met));
            }

            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (Spared(npc))
                    continue;

                ParryClass kind = Parry.Of(npc);

                if (kind == ParryClass.Never || !Faces(npc.Center))
                    continue;

                float size = Math.Max(npc.width, npc.height) * 0.5f;

                if (Nearest(middle, npc.Center, npc.velocity, out Vector2 met) > Reach + size)
                    continue;

                // At the arc: coming at them, and able to hit them now.
                if (!Coming(npc, middle) || !Threatens(npc))
                    continue;

                if (kind == ParryClass.Giant)
                    Blow(npc);
                else
                    Turn(npc, OnArc(met));
            }
        }

        /// <summary>
        /// Whether this NPC could hit this player by touch right now: what vanilla asks before
        /// it looks at whether the two are touching (Player.Update_NPCCollision), and that its
        /// touch is one that can be dodged at all.
        /// </summary>
        private bool Threatens(NPC npc)
        {
            if (npc.friendly || npc.damage <= 0 || Player.npcTypeNoAggro[npc.type] || !npc.IsDamageDodgeable())
                return false;

            int slot = -1;
            return NPCLoader.CanHitPlayer(npc, Player, ref slot);
        }

        /// <summary>The same of a shot: one the game works the hit of out itself is asked as the game asks; one the mod works out is the mod's to say.</summary>
        private bool Threatens(Projectile projectile)
        {
            if (!projectile.hostile)
                return true;

            return projectile.IsDamageDodgable() && ProjectileLoader.CanHitPlayer(projectile, Player);
        }

        /// <summary>Coming at them: its own speed is toward this point. A flying weapon is taken as coming wherever it is.</summary>
        private static bool Coming(NPC npc, Vector2 middle)
        {
            return Parry.FlyingWeapon(npc) || Vector2.Dot(npc.velocity, middle - npc.Center) > 0f;
        }

        /// <summary>
        /// Ahead of the line the arc is ahead of: in front of the player, or over or under
        /// them. Asked of where a thing's middle is before it moves this tick, or was before
        /// it moved.
        /// </summary>
        public bool Faces(Vector2 point)
        {
            return (point.X - Player.Center.X) * facing >= -Behind;
        }

        /// <summary>
        /// How near to a point something comes that is at <paramref name="from"/> and goes
        /// <paramref name="step"/> this tick, and where it is when it is nearest.
        /// </summary>
        internal static float Nearest(Vector2 point, Vector2 from, Vector2 step, out Vector2 at)
        {
            float length = step.X * step.X + step.Y * step.Y;
            float share = 0f;

            if (length > 0.0001f)
                share = MathHelper.Clamp(((point.X - from.X) * step.X + (point.Y - from.Y) * step.Y) / length, 0f, 1f);

            at = from + step * share;

            float x = at.X - point.X;
            float y = at.Y - point.Y;

            return (float)Math.Sqrt(x * x + y * y);
        }

        /// <summary>Where a parry of something met here shows: here, or on the arc if this is outside it.</summary>
        private Vector2 OnArc(Vector2 met)
        {
            Vector2 middle = Player.Center;

            float x = met.X - middle.X;
            float y = met.Y - middle.Y;
            float far = (float)Math.Sqrt(x * x + y * y);

            return far > Reach ? middle + new Vector2(x, y) * (Reach / far) : met;
        }

        private static bool Spared(NPC npc)
        {
            if (spared.Count == 0 || !spared.TryGetValue(npc.whoAmI, out (int Type, uint Until) spare))
                return false;

            // Still the one that was parried, and still within its ticks.
            if (spare.Type == npc.type && Main.GameUpdateCount < spare.Until)
                return true;

            spared.Remove(npc.whoAmI);
            return false;
        }

        private void Turn(Projectile projectile, Vector2 at)
        {
            Landed++;
            Player.PIS().BumpScreenShake(3f, 8);
            Parry.Turn(projectile, Player, at);
        }

        private void Turn(NPC npc, Vector2 at)
        {
            Landed++;
            spared[npc.whoAmI] = (npc.type, Main.GameUpdateCount + (uint)Spare);
            Player.PIS().BumpScreenShake(4.5f, 9);

            // The blow first, then what the parry does to it: the blow is no riposte.
            Strike(npc);
            Parry.Turn(npc, Player, at);
        }

        /// <summary>A giant met by the parry: not turned aside, but the blow lands, once a parry.</summary>
        private void Blow(NPC npc)
        {
            if (struck.Add(npc.whoAmI))
                Strike(npc);
        }

        /// <summary>
        /// The parry's blow: what the weapon that parried does, as this player has it, no
        /// more and no less (no critical hit, none of the game's variation), and no knockback
        /// of its own: the parry pushes it. A hit of this player's like any other: the game
        /// sends it, and everything that answers a hit answers it.
        /// </summary>
        private void Strike(NPC npc)
        {
            if (weapon == null || weapon.IsAir || npc.dontTakeDamage || npc.immortal)
                return;

            int damage = Player.GetWeaponDamage(weapon);

            if (damage <= 0)
                return;

            int direction = npc.Center.X < Player.Center.X ? -1 : 1;

            Player.ApplyDamageToNPC(npc, damage, 0f, direction, false, weapon.DamageType, false);
        }

        // ---- hits the mod works out itself -------------------------------------------------------

        /// <summary>Whether this NPC was parried by this machine's player so lately that it cannot touch them yet.</summary>
        public bool Spares(NPC npc)
        {
            return Player.whoAmI == Main.myPlayer && Spared(npc);
        }

        /// <summary>
        /// A line that is about to hurt this player (a lance, a blade), <paramref name="width"/>
        /// thick, <paramref name="from"/> the end it is held by: if a parry is active, the NPC
        /// can be parried, the line comes within the arc and its held end is ahead of the line
        /// behind the player, it is parried, and this is true: the hit is not to land.
        /// </summary>
        public bool MeetsLine(NPC npc, Vector2 from, Vector2 to, float width)
        {
            if (Player.whoAmI != Main.myPlayer || !Active || !Parry.Can(npc) || !Faces(from))
                return false;

            if (Nearest(Player.Center, from, to - from, out Vector2 met) > Reach + width * 0.5f)
                return false;

            Turn(npc, OnArc(met));
            return true;
        }

        /// <summary>
        /// A body that is about to hurt this player, going <paramref name="velocity"/> a tick:
        /// parried, and true, if a parry is active, the NPC can be parried, the body is within
        /// the arc and came from ahead of the line behind the player.
        /// </summary>
        public bool MeetsBox(NPC npc, Rectangle body, Vector2 velocity)
        {
            if (Player.whoAmI != Main.myPlayer || !Active || !Parry.Can(npc))
                return false;

            Vector2 middle = Player.Center;

            if (!Faces(body.Center.ToVector2() - velocity))
                return false;

            Vector2 met = Vector2.Clamp(middle, new Vector2(body.Left, body.Top), new Vector2(body.Right, body.Bottom));

            if (Vector2.Distance(met, middle) > Reach)
                return false;

            Turn(npc, OnArc(met));
            return true;
        }

        /// <summary>
        /// A shot whose hit the mod works out, about to land on this player from
        /// <paramref name="from"/>: parried, and true, if a parry is active and it can be.
        /// </summary>
        public bool Takes(Projectile projectile, Vector2 from)
        {
            if (Player.whoAmI != Main.myPlayer || !Active || !Parry.Can(projectile) || !Faces(from))
                return false;

            Turn(projectile, projectile.Center);
            return true;
        }

        // ---- hits the game works out ---------------------------------------------------------------

        /// <summary>Vanilla asks this once the projectile is on the player (Projectile.Damage).</summary>
        public override bool CanBeHitByProjectile(Projectile proj)
        {
            if (Player.whoAmI != Main.myPlayer || !Active || !Parry.Can(proj) || !proj.IsDamageDodgable() || !Faces(proj.Center - proj.velocity))
                return true;

            Turn(proj, proj.Center);
            return false;
        }

        /// <summary>
        /// Vanilla asks this of every hostile NPC every tick, before it has looked at whether
        /// the two are touching (Player.Update_NPCCollision): so that is looked at here, as
        /// vanilla is about to. It has already asked the NPC's own side (CanHitPlayer).
        /// </summary>
        public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
        {
            if (Player.whoAmI != Main.myPlayer)
                return true;

            if (Spared(npc))
                return false;

            if (!Active)
                return true;

            ParryClass kind = Parry.Of(npc);

            if (kind == ParryClass.Never || !Faces(npc.Center - npc.velocity))
                return true;

            // Walked into, not come at; one the game leaves this player alone by; a touch nothing dodges.
            if ((npc.velocity == Vector2.Zero && !Parry.FlyingWeapon(npc)) || Player.npcTypeNoAggro[npc.type] || !npc.IsDamageDodgeable())
                return true;

            Rectangle body = Player.Hitbox;
            Rectangle its = new Rectangle((int)(npc.position.X + npc.netOffset.X), (int)(npc.position.Y + npc.netOffset.Y), npc.width, npc.height);

            int slot = cooldownSlot;
            float damage = 1f;
            NPC.GetMeleeCollisionData(body, npc.whoAmI, ref slot, ref damage, ref its);

            if (!body.Intersects(its))
                return true;

            if (kind == ParryClass.Giant)
            {
                Blow(npc);
                return true;
            }

            Vector2 corner = new Vector2(its.Left, its.Top);
            Vector2 far = new Vector2(its.Right, its.Bottom);

            Turn(npc, Vector2.Clamp(Player.Center + new Vector2(facing * 10f, 0f), corner, far));
            return false;
        }
    }
}
