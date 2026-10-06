using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PisAndDag.Content.Combat;
using PisAndDag.Content.Particles;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace PisAndDag.Content.Projectiles.Ranged
{
    /// <summary>
    /// The Meat Hook's hook, on its chain. Thrown by the gun in the hand (MeatHookHoldOut, its
    /// mother: put the gun away and the hook is gone with it, and so it is when its wielder
    /// dies or is frozen, stoned or webbed), one at a time.
    ///
    /// OUT. It flies straight, <see cref="FlySpeed"/> a tick, with the harpoon's sound. A tile
    /// turns it back; so does being 50 tiles from its wielder (MeatHookRules.Reach).
    ///
    /// CAUGHT. The first enemy it touches, it sinks into (a hit of its own, once, with no
    /// knockback) and holds. After the bite (MeatHookRules.BiteTicks) a small one is dragged
    /// to the wielder (MeatHookNPC) and anything else drags the wielder to it (MeatHookPlayer):
    /// which, is settled the moment it bites (MeatHookRules.Hauled) and does not change. It
    /// holds on a while once what it drags is there, then comes back; sooner if what it drags
    /// is caught on something, the chain is stretched too far, what it holds dies, its wielder
    /// presses the right button again, (dragged) jumps, or is taken out of their own hands
    /// (whatever says they may use no item: a scene, being thrown). While it holds, a shot
    /// from the gun does double damage (MeatHook.Shoot asks <see cref="Holds"/>).
    ///
    /// BACK. Straight to the gun, through anything, quicker and quicker. When it is there the
    /// wait begins (MeatHookPlayer.StartWait): 3 seconds if it caught something, 5 if not.
    ///
    /// It glows red wherever it is, and lights the place. The chain is drawn from the gun to
    /// its tail, link by link: slack as it flies and as it comes back, taut while it drags.
    ///
    /// WHO DECIDES. Its wielder's machine: what it hits (the game works a player's shots out
    /// only there), when it turns back, when it lets go. Where it is in the world, which enemy
    /// it holds and which of the two is dragged are sent with it (ai), and whether it has
    /// caught anything (SendExtraAI); every machine then does the dragging for itself, from
    /// its own copy of the hook.
    /// </summary>
    public class MeatHook_Hook : ModProjectile
    {
        private const string ChainTexture = "PisAndDag/Content/Projectiles/Ranged/MeatHook_Chain";

        /// <summary>Pixels a tick it flies out at.</summary>
        public const float FlySpeed = 24f;

        /// <summary>Coming back: its first tick's speed, what each tick adds, and the most.</summary>
        private const float BackSpeed = 20f;
        private const float BackGain = 1.6f;
        private const float BackMost = 44f;

        /// <summary>From its middle back to where the chain is made fast, along the way it points (the sprite's (11, 30) from its (11, 16)).</summary>
        private const float Tail = 14f;

        /// <summary>A link's length along the chain, in pixels: the sprite's height.</summary>
        private const float LinkLength = 16f;

        /// <summary>The slack: the most a loose chain sags, how far its links wave to the side, how quickly, and how many of them along it.</summary>
        private const float SagMost = 46f;
        private const float WaveSize = 5f;
        private const float WaveSpeed = 11f;
        private const float WaveLinks = 9f;

        /// <summary>Links from the hook that take its heat.</summary>
        private const int HotLinks = 7;

        private const int Out = 0;
        private const int Caught = 1;
        private const int Back = 2;

        private const int Drags = 0;
        private const int Pulls = 1;

        private static readonly Color Red = new Color(255, 52, 36);

        /// <summary>The same red as light added to what is behind it.</summary>
        private static readonly Color Glow = new Color(255, 52, 36, 0);
        private static readonly Color Hot = new Color(255, 104, 64);
        private static readonly Color[] Sparks = { new Color(255, 214, 150), new Color(255, 132, 70), new Color(240, 60, 40) };

        private static Asset<Texture2D> chain;

        private ref float Stage => ref Projectile.ai[0];
        private ref float Target => ref Projectile.ai[1];
        private ref float Kind => ref Projectile.ai[2];

        // ---- each machine's own ------------------------------------------------------------------------

        /// <summary>The stage this machine last saw it in, and its ticks in it.</summary>
        private int seen = -1;
        private int clock;

        /// <summary>The way it points.</summary>
        private Vector2 way = Vector2.UnitX;

        /// <summary>Where in what it holds, from that one's middle.</summary>
        private Vector2 bite;

        /// <summary>
        /// What kind of thing it holds, as this machine first saw it there. An enemy's place
        /// among the enemies is given to the next one the moment it dies (a slime's young take
        /// their mother's), and the hook holds an enemy, not a place.
        /// </summary>
        private int bitten = -1;

        /// <summary>It has caught something this throw. Sent with it.</summary>
        private bool caught;

        // ---- its wielder's machine only ----------------------------------------------------------------

        /// <summary>The tick, of its holding, that what it drags got there. Less than none until then.</summary>
        private int there = -1;

        /// <summary>The least the gap has been, and the ticks since it was last less.</summary>
        private float nearest = float.MaxValue;
        private int stuck;

        private bool jumpHeld;

        /// <summary>It holds an enemy.</summary>
        public bool Latched => (int)Stage == Caught;

        /// <summary>Ticks it has held one, as this machine counts them.</summary>
        public int LatchedFor => Latched && seen == Caught ? clock : 0;

        public override void Load()
        {
            if (!Main.dedServ)
                chain = ModContent.Request<Texture2D>(ChainTexture);
        }

        public override void Unload()
        {
            chain = null;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;

            // Once into each enemy and never again: it holds the first, and hits nothing after.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        /// <summary>This player's hook, if it is out.</summary>
        public static MeatHook_Hook Of(Player player)
        {
            int type = ModContent.ProjectileType<MeatHook_Hook>();

            foreach (Projectile projectile in Main.ActiveProjectiles)
            {
                if (projectile.owner == player.whoAmI && projectile.type == type)
                    return projectile.ModProjectile as MeatHook_Hook;
            }

            return null;
        }

        /// <summary>This player's hook is in an enemy: what they shoot now does double damage.</summary>
        public static bool Holds(Player player)
        {
            MeatHook_Hook hook = Of(player);
            return hook != null && hook.Latched;
        }

        /// <summary>Its wielder lets go of what it holds.</summary>
        internal void LetGo()
        {
            if (Latched && Projectile.owner == Main.myPlayer)
                TurnBack();
        }

        private void TurnBack()
        {
            Stage = Back;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;

            // Both: netUpdate is the one for a turn made in the hook's own update, and it is
            // forgotten at the start of the next (Projectile.Update), so a turn the gun tells
            // it (LetGo, from the gun's update) would never be sent; netUpdate2 is kept until
            // it has been. One message either way.
            Projectile.netUpdate = true;
            Projectile.netUpdate2 = true;
        }

        // ---- every tick --------------------------------------------------------------------------------

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            bool mine = Projectile.owner == Main.myPlayer;

            // The gun is its mother: put away, or its wielder dead, the hook is gone with it. Its wielder
            // frozen, stoned or webbed too: the gun is out of their hands then (BaseIdleHoldoutProjectile
            // ends it, and it is made again and ended again every tick), and asked only whether there is
            // a gun the hook would be gone or not by which of the two the game updates first.
            if (!owner.active || owner.dead || owner.CCed || MeatHookHoldOut.Of(owner) == null)
            {
                if (mine)
                    Projectile.Kill();

                return;
            }

            Projectile.timeLeft = 30;

            int stage = (int)Stage;

            if (stage != seen)
            {
                seen = stage;
                clock = 0;
            }

            if (stage == Out)
                Fly(owner, mine);
            else if (stage == Caught)
                Hold(owner, mine);
            else
                Return(owner, mine);

            clock++;

            if (!Main.dedServ)
            {
                float glow = MeatHookLayer.Pulse();
                Lighting.AddLight(Projectile.Center, 0.85f * glow, 0.14f * glow, 0.08f * glow);
            }
        }

        private void Fly(Player owner, bool mine)
        {
            Projectile.tileCollide = true;

            way = Projectile.velocity.SafeNormalize(way);
            Projectile.rotation = way.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ)
            {
                // The harpoon's sound, as it leaves.
                if (clock == 0)
                    SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);

                if (clock % 2 == 0)
                {
                    PADParticleHandler.SpawnParticle(new SparkShard(Projectile.Center - way * 10f + Main.rand.NextVector2Circular(3f, 3f),
                        -way * Main.rand.NextFloat(1f, 3f), Sparks[Main.rand.Next(Sparks.Length)] * 0.8f, 7f, 12f, 16f, Main.rand.Next(7, 11), spine: false));
                }
            }

            // Not a step past its reach: it turns where the next one would take it over.
            if (mine && Vector2.Distance(Projectile.Center + Projectile.velocity, owner.Center) > MeatHookRules.Reach)
                TurnBack();
        }

        private void Hold(Player owner, bool mine)
        {
            Projectile.tileCollide = false;
            Projectile.velocity = Vector2.Zero;

            int index = (int)Target;
            NPC npc = index >= 0 && index < Main.maxNPCs ? Main.npc[index] : null;

            if (npc == null || !npc.active || npc.life <= 0 || (clock > 0 && npc.type != bitten))
            {
                if (mine)
                    TurnBack();

                return;
            }

            caught = true;

            // The first tick this machine sees it there: where it went in, kept inside what it went into.
            if (clock == 0)
            {
                bitten = npc.type;

                Vector2 off = Projectile.Center - npc.Center;
                bite = new Vector2(MathHelper.Clamp(off.X, -npc.width * 0.5f, npc.width * 0.5f), MathHelper.Clamp(off.Y, -npc.height * 0.5f, npc.height * 0.5f));
                nearest = float.MaxValue;
                stuck = 0;
                there = -1;
                jumpHeld = owner.controlJump;

                Bitten(owner, mine);
            }

            Projectile.Center = npc.Center + bite;

            Vector2 home = MeatHookHoldOut.World(owner, MeatHookHoldOut.Mount);
            way = (Projectile.Center - home).SafeNormalize(way);
            Projectile.rotation = way.ToRotation() + MathHelper.PiOver2;

            bool dragging = clock >= MeatHookRules.BiteTicks;

            if ((int)Kind == Drags)
            {
                if (npc.TryGetGlobalNPC(out MeatHookNPC hung))
                    hung.Hold(owner.whoAmI, dragging);
            }
            else if (dragging)
            {
                owner.GetModPlayer<MeatHookPlayer>().PullTo(index);
            }

            if (mine)
                Judge(owner, npc, dragging);
        }

        /// <summary>Its wielder's machine, every tick it holds: whether it still should.</summary>
        private void Judge(Player owner, NPC npc, bool dragging)
        {
            bool drags = (int)Kind == Drags;
            float gap = MeatHookRules.Gap(owner, npc, out _);
            float standoff = drags ? MeatHookRules.HaulStandoff : MeatHookRules.PullStandoff;

            if (there < 0 && dragging && gap <= standoff + 6f)
                there = clock;

            if (gap < nearest - 1f)
            {
                nearest = gap;
                stuck = 0;
            }
            else if (there < 0 && dragging)
            {
                stuck++;
            }

            bool jump = owner.controlJump;
            bool jumped = jump && !jumpHeld;
            jumpHeld = jump;

            bool held = there >= 0 && clock - there >= MeatHookRules.HoldTicks;
            bool torn = Vector2.Distance(Projectile.Center, owner.Center) > MeatHookRules.Reach * MeatHookRules.Snap;
            bool off = !drags && dragging && jumped;

            // Something has taken its wielder out of their own hands (a scene, a fall they are thrown into): the chain does not fight it.
            bool taken = owner.noItems || !PlayerLoader.CanUseItem(owner, owner.HeldItem);

            if (!held && !torn && !off && !taken && clock < MeatHookRules.LatchLimit && stuck < MeatHookRules.StuckTicks)
                return;

            // Jumped off it: what the chain gave them, they keep most of.
            if (off)
                owner.velocity *= 0.8f;

            TurnBack();
        }

        private void Return(Player owner, bool mine)
        {
            Projectile.tileCollide = false;

            Vector2 home = MeatHookHoldOut.World(owner, MeatHookHoldOut.Stow);
            Vector2 to = home - Projectile.Center;
            float left = to.Length();
            float speed = Math.Min(BackSpeed + clock * BackGain, BackMost);

            // There: its wielder's machine ends it, and the others wait at the gun for the word.
            if (left <= speed + 6f)
            {
                Projectile.velocity = Vector2.Zero;
                Projectile.Center = home;

                if (mine)
                    Projectile.Kill();

                return;
            }

            Vector2 toward = to / left;

            Projectile.velocity = toward * speed;
            way = -toward;
            Projectile.rotation = way.ToRotation() + MathHelper.PiOver2;
        }

        // ---- what it meets -----------------------------------------------------------------------------

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxCenterFrac)
        {
            // Its point, not its whole width; and through platforms.
            width = 10;
            height = 10;
            fallThrough = true;
            return true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if ((int)Stage != Out)
                return false;

            // What is left of the way to the tile, taken (the game has cut its speed to that; turned back here, it would stop short).
            Projectile.position += Projectile.velocity;

            Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);

            if (!Main.dedServ)
            {
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.25f, PitchVariance = 0.1f, Volume = 0.8f }, Projectile.Center);
                SparkShard.Spray(Projectile.Center + way * 6f, Hot, 7, 5f, 20f, 12, falls: true, palette: Sparks);
            }

            TurnBack();
            return false;
        }

        public override bool? CanDamage() => (int)Stage == Out ? null : false;

        /// <summary>
        /// Only while it flies, and only what it can sink into. Yes and not "as the game says":
        /// left to the game, nothing of a player's touches an enemy for ten ticks after a
        /// piercing shot of theirs has (NPC.immune), and a hook thrown on the heels of such a
        /// shot would go through what it was thrown at. The game still keeps it off the
        /// friendly and the spared (Projectile.Damage asks that after this).
        /// </summary>
        public override bool? CanHitNPC(NPC target)
        {
            if ((int)Stage != Out || !MeatHookRules.Latches(target))
                return false;

            return true;
        }

        public override bool CanHitPvp(Player target) => false;

        public override void ModifyDamageHitbox(ref Rectangle hitbox)
        {
            // Wider than a tick's flight is long, so that nothing in its way is stepped over.
            hitbox.Inflate(6, 6);
        }

        /// <summary>Its wielder's machine: it has gone into something.</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if ((int)Stage != Out)
                return;

            caught = true;

            // Dead of it: caught, and nothing to hold.
            if (!target.active || target.life <= 0)
            {
                TurnBack();
                return;
            }

            Stage = Caught;
            Target = target.whoAmI;
            Kind = MeatHookRules.Hauled(target) ? Drags : Pulls;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.netUpdate = true;
        }

        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(caught);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            caught = reader.ReadBoolean();
        }

        public override void OnKill(int timeLeft)
        {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active)
                return;

            // The wait is counted from here: its coming back, or its going with the gun.
            owner.GetModPlayer<MeatHookPlayer>().StartWait(caught);

            if (!Main.dedServ && (int)Stage == Back && MeatHookHoldOut.Of(owner) != null)
                SoundEngine.PlaySound(SoundID.Tink with { Pitch = 0.45f, Volume = 0.45f }, Projectile.Center);
        }

        // ---- what is seen and heard of it --------------------------------------------------------------

        /// <summary>The bite: the chain cracks taut, and what is hot on the hook comes off it.</summary>
        private void Bitten(Player owner, bool mine)
        {
            if (Main.dedServ)
                return;

            SoundEngine.PlaySound(SoundID.Item153 with { Pitch = -0.4f, PitchVariance = 0.1f, Volume = 0.6f }, Projectile.Center);

            PADParticleHandler.SpawnParticle(new BloomGeneric(Projectile.Center, Vector2.Zero, Red * 0.7f, 0.4f, 10));
            SparkShard.Spray(Projectile.Center, Hot, 9, 6f, 24f, 14, falls: true, palette: Sparks);

            if (mine)
                owner.PIS().BumpScreenShake(3.5f, 4);
        }

        /// <summary>How loose the chain hangs, none to all: a little as it flies, none once it has bitten, most as it comes back.</summary>
        private float Slack()
        {
            switch (seen)
            {
                case Caught:
                    return 0.3f * (1f - MathHelper.Clamp(clock / (float)MeatHookRules.BiteTicks, 0f, 1f));
                case Back:
                    return 0.6f;
                default:
                    return 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player owner = Main.player[Projectile.owner];
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            // The gun's end of the chain, where the gun is drawn this frame.
            Vector2 home = MeatHookHoldOut.World(owner, MeatHookHoldOut.Mount) + MeatHookHoldOut.Walk(owner);

            Chain(home, Projectile.Center - way * Tail, owner.gravDir);

            Vector2 at = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            float pulse = MeatHookLayer.Pulse();

            // The red it glows.
            Color glow = Glow * (0.5f * pulse);

            for (int i = 0; i < 8; i++)
            {
                Vector2 off = (MathHelper.TwoPi * i / 8f).ToRotationVector2() * 2.5f;
                Main.EntitySpriteDraw(texture, at + off, null, glow, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            // Lit by its own heat as much as by the place.
            Color body = new Color(Math.Max((int)lightColor.R, 176), Math.Max((int)lightColor.G, 112), Math.Max((int)lightColor.B, 96), 255);

            Main.EntitySpriteDraw(texture, at, null, body, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            return false;
        }

        /// <summary>
        /// The chain, link by link from the gun to the hook's tail. Loose, it sags the way
        /// things fall and its links wave; taut, it is a line. Each link takes the light of
        /// where it is, and the last of them the hook's heat.
        /// </summary>
        private void Chain(Vector2 from, Vector2 to, float gravity)
        {
            if (chain == null || !chain.IsLoaded)
                return;

            Vector2 span = to - from;
            float length = span.Length();

            if (length < 4f)
                return;

            Texture2D link = chain.Value;
            Vector2 origin = link.Size() * 0.5f;
            Vector2 across = new Vector2(-span.Y, span.X) / length;

            float slack = Slack();
            float sag = slack * Math.Min(SagMost, length * 0.14f) * gravity;
            float wave = slack * WaveSize;
            float time = Main.GlobalTimeWrappedHourly * WaveSpeed;

            int links = Math.Max(1, (int)Math.Round(length / LinkLength));
            Vector2 previous = from;

            for (int i = 1; i <= links; i++)
            {
                float along = i / (float)links;
                float belly = 4f * along * (1f - along);

                Vector2 point = Vector2.Lerp(from, to, along)
                    + new Vector2(0f, sag * belly)
                    + across * (wave * (float)Math.Sin(along * WaveLinks - time) * belly);

                Vector2 piece = point - previous;
                float pieceLength = piece.Length();

                if (pieceLength > 0.01f)
                {
                    Vector2 middle = (previous + point) * 0.5f;
                    Color light = Lighting.GetColor(middle.ToTileCoordinates());
                    float heat = Utils.GetLerpValue(links - HotLinks, links, i, true);
                    Color colour = Color.Lerp(light, Hot, heat * 0.6f);

                    Main.EntitySpriteDraw(link, middle - Main.screenPosition, null, colour, piece.ToRotation() + MathHelper.PiOver2, origin,
                        new Vector2(1f, pieceLength / link.Height), SpriteEffects.None, 0f);
                }

                previous = point;
            }
        }
    }
}
