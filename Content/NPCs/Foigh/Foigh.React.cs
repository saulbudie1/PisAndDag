using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using M = PisAndDag.Content.NPCs.Foigh.FoighMoves;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// What he does about what is sent at him, while he is not attacking (walking, or already
    /// turning something aside): by request,
    ///
    ///   a swing of theirs      a sword swung, or anything held and swung (a spear, a whip, a
    ///                          sword of the mod's own): three times in five he meets it. It does
    ///                          not land, and they are thrown back, whatever shield they have.
    ///   a hard shot            a bullet, an arrow, a dart, a knife: two times in five he
    ///                          strikes it out of the air, and half of those go back at whoever
    ///                          sent them (FoighReturned).
    ///   anything else thrown   a spell, a beam, a sword's light, a boomerang, fire, a rocket:
    ///                          three times in five he is not there, a flash step to in front of
    ///                          them or up over it and down on them.
    ///
    /// IN HIS SECOND PHASE, by request, each of those three is two times in three (and of
    /// the shots struck, still half go back).
    ///
    /// What is called up to fight for them (a minion, a sentry, and what those shoot) is
    /// answered by itself (Foigh.Called).
    ///
    /// WHO DECIDES. The machine of the player whose shot or swing it is, as the game decides
    /// there whether any hit of theirs lands (Projectile.Damage and Player.ItemCheck run for
    /// their owner alone). That machine rolls, once for each shot, a little before the shot
    /// gets to him so that his sword is up when it does; refuses the hit when it comes
    /// (CanBeHitByProjectile, CanCollideWithPlayerMeleeAttack); and tells the server what he
    /// did about it (Foigh.Tell), which is what moves him.
    /// </summary>
    public partial class Foigh
    {
        /// <summary>What a thing sent at him is, for what he does about it.</summary>
        private enum Shot : byte
        {
            /// <summary>Nothing he answers: it lands.</summary>
            None,

            /// <summary>What is called up to fight for them, and what that shoots: answered by itself (Foigh.Called).</summary>
            Called,

            /// <summary>Held and swung.</summary>
            Held,

            /// <summary>A solid thing shot or thrown.</summary>
            Hard,

            /// <summary>Anything else thrown at him.</summary>
            Soft,
        }

        /// <summary>Of five: swings met, hard shots struck, the rest stepped out of the way of. And of two struck, one goes back.</summary>
        private const int SwingsMet = 3;
        private const int ShotsStruck = 2;
        private const int ShotsDodged = 3;

        /// <summary>In his second phase, by request, every one of the three: this many times in this many.</summary>
        private const int SecondAnswers = 2;
        private const int SecondOf = 3;

        /// <summary>And in his third, by request ("Bump his parries to 4/5", "4 in 5 parries is phase 3").</summary>
        private const int ThirdAnswers = 4;
        private const int ThirdOf = 5;

        /// <summary>
        /// Whether he answers one thing sent at him, rolled now: so many times in five, as
        /// that kind of thing is answered in his first phase; or, in his second, two times
        /// in three whatever it is.
        /// </summary>
        private bool Answers(int ofFive)
        {
            if (phase >= 3)
                return Main.rand.Next(ThirdOf) < ThirdAnswers;

            return phase >= 2 ? Main.rand.Next(SecondOf) < SecondAnswers : Main.rand.Next(5) < ofFive;
        }

        /// <summary>How many ticks before a shot gets to him he starts to do something about it: the sword up, or gone.</summary>
        private const int GuardLead = 5;
        private const int DodgeLead = 14;

        /// <summary>Met: how they are thrown back, and for how long their feet are not theirs.</summary>
        private static readonly Vector2 ThrownBack = new Vector2(6f, -3.6f);
        private const int ThrownTicks = 14;

        /// <summary>A shot sent back: no slower and no faster than these.</summary>
        private const float BackLeast = 9f;
        private const float BackMost = 16f;

        /// <summary>
        /// Whether he answers what is sent at him right now: on his feet, the sword in his
        /// hand, walking or already turning something aside.
        /// </summary>
        public bool CanReact => hasSword && Fighting && (State == Move.Walk || State == Move.Guard || State == Move.Clash);

        // ============================================================
        // WHAT IT IS
        // ============================================================

        private static Shot Kind(Projectile p)
        {
            if (!p.friendly || p.damage <= 0 || p.npcProj || p.trap || p.owner < 0 || p.owner >= Main.maxPlayers)
                return Shot.None;

            // What fights for them, and what that shoots.
            if (p.minion || p.sentry || ProjectileID.Sets.MinionShot[p.type] || ProjectileID.Sets.SentryShot[p.type])
                return Shot.Called;

            if (ProjectileID.Sets.IsAWhip[p.type])
                return Shot.Held;

            bool melee = p.DamageType != null && p.DamageType.CountsAsClass(DamageClass.Melee);

            // In their hand: what only reaches where they can (ownerHitCheck: spears, short
            // swords, the mod's own swords), or what they are holding out.
            if (melee && (p.ownerHitCheck || Main.player[p.owner].heldProj == p.whoAmI
                || p.aiStyle == ProjAIStyleID.Spear || p.aiStyle == ProjAIStyleID.ShortSword))
                return Shot.Held;

            if (melee || ProjectileID.Sets.Explosive[p.type])
                return Shot.Soft;

            bool shot = p.DamageType != null
                && (p.DamageType.CountsAsClass(DamageClass.Ranged) || p.DamageType.CountsAsClass(DamageClass.Throwing));

            return shot && ParryRules.Solid(p) ? Shot.Hard : Shot.Soft;
        }

        // ============================================================
        // BEFORE IT GETS TO HIM: this machine's own shots
        // ============================================================

        /// <summary>
        /// Every tick, on a machine with a player: each shot of theirs that is about to reach
        /// him is rolled for, once, and if he is to do something about it the server is told
        /// now, so that he is doing it when it gets there.
        /// </summary>
        private void React()
        {
            // A stream is answered whole, a second at a time, as it gets to him (Foigh.Hold).
            if (!CanReact || Streaming)
                return;

            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
                return;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner != Main.myPlayer)
                    continue;

                // From his second phase, what they hold out and keep up is read as that
                // (Foigh.Reach), not stepped out of the way of the moment it is there.
                if (phase >= 2 && me.heldProj == p.whoAmI)
                    continue;

                Shot kind = Kind(p);

                if (kind != Shot.Hard && kind != Shot.Soft)
                    continue;

                FoighMark mark = p.GetGlobalProjectile<FoighMark>();

                if (mark.Rolled(NPC) || !Coming(p, kind == Shot.Hard ? GuardLead : DodgeLead))
                    continue;

                Roll(p, kind, mark);

                // Gone, or busy: nothing more is asked of him this tick.
                if (!CanReact)
                    return;
            }
        }

        /// <summary>The roll for a shot, once, and the server told if he is to do something about it before it lands.</summary>
        private void Roll(Projectile p, Shot kind, FoighMark mark)
        {
            if (kind == Shot.Hard)
            {
                bool struckOut = Answers(ShotsStruck);

                mark.Set(NPC, struckOut ? FoighMark.Struck : FoighMark.Lands, struckOut && Main.rand.NextBool());

                if (struckOut)
                    Tell(Word.Guard);

                return;
            }

            bool dodged = Answers(ShotsDodged);

            mark.Set(NPC, dodged ? FoighMark.Dodged : FoighMark.Lands, false);

            if (dodged)
                Tell(Word.Dodge, p.Center, p.velocity * p.MaxUpdates);
        }

        /// <summary>
        /// Whether this shot, going as it goes, is on him within this many ticks: a straight
        /// line, which over a few ticks is what anything flies in.
        /// </summary>
        private bool Coming(Projectile p, int ticks)
        {
            Vector2 step = p.velocity * p.MaxUpdates;

            if (step.LengthSquared() < 1f)
                return false;

            Rectangle body = NPC.Hitbox;

            body.Inflate(6 + p.width / 2, 6 + p.height / 2);

            Vector2 at = p.Center;

            for (int i = 0; i <= ticks; i++)
            {
                if (body.Contains(at.ToPoint()))
                    return true;

                at += step;
            }

            return false;
        }

        // ============================================================
        // WHEN IT GETS TO HIM
        // ============================================================

        /// <summary>
        /// The game asks this of every shot of this machine's player's, every tick, before it
        /// has looked at whether the two are touching (Projectile.Damage, 1.4.4): so that is
        /// looked at here, as the game is about to (<see cref="WouldHit"/>).
        /// </summary>
        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (!Fighting || projectile.owner != Main.myPlayer || Main.dedServ)
                return null;

            FoighMark mark = projectile.GetGlobalProjectile<FoighMark>();

            if (mark.Dead)
                return false;

            // Nothing is decided of a thing before it is on him.
            if (!WouldHit(projectile))
                return null;

            // Broken, down on a knee, or his sword just thrown aside: it lands (for what the cap allows).
            if (Unguarded)
                return null;

            Shot kind = Kind(projectile);

            // What is called up to fight for them is answered by itself (Foigh.Called): it is
            // no part of what keeps coming, and turned aside it owes nothing to his posture.
            if (kind == Shot.Called)
            {
                if (CalledTurned(projectile, mark))
                    return false;

                return null;
            }

            // From his second phase every hit of theirs that would land is counted, for
            // whether it is something that keeps coming.
            bool counted = phase >= 2 && kind != Shot.None;

            if (counted)
                Press();

            // More than can be taken from him now: turned aside, whatever it is (Foigh.Hold).
            if (Capped)
            {
                Deflect(projectile, mark, false);
                return false;
            }

            // What keeps coming is answered whole, a second at a time, in whatever move
            // he is in; and while he runs at them behind his sword, all of it, wherever
            // it comes at him from: it is whoever he runs at that the sword is up
            // against. (One who stands behind him then has his back.)
            if (counted)
            {
                bool run = State == Move.Charge && (Main.LocalPlayer.Center.X - NPC.Center.X) * facing >= 0f;

                if (run)
                {
                    Deflect(projectile, mark, false);
                    return false;
                }

                if (Streaming && StreamHeld())
                {
                    Deflect(projectile, mark, true);
                    return false;
                }

                // Through, for this second of it.
                if (Streaming)
                    return null;
            }

            if (kind == Shot.Held)
            {
                if (!CanReact || !Answers(SwingsMet))
                    return null;

                // The rest of this swing is through with him: as if it had landed, and for no
                // less than what is left of the swing that holds it out.
                ShareShot(NPC, projectile, Main.LocalPlayer.itemAnimation);
                Met(Main.LocalPlayer, Vector2.Clamp(projectile.Center, NPC.TopLeft, NPC.BottomRight));
                return false;
            }

            if (kind != Shot.Hard || !CanReact)
                return null;

            // On him with no roll made: it came from too near for one to be made ahead of it.
            if (!mark.Rolled(NPC))
                Roll(projectile, kind, mark);

            if (mark.Verdict != FoighMark.Struck)
                return null;

            // Struck out of the air: it does not land, it is gone at the end of its tick
            // (FoighMark), and the server is told, with whether it comes back.
            Vector2 at = Vector2.Clamp(projectile.Center, NPC.TopLeft, NPC.BottomRight);

            mark.Dead = true;
            projectile.alpha = 255;

            ParryEffects.Spark(at);
            Tell(Word.Parried, at, projectile.velocity * projectile.MaxUpdates, projectile.type, mark.Returns);

            return false;
        }

        /// <summary>
        /// A sword swung at him, the game's own swing of an item: asked every tick of the swing
        /// until it has landed (Player.ProcessHitAgainstNPC, 1.4.4), with where the swing is.
        /// </summary>
        public override bool? CanCollideWithPlayerMeleeAttack(Player player, Item item, Rectangle meleeAttackHitbox)
        {
            if (Main.dedServ || player.whoAmI != Main.myPlayer || !Fighting)
                return null;

            // More than can be taken from him now is met whatever he is doing (Foigh.Hold);
            // anything else only while he is free to meet it.
            bool capped = Capped;

            if (!capped && !CanReact)
                return null;

            // On him, and nothing between them: what the game is about to ask.
            if (!meleeAttackHitbox.Intersects(NPC.Hitbox) || (!NPC.noTileCollide && !player.CanHit(NPC)))
                return null;

            if (!capped && !Answers(SwingsMet))
                return null;

            // The rest of this swing is through with him, as if it had landed: the player's
            // own wait for this NPC, for as long as the swing has left to run, which is what
            // the game sets when a swing lands (Player.ProcessHitAgainstNPC, 1.4.4).
            player.SetMeleeHitCooldown(NPC.whoAmI, player.itemAnimation);

            Rectangle met = Rectangle.Intersect(meleeAttackHitbox, NPC.Hitbox);

            Met(player, met.Center.ToVector2());
            return false;
        }

        /// <summary>
        /// Whether this shot of this machine's player's would land on him this tick if nothing
        /// stopped it: what the game goes on to ask once this hook has answered
        /// (Projectile.Damage, 1.4.4: his wait for its owner unless it is a shot that stops at
        /// the first thing it hits, its owner's reach for a held one, and the two touching).
        /// </summary>
        private bool WouldHit(Projectile p)
        {
            bool single = p.maxPenetrate == 1 && !p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity;

            if (!single && NPC.immune[p.owner] != 0)
                return false;

            if (!NPC.noTileCollide && p.ownerHitCheck && !p.CanHitWithMeleeWeapon(NPC))
                return false;

            return p.Colliding(DamageBox(p), NPC.getRect());
        }

        /// <summary>The box a projectile hurts with (Projectile.Damage_GetHitbox, 1.4.4, which is the game's own and private).</summary>
        private static Rectangle DamageBox(Projectile p)
        {
            Rectangle box = new Rectangle((int)p.position.X, (int)p.position.Y, p.width, p.height);

            // The game's own few, by their numbers as it has them there.
            if (p.type == 101)
                box.Inflate(30, 30);

            if (p.type == 85)
            {
                int grown = (int)Utils.Remap(p.localAI[0], 0f, 72f, 10f, 40f);
                box.Inflate(grown, grown);
            }

            if (p.type == 188)
                box.Inflate(20, 20);

            if (p.aiStyle == 29)
                box.Inflate(4, 4);

            if (p.type == 967)
                box.Inflate(10, 10);

            ProjectileLoader.ModifyDamageHitbox(p, ref box);
            return box;
        }

        /// <summary>
        /// He is made immune to this projectile as if it had just hit him, the way
        /// Projectile.Damage makes what it hits (1.4.4): by its own id's timer, by its own
        /// timer for each NPC (the Stardust Dragon by its head's, all of it: Foigh.Called), or
        /// the owner's ten ticks on the NPC, and the owner's own wait for what is swung as a
        /// sword is; and for no less than <paramref name="least"/> ticks. Never shortens a
        /// wait already running.
        /// </summary>
        private static void ShareShot(NPC npc, Projectile projectile, int least)
        {
            int i = npc.whoAmI;
            bool lasting = projectile.penetrate != 1 || projectile.appliesImmunityTimeOnSingleHits;

            least = Math.Max(0, least);

            if (projectile.usesIDStaticNPCImmunity)
            {
                if (lasting)
                {
                    uint until = Main.GameUpdateCount + (uint)Math.Max(projectile.idStaticNPCHitCooldown, least);
                    uint[] waits = Projectile.perIDStaticNPCImmunity[projectile.type];
                    waits[i] = Math.Max(waits[i], until);
                }
            }
            else if (projectile.usesLocalNPCImmunity && projectile.localNPCHitCooldown != -2)
            {
                int[] own = HitsBy(projectile);
                int wait = own[i];

                // -1 is until the projectile is gone: nothing is longer.
                if (wait != -1)
                    own[i] = projectile.localNPCHitCooldown == -1 ? -1 : Math.Max(wait, Math.Max(projectile.localNPCHitCooldown, least));
            }
            else if (projectile.owner >= 0 && projectile.owner < npc.immune.Length)
            {
                npc.immune[projectile.owner] = Math.Max(npc.immune[projectile.owner], Math.Max(10, least));
            }

            // And the swing's own wait, for what is swung as a sword is (the game's blades of
            // light and the like: Projectile.usesOwnerMeleeHitCD).
            if (projectile.usesOwnerMeleeHitCD && projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            {
                Player owner = Main.player[projectile.owner];

                if (owner.CanHitNPCWithMeleeHit(i))
                    owner.SetMeleeHitCooldown(i, Math.Max(owner.itemAnimation, least));
            }
        }

        /// <summary>
        /// This machine's player's swing met his sword: steel, the star (it goes through any
        /// shield, so it is told like everything that does), them thrown back straight onto
        /// their speed, and the server told.
        /// </summary>
        private void Met(Player me, Vector2 at)
        {
            int away = me.Center.X >= NPC.Center.X ? 1 : -1;

            ParryEffects.Clash(at, new Vector2(away, -0.3f));
            CCBypass.Warn(NPC);

            me.GetModPlayer<FoighGripPlayer>().Shove(new Vector2(away * ThrownBack.X, ThrownBack.Y), ThrownTicks);
            me.PIS().BumpScreenShake(5f, 10);

            // Met is not missed (Foigh.Reach).
            swingLanded = true;

            Tell(Word.Clash);
        }

        // ============================================================
        // WHERE THE WORLD IS DECIDED
        // ============================================================

        /// <summary>The sword up across his front, toward whoever shot; again, if it is up already.</summary>
        private void Guarded(int by)
        {
            if (State == Move.Guard)
            {
                // Still up for the next one.
                guards++;
                Timer = Math.Min(Timer, M.GuardThere + 2);
                NPC.netUpdate = true;
                return;
            }

            Player who = Main.player[by];

            if (Math.Abs(who.Center.X - NPC.Center.X) > 6f)
                Face(who.Center.X > NPC.Center.X ? 1 : -1);

            guards = 0;
            Go(Move.Guard);
        }

        private void Guard(Player target)
        {
            Fall();
            NPC.velocity.X *= 0.7f;

            if (!Authority)
                return;

            // What he was going to do next, he still is.
            rest--;

            // Kept in it by something that goes on coming (Foigh.Hold), he does not wait
            // for it to stop: his next move is begun from here, and what is turned aside
            // goes on being turned aside while he makes it.
            if (rest <= 0 && Pressed && grounded && target != null)
            {
                Choose(target);
                return;
            }

            if (now >= M.GuardTicks)
                Go(Move.Walk);
        }

        /// <summary>His sword into theirs, toward them.</summary>
        private void Clashed(Player by)
        {
            if (Math.Abs(by.Center.X - NPC.Center.X) > 6f)
                Face(by.Center.X > NPC.Center.X ? 1 : -1);

            // They are thrown back: he comes on after them sooner. But not out of the
            // second or two he takes after a move from up close in his second phase, which
            // was asked for as a wait between his attacks: that he waits out.
            if (!waitingOut)
                rest = Math.Min(rest, 26);

            Go(Move.Clash);
        }

        private void Clash()
        {
            Fall();
            NPC.velocity.X = Stepped(M.ClashSteps);

            if (!Authority)
                return;

            rest--;

            if (now >= M.ClashTicks)
                Go(Move.Walk);
        }

        /// <summary>
        /// Not there when it lands: to in front of them, and straight into the sword; or up
        /// over it, and down on them. In front if they are on the ground with somewhere for
        /// him to stand, and nothing else of theirs is on its way to that place; up if not,
        /// and now and then anyway.
        /// </summary>
        private void Dodged(Player by, Vector2 at, Vector2 going)
        {
            int side = by.Center.X >= NPC.Center.X ? 1 : -1;
            bool level = by.velocity.Y == 0f || Collision.SolidCollision(by.BottomLeft, by.width, 40);

            Vector2 ahead = Feet;

            bool front = level
                && Stand(by.Center.X - side * FrontGap, by.Bottom.Y, out ahead)
                && Math.Abs(ahead.X - by.Center.X) >= 30f
                && !ShotAt(by, ahead);

            if (front && Main.rand.Next(4) != 0)
            {
                TargetIndex = by.whoAmI;
                NPC.target = by.whoAmI;

                Move next = Main.rand.NextBool() ? Move.One : Move.Three;

                last = next;
                close = phase >= 2;
                FlashTo(ahead, next);
                return;
            }

            // Up: toward them, as high as there is room for.
            Vector2 feet = Feet + new Vector2(side * Math.Min(Math.Abs(by.Center.X - NPC.Center.X) * 0.5f, 120f), 0f);
            float high = UpHigh;

            while (high > 64f && Collision.SolidCollision(new Vector2(feet.X - NPC.width * 0.5f, feet.Y - high - NPC.height), NPC.width, NPC.height))
                high -= 24f;

            if (high <= 64f)
            {
                // No room over him either: he takes it.
                return;
            }

            TargetIndex = by.whoAmI;
            NPC.target = by.whoAmI;
            last = Move.TwoGather;

            FlashTo(feet + new Vector2(0f, -high), Move.TwoAir);
        }

        /// <summary>Whether anything of this player's is on its way to where he would stand.</summary>
        private bool ShotAt(Player by, Vector2 feet)
        {
            Rectangle there = new Rectangle((int)(feet.X - NPC.width * 0.5f) - 10, (int)(feet.Y - NPC.height) - 10, NPC.width + 20, NPC.height + 20);

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner != by.whoAmI || Kind(p) < Shot.Hard)
                    continue;

                Vector2 step = p.velocity * p.MaxUpdates;
                Vector2 at = p.Center;

                // Between where he is and where he would be is on its way past already.
                for (int i = 0; i < DodgeLead + FlashTicks; i++)
                {
                    at += step;

                    if (i >= FlashTicks && there.Contains(at.ToPoint()))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A shot of theirs struck out of the air goes back at whoever sent it, the server's:
        /// what it was, from where it was struck, no slower than it came (FoighReturned).
        /// </summary>
        private void SendBack(Player to, Vector2 at, Vector2 going, int type)
        {
            if (type <= ProjectileID.None || type >= ProjectileLoader.ProjectileCount || Vector2.Distance(at, NPC.Center) > 400f)
                return;

            float speed = MathHelper.Clamp(going.Length(), BackLeast, BackMost);
            Vector2 way = (to.Center - at).SafeNormalize(new Vector2(-facing, 0f));

            Projectile.NewProjectile(NPC.GetSource_FromAI(), at, way * speed, ModContent.ProjectileType<FoighReturned>(),
                0, 0f, Main.myPlayer, type, NPC.whoAmI);
        }

        // ============================================================
        // THE ONE WHO DRANK
        // ============================================================

        /// <summary>This machine's player's wait for their next potion, as it was a tick ago; less than none before it has been looked at.</summary>
        private int waitWas = -1;

        /// <summary>
        /// Every tick, on a machine with a player: a healing potion drunk where he can see it
        /// is known by the wait for the next one beginning (Player.ApplyPotionDelay, 1.4.4,
        /// which every item that heals as a potion goes through), and the server is told.
        /// </summary>
        private void Drinks()
        {
            Player me = Main.LocalPlayer;

            if (me == null || !me.active || me.dead)
            {
                waitWas = -1;
                return;
            }

            int wait = me.potionDelay;

            if (Fighting && waitWas >= 0 && wait > waitWas && Vector2.Distance(me.Center, NPC.Center) <= SeesDrink)
                Tell(Word.Drank);

            waitWas = wait;
        }
    }

    /// <summary>
    /// What Foigh is to do about one shot, kept on the shot itself, on the machine of whoever
    /// shot it: rolled once (Foigh.React), and if he strikes it out of the air it hits nothing
    /// more and is gone at the end of its tick.
    /// </summary>
    public class FoighMark : GlobalProjectile
    {
        public const byte None = 0;
        public const byte Lands = 1;
        public const byte Struck = 2;
        public const byte Dodged = 3;

        public override bool InstancePerEntity => true;

        /// <summary>Which Foigh rolled for it, by his place among the NPCs; less than none before any has.</summary>
        private int rolledBy = -1;

        public byte Verdict;

        /// <summary>Struck out of the air, it goes back at whoever sent it.</summary>
        public bool Returns;

        /// <summary>Struck out of the air: it hits nothing more, and goes.</summary>
        public bool Dead;

        public bool Rolled(NPC by) => rolledBy == by.whoAmI && Verdict != None;

        public void Set(NPC by, byte verdict, bool returns)
        {
            rolledBy = by.whoAmI;
            Verdict = verdict;
            Returns = returns;
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target) => Dead ? false : null;

        public override bool CanHitPlayer(Projectile projectile, Player target) => !Dead;

        public override void PostAI(Projectile projectile)
        {
            if (Dead && projectile.owner == Main.myPlayer)
                projectile.Kill();
        }
    }
}
