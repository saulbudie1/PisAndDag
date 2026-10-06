using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Items.Ranged;
using PisAndDag.Content.NPCs;
using PisAndDag.Content.Projectiles.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PisAndDag.Content.Combat
{
    /// <summary>
    /// What the Meat Hook's hook does with what it catches, read off the thing itself, and the
    /// numbers of it. The hook is MeatHook_Hook, thrown by the gun in the hand (MeatHookHoldOut);
    /// this file is the two ends of the chain: the enemy dragged (<see cref="MeatHookNPC"/>) and
    /// the wielder dragged (<see cref="MeatHookPlayer"/>).
    ///
    /// SMALL OR NOT (<see cref="Hauled"/>). An enemy is dragged to the wielder when the game
    /// lets it be knocked back (NPC.knockBackResist above 0), its hitbox is no giant's
    /// (ParryRules.GiantWidth by GiantHeight: three players wide, two tall), it is no boss and
    /// no part of one (ParryRules.BossOrPart), and its own AI can be stopped for as long as it
    /// hangs (PADStaggerNPC.CanReel: not a worm's segment, not one of the many parts of
    /// something, nothing a list says is never to be stopped). Anything else drags the wielder
    /// to it.
    ///
    /// Either way the hook holds for the same things: a bite of <see cref="BiteTicks"/>, the
    /// drag, and <see cref="HoldTicks"/> once what is dragged is there; <see cref="LatchLimit"/>
    /// at the most. It lets go sooner when what it drags is caught on something
    /// (<see cref="StuckTicks"/> of coming no nearer), when the chain is stretched past
    /// <see cref="Snap"/> of its reach, when what it holds dies, and when its wielder says so.
    /// </summary>
    public static class MeatHookRules
    {
        /// <summary>How far the hook goes from its wielder before it turns back: 50 tiles.</summary>
        public const float Reach = 50 * 16f;

        /// <summary>A chain stretched this much past its reach lets go of what it holds.</summary>
        public const float Snap = 1.15f;

        /// <summary>Ticks the hook sits in what it has caught before anything is dragged.</summary>
        public const int BiteTicks = 5;

        /// <summary>Ticks it holds on once what it drags is there.</summary>
        public const int HoldTicks = 48;

        /// <summary>Ticks it holds anything, at the most.</summary>
        public const int LatchLimit = 150;

        /// <summary>Ticks of coming no nearer after which it lets go.</summary>
        public const int StuckTicks = 24;

        /// <summary>Pixels a tick an enemy is dragged at, and its wielder.</summary>
        public const float HaulSpeed = 20f;
        public const float PullSpeed = 16f;

        /// <summary>
        /// Where the drag stops, from the wielder's middle to the nearest edge of the enemy's
        /// hitbox: an enemy dragged in hangs just clear of its wielder; a wielder dragged out
        /// stops short of what cannot be moved, in the gun's reach and out of its touch.
        /// </summary>
        public const float HaulStandoff = 30f;
        public const float PullStandoff = 56f;

        /// <summary>Ticks before the hook can be thrown again, counted from its coming back: 3 seconds if it caught something, 5 if it did not.</summary>
        public const int WaitAfterCatch = 3 * 60;
        public const int WaitAfterMiss = 5 * 60;

        /// <summary>Whether the hook sinks into this at all: an enemy's body, that something can hurt.</summary>
        public static bool Latches(NPC npc)
        {
            return npc.active && npc.life > 0 && !npc.friendly && !npc.dontTakeDamage && !NPCID.Sets.ProjectileNPC[npc.type];
        }

        /// <summary>Whether this one is dragged to the wielder. If not, the wielder is dragged to it.</summary>
        public static bool Hauled(NPC npc)
        {
            return npc.knockBackResist > 0f
                && npc.width <= ParryRules.GiantWidth && npc.height <= ParryRules.GiantHeight
                && !ParryRules.BossOrPart(npc)
                && PADStaggerNPC.CanReel(npc);
        }

        /// <summary>
        /// From the player's middle to the nearest point of the enemy's hitbox, and the way to
        /// it. Inside the hitbox the gap is none, and the way is to its middle.
        /// </summary>
        public static float Gap(Player player, NPC npc, out Vector2 toward)
        {
            Vector2 from = player.Center;
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new Vector2(MathHelper.Clamp(from.X, box.Left, box.Right), MathHelper.Clamp(from.Y, box.Top, box.Bottom));
            Vector2 way = nearest - from;
            float gap = way.Length();

            toward = gap > 0.01f ? way / gap : (npc.Center - from).SafeNormalize(Vector2.UnitX * player.direction);
            return gap;
        }

        /// <summary>
        /// This tick's step for something dragged across <paramref name="gap"/> to stop at
        /// <paramref name="standoff"/>: up to <paramref name="top"/> within a few ticks of the
        /// drag's start, easing over the last of the way, and never past where it stops.
        /// </summary>
        public static float Step(float gap, float standoff, float top, int ticks)
        {
            float left = gap - standoff;

            if (left <= 0f)
                return 0f;

            float ramp = MathHelper.Clamp(0.35f + ticks * 0.13f, 0f, 1f);
            return Math.Min(Math.Min(top * ramp, left * 0.4f + 2f), left);
        }
    }

    /// <summary>
    /// An enemy on the hook. For as long as the hook says it holds it (every tick, from the
    /// hook's own AI on every machine) its own AI is not run, it touches nobody, and once the
    /// bite is over it is dragged to whoever holds the gun.
    ///
    /// Its AI is stopped, not replaced: what it keeps (ai, localAI, its timers) is left as it
    /// was, and it goes on from there when it is let down, as with a parry's stagger
    /// (PADStaggerNPC). The drag is written into its position, as far as the tiles let it go,
    /// so nothing of it resists and nothing has to be put back; its own speed is held at
    /// nothing meanwhile (NPC.UpdateNPC adds the fall after the AI, so one with weight is
    /// given exactly the fall back).
    ///
    /// Nothing is sent for it: every machine has the hook, and the hook says. The world keeps
    /// the others in step by sending the enemy as it goes.
    /// </summary>
    public class MeatHookNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>Pixels a tick the chain lifts it over what stops it sideways: a ledge, the lip of a slope.</summary>
        private const float Lift = 8f;

        /// <summary>Ticks between the world's sendings of it as it is dragged.</summary>
        private const int SendEvery = 6;

        private int holder = -1;

        /// <summary>Ticks of the hook's hold left: one, said again every tick it holds.</summary>
        private int held;

        /// <summary>Past the bite: dragged, not only held.</summary>
        private bool dragged;

        /// <summary>It was on the hook last tick.</summary>
        private bool was;

        /// <summary>Ticks it has been dragged.</summary>
        private int ticks;

        public bool Held => held > 0;

        /// <summary>The hook holds it this tick, for this player; dragging it or, in the bite, not yet.</summary>
        public void Hold(int player, bool drag)
        {
            holder = player;
            held = 1;
            dragged = drag;
        }

        public override bool PreAI(NPC npc)
        {
            if (held > 0)
                return false;   // its AI does not run while it hangs

            if (was)
            {
                was = false;
                ticks = 0;
                LetDown(npc);
            }

            return true;
        }

        public override void PostAI(NPC npc)
        {
            if (held <= 0)
                return;

            held--;
            was = true;

            // Nothing of its own moves it. The game adds the fall after this, to one with weight.
            npc.velocity = new Vector2(0f, npc.noGravity ? 0f : -npc.gravity);

            if (!dragged || holder < 0 || holder >= Main.maxPlayers)
                return;

            Player player = Main.player[holder];

            if (!player.active || player.dead)
                return;

            float gap = MeatHookRules.Gap(player, npc, out Vector2 toward);
            float speed = MeatHookRules.Step(gap, MeatHookRules.HaulStandoff, MeatHookRules.HaulSpeed, ticks++);

            if (speed <= 0f)
                return;

            // toward is the way from the player to it; it goes the other way.
            Vector2 moved = Along(npc, -toward * speed);
            npc.position += moved;

            if (Main.netMode == NetmodeID.Server && ticks % SendEvery == 0)
                npc.netUpdate = true;

            ParryEffects.Skid(npc, moved);
        }

        /// <summary>
        /// As much of this step as the tiles let it take. Stopped sideways, it is lifted: the
        /// chain is above the ground it is dragged along, and takes it over a ledge. Platforms
        /// do not stop it. What goes through tiles anyway takes the whole step.
        /// </summary>
        private static Vector2 Along(NPC npc, Vector2 step)
        {
            if (npc.noTileCollide)
                return step;

            Vector2 moved = Collision.TileCollision(npc.position, step, npc.width, npc.height, true, true);

            if (Math.Abs(moved.X) + 0.5f >= Math.Abs(step.X))
                return moved;

            Vector2 up = Collision.TileCollision(npc.position, new Vector2(0f, -Lift), npc.width, npc.height, true, true);

            if (up.Y > -0.5f)
                return moved;

            Vector2 over = Collision.TileCollision(npc.position + up, new Vector2(step.X, 0f), npc.width, npc.height, true, true);
            return up + over;
        }

        /// <summary>
        /// Off the hook: thrown a little way from whoever held it, so that it does not come
        /// down on them. As much as the game lets it be thrown at all.
        /// </summary>
        private void LetDown(NPC npc)
        {
            float away = npc.direction == 0 ? 1f : -npc.direction;

            if (holder >= 0 && holder < Main.maxPlayers && Main.player[holder].active)
                away = npc.Center.X < Main.player[holder].Center.X ? -1f : 1f;

            float weight = MathHelper.Clamp(npc.knockBackResist * 1.5f, 0f, 1f);
            npc.velocity = new Vector2(away * 5f, npc.noGravity ? 0f : -3f) * weight;

            if (Main.netMode == NetmodeID.Server)
                npc.netUpdate = true;
        }

        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => held <= 0;

        public override bool CanHitNPC(NPC npc, NPC target) => held <= 0;
    }

    /// <summary>
    /// The Meat Hook's wielder: the wait before the hook can be thrown again, the drag to
    /// something too big to be dragged, and how the gun sits in the hands this tick.
    ///
    /// THE WAIT is kept here and not on the gun in the hand, which is gone whenever something
    /// else is held: putting the gun away does not shorten it. Counted on every machine from
    /// the hook's end (MeatHook_Hook.OnKill), so that the hook on another player's gun is
    /// drawn as it is; only the wielder's own machine asks it anything.
    ///
    /// THE DRAG is the game's own grapple in small (Player.GrappleMovement): the hook says,
    /// every tick it holds, what the wielder is dragged to, and for that tick their speed is
    /// the chain's (PreUpdateMovement, after the game has added the fall and before it moves
    /// them). No fall is counted on it. Inside the standoff they are free, and are dragged
    /// back to it if they leave it.
    ///
    /// THE POSE is in the player's own upright frame (facing right, gravity down), as Visus's
    /// is (VisusGrip): set by the gun in the hand every tick on every machine, cleared at the
    /// start of the next, and drawn by MeatHookLayer.
    /// </summary>
    public class MeatHookPlayer : ModPlayer
    {
        /// <summary>Ticks the hook's coming ready is shown for.</summary>
        public const int ReadyTicks = 16;

        // ---- the wait ---------------------------------------------------------------------------------

        /// <summary>Ticks until the hook can be thrown again, and what that count began at.</summary>
        public int Wait { get; private set; }
        public int WaitFrom { get; private set; }

        /// <summary>Ticks left of showing that it has just come ready.</summary>
        public int ReadyFor { get; private set; }

        /// <summary>The hook is back: the wait begins.</summary>
        public void StartWait(bool caught)
        {
            Wait = WaitFrom = caught ? MeatHookRules.WaitAfterCatch : MeatHookRules.WaitAfterMiss;
            ReadyFor = 0;
        }

        // ---- the drag ---------------------------------------------------------------------------------

        private int pulledTo = -1;

        /// <summary>Ticks of the hook's drag left: one, said again every tick it drags.</summary>
        private int pulled;

        /// <summary>Ticks dragged so far.</summary>
        private int pulling;

        /// <summary>The chain moved them last tick: it was taut.</summary>
        private bool taut;

        /// <summary>The hook drags this player to that enemy this tick.</summary>
        public void PullTo(int npc)
        {
            pulledTo = npc;
            pulled = 1;
        }

        // ---- the pose ---------------------------------------------------------------------------------

        /// <summary>The gun is in the hands this tick.</summary>
        public bool Held;

        /// <summary>The gun's angle, upright.</summary>
        public float Angle;

        /// <summary>The front hand, where the gun's grip is, upright.</summary>
        public Vector2 Hand;

        /// <summary>The hook lies under the barrel: it is not out.</summary>
        public bool HookHome = true;

        /// <summary>The gun in the hand says how it is held this tick.</summary>
        public void Hold(float angle, Vector2 hand, bool hookHome)
        {
            Held = true;
            Angle = angle;
            Hand = hand;
            HookHome = hookHome;
        }

        public override void ResetEffects()
        {
            Held = false;
        }

        public override void UpdateDead()
        {
            Held = false;
            pulled = 0;
            pulling = 0;
            taut = false;
        }

        public override void PreUpdateMovement()
        {
            if (pulled <= 0)
            {
                pulling = 0;
                taut = false;
                return;
            }

            pulled--;

            if (pulledTo < 0 || pulledTo >= Main.maxNPCs)
                return;

            NPC npc = Main.npc[pulledTo];

            if (!npc.active || npc.life <= 0)
                return;

            // As the game does for a grapple, on the machine whose player it is: off what is ridden, and off every other hook.
            if (pulling == 0 && Player.whoAmI == Main.myPlayer)
            {
                if (Player.mount.Active)
                    Player.mount.Dismount(Player);

                Player.RemoveAllGrapplingHooks();
            }

            // No fall is counted on the chain, there or on the way.
            Player.fallStart = (int)(Player.position.Y / 16f);

            float gap = MeatHookRules.Gap(Player, npc, out Vector2 toward);
            float speed = MeatHookRules.Step(gap, MeatHookRules.PullStandoff, MeatHookRules.PullSpeed, pulling++);

            // There: on the chain, and free inside its length. What speed the chain gave them ends with it
            // (left, they would drift on in the air for as long as it holds).
            if (speed <= 0f)
            {
                if (taut)
                    Player.velocity = Vector2.Zero;

                taut = false;
                return;
            }

            taut = true;
            Player.velocity = toward * speed;
            Player.pulley = false;
            Player.RefreshMovementAbilities(true);
            Player.armorEffectDrawShadow = true;

            // Down through platforms, as a grapple takes its player.
            if (Player.velocity.Y > 0f)
                Player.GoingDownWithGrapple = true;
        }

        public override void PostUpdate()
        {
            if (ReadyFor > 0)
                ReadyFor--;

            if (Wait <= 0)
                return;

            Wait--;

            if (Wait > 0)
                return;

            ReadyFor = ReadyTicks;

            if (Main.dedServ || Player.whoAmI != Main.myPlayer || Player.HeldItem.type != ModContent.ItemType<MeatHook>())
                return;

            SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.5f, Volume = 0.4f }, Player.Center);
        }
    }

    /// <summary>
    /// Beside the player, under the magazine's count (MagazineHUD): a bar that fills as the
    /// hook's wait runs out, gone once it is ready; and, while the hook is in an enemy, "x2",
    /// which is what a shot is worth then.
    /// </summary>
    public class MeatHookHUD : ModSystem
    {
        /// <summary>From the player's middle, in world pixels: under where MagazineHUD writes.</summary>
        private static readonly Vector2 Offset = new Vector2(22f, 12f);

        private const int BarWidth = 24;
        private const int BarHeight = 3;
        private const float TextScale = 0.7f;

        private static readonly Color Cold = new Color(120, 34, 28);
        private static readonly Color Hot = new Color(236, 76, 60);
        private static readonly Color Doubled = new Color(255, 96, 72);

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");

            if (index == -1)
                return;

            // Game, not UI: drawn in the world's own zoom, as MagazineHUD is.
            layers.Insert(index, new LegacyGameInterfaceLayer(
                "PisAndDag: Meat Hook",
                () =>
                {
                    Draw(Main.spriteBatch);
                    return true;
                },
                InterfaceScaleType.Game));
        }

        private static void Draw(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;

            if (player == null || !player.active || player.dead)
                return;

            Item held = player.HeldItem;

            if (held == null || held.IsAir || held.type != ModContent.ItemType<MeatHook>())
                return;

            Vector2 at = player.Center + Offset - Main.screenPosition;

            if (MeatHook_Hook.Holds(player))
            {
                Utils.DrawBorderString(spriteBatch, "x2", at - new Vector2(0f, 3f), Doubled, TextScale);
                return;
            }

            MeatHookPlayer state = player.GetModPlayer<MeatHookPlayer>();

            if (state.Wait > 0 && state.WaitFrom > 0)
                Bar(spriteBatch, at, 1f - state.Wait / (float)state.WaitFrom, 1f);
            else if (state.ReadyFor > 0)
                Bar(spriteBatch, at, 1f, state.ReadyFor / (float)MeatHookPlayer.ReadyTicks);
        }

        /// <summary>The bar, this full, this much of it showing. Full, it is the going out of it: bright, and fading.</summary>
        private static void Bar(SpriteBatch spriteBatch, Vector2 at, float full, float showing)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int x = (int)at.X;
            int y = (int)at.Y;
            int filled = (int)MathHelper.Clamp(BarWidth * full, 0f, BarWidth);

            spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 1, BarWidth + 2, BarHeight + 2), Color.Black * (0.55f * showing));

            if (filled <= 0)
                return;

            Color fill = full >= 1f ? Color.Lerp(Hot, Color.White, 0.45f) : Color.Lerp(Cold, Hot, full);
            spriteBatch.Draw(pixel, new Rectangle(x, y, filled, BarHeight), fill * showing);
        }
    }
}
