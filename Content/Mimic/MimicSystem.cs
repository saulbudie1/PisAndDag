using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Graphics;
using PisAndDag.Content.PADplayer;
using PisAndDag.Content.Particles;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Mimic
{
    /// <summary>
    /// The Everclear Mirror's copy: a second player, its owner's copy, that the mod moves and fights with.
    ///
    /// WHAT IT IS. A Player of its own in a slot no one uses in a world of one (<see cref="Slot"/>), made from its owner
    /// when the mirror is used (<see cref="Summon"/>) and kept a copy of them (<see cref="Mirror"/>): their look; their
    /// armour, accessories, vanity and dyes, as they change; every buff on them that is a buff (not a debuff, a pet, a
    /// mount or a minion's); their crystals and the rest of what they have eaten for good; their spells, if it holds a
    /// staff. It holds a copy of what is in its owner's off hand (or, with nothing there that fights, of the best weapon
    /// on their hotbar), the ammunition they carry, and 30 Super Healing Potions of its own. Its life is three times its
    /// owner's (MimicPlayer).
    ///
    /// A PLAYER TO THE GAME. It is in the game's list of players as any second player would be (by request: "create
    /// pseudo player, doesnt count towards scaling; but still attracts the boss"): enemies and bosses choose it or its
    /// owner as they choose between any two players, the nearer (NPC.TargetClosest), and their shots and bodies hurt it
    /// as they would hurt a player (below). It is drawn as the game draws any other player, with its life under it when
    /// it is hurt. It counts for nothing a second player would: in a world of one the game sizes every enemy for one
    /// player whoever else is there (NPC.GetActivePlayerCount); enemies are not spawned about it (it is out of the list
    /// while they are, <see cref="SpawnWithoutCopy"/>); it is out of the list while the clock runs (Main.UpdateTime:
    /// invasions are sized by the players there, bosses that come at night choose a player); and it is not counted
    /// among the players that must be asleep for the night to pass (<see cref="PostUpdatePlayers"/>). Whoever was after
    /// it looks again when it goes.
    ///
    /// HOW IT RUNS. Each tick, before the players are (<see cref="PreUpdatePlayers"/>): it is mirrored; its brain is told
    /// the moment (MimicSense) and says what to press (MimicBrain). Then the game runs it with the other players, as it
    /// runs a player of another machine (Player.Update): it moves, jumps, flies and dashes as any player does. Using its
    /// weapon, and everything its weapon's shots do, it is run as the game runs the player at the keyboard
    /// (<see cref="Scope"/>: Main.myPlayer is the copy for the moment, the cursor where it aims, the mouse buttons what it
    /// presses), so every weapon is used by the game's own code: swung, shot, channelled, thrown, held out, its right
    /// click, its mana and ammunition. After the players (<see cref="PostUpdatePlayers"/>), whatever touches it hurts it
    /// as it would hurt the player at the keyboard (Player.Update_NPCCollision; enemy shots as Projectile.Damage hurts
    /// that player, which the game does for that player alone), its parry turns what it can aside (ParryPlayer), and it
    /// drinks a potion when its brain says. Every hit of its own is its owner's too, and loot is made for the owner
    /// whoever is nearer, so what it kills drops what it would for them (<see cref="MimicCredit"/>,
    /// <see cref="LootAsOwner"/>).
    ///
    /// HOW IT ENDS. At no life (MimicPlayer stops it dying as a player dies) it breaks: glass and the sound of it, and
    /// its owner's mirror is clouded for <see cref="Clouded"/> ticks. Sent away (the buff ended, its owner dead, the
    /// world left) it fades. Either way everything of its own goes with it.
    ///
    /// A world of one only: in a world of several, the others would know nothing of it.
    /// </summary>
    public class MimicSystem : ModSystem
    {
        /// <summary>The player slot the copy is kept in: the last before the server's own.</summary>
        public const int Slot = 254;

        /// <summary>Ticks the mirror stays clouded after its copy breaks (by request: "Cool down 45 secs").</summary>
        public const int Clouded = 45 * 60;

        /// <summary>
        /// The owner's minion slots past the mirror's own twelve (by request: "every slot after that is n with n = max
        /// minion - 12"), all of them the copy's: each makes it tougher and harder-hitting (MimicPlayer). Read from its
        /// owner each tick, so a summoning potion or a slot accessory put on or off counts at once.
        /// </summary>
        public static int Extra => Copy != null && Owner >= 0 && Owner < Main.maxPlayers && Main.player[Owner].active
            ? Math.Max(0, Main.player[Owner].maxMinions - Items.Summoner.EverclearMirror.Slots)
            : 0;

        /// <summary>How far the light of its breaking reaches (PrismShatter), in world pixels: a copy's, not a crack's.</summary>
        private const float BreakReach = 190f;

        /// <summary>Potions it is given.</summary>
        public const int Potions = 30;

        /// <summary>How far from its owner it may be before it is brought back to them, in pixels: 80 tiles.</summary>
        private const float Lost = 80 * 16f;

        internal static Player Copy;
        internal static int Owner = -1;
        private static Player kept;
        private static MimicBrain brain;

        /// <summary>What it has learned of each weapon it has fought with, kept from one summoning to the next.</summary>
        private static readonly Practice practice = new Practice();

        /// <summary>What the copy has learned of each weapon it has fought with (kept with its owner: MimicPlayer).</summary>
        internal static Practice Learned => practice;

        /// <summary>Whose copy's learning <see cref="Learned"/> holds (saved with them: MimicPlayer).</summary>
        internal static Player LearnedFor;

        // ---- what a weapon's right click puts out ----
        // Each tick its own projectiles are noted with the tick they were first seen; those that appear on a tick it holds
        // the right click teach which kinds of shot the held item's right click makes (AltShots). The newest such shot
        // still out is what its "set up and shoot" way aims at (MimicSense, MimicBrain).
        private static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>> AltShots = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.HashSet<int>>();
        private static readonly int[] seenAt = new int[Main.maxProjectiles];
        private static readonly int[] seenType = new int[Main.maxProjectiles];

        private static void NoteShots()
        {
            Player c = Copy;
            Item held = c.HeldItem;

            bool alt = c.altFunctionUse == 2 || intent.Alt;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                int i = p.whoAmI;

                if (p.owner != c.whoAmI || p.minion || p.sentry)
                    continue;

                if (seenType[i] == p.type + 1 && seenAt[i] > 0)
                    continue;

                seenType[i] = p.type + 1;
                seenAt[i] = tick;

                if (alt && held != null && !held.IsAir)
                {
                    if (!AltShots.TryGetValue(held.type, out var set))
                        AltShots[held.type] = set = new System.Collections.Generic.HashSet<int>();

                    set.Add(p.type);
                }
            }
        }

        /// <summary>The newest of the copy's shots its held item's right click makes, still out and within 1200 px.</summary>
        internal static bool NewestAltShot(Player c, out Projectile shot)
        {
            shot = null;
            Item held = c.HeldItem;

            if (held == null || held.IsAir || !AltShots.TryGetValue(held.type, out var set))
                return false;

            int best = -1;

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (p.owner != c.whoAmI || !set.Contains(p.type) || Vector2.DistanceSquared(p.Center, c.Center) > 1200f * 1200f)
                    continue;

                if (seenAt[p.whoAmI] > best)
                {
                    best = seenAt[p.whoAmI];
                    shot = p;
                }
            }

            return shot != null;
        }

        /// <summary>What it was told and what it did last tick, for <see cref="Report"/>.</summary>
        private static Situation lastSeen;
        private static Intent lastDone;
        private static Intent intent;
        private static int lastTarget = -1;
        private static int tick;
        private static int stuck;
        private static int dashStep, dashWay;
        private static string wornLast = "";
        private static Item weaponFrom;
        private static int weaponType = -1, weaponPrefix = -1;

        /// <summary>It is being let go: what it lets go of (its anchor) does not call it back.</summary>
        private static bool releasing;

        /// <summary>Where it aims, in the world: what the cursor is for it.</summary>
        internal static Vector2 Aim;

        public static bool Exists => Copy != null;

        /// <summary>Whether this player is the copy.</summary>
        public static bool Is(Player player) => Copy != null && ReferenceEquals(player, Copy);

        /// <summary>
        /// Damage the copy landed on something (MimicPlayer): what it learns its weapon's best distance from, and, put down to
        /// the way and distance band the shot that landed it was made under (<paramref name="tag"/>, MimicShots; -1 for a blow
        /// of the item itself, or a shot not marked: the way in use now), which way of using it lands the most.
        /// </summary>
        internal static void Landed(NPC target, int damage, int tag = -1)
        {
            if (Copy != null && brain != null && target != null)
                brain.Practice.Landed(damage, Vector2.Distance(Copy.Center, target.Center), tag);
        }

        /// <summary>The way and distance band the copy is using its weapon at now (Practice.Tag), for a shot it makes (MimicShots).</summary>
        internal static int TagNow => brain?.Practice.Tag ?? -1;

        /// <summary>
        /// Why it is or is not fighting, in a line (the /mirror command): the weapon as it reads it, its mana, its
        /// target, how far, whether in reach and in sight, and the distance it has learned to fight from.
        /// </summary>
        internal static string Report()
        {
            if (Copy == null || brain == null)
                return "No copy out.";

            Situation s = lastSeen;
            Intent i = lastDone;
            Weapon w = s.Weapon;
            var sb = new System.Text.StringBuilder();
            sb.Append("Weapon: ").Append(Copy.HeldItem?.Name ?? "none").Append(" read as ").Append(w.Kind)
              .Append(" (").Append(MimicWeapons.ClassNames[Math.Clamp(w.Class, 0, 3)]).Append(", ")
              .Append(MimicWeapons.StyleNames[Math.Clamp(w.Style, 0, MimicWeapons.StyleNames.Length - 1)]).Append(')')
              .Append(", reach ").Append((int)w.Reach).Append(" px, shot ").Append(w.Speed.ToString("0.#"))
              .Append(" px/t, kind ").Append(w.Cluster);

            if (w.Cluster >= 0 && w.Cluster < MimicWeapons.KindNames.Length)
                sb.Append(" (").Append(MimicWeapons.KindNames[w.Cluster]).Append(')');

            string watched = Copy.HeldItem == null || Copy.HeldItem.IsAir ? "" : MimicWeapons.WatchReport(Copy.HeldItem.type);

            if (watched.Length > 0)
                sb.Append(". ").Append(watched);

            if (w.ManaCost > 0f)
                sb.Append(", ").Append((int)w.ManaCost).Append(" mana a use");

            sb.Append(". Mana ").Append((int)s.Mana).Append('/').Append((int)s.ManaMax);

            if (brain.Resting)
                sb.Append(" (resting for mana)");

            sb.Append(". ");

            if (i.Target < 0 || i.Target >= s.FoeCount)
            {
                sb.Append(s.FoeCount == 0 ? "Nothing it can fight is near." : "No target chosen (nothing within its leash of you).");
            }
            else
            {
                Foe f = s.Foes[i.AttackTarget >= 0 && i.AttackTarget < s.FoeCount ? i.AttackTarget : i.Target];
                NPC npc = f.Index >= 0 && f.Index < Main.maxNPCs ? Main.npc[f.Index] : null;
                float d = Vector2.Distance(s.Me.Pos, f.Pos);
                sb.Append("Target: ").Append(npc?.GivenOrTypeName ?? "?").Append(", ").Append((int)d).Append(" px away, ")
                  .Append(f.Seen ? "in sight" : "not in sight").Append(", ")
                  .Append(d <= w.Reach + Math.Max(f.HalfW, f.HalfH) ? "in reach" : "out of reach")
                  .Append(i.Use ? ", attacking." : ", not attacking.");
            }

            string ways = practice.WaysReport();

            if (ways.Length > 0)
                sb.Append(" Ways tried with it (damage a second): ").Append(ways).Append('.');

            string best = practice.BestWays();

            if (best.Length > 0)
                sb.Append(" Uses it: ").Append(best).Append('.');

            return sb.ToString();
        }

        /// <summary>
        /// A hit written for the player at the keyboard (Main.LocalPlayer), tried on the copy too. The mod's own fighters
        /// hurt "this machine's player" by hand (a blade's arc, a thrown lance, a vent's steam), each machine its own
        /// player; the copy lives on its owner's machine, so it is put through the same check as if it were the one at
        /// the keyboard (Main.myPlayer its slot for the moment, and back). Nothing without a living copy. A check that
        /// keeps what it has already hit should keep it apart for the copy (<see cref="Is"/>).
        /// </summary>
        public static void AlsoOnCopy(Action check)
        {
            Player c = Copy;

            if (c == null || !c.active || c.dead || check == null || Main.dedServ)
                return;

            int was = Main.myPlayer;
            Main.myPlayer = Slot;

            try
            {
                check();
            }
            finally
            {
                Main.myPlayer = was;
            }
        }

        public override void Load()
        {
            On_Player.ItemCheckWrapped += UseAsLocal;
            On_Player.ItemCheck_ManageRightClickFeatures += RightClickAsLocal;
            On_Projectile.Update += ShotAsLocal;
            On_NPC.SpawnNPC += SpawnWithoutCopy;
            On_NPC.NPCLoot += LootAsOwner;
        }

        public override void Unload() => Release(false);

        public override void OnWorldUnload() => Release(false);

        // ---- coming and going ---------------------------------------------------------------------------------

        /// <summary>Makes the copy of <paramref name="owner"/> at <paramref name="at"/>. A world of one only.</summary>
        public static bool Summon(Player owner, Vector2 at)
        {
            if (Main.netMode != NetmodeID.SinglePlayer || Copy != null)
                return false;

            var copy = new Player();
            copy.name = owner.name;
            copy.difficulty = PlayerDifficultyID.SoftCore;
            copy.whoAmI = Slot;
            copy.team = owner.team;
            copy.hostile = false;
            copy.direction = owner.direction;

            kept = Main.player[Slot];
            Main.player[Slot] = copy;
            Copy = copy;
            Owner = owner.whoAmI;
            if (LearnedFor != owner)
            {
                LearnedFor = owner;
                owner.GetModPlayer<MimicPlayer>().Teach(practice);
            }

            brain = new MimicBrain(MimicGenes.Trained, owner.whoAmI * 977 + (int)Main.GameUpdateCount, practice);
            intent = default;
            lastTarget = -1;
            tick = 0;
            stuck = 0;
            dashStep = 0;
            wornLast = "";
            weaponType = -1;
            weaponFrom = null;

            Look(owner);
            Mirror(owner, true);

            copy.inventory[10] = new Item(ItemID.SuperHealingPotion, Potions);
            copy.Center = at;
            copy.velocity = Vector2.Zero;
            copy.fallStart = (int)(copy.position.Y / 16f);
            copy.GetModPlayer<MimicPlayer>().Fresh = true;
            copy.active = true;

            Shimmer(copy.Center, 40);
            return true;
        }

        /// <summary>Breaks it: glass, the sound of it, and the owner's mirror clouded.</summary>
        public static void Shatter()
        {
            if (Copy == null || releasing)
                return;

            Vector2 at = Copy.Center;
            SoundEngine.PlaySound(SoundID.Shatter, at);

            if (!Main.dedServ)
            {
                for (int i = 0; i < 40; i++)
                {
                    Dust dust = Dust.NewDustDirect(Copy.position, Copy.width, Copy.height, DustID.Glass, 0f, 0f, 0, default, 1.2f);
                    dust.velocity = Main.rand.NextVector2Circular(6f, 6f) + new Vector2(0f, -1.5f);
                }
            }

            Shimmer(at, 30);

            // Its glass breaking as light (by request, "maybe add that effect to the Death of Everclear Mirror too": the
            // Empress's crack's burst, PrismShatter, in the mirror's colours and the copy's size), its panes thrown out of it.
            if (!Main.dedServ)
            {
                PADParticleHandler.SpawnParticle(new PrismShatterBurst(at, Main.rand.Next(1 << 20), BreakReach, PrismShatter.Look.Mirror, true));
                SoundEngine.PlaySound(NPCs.Empress.EmpressSounds.Shards with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 2 }, at);
                SoundEngine.PlaySound(NPCs.Empress.EmpressSounds.Shock with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 2 }, at);

                if (Main.LocalPlayer.WithinRange(at, 1600f))
                    Main.LocalPlayer.PIS().BumpScreenShake(7f, 16);
            }

            if (Owner >= 0 && Main.player[Owner].active)
            {
                Player owner = Main.player[Owner];
                owner.GetModPlayer<MimicPlayer>().Cloud = Clouded;

                // Its last gift (by request: "upon it dying heals player by half max player cumulative HP"): half its
                // owner's whole life, as it is with everything worn and every buff (statLifeMax2).
                if (!owner.dead)
                    owner.Heal(owner.statLifeMax2 / 2);
            }

            Release(true);
        }

        /// <summary>Sends it away: a shimmer where it stood, and nothing of it left.</summary>
        public static void Dismiss()
        {
            if (Copy == null || releasing)
                return;

            Shimmer(Copy.Center, 30);
            Release(true);
        }

        /// <summary>
        /// Everything of its own gone, its owner's anchor and buff with it, the slot as it was, and whatever was after it
        /// looking again.
        /// </summary>
        private static void Release(bool tidy)
        {
            if (Copy == null || releasing)
                return;

            releasing = true;

            try
            {
                Player copy = Copy;

                if (tidy)
                {
                    // Its shots end as they would end for it (in its own hands: what they leave behind is its too).
                    Scope.Enter();

                    try
                    {
                        foreach (Projectile p in Main.ActiveProjectiles)
                        {
                            if (p.owner == Slot)
                                p.Kill();
                        }
                    }
                    finally
                    {
                        Scope.Exit();
                    }

                    if (Owner >= 0 && Main.player[Owner].active)
                    {
                        Player owner = Main.player[Owner];
                        owner.ClearBuff(ModContent.BuffType<Buffs.EverclearMirrorBuff>());

                        foreach (Projectile p in Main.ActiveProjectiles)
                        {
                            if (p.owner == Owner && p.type == ModContent.ProjectileType<Projectiles.Summoner.MimicAnchor>())
                                p.Kill();
                        }
                    }
                }

                // Whatever of its own is left (all of it, when the world is left) simply goes.
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].owner == Slot)
                        Main.projectile[i].active = false;
                }

                MimicShots.Clear();
                copy.active = false;
                Main.player[Slot] = kept ?? new Player();
                kept = null;
                Copy = null;
                Owner = -1;
                brain = null;

                if (tidy)
                {
                    foreach (NPC npc in Main.ActiveNPCs)
                    {
                        if (npc.target == Slot)
                            npc.TargetClosest(false);
                    }
                }
            }
            finally
            {
                releasing = false;
            }
        }

        private static void Shimmer(Vector2 at, int count)
        {
            if (Main.dedServ)
                return;

            for (int i = 0; i < count; i++)
            {
                Dust dust = Dust.NewDustPerfect(at + Main.rand.NextVector2Circular(14f, 24f), DustID.MagicMirror, Main.rand.NextVector2Circular(2.5f, 2.5f), 150, default, 1.1f);
                dust.noGravity = true;
            }
        }

        // ---- out of the list where a second player would count ---------------------------------------------------

        /// <summary>Enemies spawn about the players; not about it (NPC.SpawnNPC goes through every active player).</summary>
        private static void SpawnWithoutCopy(On_NPC.orig_SpawnNPC orig)
        {
            if (Copy == null || !Copy.active)
            {
                orig();
                return;
            }

            Copy.active = false;

            try
            {
                orig();
            }
            finally
            {
                if (Copy != null)
                    Copy.active = true;
            }
        }

        /// <summary>
        /// What dies drops for the owner, whoever killed it: the game gives the loot of a kill to the player nearest it
        /// (NPC.NPCLoot: the drops' conditions are asked of that player, coins go by their luck, hearts by their life),
        /// and runs everything a kill sets off as the player at the keyboard. So while loot is made the copy is out of
        /// the list and its owner is that player, even when it was the copy's blow (made as the player at the keyboard,
        /// <see cref="Scope"/>).
        /// </summary>
        private static void LootAsOwner(On_NPC.orig_NPCLoot orig, NPC self)
        {
            if (Copy == null)
            {
                orig(self);
                return;
            }

            bool was = Copy.active;
            int me = Main.myPlayer;
            Copy.active = false;

            if (me == Slot && Owner >= 0)
                Main.myPlayer = Owner;

            try
            {
                orig(self);
            }
            finally
            {
                Main.myPlayer = me;

                if (Copy != null)
                    Copy.active = was;
            }
        }

        /// <summary>
        /// Out of the list while the clock runs (Main.UpdateTime): an invasion is sized by the players there with enough
        /// life, a boss that comes at night chooses one of them, and the rest of the world's own events look at them.
        /// </summary>
        public override void PreUpdateTime()
        {
            if (Copy != null)
                Copy.active = false;
        }

        public override void PostUpdateTime()
        {
            if (Copy != null)
                Copy.active = true;
        }

        // ---- each tick ------------------------------------------------------------------------------------------

        public override void PreUpdatePlayers()
        {
            if (Copy == null)
                return;

            Player owner = Owner >= 0 ? Main.player[Owner] : null;

            if (owner == null || !owner.active || owner.dead || Main.netMode != NetmodeID.SinglePlayer)
            {
                Dismiss();
                return;
            }

            Copy.active = true;
            tick++;
            Mirror(owner, false);
            Bring(owner);

            MimicPlayer self = Copy.GetModPlayer<MimicPlayer>();
            Situation s = MimicSense.Read(Copy, owner, lastTarget, self.JumpSpeed, self.JumpTicks);

            // The numbers for the kind of weapon it holds (MimicWeapons: the trainer sorted every weapon into kinds and
            // trained each kind's numbers apart).
            brain.Genes = MimicGenes.For(s.Weapon.Cluster);
            intent = brain.Think(ref s);
            lastSeen = s;
            lastDone = intent;
            lastTarget = intent.Target >= 0 ? s.Foes[intent.Target].Index : -1;
            Aim = intent.Aim;
            Drive();
        }

        /// <summary>
        /// After the projectiles have moved: what its weapon's shots do as they fly, and whether one is held in its hands (a
        /// held projectile makes itself so in its own update; round 81: MimicShots, MimicWeapons.Watched).
        /// </summary>
        public override void PostUpdateProjectiles()
        {
            if (Copy != null && Copy.active)
                MimicShots.Watch(Copy);
        }

        /// <summary>
        /// After the game has run every player, the copy among them: it is taken off the count of players (so a night
        /// can be slept through with it out), hurt by what touches it, parries, drinks, and its own debuffs run down;
        /// at no life it breaks.
        /// </summary>
        public override void PostUpdatePlayers()
        {
            if (Copy == null)
                return;

            if (Copy.active && Main.CurrentFrameFlags.ActivePlayersCount > 1)
                Main.CurrentFrameFlags.ActivePlayersCount--;

            NoteShots();

            Scope.Enter();

            try
            {
                if (!Copy.dead)
                {
                    Shots();
                    Copy.Update_NPCCollision();

                    ParryPlayer parry = Copy.GetModPlayer<ParryPlayer>();

                    if (intent.Parry && !parry.Busy)
                    {
                        Copy.direction = intent.ParryFacing;
                        parry.Begin(ParryTiming.CurvedSword, intent.ParryFacing, Copy.HeldItem);
                    }

                    parry.PostUpdate();

                    if (intent.Heal)
                        Copy.QuickHeal();
                }
            }
            finally
            {
                Scope.Exit();
            }

            TickOwnBuffs();

            if (Copy.statLife <= 0 || Copy.GetModPlayer<MimicPlayer>().Broken || Copy.dead)
                Shatter();
        }

        /// <summary>
        /// What the copy presses this tick, from what its brain wants: left or right, jump (let go for a tick to jump
        /// again from the ground), down, the weapon and its right click (not while it parries); a dash is two taps
        /// the same way, as the game reads one (Player.DoCommonDashHandle).
        /// </summary>
        private static void Drive()
        {
            Player c = Copy;
            bool busy = c.GetModPlayer<ParryPlayer>().Busy;

            int move = intent.Move;

            if (dashStep == 0 && intent.Dash != 0 && c.dashType > 0 && c.dashDelay == 0)
            {
                dashStep = 4;
                dashWay = intent.Dash;
            }

            if (dashStep > 0)
            {
                // Away, tap, let go, tap: the game dashes on the second tap.
                move = dashStep == 4 ? 0 : dashStep == 2 ? 0 : dashWay;
                dashStep--;
            }

            c.controlLeft = move < 0;
            c.controlRight = move > 0;
            c.controlUp = false;
            c.controlDown = intent.Down;

            bool jump = intent.Jump;

            if (jump && c.velocity.Y == 0f && !c.releaseJump)
                jump = false;

            // A jump in the air is a new press: let go this tick, and the brain holds it again on the next.
            if (intent.JumpTap && c.velocity.Y != 0f)
                jump = false;

            c.controlJump = jump;
            c.controlUseItem = intent.Use && !busy;
            c.controlUseTile = intent.Alt && !busy && !intent.Use;
            c.controlHook = false;
            c.controlMount = false;
            c.controlQuickHeal = false;
            c.controlQuickMana = false;
            c.controlThrow = false;
            c.controlInv = false;
            c.controlSmart = false;
            c.controlTorch = false;
            c.mapZoomIn = c.mapZoomOut = c.mapAlphaUp = c.mapAlphaDown = c.mapFullScreen = c.mapStyle = false;
            c.selectedItem = 0;
            c.autoReuseAllWeapons = true;
        }

        /// <summary>
        /// Back by its owner, with a shimmer at both ends, when it is too far from them, or has been stuck in blocks
        /// for a second.
        /// </summary>
        private static void Bring(Player owner)
        {
            Player c = Copy;
            stuck = Collision.SolidCollision(c.position, c.width, c.height) ? stuck + 1 : 0;

            if (Vector2.Distance(c.Center, owner.Center) < Lost && stuck < 60)
                return;

            Shimmer(c.Center, 20);
            c.Center = owner.Center + new Vector2(-owner.direction * 32f, 0f);
            c.velocity = Vector2.Zero;
            c.fallStart = (int)(c.position.Y / 16f);
            stuck = 0;
            brain.Reset();
            Shimmer(c.Center, 20);
        }

        // ---- being hurt -----------------------------------------------------------------------------------------

        /// <summary>
        /// Enemy shots on it, as the game hurts the player at the keyboard with them (Projectile.Damage, 1.4.4): one
        /// that is hostile, has damage, is touching it and may hit it (the mod's hooks first: its parry turns them
        /// aside there); turned aside by a raised shield if the game says so; else twice its damage, the way the game
        /// lands an enemy shot, on its own cooldown if it keeps one, and the shot's debuffs with it.
        /// </summary>
        private static void Shots()
        {
            Player c = Copy;
            Rectangle body = c.getRect();

            foreach (Projectile p in Main.ActiveProjectiles)
            {
                if (!p.hostile || p.damage <= 0 || c.dead)
                    continue;

                int cooldown = CooldownOf(p);

                if (c.immune && cooldown == -1)
                    continue;

                if (!p.Colliding(HitboxOf(p), body) || !CombinedHooks.CanBeHitByProjectile(c, p))
                    continue;

                if (c.CanParryAgainst(c.Hitbox, p.Hitbox, p.velocity))
                    continue;

                int way = c.Center.X < p.Center.X ? -1 : 1;
                int damage = Main.DamageVar(p.damage, p.owner >= 0 && p.owner < Main.maxPlayers ? -Main.player[p.owner].luck : 0f) * 2;

                if (c.Hurt(PlayerDeathReason.ByProjectile(-1, p.whoAmI), damage, way, false, false, cooldown, p.IsDamageDodgable(), p.ArmorPenetration) > 0.0 && !c.dead)
                    p.StatusPlayer(Slot);
            }
        }

        /// <summary>The cooldown a shot hits on, as the game has it (Projectile.Damage): a few of its own on the first, a modded one its own.</summary>
        private static int CooldownOf(Projectile p)
        {
            if (p.ModProjectile != null)
                return p.ModProjectile.CooldownSlot;

            switch (p.type)
            {
                case 452:
                case 454:
                case 455:
                case 462:
                case 871:
                case 872:
                case 873:
                case 874:
                case 919:
                case 923:
                case 924:
                    return 1;
                default:
                    return -1;
            }
        }

        /// <summary>
        /// The box a shot hurts with, as the game makes it (Projectile.Damage_GetHitbox, which the game keeps to itself):
        /// a few of its own made larger, and whatever a mod makes of it.
        /// </summary>
        private static Rectangle HitboxOf(Projectile p)
        {
            Rectangle box = p.Hitbox;

            if (p.type == 101)
                box.Inflate(30, 30);

            if (p.type == 85)
            {
                int grow = (int)Utils.Remap(p.localAI[0], 0f, 72f, 10f, 40f);
                box.Inflate(grow, grow);
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
        /// Its own debuffs run down, as the game runs down only the keyboard player's (Player.UpdateBuffs): what it
        /// mirrors is written afresh each tick, and what was put on it (a burn, a poison, its potion sickness) is its own.
        /// </summary>
        private static void TickOwnBuffs()
        {
            Player c = Copy;

            for (int i = 0; i < Player.MaxBuffs; i++)
            {
                int type = c.buffType[i];

                if (type <= 0 || !Main.debuff[type] || BuffID.Sets.TimeLeftDoesNotDecrease[type])
                    continue;

                if (--c.buffTime[i] <= 0)
                {
                    c.DelBuff(i);
                    i--;
                }
            }
        }

        // ---- the copy of its owner -------------------------------------------------------------------------------

        /// <summary>How its owner looks: their skin, hair, eyes, clothes; their name.</summary>
        private static void Look(Player owner)
        {
            Player c = Copy;
            c.name = owner.name;
            c.Male = owner.Male;
            c.skinVariant = owner.skinVariant;
            c.hair = owner.hair;
            c.hairDye = owner.hairDye;
            c.hairColor = owner.hairColor;
            c.skinColor = owner.skinColor;
            c.eyeColor = owner.eyeColor;
            c.shirtColor = owner.shirtColor;
            c.underShirtColor = owner.underShirtColor;
            c.pantsColor = owner.pantsColor;
            c.shoeColor = owner.shoeColor;
        }

        /// <summary>
        /// Kept a copy of its owner: what they wear and how it shows (when it changes), their weapon (when it changes and
        /// the copy is not in the middle of using its own), their ammunition and their spells now and then, their buffs
        /// every tick.
        /// </summary>
        private static void Mirror(Player owner, bool all)
        {
            if (all || tick % 60 == 0)
            {
                Look(owner);
                Stats(owner);
                Ammo(owner);
                Arsenal(owner);
            }

            if (all || tick % 10 == 0)
            {
                string worn = Worn(owner);

                if (all || worn != wornLast)
                {
                    wornLast = worn;
                    Equip(owner);
                }
            }

            Weapon(owner, all);
            Buffs(owner);
        }

        /// <summary>A short line that changes when anything they wear does.</summary>
        private static string Worn(Player owner)
        {
            var sb = new System.Text.StringBuilder(128);

            for (int i = 0; i < owner.armor.Length; i++)
                sb.Append(owner.armor[i].type).Append(':').Append(owner.armor[i].prefix).Append(',');

            for (int i = 0; i < owner.hideVisibleAccessory.Length; i++)
                sb.Append(owner.hideVisibleAccessory[i] ? 'h' : 's');

            sb.Append(',');

            for (int i = 0; i < owner.dye.Length; i++)
                sb.Append(owner.dye[i].type).Append(',');

            return sb.ToString();
        }

        private static void Equip(Player owner)
        {
            Player c = Copy;

            for (int i = 0; i < owner.armor.Length && i < c.armor.Length; i++)
                c.armor[i] = owner.armor[i].Clone();

            for (int i = 0; i < owner.dye.Length && i < c.dye.Length; i++)
                c.dye[i] = owner.dye[i].Clone();

            // Not its owner's pet, light, cart, mount or hook (Player.miscEquips): nothing of those shows on a player.

            for (int i = 0; i < owner.hideVisibleAccessory.Length && i < c.hideVisibleAccessory.Length; i++)
                c.hideVisibleAccessory[i] = owner.hideVisibleAccessory[i];
        }

        /// <summary>Their life and mana crystals and fruit, and the things eaten for good, so its numbers are theirs.</summary>
        private static void Stats(Player owner)
        {
            Player c = Copy;
            c.statLifeMax = owner.statLifeMax;
            c.statManaMax = owner.statManaMax;
            c.ConsumedLifeCrystals = owner.ConsumedLifeCrystals;
            c.ConsumedLifeFruit = owner.ConsumedLifeFruit;
            c.ConsumedManaCrystals = owner.ConsumedManaCrystals;
            c.extraAccessory = owner.extraAccessory;
            c.usedAegisCrystal = owner.usedAegisCrystal;
            c.usedAegisFruit = owner.usedAegisFruit;
            c.usedArcaneCrystal = owner.usedArcaneCrystal;
            c.usedGalaxyPearl = owner.usedGalaxyPearl;
            c.usedGummyWorm = owner.usedGummyWorm;
            c.usedAmbrosia = owner.usedAmbrosia;
            c.anglerQuestsFinished = owner.anglerQuestsFinished;
        }

        /// <summary>
        /// Their ammunition and their coins, renewed: the copy never runs out of what they carry, and never spends theirs
        /// (coins for a weapon that throws them: Midas Prime's right click).
        /// </summary>
        private static void Ammo(Player owner)
        {
            Player c = Copy;

            for (int i = 50; i < 58; i++)
                c.inventory[i] = owner.inventory[i].Clone();

            // Ammunition kept in the pack instead, for the weapon it holds: the first stack of it.
            Item weapon = c.inventory[0];

            if (weapon.useAmmo > 0 && !Has(c, weapon.useAmmo, 54, 58))
            {
                for (int i = 0; i < 54; i++)
                {
                    if (owner.inventory[i].ammo == weapon.useAmmo && owner.inventory[i].stack > 0)
                    {
                        c.inventory[54] = owner.inventory[i].Clone();
                        break;
                    }
                }
            }
        }

        private static bool Has(Player player, int ammo, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (player.inventory[i].ammo == ammo && player.inventory[i].stack > 0)
                    return true;
            }

            return false;
        }

        /// <summary>Their spells and which is up, so a staff of theirs casts in its hands what it casts in theirs.</summary>
        private static void Arsenal(Player owner)
        {
            MagicArsenal theirs = owner.GetModPlayer<MagicArsenal>();
            MagicArsenal mine = Copy.GetModPlayer<MagicArsenal>();

            for (int i = 0; i < theirs.Slots.Length && i < mine.Slots.Length; i++)
                mine.Slots[i] = theirs.Slots[i].Clone();

            mine.Unlocked = theirs.Unlocked;
            mine.Maxed = theirs.Maxed;
            mine.Select(theirs.Selected(Items.Mage.CastKind.Spell));
            mine.Select(theirs.Selected(Items.Mage.CastKind.Incantation));
        }

        /// <summary>
        /// The weapon: a copy of what is in the owner's off hand (where the game has it while the off hand is in use,
        /// OffHandPlayer.Held), if it is one that fights and is not a tool or the mirror; else of the best of that kind
        /// on their hotbar (the most damage a second). Changed only between its own uses.
        /// </summary>
        private static void Weapon(Player owner, bool all)
        {
            Player c = Copy;
            Item wanted = Chosen(owner);

            if (wanted == null)
            {
                if (!c.inventory[0].IsAir && c.itemAnimation == 0)
                    c.inventory[0] = new Item();

                weaponType = -1;
                return;
            }

            bool changed = wanted.type != weaponType || wanted.prefix != weaponPrefix || !ReferenceEquals(wanted, weaponFrom);

            if (!all && (!changed || c.itemAnimation > 0 || c.channel))
            {
                // The same weapon: its stack kept up for one that is thrown away as it is used.
                if (!changed && c.inventory[0].type == wanted.type && c.inventory[0].stack < wanted.stack && c.itemAnimation == 0)
                    c.inventory[0].stack = wanted.stack;

                return;
            }

            c.inventory[0] = wanted.Clone();
            c.inventory[0].favorited = false;
            weaponType = wanted.type;
            weaponPrefix = wanted.prefix;
            weaponFrom = wanted;
            brain?.Reset();
        }

        private static Item Chosen(Player owner)
        {
            if (OffHandPlayer.Open)
            {
                Item hand = owner.GetModPlayer<OffHandPlayer>().Held;

                if (Fights(hand))
                    return hand;
            }

            Item best = null;
            float bestRate = 0f;

            for (int i = 0; i < 10; i++)
            {
                Item item = owner.inventory[i];

                if (!Fights(item))
                    continue;

                float rate = item.damage * 60f / Math.Max(1, item.useTime);

                if (rate > bestRate)
                {
                    bestRate = rate;
                    best = item;
                }
            }

            return best;
        }

        /// <summary>A weapon it can fight with: used, with damage, not a tool, a rod, a block, a potion or the mirror.</summary>
        private static bool Fights(Item item)
        {
            if (item == null || item.IsAir || item.useStyle == ItemUseStyleID.None || item.accessory)
                return false;

            if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0 || item.createTile >= 0 || item.createWall >= 0)
                return false;

            if (item.ModItem is Items.Summoner.EverclearMirror || item.potion || item.healLife > 0 || item.mountType >= 0)
                return false;

            return item.damage > 0 || item.ModItem is Items.Mage.Catalyst;
        }

        /// <summary>Not these, ever: what would be wrong on a copy, or is its own.</summary>
        private static readonly HashSet<int> NotMirrored = new HashSet<int>
        {
            BuffID.PotionSickness, BuffID.ManaSickness, BuffID.Gravitation, BuffID.ShadowDodge, BuffID.Werewolf, BuffID.Merfolk,
        };

        /// <summary>
        /// Its buffs: its own debuffs and potion sickness kept where they are, and after them every buff its owner has
        /// that is one (not a debuff; not a pet's, a light's, a mount's or a minion's: no time is shown for those and
        /// they are not kept between visits, Main.buffNoTimeDisplay and Main.buffNoSave; not the few in
        /// <see cref="NotMirrored"/>), as long as theirs has left. The Empress's favour too, though it is shown and kept
        /// as those are: it is the flight her arena lends.
        /// </summary>
        private static void Buffs(Player owner)
        {
            Player c = Copy;
            int mine = ModContent.BuffType<Buffs.EverclearMirrorBuff>();
            int favour = ModContent.BuffType<Buffs.FavorOfAnEmpress>();
            Span<int> types = stackalloc int[Player.MaxBuffs];
            Span<int> times = stackalloc int[Player.MaxBuffs];
            int n = 0;

            for (int i = 0; i < Player.MaxBuffs; i++)
            {
                int type = c.buffType[i];

                if (type > 0 && Main.debuff[type] && n < types.Length)
                {
                    types[n] = type;
                    times[n++] = c.buffTime[i];
                }
            }

            for (int i = 0; i < Player.MaxBuffs && n < types.Length; i++)
            {
                int type = owner.buffType[i];

                if (type <= 0 || owner.buffTime[i] <= 0 || Main.debuff[type] || type == mine || NotMirrored.Contains(type))
                    continue;

                // The Empress's favour is shown without time and never kept, but it is what lets anyone in her arena fly
                // (Buffs.FavorOfAnEmpress): her arena gives it only to the player at the keyboard, and the copy with them
                // flies on it as they do.
                if (type != favour && (Main.vanityPet[type] || Main.lightPet[type] || Main.buffNoTimeDisplay[type] && Main.buffNoSave[type]))
                    continue;

                types[n] = type;
                times[n++] = owner.buffTime[i];
            }

            for (int i = 0; i < Player.MaxBuffs; i++)
            {
                c.buffType[i] = i < n ? types[i] : 0;
                c.buffTime[i] = i < n ? times[i] : 0;
            }
        }

        // ---- run as the player at the keyboard ---------------------------------------------------------------------

        /// <summary>
        /// The moments the copy is run as the player at the keyboard: Main.myPlayer is its slot; the cursor is where it
        /// aims (Main.mouseX and Y, and the input's, as the game moves them itself to aim for a player,
        /// Player.ForceForwardCursor); the mouse buttons are what it presses; nothing is on the cursor. All of it put
        /// back as it was after. Nested, it is entered once.
        /// </summary>
        internal static class Scope
        {
            private static int depth;
            private static int myPlayer, mouseX, mouseY, inputX, inputY;
            private static bool left, right, leftRelease, rightRelease, block, hoverNPC, smartShowing;
            private static Item mouseItem;
            private static readonly Item Empty = new Item();

            public static void Enter()
            {
                if (depth++ > 0 || Copy == null)
                    return;

                myPlayer = Main.myPlayer;
                mouseX = Main.mouseX;
                mouseY = Main.mouseY;
                inputX = PlayerInput.MouseX;
                inputY = PlayerInput.MouseY;
                left = Main.mouseLeft;
                right = Main.mouseRight;
                leftRelease = Main.mouseLeftRelease;
                rightRelease = Main.mouseRightRelease;
                block = Main.blockMouse;
                hoverNPC = Main.HoveringOverAnNPC;
                smartShowing = Main.SmartInteractShowingGenuine;
                mouseItem = Main.mouseItem;

                Main.myPlayer = Slot;
                Vector2 screen = Aim - Main.screenPosition;
                Main.mouseX = PlayerInput.MouseX = (int)MathHelper.Clamp(screen.X, -4000f, 8000f);
                Main.mouseY = PlayerInput.MouseY = (int)MathHelper.Clamp(screen.Y, -4000f, 8000f);
                Main.mouseLeft = Copy.controlUseItem;
                Main.mouseRight = Copy.controlUseTile;

                // Calamity keeps its own record of the cursor, for the player at the keyboard only: kept for the copy too.
                MimicCalamity.SyncMouse(Copy, Aim, Copy.controlUseTile);
                Main.mouseLeftRelease = true;
                Main.mouseRightRelease = true;
                Main.blockMouse = false;
                Main.HoveringOverAnNPC = false;
                Main.SmartInteractShowingGenuine = false;

                Empty.TurnToAir();
                Main.mouseItem = Empty;
            }

            public static void Exit()
            {
                if (--depth > 0)
                    return;

                depth = 0;
                Main.myPlayer = myPlayer;
                Main.mouseX = mouseX;
                Main.mouseY = mouseY;
                PlayerInput.MouseX = inputX;
                PlayerInput.MouseY = inputY;
                Main.mouseLeft = left;
                Main.mouseRight = right;
                Main.mouseLeftRelease = leftRelease;
                Main.mouseRightRelease = rightRelease;
                Main.blockMouse = block;
                Main.HoveringOverAnNPC = hoverNPC;
                Main.SmartInteractShowingGenuine = smartShowing;
                Main.mouseItem = mouseItem;
            }
        }

        private static void UseAsLocal(On_Player.orig_ItemCheckWrapped orig, Player self, int i)
        {
            if (!Is(self))
            {
                orig(self, i);
                return;
            }

            Scope.Enter();

            try
            {
                orig(self, i);
            }
            finally
            {
                Scope.Exit();
            }
        }

        private static void RightClickAsLocal(On_Player.orig_ItemCheck_ManageRightClickFeatures orig, Player self)
        {
            if (!Is(self))
            {
                orig(self);
                return;
            }

            Scope.Enter();

            try
            {
                orig(self);
            }
            finally
            {
                Scope.Exit();
            }
        }

        private static void ShotAsLocal(On_Projectile.orig_Update orig, Projectile self, int i)
        {
            if (Copy == null || !self.active || self.owner != Slot)
            {
                orig(self, i);
                return;
            }

            Scope.Enter();

            try
            {
                orig(self, i);
            }
            finally
            {
                Scope.Exit();
            }
        }

        // ---- seen ----------------------------------------------------------------------------------------------

        /// <summary>
        /// The potions it has left, beside where the game draws its life when it is hurt (Main.HealthBarDrawSettings:
        /// under it, or over it; under it when the bars are off).
        /// </summary>
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int at = layers.FindIndex(layer => layer.Name == "Vanilla: Entity Health Bars");

            if (at < 0)
                return;

            layers.Insert(at + 1, new LegacyGameInterfaceLayer("PisAndDag: Everclear Copy", () =>
            {
                DrawPotions();
                return true;
            }, InterfaceScaleType.Game));
        }

        private static void DrawPotions()
        {
            Player c = Copy;

            if (c == null || !c.active || c.dead || Main.gameMenu || Main.hideUI)
                return;

            float y = Main.HealthBarDrawSettings == 2 ? c.position.Y - 20f : c.position.Y + c.height + 10f;
            Vector2 at = new Vector2(c.Center.X - 30f, y + 8f + c.gfxOffY) - Main.screenPosition;
            Texture2D potion = TextureAssets.Item[ItemID.SuperHealingPotion].Value;
            Color light = Lighting.GetColor(c.Center.ToTileCoordinates());
            float shade = Math.Max(0.6f, (light.R + light.G + light.B) / (3f * 255f));

            Main.spriteBatch.Draw(potion, at, null, Color.White * shade, 0f, potion.Size() / 2f, 0.55f, SpriteEffects.None, 0f);
            Utils.DrawBorderString(Main.spriteBatch, c.CountItem(ItemID.SuperHealingPotion).ToString(), at + new Vector2(-10f, -6f), Color.White * shade, 0.7f, 1f);
        }
    }

    /// <summary>
    /// Every hit of the copy's is its owner's too (NPC.ApplyInteraction), marked before the blow lands: in a world of
    /// one the game asks whether the player at the keyboard ever touched what died before it drops anything
    /// (NPC.AnyInteractions), and loot is made as the owner (MimicSystem.LootAsOwner). So what the copy kills, even with
    /// its first blow, or what its poisons finish, drops what it would for the owner, and counts as theirs.
    /// </summary>
    public class MimicCredit : GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (projectile.owner == MimicSystem.Slot && MimicSystem.Exists && MimicSystem.Owner >= 0)
                npc.ApplyInteraction(MimicSystem.Owner);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (MimicSystem.Is(player) && MimicSystem.Owner >= 0)
                npc.ApplyInteraction(MimicSystem.Owner);
        }
    }
}
