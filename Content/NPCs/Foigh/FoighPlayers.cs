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
        }

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            if (faceAsked > 0)
                faceAsked--;

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

            if (!found)
                return;

            if (Player.mount.Active)
                Player.mount.Dismount(Player);

            Player.RemoveAllGrapplingHooks();
            Player.pulley = false;
            Player.gravDir = 1f;

            Player.Bottom = feet;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
            Player.direction = -side;

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

        /// <summary>Facing him: asked for by the scene, and all through the walk up but the walking itself.</summary>
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

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

        public bool Held => heldBy >= 0 && heldBy < Main.maxNPCs && heldTicks > 0 && !Player.dead
            && Main.npc[heldBy].active && Main.npc[heldBy].ModNPC is Foigh;

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
        }

        public override void OnEnterWorld()
        {
            heldBy = -1;
            heldTicks = 0;
            shoved = 0;
        }

        public override void ResetEffects()
        {
            if (shoved > 0)
                shoved--;

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
        }

        public override void PreUpdateMovement()
        {
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

        public override bool CanUseItem(Item item) => !Held;

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
}
