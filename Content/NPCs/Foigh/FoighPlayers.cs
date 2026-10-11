using System;
using Microsoft.Xna.Framework;
using PisAndDag.Content.Combat;
using PisAndDag.Content.NPCs.DonaQuinn;
using PisAndDag.Content.PADplayer;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PisAndDag.Content.NPCs.Foigh
{
    /// <summary>
    /// The player's side of Foigh's road: the walk up to him, and facing him while he talks.
    /// And of the passages into his second phase and his third: put down a way off him,
    /// under the black (<see cref="PutOff"/>); their back kept to him while he is behind
    /// them (<see cref="FaceAway"/>); and, once he has cut them off, their wings not drawn
    /// for as long as he is there (they are in their slot still: Foigh.WingsCut, which is
    /// also what stops them flying, in PisPlayer).
    ///
    /// THE WALK UP is a cut from play into a scene, done as a fade (Cinematic.Fade): the
    /// picture goes to black; under the black the player is put down behind him, twenty-eight
    /// tiles off, on their feet, facing him, with the bars in; the picture comes back up on
    /// that, and the player walks the rest of the way, slowly, on their own, and stops twenty
    /// tiles off him. The server is told they are there (Foigh.Word.Arrived), and he turns.
    ///
    /// From the first moment of it: no weapon, no building, nothing able to touch them, off
    /// any mount and hook, and a walk whatever they have on their feet. Asked for every tick
    /// by his scene, and gone when it stops asking.
    /// </summary>
    public class FoighScenePlayer : ModPlayer
    {
        /// <summary>The fade out, the black, the fade in: ticks.</summary>
        private const int FadeOutTicks = 30;
        private const int DarkTicks = 16;
        private const int FadeInTicks = 36;

        /// <summary>The tick of the black the player is moved on: by then the black is on screen.</summary>
        private const int CutAt = 3;

        /// <summary>The walk: pixels a tick at most (a run is three), and how quickly it gets up to that.</summary>
        private const float WalkPace = 1.6f;
        private const float WalkAccel = 0.06f;

        /// <summary>The walk lets go this short of the stop: the last of it is them slowing down.</summary>
        private const float Coast = 6f;

        /// <summary>A walk that has not got there in this long has got as far as it is going to.</summary>
        private const int WalkLongest = 420;

        private enum Step
        {
            None,
            FadeOut,
            Dark,
            FadeIn,
            Walk,
            There,
        }

        private static int approachTo = -1;
        private static int approachAsked;

        /// <summary>The side of him the player comes up on: behind him.</summary>
        private static int side = 1;

        private static Step step;
        private static int stepTicks;
        private static float blackness;
        private static bool reported;

        private static int faceTo = -1;
        private static int faceAsked;

        private static int awayFrom = -1;
        private static int awayAsked;

        private int stuck;

        /// <summary>The walk is done: they are standing off him, facing him.</summary>
        public static bool There => step == Step.There;

        /// <summary>The walk up to him, this tick: asked every tick by his scene for as long as it wants it.</summary>
        public static void Approach(Foigh foigh)
        {
            NPC npc = foigh.NPC;

            if (npc.whoAmI != approachTo)
            {
                ResetApproach();
                side = -foigh.Faces;
            }

            approachTo = npc.whoAmI;
            approachAsked = 2;
        }

        /// <summary>This machine's player turns to face him, this tick.</summary>
        public static void FaceToward(NPC npc)
        {
            faceTo = npc.whoAmI;
            faceAsked = 2;
        }

        /// <summary>This machine's player keeps their back to him, this tick: he is behind them, wherever he is.</summary>
        public static void FaceAway(NPC npc)
        {
            awayFrom = npc.whoAmI;
            awayAsked = 2;
        }

        private static void ResetApproach()
        {
            step = Step.None;
            stepTicks = 0;
            blackness = 0f;
            reported = false;
        }

        private static Foigh Him(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];
            return npc.active ? npc.ModNPC as Foigh : null;
        }

        /// <summary>The walk up has hold of this player.</summary>
        private bool Approaching => Player.whoAmI == Main.myPlayer && step != Step.None && Him(approachTo) != null;

        public override void OnEnterWorld()
        {
            ResetApproach();
            approachTo = -1;
            approachAsked = 0;
            faceTo = -1;
            faceAsked = 0;
            awayFrom = -1;
            awayAsked = 0;
        }

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (faceAsked > 0)
                faceAsked--;

            if (awayAsked > 0)
                awayAsked--;

            Foigh him = Him(approachTo);

            if (approachAsked <= 0 || him == null)
            {
                if (step != Step.None)
                    ResetApproach();

                approachTo = -1;
                return;
            }

            approachAsked--;
            stepTicks++;

            switch (step)
            {
                case Step.None:
                    Next(Step.FadeOut);
                    break;

                case Step.FadeOut:
                    blackness = Smooth(stepTicks / (float)FadeOutTicks);

                    if (stepTicks >= FadeOutTicks)
                    {
                        blackness = 1f;
                        Next(Step.Dark);
                    }
                    break;

                case Step.Dark:
                    blackness = 1f;

                    if (stepTicks == CutAt)
                        PutDown(him.NPC);

                    if (stepTicks >= DarkTicks)
                        Next(Step.FadeIn);
                    break;

                case Step.FadeIn:
                    blackness = 1f - Smooth(stepTicks / (float)FadeInTicks);

                    if (stepTicks >= FadeInTicks)
                    {
                        blackness = 0f;
                        Next(Step.Walk);
                    }
                    break;

                case Step.Walk:
                    if (Arrived(him.NPC) || stepTicks >= WalkLongest)
                        Next(Step.There);
                    break;

                case Step.There:
                    if (!reported)
                    {
                        reported = true;
                        him.Tell(Foigh.Word.Arrived);
                    }
                    break;
            }

            if (blackness > 0f)
                Cinematic.Fade(blackness);
        }

        private static void Next(Step to)
        {
            step = to;
            stepTicks = 0;
        }

        private static float Smooth(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Within the coast of the stop, or nearer him than it.</summary>
        private bool Arrived(NPC him)
        {
            float stopX = him.Center.X + side * Foigh.StopAt;
            return (Player.Center.X - stopX) * side <= Coast;
        }

        /// <summary>
        /// Under the black: the player put down behind him, twenty-eight tiles off or the
        /// nearest to that with ground to stand on, on their feet, facing him, off any mount,
        /// hook or rope, the right way up. Where there is nowhere, they stay where they are
        /// and walk from there.
        /// </summary>
        private void PutDown(NPC him)
        {
            Vector2 feet = Vector2.Zero;
            bool found = false;

            for (float off = Foigh.PutDownAt; off >= Foigh.StopAt + 16f && !found; off -= 16f)
            {
                float x = him.Center.X + side * off;

                if (DonaQuinnEncounter.Ground(x, him.Bottom.Y, Player.width, Player.height, 1, 10, out float ground)
                    && Collision.CanHitLine(new Vector2(x, ground - Player.height), 2, 2, him.Top, 2, 2))
                {
                    feet = new Vector2(x, ground);
                    found = true;
                }
            }

            if (found)
                StandAt(feet, -side);
        }

        /// <summary>
        /// Under the black, between his first phase and his second: this machine's player
        /// put down a long way off him. On the side of him they are already on, if there is
        /// ground that far off there; on the other, if not; and with none that far off on
        /// either, nearer, two tiles at a time, down to the least. On their feet, facing him.
        /// False, and they stay where they are, if there is nowhere at all.
        /// </summary>
        public bool PutOff(NPC him, float far, float least)
        {
            if (Player.whoAmI != Main.myPlayer)
                return false;

            int first = Player.Center.X >= him.Center.X ? 1 : -1;

            for (float off = far; off >= least; off -= 32f)
            {
                for (int turn = 0; turn < 2; turn++)
                {
                    int way = turn == 0 ? first : -first;
                    float x = him.Center.X + way * off;

                    if (!DonaQuinnEncounter.Ground(x, him.Bottom.Y, Player.width, Player.height, 1, 10, out float ground))
                        continue;

                    StandAt(new Vector2(x, ground), -way);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The player put on their feet here, facing this way: off any mount, hook or rope,
        /// the right way up, up off the ground if they had been thrown down on it, and still.
        /// (His end puts them down so too: FoighEndPlayer.)
        /// </summary>
        internal void StandAt(Vector2 feet, int facing)
        {
            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.RemoveAllGrapplingHooks();
            Player.pulley = false;
            Player.gravDir = 1f;
            Player.GetModPlayer<RagdollPlayer>().StandNow();

            Player.Bottom = feet;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
            Player.direction = facing;

            // Where they are now, for everyone else, without waiting for the next time it goes.
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
        }

        public override void ResetEffects()
        {
            if (!Approaching)
                return;

            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (!Approaching)
            {
                stuck = 0;
                return;
            }

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlInv = false;

            if (step != Step.Walk)
            {
                stuck = 0;
                return;
            }

            // Toward him, from behind him.
            if (side < 0)
                Player.controlRight = true;
            else
                Player.controlLeft = true;

            // Something in the way that a step will not take: over it.
            stuck = stepTicks > 12 && Math.Abs(Player.velocity.X) < 0.3f && Player.velocity.Y == 0f ? stuck + 1 : 0;

            if (stuck > 8)
                Player.controlJump = true;
        }

        /// <summary>A walk, whatever is on their feet: vanilla's own run, held to a walking pace.</summary>
        public override void PostUpdateRunSpeeds()
        {
            if (!Approaching || step != Step.Walk)
                return;

            Player.maxRunSpeed = WalkPace;
            Player.accRunSpeed = WalkPace;
            Player.runAcceleration = WalkAccel;
        }

        public override bool CanUseItem(Item item) => !Approaching;

        public override void PostUpdateEquips()
        {
            if (!Approaching)
                return;

            // Nothing may touch them while they are not theirs to move.
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
            Player.immuneAlpha = 0;
        }

        /// <summary>
        /// Their wings are not drawn while one of him is there who has cut them off.
        /// Everybody's: nobody flies while he is (PisPlayer), and wings that do nothing
        /// are not shown doing it. They are in their slot still, and back when he is gone.
        /// </summary>
        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (Foigh.WingsCut)
                PlayerDrawLayers.Wings.Hide();
        }

        /// <summary>Facing him: asked for by the scene, and all through the walk up but the walking itself. Or their back to him, where the scene asks that.</summary>
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            NPC behind = awayAsked > 0 ? Him(awayFrom)?.NPC : null;

            if (behind != null)
            {
                if (Math.Abs(behind.Center.X - Player.Center.X) >= 4f)
                    Player.direction = -Math.Sign(behind.Center.X - Player.Center.X);

                return;
            }

            NPC him = faceAsked > 0 ? Him(faceTo)?.NPC : null;

            if (him == null && Approaching && step != Step.Walk)
                him = Him(approachTo)?.NPC;

            if (him == null || Math.Abs(him.Center.X - Player.Center.X) < 4f)
                return;

            Player.direction = Math.Sign(him.Center.X - Player.Center.X);
        }
    }

    /// <summary>
    /// A player in Foigh's hand, or on his sword, or thrown back off it.
    ///
    /// HELD. He asks every tick, with where they are and how they lie (Hold); they cannot
    /// move or use anything, and are put there. The tick he lets go (LetGo), or stops asking,
    /// they are out: thrown the way he threw them, as a ragdoll, down for a second after they
    /// land; or only dropped, if he never got that far.
    ///
    /// THROWN BACK. A swing of theirs met by his sword (Shove): straight onto their speed, so
    /// no shield turns it, and for a few ticks their feet are not theirs.
    ///
    /// STRUCK, from his second phase, by request: a hit of his on one who hangs from a hook
    /// lets it go, and for a second they throw no other; a hit on one who is off the ground
    /// shuts their wings, their rocket boots and whatever else would hold them up (a booster,
    /// a carpet, a mount that flies) for two seconds, and they fall. What time their wings
    /// and boots had left is theirs again after it, if they are still in the air.
    ///
    /// PULLED IN. On his hook and off the ground (Drag): he asks every tick, with where to;
    /// their own hooks are let go, their feet are not theirs, and they come to him at a
    /// speed of their own. Their hands are theirs: the blow that waits for them can be met.
    ///
    /// Each machine's own player only: their own machine moves them, as it moves them always.
    /// </summary>
    public class FoighGripPlayer : ModPlayer
    {
        /// <summary>
        /// The sword's own immunity slot while they are on it: nothing else's immunity stops
        /// it and it gives none of the general kind, so it lands whatever had just hit them.
        /// </summary>
        private const int Slot = ImmunityCooldownID.WrongBugNet;

        /// <summary>Thrown off the sword: how long they lie where they land.</summary>
        public const int Lie = 60;

        /// <summary>Only dropped: a moment in which nothing else can have them.</summary>
        private const int Grace = 30;

        private int heldBy = -1;
        private int heldTicks;
        private Vector2 at;
        private float turn;

        private int shoved;

        /// <summary>Struck: ticks no hook of theirs is thrown, and ticks nothing holds them up.</summary>
        public const int HookLock = 60;
        public const int FlightLock = 120;

        /// <summary>Pulled in on his hook: pixels a tick, and how near the place they stop being pulled.</summary>
        public const float DragSpeed = 15f;
        public const float DragReach = 20f;

        /// <summary>The update of the game each lasts through; none at nothing.</summary>
        private uint hookLockedUntil;
        private uint groundedUntil;

        /// <summary>What their wings and their boots had left as they were shut; and whether that is still to be given back.</summary>
        private float wingsKept;
        private int rocketsKept;
        private bool owedBack;

        private int draggedBy = -1;
        private int dragTicks;
        private Vector2 dragTo;

        public bool Held => heldBy >= 0 && heldBy < Main.maxNPCs && heldTicks > 0 && !Player.dead
            && Main.npc[heldBy].active && Main.npc[heldBy].ModNPC is Foigh;

        /// <summary>No hook of theirs is thrown just now; nothing holds them up just now.</summary>
        public bool HookLocked => hookLockedUntil != 0 && Main.GameUpdateCount <= hookLockedUntil;
        public bool Grounded => groundedUntil != 0 && Main.GameUpdateCount <= groundedUntil;

        /// <summary>On his hook, and being pulled in to him.</summary>
        public bool Dragged => draggedBy >= 0 && draggedBy < Main.maxNPCs && dragTicks > 0 && !Player.dead
            && Main.npc[draggedBy].active && Main.npc[draggedBy].ModNPC is Foigh;

        /// <summary>
        /// A hit of his has landed on this machine's player (asked where the hit is
        /// worked out: Foigh.Pierce, Through). From his second phase: a hook of theirs
        /// in something is let go, and no other is thrown for a while; and off the
        /// ground, nothing holds them up for a while.
        /// </summary>
        public void Struck()
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead || !Foigh.PastFirst)
                return;

            if (Foigh.Hooked(Player))
            {
                Player.RemoveAllGrapplingHooks();
                hookLockedUntil = Main.GameUpdateCount + HookLock;
            }

            if (!Foigh.Aloft(Player))
                return;

            // (Struck again while they are shut, what they had left is still what it was when they were shut.)
            if (!Grounded)
            {
                wingsKept = Player.wingTime;
                rocketsKept = Player.rocketTime;
            }

            groundedUntil = Main.GameUpdateCount + FlightLock;
            owedBack = true;

            if (Flies(Player.mount))
                Player.mount.Dismount(Player);
        }

        /// <summary>Whether a mount is one that holds its rider up.</summary>
        private static bool Flies(Mount mount) => mount.Active && (mount.CanFly() || mount.CanHover());

        /// <summary>Pulled in to this place by this one, for the next few ticks. Asked every tick for as long as he pulls.</summary>
        public void Drag(int npcIndex, Vector2 to)
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead || Held)
                return;

            if (draggedBy != npcIndex || dragTicks <= 0)
            {
                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
                Player.pulley = false;
            }

            draggedBy = npcIndex;
            dragTicks = 3;
            dragTo = to;
        }

        /// <summary>Held by this one for the next few ticks, here, lying so. Asked every tick for as long as he has them.</summary>
        public void Hold(int npcIndex, Vector2 middle, float lying)
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            if (heldBy != npcIndex || heldTicks <= 0)
            {
                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
                Player.pulley = false;
            }

            heldBy = npcIndex;
            heldTicks = 3;
            at = middle;
            turn = lying;
        }

        /// <summary>Let go of, now, and thrown with this.</summary>
        public void LetGo(int npcIndex, Vector2 thrown)
        {
            if (Player.whoAmI != Main.myPlayer || heldBy != npcIndex || heldTicks <= 0)
                return;

            Out(thrown);
        }

        /// <summary>Thrown back with this, and for this many ticks their feet are not theirs.</summary>
        public void Shove(Vector2 velocity, int ticks)
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead || Held)
                return;

            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.RemoveAllGrapplingHooks();
            Player.velocity = velocity;
            Player.fallStart = (int)(Player.position.Y / 16f);

            shoved = ticks;
        }

        /// <summary>
        /// The sword through this machine's player, through armour, whatever had just hit
        /// them and whatever they would have dodged it with: they are on it.
        /// </summary>
        public static void Through(Player me, string death, int damage, int direction)
        {
            if (me.whoAmI != Main.myPlayer || me.dead)
                return;

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + death, me.name));

            me.hurtCooldowns[Slot] = 0;
            me.Hurt(reason, damage, direction, false, false, Slot, false, 0f, 1f, 0f);

            // A hit of his like any other, for what it takes from them (Struck).
            me.GetModPlayer<FoighGripPlayer>().Struck();
        }

        /// <summary>
        /// His hook into this machine's player, through armour: on the sword's own slot,
        /// so that it leaves them none of the grace a hurt leaves, against the blow it
        /// brings. (One whom a hurt still shields it goes through: whoever asks this has
        /// looked.) It can be dodged as anything can. True if it bit.
        /// </summary>
        public static bool Bite(Player me, string death, int damage, int direction)
        {
            if (me.whoAmI != Main.myPlayer || me.dead)
                return false;

            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                NetworkText.FromKey("Mods.PisAndDag.DeathText." + death, me.name));

            me.hurtCooldowns[Slot] = 0;

            if (me.Hurt(reason, damage, direction, false, false, Slot, true, 0f, 1f, 0f) <= 0)
                return false;

            me.GetModPlayer<FoighGripPlayer>().Struck();
            return true;
        }

        public override void OnEnterWorld()
        {
            heldBy = -1;
            heldTicks = 0;
            shoved = 0;
            Free();
        }

        /// <summary>Nothing of his is on them any more: no lock, no pull.</summary>
        private void Free()
        {
            hookLockedUntil = 0;
            groundedUntil = 0;
            owedBack = false;
            draggedBy = -1;
            dragTicks = 0;
        }

        public override void UpdateDead()
        {
            Free();
        }

        public override void ResetEffects()
        {
            if (shoved > 0)
                shoved--;

            if (dragTicks > 0 && --dragTicks <= 0)
            {
                // He has stopped pulling: what is left of the way in is theirs, slower.
                draggedBy = -1;
                Player.velocity *= 0.3f;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            if (!Held)
            {
                // He stopped asking without letting go: they are dropped.
                if (heldBy >= 0 && heldTicks > 0)
                    Out(Vector2.Zero);

                heldBy = -1;
                heldTicks = 0;
                return;
            }

            heldTicks--;

            if (heldTicks <= 0)
            {
                Out(Vector2.Zero);
                return;
            }

            // Every item, including the ones that do not go through the controls below.
            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (HookLocked || Dragged)
                Player.controlHook = false;

            if (Dragged && !Held)
            {
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;
                Player.controlJump = false;
                Player.controlMount = false;
            }

            if (Held)
            {
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;
                Player.controlJump = false;
                Player.controlHook = false;
                Player.controlMount = false;
                Player.controlUseItem = false;
                Player.controlUseTile = false;
                Player.controlThrow = false;
                return;
            }

            if (shoved <= 0)
                return;

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
        }

        /// <summary>Thrown back: no braking while it lasts, or the shove is over in a tick.</summary>
        public override void PostUpdateRunSpeeds()
        {
            if (shoved > 0 && !Held)
                Player.runSlowdown = 0.05f;

            // Struck off the ground: nothing holds them up. Here, after the game has gone
            // over what is worn and before it flies anybody by it (Player.Update, 1.4.4:
            // with no wings and no boots it empties what time both had left itself).
            if (Grounded)
            {
                Player.wingsLogic = 0;
                Player.rocketBoots = 0;
                Player.empressBrooch = false;
                Player.carpet = false;
            }
            else if (owedBack)
            {
                owedBack = false;

                // Still in the air: what they had left when it was shut. (On the ground the game has filled both again.)
                if (Player.velocity.Y != 0f)
                {
                    Player.wingTime = Math.Max(Player.wingTime, wingsKept);
                    Player.rocketTime = Math.Max(Player.rocketTime, rocketsKept);
                }
            }
        }

        public override void PreUpdateMovement()
        {
            if (Grounded && Flies(Player.mount))
                Player.mount.Dismount(Player);

            if (Dragged && !Held)
            {
                // Along his chain to where he wants them, whatever they had been doing;
                // there, nearly still. Tiles stop them as they stop anything.
                Vector2 off = dragTo - Player.Center;
                float far = off.Length();

                Player.velocity = far > DragReach ? off * (Math.Min(DragSpeed, far) / far) : Player.velocity * 0.5f;
                Player.fallStart = (int)(Player.position.Y / 16f);
                return;
            }

            if (!Held)
                return;

            // Where he has them; or, if that is in a wall or a ceiling (his arm and his sword go
            // through them, a player must not be let go of there), as near it as there is room
            // for them, back toward him.
            Vector2 corner = at - Player.Size * 0.5f;

            if (Collision.SolidCollision(corner, Player.width, Player.height))
            {
                Vector2 his = Main.npc[heldBy].Center - Player.Size * 0.5f;

                for (int i = 1; i <= 8; i++)
                {
                    Vector2 nearer = Vector2.Lerp(corner, his, i / 8f);

                    if (i == 8 || !Collision.SolidCollision(nearer, Player.width, Player.height))
                    {
                        corner = nearer;
                        break;
                    }
                }
            }

            Player.position = corner;
            Player.velocity = Vector2.Zero;

            Player.fullRotation = turn;
            Player.fullRotationOrigin = Player.Size * 0.5f;

            // Or the whole of it counts as a fall the moment he lets go.
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        public override bool CanUseItem(Item item)
        {
            if (Held)
                return false;

            // (A hook held and thrown as a thing is a hook all the same.)
            return !(HookLocked || Dragged) || item.shoot <= ProjectileID.None || item.shoot >= Main.projHook.Length || !Main.projHook[item.shoot];
        }

        /// <summary>Out of his hand or off his sword: thrown, as a ragdoll; or dropped, upright, with a moment's grace.</summary>
        private void Out(Vector2 thrown)
        {
            heldBy = -1;
            heldTicks = 0;

            Player.fullRotation = 0f;
            Player.fallStart = (int)(Player.position.Y / 16f);

            if (Player.dead)
                return;

            if (thrown != Vector2.Zero)
            {
                Player.GetModPlayer<RagdollPlayer>().Throw(thrown, RagdollStyle.Spin, Lie);
                return;
            }

            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, Grace);
            Player.velocity = new Vector2(0f, -2f);
        }
    }

    /// <summary>
    /// What the game made of a player's weight and of their jump, this tick: kept for his
    /// sum of where a player in the air is going (Foigh.Ahead).
    ///
    /// The game settles these early in a player's tick, and by the end of it has changed
    /// some of them or lost them: one who falls slowly has their weight lightened in place
    /// as they fall, a flying carpet under them takes their slow fall off for the tick,
    /// and how fast and how long a jump goes up is kept in one place for whichever player
    /// the game moved last. PostUpdateRunSpeeds is the last a mod hears of a player after
    /// the game has settled all of these and before it moves them by them (Player.Update,
    /// 1.4.4): so they are read there, as they are about to be used.
    ///
    /// Every machine, every player: each machine runs every player's tick. (Not one on a
    /// hook or a rope: the game does not come this way for them, and he takes them to stay
    /// where they hang.)
    /// </summary>
    public class FoighAirPlayer : ModPlayer
    {
        /// <summary>What the game adds to their fall each tick, before any lightening (Player.gravity).</summary>
        public float Weight { get; private set; } = Terraria.Player.defaultGravity;

        /// <summary>How fast a jump of theirs goes up, and for how many ticks with the key held (Player.jumpSpeed, Player.jumpHeight).</summary>
        public float JumpSpeed { get; private set; } = 5.01f;
        public int JumpTicks { get; private set; } = 15;

        /// <summary>Whether they fall slowly (Player.slowFall), a carpet under them or not.</summary>
        public bool FallsSlowly { get; private set; }

        public override void PostUpdateRunSpeeds()
        {
            Weight = Player.gravity;
            JumpSpeed = Terraria.Player.jumpSpeed;
            JumpTicks = Terraria.Player.jumpHeight;
            FallsSlowly = Player.slowFall;
        }
    }

    /// <summary>
    /// The player's side of Foigh's end (Foigh.End).
    ///
    /// On their own machine, asked for every tick by his scene, and gone when it stops
    /// asking: held for all of it (by Cinematic.Hold where nothing moves them, and by this
    /// where something does: no weapon, no building, no keys of their own), nothing able to
    /// touch them, no life lost by itself; put down before him under the black
    /// (<see cref="PutAhead"/>); walked where his end walks them, at a walk, whatever is on
    /// their feet (<see cref="WalkTo"/>); turned to him (<see cref="FaceHim"/>).
    ///
    /// On every machine, for every player: their arms as his end has them, with his sword
    /// in their hands (Foigh.EndHandsFor), set after the game has set them for the tick
    /// (PostItemCheck: the game lets go of both arms as every item check begins); and
    /// nothing of their own in their hand drawn while it does.
    /// </summary>
    public class FoighEndPlayer : ModPlayer
    {
        /// <summary>A walk: pixels a tick at most, and how quickly it gets up to that (as the road's walk up: FoighScenePlayer).</summary>
        private const float WalkPace = 1.6f;
        private const float WalkAccel = 0.06f;

        /// <summary>A walk lets go this short of where it is to: the last of it is them slowing down (Foigh.End's EndCoast is the same).</summary>
        private const float Coast = 6f;

        /// <summary>The ticks of standing still on the ground, walking, before what is in the way is jumped.</summary>
        private const int StuckTicks = 20;

        private static int endOf = -1;
        private static int endAsked;

        private static int faceTo = -1;
        private static int faceAsked;

        private static float walkTo;
        private static int walkAsked;

        private int stuck;

        /// <summary>This machine's player is in his end, this tick: asked every tick by his scene.</summary>
        public static void Ask(NPC npc)
        {
            endOf = npc.whoAmI;
            endAsked = 2;
        }

        /// <summary>This machine's player turns to face him, this tick; given nothing, they are not turned (and keep the way they face).</summary>
        public static void FaceHim(NPC npc)
        {
            if (npc == null)
            {
                faceAsked = 0;
                return;
            }

            faceTo = npc.whoAmI;
            faceAsked = 2;
        }

        /// <summary>This machine's player walks toward this x, this tick, and stops near it.</summary>
        public static void WalkTo(float x)
        {
            walkTo = x;
            walkAsked = 2;
        }

        private static NPC Him(int index)
        {
            if (index < 0 || index >= Main.maxNPCs)
                return null;

            NPC npc = Main.npc[index];
            return npc.active && npc.ModNPC is Foigh ? npc : null;
        }

        /// <summary>His end has hold of this player, on their own machine.</summary>
        private bool InEnd => Player.whoAmI == Main.myPlayer && endAsked > 0 && Him(endOf) != null;

        /// <summary>And is walking them, this tick, and they are not there yet.</summary>
        private bool Walking => InEnd && walkAsked > 0 && Math.Abs(walkTo - Player.Center.X) > Coast;

        public override void OnEnterWorld()
        {
            endOf = -1;
            endAsked = 0;
            faceTo = -1;
            faceAsked = 0;
            walkAsked = 0;
        }

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (endAsked > 0)
                endAsked--;

            if (faceAsked > 0)
                faceAsked--;

            if (walkAsked > 0)
                walkAsked--;
        }

        /// <summary>
        /// Under the black, at his end: this machine's player put down before him, this far
        /// off the way he faces, or nearer, a tile at a time, down to the least, where there
        /// is ground with nothing between them and him; with none on that side, the same on
        /// the other. On their feet, facing him. False, and they stay where they are, if
        /// there is nowhere at all.
        /// </summary>
        public bool PutAhead(NPC him, int way, float far, float least)
        {
            if (Player.whoAmI != Main.myPlayer)
                return false;

            for (int turn = 0; turn < 2; turn++)
            {
                int side = turn == 0 ? way : -way;

                for (float off = far; off >= least; off -= 16f)
                {
                    float x = him.Center.X + side * off;

                    if (!DonaQuinnEncounter.Ground(x, him.Bottom.Y, Player.width, Player.height, 1, 10, out float ground)
                        || !Collision.CanHitLine(new Vector2(x, ground - Player.height), 2, 2, him.Top, 2, 2))
                        continue;

                    Player.GetModPlayer<FoighScenePlayer>().StandAt(new Vector2(x, ground), -side);
                    return true;
                }
            }

            return false;
        }

        public override void ResetEffects()
        {
            if (!InEnd)
                return;

            Player.noItems = true;
            Player.noBuilding = true;
        }

        public override void SetControls()
        {
            if (!InEnd)
            {
                stuck = 0;
                return;
            }

            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
            Player.controlInv = false;

            if (!Walking)
            {
                stuck = 0;
                return;
            }

            if (walkTo > Player.Center.X)
                Player.controlRight = true;
            else
                Player.controlLeft = true;

            // Something in the way that a step will not take: over it.
            stuck = Math.Abs(Player.velocity.X) < 0.3f && Player.velocity.Y == 0f ? stuck + 1 : 0;

            if (stuck > StuckTicks)
                Player.controlJump = true;
        }

        /// <summary>A walk, whatever is on their feet: vanilla's own run, held to a walking pace.</summary>
        public override void PostUpdateRunSpeeds()
        {
            if (!Walking)
                return;

            Player.maxRunSpeed = WalkPace;
            Player.accRunSpeed = WalkPace;
            Player.runAcceleration = WalkAccel;
        }

        public override bool CanUseItem(Item item) => !InEnd;

        public override void PostUpdateEquips()
        {
            if (!InEnd)
                return;

            // Nothing may touch them while they are not theirs to move.
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 2);
            Player.immuneNoBlink = true;
            Player.immuneAlpha = 0;
        }

        /// <summary>No life lost by itself while his end has them: nothing burning, poisoning or bleeding them takes any (what the game and the mods have put on lifeRegen by now).</summary>
        public override void UpdateBadLifeRegen()
        {
            if (InEnd && Player.lifeRegen < 0)
                Player.lifeRegen = 0;
        }

        /// <summary>Turned to him, where his scene asks it and they are not walking.</summary>
        public override void PostUpdate()
        {
            if (!InEnd || faceAsked <= 0 || Walking)
                return;

            NPC him = Him(faceTo);

            if (him == null || Math.Abs(him.Center.X - Player.Center.X) < 4f)
                return;

            Player.direction = Math.Sign(him.Center.X - Player.Center.X);
        }

        /// <summary>Their arms, where his end has them: every machine, every player.</summary>
        public override void PostItemCheck()
        {
            Foigh him = Foigh.EndFor(Player.whoAmI);

            if (him == null || !him.EndHandsFor(Player, out Foigh.EndHands hands))
                return;

            if (hands.Front)
                Player.SetCompositeArmFront(true, hands.FrontStretch, hands.FrontRotation);

            if (hands.Back)
                Player.SetCompositeArmBack(true, hands.BackStretch, hands.BackRotation);
        }

        /// <summary>Nothing of their own in their hand is drawn while his end has their arms.</summary>
        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            Foigh him = Foigh.EndFor(Player.whoAmI);

            if (him != null && him.EndHandsFor(Player, out _))
                PlayerDrawLayers.HeldItem.Hide();
        }
    }
}
