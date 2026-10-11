using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Items;
using PisAndDag.Content.Particles;
using PisAndDag.Content.Projectiles.Base;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// What reaches them from his second phase on, wherever they have got to. By request:
    /// "he has a meat hook on his bottom hand", "we need more projectile on phase 2-3 ...
    /// player can js dodge by using hooks and rocket boots and wings", and, of what was
    /// offered and picked: pale wind cuts in his second phase, knife volleys, a hook that
    /// does both by where they are, and reading what they do.
    ///
    /// THE WIND (his second phase only; his third has what the sword burns with). Two
    /// cuts where he stands, down and up, each let go of as a cut of wind (FoighSlash,
    /// its pale kind) at them wherever they are: half his sword's blow, and no wound.
    ///
    /// THE HOOK (from his second phase). In his bottom hand from the moment he takes it
    /// out, in the passage into his second phase, and says nothing of it. Thrown
    /// (HookCast): fast and straight at where they will be, fifty tiles of chain. What it
    /// bites it holds:
    ///
    ///   one stood on the ground   he is pulled along his own chain to them (Haul),
    ///                             and the sword comes down as he gets there (Reap)
    ///   one off the ground        in the air, hanging from a hook of their own, up on
    ///                             the ceiling: they are pulled in to him (Winch, and
    ///                             FoighGripPlayer.Drag on their own machine), and the
    ///                             sword comes down as they get there (Reap)
    ///
    /// Met by a parry, or gone against a wall, or run out: he takes it in again
    /// (HookMiss), open for as long.
    ///
    /// WHAT HE READS (from his second phase): four things their own machine sees them do
    /// and tells the server (Word.Read), each no oftener than every two and a half
    /// seconds, and answered as often as he answers anything (two times in three; four
    /// in five in his third phase), while he is free to (walking, or with his sword up):
    ///
    ///   a swing that missed him from near       a flash step to in front of them, and
    ///   something held on him for 3/4 second    the sword (as he steps out of the way of
    ///   a reload begun (the mod's magazines)    a spell); at one off the ground, the hook
    ///   a hook of theirs biting                 the hook
    ///
    /// WHO DECIDES. The server what he does. Each player's machine whether the hook has
    /// bitten them, where they were then, and their own way in along the chain.
    /// </summary>
    public partial class Foigh
    {
        // ============================================================
        // NUMBERS
        // ============================================================

        /// <summary>The hook's blow, through armour, the same in every world: half a knife's.</summary>
        public const int HookDamage = 50;

        /// <summary>
        /// The wind: how likely he is to begin it, beside what else he may pick (Choose),
        /// up close, from further off, and at one over his head; and how far off he will
        /// begin it from.
        /// </summary>
        private const float GaleNear = 0.8f;
        private const float GaleFar = 2f;
        private const float GaleAbove = 2.5f;
        private const float GaleReach = 900f;

        /// <summary>
        /// The hook: how likely, from further off along the ground (a way in), at one who
        /// is off it, and at one over his head. Not from nearer than this (there he has
        /// his sword), nor from further than its chain goes, less a little.
        /// </summary>
        private const float HookFar = 1.4f;
        private const float HookAloft = 3f;
        private const float HookAbove = 3.5f;
        private const float HookLeast = 140f;
        private const float HookMost = FoighHook.Speed * FoighHook.Life - 60f;

        /// <summary>Knives are thrown more often than they were, from his second phase: from further off, and at one over his head.</summary>
        private const float KnivesFar = 1.6f;
        private const float KnivesAbove = 2.5f;

        /// <summary>
        /// Pulled along his chain: pixels a tick (as fast as tiles can stop), how near
        /// their middle he lets go of it, and the longest it lasts.
        /// </summary>
        private const float HaulSpeed = 15f;
        private const float HaulReach = 78f;
        private const int HaulLongest = 50;

        /// <summary>
        /// Pulled in to him: to this far ahead of his middle and this far over it; the
        /// blow is begun when they are this near that place (they come at
        /// FoighGripPlayer.DragSpeed, fifteen pixels a tick, and the blade is down ten
        /// ticks after); and the longest he pulls.
        /// </summary>
        private const float WinchTo = 58f;
        private const float WinchUp = 6f;
        private const float WinchCuts = 132f;
        private const int WinchLongest = 70;

        /// <summary>
        /// What he reads: told no oftener than this by one machine, and taken no oftener
        /// than half of it by him; a swing counts as missed from no further off than
        /// this; a thing is held on him after this many ticks of it, from no nearer
        /// than this (nearer, he has his sword for whoever holds it); and he sees a
        /// reload, a hook or a thing held from no further off than this.
        /// </summary>
        private const int ReadEvery = 150;
        private const float WhiffWithin = 240f;
        private const int HeldTicks = 45;
        private const float HeldFrom = 200f;
        private const float ReadSees = 1100f;

        /// <summary>What was read (Word.Read's number).</summary>
        private const int ReadSwing = 1;
        private const int ReadHeld = 2;
        private const int ReadReload = 3;
        private const int ReadGrapple = 4;

        // ============================================================
        // SENT WITH HIM
        // ============================================================

        /// <summary>The player on his hook, or none.</summary>
        private int hooked = -1;

        // ---- where the world is decided only ----

        /// <summary>The update of the game he last took something read of them.</summary>
        private uint readAt;

        // ---- each machine's own ----

        /// <summary>Where his hook was when it was last gone from the air: where the chain comes back from.</summary>
        private Vector2 hookFell;

        /// <summary>Ticks before this machine tells the server of anything read again.</summary>
        private int readWait;

        /// <summary>This machine's player: the swing they are in (ticks left of it a tick ago), and whether anything of theirs has landed on him, or been met or turned aside by him, in it.</summary>
        private int swingWas;
        private bool swingLanded;

        /// <summary>Ticks they have held something on him; whether a hook of theirs was in something a tick ago; whether they were reloading.</summary>
        private int heldFor;
        private bool grappleWas;
        private bool reloadWas;

        /// <summary>The update of the game in which one of him was last seen fighting past his first phase; none yet at nothing.</summary>
        private static uint laterSeen;

        /// <summary>
        /// One of him is here, past his first phase: from then a hit of his lets go
        /// their hook and shuts their wings (FoighGripPlayer.Struck). True for two
        /// updates after he was last seen so, on every machine by itself: a player's
        /// update comes before his.
        /// </summary>
        public static bool PastFirst => laterSeen != 0 && Main.GameUpdateCount - laterSeen <= 2;

        /// <summary>Said every tick he is (the end of his own tick).</summary>
        private void SeenLater()
        {
            if (phase >= 2 && Fighting)
                laterSeen = Math.Max(1u, Main.GameUpdateCount);
        }

        // ============================================================
        // THE WIND
        // ============================================================

        /// <summary>
        /// Two cuts where he stands. Before each he looks for them again, and comes
        /// round if they have got behind him; in a cut he does not. Where the world is
        /// decided, each is let go of as it is made (Cast, as his third phase's are).
        /// </summary>
        private void Gale(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            foreach (int tick in M.GaleLooks)
            {
                if (At(tick))
                    FaceThem(target);
            }

            if (!Authority)
                return;

            for (int which = 0; which < M.GaleCasts.Length; which++)
            {
                if (At(M.GaleCasts[which]))
                    Cast(target, which, FoighSlash.Pale);
            }

            if (now >= M.GaleTicks)
                Rest(34, 54);
        }

        // ============================================================
        // THE HOOK
        // ============================================================

        /// <summary>His hook is in the air, or about to be: what is in the air is his while this is so (FoighHook).</summary>
        internal bool Casting => Fighting && State == Move.HookCast;

        /// <summary>Where the chain is made fast: his bottom hand, as he is shown.</summary>
        internal Vector2 HookHand => frame.FarFist;

        /// <summary>The player on his hook; null if none.</summary>
        private Player OnHook()
        {
            if (hooked < 0 || hooked >= Main.maxPlayers)
                return null;

            Player player = Main.player[hooked];
            return player.active && !player.dead ? player : null;
        }

        /// <summary>His hook in the air, if it is: his by his index (ai[0]), on every machine.</summary>
        private Projectile Hook()
        {
            int type = ModContent.ProjectileType<FoighHook>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.type == type && (int)projectile.ai[0] == NPC.whoAmI)
                    return projectile;
            }

            return null;
        }

        /// <summary>Whether he would throw it at them from here: within its chain, not too near, and nothing in the way.</summary>
        private bool Hooks(Player target)
        {
            if (phase < 2 || target == null || target.dead)
                return false;

            float far = Vector2.Distance(target.Center, NPC.Center);

            return far >= HookLeast && far <= HookMost
                && Collision.CanHitLine(NPC.Center - new Vector2(0f, 8f), 1, 1, target.Center, 1, 1);
        }

        /// <summary>
        /// The hand back with it, and through. Where the world is decided it leaves at
        /// the tick the hand is through; and from then, with nothing of it left in the
        /// air and nobody on it, there is nothing on it. (What is in the air is gone of
        /// itself the tick he is doing anything else: FoighHook, Casting.)
        /// </summary>
        private void HookCast(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (At(6))
                FaceThem(target);

            if (!Authority)
                return;

            if (At(M.HookLets))
            {
                ThrowHook(target);
                return;
            }

            if (now > M.HookLets && Hook() == null)
                Go(Move.HookMiss);
        }

        /// <summary>Out of his bottom hand, the server's: at where they will be, half of where they are going allowed for (as a knife is).</summary>
        private void ThrowHook(Player target)
        {
            FoighRig.Frame at = FoighRig.Build(PoseOf(State, clock), Feet, facing);
            Vector2 hand = at.FarFist;

            Vector2 to = target != null ? target.Center : hand + new Vector2(facing * 300f, 0f);

            if (target != null)
                to += target.velocity * (Vector2.Distance(hand, to) / FoighHook.Speed) * 0.5f;

            Vector2 way = (to - hand).SafeNormalize(new Vector2(facing, 0f));

            Projectile.NewProjectile(NPC.GetSource_FromAI(), hand, way * FoighHook.Speed,
                ModContent.ProjectileType<FoighHook>(), 0, 0f, Main.myPlayer, NPC.whoAmI);
        }

        /// <summary>
        /// It has bitten this player, their own machine's word, with whether they were
        /// stood on the ground: the server's. On the ground he goes to them; off it they
        /// come to him.
        /// </summary>
        private void Bitten(Player by, bool onGround)
        {
            hooked = by.whoAmI;
            TargetIndex = by.whoAmI;
            NPC.target = by.whoAmI;

            FaceThem(by);
            Go(onGround ? Move.Haul : Move.Winch);
        }

        /// <summary>Nobody is on it any more, where the world is decided.</summary>
        private void Unhook()
        {
            if (hooked < 0)
                return;

            hooked = -1;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// Every tick, where the world is decided: out of the moves the hook is used in
        /// (whatever took him out of them), nobody is on it.
        /// </summary>
        private void HookTick()
        {
            if (State != Move.HookCast && State != Move.Haul && State != Move.Winch && State != Move.Reap)
                Unhook();
        }

        /// <summary>
        /// Along his own chain to whoever is on it, with no weight, as fast as tiles can
        /// stop. Every machine, from where they are on it. Where the world is decided:
        /// near enough, the hook is out of them and the sword comes down (or, with them
        /// off the ground by now, he is beside them in the air already, and it is the
        /// blow that sends them down); a wall between, or too long, and he lets go.
        /// </summary>
        private void Haul()
        {
            Player prey = OnHook();

            if (prey == null)
            {
                Fall();
                NPC.velocity.X *= 0.8f;

                if (Authority)
                    Rest(16, 28);

                return;
            }

            Vector2 to = prey.Center - NPC.Center;
            float far = to.Length();

            if (Math.Abs(to.X) > 10f)
                Face(to.X > 0f ? 1 : -1);

            NPC.velocity = far > 1f ? to * (Math.Min(HaulSpeed, far) * Math.Min(1f, (now + 1) / 4f) / far) : Vector2.Zero;

            if (!Authority)
                return;

            if (far <= HaulReach)
            {
                Meet(prey);
                return;
            }

            if (now >= HaulLongest || (now > 6 && Vector2.Distance(Feet, feetWas) < 2f))
                Rest(16, 28);
        }

        /// <summary>Where one who is pulled in to him is pulled to: ahead of him, at his chest.</summary>
        internal Vector2 WinchPoint => NPC.Center + new Vector2(facing * WinchTo, -WinchUp);

        /// <summary>
        /// He stands, and pulls: whoever is on it comes in to him (their own machine
        /// moves them: Tow). Where the world is decided, the blow is begun when they are
        /// near enough to be under it as it comes down.
        /// </summary>
        private void Winch()
        {
            Fall();
            NPC.velocity.X *= 0.6f;

            if (!Authority)
                return;

            Player prey = OnHook();

            if (prey == null || now >= WinchLongest)
            {
                Rest(16, 28);
                return;
            }

            if (now >= M.WinchPulled - 4 && Vector2.Distance(prey.Center, WinchPoint) <= WinchCuts)
                Meet(prey);
        }

        /// <summary>The two of them brought together, the server's: the blow.</summary>
        private void Meet(Player prey)
        {
            bool pulledIn = State == Move.Winch;

            FaceThem(prey);
            close = true;

            // Gone off the ground while he came: he is beside them in the air.
            if (!pulledIn && Aloft(prey))
            {
                last = Move.Hang;
                Go(Move.Hang);
                return;
            }

            // (Pulled in, they are on it until the blade is down: 1.)
            Go(Move.Reap, pulledIn ? 1f : 0f);
        }

        /// <summary>The blow down, a step into it out of the haul; and back to his stance.</summary>
        private void Reap()
        {
            Fall();

            if (Arg > 0.5f)
                NPC.velocity.X *= 0.6f;
            else
                NPC.velocity.X = Stepped(M.ReapSteps);

            if (!Authority)
                return;

            if (Arg <= 0.5f || now > M.ReapDown)
                Unhook();

            if (now >= M.ReapTicks)
                Rest(26, 44);
        }

        /// <summary>
        /// Every tick, on a machine with a player: this machine's player on his hook and
        /// being pulled in is asked to come (FoighGripPlayer.Drag), for as long as he
        /// pulls and until the blade is down.
        /// </summary>
        private void Tow()
        {
            if (hooked != Main.myPlayer)
                return;

            if (State == Move.Winch || (State == Move.Reap && Arg > 0.5f && now <= M.ReapDown))
                Main.LocalPlayer.GetModPlayer<FoighGripPlayer>().Drag(NPC.whoAmI, WinchPoint);
        }

        /// <summary>His hook is gone from the air here (FoighHook.OnKill): where.</summary>
        internal void HookFell(Vector2 at)
        {
            hookFell = at;
        }

        // ============================================================
        // WHAT HE READS: this machine's own player
        // ============================================================

        /// <summary>Something of this machine's player's has landed on him: the swing they are in has not missed.</summary>
        public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            if (player.whoAmI == Main.myPlayer)
                swingLanded = true;
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.owner == Main.myPlayer)
                swingLanded = true;

            // What fights for them, counted against its own cap (Foigh.Called) and toward
            // the ball (Foigh.Wild).
            CalledLanded(projectile, damageDone);
            Swarmed(projectile, damageDone);
        }

        /// <summary>
        /// Every tick, on a machine with a player, from his second phase: what they do
        /// that he reads, told to the server as it happens.
        /// </summary>
        private void Reads()
        {
            Player me = Main.LocalPlayer;

            if (phase < 2 || !Fighting || Passing || me == null || !me.active || me.dead)
            {
                swingWas = 0;
                heldFor = 0;
                grappleWas = false;
                reloadWas = false;
                return;
            }

            if (readWait > 0)
                readWait--;

            Item item = me.HeldItem;
            bool armed = item != null && !item.IsAir && item.damage > 0;
            float far = Vector2.Distance(me.Center, NPC.Center);

            // A swing: over when the next begins or it runs out (the game counts a use
            // down before it begins the next: Player.ItemCheck_Inner, 1.4.4, so the tick
            // one begins is seen here), and missed if nothing of theirs landed on him, or
            // was met or turned aside by him, in it. (Nothing can land on him while
            // nothing can, and a parry is not a swing: neither is a miss.)
            bool swings = armed && Swung(item) && me.itemAnimation > 0;
            bool began = swings && me.ItemAnimationJustStarted;

            if (swingWas > 0 && (!swings || began) && !swingLanded && !NPC.dontTakeDamage && far <= WhiffWithin)
                Told(ReadSwing);

            if (began || !swings)
                swingLanded = false;

            if (swings && (NPC.dontTakeDamage || me.GetModPlayer<ParryPlayer>().Busy))
                swingLanded = true;

            swingWas = swings ? me.itemAnimation : 0;

            // Something held on him: a beam, anything that is kept up by holding the button.
            // Told when it has been held long enough, and again for as long as it goes on
            // being held, as often as anything is told.
            bool holds = armed && me.channel && me.itemAnimation > 0 && far >= HeldFrom && far <= ReadSees;

            heldFor = holds ? heldFor + 1 : 0;

            if (heldFor >= HeldTicks)
                Told(ReadHeld);

            // A reload begun (the mod's magazines).
            bool reload = item != null && !item.IsAir && item.TryGetGlobalItem(out PSItem magazine) && magazine.Reloading;

            if (reload && !reloadWas && far <= ReadSees)
                Told(ReadReload);

            reloadWas = reload;

            // A hook of theirs biting.
            bool grapple = Hooked(me);

            if (grapple && !grappleWas && far <= ReadSees)
                Told(ReadGrapple);

            grappleWas = grapple;
        }

        /// <summary>
        /// Whether this is swung or thrust in the hand: what hurts by its own swing; and
        /// anything of the melee kind, whatever of it does the hitting (a spear, and the
        /// mod's own swords, which the game has as things held out); and a whip.
        /// </summary>
        private static bool Swung(Item item)
        {
            if (!item.noMelee || (item.DamageType != null && item.DamageType.CountsAsClass(DamageClass.Melee)))
                return true;

            return item.shoot > ProjectileID.None && item.shoot < ProjectileID.Sets.IsAWhip.Length && ProjectileID.Sets.IsAWhip[item.shoot];
        }

        /// <summary>The server is told, if this machine has not told it anything too lately. True if it was.</summary>
        private bool Told(int what)
        {
            if (readWait > 0)
                return false;

            readWait = ReadEvery;
            Tell(Word.Read, n: what);
            return true;
        }

        // ============================================================
        // WHAT HE READS: where the world is decided
        // ============================================================

        /// <summary>
        /// A player's machine has seen them do something he reads. Free to answer, and
        /// not having answered another too lately, he answers as often as he answers
        /// anything: at one off the ground, the hook (or up after them, or a fan of
        /// knives); at one on it, a flash step to in front of them and the sword.
        /// </summary>
        private void Read(Player by, int what)
        {
            if (what < ReadSwing || what > ReadGrapple || !CanReact || !grounded || held >= 0)
                return;

            if (readAt != 0 && Main.GameUpdateCount - readAt < ReadEvery / 2)
                return;

            readAt = Math.Max(1u, Main.GameUpdateCount);

            if (!Answers(0))
                return;

            TargetIndex = by.whoAmI;
            NPC.target = by.whoAmI;
            waitingOut = false;

            if (Aloft(by) || Hooked(by))
            {
                if (Hooks(by))
                {
                    last = Move.HookCast;
                    close = false;
                    FaceThem(by);
                    Go(Move.HookCast);
                    return;
                }

                if (Vector2.Distance(by.Center, NPC.Center) < SkyReach && SkyTo(by, false))
                {
                    last = Move.Hang;
                    return;
                }

                Fan(by);
                return;
            }

            int side = by.Center.X >= NPC.Center.X ? 1 : -1;

            if (Stand(by.Center.X - side * FrontGap, by.Bottom.Y, out Vector2 ahead)
                && Math.Abs(ahead.X - by.Center.X) >= 30f
                && !ShotAt(by, ahead))
            {
                Move next = Main.rand.NextBool() ? Move.One : Move.Three;

                last = next;
                close = true;
                FlashTo(ahead, next);
                return;
            }

            Fan(by);
        }

        /// <summary>A fan of knives at them, the server's.</summary>
        private void Fan(Player by)
        {
            last = Move.Knives;
            close = false;
            FaceThem(by);
            Go(Move.Knives, FanArg);
        }

        // ============================================================
        // THE HOOK, DRAWN
        // ============================================================

        private static Asset<Texture2D> hookHead;
        private static Asset<Texture2D> hookLink;

        /// <summary>A link of the chain is this long; a chain that is not taut sags this much at the most.</summary>
        private const float LinkLength = 12f;
        private const float SagMost = 16f;

        private static void LoadHook()
        {
            hookHead = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Ranged/MeatHook_Hook");
            hookLink = ModContent.Request<Texture2D>("PisAndDag/Content/Projectiles/Ranged/MeatHook_Chain");
        }

        private static void UnloadHook()
        {
            hookHead = null;
            hookLink = null;
        }

        /// <summary>Where his hook is, for the picture.</summary>
        private enum HookAt
        {
            /// <summary>He has not got it yet, or he is not there to have it.</summary>
            None,

            /// <summary>In his bottom hand by its tail.</summary>
            Hand,

            /// <summary>Slung behind his hip: the hand is at something else.</summary>
            Hip,

            /// <summary>In the air (it draws itself: FoighHook).</summary>
            Flying,

            /// <summary>In somebody.</summary>
            Bitten,

            /// <summary>On its way back to his hand, nothing on it.</summary>
            Back,
        }

        private HookAt HookIs
        {
            get
            {
                if (phase < 2 || State < Move.Walk)
                    return HookAt.None;

                switch (State)
                {
                    // The passage into his second phase: taken out while his third line is up.
                    case Move.Knee:
                    case Move.Whet:
                        return HookAt.None;

                    case Move.Spent:
                        return phase >= 3 ? HookAt.Hand : HookAt.None;

                    case Move.Oath:
                        return now >= M.HookTaken ? HookAt.Hand : HookAt.None;

                    // The passage into his third: both hands are at his sword, and then one has their wings.
                    case Move.Shudder:
                    case Move.Gut:
                    case Move.Bled:
                    case Move.Sever:
                        return HookAt.Hip;

                    case Move.Resume:
                        return phase >= 3 ? HookAt.Hip : HookAt.Hand;

                    case Move.Flash:
                        return then == Move.Sever || then == Move.Grab ? HookAt.Hip : HookAt.Hand;

                    case Move.HookCast:
                        return now < M.HookLets ? HookAt.Hand : HookAt.Flying;

                    case Move.Haul:
                    case Move.Winch:
                        return HookAt.Bitten;

                    case Move.Reap:
                        return Arg > 0.5f && now <= M.ReapDown ? HookAt.Bitten : HookAt.Hand;

                    case Move.HookMiss:
                        return now < M.HookIn ? HookAt.Back : HookAt.Hand;

                    // The hand throws, or is for somebody, or is on the sword with the other, or about
                    // his legs, curled up (the ball).
                    case Move.Knives:
                    case Move.TwoGather:
                    case Move.TwoAir:
                    case Move.TwoStuck:
                    case Move.TwoFree:
                    case Move.TwoLanded:
                    case Move.Three:
                    case Move.ThreeRound:
                    case Move.ThreeEnd:
                    case Move.Grab:
                    case Move.Miss:
                    case Move.Lift:
                    case Move.Spit:
                    case Move.Spitted:
                    case Move.Fling:
                    case Move.Crouch:
                    case Move.Spring:
                    case Move.Caught:
                    case Move.Pierce:
                    case Move.Hoist:
                    case Move.Curl:
                        return HookAt.Hip;

                    default:
                        return HookAt.Hand;
                }
            }
        }

        /// <summary>
        /// His hook as it is to be drawn where he is drawn, wherever it is but in the
        /// air: where its tail is and the way it points, and the two ends of what chain
        /// is seen with it, and whether that is taut. In his hand it is held by its
        /// tail, its points ahead of him and a little down, a link of its chain hanging
        /// from the hand; behind his hip it points up and back, and no chain is seen;
        /// in somebody, the chain is taut from his hand to them; on its way back, slack.
        /// False with nothing of it to draw here.
        /// </summary>
        private bool HookLaid(FoighRig.Frame of, out Vector2 tail, out Vector2 way, out Vector2 chainFrom, out Vector2 chainTo, out bool taut)
        {
            tail = way = chainFrom = chainTo = Vector2.Zero;
            taut = true;

            int d = pose.Turn < 0f ? -facing : facing;

            switch (HookIs)
            {
                case HookAt.Hand:
                {
                    // The way it points, as a limb's way is said: from straight down, forward positive.
                    float points = MathHelper.ToRadians(64f) + 0.5f * Math.Max(0f, pose.FarArm);

                    tail = of.FarFist;
                    way = new Vector2(d * (float)Math.Sin(points), (float)Math.Cos(points));
                    chainFrom = of.FarFist + new Vector2(0f, 1f);
                    chainTo = of.FarFist + new Vector2(-d * 1.5f, 12f);
                    return true;
                }

                case HookAt.Hip:
                    tail = of.Hips + new Vector2(-d * 5f, 0f);
                    way = new Vector2(-d * 0.5f, -0.866f);
                    chainFrom = chainTo = tail;
                    return true;

                case HookAt.Bitten:
                {
                    Player prey = OnHook();

                    if (prey == null)
                        return false;

                    way = (prey.Center - of.FarFist).SafeNormalize(new Vector2(facing, 0f));
                    tail = prey.Center - way * FoighHook.Tail;
                    chainFrom = of.FarFist;
                    chainTo = tail;
                    return true;
                }

                case HookAt.Back:
                {
                    float t = Easing.Smoother(MathHelper.Clamp(now / (float)M.HookIn, 0f, 1f));

                    way = (hookFell - of.FarFist).SafeNormalize(new Vector2(facing, 0f));
                    tail = Vector2.Lerp(hookFell - way * FoighHook.Tail, of.FarFist, t);
                    chainFrom = of.FarFist;
                    chainTo = tail;
                    taut = false;
                    return true;
                }

                default:
                    return false;
            }
        }

        /// <summary>His hook, under the rest of him, as his bottom arm is: lit as he is in his hand or at his hip, and as the place it is in anywhere else.</summary>
        private void DrawHook(SpriteBatch spriteBatch, Vector2 screenPos, FoighRig.Frame of, Color light)
        {
            if (!HookLaid(of, out Vector2 tail, out Vector2 way, out Vector2 chainFrom, out Vector2 chainTo, out bool taut))
                return;

            HookAt where = HookIs;
            bool his = where == HookAt.Hand || where == HookAt.Hip;

            DrawChain(spriteBatch, chainFrom, chainTo, screenPos, taut);
            DrawHookHead(spriteBatch, tail, way, screenPos, his ? light : Lighting.GetColor(tail.ToTileCoordinates()));
        }

        /// <summary>The hook itself: its tail here, pointing this way. (The picture points up, its tail at (11, 30).)</summary>
        internal static void DrawHookHead(SpriteBatch spriteBatch, Vector2 tail, Vector2 way, Vector2 screenPos, Color light)
        {
            if (hookHead == null || !hookHead.IsLoaded)
                return;

            Texture2D head = hookHead.Value;

            spriteBatch.Draw(head, tail - screenPos, null, light, way.ToRotation() + MathHelper.PiOver2, new Vector2(head.Width * 0.5f, head.Height - 2f), 1f,
                SpriteEffects.None, 0f);
        }

        /// <summary>
        /// The chain between two places, link by link, each link lit as the place it is
        /// in: a line, taut; and not taut, sagging as things fall.
        /// </summary>
        internal static void DrawChain(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Vector2 screenPos, bool taut)
        {
            if (hookLink == null || !hookLink.IsLoaded)
                return;

            Vector2 span = to - from;
            float length = span.Length();

            if (length < 3f)
                return;

            Texture2D link = hookLink.Value;
            Vector2 origin = link.Size() * 0.5f;

            float sag = taut ? 0f : Math.Min(SagMost, length * 0.12f);
            int links = Math.Max(1, (int)Math.Round(length / LinkLength));
            Vector2 previous = from;

            for (int i = 1; i <= links; i++)
            {
                float along = i / (float)links;
                Vector2 point = Vector2.Lerp(from, to, along) + new Vector2(0f, sag * 4f * along * (1f - along));
                Vector2 piece = point - previous;
                float pieceLength = piece.Length();

                if (pieceLength > 0.01f)
                {
                    Vector2 middle = (previous + point) * 0.5f;

                    spriteBatch.Draw(link, middle - screenPos, null, Lighting.GetColor(middle.ToTileCoordinates()), piece.ToRotation() + MathHelper.PiOver2, origin,
                        new Vector2(1f, pieceLength / link.Height), SpriteEffects.None, 0f);
                }

                previous = point;
            }
        }

        // ---- what the hook's moves look and sound like ----

        /// <summary>Every tick, on a machine with a screen: the hook's moves, and the wind's.</summary>
        private void ReachLooks(Vector2 feet)
        {
            switch (State)
            {
                case Move.Gale:
                    if (At(M.GaleHold)) Sound(SoundID.Item71, NPC.Center, -0.1f, 0.9f);
                    if (At(M.GaleDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 0.7f);
                    if (At(M.GaleSinks)) Glint(frame.Tip, Steel, 26f, 10);
                    if (At(M.GaleCoiled + 1)) Sound(SoundID.Item71, NPC.Center, 0.2f, 0.9f);
                    if (At(M.GaleOver + 8)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.6f);

                    if (At(M.GaleTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.5f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 4, 2f, 0.8f);
                    }
                    break;

                case Move.HookCast:
                    // The hand back with it: a light on it, and its chain heard.
                    if (At(3))
                    {
                        Glint(frame.FarFist, Steel, 24f, 12);
                        Sound(SoundID.Item37, frame.FarFist, -0.2f, 0.5f);
                    }

                    if (At(M.HookLets)) Sound(SoundID.DD2_JavelinThrowersAttack, frame.FarFist, 0.1f, 0.8f);
                    break;

                case Move.Haul:
                    if (now % 2 == 0)
                        Wind(feet + new Vector2(-facing * 26f, -30f), feet + new Vector2(facing * 10f, -30f), 1);

                    if (now % 3 == 0)
                        LeaveGhost(0.4f);
                    break;

                case Move.Winch:
                    if (At(2)) Sound(SoundID.Item37, frame.FarFist, -0.4f, 0.7f);
                    if (At(M.WinchPulled - 2)) Glint(frame.Tip, Steel, 30f, 10);
                    break;

                case Move.Reap:
                    if (At(M.ReapHold)) Sound(SoundID.Item71, NPC.Center, -0.4f, 1f);
                    if (At(M.ReapDown)) Impact(new Vector2(frame.Tip.X, feet.Y), 1.5f);
                    if (At(M.ReapRest + 9)) Sound(SoundID.Item1, NPC.Center, -0.7f, 0.7f);

                    if (At(M.ReapTicks - 6))
                    {
                        Sound(SoundID.Tink, frame.Tip, -0.6f, 0.6f);
                        Puff(new Vector2(frame.Tip.X, feet.Y), 5, 2f, 0.8f);
                    }
                    break;

                case Move.HookMiss:
                    if (now < M.HookIn && now % 4 == 0)
                        Sound(SoundID.Item37, frame.FarFist, 0.3f, 0.3f);

                    if (At(M.HookIn)) Sound(SoundID.Item1, frame.FarFist, -0.3f, 0.6f);
                    break;

                // The hook taken out, in the passage: heard, and no more said of it.
                case Move.Oath:
                    if (phase == 2 && At(M.HookTaken))
                    {
                        Sound(SoundID.Item37, frame.FarFist, -0.3f, 0.6f);
                        Sound(SoundID.Item1, frame.FarFist, -0.6f, 0.5f);
                    }
                    break;
            }
        }

        /// <summary>What going into one of them looks and sounds like, the tick this machine sees it.</summary>
        private void ReachEntered(Vector2 feet)
        {
            switch (State)
            {
                case Move.Gale:
                    Sound(SoundID.Item71, NPC.Center, -0.3f, 0.7f);
                    Glint(frame.EyeAt, Eye, 24f);
                    break;

                case Move.HookCast:
                    Glint(frame.EyeAt, Eye, 26f);
                    break;

                // It has bitten: the chain goes taut.
                case Move.Haul:
                    Sound(SoundID.Item37, frame.FarFist, -0.5f, 0.9f);
                    Sound(SoundID.DD2_MonkStaffSwing, NPC.Center, 0.1f, 0.8f);
                    Puff(feet, 7, 3f, 0.8f);
                    break;

                case Move.Winch:
                    Sound(SoundID.Item37, frame.FarFist, -0.5f, 0.9f);
                    Glint(frame.FarFist, Steel, 22f, 10);
                    break;
            }
        }
    }
}
